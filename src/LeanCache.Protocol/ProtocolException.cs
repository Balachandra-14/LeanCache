namespace LeanCache.Protocol;

/// <summary>
/// Thrown when the RESP protocol stream contains invalid data.
/// </summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
    public ProtocolException(string message, Exception innerException) : base(message, innerException) { }
}
