using LeanCache.Core;
using LeanCache.Protocol;

namespace LeanCache.Server.Tests;

/// <summary>
/// Unit tests for CommandHandler — tests the command logic directly without TCP.
/// </summary>
public class CommandHandlerTests : IDisposable
{
    private readonly CacheStore _store = new();
    private readonly CommandHandler _handler;

    public CommandHandlerTests()
    {
        _handler = new CommandHandler(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Ping_NullArray_ReturnsError()
    {
        var result = _handler.Execute(RespValue.SimpleString("PING"));
        Assert.Equal(RespType.Error, result.Type);
    }

    [Fact]
    public void Set_TooFewArgs_ReturnsError()
    {
        var result = _handler.Execute(MakeCommand("SET", "key"));
        Assert.Equal(RespType.Error, result.Type);
        Assert.Contains("wrong number of arguments", result.StringValue);
    }

    [Fact]
    public void Get_TooFewArgs_ReturnsError()
    {
        var result = _handler.Execute(MakeCommand("GET"));
        Assert.Equal(RespType.Error, result.Type);
    }

    [Fact]
    public void Set_WithPx_SetsMillisecondTtl()
    {
        _handler.Execute(MakeCommand("SET", "pxkey", "val", "PX", "5000"));

        var ttl = _store.GetTimeToLive("pxkey");
        Assert.NotNull(ttl);
        Assert.True(ttl.Value.TotalMilliseconds > 0);
        Assert.True(ttl.Value.TotalMilliseconds <= 5000);
    }

    [Fact]
    public void Del_TooFewArgs_ReturnsError()
    {
        var result = _handler.Execute(MakeCommand("DEL"));
        Assert.Equal(RespType.Error, result.Type);
    }

    [Fact]
    public void Exists_TooFewArgs_ReturnsError()
    {
        var result = _handler.Execute(MakeCommand("EXISTS"));
        Assert.Equal(RespType.Error, result.Type);
    }

    [Fact]
    public void Expire_InvalidSeconds_ReturnsError()
    {
        _store.Set("k", new byte[] { 1 });
        var result = _handler.Execute(MakeCommand("EXPIRE", "k", "notanumber"));
        Assert.Equal(RespType.Error, result.Type);
        Assert.Contains("not an integer", result.StringValue);
    }

    [Fact]
    public void Quit_ReturnsOk()
    {
        var result = _handler.Execute(MakeCommand("QUIT"));
        Assert.Equal("OK", result.StringValue);
    }

    [Fact]
    public void Command_CaseInsensitive()
    {
        var result = _handler.Execute(MakeCommand("ping"));
        Assert.Equal("PONG", result.StringValue);
    }

    [Fact]
    public void Info_WithSection_ReturnsFiltered()
    {
        var result = _handler.Execute(MakeCommand("INFO", "keyspace"));
        Assert.Contains("# Keyspace", result.StringValue);
        Assert.DoesNotContain("# Server", result.StringValue);
    }

    private static RespValue MakeCommand(params string[] args)
    {
        var elements = new RespValue[args.Length];

        for (var i = 0; i < args.Length; i++)
        {
            elements[i] = RespValue.BulkString(args[i]);
        }

        return RespValue.Array(elements);
    }
}
