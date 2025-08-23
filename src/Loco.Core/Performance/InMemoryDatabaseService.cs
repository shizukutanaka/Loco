using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.Serialization;
using System.Text.Json;
using System.IO;
using System.Runtime.CompilerServices;

namespace Loco.Core.Performance
{
    public interface IInMemoryDatabaseService
    {
        Task<T> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task<bool> DeleteAsync(string key);
        Task<List<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class;
        Task<List<TResult>> QueryAsync<T, TResult>(Func<T, bool> filter, Func<T, TResult> selector) where T : class;
        Task<bool> ExistsAsync(string key);
        Task<long> CountAsync<T>() where T : class;
        Task<bool> TransactionAsync(Func<IInMemoryTransaction, Task> transactionFunc);
        Task CreateIndexAsync<T>(string indexName, Func<T, object> keySelector) where T : class;
        Task<List<T>> QueryByIndexAsync<T>(string indexName, object indexValue) where T : class;
        Task<DatabaseStats> GetStatsAsync();
        Task FlushAsync();
        Task<long> GetSizeAsync();
        Task CompactAsync();
        Task<BackupResult> BackupAsync(string path);
        Task<RestoreResult> RestoreAsync(string path);
    }

    public interface IInMemoryTransaction
    {
        Task<T> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task<bool> DeleteAsync(string key);
        Task CommitAsync();
        Task RollbackAsync();
    }

    public class DatabaseStats
    {
        public long TotalItems { get; set; }
        public long TotalMemoryUsage { get; set; }
        public Dictionary<string, long> TypeCounts { get; set; } = new();
        public Dictionary<string, IndexStats> IndexStats { get; set; } = new();
        public long TransactionsCompleted { get; set; }
        public long TransactionsRolledBack { get; set; }
        public TimeSpan AverageQueryTime { get; set; }
        public DateTime LastCompactionTime { get; set; }
        public double CompressionRatio { get; set; }
        public long TotalOperations { get; set; }
        public double HitRate { get; set; }
    }

    public class IndexStats
    {
        public string Name { get; set; }
        public long Items { get; set; }
        public long MemoryUsage { get; set; }
        public long QueriesCount { get; set; }
        public TimeSpan AverageQueryTime { get; set; }
        public DateTime LastUsed { get; set; }
    }

