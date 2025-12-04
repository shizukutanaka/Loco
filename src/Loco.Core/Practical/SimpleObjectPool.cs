// John Carmack: "The resource that's most limited is human understanding"
// Rob Pike: "Make the zero value useful"

using System.Collections.Concurrent;
using System.Text;

namespace Loco.Core.Practical;

/// <summary>
/// Simple object pool - Reduce allocations, improve performance
/// Thread-safe, automatic cleanup, zero dependencies
/// </summary>
public class SimpleObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetAction;
    private readonly int _maxSize;
    private int _currentCount;
    private readonly SimpleMetrics? _metrics;

    public SimpleObjectPool(
        Func<T> factory,
        Action<T>? resetAction = null,
        int maxSize = 100,
        SimpleMetrics? metrics = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
        _maxSize = maxSize;
        _metrics = metrics;
    }

    // Rent an object from the pool
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            _metrics?.IncrementCounter("pool.hit");
            return item;
        }

        _metrics?.IncrementCounter("pool.miss");
        Interlocked.Increment(ref _currentCount);
        return _factory();
    }

    // Return an object to the pool
    public void Return(T item)
    {
        if (item == null) return;

        // Reset the object if a reset action is provided
        _resetAction?.Invoke(item);

        // Only add back to pool if under max size
        if (_currentCount <= _maxSize)
        {
            _pool.Add(item);
            _metrics?.IncrementCounter("pool.return");
        }
        else
        {
            // Let GC collect it
            Interlocked.Decrement(ref _currentCount);
            _metrics?.IncrementCounter("pool.discard");
        }
    }

    // Get pool statistics
    public (int pooled, int total, int maxSize) GetStats()
    {
        return (_pool.Count, _currentCount, _maxSize);
    }

    // Clear the pool
    public void Clear()
    {
        while (_pool.TryTake(out _))
        {
            Interlocked.Decrement(ref _currentCount);
        }
    }
}

/// <summary>
/// Pooled object wrapper - Automatically returns to pool on dispose
/// </summary>
public struct PooledObject<T> : IDisposable where T : class
{
    private readonly SimpleObjectPool<T> _pool;
    private T? _item;

    public T Value => _item ?? throw new ObjectDisposedException(nameof(PooledObject<T>));

    internal PooledObject(SimpleObjectPool<T> pool, T item)
    {
        _pool = pool;
        _item = item;
    }

    public void Dispose()
    {
        var item = _item;
        if (item != null)
        {
            _item = null;
            _pool.Return(item);
        }
    }
}

/// <summary>
/// Object pool with automatic disposal
/// </summary>
public class AutoReturnObjectPool<T> where T : class
{
    private readonly SimpleObjectPool<T> _pool;

    public AutoReturnObjectPool(
        Func<T> factory,
        Action<T>? resetAction = null,
        int maxSize = 100)
    {
        _pool = new SimpleObjectPool<T>(factory, resetAction, maxSize);
    }

    // Rent with automatic return on disposal
    public PooledObject<T> Rent()
    {
        var item = _pool.Rent();
        return new PooledObject<T>(_pool, item);
    }

    public (int pooled, int total, int maxSize) GetStats() => _pool.GetStats();
    public void Clear() => _pool.Clear();
}

/// <summary>
/// Common pooled objects
/// </summary>
public static class CommonPools
{
    // StringBuilder pool
    private static readonly SimpleObjectPool<StringBuilder> _stringBuilderPool =
        new SimpleObjectPool<StringBuilder>(
            factory: () => new StringBuilder(),
            resetAction: sb => sb.Clear(),
            maxSize: 50);

    public static StringBuilder RentStringBuilder() => _stringBuilderPool.Rent();
    public static void ReturnStringBuilder(StringBuilder sb) => _stringBuilderPool.Return(sb);

    // List<T> pool factory
    public static SimpleObjectPool<List<T>> CreateListPool<T>(int maxSize = 50)
    {
        return new SimpleObjectPool<List<T>>(
            factory: () => new List<T>(),
            resetAction: list => list.Clear(),
            maxSize: maxSize);
    }

    // Dictionary<TKey, TValue> pool factory
    public static SimpleObjectPool<Dictionary<TKey, TValue>> CreateDictionaryPool<TKey, TValue>(int maxSize = 50)
        where TKey : notnull
    {
        return new SimpleObjectPool<Dictionary<TKey, TValue>>(
            factory: () => new Dictionary<TKey, TValue>(),
            resetAction: dict => dict.Clear(),
            maxSize: maxSize);
    }

    // MemoryStream pool
    private static readonly SimpleObjectPool<MemoryStream> _memoryStreamPool =
        new SimpleObjectPool<MemoryStream>(
            factory: () => new MemoryStream(),
            resetAction: ms =>
            {
                ms.Position = 0;
                ms.SetLength(0);
            },
            maxSize: 20);

    public static MemoryStream RentMemoryStream() => _memoryStreamPool.Rent();
    public static void ReturnMemoryStream(MemoryStream ms) => _memoryStreamPool.Return(ms);
}

