using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Caching;

/// <summary>
/// 高性能分散キャッシュシステム
/// </summary>
public class DistributedCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, CacheRegion> _regions = new();
    private readonly CacheConfiguration _config;
    private readonly Timer _cleanupTimer;

    public DistributedCache(CacheConfiguration? config = null)
    {
        _config = config ?? new CacheConfiguration();
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// キャッシュエントリを取得
    /// </summary>
    public async Task<CacheResult<T>> GetAsync<T>(string key, string? region = null)
    {
        var fullKey = BuildFullKey(key, region);

        if (_cache.TryGetValue(fullKey, out var entry))
        {
            if (!entry.IsExpired)
            {
                // アクセス時間を更新（LRU）
                entry.LastAccessed = DateTime.UtcNow;
                Interlocked.Increment(ref entry.AccessCount);

                return entry.Value is T typedValue
                    ? CacheResult<T>.Hit(typedValue)
                    : CacheResult<T>.Miss();
            }
            else
            {
                // 期限切れエントリを削除
                _cache.TryRemove(fullKey, out _);
                UpdateRegionStats(region, removed: 1);
            }
        }

        return CacheResult<T>.Miss();
    }

    /// <summary>
    /// キャッシュエントリを設定
    /// </summary>
    public async Task SetAsync<T>(string key, T value, CacheOptions? options = null)
    {
        options ??= new CacheOptions();
        var fullKey = BuildFullKey(key, options.Region);

        var entry = new CacheEntry
        {
            Key = fullKey,
            Value = value,
            Created = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            Expiration = options.AbsoluteExpiration ?? DateTime.UtcNow.Add(_config.DefaultExpiration),
            SlidingExpiration = options.SlidingExpiration,
            Priority = options.Priority,
            Tags = options.Tags ?? Array.Empty<string>(),
            Size = CalculateObjectSize(value)
        };

        _cache[fullKey] = entry;
        UpdateRegionStats(options.Region, added: 1, sizeDelta: entry.Size);

        // メモリ制限チェック
        await EnforceMemoryLimitsAsync();
    }

    /// <summary>
    /// キャッシュエントリを削除
    /// </summary>
    public async Task<bool> RemoveAsync(string key, string? region = null)
    {
        var fullKey = BuildFullKey(key, region);

        if (_cache.TryRemove(fullKey, out var entry))
        {
            UpdateRegionStats(region, removed: 1, sizeDelta: -entry.Size);
            return true;
        }

        return false;
    }

    /// <summary>
    /// タグに基づいてエントリを削除
    /// </summary>
    public async Task<int> RemoveByTagAsync(string tag)
    {
        var removedCount = 0;
        var keysToRemove = new List<string>();

        foreach (var entry in _cache)
        {
            if (entry.Value.Tags.Contains(tag))
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                UpdateRegionStats(ExtractRegion(key), removed: 1, sizeDelta: -entry.Size);
                removedCount++;
            }
        }

        return removedCount;
    }

    /// <summary>
    /// リージョンをクリア
    /// </summary>
    public async Task<int> ClearRegionAsync(string region)
    {
        var removedCount = 0;
        var keysToRemove = new List<string>();

        foreach (var entry in _cache)
        {
            if (ExtractRegion(entry.Key) == region)
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                UpdateRegionStats(region, removed: 1, sizeDelta: -entry.Size);
                removedCount++;
            }
        }

        return removedCount;
    }

    /// <summary>
    /// すべてのキャッシュをクリア
    /// </summary>
    public async Task ClearAsync()
    {
        _cache.Clear();
        _regions.Clear();
    }

    /// <summary>
    /// キャッシュ統計を取得
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        var totalEntries = _cache.Count;
        var totalSize = _cache.Values.Sum(e => e.Size);
        var hitRate = CalculateHitRate();

        return new CacheStatistics
        {
            TotalEntries = totalEntries,
            TotalSizeBytes = totalSize,
            HitRate = hitRate,
            RegionStats = _regions.ToDictionary(r => r.Key, r => r.Value.GetStatistics())
        };
    }

    /// <summary>
    /// キャッシュをウォームアップ
    /// </summary>
    public async Task WarmupAsync(IEnumerable<CacheWarmupItem> items)
    {
        foreach (var item in items)
        {
            await SetAsync(item.Key, item.Value, item.Options);
        }
    }

    private string BuildFullKey(string key, string? region)
    {
        return region != null ? $"{region}:{key}" : key;
    }

    private string? ExtractRegion(string fullKey)
    {
        var colonIndex = fullKey.IndexOf(':');
        return colonIndex >= 0 ? fullKey.Substring(0, colonIndex) : null;
    }

    private void UpdateRegionStats(string? region, int added = 0, int removed = 0, long sizeDelta = 0)
    {
        if (region == null) return;

        var regionStats = _regions.GetOrAdd(region, _ => new CacheRegion());
        regionStats.EntryCount += added - removed;
        regionStats.TotalSizeBytes += sizeDelta;
    }

    private async Task EnforceMemoryLimitsAsync()
    {
        // メモリ制限を超えている場合、古いエントリを削除
        while (GetCurrentMemoryUsage() > _config.MaxMemoryBytes)
        {
            if (!RemoveLeastRecentlyUsed())
                break;
        }

        // エントリ数制限
        while (_cache.Count > _config.MaxEntries)
        {
            if (!RemoveLeastRecentlyUsed())
                break;
        }
    }

    private bool RemoveLeastRecentlyUsed()
    {
        var lruEntry = _cache.Values
            .OrderBy(e => e.LastAccessed)
            .FirstOrDefault();

        if (lruEntry != null)
        {
            _cache.TryRemove(lruEntry.Key, out _);
            UpdateRegionStats(ExtractRegion(lruEntry.Key), removed: 1, sizeDelta: -lruEntry.Size);
            return true;
        }

        return false;
    }

    private long GetCurrentMemoryUsage()
    {
        return _cache.Values.Sum(e => e.Size);
    }

    private double CalculateHitRate()
    {
        var totalAccesses = _cache.Values.Sum(e => (double)e.AccessCount);
        if (totalAccesses == 0) return 0;

        var hits = _cache.Values.Count(e => e.AccessCount > 0);
        return hits / (double)_cache.Count;
    }

    private static long CalculateObjectSize(object obj)
    {
        // 簡易的なサイズ計算（実際の実装ではより正確な計算が必要）
        return obj?.ToString()?.Length * 2 ?? 0;
    }

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                UpdateRegionStats(ExtractRegion(key), removed: 1, sizeDelta: -entry.Size);
            }
        }
    }

    // ネストされたクラス
    private class CacheEntry
    {
        public string Key = "";
        public object? Value;
        public DateTime Created;
        public DateTime LastAccessed;
        public DateTime Expiration;
        public TimeSpan? SlidingExpiration;
        public CachePriority Priority = CachePriority.Normal;
        public string[] Tags = Array.Empty<string>();
        public long Size;
        public long AccessCount;

        public bool IsExpired => DateTime.UtcNow > Expiration ||
                               (SlidingExpiration.HasValue &&
                                DateTime.UtcNow - LastAccessed > SlidingExpiration.Value);
    }

    private class CacheRegion
    {
        public int EntryCount;
        public long TotalSizeBytes;

        public CacheRegionStatistics GetStatistics()
        {
            return new CacheRegionStatistics
            {
                EntryCount = EntryCount,
                TotalSizeBytes = TotalSizeBytes
            };
        }
    }
}

