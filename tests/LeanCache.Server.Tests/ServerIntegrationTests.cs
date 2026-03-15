using System.IO.Pipelines;
using System.Net.Sockets;
using LeanCache.Core;
using LeanCache.Protocol;

namespace LeanCache.Server.Tests;

/// <summary>
/// Integration tests that spin up a real TCP server and send RESP commands.
/// </summary>
public class ServerIntegrationTests : IAsyncLifetime
{
    private LeanCacheServer _server = null!;
    private int _port;

    public async Task InitializeAsync()
    {
        // Use port 0 to get an OS-assigned free port, but our server needs
        // a known port. Pick a random high port to minimize conflicts.
        _port = Random.Shared.Next(30000, 60000);
        _server = new LeanCacheServer(_port, new CacheStore());
        await _server.StartAsync();

        // Give the listener a moment to bind
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
    }

    [Fact]
    public async Task Ping_ReturnsPong()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "PING");

        Assert.Equal(RespType.SimpleString, response!.Type);
        Assert.Equal("PONG", response.StringValue);
    }

    [Fact]
    public async Task Ping_WithMessage_ReturnsMessage()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "PING", "hello");

        Assert.Equal(RespType.BulkString, response!.Type);
        Assert.Equal("hello", response.StringValue);
    }

    [Fact]
    public async Task Echo_ReturnsMessage()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "ECHO", "world");

        Assert.Equal(RespType.BulkString, response!.Type);
        Assert.Equal("world", response.StringValue);
    }

    [Fact]
    public async Task Set_And_Get_BasicRoundTrip()
    {
        await using var client = await ConnectAsync();

        var setResult = await SendCommandAsync(client, "SET", "mykey", "myvalue");
        Assert.Equal("OK", setResult!.StringValue);

        var getResult = await SendCommandAsync(client, "GET", "mykey");
        Assert.Equal("myvalue", getResult!.StringValue);
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "GET", "nonexistent");

        Assert.True(response!.IsNull);
    }

    [Fact]
    public async Task Del_ExistingKey_ReturnsOne()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "delme", "value");

        var response = await SendCommandAsync(client, "DEL", "delme");
        Assert.Equal(1, response!.IntValue);

        var getResult = await SendCommandAsync(client, "GET", "delme");
        Assert.True(getResult!.IsNull);
    }

    [Fact]
    public async Task Del_MultipleKeys_ReturnsCount()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "k1", "v1");
        await SendCommandAsync(client, "SET", "k2", "v2");
        await SendCommandAsync(client, "SET", "k3", "v3");

        var response = await SendCommandAsync(client, "DEL", "k1", "k2", "k3", "k4");
        Assert.Equal(3, response!.IntValue); // k4 doesn't exist
    }

    [Fact]
    public async Task Exists_ExistingKey_ReturnsOne()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "existkey", "val");

        var response = await SendCommandAsync(client, "EXISTS", "existkey");
        Assert.Equal(1, response!.IntValue);
    }

    [Fact]
    public async Task Exists_MissingKey_ReturnsZero()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "EXISTS", "nope");
        Assert.Equal(0, response!.IntValue);
    }

    [Fact]
    public async Task Set_WithEx_ExpiresSetsAndTtlReturns()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "ttlkey", "val", "EX", "60");

        var ttlResponse = await SendCommandAsync(client, "TTL", "ttlkey");
        Assert.True(ttlResponse!.IntValue > 0);
        Assert.True(ttlResponse.IntValue <= 60);
    }

    [Fact]
    public async Task Ttl_NoExpiry_ReturnsMinusOne()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "persistkey", "val");

        var response = await SendCommandAsync(client, "TTL", "persistkey");
        Assert.Equal(-1, response!.IntValue);
    }

    [Fact]
    public async Task Ttl_MissingKey_ReturnsMinusTwo()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "TTL", "missing");
        Assert.Equal(-2, response!.IntValue);
    }

    [Fact]
    public async Task Expire_SetsExpiryOnKey()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "expkey", "val");

        var response = await SendCommandAsync(client, "EXPIRE", "expkey", "30");
        Assert.Equal(1, response!.IntValue);

        var ttl = await SendCommandAsync(client, "TTL", "expkey");
        Assert.True(ttl!.IntValue > 0);
    }

    [Fact]
    public async Task Persist_RemovesExpiry()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "pkey", "val", "EX", "60");

        var response = await SendCommandAsync(client, "PERSIST", "pkey");
        Assert.Equal(1, response!.IntValue);

        var ttl = await SendCommandAsync(client, "TTL", "pkey");
        Assert.Equal(-1, ttl!.IntValue);
    }

    [Fact]
    public async Task Keys_ReturnsMatchingKeys()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "user:1", "a");
        await SendCommandAsync(client, "SET", "user:2", "b");
        await SendCommandAsync(client, "SET", "order:1", "c");

        var response = await SendCommandAsync(client, "KEYS", "user:*");
        Assert.Equal(RespType.Array, response!.Type);
        Assert.Equal(2, response.ArrayValue!.Length);
    }

    [Fact]
    public async Task DbSize_ReturnsKeyCount()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "a", "1");
        await SendCommandAsync(client, "SET", "b", "2");

        var response = await SendCommandAsync(client, "DBSIZE");
        Assert.Equal(2, response!.IntValue);
    }

    [Fact]
    public async Task FlushDb_ClearsAllKeys()
    {
        await using var client = await ConnectAsync();
        await SendCommandAsync(client, "SET", "x", "1");
        await SendCommandAsync(client, "SET", "y", "2");

        var flush = await SendCommandAsync(client, "FLUSHDB");
        Assert.Equal("OK", flush!.StringValue);

        var size = await SendCommandAsync(client, "DBSIZE");
        Assert.Equal(0, size!.IntValue);
    }

    [Fact]
    public async Task Info_ReturnsServerInfo()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "INFO");

        Assert.Equal(RespType.BulkString, response!.Type);
        Assert.Contains("lean_cache_version", response.StringValue);
        Assert.Contains("uptime_in_seconds", response.StringValue);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsError()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "FOOBAR");

        Assert.Equal(RespType.Error, response!.Type);
        Assert.Contains("unknown command", response.StringValue);
    }

    [Fact]
    public async Task MultipleClients_IndependentSessions()
    {
        await using var client1 = await ConnectAsync();
        await using var client2 = await ConnectAsync();

        await SendCommandAsync(client1, "SET", "shared", "from1");
        var response = await SendCommandAsync(client2, "GET", "shared");

        Assert.Equal("from1", response!.StringValue);
    }

    [Fact]
    public async Task Command_ReturnsEmptyArray()
    {
        await using var client = await ConnectAsync();
        var response = await SendCommandAsync(client, "COMMAND");

        Assert.Equal(RespType.Array, response!.Type);
        Assert.Empty(response.ArrayValue!);
    }

    // ── Helpers ──────────────────────────────────────────────

    private async Task<TestClient> ConnectAsync()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync("127.0.0.1", _port);
        return new TestClient(socket);
    }

    private static async Task<RespValue?> SendCommandAsync(TestClient client, params string[] args)
    {
        var elements = new RespValue[args.Length];

        for (var i = 0; i < args.Length; i++)
        {
            elements[i] = RespValue.BulkString(args[i]);
        }

        await client.Writer.WriteAsync(RespValue.Array(elements));
        return await client.Reader.ReadAsync();
    }

    /// <summary>
    /// A lightweight TCP test client wrapping a socket with RESP reader/writer.
    /// </summary>
    private sealed class TestClient : IAsyncDisposable
    {
        private readonly Socket _socket;
        private readonly NetworkStream _stream;

        public TestClient(Socket socket)
        {
            _socket = socket;
            _stream = new NetworkStream(socket, ownsSocket: false);
            Reader = new RespReader(PipeReader.Create(_stream));
            Writer = new RespWriter(PipeWriter.Create(_stream));
        }

        public RespReader Reader { get; }
        public RespWriter Writer { get; }

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

            await _stream.DisposeAsync();
            _socket.Dispose();
        }
    }
}
