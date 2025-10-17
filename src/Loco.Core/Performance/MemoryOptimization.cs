using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Loco.Core.Performance;

/// <summary>
/// Memory optimization utilities using ArrayPool and Span
/// </summary>
public static class MemoryOptimization
{
    /// <summary>
    /// Rent buffer from array pool
    /// </summary>
    public static ArrayPoolRental<T> RentArray<T>(int minimumLength)
    {
        return new ArrayPoolRental<T>(minimumLength);
    }

    /// <summary>
    /// Convert string to bytes without allocation
    /// </summary>
    public static int GetBytes(ReadOnlySpan<char> chars, Span<byte> destination, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return encoding.GetBytes(chars, destination);
    }

    /// <summary>
    /// Split string without allocations
    /// </summary>
    public static SpanSplitEnumerator Split(ReadOnlySpan<char> input, char separator)
    {
        return new SpanSplitEnumerator(input, separator);
    }
}

/// <summary>
/// RAII wrapper for ArrayPool
/// </summary>
public readonly struct ArrayPoolRental<T> : IDisposable
{
    private readonly T[] _array;
    private readonly int _length;

    public ArrayPoolRental(int minimumLength)
    {
        _array = ArrayPool<T>.Shared.Rent(minimumLength);
        _length = minimumLength;
    }

    public Span<T> Span => _array.AsSpan(0, _length);
    public Memory<T> Memory => _array.AsMemory(0, _length);

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array, clearArray: true);
    }
}

/// <summary>
/// Span-based string splitter (zero allocation)
/// </summary>
public ref struct SpanSplitEnumerator
{
    private ReadOnlySpan<char> _remaining;
    private readonly char _separator;

    public SpanSplitEnumerator(ReadOnlySpan<char> span, char separator)
    {
        _remaining = span;
        _separator = separator;
        Current = default;
    }

    public ReadOnlySpan<char> Current { get; private set; }

    public bool MoveNext()
    {
        if (_remaining.Length == 0)
            return false;

        var index = _remaining.IndexOf(_separator);
        if (index == -1)
        {
            Current = _remaining;
            _remaining = ReadOnlySpan<char>.Empty;
            return true;
        }

        Current = _remaining.Slice(0, index);
        _remaining = _remaining.Slice(index + 1);
        return true;
    }

    public SpanSplitEnumerator GetEnumerator() => this;
}

/// <summary>
/// Object pool for reusable objects
/// </summary>
public class ObjectPool<T> where T : class, new()
{
    private readonly Stack<T> _pool = new();
    private readonly int _maxSize;
    private readonly Action<T>? _resetAction;

    public ObjectPool(int maxSize = 100, Action<T>? resetAction = null)
    {
        _maxSize = maxSize;
        _resetAction = resetAction;
    }

    public PooledObject<T> Rent()
    {
        T? obj;
        lock (_pool)
        {
            obj = _pool.Count > 0 ? _pool.Pop() : new T();
        }

        return new PooledObject<T>(obj, this);
    }

    internal void Return(T obj)
    {
        _resetAction?.Invoke(obj);

        lock (_pool)
        {
            if (_pool.Count < _maxSize)
                _pool.Push(obj);
        }
    }
}

/// <summary>
/// RAII wrapper for pooled objects
/// </summary>
public readonly struct PooledObject<T> : IDisposable where T : class, new()
{
    private readonly T _object;
    private readonly ObjectPool<T> _pool;

    public PooledObject(T obj, ObjectPool<T> pool)
    {
        _object = obj;
        _pool = pool;
    }

    public T Value => _object;

    public void Dispose()
    {
        _pool.Return(_object);
    }
}

/// <summary>
/// String builder pool (manual implementation without ObjectPool due to constructor constraints)
/// </summary>
public static class StringBuilderPool
{
    private static readonly Stack<StringBuilder> Pool = new();
    private static readonly object Lock = new();
    private const int MaxSize = 50;

    public static StringBuilderRental Rent(int capacity = 256)
    {
        StringBuilder? sb;
        lock (Lock)
        {
            sb = Pool.Count > 0 ? Pool.Pop() : null;
        }

        sb ??= new StringBuilder(capacity);
        sb.Clear();
        sb.EnsureCapacity(capacity);

        return new StringBuilderRental(sb);
    }

