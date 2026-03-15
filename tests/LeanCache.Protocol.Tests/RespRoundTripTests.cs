namespace LeanCache.Protocol.Tests;

/// <summary>
/// Round-trip tests: write → read → verify identical values.
/// </summary>
public class RespRoundTripTests
{
    [Fact]
    public async Task RoundTrip_SimpleString()
    {
        var original = RespValue.SimpleString("hello");
        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal(RespType.SimpleString, parsed.Type);
        Assert.Equal("hello", parsed.StringValue);
    }

    [Fact]
    public async Task RoundTrip_Error()
    {
        var original = RespValue.Error("ERR something went wrong");
        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal(RespType.Error, parsed.Type);
        Assert.Equal("ERR something went wrong", parsed.StringValue);
    }

    [Fact]
    public async Task RoundTrip_Integer()
    {
        var original = RespValue.IntegerFrom(999999);
        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal(999999, parsed.IntValue);
    }

    [Fact]
    public async Task RoundTrip_BulkString()
    {
        var original = RespValue.BulkString("The quick brown fox");
        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal("The quick brown fox", parsed.StringValue);
    }

    [Fact]
    public async Task RoundTrip_NullBulkString()
    {
        var original = RespValue.Null;
        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.True(parsed.IsNull);
    }

    [Fact]
    public async Task RoundTrip_Array()
    {
        var original = RespValue.Array(
            RespValue.BulkString("SET"),
            RespValue.BulkString("key"),
            RespValue.BulkString("value"));

        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal(3, parsed.ArrayValue!.Length);
        Assert.Equal("SET", parsed.ArrayValue[0].StringValue);
        Assert.Equal("key", parsed.ArrayValue[1].StringValue);
        Assert.Equal("value", parsed.ArrayValue[2].StringValue);
    }

    [Fact]
    public async Task RoundTrip_NestedArray()
    {
        var original = RespValue.Array(
            RespValue.IntegerFrom(42),
            RespValue.Array(
                RespValue.BulkString("nested"),
                RespValue.Null),
            RespValue.SimpleString("OK"));

        var serialized = await TestHelpers.SerializeAsync(original);
        var parsed = await TestHelpers.ParseAsync(serialized);

        Assert.NotNull(parsed);
        Assert.Equal(3, parsed.ArrayValue!.Length);
        Assert.Equal(42, parsed.ArrayValue[0].IntValue);
        Assert.Equal(RespType.Array, parsed.ArrayValue[1].Type);
        Assert.Equal("nested", parsed.ArrayValue[1].ArrayValue![0].StringValue);
        Assert.True(parsed.ArrayValue[1].ArrayValue![1].IsNull);
        Assert.Equal("OK", parsed.ArrayValue[2].StringValue);
    }
}
