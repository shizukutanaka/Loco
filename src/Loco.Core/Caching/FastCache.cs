using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Caching;

/// <summary>
/// High-performance cache implementation - Robert C. Martin's clean code
/// Lock-free, memory-efficient with automatic expiration
/// </summary>
public sealed class FastCache<TKey, TValue> : IDisposable
{
    private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _defaultExpiration;
    private readonly int _maxSize;
    private long _accessCount;
    private long _hitCount;
    
    public FastCache(int maxSize = 10000, TimeSpan? defaultExpiration = null)
    {
        _maxSize = maxSize;
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(10);
        
        // Cleanup expired entries every minute
        _cleanupTimer = new Timer(
            _ => CleanupExpired(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }
    
    public double HitRate => _accessCount == 0 ? 0 : (double)_hitCount / _accessCount;
    public int Count => _cache.Count;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(TKey key, out TValue value)
    {
        Interlocked.Increment(ref _accessCount);
        
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                Interlocked.Increment(ref _hitCount);
                entry.LastAccessed = DateTime.UtcNow;
                value = entry.Value;
                return true;
            }
            
            // Expired - remove it
            _cache.TryRemove(key, out _);
        }
        
        value = default;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(TKey key, TValue value, TimeSpan? expiration = null)
    {
        var expiresAt = DateTime.UtcNow.Add(expiration ?? _defaultExpiration);
        
        var entry = new CacheEntry
        {
            Value = value,
            ExpiresAt = expiresAt,
            LastAccessed = DateTime.UtcNow
        };
        
        _cache.AddOrUpdate(key, entry, (_, _) => entry);
        
        // Simple size limit enforcement
        if (_cache.Count > _maxSize)
        {
            Task.Run(() => EvictLeastRecentlyUsed());
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<TValue> GetOrCreateAsync(
        TKey key,
        Func<Task<TValue>> factory,
        TimeSpan? expiration = null)
    {
        if (TryGet(key, out var value))
            return value;
        
        value = await factory().ConfigureAwait(false);
        Set(key, value, expiration);
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetOrCreate(
        TKey key,
        Func<TValue> factory,
        TimeSpan? expiration = null)
    {
        if (TryGet(key, out var value))
            return value;
        
        value = factory();
        Set(key, value, expiration);
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key) => _cache.TryRemove(key, out _);
    
    public void Clear() => _cache.Clear();
    
    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<TKey>();
        
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt <= now)
                keysToRemove.Add(kvp.Key);
        }
        
        foreach (var key in keysToRemove)
            _cache.TryRemove(key, out _);
    }
    
    private void EvictLeastRecentlyUsed()
    {
        if (_cache.Count <= _maxSize)
            return;
        
        var toRemove = _cache.Count - _maxSize + _maxSize / 10; // Remove 10% more
        var sorted = new List<KeyValuePair<TKey, CacheEntry>>(_cache);
        sorted.Sort((a, b) => a.Value.LastAccessed.CompareTo(b.Value.LastAccessed));
        
        for (int i = 0; i < Math.Min(toRemove, sorted.Count); i++)
        {
            _cache.TryRemove(sorted[i].Key, out _);
        }
    }
    
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache.Clear();
    }
    
    private class CacheEntry
    {
        public TValue Value;
        public DateTime ExpiresAt;
        public DateTime LastAccessed;
    }
}

/// <summary>
/// Global cache manager
/// </summary>
public static class CacheManager
{
    private static readonly ConcurrentDictionary<string, object> _caches = new();
    
    public static FastCache<TKey, TValue> GetCache<TKey, TValue>(
        string name,
        int maxSize = 10000,
        TimeSpan? defaultExpiration = null)
    {
        return (FastCache<TKey, TValue>)_caches.GetOrAdd(
            name,
            _ => new FastCache<TKey, TValue>(maxSize, defaultExpiration)
        );
    }
    
    public static void ClearAll()
    {
        foreach (var cache in _caches.Values)
        {
            if (cache is IDisposable disposable)
                disposable.Dispose();
        }
        _caches.Clear();
    }
}