    internal static void Return(StringBuilder sb)
    {
        lock (Lock)
        {
            if (Pool.Count < MaxSize)
            {
                sb.Clear();
                Pool.Push(sb);
            }
        }
    }
}

/// <summary>
/// RAII wrapper for pooled StringBuilder
/// </summary>
public readonly struct StringBuilderRental : IDisposable
{
    private readonly StringBuilder _stringBuilder;

    public StringBuilderRental(StringBuilder sb)
    {
        _stringBuilder = sb;
    }

    public StringBuilder Value => _stringBuilder;

    public void Dispose()
    {
        StringBuilderPool.Return(_stringBuilder);
    }
}

/// <summary>
/// List pool (manual implementation without ObjectPool due to constructor constraints)
/// </summary>
public static class ListPool<T>
{
    private static readonly Stack<List<T>> Pool = new();
    private static readonly object Lock = new();
    private const int MaxSize = 50;

    public static ListRental<T> Rent(int capacity = 16)
    {
        List<T>? list;
        lock (Lock)
        {
            list = Pool.Count > 0 ? Pool.Pop() : null;
        }

        list ??= new List<T>(capacity);
        list.Clear();
        if (list.Capacity < capacity)
            list.Capacity = capacity;

        return new ListRental<T>(list);
    }

    internal static void Return(List<T> list)
    {
        lock (Lock)
        {
            if (Pool.Count < MaxSize)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}

/// <summary>
/// RAII wrapper for pooled List
/// </summary>
public readonly struct ListRental<T> : IDisposable
{
    private readonly List<T> _list;

    public ListRental(List<T> list)
    {
        _list = list;
    }

    public List<T> Value => _list;

    public void Dispose()
    {
        ListPool<T>.Return(_list);
    }
}

/// <summary>
/// Dictionary pool (manual implementation without ObjectPool due to constructor constraints)
/// </summary>
public static class DictionaryPool<TKey, TValue> where TKey : notnull
{
    private static readonly Stack<Dictionary<TKey, TValue>> Pool = new();
    private static readonly object Lock = new();
    private const int MaxSize = 50;

    public static DictionaryRental<TKey, TValue> Rent(int capacity = 16)
    {
        Dictionary<TKey, TValue>? dict;
        lock (Lock)
        {
            dict = Pool.Count > 0 ? Pool.Pop() : null;
        }

        dict ??= new Dictionary<TKey, TValue>(capacity);
        dict.Clear();
        dict.EnsureCapacity(capacity);

        return new DictionaryRental<TKey, TValue>(dict);
    }

    internal static void Return(Dictionary<TKey, TValue> dict)
    {
        lock (Lock)
        {
            if (Pool.Count < MaxSize)
            {
                dict.Clear();
                Pool.Push(dict);
            }
        }
    }
}

/// <summary>
/// RAII wrapper for pooled Dictionary
/// </summary>
public readonly struct DictionaryRental<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _dictionary;

    public DictionaryRental(Dictionary<TKey, TValue> dict)
    {
        _dictionary = dict;
    }

    public Dictionary<TKey, TValue> Value => _dictionary;

    public void Dispose()
    {
        DictionaryPool<TKey, TValue>.Return(_dictionary);
    }
}

/// <summary>
/// High-performance batch processor with memory pooling
/// </summary>
public class BatchProcessor<T>
{
    private readonly int _batchSize;
    private readonly Func<ReadOnlyMemory<T>, ValueTask> _processor;

    public BatchProcessor(int batchSize, Func<ReadOnlyMemory<T>, ValueTask> processor)
    {
        _batchSize = batchSize;
        _processor = processor;
    }

    public async ValueTask ProcessAsync(IAsyncEnumerable<T> items)
    {
        using var rental = MemoryOptimization.RentArray<T>(_batchSize);
        var buffer = rental.Memory;
        var count = 0;

        await foreach (var item in items)
        {
            buffer.Span[count++] = item;

            if (count == _batchSize)
            {
                await _processor(buffer.Slice(0, count));
                count = 0;
            }
        }

        if (count > 0)
        {
            await _processor(buffer.Slice(0, count));
        }
    }
}
