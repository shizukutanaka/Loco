using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Loco.Core.Performance
{
    public interface IAdvancedCacheService
    {
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CacheLevel level = CacheLevel.L1) where T : class;
        Task RemoveAsync(string key);
        Task InvalidatePatternAsync(string pattern);
        Task<CacheStats> GetStatsAsync();
        Task WarmupAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        Task ClearAsync();
        Task<bool> ExistsAsync(string key);
        Task RefreshAsync(string key);
        void StartBackgroundCleanup();
        void StopBackgroundCleanup();
    }

    public enum CacheLevel
    {
        L1 = 1,  // In-memory cache
        L2 = 2,  // Distributed cache
        L3 = 3,  // Persistent cache
        All = 99 // All levels
    }

    public class CacheStats
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;
        public Dictionary<CacheLevel, CacheLevelStats> LevelStats { get; set; } = new();
        public long TotalMemoryUsage { get; set; }
        public int TotalItems { get; set; }
        public TimeSpan AverageRetrievalTime { get; set; }
        public long EvictedItems { get; set; }
        public Dictionary<string, int> TopKeys { get; set; } = new();
    }

    public class CacheLevelStats
    {
        public long Requests { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public double HitRate => Requests > 0 ? (double)Hits / Requests * 100 : 0;
        public int Items { get; set; }
        public long MemoryUsage { get; set; }
    }

    public class CacheEntry<T>
    {
        public string Key { get; set; }
        public T Value { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public int AccessCount { get; set; }
        public CacheLevel Level { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class AdvancedCacheOptions
    {
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
        public int MaxL1Items { get; set; } = 10000;
        public int MaxL2Items { get; set; } = 100000;
        public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        public double EvictionThreshold { get; set; } = 0.8; // 80% full
        public bool EnableCompression { get; set; } = true;
        public bool EnablePersistentCache { get; set; } = true;
        public string PersistentCachePath { get; set; } = "cache";
        public bool EnableDistributedCache { get; set; } = false;
        public string DistributedCacheConnectionString { get; set; }
        public bool EnableStatistics { get; set; } = true;
        public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount * 4;
    }

    public class AdvancedCacheService : IAdvancedCacheService, IDisposable
    {
        private readonly ILogger<AdvancedCacheService> _logger;
        private readonly AdvancedCacheOptions _options;
        
        // Multi-level cache storage
        private readonly ConcurrentDictionary<string, CacheEntry<object>> _l1Cache; // In-memory
        private readonly ConcurrentDictionary<string, CacheEntry<object>> _l2Cache; // Distributed
        private readonly string _l3CachePath; // Persistent
        
        // Statistics and monitoring
        private readonly CacheStats _stats;
        private readonly ConcurrentDictionary<string, int> _keyAccessCount;
        private readonly object _statsLock = new object();
        
        // Background cleanup
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _operationSemaphore;
        private volatile bool _disposed;

        // Performance optimization
        private readonly MemoryCache _fastCache; // Ultra-fast cache for hot data
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public AdvancedCacheService(
            ILogger<AdvancedCacheService> logger,
            IOptions<AdvancedCacheOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new AdvancedCacheOptions();
            
            _l1Cache = new ConcurrentDictionary<string, CacheEntry<object>>();
            _l2Cache = new ConcurrentDictionary<string, CacheEntry<object>>();
            _l3CachePath = Path.Combine(Directory.GetCurrentDirectory(), _options.PersistentCachePath);
            
            _stats = new CacheStats();
            _keyAccessCount = new ConcurrentDictionary<string, int>();
            _operationSemaphore = new SemaphoreSlim(_options.MaxConcurrentOperations, _options.MaxConcurrentOperations);
            
            _fastCache = new MemoryCache("FastCache");
            
            // Initialize cache levels
            InitializeCacheLevels();
            
            // Start background cleanup
            _cleanupTimer = new Timer(CleanupCallback, null, _options.CleanupInterval, _options.CleanupInterval);
            
            _logger.LogInformation("Advanced cache service initialized with {L1MaxItems} L1 items, {L2MaxItems} L2 items", 
                _options.MaxL1Items, _options.MaxL2Items);
        }

        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Update statistics
                UpdateStats(statsAction => statsAction.TotalRequests++);
                _keyAccessCount.AddOrUpdate(key, 1, (k, v) => v + 1);

                // Try ultra-fast cache first
                if (_fastCache.Get(key) is T fastValue)
                {
                    UpdateStats(statsAction => statsAction.CacheHits++);
                    return fastValue;
                }

                // Try L1 cache
                if (_l1Cache.TryGetValue(key, out var l1Entry) && !IsExpired(l1Entry))
                {
                    l1Entry.LastAccessed = DateTime.UtcNow;
                    l1Entry.AccessCount++;
                    
                    UpdateStats(statsAction => 
                    {
                        statsAction.CacheHits++;
                        statsAction.LevelStats[CacheLevel.L1].Hits++;
                        statsAction.LevelStats[CacheLevel.L1].Requests++;
                    });
                    
                    var value = (T)l1Entry.Value;
                    
                    // Promote to fast cache if frequently accessed
                    if (l1Entry.AccessCount > 10)
                    {
                        _fastCache.Set(key, value, l1Entry.ExpiresAt);
                    }
                    
                    return value;
                }

                // Try L2 cache (distributed)
                if (_options.EnableDistributedCache && _l2Cache.TryGetValue(key, out var l2Entry) && !IsExpired(l2Entry))
                {
                    l2Entry.LastAccessed = DateTime.UtcNow;
                    l2Entry.AccessCount++;
                    
                    // Promote to L1
                    await PromoteToL1Async(key, l2Entry);
                    
                    UpdateStats(statsAction => 
                    {
                        statsAction.CacheHits++;
                        statsAction.LevelStats[CacheLevel.L2].Hits++;
                        statsAction.LevelStats[CacheLevel.L2].Requests++;
                    });
                    
                    return (T)l2Entry.Value;
                }

                // Try L3 cache (persistent)
                if (_options.EnablePersistentCache)
                {
                    var l3Value = await LoadFromPersistentCacheAsync<T>(key);
                    if (l3Value != null)
                    {
                        // Promote to higher levels
                        await SetAsync(key, l3Value, _options.DefaultExpiration, CacheLevel.L1);
                        
                        UpdateStats(statsAction => 
                        {
                            statsAction.CacheHits++;
                            statsAction.LevelStats[CacheLevel.L3].Hits++;
                            statsAction.LevelStats[CacheLevel.L3].Requests++;
                        });
                        
                        return l3Value;
                    }
                }

                // Cache miss
                UpdateStats(statsAction => statsAction.CacheMisses++);
                return null;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CacheLevel level = CacheLevel.L1) where T : class
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                return;

            await _operationSemaphore.WaitAsync();
            try
            {
                var exp = expiration ?? _options.DefaultExpiration;
                var expiresAt = DateTime.UtcNow.Add(exp);
                var size = EstimateObjectSize(value);
                var hash = ComputeHash(JsonSerializer.Serialize(value));

                var entry = new CacheEntry<object>
                {
                    Key = key,
                    Value = value,
                    ExpiresAt = expiresAt,
                    Level = level,
                    Size = size,
                    Hash = hash
                };

                // Set in appropriate cache level(s)
                switch (level)
                {
                    case CacheLevel.L1:
                        await SetInL1Async(key, entry);
                        break;
                    case CacheLevel.L2:
                        await SetInL2Async(key, entry);
                        break;
                    case CacheLevel.L3:
                        await SetInL3Async(key, entry);
                        break;
                    case CacheLevel.All:
                        await SetInL1Async(key, entry);
                        if (_options.EnableDistributedCache)
                            await SetInL2Async(key, entry);
                        if (_options.EnablePersistentCache)
                            await SetInL3Async(key, entry);
                        break;
                }

                UpdateStats(statsAction => statsAction.TotalItems++);
                
                _logger.LogDebug("Cached item {Key} at level {Level} for {Duration}", key, level, exp);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
                return cached;

            var value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }

            return value;
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            await _operationSemaphore.WaitAsync();
            try
            {
                _fastCache.Remove(key);
                _l1Cache.TryRemove(key, out _);
                _l2Cache.TryRemove(key, out _);
                
                if (_options.EnablePersistentCache)
                {
                    await RemoveFromPersistentCacheAsync(key);
                }

                UpdateStats(statsAction => statsAction.TotalItems--);
                
                _logger.LogDebug("Removed cache item {Key}", key);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task InvalidatePatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return;

            await _operationSemaphore.WaitAsync();
            try
            {
                var keysToRemove = _l1Cache.Keys
                    .Concat(_l2Cache.Keys)
                    .Where(key => IsPatternMatch(key, pattern))
                    .Distinct()
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    await RemoveAsync(key);
                }

                _logger.LogInformation("Invalidated {Count} cache items matching pattern {Pattern}", keysToRemove.Count, pattern);
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<CacheStats> GetStatsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_statsLock)
                {
                    var stats = new CacheStats
                    {
                        TotalRequests = _stats.TotalRequests,
                        CacheHits = _stats.CacheHits,
                        CacheMisses = _stats.CacheMisses,
                        TotalItems = _l1Cache.Count + _l2Cache.Count,
                        TotalMemoryUsage = CalculateMemoryUsage(),
                        EvictedItems = _stats.EvictedItems,
                        TopKeys = _keyAccessCount
                            .OrderByDescending(kv => kv.Value)
                            .Take(10)
                            .ToDictionary(kv => kv.Key, kv => kv.Value)
                    };

                    stats.LevelStats[CacheLevel.L1] = new CacheLevelStats
                    {
                        Items = _l1Cache.Count,
                        MemoryUsage = _l1Cache.Values.Sum(e => e.Size),
                        Hits = _stats.LevelStats.GetValueOrDefault(CacheLevel.L1)?.Hits ?? 0,
                        Requests = _stats.LevelStats.GetValueOrDefault(CacheLevel.L1)?.Requests ?? 0
                    };

                    stats.LevelStats[CacheLevel.L2] = new CacheLevelStats
                    {
                        Items = _l2Cache.Count,
                        MemoryUsage = _l2Cache.Values.Sum(e => e.Size),
                        Hits = _stats.LevelStats.GetValueOrDefault(CacheLevel.L2)?.Hits ?? 0,
                        Requests = _stats.LevelStats.GetValueOrDefault(CacheLevel.L2)?.Requests ?? 0
                    };

                    return stats;
                }
            });
        }

        public async Task WarmupAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var value = await factory();
                if (value != null)
                {
                    await SetAsync(key, value, expiration, CacheLevel.All);
                    _logger.LogDebug("Warmed up cache for key {Key}", key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm up cache for key {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                _fastCache.Dispose();
                _l1Cache.Clear();
                _l2Cache.Clear();
                _keyAccessCount.Clear();

                if (_options.EnablePersistentCache && Directory.Exists(_l3CachePath))
                {
                    Directory.Delete(_l3CachePath, true);
                }

                // Reset statistics
                lock (_statsLock)
                {
                    _stats.TotalRequests = 0;
                    _stats.CacheHits = 0;
                    _stats.CacheMisses = 0;
                    _stats.TotalItems = 0;
                    _stats.EvictedItems = 0;
                    _stats.LevelStats.Clear();
                }

                _logger.LogInformation("Cache cleared");
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _fastCache.Contains(key) ||
                   _l1Cache.ContainsKey(key) ||
                   _l2Cache.ContainsKey(key) ||
                   await ExistsInPersistentCacheAsync(key);
        }

        public async Task RefreshAsync(string key)
        {
            // This would typically reload data from the original source
            // For now, just extend the expiration
            if (_l1Cache.TryGetValue(key, out var entry))
            {
                entry.ExpiresAt = DateTime.UtcNow.Add(_options.DefaultExpiration);
                _logger.LogDebug("Refreshed cache entry {Key}", key);
            }

            await Task.CompletedTask;
        }

        public void StartBackgroundCleanup()
        {
            _logger.LogInformation("Background cleanup started");
        }

        public void StopBackgroundCleanup()
        {
            _logger.LogInformation("Background cleanup stopped");
        }

        private void InitializeCacheLevels()
        {
            // Initialize cache level statistics
            lock (_statsLock)
            {
                _stats.LevelStats[CacheLevel.L1] = new CacheLevelStats();
                _stats.LevelStats[CacheLevel.L2] = new CacheLevelStats();
                _stats.LevelStats[CacheLevel.L3] = new CacheLevelStats();
            }

            // Create persistent cache directory
            if (_options.EnablePersistentCache)
            {
                Directory.CreateDirectory(_l3CachePath);
            }
        }

        private bool IsExpired(CacheEntry<object> entry)
        {
            return DateTime.UtcNow > entry.ExpiresAt;
        }

        private async Task SetInL1Async(string key, CacheEntry<object> entry)
        {
            // Check if we need to evict items
            if (_l1Cache.Count >= _options.MaxL1Items)
            {
                await EvictFromL1Async();
            }

            _l1Cache.AddOrUpdate(key, entry, (k, old) => entry);
        }

        private async Task SetInL2Async(string key, CacheEntry<object> entry)
        {
            if (_l2Cache.Count >= _options.MaxL2Items)
            {
                await EvictFromL2Async();
            }

            _l2Cache.AddOrUpdate(key, entry, (k, old) => entry);
        }

        private async Task SetInL3Async(string key, CacheEntry<object> entry)
        {
            try
            {
                var filePath = GetPersistentCacheFilePath(key);
                var directory = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write to persistent cache for key {Key}", key);
            }
        }

        private async Task<T> LoadFromPersistentCacheAsync<T>(string key) where T : class
        {
            try
            {
                var filePath = GetPersistentCacheFilePath(key);
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                var entry = JsonSerializer.Deserialize<CacheEntry<object>>(json);

                if (entry == null || IsExpired(entry))
                {
                    File.Delete(filePath);
                    return null;
                }

                return entry.Value as T;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load from persistent cache for key {Key}", key);
                return null;
            }
        }

        private async Task RemoveFromPersistentCacheAsync(string key)
        {
            try
            {
                var filePath = GetPersistentCacheFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove from persistent cache for key {Key}", key);
            }

            await Task.CompletedTask;
        }

        private async Task<bool> ExistsInPersistentCacheAsync(string key)
        {
            if (!_options.EnablePersistentCache)
                return false;

            var filePath = GetPersistentCacheFilePath(key);
            return File.Exists(filePath);
        }

        private string GetPersistentCacheFilePath(string key)
        {
            var safeKey = key.Replace(Path.GetInvalidFileNameChars(), '_');
            var hash = ComputeHash(key);
            return Path.Combine(_l3CachePath, hash[..2], $"{safeKey}_{hash}.cache");
        }

        private async Task PromoteToL1Async(string key, CacheEntry<object> entry)
        {
            entry.Level = CacheLevel.L1;
            await SetInL1Async(key, entry);
        }

        private async Task EvictFromL1Async()
        {
            var itemsToEvict = (int)(_options.MaxL1Items * (1.0 - _options.EvictionThreshold));
            var oldestItems = _l1Cache.Values
                .OrderBy(e => e.LastAccessed)
                .Take(itemsToEvict)
                .ToList();

            foreach (var item in oldestItems)
            {
                _l1Cache.TryRemove(item.Key, out _);
                UpdateStats(statsAction => statsAction.EvictedItems++);
            }

            _logger.LogDebug("Evicted {Count} items from L1 cache", itemsToEvict);
        }

        private async Task EvictFromL2Async()
        {
            var itemsToEvict = (int)(_options.MaxL2Items * (1.0 - _options.EvictionThreshold));
            var oldestItems = _l2Cache.Values
                .OrderBy(e => e.LastAccessed)
                .Take(itemsToEvict)
                .ToList();

            foreach (var item in oldestItems)
            {
                _l2Cache.TryRemove(item.Key, out _);
                UpdateStats(statsAction => statsAction.EvictedItems++);
            }

            _logger.LogDebug("Evicted {Count} items from L2 cache", itemsToEvict);
        }

        private void CleanupCallback(object state)
        {
            try
            {
                _ = Task.Run(async () => await PerformCleanupAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }

        private async Task PerformCleanupAsync()
        {
            var now = DateTime.UtcNow;
            var expiredL1Keys = _l1Cache
                .Where(kv => now > kv.Value.ExpiresAt)
                .Select(kv => kv.Key)
                .ToList();

            var expiredL2Keys = _l2Cache
                .Where(kv => now > kv.Value.ExpiresAt)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredL1Keys)
            {
                _l1Cache.TryRemove(key, out _);
            }

            foreach (var key in expiredL2Keys)
            {
                _l2Cache.TryRemove(key, out _);
            }

            // Cleanup persistent cache files
            if (_options.EnablePersistentCache && Directory.Exists(_l3CachePath))
            {
                await CleanupPersistentCacheAsync();
            }

            if (expiredL1Keys.Count > 0 || expiredL2Keys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {L1Count} L1 and {L2Count} L2 expired cache entries",
                    expiredL1Keys.Count, expiredL2Keys.Count);
            }
        }

        private async Task CleanupPersistentCacheAsync()
        {
            try
            {
                var files = Directory.GetFiles(_l3CachePath, "*.cache", SearchOption.AllDirectories);
                var expiredFiles = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var lastWrite = File.GetLastWriteTimeUtc(file);
                        if (DateTime.UtcNow - lastWrite > _options.DefaultExpiration.Add(TimeSpan.FromHours(1)))
                        {
                            File.Delete(file);
                            expiredFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup persistent cache file {File}", file);
                    }
                }

                if (expiredFiles > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired persistent cache files", expiredFiles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup persistent cache directory");
            }
        }

        private long EstimateObjectSize(object obj)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj);
                return Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Default estimate
            }
        }

        private long CalculateMemoryUsage()
        {
            return _l1Cache.Values.Sum(e => e.Size) + _l2Cache.Values.Sum(e => e.Size);
        }

        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private bool IsPatternMatch(string key, string pattern)
        {
            // Simple pattern matching with * wildcard
            if (pattern.Contains("*"))
            {
                var regex = new System.Text.RegularExpressions.Regex(
                    "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return regex.IsMatch(key);
            }

            return key.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateStats(Action<CacheStats> statsAction)
        {
            if (!_options.EnableStatistics)
                return;

            lock (_statsLock)
            {
                statsAction(_stats);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            _cleanupTimer?.Dispose();
            _operationSemaphore?.Dispose();
            _fastCache?.Dispose();
            _rwLock?.Dispose();

            _logger.LogInformation("Advanced cache service disposed");
        }
    }
}