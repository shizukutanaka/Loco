using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Caching
{
    /// <summary>
    /// 高性能インテリジェントキャッシュマネージャー
    /// LRUアルゴリズム、自動クリーンアップ、ヒット率監視を提供
    /// </summary>
    public class IntelligentCacheManager : IDisposable
    {
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly LinkedList<string> _lruList = new();
        private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new();
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly CacheConfig _config;
        private bool _disposed;

        /// <summary>
        /// キャッシュ統計情報
        /// </summary>
        public CacheStats Stats { get; } = new();

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="config">キャッシュ設定</param>
        /// <param name="logger">ロガー</param>
        public IntelligentCacheManager(CacheConfig? config = null, ILogger? logger = null)
        {
            _config = config ?? new CacheConfig();
            _logger = logger;
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, _config.CleanupInterval, _config.CleanupInterval);

            _logger?.LogInformation(
                "IntelligentCacheManager initialized with max size: {MaxSize}, TTL: {DefaultTtl}",
                _config.MaxSize, _config.DefaultTtl);
        }

        /// <summary>
        /// キャッシュから値を取得
        /// </summary>
        /// <typeparam name="T">値の型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="factory">値の生成関数（キャッシュミス時）</param>
        /// <param name="ttl">有効期限（nullでデフォルト値を使用）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>キャッシュされた値</returns>
        public async Task<T> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            await _lock.WaitAsync(cancellationToken);

            try
            {
                // キャッシュから取得を試行
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (!entry.IsExpired)
                    {
                        // LRUリストを更新
                        UpdateLRU(key);

                        Stats.CacheHits++;
                        _logger?.LogDebug("Cache hit for key: {Key}", key);

                        return (T)entry.Value;
                    }
                    else
                    {
                        // 期限切れのエントリを削除
                        RemoveEntry(key);
                        Stats.CacheMisses++;
                    }
                }
                else
                {
                    Stats.CacheMisses++;
                }

                // キャッシュサイズチェックとクリーンアップ
                await EnsureCacheSizeAsync(cancellationToken);

                // 新しい値を生成
                var value = await factory();

                // キャッシュに追加
                var newEntry = new CacheEntry
                {
                    Key = key,
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow + (ttl ?? _config.DefaultTtl),
                    AccessCount = 1,
                    LastAccessedAt = DateTime.UtcNow
                };

                AddEntry(key, newEntry);

                _logger?.LogDebug("Cache miss - added new entry for key: {Key}", key);

                return value;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// キャッシュから値を取得（同期版）
        /// </summary>
        /// <typeparam name="T">値の型</typeparam>
        /// <param name="key">キー</param>
        /// <returns>キャッシュされた値（存在しない場合はdefault）</returns>
        public T? Get<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                UpdateLRU(key);
                Stats.CacheHits++;
                return (T)entry.Value;
            }

            Stats.CacheMisses++;
            return default;
        }

        /// <summary>
        /// キャッシュに値を設定
        /// </summary>
        /// <typeparam name="T">値の型</typeparam>
        /// <param name="key">キー</param>
        /// <param name="value">値</param>
        /// <param name="ttl">有効期限（nullでデフォルト値を使用）</param>
        public void Set<T>(string key, T value, TimeSpan? ttl = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            var entry = new CacheEntry
            {
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + (ttl ?? _config.DefaultTtl),
                AccessCount = 0,
                LastAccessedAt = DateTime.UtcNow
            };

            AddEntry(key, entry);
        }

        /// <summary>
        /// キャッシュからエントリを削除
        /// </summary>
        /// <param name="key">キー</param>
        /// <returns>削除が成功したかどうか</returns>
        public bool Remove(string key)
        {
            return RemoveEntry(key);
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
            _lruNodes.Clear();

            _logger?.LogInformation("Cache cleared");
        }

        /// <summary>
        /// キャッシュ統計情報を取得
        /// </summary>
        /// <returns>統計情報</returns>
        public CacheStats GetStats() => Stats.Clone();

        /// <summary>
        /// キャッシュ統計情報をリセット
        /// </summary>
        public void ResetStats()
        {
            Stats.Reset();
        }

        private void AddEntry(string key, CacheEntry entry)
        {
            _cache[key] = entry;

            // LRUリストに追加
            var node = _lruList.AddLast(key);
            _lruNodes[key] = node;

            // サイズ制限チェック
            if (_cache.Count > _config.MaxSize)
            {
                EvictLRU();
            }
        }

        private bool RemoveEntry(string key)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                // LRUリストから削除
                if (_lruNodes.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _lruNodes.Remove(key);
                }

                _logger?.LogDebug("Removed cache entry: {Key}", key);
                return true;
            }

            return false;
        }

        private void UpdateLRU(string key)
        {
            if (_lruNodes.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddLast(node);

                var entry = _cache[key];
                entry.AccessCount++;
                entry.LastAccessedAt = DateTime.UtcNow;
            }
        }

        private void EvictLRU()
        {
            if (_lruList.First == null) return;

            var keyToEvict = _lruList.First.Value;
            RemoveEntry(keyToEvict);

            _logger?.LogDebug("Evicted LRU entry: {Key}", keyToEvict);
        }

        private async Task EnsureCacheSizeAsync(CancellationToken cancellationToken)
        {
            while (_cache.Count >= _config.MaxSize && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken); // 短い遅延で他のスレッドに機会を与える
                EvictLRU();
            }
        }

        private void CleanupExpiredEntries(object? state)
        {
            try
            {
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    RemoveEntry(key);
                }

                if (expiredKeys.Any())
                {
                    _logger?.LogDebug("Cleaned up {ExpiredCount} expired cache entries", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during cache cleanup");
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _cleanupTimer.Dispose();
            _lock.Dispose();
            _disposed = true;

            _logger?.LogInformation("IntelligentCacheManager disposed");
        }
    }

    /// <summary>
    /// キャッシュエントリ
    /// </summary>
    public class CacheEntry
    {
        public string Key { get; set; } = string.Empty;
        public object Value { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AccessCount { get; set; }
        public DateTime LastAccessedAt { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// キャッシュ設定
    /// </summary>
    public class CacheConfig
    {
        public int MaxSize { get; set; } = 1000;
        public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableMetrics { get; set; } = true;
    }

    /// <summary>
    /// キャッシュ統計情報
    /// </summary>
    public class CacheStats
    {
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        public int TotalRequests => CacheHits + CacheMisses;
        public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
        public int CurrentSize { get; set; }
        public int Evictions { get; set; }
        public int ExpiredEntries { get; set; }

        public CacheStats Clone()
        {
            return new CacheStats
            {
                CacheHits = CacheHits,
                CacheMisses = CacheMisses,
                CurrentSize = CurrentSize,
                Evictions = Evictions,
                ExpiredEntries = ExpiredEntries
            };
        }

        public void Reset()
        {
            CacheHits = 0;
            CacheMisses = 0;
            CurrentSize = 0;
            Evictions = 0;
            ExpiredEntries = 0;
        }
    }
}