    public class DatabaseItem<T> where T : class
    {
        public string Key { get; set; }
        public T Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public long Version { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class InMemoryDatabaseOptions
    {
        public long MaxMemorySize { get; set; } = 2L * 1024 * 1024 * 1024; // 2GB
        public int MaxItems { get; set; } = 10_000_000;
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan CompactionInterval { get; set; } = TimeSpan.FromMinutes(30);
        public bool EnableCompression { get; set; } = true;
        public bool EnableIndexing { get; set; } = true;
        public bool EnableTransactions { get; set; } = true;
        public bool EnablePersistence { get; set; } = false;
        public string PersistencePath { get; set; } = "database";
        public TimeSpan PersistenceInterval { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxConcurrentTransactions { get; set; } = 1000;
        public bool EnableStatistics { get; set; } = true;
        public double EvictionThreshold { get; set; } = 0.9; // 90% full
    }

    public class InMemoryDatabaseService : IInMemoryDatabaseService, IDisposable
    {
        private readonly ILogger<InMemoryDatabaseService> _logger;
        private readonly InMemoryDatabaseOptions _options;
        
        // Core storage
        private readonly ConcurrentDictionary<string, object> _storage;
        private readonly ConcurrentDictionary<string, DateTime> _expirations;
        private readonly ConcurrentDictionary<string, DatabaseItemMetadata> _metadata;
        
        // Indexing system
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>> _indexes;
        private readonly ConcurrentDictionary<string, IndexStats> _indexStats;
        
        // Transaction management
        private readonly ConcurrentDictionary<string, InMemoryTransaction> _activeTransactions;
        private readonly SemaphoreSlim _transactionSemaphore;
        
        // Performance monitoring
        private readonly DatabaseStats _stats;
        private readonly object _statsLock = new object();
        private long _currentVersion = 0;
        private long _totalOperations = 0;
        private long _hitCount = 0;
        private long _missCount = 0;
        
        // Background tasks
        private readonly Timer _compactionTimer;
        private readonly Timer _persistenceTimer;
        private readonly Timer _cleanupTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private volatile bool _disposed = false;

        public InMemoryDatabaseService(
            ILogger<InMemoryDatabaseService> logger,
            IOptions<InMemoryDatabaseOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new InMemoryDatabaseOptions();
            
            _storage = new ConcurrentDictionary<string, object>();
            _expirations = new ConcurrentDictionary<string, DateTime>();
            _metadata = new ConcurrentDictionary<string, DatabaseItemMetadata>();
            _indexes = new ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<string>>>();
            _indexStats = new ConcurrentDictionary<string, IndexStats>();
            _activeTransactions = new ConcurrentDictionary<string, InMemoryTransaction>();
            _transactionSemaphore = new SemaphoreSlim(_options.MaxConcurrentTransactions);
            _stats = new DatabaseStats();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize background timers
            _compactionTimer = new Timer(CompactionCallback, null, _options.CompactionInterval, _options.CompactionInterval);
            
            if (_options.EnablePersistence)
            {
                _persistenceTimer = new Timer(PersistenceCallback, null, _options.PersistenceInterval, _options.PersistenceInterval);
            }
            
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            
            _logger.LogInformation("In-memory database service initialized with max size: {MaxSize:N0} bytes, max items: {MaxItems:N0}",
                _options.MaxMemorySize, _options.MaxItems);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<T> GetAsync<T>(string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            Interlocked.Increment(ref _totalOperations);

            // Check expiration first
            if (_expirations.TryGetValue(key, out var expiresAt) && DateTime.UtcNow > expiresAt)
            {
                await DeleteAsync(key);
                Interlocked.Increment(ref _missCount);
                return null;
            }

            if (_storage.TryGetValue(key, out var value))
            {
                // Update access metadata
                if (_metadata.TryGetValue(key, out var metadata))
                {
                    metadata.LastAccessed = DateTime.UtcNow;
                    metadata.AccessCount++;
                }

                Interlocked.Increment(ref _hitCount);
                return value as T;
            }

            Interlocked.Increment(ref _missCount);
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
                return;

            // Check memory limits
            if (await IsMemoryLimitReachedAsync())
            {
                await EvictItemsAsync();
            }

            var exp = expiration ?? _options.DefaultExpiration;
            var expiresAt = DateTime.UtcNow.Add(exp);
            var version = Interlocked.Increment(ref _currentVersion);

            _storage.AddOrUpdate(key, value, (k, oldValue) => value);
            _expirations.AddOrUpdate(key, expiresAt, (k, oldExp) => expiresAt);
            
            var metadata = new DatabaseItemMetadata
            {
                CreatedAt = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                Version = version,
                AccessCount = 1,
                Size = EstimateObjectSize(value)
            };
            
            _metadata.AddOrUpdate(key, metadata, (k, oldMeta) =>
            {
                oldMeta.Version = version;
                oldMeta.Size = metadata.Size;
                return oldMeta;
            });

            // Update indexes
            await UpdateIndexesAsync(key, value);

            // Update statistics
            if (_options.EnableStatistics)
            {
                UpdateTypeCount<T>(1);
            }

            Interlocked.Increment(ref _totalOperations);
        }

        public async Task<bool> DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var removed = _storage.TryRemove(key, out var value);
            _expirations.TryRemove(key, out _);
            
            if (_metadata.TryRemove(key, out var metadata))
            {
                // Remove from indexes
                await RemoveFromIndexesAsync(key, value);
                
                // Update statistics
                if (_options.EnableStatistics && value != null)
                {
                    UpdateTypeCount(value.GetType(), -1);
                }
            }

            Interlocked.Increment(ref _totalOperations);
            return removed;
        }

        public async Task<List<T>> QueryAsync<T>(Func<T, bool> predicate) where T : class
        {
            if (predicate == null)
                return new List<T>();

            var results = new List<T>();
            var queryStartTime = DateTime.UtcNow;

            await Task.Run(() =>
            {
                foreach (var kvp in _storage)
                {
                    if (kvp.Value is T item && predicate(item))
                    {
                        // Check if not expired
                        if (!_expirations.TryGetValue(kvp.Key, out var expiresAt) || DateTime.UtcNow <= expiresAt)
                        {
                            results.Add(item);
                        }
                    }
                }
            });

            var queryTime = DateTime.UtcNow - queryStartTime;
            UpdateQueryStats(queryTime);

            Interlocked.Increment(ref _totalOperations);
            return results;
        }

        public async Task<List<TResult>> QueryAsync<T, TResult>(Func<T, bool> filter, Func<T, TResult> selector) where T : class
        {
            if (filter == null || selector == null)
                return new List<TResult>();

            var results = new List<TResult>();
            var queryStartTime = DateTime.UtcNow;

            await Task.Run(() =>
            {
                foreach (var kvp in _storage)
                {
                    if (kvp.Value is T item && filter(item))
                    {
                        // Check if not expired
                        if (!_expirations.TryGetValue(kvp.Key, out var expiresAt) || DateTime.UtcNow <= expiresAt)
                        {
                            results.Add(selector(item));
                        }
                    }
                }
            });

            var queryTime = DateTime.UtcNow - queryStartTime;
            UpdateQueryStats(queryTime);

            Interlocked.Increment(ref _totalOperations);
            return results;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            // Check expiration
            if (_expirations.TryGetValue(key, out var expiresAt) && DateTime.UtcNow > expiresAt)
            {
                await DeleteAsync(key);
                return false;
            }

            return _storage.ContainsKey(key);
        }

        public async Task<long> CountAsync<T>() where T : class
        {
            return await Task.Run(() =>
            {
                return _storage.Values.Count(v => v is T);
            });
        }

        public async Task<bool> TransactionAsync(Func<IInMemoryTransaction, Task> transactionFunc)
        {
            if (!_options.EnableTransactions || transactionFunc == null)
                return false;

            await _transactionSemaphore.WaitAsync();
            
            try
            {
                var transactionId = Guid.NewGuid().ToString();
                var transaction = new InMemoryTransaction(transactionId, this);
                
                _activeTransactions[transactionId] = transaction;

                try
                {
                    await transactionFunc(transaction);
                    await transaction.CommitAsync();
                    
                    lock (_statsLock)
                    {
                        _stats.TransactionsCompleted++;
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Transaction {TransactionId} failed, rolling back", transactionId);
                    await transaction.RollbackAsync();
                    
                    lock (_statsLock)
                    {
                        _stats.TransactionsRolledBack++;
                    }
                    
                    return false;
                }
                finally
                {
                    _activeTransactions.TryRemove(transactionId, out _);
                }
            }
            finally
            {
                _transactionSemaphore.Release();
            }
        }

        public async Task CreateIndexAsync<T>(string indexName, Func<T, object> keySelector) where T : class
        {
            if (!_options.EnableIndexing || string.IsNullOrWhiteSpace(indexName) || keySelector == null)
                return;

            var index = new ConcurrentDictionary<object, HashSet<string>>();
            var indexStats = new IndexStats
            {
                Name = indexName,
                Items = 0,
                QueriesCount = 0,
                LastUsed = DateTime.UtcNow
            };

            // Build initial index
            await Task.Run(() =>
            {
                foreach (var kvp in _storage)
                {
                    if (kvp.Value is T item)
                    {
                        var indexKey = keySelector(item);
                        if (indexKey != null)
                        {
                            var keySet = index.GetOrAdd(indexKey, _ => new HashSet<string>());
                            lock (keySet)
                            {
                                keySet.Add(kvp.Key);
                            }
                            indexStats.Items++;
                        }
                    }
                }
            });

            _indexes[indexName] = index;
            _indexStats[indexName] = indexStats;

            _logger.LogInformation("Created index '{IndexName}' with {Items} items", indexName, indexStats.Items);
        }

        public async Task<List<T>> QueryByIndexAsync<T>(string indexName, object indexValue) where T : class
        {
            if (!_options.EnableIndexing || string.IsNullOrWhiteSpace(indexName))
                return new List<T>();

            if (!_indexes.TryGetValue(indexName, out var index))
                return new List<T>();

            var results = new List<T>();
            var queryStartTime = DateTime.UtcNow;

            if (index.TryGetValue(indexValue, out var keySet))
            {
                await Task.Run(() =>
                {
                    HashSet<string> keys;
                    lock (keySet)
                    {
                        keys = new HashSet<string>(keySet);
                    }

                    foreach (var key in keys)
                    {
                        if (_storage.TryGetValue(key, out var value) && value is T item)
                        {
                            // Check if not expired
                            if (!_expirations.TryGetValue(key, out var expiresAt) || DateTime.UtcNow <= expiresAt)
                            {
                                results.Add(item);
                            }
                        }
                    }
                });
            }

            // Update index statistics
            if (_indexStats.TryGetValue(indexName, out var stats))
            {
                stats.QueriesCount++;
                stats.LastUsed = DateTime.UtcNow;
                var queryTime = DateTime.UtcNow - queryStartTime;
                stats.AverageQueryTime = TimeSpan.FromMilliseconds(
                    (stats.AverageQueryTime.TotalMilliseconds * (stats.QueriesCount - 1) + queryTime.TotalMilliseconds) / stats.QueriesCount
                );
            }

            return results;
        }

        public async Task<DatabaseStats> GetStatsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_statsLock)
                {
                    var stats = new DatabaseStats
                    {
                        TotalItems = _storage.Count,
                        TotalMemoryUsage = CalculateMemoryUsage(),
                        TransactionsCompleted = _stats.TransactionsCompleted,
                        TransactionsRolledBack = _stats.TransactionsRolledBack,
                        LastCompactionTime = _stats.LastCompactionTime,
                        CompressionRatio = _stats.CompressionRatio,
                        TotalOperations = _totalOperations,
                        HitRate = _totalOperations > 0 ? (double)_hitCount / _totalOperations * 100 : 0
                    };

                    // Copy type counts
                    stats.TypeCounts = new Dictionary<string, long>(_stats.TypeCounts);

                    // Copy index stats
                    foreach (var kvp in _indexStats)
                    {
                        stats.IndexStats[kvp.Key] = new IndexStats
                        {
                            Name = kvp.Value.Name,
                            Items = kvp.Value.Items,
                            MemoryUsage = kvp.Value.MemoryUsage,
                            QueriesCount = kvp.Value.QueriesCount,
                            AverageQueryTime = kvp.Value.AverageQueryTime,
                            LastUsed = kvp.Value.LastUsed
                        };
                    }

                    return stats;
                }
            });
        }

