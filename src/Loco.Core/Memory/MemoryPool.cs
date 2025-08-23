using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Loco.Core.Memory;

/// <summary>
/// High-performance memory pool following John Carmack's principles
/// Reduces GC pressure and improves performance
/// </summary>
public sealed class MemoryPool<T> : IDisposable where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T> _reset;
    private readonly int _maxSize;
    private int _currentSize;
    
    public MemoryPool(int maxSize = 1024, Func<T> factory = null, Action<T> reset = null)
    {
        _maxSize = maxSize;
        _factory = factory ?? (() => new T());
        _reset = reset ?? (_ => { });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Decrement(ref _currentSize);
            return item;
        }
        
        return _factory();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item == null) return;
        
        _reset(item);
        
        if (_currentSize < _maxSize)
        {
            _pool.Add(item);
            Interlocked.Increment(ref _currentSize);
        }
    }
    
    public void Dispose()
    {
        while (_pool.TryTake(out var item))
        {
            if (item is IDisposable disposable)
                disposable.Dispose();
        }
    }
}

/// <summary>
/// Buffer pool for byte arrays - reduces allocations
/// </summary>
public static class BufferPool
{
    private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Rent(int minimumLength) => _arrayPool.Rent(minimumLength);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(byte[] array, bool clearArray = false) => _arrayPool.Return(array, clearArray);
}

/// <summary>
/// Pooled object wrapper for automatic return
/// </summary>
public readonly struct PooledObject<T> : IDisposable where T : class, new()
{
    private readonly MemoryPool<T> _pool;
    public readonly T Value;
    
    public PooledObject(MemoryPool<T> pool)
    {
        _pool = pool;
        Value = pool.Rent();
    }
    
    public void Dispose()
    {
        _pool?.Return(Value);
    }
}

/// <summary>
/// Fast string builder pool
/// </summary>
public sealed class StringBuilderPool
{
    private static readonly MemoryPool<System.Text.StringBuilder> _pool = new(
        maxSize: 32,
        factory: () => new System.Text.StringBuilder(256),
        reset: sb => sb.Clear()
    );
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Text.StringBuilder Rent() => _pool.Rent();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(System.Text.StringBuilder sb) => _pool.Return(sb);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringAndReturn(System.Text.StringBuilder sb)
    {
        var result = sb.ToString();
        Return(sb);
        return result;
    }
}
