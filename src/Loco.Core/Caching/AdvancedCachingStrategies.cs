#nullable enable

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Caching;

/// <summary>
/// Cache strategy enumeration
/// </summary>
public enum CacheStrategy
{
    /// <summary>
    /// Cache-Aside: Load from source on miss, populate cache
    /// </summary>
    CacheAside,

    /// <summary>
    /// Write-Through: Write to cache and source synchronously
    /// </summary>
    WriteThrough,

    /// <summary>
    /// Write-Behind: Write to cache first, source asynchronously
    /// </summary>
    WriteBehind,

    /// <summary>
    /// Read-Through: Cache handles loading from source
    /// </summary>
    ReadThrough
}

/// <summary>
/// Cache invalidation strategy
/// </summary>
public enum CacheInvalidationStrategy
{
    /// <summary>
    /// TTL-based expiration
    /// </summary>
    TimeToLive,

    /// <summary>
    /// Manual invalidation
    /// </summary>
    Manual,

    /// <summary>
    /// Event-driven invalidation
    /// </summary>
    EventDriven,

    /// <summary>
    /// LRU (Least Recently Used) eviction
    /// </summary>
    LRU,

    /// <summary>
    /// Combined TTL + Manual
    /// </summary>
    Hybrid
}

/// <summary>
/// Cache configuration
/// </summary>
public class CacheConfig
{
    /// <summary>
    /// Default TTL in seconds
    /// </summary>
    public int DefaultTTLSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum cache size
    /// </summary>
    public int MaxCacheSize { get; set; } = 10000;

    /// <summary>
    /// Cache strategy to use
    /// </summary>
    public CacheStrategy Strategy { get; set; } = CacheStrategy.CacheAside;

    /// <summary>
    /// Invalidation strategy
    /// </summary>
    public CacheInvalidationStrategy InvalidationStrategy { get; set; } = CacheInvalidationStrategy.TimeToLive;

    /// <summary>
    /// Enable compression for large values
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Compression threshold in bytes
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024;

    /// <summary>
    /// Enable cache statistics
    /// </summary>
    public bool EnableStatistics { get; set; } = true;
}

/// <summary>
/// Cache statistics for monitoring
/// </summary>
public class CacheStatistics
{
    public long Hits { get; set; }
    public long Misses { get; set; }
    public long Writes { get; set; }
    public long Deletes { get; set; }

    public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) * 100 : 0;

    public string GetSummary() =>
        $"Hits: {Hits}, Misses: {Misses}, Hit Rate: {HitRate:F2}%, " +
        $"Writes: {Writes}, Deletes: {Deletes}";
}

/// <summary>
/// Cache-Aside pattern implementation
/// Client is responsible for loading from source on cache miss
/// </summary>
public class CacheAsideStrategy<TKey, TValue> : ICacheStrategy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheAsideStrategy<TKey, TValue>> _logger;
    private readonly CacheConfig _config;
    private readonly CacheStatistics _statistics;

    public CacheAsideStrategy(
        IDistributedCache cache,
        ILogger<CacheAsideStrategy<TKey, TValue>> logger,
        CacheConfig config)
    {
        _cache = cache;
        _logger = logger;
        _config = config;
        _statistics = new CacheStatistics();
    }

    public async Task<TValue?> GetAsync(TKey key, Func<TKey, Task<TValue?>> loader)
    {
        var cacheKey = GenerateKey(key);

        // Try to get from cache
        var cachedValue = await _cache.GetStringAsync(cacheKey);
        if (cachedValue != null)
        {
            _statistics.Hits++;
            _logger.LogDebug("Cache hit for key: {Key}", cacheKey);
            return Deserialize(cachedValue);
        }

        _statistics.Misses++;
        _logger.LogDebug("Cache miss for key: {Key}", cacheKey);

        // Load from source
        var value = await loader(key);
        if (value != null)
        {
            // Populate cache
            await SetAsync(key, value);
        }

        return value;
    }

    public async Task SetAsync(TKey key, TValue value, TimeSpan? ttl = null)
    {
        var cacheKey = GenerateKey(key);
        var serialized = Serialize(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_config.DefaultTTLSeconds)
        };

        await _cache.SetStringAsync(cacheKey, serialized, options);
        _statistics.Writes++;
        _logger.LogDebug("Cache set for key: {Key}, TTL: {TTL}s", cacheKey, options.AbsoluteExpirationRelativeToNow?.TotalSeconds);
    }

    public async Task InvalidateAsync(TKey key)
    {
        var cacheKey = GenerateKey(key);
        await _cache.RemoveAsync(cacheKey);
        _statistics.Deletes++;
        _logger.LogDebug("Cache invalidated for key: {Key}", cacheKey);
    }

    public CacheStatistics GetStatistics() => _statistics;

    private string GenerateKey(TKey key) => $"{typeof(TValue).Name}:{key}";

    private string Serialize(TValue value) => System.Text.Json.JsonSerializer.Serialize(value);

    private TValue? Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
}

