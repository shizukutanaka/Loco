using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loco.Core.Caching;

/// <summary>
/// Distributed cache service implementation using IDistributedCache
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<DistributedCacheService> _logger;
    private CacheStatistics _statistics = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DistributedCacheService(
        IDistributedCache cache,
        CacheOptions options,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
        _statistics.LastResetTime = DateTime.UtcNow;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var prefixedKey = PrefixKey(key);
            var data = await _cache.GetAsync(prefixedKey);

            if (data == null)
            {
                IncrementMiss();
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            IncrementHit();
            var json = System.Text.Encoding.UTF8.GetString(data);
            var value = JsonSerializer.Deserialize<T>(json, JsonOptions);

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache: {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var prefixedKey = PrefixKey(key);
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var data = System.Text.Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? _options.DefaultExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(5) // Refresh every 5 minutes
            };

            await _cache.SetAsync(prefixedKey, data, options);
            _logger.LogDebug("Set cache for key: {Key}, Expiration: {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            var prefixedKey = PrefixKey(key);
            await _cache.RemoveAsync(prefixedKey);
            _logger.LogDebug("Removed cache for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from cache: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var prefixedKey = PrefixKey(key);
            var data = await _cache.GetAsync(prefixedKey);
            return data != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence: {Key}", key);
            return false;
        }
    }

    public async Task<IDictionary<string, T>> GetManyAsync<T>(params string[] keys) where T : class
    {
        var result = new Dictionary<string, T>();

        try
        {
            foreach (var key in keys)
            {
                var value = await GetAsync<T>(key);
                if (value != null)
                {
                    result[key] = value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multiple items from cache");
        }

        return result;
    }

    public async Task SetManyAsync<T>(IDictionary<string, T> items, TimeSpan? expiration = null) where T : class
    {
        try
        {
            foreach (var kvp in items)
            {
                await SetAsync(kvp.Key, kvp.Value, expiration);
            }

            _logger.LogDebug("Set {Count} items in cache", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting multiple items in cache");
        }
    }

    public async Task RemoveManyAsync(params string[] keys)
    {
        try
        {
            foreach (var key in keys)
            {
                await RemoveAsync(key);
            }

            _logger.LogDebug("Removed {Count} items from cache", keys.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing multiple items from cache");
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null) where T : class
    {
        try
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }

            return value!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrCreateAsync: {Key}", key);
            return (await factory())!;
        }
    }

    public async Task InvalidateByPatternAsync(string pattern)
    {
        try
        {
            _logger.LogInformation("Invalidating cache by pattern: {Pattern}", pattern);
            // Note: Pattern-based invalidation requires Redis-specific implementation
            // This is a simplified version - in production, use StackExchange.Redis directly
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache by pattern: {Pattern}", pattern);
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        return await Task.FromResult(_statistics);
    }

    public async Task ClearAllAsync()
    {
        try
        {
            _logger.LogWarning("Clearing all cache");
            // Note: Full cache clear requires connection to actual cache implementation
            _statistics = new CacheStatistics { LastResetTime = DateTime.UtcNow };
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    private string PrefixKey(string key)
    {
        return $"{_options.KeyPrefix}{key}";
    }

    private void IncrementHit()
    {
        if (_options.EnableStatistics)
        {
            Interlocked.Increment(ref _statistics.Hits);
        }
    }

    private void IncrementMiss()
    {
        if (_options.EnableStatistics)
        {
            Interlocked.Increment(ref _statistics.Misses);
        }
    }
}
