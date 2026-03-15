namespace LeanCache.Core.Tests;

public class CacheStoreTtlTests : IDisposable
{
    // Use a long sweep interval so active expiry doesn't interfere with tests.
    // We test passive (lazy) expiry behavior directly.
    private readonly CacheStore _store = new(expiryInterval: TimeSpan.FromHours(1));

    public void Dispose() => _store.Dispose();

    // ── PASSIVE (LAZY) EXPIRY ─────────────────────────────────

    [Fact]
    public void Get_ExpiredKey_ReturnsNull()
    {
        _store.Set("key1", [1], ttl: TimeSpan.Zero);

        Assert.Null(_store.Get("key1"));
    }

    [Fact]
    public void Exists_ExpiredKey_ReturnsFalse()
    {
        _store.Set("key1", [1], ttl: TimeSpan.Zero);

        Assert.False(_store.Exists("key1"));
    }

    [Fact]
    public void Get_ExpiredKey_RemovesFromStore()
    {
        _store.Set("key1", [1], ttl: TimeSpan.Zero);

        _ = _store.Get("key1"); // triggers passive expiry

        Assert.Equal(0, _store.Count);
    }

    [Fact]
    public void Keys_ExcludesExpiredKeys()
    {
        _store.Set("alive", [1], ttl: TimeSpan.FromHours(1));
        _store.Set("dead", [2], ttl: TimeSpan.Zero);

        var keys = _store.Keys();

        Assert.Single(keys);
        Assert.Equal("alive", keys[0]);
    }

    [Fact]
    public void Get_NonExpiredKey_ReturnsValue()
    {
        _store.Set("key1", [42], ttl: TimeSpan.FromHours(1));

        Assert.Equal([42], _store.Get("key1"));
    }

    // ── EXPIRE / PERSIST / TTL ────────────────────────────────

    [Fact]
    public void Expire_SetsExpiryOnExistingKey()
    {
        _store.Set("key1", [1]);

        var result = _store.Expire("key1", TimeSpan.FromMinutes(5));

        Assert.True(result);
        Assert.NotNull(_store.GetTimeToLive("key1"));
    }

    [Fact]
    public void Expire_MissingKey_ReturnsFalse()
    {
        Assert.False(_store.Expire("nope", TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Persist_RemovesExpiry()
    {
        _store.Set("key1", [1], ttl: TimeSpan.FromMinutes(5));

        var result = _store.Persist("key1");

        Assert.True(result);
        Assert.Null(_store.GetTimeToLive("key1"));
    }

    [Fact]
    public void Persist_MissingKey_ReturnsFalse()
    {
        Assert.False(_store.Persist("nope"));
    }

    [Fact]
    public void GetTimeToLive_NoExpiry_ReturnsNull()
    {
        _store.Set("key1", [1]);

        Assert.Null(_store.GetTimeToLive("key1"));
    }

    [Fact]
    public void GetTimeToLive_WithExpiry_ReturnsPositive()
    {
        _store.Set("key1", [1], ttl: TimeSpan.FromMinutes(5));

        var ttl = _store.GetTimeToLive("key1");

        Assert.NotNull(ttl);
        Assert.True(ttl.Value > TimeSpan.Zero);
        Assert.True(ttl.Value <= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void GetTimeToLive_Expired_ReturnsZero()
    {
        _store.Set("key1", [1], ttl: TimeSpan.Zero);

        var ttl = _store.GetTimeToLive("key1");

        Assert.NotNull(ttl);
        Assert.Equal(TimeSpan.Zero, ttl.Value);
    }

    [Fact]
    public void GetTimeToLive_MissingKey_ReturnsNull()
    {
        Assert.Null(_store.GetTimeToLive("nope"));
    }

    // ── ACTIVE EXPIRY (BACKGROUND SWEEP) ──────────────────────

    [Fact]
    public void RemoveExpiredKeys_CleansUpExpiredEntries()
    {
        _store.Set("alive", [1], ttl: TimeSpan.FromHours(1));
        _store.Set("dead1", [2], ttl: TimeSpan.Zero);
        _store.Set("dead2", [3], ttl: TimeSpan.Zero);

        var removed = _store.RemoveExpiredKeys();

        Assert.Equal(2, removed);
        Assert.Equal(1, _store.Count);
        Assert.NotNull(_store.Get("alive"));
    }

    [Fact]
    public void RemoveExpiredKeys_NoExpired_ReturnsZero()
    {
        _store.Set("a", [1]);
        _store.Set("b", [2]);

        Assert.Equal(0, _store.RemoveExpiredKeys());
    }

    [Fact]
    public void ActiveExpiry_TracksRemovedCount()
    {
        _store.Set("dead", [1], ttl: TimeSpan.Zero);
        _store.RemoveExpiredKeys();

        var stats = _store.GetStats();

        Assert.Equal(1, stats.ExpiredKeysRemoved);
    }

    [Fact]
    public async Task BackgroundTimer_EventuallyRemovesExpiredKeys()
    {
        // Use a fast sweep interval
        using var fastStore = new CacheStore(expiryInterval: TimeSpan.FromMilliseconds(50));
        fastStore.Set("temp", [1], ttl: TimeSpan.Zero);

        // Wait for the background sweep to kick in
        await Task.Delay(300);

        Assert.Equal(0, fastStore.Count);
    }
}