/// <summary>
/// Write-Through pattern implementation
/// Updates cache and source synchronously
/// </summary>
public class WriteThroughStrategy<TKey, TValue> : ICacheStrategy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<WriteThroughStrategy<TKey, TValue>> _logger;
    private readonly CacheConfig _config;
    private readonly CacheStatistics _statistics;
    private readonly Func<TKey, Task<TValue?>> _sourceLoader;
    private readonly Func<TKey, TValue, Task> _sourcePersister;

    public WriteThroughStrategy(
        IDistributedCache cache,
        ILogger<WriteThroughStrategy<TKey, TValue>> logger,
        CacheConfig config,
        Func<TKey, Task<TValue?>> sourceLoader,
        Func<TKey, TValue, Task> sourcePersister)
    {
        _cache = cache;
        _logger = logger;
        _config = config;
        _statistics = new CacheStatistics();
        _sourceLoader = sourceLoader;
        _sourcePersister = sourcePersister;
    }

    public async Task<TValue?> GetAsync(TKey key, Func<TKey, Task<TValue?>> loader)
    {
        var cacheKey = GenerateKey(key);

        // Try cache first
        var cachedValue = await _cache.GetStringAsync(cacheKey);
        if (cachedValue != null)
        {
            _statistics.Hits++;
            return Deserialize(cachedValue);
        }

        _statistics.Misses++;

        // Load from source
        var value = await _sourceLoader(key);
        if (value != null)
        {
            // Update cache immediately
            await SetAsync(key, value);
        }

        return value;
    }

    public async Task SetAsync(TKey key, TValue value, TimeSpan? ttl = null)
    {
        var cacheKey = GenerateKey(key);

        // Write to source first (synchronously)
        try
        {
            await _sourcePersister(key, value);
            _logger.LogDebug("Source persisted for key: {Key}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist to source for key: {Key}", cacheKey);
            throw; // Fail-fast: don't update cache if source fails
        }

        // Write to cache
        var serialized = Serialize(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_config.DefaultTTLSeconds)
        };

        await _cache.SetStringAsync(cacheKey, serialized, options);
        _statistics.Writes++;
        _logger.LogDebug("Cache set for key: {Key} (Write-Through)", cacheKey);
    }

    public async Task InvalidateAsync(TKey key)
    {
        var cacheKey = GenerateKey(key);
        await _cache.RemoveAsync(cacheKey);
        _statistics.Deletes++;
        _logger.LogDebug("Cache invalidated for key: {Key}", cacheKey);
    }

    public CacheStatistics GetStatistics() => _statistics;

    private string GenerateKey(TKey key) => $"{typeof(TValue).Name}:{key}";

    private string Serialize(TValue value) => System.Text.Json.JsonSerializer.Serialize(value);

    private TValue? Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
}

