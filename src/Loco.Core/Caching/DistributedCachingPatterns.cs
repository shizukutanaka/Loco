#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Caching;

/// <summary>
/// Distributed Caching Patterns - Redis, Memcached, Cache Strategies
/// </summary>

/// <summary>
/// Cache entry
/// </summary>
public class CacheEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastAccessedAt")]
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("accessCount")]
    public int AccessCount { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    public bool IsExpired => ExpiresAt != null && DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// Cache configuration
/// </summary>
public class CacheConfig
{
    [JsonPropertyName("maxSize")]
    public long MaxSize { get; set; } = 1_000_000_000; // 1GB

    [JsonPropertyName("maxEntries")]
    public int MaxEntries { get; set; } = 100_000;

    [JsonPropertyName("defaultTtl")]
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(1);

    [JsonPropertyName("evictionPolicy")]
    public string EvictionPolicy { get; set; } = "LRU"; // LRU, LFU, FIFO

    [JsonPropertyName("statistics")]
    public bool Statistics { get; set; } = true;
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    [JsonPropertyName("hits")]
    public long Hits { get; set; }

    [JsonPropertyName("misses")]
    public long Misses { get; set; }

    [JsonPropertyName("evictions")]
    public long Evictions { get; set; }

    [JsonPropertyName("hitRate")]
    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;

    [JsonPropertyName("currentSize")]
    public long CurrentSize { get; set; }

    [JsonPropertyName("currentEntries")]
    public int CurrentEntries { get; set; }
}

/// <summary>
/// Distributed cache implementation
/// </summary>
public class DistributedCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly CacheConfig _config;
    private readonly CacheStatistics _statistics = new();
    private readonly ILogger<DistributedCache> _logger;

    public DistributedCache(CacheConfig config, ILogger<DistributedCache> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get value from cache
    /// </summary>
    public async Task<T?> GetAsync<T>(string key) where T : class?
    {
        if (!_cache.TryGetValue(key, out var entry))
        {
            _statistics.Misses++;
            _logger.LogDebug("Cache miss: {Key}", key);
            return null;
        }

        // Check expiration
        if (entry.IsExpired)
        {
            _cache.TryRemove(key, out _);
            _statistics.Misses++;
            _logger.LogDebug("Cache entry expired: {Key}", key);
            return null;
        }

        // Update access info
        entry.LastAccessedAt = DateTime.UtcNow;
        entry.AccessCount++;

        _statistics.Hits++;

        _logger.LogDebug("Cache hit: {Key}", key);

        return entry.Value as T;
    }

    /// <summary>
    /// Set value in cache
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? ttl = null) where T : class?
    {
        var size = EstimateSize(value);

        // Check size limits
        if (_cache.Count >= _config.MaxEntries || _statistics.CurrentSize + size > _config.MaxSize)
        {
            await EvictAsync();
        }

        var entry = new CacheEntry
        {
            Key = key,
            Value = value,
            ExpiresAt = ttl != null ? DateTime.UtcNow.Add(ttl.Value) : DateTime.UtcNow.Add(_config.DefaultTtl),
            Size = size
        };

        _cache[key] = entry;
        _statistics.CurrentSize += size;
        _statistics.CurrentEntries = _cache.Count;

        _logger.LogDebug(
            "Cached value: {Key} (TTL: {Ttl}, Size: {Size} bytes)",
            key,
            ttl,
            size);
    }

    /// <summary>
    /// Remove value from cache
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        if (_cache.TryRemove(key, out var entry))
        {
            _statistics.CurrentSize -= entry.Size;
            _statistics.CurrentEntries = _cache.Count;

            _logger.LogDebug("Removed from cache: {Key}", key);
        }
    }

    /// <summary>
    /// Clear all cache
    /// </summary>
    public async Task ClearAsync()
    {
        _cache.Clear();
        _statistics.CurrentSize = 0;
        _statistics.CurrentEntries = 0;

        _logger.LogInformation("Cleared cache");
    }

    /// <summary>
    /// Evict entries based on policy
    /// </summary>
    private async Task EvictAsync()
    {
        var entriesToEvict = (int)(_cache.Count * 0.25); // Evict 25% of entries

        List<CacheEntry> candidates = _config.EvictionPolicy switch
        {
            "LRU" => _cache.Values
                .OrderBy(e => e.LastAccessedAt)
                .Take(entriesToEvict)
                .ToList(),

            "LFU" => _cache.Values
                .OrderBy(e => e.AccessCount)
                .Take(entriesToEvict)
                .ToList(),

            "FIFO" => _cache.Values
                .OrderBy(e => e.CreatedAt)
                .Take(entriesToEvict)
                .ToList(),

            _ => _cache.Values.Take(entriesToEvict).ToList()
        };

        foreach (var entry in candidates)
        {
            if (_cache.TryRemove(entry.Key, out _))
            {
                _statistics.Evictions++;
                _statistics.CurrentSize -= entry.Size;

                _logger.LogDebug(
                    "Evicted cache entry: {Key} ({Policy})",
                    entry.Key,
                    _config.EvictionPolicy);
            }
        }

        _statistics.CurrentEntries = _cache.Count;
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        _statistics.CurrentEntries = _cache.Count;
        return _statistics;
    }

    /// <summary>
    /// Estimate size of object
    /// </summary>
    private long EstimateSize(object? obj)
    {
        if (obj == null)
            return 0;

        // Rough estimation
        var json = System.Text.Json.JsonSerializer.Serialize(obj);
        return System.Text.Encoding.UTF8.GetByteCount(json);
    }
}