        public async Task FlushAsync()
        {
            _storage.Clear();
            _expirations.Clear();
            _metadata.Clear();
            
            foreach (var index in _indexes.Values)
            {
                index.Clear();
            }

            ResetStats();
            _logger.LogInformation("Database flushed - all data cleared");
            
            await Task.CompletedTask;
        }

        public async Task<long> GetSizeAsync()
        {
            return await Task.Run(() => CalculateMemoryUsage());
        }

        public async Task CompactAsync()
        {
            var compactionStartTime = DateTime.UtcNow;
            var itemsRemoved = 0;
            var memoryFreed = 0L;

            await Task.Run(() =>
            {
                var keysToRemove = new List<string>();
                var now = DateTime.UtcNow;

                // Find expired items
                foreach (var kvp in _expirations)
                {
                    if (now > kvp.Value)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                // Remove expired items
                foreach (var key in keysToRemove)
                {
                    if (_storage.TryRemove(key, out var value))
                    {
                        _expirations.TryRemove(key, out _);
                        
                        if (_metadata.TryRemove(key, out var metadata))
                        {
                            memoryFreed += metadata.Size;
                        }
                        
                        itemsRemoved++;
                        
                        // Remove from indexes
                        _ = Task.Run(() => RemoveFromIndexesAsync(key, value));
                    }
                }

                // Compact indexes
                foreach (var index in _indexes.Values)
                {
                    var indexKeysToRemove = new List<object>();
                    
                    foreach (var kvp in index)
                    {
                        lock (kvp.Value)
                        {
                            kvp.Value.RemoveWhere(k => !_storage.ContainsKey(k));
                            if (kvp.Value.Count == 0)
                            {
                                indexKeysToRemove.Add(kvp.Key);
                            }
                        }
                    }

                    foreach (var key in indexKeysToRemove)
                    {
                        index.TryRemove(key, out _);
                    }
                }
            });

            var compactionTime = DateTime.UtcNow - compactionStartTime;
            
            lock (_statsLock)
            {
                _stats.LastCompactionTime = DateTime.UtcNow;
            }

            _logger.LogInformation("Compaction completed in {Duration:F2}ms: removed {Items} expired items, freed {Memory:N0} bytes",
                compactionTime.TotalMilliseconds, itemsRemoved, memoryFreed);
        }

        public async Task<BackupResult> BackupAsync(string path)
        {
            var result = new BackupResult { Success = false };
            
            try
            {
                var backupData = await Task.Run(() =>
                {
                    return new
                    {
                        Storage = _storage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        Expirations = _expirations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        Metadata = _metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        BackupTime = DateTime.UtcNow,
                        Version = _currentVersion
                    };
                });

                var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions 
                { 
                    WriteIndented = false,
                    IncludeFields = true 
                });

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await File.WriteAllTextAsync(path, json);

                result.Success = true;
                result.BackupSize = new FileInfo(path).Length;
                result.ItemCount = _storage.Count;

                _logger.LogInformation("Database backup completed: {Items} items, {Size:N0} bytes to {Path}",
                    result.ItemCount, result.BackupSize, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database backup failed to {Path}", path);
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<RestoreResult> RestoreAsync(string path)
        {
            var result = new RestoreResult { Success = false };

            try
            {
                if (!File.Exists(path))
                {
                    result.ErrorMessage = "Backup file not found";
                    return result;
                }

                var json = await File.ReadAllTextAsync(path);
                var backupData = JsonSerializer.Deserialize<BackupData>(json);

                await FlushAsync(); // Clear current data

                await Task.Run(() =>
                {
                    // Restore storage
                    foreach (var kvp in backupData.Storage)
                    {
                        _storage[kvp.Key] = kvp.Value;
                    }

                    // Restore expirations
                    foreach (var kvp in backupData.Expirations)
                    {
                        _expirations[kvp.Key] = kvp.Value;
                    }

                    // Restore metadata
                    foreach (var kvp in backupData.Metadata)
                    {
                        _metadata[kvp.Key] = kvp.Value;
                    }

                    _currentVersion = backupData.Version;
                });

                result.Success = true;
                result.RestoredItems = _storage.Count;

                _logger.LogInformation("Database restore completed: {Items} items from {Path}",
                    result.RestoredItems, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database restore failed from {Path}", path);
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        // Helper methods
        private async Task<bool> IsMemoryLimitReachedAsync()
        {
            var currentMemory = CalculateMemoryUsage();
            var itemCount = _storage.Count;
            
            return currentMemory > _options.MaxMemorySize * _options.EvictionThreshold ||
                   itemCount > _options.MaxItems * _options.EvictionThreshold;
        }

        private async Task EvictItemsAsync()
        {
            var itemsToEvict = (int)(_storage.Count * 0.1); // Evict 10%
            var keysToEvict = new List<string>();

            // Use LRU eviction strategy
            var lruItems = _metadata
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .ThenBy(kvp => kvp.Value.AccessCount)
                .Take(itemsToEvict)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in lruItems)
            {
                await DeleteAsync(key);
            }

            _logger.LogDebug("Evicted {Count} items using LRU strategy", lruItems.Count);
        }

        private long CalculateMemoryUsage()
        {
            // Simplified memory calculation
            return _metadata.Values.Sum(m => m.Size);
        }

        private long EstimateObjectSize(object obj)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                return 1024; // Default estimate
            }
        }

        private async Task UpdateIndexesAsync<T>(string key, T value) where T : class
        {
            // This would update all relevant indexes for the type T
            // Implementation depends on registered indexes
            await Task.CompletedTask;
        }

        private async Task RemoveFromIndexesAsync(string key, object value)
        {
            // Remove key from all indexes that might contain it
            await Task.CompletedTask;
        }

        private void UpdateTypeCount<T>(int delta) where T : class
        {
            UpdateTypeCount(typeof(T), delta);
        }

        private void UpdateTypeCount(Type type, int delta)
        {
            if (_options.EnableStatistics)
            {
                lock (_statsLock)
                {
                    var typeName = type.Name;
                    _stats.TypeCounts.TryGetValue(typeName, out var count);
                    _stats.TypeCounts[typeName] = Math.Max(0, count + delta);
                }
            }
        }

        private void UpdateQueryStats(TimeSpan queryTime)
        {
            if (_options.EnableStatistics)
            {
                lock (_statsLock)
                {
                    var totalQueries = _stats.TotalOperations + 1;
                    _stats.AverageQueryTime = TimeSpan.FromMilliseconds(
                        (_stats.AverageQueryTime.TotalMilliseconds * (_stats.TotalOperations - 1) + queryTime.TotalMilliseconds) / totalQueries
                    );
                }
            }
        }

        private void ResetStats()
        {
            lock (_statsLock)
            {
                _stats.TotalItems = 0;
                _stats.TotalMemoryUsage = 0;
                _stats.TypeCounts.Clear();
                _stats.IndexStats.Clear();
                _stats.TransactionsCompleted = 0;
                _stats.TransactionsRolledBack = 0;
                _stats.AverageQueryTime = TimeSpan.Zero;
                _stats.TotalOperations = 0;
            }
            
            Interlocked.Exchange(ref _totalOperations, 0);
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
        }

        // Timer callbacks
        private void CompactionCallback(object state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await CompactAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled compaction");
                }
            });
        }

