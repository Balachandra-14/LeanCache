using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace LeanCache.Protocol.Tests;

/// <summary>
/// Helper to create pipe-based reader/writer pairs for testing.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Writes raw RESP bytes into a pipe and returns a RespReader to parse them.
    /// </summary>
    public static async Task<RespValue?> ParseAsync(string rawResp)
    {
        var pipe = new Pipe();
        var bytes = Encoding.UTF8.GetBytes(rawResp);

        await pipe.Writer.WriteAsync(bytes);
        await pipe.Writer.CompleteAsync();

        var reader = new RespReader(pipe.Reader);
        var result = await reader.ReadAsync();

        await pipe.Reader.CompleteAsync();
        return result;
    }

    /// <summary>
    /// Serializes a RespValue and returns the raw bytes as a string.
    /// </summary>
    public static async Task<string> SerializeAsync(RespValue value)
    {
        var pipe = new Pipe();
        var writer = new RespWriter(pipe.Writer);

        await writer.WriteAsync(value);
        await pipe.Writer.CompleteAsync();

        var result = await pipe.Reader.ReadAsync();
        var text = Encoding.UTF8.GetString(result.Buffer);
        await pipe.Reader.CompleteAsync();
        return text;
    }
}
