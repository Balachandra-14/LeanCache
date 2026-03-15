using System.Collections.Concurrent;

namespace LeanCache.Core;

/// <summary>
/// Thread-safe in-memory key/value cache with TTL support and background expiry.
/// </summary>
public sealed class CacheStore : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();
    private readonly Timer _expiryTimer;
    private readonly TimeSpan _expiryInterval;
    private bool _disposed;

    /// <summary>
    /// Creates a new CacheStore with a background expiry sweep at the given interval.
    /// </summary>
    /// <param name="expiryInterval">
    /// How often the background sweep runs to remove expired keys.
    /// Defaults to 1 second.
    /// </param>
    public CacheStore(TimeSpan? expiryInterval = null)
    {
        _expiryInterval = expiryInterval ?? TimeSpan.FromSeconds(1);
        _expiryTimer = new Timer(
            callback: _ => RemoveExpiredKeys(),
            state: null,
            dueTime: _expiryInterval,
            period: _expiryInterval);
    }

    /// <summary>Number of keys currently in the store (including not-yet-swept expired keys).</summary>
    public int Count => _store.Count;

    /// <summary>
    /// Sets a key to the given value with an optional TTL.
    /// Overwrites any existing entry.
    /// </summary>
    public void Set(string key, byte[] value, TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        var entry = new CacheEntry(value, ttl);
        _store[key] = entry;
    }

    /// <summary>
    /// Gets the value for a key. Returns null if the key doesn't exist or is expired.
    /// Performs passive (lazy) expiry: removes the key if it has expired.
    /// </summary>
    public byte[]? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_store.TryGetValue(key, out var entry))
        {
            return null;
        }

        // Passive expiry: evict on read if expired
        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return null;
        }

        entry.Touch();
        return entry.Value;
    }

    /// <summary>
    /// Deletes a key from the cache. Returns true if the key existed.
    /// </summary>
    public bool Delete(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _store.TryRemove(key, out _);
    }

    /// <summary>
    /// Checks whether a key exists and is not expired.
    /// Performs passive expiry on expired keys.
    /// </summary>
    public bool Exists(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_store.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.IsExpired)
        {
            _store.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns all non-expired keys matching an optional pattern.
    /// A null or "*" pattern returns all keys.
    /// Supports simple glob: "user:*" matches keys starting with "user:".
    /// </summary>
    public IReadOnlyList<string> Keys(string? pattern = null)
    {
        var allKeys = _store.Keys;
        var result = new List<string>();

        foreach (var key in allKeys)
        {
            if (_store.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                if (MatchesPattern(key, pattern))
                {
                    result.Add(key);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Sets an expiry on an existing key. Returns true if the key exists and the expiry was set.
    /// </summary>
    public bool Expire(string key, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_store.TryGetValue(key, out var entry) || entry.IsExpired)
        {
            return false;
        }

        entry.SetExpiry(ttl);
        return true;
    }

    /// <summary>
    /// Removes the expiry from an existing key, making it persist until explicitly deleted.
    /// Returns true if the key exists.
    /// </summary>
    public bool Persist(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_store.TryGetValue(key, out var entry) || entry.IsExpired)
        {
            return false;
        }

        entry.RemoveExpiry();
        return true;
    }

    /// <summary>
    /// Returns the remaining TTL for a key, or null if the key has no expiry.
    /// Returns TimeSpan.Zero if expired. Returns null if the key doesn't exist.
    /// </summary>
    public TimeSpan? GetTimeToLive(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_store.TryGetValue(key, out var entry))
        {
            return null;
        }

        return entry.TimeToLive;
    }

    /// <summary>
    /// Removes all keys from the cache.
    /// </summary>
    public void Clear() => _store.Clear();

    /// <summary>
    /// Returns a snapshot of cache statistics.
    /// </summary>
    public CacheStats GetStats() => new(
        KeyCount: _store.Count,
        ExpiredKeysRemoved: _expiredKeysRemoved);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _expiryTimer.Dispose();
    }

    #region Internal helpers

    private long _expiredKeysRemoved;

    /// <summary>
    /// Active expiry: sweep all keys and remove expired ones.
    /// Called periodically by the background timer.
    /// </summary>
    internal int RemoveExpiredKeys()
    {
        var removed = 0;

        foreach (var key in _store.Keys)
        {
            if (_store.TryGetValue(key, out var entry) && entry.IsExpired)
            {
                if (_store.TryRemove(key, out _))
                {
                    removed++;
                }
            }
        }

        Interlocked.Add(ref _expiredKeysRemoved, removed);
        return removed;
    }

    private static bool MatchesPattern(string key, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
        {
            return true;
        }

        // "prefix*" — starts-with matching
        if (pattern.EndsWith('*') && !pattern.AsSpan(0, pattern.Length - 1).Contains('*'))
        {
            return key.StartsWith(pattern[..^1], StringComparison.Ordinal);
        }

        // Exact match fallback
        return string.Equals(key, pattern, StringComparison.Ordinal);
    }

    #endregion
}

/// <summary>
/// A snapshot of cache statistics.
/// </summary>
public sealed record CacheStats(int KeyCount, long ExpiredKeysRemoved);
