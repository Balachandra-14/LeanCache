namespace LeanCache.Core.Tests;

public class CacheStoreConcurrencyTests : IDisposable
{
    private readonly CacheStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task ConcurrentSets_AllKeysAreStored()
    {
        const int taskCount = 10;
        const int keysPerTask = 100;

        var tasks = Enumerable.Range(0, taskCount).Select(t =>
            Task.Run(() =>
            {
                for (var i = 0; i < keysPerTask; i++)
                {
                    _store.Set($"t{t}:k{i}", [(byte)(t + i)]);
                }
            }));

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount * keysPerTask, _store.Count);
    }

    [Fact]
    public async Task ConcurrentGets_NeverThrow()
    {
        _store.Set("shared", [42]);

        var tasks = Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    var val = _store.Get("shared");
                    Assert.NotNull(val);
                    Assert.Equal([42], val);
                }
            }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentSetAndDelete_NoCrashes()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                _store.Set($"key{i % 100}", [(byte)(i % 256)]);
                i++;
                if (i % 50 == 0)
                {
                    await Task.Yield();
                }
            }
        });

        var deleter = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                _store.Delete($"key{i % 100}");
                i++;
                if (i % 50 == 0)
                {
                    await Task.Yield();
                }
            }
        });

        var reader = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                _ = _store.Get($"key{i % 100}");
                i++;
                if (i % 50 == 0)
                {
                    await Task.Yield();
                }
            }
        });

        await Task.WhenAll(writer, deleter, reader);

        // If we got here without exceptions, concurrency is safe.
    }

    [Fact]
    public async Task ConcurrentExpiry_DoesNotCorruptStore()
    {
        const int count = 500;

        // Set many keys with short TTL
        for (var i = 0; i < count; i++)
        {
            _store.Set($"key{i}", [(byte)(i % 256)], ttl: TimeSpan.FromMilliseconds(10));
        }

        // Read them concurrently while they're expiring
        var tasks = Enumerable.Range(0, count).Select(i =>
            Task.Run(() =>
            {
                _ = _store.Get($"key{i}");
                _ = _store.Exists($"key{i}");
            }));

        await Task.WhenAll(tasks);

        // Sweep should clean up anything remaining
        _store.RemoveExpiredKeys();

        Assert.Equal(0, _store.Count);
    }
}
