namespace LeanCache.Core;

/// <summary>
/// Represents a single entry in the cache with its value and metadata.
/// </summary>
public sealed class CacheEntry
{
    public byte[] Value { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset LastAccessedAt { get; private set; }

    public CacheEntry(byte[] value, TimeSpan? ttl = null)
    {
        Value = value;
        CreatedAt = DateTimeOffset.UtcNow;
        LastAccessedAt = CreatedAt;
        ExpiresAt = ttl.HasValue ? CreatedAt + ttl.Value : null;
    }

    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;

    /// <summary>
    /// Updates the last-accessed timestamp (used for future LRU eviction).
    /// </summary>
    public void Touch() => LastAccessedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Sets or updates the expiry time for this entry.
    /// </summary>
    public void SetExpiry(TimeSpan ttl) => ExpiresAt = DateTimeOffset.UtcNow + ttl;

    /// <summary>
    /// Removes the expiry so the entry lives until explicitly deleted.
    /// </summary>
    public void RemoveExpiry() => ExpiresAt = null;

    /// <summary>
    /// Returns the remaining time-to-live, or null if no expiry is set.
    /// Returns TimeSpan.Zero if already expired.
    /// </summary>
    public TimeSpan? TimeToLive
    {
        get
        {
            if (ExpiresAt is null)
            {
                return null;
            }

            var remaining = ExpiresAt.Value - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}
