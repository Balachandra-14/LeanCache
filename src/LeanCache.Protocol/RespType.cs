namespace LeanCache.Protocol;

/// <summary>
/// Defines the RESP (Redis Serialization Protocol) data types.
/// </summary>
public enum RespType : byte
{
    SimpleString = (byte)'+',
    Error = (byte)'-',
    IntegerValue = (byte)':',
    BulkString = (byte)'$',
    Array = (byte)'*',
}
