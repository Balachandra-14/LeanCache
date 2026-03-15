using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace LeanCache.Protocol;

/// <summary>
/// Reads and parses RESP-encoded values from a PipeReader.
/// </summary>
public sealed class RespReader
{
    private readonly PipeReader _reader;

    public RespReader(PipeReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Reads the next complete RESP value from the pipe.
    /// Returns null if the connection is closed (no more data).
    /// </summary>
    public async ValueTask<RespValue?> ReadAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var result = await _reader.ReadAsync(ct);
            var buffer = result.Buffer;

            if (TryParse(ref buffer, out var value))
            {
                _reader.AdvanceTo(buffer.Start);
                return value;
            }

            // Not enough data yet — tell the pipe what we examined.
            _reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                return null; // Connection closed.
            }
        }
    }

    /// <summary>
    /// Attempts to parse a complete RESP value from the buffer.
    /// Advances the buffer past the consumed data on success.
    /// </summary>
    internal static bool TryParse(ref ReadOnlySequence<byte> buffer, out RespValue? value)
    {
        value = null;
        var reader = new SequenceReader<byte>(buffer);

        if (!TryParseValue(ref reader, out value))
        {
            return false;
        }

        buffer = buffer.Slice(reader.Position);
        return true;
    }

    private static bool TryParseValue(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!reader.TryRead(out var typeByte))
        {
            return false;
        }

        var type = (RespType)typeByte;

        return type switch
        {
            RespType.SimpleString => TryParseSimpleString(ref reader, out value),
            RespType.Error => TryParseError(ref reader, out value),
            RespType.IntegerValue => TryParseInteger(ref reader, out value),
            RespType.BulkString => TryParseBulkString(ref reader, out value),
            RespType.Array => TryParseArray(ref reader, out value),
            _ => throw new ProtocolException($"Unknown RESP type byte: 0x{typeByte:X2}"),
        };
    }

    // +OK\r\n
    private static bool TryParseSimpleString(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!TryReadLine(ref reader, out var line))
        {
            return false;
        }

        value = RespValue.SimpleString(line);
        return true;
    }

    // -ERR message\r\n
    private static bool TryParseError(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!TryReadLine(ref reader, out var line))
        {
            return false;
        }

        value = RespValue.Error(line);
        return true;
    }

    // :1000\r\n
    private static bool TryParseInteger(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!TryReadLine(ref reader, out var line))
        {
            return false;
        }

        if (!long.TryParse(line, out var intValue))
        {
            throw new ProtocolException($"Invalid RESP integer: '{line}'");
        }

        value = RespValue.IntegerFrom(intValue);
        return true;
    }

    // $6\r\nfoobar\r\n  or  $-1\r\n (null)
    private static bool TryParseBulkString(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!TryReadLine(ref reader, out var lengthStr))
        {
            return false;
        }

        if (!int.TryParse(lengthStr, out var length))
        {
            throw new ProtocolException($"Invalid bulk string length: '{lengthStr}'");
        }

        // Null bulk string
        if (length < 0)
        {
            value = RespValue.NullBulkString();
            return true;
        }

        // Need length bytes + 2 (\r\n)
        if (reader.Remaining < length + 2)
        {
            return false;
        }

        var payload = new byte[length];
        reader.TryCopyTo(payload);
        reader.Advance(length);

        // Skip trailing \r\n
        if (!reader.TryRead(out var cr) || !reader.TryRead(out var lf) || cr != '\r' || lf != '\n')
        {
            throw new ProtocolException("Bulk string not terminated with \\r\\n");
        }

        value = RespValue.BulkString(Encoding.UTF8.GetString(payload));
        return true;
    }

    // *2\r\n...\r\n  or  *-1\r\n (null)
    private static bool TryParseArray(ref SequenceReader<byte> reader, out RespValue? value)
    {
        value = null;

        if (!TryReadLine(ref reader, out var countStr))
        {
            return false;
        }

        if (!int.TryParse(countStr, out var count))
        {
            throw new ProtocolException($"Invalid array count: '{countStr}'");
        }

        // Null array
        if (count < 0)
        {
            value = RespValue.NullArray();
            return true;
        }

        var elements = new RespValue[count];

        for (var i = 0; i < count; i++)
        {
            if (!TryParseValue(ref reader, out var element))
            {
                return false; // Need more data
            }

            elements[i] = element!;
        }

        value = RespValue.Array(elements);
        return true;
    }

    /// <summary>
    /// Reads a line terminated by \r\n and returns the content without the terminator.
    /// </summary>
    private static bool TryReadLine(ref SequenceReader<byte> reader, out string line)
    {
        line = string.Empty;

        if (!reader.TryReadTo(out ReadOnlySequence<byte> lineBytes, "\r\n"u8))
        {
            return false;
        }

        line = Encoding.UTF8.GetString(lineBytes);
        return true;
    }
}
