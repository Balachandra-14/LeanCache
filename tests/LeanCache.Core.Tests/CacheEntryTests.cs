namespace LeanCache.Core.Tests;

public class CacheEntryTests
{
    [Fact]
    public void NewEntry_WithoutTtl_IsNotExpired()
    {
        var entry = new CacheEntry([1, 2, 3]);

        Assert.False(entry.IsExpired);
        Assert.Null(entry.ExpiresAt);
    }

    [Fact]
    public void NewEntry_PreservesValue()
    {
        byte[] data = [0xCA, 0xFE];
        var entry = new CacheEntry(data);

        Assert.Equal(data, entry.Value);
    }

    [Fact]
    public void NewEntry_WithTtl_SetsExpiry()
    {
        var entry = new CacheEntry([1], ttl: TimeSpan.FromMinutes(5));

        Assert.NotNull(entry.ExpiresAt);
        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void Entry_WithZeroTtl_IsExpiredImmediately()
    {
        var entry = new CacheEntry([1], ttl: TimeSpan.Zero);

        Assert.True(entry.IsExpired);
    }
}