namespace LeanCache.Core.Tests;

public class CacheStoreTests : IDisposable
{
    private readonly CacheStore _store = new();

    public void Dispose() => _store.Dispose();

    // ── SET / GET ──────────────────────────────────────────────

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        Assert.Null(_store.Get("nonexistent"));
    }

    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        _store.Set("key1", "hello"u8.ToArray());

        var result = _store.Get("key1");

        Assert.Equal("hello"u8.ToArray(), result);
    }

    [Fact]
    public void Set_OverwritesExistingKey()
    {
        _store.Set("key1", [1]);
        _store.Set("key1", [2]);

        Assert.Equal([2], _store.Get("key1"));
    }

    [Fact]
    public void Set_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _store.Set(null!, [1]));
    }

    [Fact]
    public void Set_NullValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _store.Set("key", null!));
    }

    // ── DELETE ─────────────────────────────────────────────────

    [Fact]
    public void Delete_ExistingKey_ReturnsTrue()
    {
        _store.Set("key1", [1]);
        Assert.True(_store.Delete("key1"));
    }

    [Fact]
    public void Delete_MissingKey_ReturnsFalse()
    {
        Assert.False(_store.Delete("nope"));
    }

    [Fact]
    public void Delete_RemovesKey()
    {
        _store.Set("key1", [1]);
        _store.Delete("key1");

        Assert.Null(_store.Get("key1"));
        Assert.Equal(0, _store.Count);
    }

    // ── EXISTS ─────────────────────────────────────────────────

    [Fact]
    public void Exists_PresentKey_ReturnsTrue()
    {
        _store.Set("key1", [1]);
        Assert.True(_store.Exists("key1"));
    }

    [Fact]
    public void Exists_MissingKey_ReturnsFalse()
    {
        Assert.False(_store.Exists("nope"));
    }

    // ── KEYS ───────────────────────────────────────────────────

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        _store.Set("a", [1]);
        _store.Set("b", [2]);
        _store.Set("c", [3]);

        var keys = _store.Keys();

        Assert.Equal(3, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.Contains("c", keys);
    }

    [Fact]
    public void Keys_WithPrefixPattern_FiltersCorrectly()
    {
        _store.Set("user:1", [1]);
        _store.Set("user:2", [2]);
        _store.Set("order:1", [3]);

        var keys = _store.Keys("user:*");

        Assert.Equal(2, keys.Count);
        Assert.Contains("user:1", keys);
        Assert.Contains("user:2", keys);
    }

    [Fact]
    public void Keys_WildcardStar_ReturnsAll()
    {
        _store.Set("a", [1]);
        _store.Set("b", [2]);

        Assert.Equal(2, _store.Keys("*").Count);
    }

    [Fact]
    public void Keys_EmptyStore_ReturnsEmpty()
    {
        Assert.Empty(_store.Keys());
    }

    // ── COUNT ──────────────────────────────────────────────────

    [Fact]
    public void Count_ReflectsInsertions()
    {
        Assert.Equal(0, _store.Count);

        _store.Set("a", [1]);
        Assert.Equal(1, _store.Count);

        _store.Set("b", [2]);
        Assert.Equal(2, _store.Count);
    }

    // ── CLEAR ──────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllKeys()
    {
        _store.Set("a", [1]);
        _store.Set("b", [2]);

        _store.Clear();

        Assert.Equal(0, _store.Count);
        Assert.Null(_store.Get("a"));
    }

    // ── STATS ──────────────────────────────────────────────────

    [Fact]
    public void GetStats_ReturnsKeyCount()
    {
        _store.Set("a", [1]);
        _store.Set("b", [2]);

        var stats = _store.GetStats();

        Assert.Equal(2, stats.KeyCount);
    }
}
