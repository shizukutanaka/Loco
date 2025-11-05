// John Carmack: "Simplicity is prerequisite for reliability"
// Rob Pike: "Do one thing well"
// Uncle Bob: "Clean code reads like well-written prose"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple, fast, practical cache - no over-engineering
/// Carmack: Direct memory access, minimal overhead
/// Pike: Simple interface, clear semantics
/// Martin: Single Responsibility Principle
/// </summary>
public class SimpleCache<T>
{
    private readonly ConcurrentDictionary<string, (T value, DateTime expiry)> _cache = new();
    private readonly TimeSpan _defaultTtl;

    public SimpleCache(TimeSpan? defaultTtl = null)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
    }

    // Simple Get - returns value or default
    public T? Get(string key)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.expiry > DateTime.UtcNow)
                return entry.value;

            _cache.TryRemove(key, out _);
        }
        return default;
    }

    // Simple Set - no complex options
    public void Set(string key, T value, TimeSpan? ttl = null)
    {
        var expiry = DateTime.UtcNow.Add(ttl ?? _defaultTtl);
        _cache[key] = (value, expiry);
    }

    // Simple Remove
    public bool Remove(string key) => _cache.TryRemove(key, out _);

    // Simple Clear
    public void Clear() => _cache.Clear();

    // Get stats without complexity
    public (int count, long sizeEstimate) GetStats()
    {
        var count = _cache.Count;
        var size = count * (IntPtr.Size + 32); // Rough estimate
        return (count, size);
    }
}