namespace Loco.Core.Caching;

/// <summary>
/// Distributed cache service interface for Redis integration
/// </summary>
public interface IDistributedCacheService
{
    /// <summary>
    /// Gets a value from cache
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Sets a value in cache
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a value from cache
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Checks if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Gets multiple values from cache
    /// </summary>
    Task<IDictionary<string, T>> GetManyAsync<T>(params string[] keys) where T : class;

    /// <summary>
    /// Sets multiple values in cache
    /// </summary>
    Task SetManyAsync<T>(IDictionary<string, T> items, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes multiple values from cache
    /// </summary>
    Task RemoveManyAsync(params string[] keys);

    /// <summary>
    /// Gets or creates a value
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Invalidates cache by pattern
    /// </summary>
    Task InvalidateByPatternAsync(string pattern);

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Clears all cache
    /// </summary>
    Task ClearAllAsync();
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total items in cache
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    /// Cache hits
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Cache misses
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Hit rate percentage
    /// </summary>
    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) * 100 : 0;

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Last reset time
    /// </summary>
    public DateTime LastResetTime { get; set; }
}

/// <summary>
/// Cache configuration options
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Redis connection string
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Default expiration time
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Enable compression
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Enable statistics
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Cache key prefix
    /// </summary>
    public string KeyPrefix { get; set; } = "loco:";

    /// <summary>
    /// Maximum retries for cache operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for cache operations (milliseconds)
    /// </summary>
    public int OperationTimeout { get; set; } = 5000;
}
