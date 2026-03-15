using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LeanCache.Core;

namespace LeanCache.Server;

/// <summary>
/// Async TCP server that listens for RESP-speaking clients and dispatches
/// commands to the cache engine.
/// </summary>
public sealed class LeanCacheServer : IAsyncDisposable
{
    private readonly int _port;
    private readonly CacheStore _store;
    private readonly CommandHandler _handler;
    private readonly ConcurrentDictionary<string, Task> _connections = new();

    private Socket? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private int _connectionCounter;

    public LeanCacheServer(int port, CacheStore? store = null)
    {
        _port = port;
        _store = store ?? new CacheStore();
        _handler = new CommandHandler(_store);
    }

    /// <summary>The port the server is listening on.</summary>
    public int Port => _port;

    /// <summary>Number of currently active client connections.</summary>
    public int ActiveConnections => _connections.Count;

    /// <summary>
    /// Starts the TCP listener and begins accepting connections.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(new IPEndPoint(IPAddress.Any, _port));
        _listener.Listen(128);

        _acceptLoop = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server and waits for all connections to drain.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        _listener?.Dispose();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Wait for all active connections to finish
        await Task.WhenAll(_connections.Values);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _store.Dispose();
        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener!.AcceptAsync(ct);
                var connectionId = $"conn-{Interlocked.Increment(ref _connectionCounter)}";

                var connectionTask = HandleConnectionAsync(clientSocket, connectionId, ct);
                _connections[connectionId] = connectionTask;

                // Clean up completed connection from the dictionary when done
                _ = connectionTask.ContinueWith(
                    t => _connections.TryRemove(connectionId, out _),
                    TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break; // Listener was disposed (shutdown)
            }
        }
    }

    private async Task HandleConnectionAsync(Socket socket, string id, CancellationToken ct)
    {
        await using var connection = new ClientConnection(socket, _handler, id);

        try
        {
            await connection.RunAsync(ct);
        }
        catch (Exception)
        {
            // Log in the future; for now silently close
        }
    }
}
