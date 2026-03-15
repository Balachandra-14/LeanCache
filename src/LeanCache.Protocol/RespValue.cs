namespace LeanCache.Protocol;

/// <summary>
/// Represents a parsed RESP value. This is the core data type for the protocol layer.
/// </summary>
public sealed class RespValue
{
    public RespType Type { get; }

    /// <summary>The string content for SimpleString, Error, and BulkString types.</summary>
    public string? StringValue { get; }

    /// <summary>The integer content for IntegerValue type.</summary>
    public long IntValue { get; }

    /// <summary>The array elements for Array type.</summary>
    public RespValue[]? ArrayValue { get; }

    /// <summary>True if this is a null bulk string ($-1).</summary>
    public bool IsNull { get; }

    private RespValue(RespType type, string? stringValue = null, long intValue = 0,
                      RespValue[]? arrayValue = null, bool isNull = false)
    {
        Type = type;
        StringValue = stringValue;
        IntValue = intValue;
        ArrayValue = arrayValue;
        IsNull = isNull;
    }

    // ── Factory methods ───────────────────────────────────────

    public static RespValue SimpleString(string value) =>
        new(RespType.SimpleString, stringValue: value);

    public static RespValue Error(string message) =>
        new(RespType.Error, stringValue: message);

    public static RespValue IntegerFrom(long value) =>
        new(RespType.IntegerValue, intValue: value);

    public static RespValue BulkString(string value) =>
        new(RespType.BulkString, stringValue: value);

    public static RespValue NullBulkString() =>
        new(RespType.BulkString, isNull: true);

    public static RespValue Array(params RespValue[] elements) =>
        new(RespType.Array, arrayValue: elements);

    public static RespValue NullArray() =>
        new(RespType.Array, isNull: true);

    // ── Common constants ──────────────────────────────────────

    public static readonly RespValue Ok = SimpleString("OK");
    public static readonly RespValue Pong = SimpleString("PONG");
    public static readonly RespValue Null = NullBulkString();
    public static readonly RespValue EmptyArray = Array();
    public static readonly RespValue Zero = IntegerFrom(0);
    public static readonly RespValue One = IntegerFrom(1);
}
