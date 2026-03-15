namespace LeanCache.Client;

/// <summary>
/// Client for connecting to a LeanCache server.
/// </summary>
public sealed class LeanCacheClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;

    public LeanCacheClient(string host = "localhost", int port = 6379)
    {
        _host = host;
        _port = port;
    }

    public void Dispose()
    {
        // Connection cleanup will be added in Phase 9.
    }
}
