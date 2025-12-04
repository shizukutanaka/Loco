// John Carmack: "Premature optimization is the root of all evil, but so is premature generalization"
// Rob Pike: "Caches aren't magic"
// Uncle Bob: "The Dependency Inversion Principle"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Unified cache implementation - combines in-memory and distributed caching
/// Simple interface, practical implementation, measurable performance
/// </summary>
public interface ICache<T>
{
    T? Get(string key);
    Task<T?> GetAsync(string key);
    void Set(string key, T value, TimeSpan? ttl = null);
    Task SetAsync(string key, T value, TimeSpan? ttl = null);
    bool Remove(string key);
    Task<bool> RemoveAsync(string key);
    void Clear();
}

/// <summary>
/// In-memory cache implementation - fastest option
/// Use for: single server, low memory footprint, sub-millisecond access
/// </summary>
public class MemoryCache<T> : ICache<T>
{
    private readonly ConcurrentDictionary<string, (T value, DateTime expiry)> _cache = new();
    private readonly TimeSpan _defaultTtl;
    private readonly Timer _cleanupTimer;

    public MemoryCache(TimeSpan? defaultTtl = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);

        // Cleanup expired items every minute
        _cleanupTimer = new Timer(_ => CleanupExpired(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public T? Get(string key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.expiry > DateTime.UtcNow)
                return entry.value;

            _cache.TryRemove(key, out _);
        }
        return default;
    }

    public Task<T?> GetAsync(string key) => Task.FromResult(Get(key));

    public void Set(string key, T value, TimeSpan? ttl = null)
    {
        var expiry = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
        _cache[key] = (value, expiry);
    }

    public Task SetAsync(string key, T value, TimeSpan? ttl = null)
    {
        Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public bool Remove(string key) => _cache.TryRemove(key, out _);

    public Task<bool> RemoveAsync(string key) => Task.FromResult(Remove(key));

    public void Clear() => _cache.Clear();

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var expired = _cache.Where(kvp => kvp.Value.expiry <= now)
                           .Select(kvp => kvp.Key)
                           .ToList();

        foreach (var key in expired)
        {
            _cache.TryRemove(key, out _);
        }
    }

    // Stats for monitoring
    public (int count, int expired) GetStats()
    {
        var now = DateTime.UtcNow;
        var total = _cache.Count;
        var expired = _cache.Count(kvp => kvp.Value.expiry <= now);
        return (total, expired);
    }
}

/// <summary>
/// Two-level cache: L1 (memory) + L2 (distributed)
/// Use for: multi-server, larger datasets, balanced performance
/// </summary>
public class TieredCache<T> : ICache<T>
{
    private readonly MemoryCache<T> _l1Cache;
    private readonly ICache<T>? _l2Cache;
    private readonly TimeSpan _l1Ttl;

    public TieredCache(ICache<T>? l2Cache = null, TimeSpan? l1Ttl = null)
    {
        _l1Cache = new MemoryCache<T>(l1Ttl ?? TimeSpan.FromSeconds(30));
        _l2Cache = l2Cache;
        _l1Ttl = l1Ttl ?? TimeSpan.FromSeconds(30);
    }

    public T? Get(string key)
    {
        // Check L1 first (fastest)
        var value = _l1Cache.Get(key);
        if (value != null)
            return value;

        // Check L2 if available
        if (_l2Cache != null)
        {
            value = _l2Cache.Get(key);
            if (value != null)
            {
                // Promote to L1
                _l1Cache.Set(key, value, _l1Ttl);
            }
        }

        return value;
    }

    public async Task<T?> GetAsync(string key)
    {
        // Check L1 first (fastest)
        var value = await _l1Cache.GetAsync(key);
        if (value != null)
            return value;

        // Check L2 if available
        if (_l2Cache != null)
        {
            value = await _l2Cache.GetAsync(key);
            if (value != null)
            {
                // Promote to L1
                await _l1Cache.SetAsync(key, value, _l1Ttl);
            }
        }

        return value;
    }

    public void Set(string key, T value, TimeSpan? ttl = null)
    {
        _l1Cache.Set(key, value, Math.Min(ttl?.TotalSeconds ?? 30, 30)
            * TimeSpan.FromSeconds(1));
        _l2Cache?.Set(key, value, ttl);
    }

    public async Task SetAsync(string key, T value, TimeSpan? ttl = null)
    {
        await _l1Cache.SetAsync(key, value, Math.Min(ttl?.TotalSeconds ?? 30, 30)
            * TimeSpan.FromSeconds(1));
        if (_l2Cache != null)
            await _l2Cache.SetAsync(key, value, ttl);
    }

    public bool Remove(string key)
    {
        var l1Result = _l1Cache.Remove(key);
        var l2Result = _l2Cache?.Remove(key) ?? false;
        return l1Result || l2Result;
    }

    public async Task<bool> RemoveAsync(string key)
    {
        var l1Task = _l1Cache.RemoveAsync(key);
        var l2Task = _l2Cache?.RemoveAsync(key) ?? Task.FromResult(false);

        await Task.WhenAll(l1Task, l2Task);
        return l1Task.Result || l2Task.Result;
    }

    public void Clear()
    {
        _l1Cache.Clear();
        _l2Cache?.Clear();
    }
}

/// <summary>
/// Simple cache factory - dependency injection friendly
/// </summary>
public static class CacheFactory
{
    public static ICache<T> CreateMemoryCache<T>(TimeSpan? defaultTtl = null)
    {
        return new MemoryCache<T>(defaultTtl);
    }

    public static ICache<T> CreateTieredCache<T>(ICache<T>? l2Cache = null, TimeSpan? l1Ttl = null)
    {
        return new TieredCache<T>(l2Cache, l1Ttl);
    }

    // Simple singleton for application-wide cache
    private static readonly ConcurrentDictionary<Type, object> _globalCaches = new();

    public static ICache<T> GetGlobalCache<T>()
    {
        return (ICache<T>)_globalCaches.GetOrAdd(typeof(T),
            _ => CreateMemoryCache<T>(TimeSpan.FromMinutes(10)));
    }
}