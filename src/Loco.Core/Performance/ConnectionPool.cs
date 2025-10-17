using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Performance;

/// <summary>
/// High-performance connection pool for HTTP clients and other reusable resources
/// Reduces overhead by reusing connections and managing lifecycle
/// </summary>
public class ConnectionPool<T> : IDisposable where T : class
{
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetAction;
    private readonly Action<T>? _disposeAction;
    private readonly int _maxSize;
    private readonly TimeSpan _maxLifetime;
    private readonly ILogger? _logger;
    private readonly ConcurrentBag<PooledConnection<T>> _pool;
    private readonly SemaphoreSlim _semaphore;
    private int _currentSize;
    private bool _disposed;

    public ConnectionPool(
        Func<T> factory,
        int maxSize = 10,
        TimeSpan? maxLifetime = null,
        Action<T>? resetAction = null,
        Action<T>? disposeAction = null,
        ILogger? logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _maxSize = maxSize > 0 ? maxSize : throw new ArgumentOutOfRangeException(nameof(maxSize));
        _maxLifetime = maxLifetime ?? TimeSpan.FromMinutes(5);
        _resetAction = resetAction;
        _disposeAction = disposeAction;
        _logger = logger;
        _pool = new ConcurrentBag<PooledConnection<T>>();
        _semaphore = new SemaphoreSlim(maxSize, maxSize);
        _currentSize = 0;
    }

    /// <summary>
    /// Acquire connection from pool
    /// </summary>
    public async Task<PooledConnectionHandle<T>> AcquireAsync(TimeSpan? timeout = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConnectionPool<T>));

        var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);

        if (!await _semaphore.WaitAsync(actualTimeout))
        {
            throw new TimeoutException($"Failed to acquire connection within {actualTimeout.TotalSeconds}s");
        }

        try
        {
            PooledConnection<T> pooledConn;

            // Try to get from pool
            while (_pool.TryTake(out pooledConn!))
            {
                // Check if connection is still valid
                if (DateTime.UtcNow - pooledConn.CreatedAt < _maxLifetime)
                {
                    // Reset connection before reuse
                    try
                    {
                        _resetAction?.Invoke(pooledConn.Connection);
                        pooledConn.LastUsed = DateTime.UtcNow;
                        pooledConn.UseCount++;

                        _logger?.LogDebug("Reusing pooled connection (Age: {Age}s, Uses: {Uses})",
                            (DateTime.UtcNow - pooledConn.CreatedAt).TotalSeconds, pooledConn.UseCount);

                        return new PooledConnectionHandle<T>(pooledConn, this);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to reset connection, will create new one");
                        DisposeConnection(pooledConn);
                        Interlocked.Decrement(ref _currentSize);
                    }
                }
                else
                {
                    // Connection expired
                    _logger?.LogDebug("Connection expired (Age: {Age}s), creating new one",
                        (DateTime.UtcNow - pooledConn.CreatedAt).TotalSeconds);
                    DisposeConnection(pooledConn);
                    Interlocked.Decrement(ref _currentSize);
                }
            }

            // Create new connection
            var connection = _factory();
            Interlocked.Increment(ref _currentSize);

            pooledConn = new PooledConnection<T>
            {
                Connection = connection,
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow,
                UseCount = 1
            };

            _logger?.LogDebug("Created new connection (Total: {CurrentSize}/{MaxSize})",
                _currentSize, _maxSize);

            return new PooledConnectionHandle<T>(pooledConn, this);
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Return connection to pool
    /// </summary>
    internal void Return(PooledConnection<T> connection)
    {
        if (_disposed)
        {
            DisposeConnection(connection);
            Interlocked.Decrement(ref _currentSize);
        }
        else
        {
            _pool.Add(connection);
        }

        _semaphore.Release();
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            MaxSize = _maxSize,
            CurrentSize = _currentSize,
            AvailableConnections = _pool.Count,
            ActiveConnections = _currentSize - _pool.Count
        };
    }

    /// <summary>
    /// Clear all pooled connections
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out var conn))
        {
            DisposeConnection(conn);
            Interlocked.Decrement(ref _currentSize);
        }

        _logger?.LogInformation("Connection pool cleared");
    }

    private void DisposeConnection(PooledConnection<T> connection)
    {
        try
        {
            _disposeAction?.Invoke(connection.Connection);

            if (connection.Connection is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error disposing connection");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
        _semaphore.Dispose();
    }
}

/// <summary>
/// Pooled connection wrapper
/// </summary>
public class PooledConnection<T> where T : class
{
    public T Connection { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
    public int UseCount { get; set; }
}

/// <summary>
/// Handle for using pooled connection with automatic return
/// </summary>
public class PooledConnectionHandle<T> : IDisposable where T : class
{
    private readonly PooledConnection<T> _connection;
    private readonly ConnectionPool<T> _pool;
    private bool _disposed;

    internal PooledConnectionHandle(PooledConnection<T> connection, ConnectionPool<T> pool)
    {
        _connection = connection;
        _pool = pool;
    }

    public T Connection => _connection.Connection;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _pool.Return(_connection);
        }
    }
}

/// <summary>
/// Pool statistics
/// </summary>
public class PoolStatistics
{
    public int MaxSize { get; set; }
    public int CurrentSize { get; set; }
    public int AvailableConnections { get; set; }
    public int ActiveConnections { get; set; }
}

/// <summary>
/// Pre-configured HTTP client pool for optimal performance
/// </summary>
public class HttpClientPool : IDisposable
{
    private readonly ConnectionPool<HttpClient> _pool;
    private static readonly HttpClient SharedClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public HttpClientPool(int maxSize = 10, ILogger? logger = null)
    {
        _pool = new ConnectionPool<HttpClient>(
            factory: () =>
            {
                var client = new HttpClient(new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 10
                })
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                return client;
            },
            maxSize: maxSize,
            maxLifetime: TimeSpan.FromMinutes(10),
            resetAction: null, // HttpClient doesn't need reset
            disposeAction: client => client.Dispose(),
            logger: logger
        );
    }

    public async Task<PooledConnectionHandle<HttpClient>> AcquireAsync(TimeSpan? timeout = null)
    {
        return await _pool.AcquireAsync(timeout);
    }

    public PoolStatistics GetStatistics() => _pool.GetStatistics();

    public void Dispose()
    {
        _pool.Dispose();
    }
}
