using System.Globalization;
using System.IO.Pipelines;
using System.Text;

namespace LeanCache.Protocol;

/// <summary>
/// Writes RESP-encoded values to a PipeWriter.
/// </summary>
public sealed class RespWriter
{
    private static readonly byte[] Crlf = "\r\n"u8.ToArray();

    private readonly PipeWriter _writer;

    public RespWriter(PipeWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    /// Serializes a RespValue and flushes the writer.
    /// </summary>
    public async ValueTask WriteAsync(RespValue value, CancellationToken ct = default)
    {
        Write(value);
        await _writer.FlushAsync(ct);
    }

    /// <summary>
    /// Serializes a RespValue into the pipe buffer without flushing.
    /// Call FlushAsync on the PipeWriter when ready to send.
    /// </summary>
    public void Write(RespValue value)
    {
        switch (value.Type)
        {
            case RespType.SimpleString:
                WriteSimpleString(value.StringValue!);
                break;
            case RespType.Error:
                WriteError(value.StringValue!);
                break;
            case RespType.IntegerValue:
                WriteInteger(value.IntValue);
                break;
            case RespType.BulkString:
                WriteBulkString(value);
                break;
            case RespType.Array:
                WriteArray(value);
                break;
            default:
                throw new InvalidOperationException($"Unknown RESP type: {value.Type}");
        }
    }

    // +OK\r\n
    private void WriteSimpleString(string value)
    {
        WritePrefix(RespType.SimpleString);
        WriteUtf8(value);
        WriteCrlf();
    }

    // -ERR message\r\n
    private void WriteError(string message)
    {
        WritePrefix(RespType.Error);
        WriteUtf8(message);
        WriteCrlf();
    }

    // :1000\r\n
    private void WriteInteger(long value)
    {
        WritePrefix(RespType.IntegerValue);
        WriteUtf8(value.ToString(CultureInfo.InvariantCulture));
        WriteCrlf();
    }

    // $6\r\nfoobar\r\n  or  $-1\r\n (null)
    private void WriteBulkString(RespValue value)
    {
        WritePrefix(RespType.BulkString);

        if (value.IsNull)
        {
            WriteUtf8("-1");
            WriteCrlf();
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value.StringValue!);
        WriteUtf8(bytes.Length.ToString(CultureInfo.InvariantCulture));
        WriteCrlf();
        WriteRaw(bytes);
        WriteCrlf();
    }

    // *2\r\n$3\r\nfoo\r\n$3\r\nbar\r\n  or  *-1\r\n (null)
    private void WriteArray(RespValue value)
    {
        WritePrefix(RespType.Array);

        if (value.IsNull)
        {
            WriteUtf8("-1");
            WriteCrlf();
            return;
        }

        var elements = value.ArrayValue!;
        WriteUtf8(elements.Length.ToString(CultureInfo.InvariantCulture));
        WriteCrlf();

        foreach (var element in elements)
        {
            Write(element);
        }
    }

    private void WritePrefix(RespType type)
    {
        var span = _writer.GetSpan(1);
        span[0] = (byte)type;
        _writer.Advance(1);
    }

    private void WriteUtf8(string text)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var span = _writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(text, span);
        _writer.Advance(byteCount);
    }

    private void WriteRaw(byte[] data)
    {
        var span = _writer.GetSpan(data.Length);
        data.CopyTo(span);
        _writer.Advance(data.Length);
    }

    private void WriteCrlf()
    {
        var span = _writer.GetSpan(2);
        Crlf.CopyTo(span);
        _writer.Advance(2);
    }
}