/// <summary>
/// Write-Behind pattern implementation
/// Writes to cache immediately, source asynchronously
/// </summary>
public class WriteBehindStrategy<TKey, TValue> : ICacheStrategy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<WriteBehindStrategy<TKey, TValue>> _logger;
    private readonly CacheConfig _config;
    private readonly CacheStatistics _statistics;
    private readonly Func<TKey, TValue, Task> _sourcePersister;
    private readonly ConcurrentQueue<(TKey, TValue)> _writeQueue;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public WriteBehindStrategy(
        IDistributedCache cache,
        ILogger<WriteBehindStrategy<TKey, TValue>> logger,
        CacheConfig config,
        Func<TKey, TValue, Task> sourcePersister)
    {
        _cache = cache;
        _logger = logger;
        _config = config;
        _statistics = new CacheStatistics();
        _sourcePersister = sourcePersister;
        _writeQueue = new ConcurrentQueue<(TKey, TValue)>();
        _cancellationTokenSource = new CancellationTokenSource();

        // Start background writer
        StartBackgroundWriter();
    }

    public async Task<TValue?> GetAsync(TKey key, Func<TKey, Task<TValue?>> loader)
    {
        var cacheKey = GenerateKey(key);

        // Try cache first
        var cachedValue = await _cache.GetStringAsync(cacheKey);
        if (cachedValue != null)
        {
            _statistics.Hits++;
            return Deserialize(cachedValue);
        }

        _statistics.Misses++;

        // Load from source
        var value = await loader(key);
        return value;
    }

    public async Task SetAsync(TKey key, TValue value, TimeSpan? ttl = null)
    {
        var cacheKey = GenerateKey(key);

        // Write to cache immediately (fast path)
        var serialized = Serialize(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_config.DefaultTTLSeconds)
        };

        await _cache.SetStringAsync(cacheKey, serialized, options);
        _statistics.Writes++;

        // Queue for source persistence (background)
        _writeQueue.Enqueue((key, value));
        _logger.LogDebug("Cache set and queued for source persistence: {Key} (Write-Behind)", cacheKey);
    }

    public async Task InvalidateAsync(TKey key)
    {
        var cacheKey = GenerateKey(key);
        await _cache.RemoveAsync(cacheKey);
        _statistics.Deletes++;
        _logger.LogDebug("Cache invalidated for key: {Key}", cacheKey);
    }

    public CacheStatistics GetStatistics() => _statistics;

    private void StartBackgroundWriter()
    {
        _ = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    while (_writeQueue.TryDequeue(out var item))
                    {
                        try
                        {
                            await _sourcePersister(item.Item1, item.Item2);
                            _logger.LogDebug("Background persisted to source: {Key}", item.Item1);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background persistence failed for key: {Key}, requeuing", item.Item1);
                            _writeQueue.Enqueue(item); // Requeue on failure
                        }
                    }

                    // Check queue every 100ms
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background writer error");
                }
            }
        }, _cancellationTokenSource.Token);
    }

    private string GenerateKey(TKey key) => $"{typeof(TValue).Name}:{key}";

    private string Serialize(TValue value) => System.Text.Json.JsonSerializer.Serialize(value);

    private TValue? Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<TValue>(json);
}

/// <summary>
/// Cache strategy interface
/// </summary>
public interface ICacheStrategy<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>
    /// Gets value from cache, loading from source if needed
    /// </summary>
    Task<TValue?> GetAsync(TKey key, Func<TKey, Task<TValue?>> loader);

    /// <summary>
    /// Sets value in cache
    /// </summary>
    Task SetAsync(TKey key, TValue value, TimeSpan? ttl = null);

    /// <summary>
    /// Invalidates cached value
    /// </summary>
    Task InvalidateAsync(TKey key);

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Unified cache manager supporting multiple strategies
/// </summary>
public class StrategyAwareCacheManager<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly Dictionary<CacheStrategy, ICacheStrategy<TKey, TValue>> _strategies;
    private ICacheStrategy<TKey, TValue> _currentStrategy;
    private readonly ILogger<StrategyAwareCacheManager<TKey, TValue>> _logger;

    public StrategyAwareCacheManager(
        Dictionary<CacheStrategy, ICacheStrategy<TKey, TValue>> strategies,
        CacheStrategy defaultStrategy,
        ILogger<StrategyAwareCacheManager<TKey, TValue>> logger)
    {
        _strategies = strategies;
        _currentStrategy = strategies[defaultStrategy];
        _logger = logger;
    }

    /// <summary>
    /// Switches cache strategy at runtime
    /// </summary>
    public void SwitchStrategy(CacheStrategy strategy)
    {
        if (_strategies.TryGetValue(strategy, out var newStrategy))
        {
            _currentStrategy = newStrategy;
            _logger.LogInformation("Switched cache strategy to: {Strategy}", strategy);
        }
    }

    /// <summary>
    /// Gets value using current strategy
    /// </summary>
    public async Task<TValue?> GetAsync(TKey key, Func<TKey, Task<TValue?>> loader)
    {
        return await _currentStrategy.GetAsync(key, loader);
    }

    /// <summary>
    /// Sets value using current strategy
    /// </summary>
    public async Task SetAsync(TKey key, TValue value, TimeSpan? ttl = null)
    {
        await _currentStrategy.SetAsync(key, value, ttl);
    }

    /// <summary>
    /// Invalidates value in current strategy
    /// </summary>
    public async Task InvalidateAsync(TKey key)
    {
        await _currentStrategy.InvalidateAsync(key);
    }

    /// <summary>
    /// Gets statistics from current strategy
    /// </summary>
    public CacheStatistics GetStatistics() => _currentStrategy.GetStatistics();

    /// <summary>
    /// Gets current strategy
    /// </summary>
    public CacheStrategy GetCurrentStrategy() =>
        _strategies.FirstOrDefault(kvp => kvp.Value == _currentStrategy).Key;
}