/// <summary>
/// キャッシュ設定
/// </summary>
public class CacheConfiguration
{
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    public int MaxEntries { get; set; } = 10000;
    public bool EnableCompression { get; set; } = true;
    public bool EnableEncryption { get; set; } = false;
}

/// <summary>
/// キャッシュオプション
/// </summary>
public class CacheOptions
{
    public string? Region { get; set; }
    public DateTime? AbsoluteExpiration { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public CachePriority Priority { get; set; } = CachePriority.Normal;
    public string[]? Tags { get; set; }
}

/// <summary>
/// キャッシュ優先度
/// </summary>
public enum CachePriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// キャッシュ結果
/// </summary>
public class CacheResult<T>
{
    public bool IsHit { get; private set; }
    public T? Value { get; private set; }

    public static CacheResult<T> Hit(T value)
    {
        return new CacheResult<T> { IsHit = true, Value = value };
    }

    public static CacheResult<T> Miss()
    {
        return new CacheResult<T> { IsHit = false };
    }
}

/// <summary>
/// キャッシュ統計
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long TotalSizeBytes { get; set; }
    public double HitRate { get; set; }
    public Dictionary<string, CacheRegionStatistics> RegionStats { get; set; } = new();
}

/// <summary>
/// リージョン統計
/// </summary>
public class CacheRegionStatistics
{
    public int EntryCount { get; set; }
    public long TotalSizeBytes { get; set; }
}

