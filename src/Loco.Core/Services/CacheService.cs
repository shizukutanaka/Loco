using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Services
{
    /// <summary>
    /// High-performance in-memory cache
    /// Following John Carmack's performance principles
    /// </summary>
    public sealed class CacheService : IDisposable
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);
        private volatile int _disposed;

        public CacheService()
        {
            // Cleanup expired entries every minute
            _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// Get or add item to cache with factory method
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<T> GetOrAddAsync<T>(
            string key, 
            Func<Task<T>> factory, 
            TimeSpan? expiration = null)
        {
            if (_disposed > 0) throw new ObjectDisposedException(nameof(CacheService));

            // Fast path - check if exists and not expired
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                return (T)entry.Value;
            }

            // Slow path - create new entry
            var value = await factory().ConfigureAwait(false);
            var newEntry = new CacheEntry(value, expiration ?? _defaultExpiration);
            
            _cache.AddOrUpdate(key, newEntry, (k, old) => newEntry);
            return value;
        }

        /// <summary>
        /// Try get cached value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(string key, out T value)
        {
            value = default!;
            
            if (_disposed > 0) return false;
            
            if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                value = (T)entry.Value;
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Set cache value
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (_disposed > 0) return;
            
            var entry = new CacheEntry(value!, expiration ?? _defaultExpiration);
            _cache.AddOrUpdate(key, entry, (k, old) => entry);
        }

        /// <summary>
        /// Remove item from cache
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clear all cache entries
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStats GetStats()
        {
            var count = 0;
            var expiredCount = 0;
            
            foreach (var entry in _cache.Values)
            {
                count++;
                if (entry.IsExpired) expiredCount++;
            }
            
            return new CacheStats
            {
                TotalEntries = count,
                ExpiredEntries = expiredCount,
                ActiveEntries = count - expiredCount
            };
        }

        private void CleanupExpired(object? state)
        {
            if (_disposed > 0) return;
            
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _cleanupTimer?.Dispose();
                _cache.Clear();
            }
        }

        private sealed class CacheEntry
        {
            public object Value { get; }
            public DateTime ExpiresAt { get; }
            
            public CacheEntry(object value, TimeSpan expiration)
            {
                Value = value;
                ExpiresAt = DateTime.UtcNow.Add(expiration);
            }
            
            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }
    }

    public struct CacheStats
    {
        public int TotalEntries { get; init; }
        public int ActiveEntries { get; init; }
        public int ExpiredEntries { get; init; }
    }
}
