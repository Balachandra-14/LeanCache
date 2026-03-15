namespace LeanCache.Client.Tests;

public class LeanCacheClientTests
{
    [Fact]
    public void Client_CanBeCreatedWithDefaults()
    {
        using var client = new LeanCacheClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void Client_AcceptsCustomHostAndPort()
    {
        using var client = new LeanCacheClient("10.0.0.1", 9999);
        Assert.NotNull(client);
    }
}