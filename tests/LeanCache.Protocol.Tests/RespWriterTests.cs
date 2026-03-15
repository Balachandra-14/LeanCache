namespace LeanCache.Protocol.Tests;

public class RespWriterTests
{
    // ── Simple Strings ────────────────────────────────────────

    [Fact]
    public async Task Write_SimpleString_OK()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.Ok);
        Assert.Equal("+OK\r\n", result);
    }

    [Fact]
    public async Task Write_SimpleString_PONG()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.Pong);
        Assert.Equal("+PONG\r\n", result);
    }

    [Fact]
    public async Task Write_SimpleString_CustomMessage()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.SimpleString("hello world"));
        Assert.Equal("+hello world\r\n", result);
    }

    // ── Errors ────────────────────────────────────────────────

    [Fact]
    public async Task Write_Error()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.Error("ERR unknown command"));
        Assert.Equal("-ERR unknown command\r\n", result);
    }

    // ── Integers ──────────────────────────────────────────────

    [Fact]
    public async Task Write_Integer_Positive()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.IntegerFrom(1000));
        Assert.Equal(":1000\r\n", result);
    }

    [Fact]
    public async Task Write_Integer_Zero()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.Zero);
        Assert.Equal(":0\r\n", result);
    }

    [Fact]
    public async Task Write_Integer_Negative()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.IntegerFrom(-42));
        Assert.Equal(":-42\r\n", result);
    }

    // ── Bulk Strings ──────────────────────────────────────────

    [Fact]
    public async Task Write_BulkString()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.BulkString("foobar"));
        Assert.Equal("$6\r\nfoobar\r\n", result);
    }

    [Fact]
    public async Task Write_BulkString_Empty()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.BulkString(""));
        Assert.Equal("$0\r\n\r\n", result);
    }

    [Fact]
    public async Task Write_BulkString_Null()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.Null);
        Assert.Equal("$-1\r\n", result);
    }

    // ── Arrays ────────────────────────────────────────────────

    [Fact]
    public async Task Write_Array_TwoBulkStrings()
    {
        var value = RespValue.Array(
            RespValue.BulkString("foo"),
            RespValue.BulkString("bar"));

        var result = await TestHelpers.SerializeAsync(value);
        Assert.Equal("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n", result);
    }

    [Fact]
    public async Task Write_Array_MixedTypes()
    {
        var value = RespValue.Array(
            RespValue.IntegerFrom(1),
            RespValue.IntegerFrom(2),
            RespValue.IntegerFrom(3));

        var result = await TestHelpers.SerializeAsync(value);
        Assert.Equal("*3\r\n:1\r\n:2\r\n:3\r\n", result);
    }

    [Fact]
    public async Task Write_Array_Empty()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.EmptyArray);
        Assert.Equal("*0\r\n", result);
    }

    [Fact]
    public async Task Write_Array_Null()
    {
        var result = await TestHelpers.SerializeAsync(RespValue.NullArray());
        Assert.Equal("*-1\r\n", result);
    }

    [Fact]
    public async Task Write_Array_Nested()
    {
        var value = RespValue.Array(
            RespValue.Array(
                RespValue.IntegerFrom(1),
                RespValue.IntegerFrom(2)),
            RespValue.Array(
                RespValue.BulkString("hello")));

        var result = await TestHelpers.SerializeAsync(value);
        Assert.Equal("*2\r\n*2\r\n:1\r\n:2\r\n*1\r\n$5\r\nhello\r\n", result);
    }

    // ── Redis command format ──────────────────────────────────

    [Fact]
    public async Task Write_SetCommand_AsRespArray()
    {
        // SET mykey myvalue — as sent by redis-cli
        var cmd = RespValue.Array(
            RespValue.BulkString("SET"),
            RespValue.BulkString("mykey"),
            RespValue.BulkString("myvalue"));

        var result = await TestHelpers.SerializeAsync(cmd);
        Assert.Equal("*3\r\n$3\r\nSET\r\n$5\r\nmykey\r\n$7\r\nmyvalue\r\n", result);
    }
}
