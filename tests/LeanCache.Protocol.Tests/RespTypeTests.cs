namespace LeanCache.Protocol.Tests;

public class RespTypeTests
{
    [Fact]
    public void RespType_SimpleString_HasCorrectByteValue()
    {
        Assert.Equal((byte)'+', (byte)RespType.SimpleString);
    }

    [Fact]
    public void RespType_Error_HasCorrectByteValue()
    {
        Assert.Equal((byte)'-', (byte)RespType.Error);
    }

    [Fact]
    public void RespType_BulkString_HasCorrectByteValue()
    {
        Assert.Equal((byte)'$', (byte)RespType.BulkString);
    }

    [Fact]
    public void RespType_Array_HasCorrectByteValue()
    {
        Assert.Equal((byte)'*', (byte)RespType.Array);
    }
}