        private void PersistenceCallback(object state)
        {
            if (!_options.EnablePersistence)
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var backupPath = Path.Combine(_options.PersistencePath, $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                    await BackupAsync(backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled persistence");
                }
            });
        }

        private void CleanupCallback(object state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Cleanup expired transactions
                    var expiredTransactions = _activeTransactions
                        .Where(kvp => DateTime.UtcNow - kvp.Value.CreatedAt > TimeSpan.FromMinutes(30))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var transactionId in expiredTransactions)
                    {
                        if (_activeTransactions.TryRemove(transactionId, out var transaction))
                        {
                            await transaction.RollbackAsync();
                            _logger.LogWarning("Rolled back expired transaction: {TransactionId}", transactionId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup");
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            _cancellationTokenSource?.Cancel();
            _compactionTimer?.Dispose();
            _persistenceTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _transactionSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();

            _logger.LogInformation("In-memory database service disposed");
        }
    }

    // Supporting classes
    public class DatabaseItemMetadata
    {
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public long Version { get; set; }
        public long Size { get; set; }
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public long BackupSize { get; set; }
        public long ItemCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public long RestoredItems { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime RestoredAt { get; set; } = DateTime.UtcNow;
    }

    public class BackupData
    {
        public Dictionary<string, object> Storage { get; set; } = new();
        public Dictionary<string, DateTime> Expirations { get; set; } = new();
        public Dictionary<string, DatabaseItemMetadata> Metadata { get; set; } = new();
        public DateTime BackupTime { get; set; }
        public long Version { get; set; }
    }

    // Transaction implementation
    public class InMemoryTransaction : IInMemoryTransaction
    {
        public string TransactionId { get; }
        public DateTime CreatedAt { get; }
        
        private readonly InMemoryDatabaseService _database;
        private readonly Dictionary<string, object> _transactionLog;
        private readonly HashSet<string> _modifiedKeys;
        private readonly object _lockObject = new object();
        private bool _isCommitted = false;
        private bool _isRolledBack = false;

        public InMemoryTransaction(string transactionId, InMemoryDatabaseService database)
        {
            TransactionId = transactionId;
            CreatedAt = DateTime.UtcNow;
            _database = database;
            _transactionLog = new Dictionary<string, object>();
            _modifiedKeys = new HashSet<string>();
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            lock (_lockObject)
            {
                if (_transactionLog.TryGetValue(key, out var value))
                {
                    return value as T;
                }
            }

            return await _database.GetAsync<T>(key);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            lock (_lockObject)
            {
                if (_isCommitted || _isRolledBack)
                    throw new InvalidOperationException("Transaction is already finalized");

                _transactionLog[key] = value;
                _modifiedKeys.Add(key);
            }

            await Task.CompletedTask;
        }

        public async Task<bool> DeleteAsync(string key)
        {
            lock (_lockObject)
            {
                if (_isCommitted || _isRolledBack)
                    throw new InvalidOperationException("Transaction is already finalized");

                _transactionLog[key] = null; // Mark as deleted
                _modifiedKeys.Add(key);
            }

            await Task.CompletedTask;
            return true;
        }

        public async Task CommitAsync()
        {
            lock (_lockObject)
            {
                if (_isCommitted || _isRolledBack)
                    throw new InvalidOperationException("Transaction is already finalized");

                _isCommitted = true;
            }

            // Apply all changes to the main database
            foreach (var kvp in _transactionLog)
            {
                if (kvp.Value == null)
                {
                    await _database.DeleteAsync(kvp.Key);
                }
                else
                {
                    await _database.SetAsync(kvp.Key, kvp.Value);
                }
            }
        }

        public async Task RollbackAsync()
        {
            lock (_lockObject)
            {
                if (_isCommitted || _isRolledBack)
                    throw new InvalidOperationException("Transaction is already finalized");

                _isRolledBack = true;
                _transactionLog.Clear();
                _modifiedKeys.Clear();
            }

            await Task.CompletedTask;
        }
    }
}