/// <summary>
/// Array pool - Special case for arrays
/// </summary>
public class SimpleArrayPool<T>
{
    private readonly ConcurrentDictionary<int, ConcurrentBag<T[]>> _buckets = new();
    private readonly int _maxArrayLength;
    private readonly int _maxPoolSize;

    public SimpleArrayPool(int maxArrayLength = 1024 * 1024, int maxPoolSize = 50)
    {
        _maxArrayLength = maxArrayLength;
        _maxPoolSize = maxPoolSize;
    }

    public T[] Rent(int minimumLength)
    {
        if (minimumLength <= 0) throw new ArgumentOutOfRangeException(nameof(minimumLength));
        if (minimumLength > _maxArrayLength) return new T[minimumLength];

        // Round up to next power of 2
        var size = GetBucketSize(minimumLength);

        var bucket = _buckets.GetOrAdd(size, _ => new ConcurrentBag<T[]>());

        if (bucket.TryTake(out var array))
        {
            return array;
        }

        return new T[size];
    }

    public void Return(T[] array, bool clearArray = false)
    {
        if (array == null) return;
        if (array.Length > _maxArrayLength) return;

        if (clearArray)
        {
            Array.Clear(array, 0, array.Length);
        }

        var size = GetBucketSize(array.Length);
        var bucket = _buckets.GetOrAdd(size, _ => new ConcurrentBag<T[]>());

        if (bucket.Count < _maxPoolSize)
        {
            bucket.Add(array);
        }
    }

    private static int GetBucketSize(int length)
    {
        // Round up to next power of 2
        if (length <= 16) return 16;
        if (length <= 32) return 32;
        if (length <= 64) return 64;
        if (length <= 128) return 128;
        if (length <= 256) return 256;
        if (length <= 512) return 512;
        if (length <= 1024) return 1024;
        if (length <= 2048) return 2048;
        if (length <= 4096) return 4096;
        if (length <= 8192) return 8192;
        if (length <= 16384) return 16384;
        if (length <= 32768) return 32768;
        if (length <= 65536) return 65536;
        return 131072;
    }
}

/// <summary>
/// Example: Database connection pool
/// </summary>
public class ConnectionPool<TConnection> where TConnection : class
{
    private readonly AutoReturnObjectPool<TConnection> _pool;
    private readonly SimpleLogger _logger;

    public ConnectionPool(
        Func<TConnection> connectionFactory,
        Action<TConnection>? resetConnection = null,
        int maxSize = 20,
        SimpleLogger? logger = null)
    {
        _pool = new AutoReturnObjectPool<TConnection>(
            connectionFactory,
            resetConnection,
            maxSize);

        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(ConnectionPool<TConnection>));
    }

    public async Task<T> ExecuteAsync<T>(Func<TConnection, Task<T>> operation)
    {
        using var pooled = _pool.Rent();

        try
        {
            return await operation(pooled.Value);
        }
        catch (Exception ex)
        {
            _logger.Error("Connection operation failed", ex);
            throw;
        }
    }

    public (int available, int total) GetPoolStats()
    {
        var (pooled, total, _) = _pool.GetStats();
        return (pooled, total);
    }
}

/// <summary>
/// Example: Buffer pool for I/O operations
/// </summary>
public static class BufferPool
{
    private static readonly SimpleArrayPool<byte> _byteArrayPool = new();

    public static byte[] RentBuffer(int minimumSize) => _byteArrayPool.Rent(minimumSize);
    public static void ReturnBuffer(byte[] buffer) => _byteArrayPool.Return(buffer, clearArray: true);

    // Helper for temporary buffer usage
    public static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        var buffer = RentBuffer(4096);
        try
        {
            using var ms = CommonPools.RentMemoryStream();
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, bytesRead);
            }
            var result = ms.ToArray();
            CommonPools.ReturnMemoryStream(ms);
            return result;
        }
        finally
        {
            ReturnBuffer(buffer);
        }
    }
}

/// <summary>
/// Example: Worker thread pool
/// </summary>
public class WorkerPool
{
    private readonly SimpleObjectPool<Worker> _pool;
    private readonly SimpleLogger _logger;

    public WorkerPool(int maxWorkers = 10, SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(WorkerPool));

        _pool = new SimpleObjectPool<Worker>(
            factory: () => new Worker(_logger),
            resetAction: w => w.Reset(),
            maxSize: maxWorkers);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work)
    {
        var worker = _pool.Rent();
        try
        {
            return await worker.ExecuteAsync(work);
        }
        finally
        {
            _pool.Return(worker);
        }
    }

    public class Worker
    {
        private readonly SimpleLogger _logger;
        private int _taskCount;

        public Worker(SimpleLogger logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> work)
        {
            _taskCount++;
            _logger.Debug($"Worker executing task #{_taskCount}");
            return await work();
        }

        public void Reset()
        {
            // Reset any worker state if needed
        }
    }
}