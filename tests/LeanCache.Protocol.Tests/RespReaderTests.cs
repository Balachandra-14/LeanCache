namespace LeanCache.Protocol.Tests;

public class RespReaderTests
{
    // ── Simple Strings ────────────────────────────────────────

    [Fact]
    public async Task Read_SimpleString_OK()
    {
        var value = await TestHelpers.ParseAsync("+OK\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.SimpleString, value.Type);
        Assert.Equal("OK", value.StringValue);
    }

    [Fact]
    public async Task Read_SimpleString_PONG()
    {
        var value = await TestHelpers.ParseAsync("+PONG\r\n");

        Assert.NotNull(value);
        Assert.Equal("PONG", value.StringValue);
    }

    [Fact]
    public async Task Read_SimpleString_WithSpaces()
    {
        var value = await TestHelpers.ParseAsync("+hello world\r\n");

        Assert.NotNull(value);
        Assert.Equal("hello world", value.StringValue);
    }

    // ── Errors ────────────────────────────────────────────────

    [Fact]
    public async Task Read_Error()
    {
        var value = await TestHelpers.ParseAsync("-ERR unknown command 'foobar'\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.Error, value.Type);
        Assert.Equal("ERR unknown command 'foobar'", value.StringValue);
    }

    [Fact]
    public async Task Read_Error_WrongType()
    {
        var value = await TestHelpers.ParseAsync("-WRONGTYPE Operation against a key\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.Error, value.Type);
        Assert.Equal("WRONGTYPE Operation against a key", value.StringValue);
    }

    // ── Integers ──────────────────────────────────────────────

    [Fact]
    public async Task Read_Integer_Positive()
    {
        var value = await TestHelpers.ParseAsync(":1000\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.IntegerValue, value.Type);
        Assert.Equal(1000, value.IntValue);
    }

    [Fact]
    public async Task Read_Integer_Zero()
    {
        var value = await TestHelpers.ParseAsync(":0\r\n");

        Assert.NotNull(value);
        Assert.Equal(0, value.IntValue);
    }

    [Fact]
    public async Task Read_Integer_Negative()
    {
        var value = await TestHelpers.ParseAsync(":-42\r\n");

        Assert.NotNull(value);
        Assert.Equal(-42, value.IntValue);
    }

    // ── Bulk Strings ──────────────────────────────────────────

    [Fact]
    public async Task Read_BulkString()
    {
        var value = await TestHelpers.ParseAsync("$6\r\nfoobar\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.BulkString, value.Type);
        Assert.Equal("foobar", value.StringValue);
        Assert.False(value.IsNull);
    }

    [Fact]
    public async Task Read_BulkString_Empty()
    {
        var value = await TestHelpers.ParseAsync("$0\r\n\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.BulkString, value.Type);
        Assert.Equal("", value.StringValue);
    }

    [Fact]
    public async Task Read_BulkString_Null()
    {
        var value = await TestHelpers.ParseAsync("$-1\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.BulkString, value.Type);
        Assert.True(value.IsNull);
    }

    // ── Arrays ────────────────────────────────────────────────

    [Fact]
    public async Task Read_Array_TwoBulkStrings()
    {
        var value = await TestHelpers.ParseAsync("*2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.Array, value.Type);
        Assert.NotNull(value.ArrayValue);
        Assert.Equal(2, value.ArrayValue.Length);
        Assert.Equal("foo", value.ArrayValue[0].StringValue);
        Assert.Equal("bar", value.ArrayValue[1].StringValue);
    }

    [Fact]
    public async Task Read_Array_ThreeIntegers()
    {
        var value = await TestHelpers.ParseAsync("*3\r\n:1\r\n:2\r\n:3\r\n");

        Assert.NotNull(value);
        Assert.Equal(3, value.ArrayValue!.Length);
        Assert.Equal(1, value.ArrayValue[0].IntValue);
        Assert.Equal(2, value.ArrayValue[1].IntValue);
        Assert.Equal(3, value.ArrayValue[2].IntValue);
    }

    [Fact]
    public async Task Read_Array_Empty()
    {
        var value = await TestHelpers.ParseAsync("*0\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.Array, value.Type);
        Assert.NotNull(value.ArrayValue);
        Assert.Empty(value.ArrayValue);
    }

    [Fact]
    public async Task Read_Array_Null()
    {
        var value = await TestHelpers.ParseAsync("*-1\r\n");

        Assert.NotNull(value);
        Assert.Equal(RespType.Array, value.Type);
        Assert.True(value.IsNull);
    }

    [Fact]
    public async Task Read_Array_MixedTypes()
    {
        // [1, "two", nil]
        var value = await TestHelpers.ParseAsync("*3\r\n:1\r\n$3\r\ntwo\r\n$-1\r\n");

        Assert.NotNull(value);
        Assert.Equal(3, value.ArrayValue!.Length);
        Assert.Equal(RespType.IntegerValue, value.ArrayValue[0].Type);
        Assert.Equal(1, value.ArrayValue[0].IntValue);
        Assert.Equal("two", value.ArrayValue[1].StringValue);
        Assert.True(value.ArrayValue[2].IsNull);
    }

    [Fact]
    public async Task Read_Array_Nested()
    {
        var value = await TestHelpers.ParseAsync("*2\r\n*2\r\n:1\r\n:2\r\n*1\r\n$5\r\nhello\r\n");

        Assert.NotNull(value);
        Assert.Equal(2, value.ArrayValue!.Length);

        // First element: [1, 2]
        Assert.Equal(RespType.Array, value.ArrayValue[0].Type);
        Assert.Equal(2, value.ArrayValue[0].ArrayValue!.Length);
        Assert.Equal(1, value.ArrayValue[0].ArrayValue![0].IntValue);
        Assert.Equal(2, value.ArrayValue[0].ArrayValue![1].IntValue);

        // Second element: ["hello"]
        Assert.Equal(RespType.Array, value.ArrayValue[1].Type);
        Assert.Equal("hello", value.ArrayValue[1].ArrayValue![0].StringValue);
    }

    // ── Real Redis commands ───────────────────────────────────

    [Fact]
    public async Task Read_SetCommand()
    {
        // SET mykey myvalue — as a client would send
        var value = await TestHelpers.ParseAsync("*3\r\n$3\r\nSET\r\n$5\r\nmykey\r\n$7\r\nmyvalue\r\n");

        Assert.NotNull(value);
        Assert.Equal(3, value.ArrayValue!.Length);
        Assert.Equal("SET", value.ArrayValue[0].StringValue);
        Assert.Equal("mykey", value.ArrayValue[1].StringValue);
        Assert.Equal("myvalue", value.ArrayValue[2].StringValue);
    }

    [Fact]
    public async Task Read_GetCommand()
    {
        var value = await TestHelpers.ParseAsync("*2\r\n$3\r\nGET\r\n$5\r\nmykey\r\n");

        Assert.NotNull(value);
        Assert.Equal(2, value.ArrayValue!.Length);
        Assert.Equal("GET", value.ArrayValue[0].StringValue);
        Assert.Equal("mykey", value.ArrayValue[1].StringValue);
    }

    // ── Connection closed ─────────────────────────────────────

    [Fact]
    public async Task Read_EmptyStream_ReturnsNull()
    {
        var value = await TestHelpers.ParseAsync("");
        Assert.Null(value);
    }

    // ── Error handling ────────────────────────────────────────

    [Fact]
    public async Task Read_InvalidTypeByte_ThrowsProtocolException()
    {
        await Assert.ThrowsAsync<ProtocolException>(async () =>
            await TestHelpers.ParseAsync("~invalid\r\n"));
    }
}