/// <summary>
/// Extension methods for cache strategy registration
/// </summary>
public static class CacheStrategyExtensions
{
    /// <summary>
    /// Adds cache strategy services
    /// </summary>
    public static IServiceCollection AddCacheStrategies(
        this IServiceCollection services,
        CacheConfig? config = null)
    {
        config ??= new CacheConfig();
        services.AddSingleton(config);
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = "localhost:6379";
        });

        return services;
    }

    /// <summary>
    /// Adds Cache-Aside strategy for specific type
    /// </summary>
    public static IServiceCollection AddCacheAsideStrategy<TKey, TValue>(
        this IServiceCollection services)
        where TKey : notnull
        where TValue : class
    {
        services.AddSingleton<ICacheStrategy<TKey, TValue>>(provider =>
            new CacheAsideStrategy<TKey, TValue>(
                provider.GetRequiredService<IDistributedCache>(),
                provider.GetRequiredService<ILogger<CacheAsideStrategy<TKey, TValue>>>(),
                provider.GetRequiredService<CacheConfig>()));

        return services;
    }

    /// <summary>
    /// Adds Write-Through strategy for specific type
    /// </summary>
    public static IServiceCollection AddWriteThroughStrategy<TKey, TValue>(
        this IServiceCollection services,
        Func<TKey, Task<TValue?>> sourceLoader,
        Func<TKey, TValue, Task> sourcePersister)
        where TKey : notnull
        where TValue : class
    {
        services.AddSingleton<ICacheStrategy<TKey, TValue>>(provider =>
            new WriteThroughStrategy<TKey, TValue>(
                provider.GetRequiredService<IDistributedCache>(),
                provider.GetRequiredService<ILogger<WriteThroughStrategy<TKey, TValue>>>(),
                provider.GetRequiredService<CacheConfig>(),
                sourceLoader,
                sourcePersister));

        return services;
    }

    /// <summary>
    /// Adds Write-Behind strategy for specific type
    /// </summary>
    public static IServiceCollection AddWriteBehindStrategy<TKey, TValue>(
        this IServiceCollection services,
        Func<TKey, TValue, Task> sourcePersister)
        where TKey : notnull
        where TValue : class
    {
        services.AddSingleton<ICacheStrategy<TKey, TValue>>(provider =>
            new WriteBehindStrategy<TKey, TValue>(
                provider.GetRequiredService<IDistributedCache>(),
                provider.GetRequiredService<ILogger<WriteBehindStrategy<TKey, TValue>>>(),
                provider.GetRequiredService<CacheConfig>(),
                sourcePersister));

        return services;
    }
}

/// <summary>
/// Example usage of caching strategies
/// </summary>
public class CachingStrategyExample
{
    private readonly ICacheStrategy<string, User> _userCache;
    private readonly ILogger<CachingStrategyExample> _logger;

    public CachingStrategyExample(
        ICacheStrategy<string, User> userCache,
        ILogger<CachingStrategyExample> logger)
    {
        _userCache = userCache;
        _logger = logger;
    }

    /// <summary>
    /// Gets user with cache-aside pattern
    /// </summary>
    public async Task<User?> GetUserAsync(string userId)
    {
        return await _userCache.GetAsync(userId, async key =>
        {
            _logger.LogInformation("Loading user from database: {UserId}", key);
            // Simulate database load
            await Task.Delay(50);
            return new User { Id = key, Name = $"User {key}" };
        });
    }

    /// <summary>
    /// Updates user with write-through pattern
    /// </summary>
    public async Task UpdateUserAsync(string userId, User user)
    {
        await _userCache.SetAsync(userId, user, TimeSpan.FromMinutes(5));
        _logger.LogInformation("User updated: {UserId}", userId);
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public string GetStatistics() => _userCache.GetStatistics().GetSummary();
}

/// <summary>
/// Example user entity for cache demonstrations
/// </summary>
public class User
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
