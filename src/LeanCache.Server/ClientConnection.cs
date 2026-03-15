using System.IO.Pipelines;
using System.Net.Sockets;
using LeanCache.Protocol;

namespace LeanCache.Server;

/// <summary>
/// Manages a single client connection: reads RESP commands, dispatches them,
/// and writes responses back.
/// </summary>
internal sealed class ClientConnection : IAsyncDisposable
{
    private readonly Socket _socket;
    private readonly CommandHandler _handler;
    private readonly string _id;

    public ClientConnection(Socket socket, CommandHandler handler, string id)
    {
        _socket = socket;
        _handler = handler;
        _id = id;
    }

    public string Id => _id;

    /// <summary>
    /// Runs the read-dispatch-write loop until the client disconnects or an error occurs.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        await using var stream = new NetworkStream(_socket, ownsSocket: false);
        var reader = new RespReader(PipeReader.Create(stream));
        var writer = new RespWriter(PipeWriter.Create(stream));

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var request = await reader.ReadAsync(ct);

                // null means the client closed the connection
                if (request is null)
                {
                    break;
                }

                var response = _handler.Execute(request);
                await writer.WriteAsync(response, ct);

                // QUIT command — send OK (already done above) and disconnect
                if (IsQuitCommand(request))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutting down — expected
        }
        catch (IOException)
        {
            // Client disconnected abruptly — expected
        }
        catch (SocketException)
        {
            // Connection reset — expected
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
            // Already disconnected
        }

        _socket.Dispose();
        await ValueTask.CompletedTask;
    }

    private static bool IsQuitCommand(RespValue request)
    {
        if (request.Type != RespType.Array || request.ArrayValue is null || request.ArrayValue.Length == 0)
        {
            return false;
        }

        var cmd = request.ArrayValue[0].StringValue;
        return string.Equals(cmd, "QUIT", StringComparison.OrdinalIgnoreCase);
    }
}