/// <summary>
/// キャッシュウォームアップアイテム
/// </summary>
public class CacheWarmupItem
{
    public string Key = "";
    public object? Value;
    public CacheOptions? Options;
}

/// <summary>
/// リソースプールマネージャー
/// </summary>
public class ResourcePoolManager<T> where T : class
{
    private readonly ConcurrentQueue<T> _available = new();
    private readonly ConcurrentDictionary<T, DateTime> _leased = new();
    private readonly Func<Task<T>> _factory;
    private readonly Action<T> _cleanup;
    private readonly int _maxPoolSize;
    private readonly TimeSpan _leaseTimeout;

    public ResourcePoolManager(
        Func<Task<T>> factory,
        Action<T> cleanup,
        int maxPoolSize = 10,
        TimeSpan? leaseTimeout = null)
    {
        _factory = factory;
        _cleanup = cleanup;
        _maxPoolSize = maxPoolSize;
        _leaseTimeout = leaseTimeout ?? TimeSpan.FromMinutes(5);
    }

    public async Task<ResourceLease<T>> AcquireAsync()
    {
        T? resource = null;

        // 利用可能なリソースを取得
        if (_available.TryDequeue(out resource))
        {
            _leased[resource] = DateTime.UtcNow;
            return new ResourceLease<T>(resource, this);
        }

        // 新しいリソースを作成
        if (_leased.Count < _maxPoolSize)
        {
            resource = await _factory();
            _leased[resource] = DateTime.UtcNow;
            return new ResourceLease<T>(resource, this);
        }

        // プールが満杯の場合は待機
        throw new InvalidOperationException("Resource pool exhausted");
    }

    public void Release(T resource)
    {
        if (_leased.TryRemove(resource, out _))
        {
            // リソースをクリーンアップしてからプールに戻す
            _cleanup(resource);
            _available.Enqueue(resource);
        }
    }

    public void CleanupExpiredLeases()
    {
        var expiredResources = _leased
            .Where(kvp => DateTime.UtcNow - kvp.Value > _leaseTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var resource in expiredResources)
        {
            if (_leased.TryRemove(resource, out _))
            {
                _cleanup(resource);
            }
        }
    }
}

/// <summary>
/// リソースリース
/// </summary>
public class ResourceLease<T> : IDisposable where T : class
{
    private readonly T _resource;
    private readonly ResourcePoolManager<T> _pool;
    private bool _disposed;

    public T Resource => _resource;

    public ResourceLease(T resource, ResourcePoolManager<T> pool)
    {
        _resource = resource;
        _pool = pool;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Release(_resource);
            _disposed = true;
        }
    }
}

/// <summary>
/// 非同期タスクスケジューラー
/// </summary>
public class AsyncTaskScheduler
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<string, Task> _runningTasks = new();
    private readonly int _maxConcurrency;

    public AsyncTaskScheduler(int maxConcurrency = 10)
    {
        _maxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency);
    }

    public async Task<T> ScheduleAsync<T>(string taskId, Func<Task<T>> taskFactory, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            // 同じタスクIDの実行中のタスクがある場合は待機
            if (_runningTasks.TryGetValue(taskId, out var existingTask))
            {
                await existingTask;
            }

            var task = taskFactory();
            _runningTasks[taskId] = task;

            try
            {
                var result = await task;
                return result;
            }
            finally
            {
                _runningTasks.TryRemove(taskId, out _);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ScheduleAsync(string taskId, Func<Task> taskFactory, CancellationToken cancellationToken = default)
    {
        await ScheduleAsync(taskId, async () => { await taskFactory(); return true; }, cancellationToken);
    }

    public int GetActiveTaskCount()
    {
        return _runningTasks.Count;
    }

    public IEnumerable<string> GetActiveTaskIds()
    {
        return _runningTasks.Keys;
    }
}
