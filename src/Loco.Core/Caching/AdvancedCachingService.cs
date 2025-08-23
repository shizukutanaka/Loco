using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Loco.Core.Caching
{
    public interface IAdvancedCachingService
    {
        Task<T> GetAsync<T>(string key, Func<Task<T>> factory = null, CacheOptions options = null);
        Task SetAsync<T>(string key, T value, CacheOptions options = null);
        Task<bool> ExistsAsync(string key);
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        Task<CacheStatistics> GetStatisticsAsync();
        Task FlushAsync();
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions options = null);
        Task<IDictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys);
        Task SetManyAsync<T>(IDictionary<string, T> items, CacheOptions options = null);
        Task<bool> TryUpdateAsync<T>(string key, Func<T, T> updateFunc, CacheOptions options = null);
        IAsyncEnumerable<string> GetKeysAsync(string pattern);
    }

    public class AdvancedCachingService : IAdvancedCachingService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<AdvancedCachingService> _logger;
        private readonly CacheConfiguration _configuration;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private readonly CacheStatisticsCollector _statisticsCollector;

        public AdvancedCachingService(
            IMemoryCache memoryCache,
            IDistributedCache distributedCache,
            IConnectionMultiplexer redis,
            IOptions<CacheConfiguration> configuration,
            ILogger<AdvancedCachingService> logger)
        {
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _redis = redis;
            _configuration = configuration.Value;
            _logger = logger;
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _statisticsCollector = new CacheStatisticsCollector();
        }

        public async Task<T> GetAsync<T>(string key, Func<Task<T>> factory = null, CacheOptions options = null)
        {
            options ??= CacheOptions.Default;
            _statisticsCollector.RecordRequest();

            // Try L1 cache (memory)
            if (_configuration.EnableL1Cache && _memoryCache.TryGetValue(key, out T cachedValue))
            {
                _statisticsCollector.RecordHit(CacheLevel.L1);
                _logger.LogDebug("L1 cache hit for key: {Key}", key);
                return cachedValue;
            }

            // Try L2 cache (distributed)
            if (_configuration.EnableL2Cache)
            {
                var distributedValue = await GetFromDistributedCacheAsync<T>(key);
                if (distributedValue != null)
                {
                    _statisticsCollector.RecordHit(CacheLevel.L2);
                    _logger.LogDebug("L2 cache hit for key: {Key}", key);

                    // Populate L1 cache
                    if (_configuration.EnableL1Cache)
                    {
                        SetMemoryCache(key, distributedValue, options);
                    }

                    return distributedValue;
                }
            }

            _statisticsCollector.RecordMiss();

            // If no factory provided, return default
            if (factory == null)
            {
                return default(T);
            }

            // Use double-checked locking pattern
            var lockKey = $"lock:{key}";
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                // Check cache again after acquiring lock
                if (_configuration.EnableL1Cache && _memoryCache.TryGetValue(key, out cachedValue))
                {
                    return cachedValue;
                }

                if (_configuration.EnableL2Cache)
                {
                    var distributedValue = await GetFromDistributedCacheAsync<T>(key);
                    if (distributedValue != null)
                    {
                        if (_configuration.EnableL1Cache)
                        {
                            SetMemoryCache(key, distributedValue, options);
                        }
                        return distributedValue;
                    }
                }

                // Generate value
                var value = await factory();
                
                // Set in both caches
                await SetAsync(key, value, options);

                return value;
            }
            finally
            {
                semaphore.Release();
                
                // Clean up lock after some time
                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
                {
                    _locks.TryRemove(lockKey, out _);
                });
            }
        }

        public async Task SetAsync<T>(string key, T value, CacheOptions options = null)
        {
            options ??= CacheOptions.Default;

            // Set in L1 cache
            if (_configuration.EnableL1Cache)
            {
                SetMemoryCache(key, value, options);
            }

            // Set in L2 cache
            if (_configuration.EnableL2Cache)
            {
                await SetDistributedCacheAsync(key, value, options);
            }

            _logger.LogDebug("Set cache for key: {Key}", key);
        }

        public async Task<bool> ExistsAsync(string key)
        {
            // Check L1 cache
            if (_configuration.EnableL1Cache && _memoryCache.TryGetValue(key, out _))
            {
                return true;
            }

            // Check L2 cache
            if (_configuration.EnableL2Cache)
            {
                var db = _redis.GetDatabase();
                return await db.KeyExistsAsync(key);
            }

            return false;
        }

        public async Task RemoveAsync(string key)
        {
            // Remove from L1 cache
            if (_configuration.EnableL1Cache)
            {
                _memoryCache.Remove(key);
            }

            // Remove from L2 cache
            if (_configuration.EnableL2Cache)
            {
                await _distributedCache.RemoveAsync(key);
            }

            _logger.LogDebug("Removed cache for key: {Key}", key);
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            if (_configuration.EnableL2Cache)
            {
                var db = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Any())
                {
                    await db.KeyDeleteAsync(keys);
                    _logger.LogDebug("Removed {Count} keys matching pattern: {Pattern}", 
                        keys.Length, pattern);
                }
            }

            // L1 cache doesn't support pattern removal efficiently
            // Consider using tags or maintaining a key registry
        }

        public async Task<CacheStatistics> GetStatisticsAsync()
        {
            var stats = _statisticsCollector.GetStatistics();

            if (_configuration.EnableL2Cache)
            {
                var db = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var info = await server.InfoAsync("stats");
                
                // Parse Redis stats and merge
                // This is simplified - actual implementation would parse INFO output
                stats.RedisInfo = info.ToString();
            }

            return stats;
        }

        public async Task FlushAsync()
        {
            // Clear L1 cache
            if (_configuration.EnableL1Cache && _memoryCache is MemoryCache mc)
            {
                mc.Compact(1.0);
            }

            // Clear L2 cache
            if (_configuration.EnableL2Cache)
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                await server.FlushDatabaseAsync();
            }

            _statisticsCollector.Reset();
            _logger.LogWarning("Cache flushed");
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CacheOptions options = null)
        {
            return await GetAsync(key, factory, options);
        }

        public async Task<IDictionary<string, T>> GetManyAsync<T>(IEnumerable<string> keys)
        {
            var result = new Dictionary<string, T>();
            var keysList = keys.ToList();

            // Try L1 cache first
            var missingKeys = new List<string>();
            foreach (var key in keysList)
            {
                if (_configuration.EnableL1Cache && _memoryCache.TryGetValue(key, out T value))
                {
                    result[key] = value;
                }
                else
                {
                    missingKeys.Add(key);
                }
            }

            // Try L2 cache for missing keys
            if (_configuration.EnableL2Cache && missingKeys.Any())
            {
                var db = _redis.GetDatabase();
                var redisKeys = missingKeys.Select(k => (RedisKey)k).ToArray();
                var values = await db.StringGetAsync(redisKeys);

                for (int i = 0; i < missingKeys.Count; i++)
                {
                    if (values[i].HasValue)
                    {
                        var value = JsonSerializer.Deserialize<T>(values[i]);
                        result[missingKeys[i]] = value;
                        
                        // Populate L1 cache
                        if (_configuration.EnableL1Cache)
                        {
                            SetMemoryCache(missingKeys[i], value, CacheOptions.Default);
                        }
                    }
                }
            }

            return result;
        }

        public async Task SetManyAsync<T>(IDictionary<string, T> items, CacheOptions options = null)
        {
            options ??= CacheOptions.Default;

            var tasks = new List<Task>();

            foreach (var kvp in items)
            {
                // Set in L1 cache
                if (_configuration.EnableL1Cache)
                {
                    SetMemoryCache(kvp.Key, kvp.Value, options);
                }

                // Set in L2 cache
                if (_configuration.EnableL2Cache)
                {
                    tasks.Add(SetDistributedCacheAsync(kvp.Key, kvp.Value, options));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task<bool> TryUpdateAsync<T>(string key, Func<T, T> updateFunc, CacheOptions options = null)
        {
            var lockKey = $"lock:{key}";
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                var current = await GetAsync<T>(key);
                if (current == null)
                {
                    return false;
                }

                var updated = updateFunc(current);
                await SetAsync(key, updated, options);
                return true;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async IAsyncEnumerable<string> GetKeysAsync(string pattern)
        {
            if (_configuration.EnableL2Cache)
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                
                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    yield return key.ToString();
                }
            }
        }

        private void SetMemoryCache<T>(string key, T value, CacheOptions options)
        {
            var memoryCacheOptions = new MemoryCacheEntryOptions();

            if (options.AbsoluteExpiration.HasValue)
            {
                memoryCacheOptions.AbsoluteExpiration = options.AbsoluteExpiration;
            }

            if (options.SlidingExpiration.HasValue)
            {
                memoryCacheOptions.SlidingExpiration = options.SlidingExpiration;
            }

            memoryCacheOptions.Priority = options.Priority switch
            {
                CachePriority.Low => CacheItemPriority.Low,
                CachePriority.Normal => CacheItemPriority.Normal,
                CachePriority.High => CacheItemPriority.High,
                CachePriority.NeverRemove => CacheItemPriority.NeverRemove,
                _ => CacheItemPriority.Normal
            };

            memoryCacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _logger.LogDebug("L1 cache evicted: {Key}, Reason: {Reason}", key, reason);
                _statisticsCollector.RecordEviction(CacheLevel.L1, reason.ToString());
            });

            _memoryCache.Set(key, value, memoryCacheOptions);
        }

        private async Task<T> GetFromDistributedCacheAsync<T>(string key)
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key);
                if (bytes != null && bytes.Length > 0)
                {
                    var json = Encoding.UTF8.GetString(bytes);
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting from distributed cache: {Key}", key);
            }

            return default(T);
        }

        private async Task SetDistributedCacheAsync<T>(string key, T value, CacheOptions options)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = Encoding.UTF8.GetBytes(json);

                var distributedOptions = new DistributedCacheEntryOptions();

                if (options.AbsoluteExpiration.HasValue)
                {
                    distributedOptions.AbsoluteExpiration = options.AbsoluteExpiration;
                }

                if (options.SlidingExpiration.HasValue)
                {
                    distributedOptions.SlidingExpiration = options.SlidingExpiration;
                }

                await _distributedCache.SetAsync(key, bytes, distributedOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting distributed cache: {Key}", key);
            }
        }
    }

    // Cache configuration
    public class CacheConfiguration
    {
        public bool EnableL1Cache { get; set; } = true;
        public bool EnableL2Cache { get; set; } = true;
        public int L1CacheSizeMB { get; set; } = 100;
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableStatistics { get; set; } = true;
        public bool EnableCompression { get; set; } = true;
        public int CompressionThresholdBytes { get; set; } = 1024;
    }

    // Cache options
    public class CacheOptions
    {
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public CachePriority Priority { get; set; } = CachePriority.Normal;
        public bool CompressValue { get; set; }
        public string[] Tags { get; set; }

        public static CacheOptions Default => new CacheOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            Priority = CachePriority.Normal
        };

        public static CacheOptions NoExpiration => new CacheOptions
        {
            Priority = CachePriority.High
        };

        public static CacheOptions ShortTerm => new CacheOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(1),
            Priority = CachePriority.Low
        };

        public static CacheOptions LongTerm => new CacheOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(24),
            Priority = CachePriority.Normal
        };
    }

    public enum CachePriority
    {
        Low,
        Normal,
        High,
        NeverRemove
    }

    public enum CacheLevel
    {
        L1,
        L2
    }

    // Statistics collector
    public class CacheStatisticsCollector
    {
        private long _requests;
        private long _hits;
        private long _misses;
        private readonly ConcurrentDictionary<CacheLevel, long> _levelHits;
        private readonly ConcurrentDictionary<string, long> _evictions;
        private readonly DateTime _startTime;

        public CacheStatisticsCollector()
        {
            _levelHits = new ConcurrentDictionary<CacheLevel, long>();
            _evictions = new ConcurrentDictionary<string, long>();
            _startTime = DateTime.UtcNow;
        }

        public void RecordRequest()
        {
            Interlocked.Increment(ref _requests);
        }

        public void RecordHit(CacheLevel level)
        {
            Interlocked.Increment(ref _hits);
            _levelHits.AddOrUpdate(level, 1, (_, count) => count + 1);
        }

        public void RecordMiss()
        {
            Interlocked.Increment(ref _misses);
        }

        public void RecordEviction(CacheLevel level, string reason)
        {
            var key = $"{level}:{reason}";
            _evictions.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        public CacheStatistics GetStatistics()
        {
            var hitRate = _requests > 0 ? (double)_hits / _requests * 100 : 0;

            return new CacheStatistics
            {
                TotalRequests = _requests,
                TotalHits = _hits,
                TotalMisses = _misses,
                HitRate = hitRate,
                L1Hits = _levelHits.GetValueOrDefault(CacheLevel.L1),
                L2Hits = _levelHits.GetValueOrDefault(CacheLevel.L2),
                Evictions = new Dictionary<string, long>(_evictions),
                Uptime = DateTime.UtcNow - _startTime
            };
        }

        public void Reset()
        {
            _requests = 0;
            _hits = 0;
            _misses = 0;
            _levelHits.Clear();
            _evictions.Clear();
        }
    }

    public class CacheStatistics
    {
        public long TotalRequests { get; set; }
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public double HitRate { get; set; }
        public long L1Hits { get; set; }
        public long L2Hits { get; set; }
        public Dictionary<string, long> Evictions { get; set; }
        public TimeSpan Uptime { get; set; }
        public string RedisInfo { get; set; }
    }
}