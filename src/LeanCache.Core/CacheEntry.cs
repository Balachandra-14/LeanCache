namespace LeanCache.Core;

/// <summary>
/// Represents a single entry in the cache with its value and metadata.
/// </summary>
public sealed class CacheEntry
{
    public byte[] Value { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }

    public CacheEntry(byte[] value, TimeSpan? ttl = null)
    {
        Value = value;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = ttl.HasValue ? CreatedAt + ttl.Value : null;
    }

    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow >= ExpiresAt.Value;
}