/// <summary>
/// Consistent hashing for distributed cache
/// </summary>
public class ConsistentHashRing
{
    private readonly SortedDictionary<uint, string> _ring = new();
    private readonly int _virtualNodes;
    private readonly ILogger<ConsistentHashRing> _logger;

    public ConsistentHashRing(int virtualNodes, ILogger<ConsistentHashRing> logger)
    {
        _virtualNodes = virtualNodes;
        _logger = logger;
    }

    /// <summary>
    /// Add node to ring
    /// </summary>
    public void AddNode(string node)
    {
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = Hash($"{node}:{i}");
            _ring[hash] = node;
        }

        _logger.LogInformation(
            "Added node to consistent hash ring: {Node} ({VirtualNodes} virtual nodes)",
            node,
            _virtualNodes);
    }

    /// <summary>
    /// Remove node from ring
    /// </summary>
    public void RemoveNode(string node)
    {
        for (int i = 0; i < _virtualNodes; i++)
        {
            var hash = Hash($"{node}:{i}");
            _ring.Remove(hash);
        }

        _logger.LogInformation(
            "Removed node from consistent hash ring: {Node}",
            node);
    }

    /// <summary>
    /// Get node for key
    /// </summary>
    public string? GetNode(string key)
    {
        if (_ring.Count == 0)
            return null;

        var hash = Hash(key);

        // Find first node >= hash
        var node = _ring
            .Where(kvp => kvp.Key >= hash)
            .FirstOrDefault();

        // If no node found, wrap around to first
        if (node.Value == null)
        {
            node = _ring.First();
        }

        return node.Value;
    }

    private uint Hash(string key)
    {
        var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return BitConverter.ToUInt32(hash, 0);
    }
}

/// <summary>
/// Cache warming strategy
/// </summary>
public class CacheWarmer
{
    private readonly DistributedCache _cache;
    private readonly ILogger<CacheWarmer> _logger;

    public CacheWarmer(DistributedCache cache, ILogger<CacheWarmer> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Warm cache with initial data
    /// </summary>
    public async Task WarmCacheAsync<T>(
        Dictionary<string, T> data,
        TimeSpan? ttl = null) where T : class?
    {
        var stopwatch = Stopwatch.StartNew();

        foreach (var kvp in data)
        {
            await _cache.SetAsync(kvp.Key, kvp.Value, ttl);
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Warmed cache with {Count} entries in {Time}ms",
            data.Count,
            stopwatch.ElapsedMilliseconds);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class CachingExtensions
{
    public static IServiceCollection AddDistributedCaching(
        this IServiceCollection services,
        CacheConfig? config = null)
    {
        config ??= new CacheConfig();

        services.AddSingleton(config);
        services.AddSingleton<DistributedCache>();
        services.AddSingleton<CacheWarmer>();

        return services;
    }
}
