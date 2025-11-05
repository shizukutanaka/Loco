// Rob Pike: "Don't communicate by sharing memory; share memory by communicating"
// John Carmack: "The most important optimization is choosing the right algorithm"

using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;

namespace Loco.Core.Practical;

/// <summary>
/// Simple database connection pool
/// No complex lifecycle management, just basic pooling that works
/// </summary>
public class SimpleConnectionPool : IDisposable
{
    private readonly string _connectionString;
    private readonly Func<DbConnection> _connectionFactory;
    private readonly ConcurrentBag<DbConnection> _pool = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxSize;
    private int _currentSize;
    private bool _disposed;

    public SimpleConnectionPool(
        string connectionString,
        Func<DbConnection> connectionFactory,
        int maxSize = 10)
    {
        _connectionString = connectionString;
        _connectionFactory = connectionFactory;
        _maxSize = maxSize;
        _semaphore = new SemaphoreSlim(maxSize, maxSize);
    }

    public async Task<IDbConnection> GetConnectionAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SimpleConnectionPool));

        await _semaphore.WaitAsync();

        try
        {
            // Try to get from pool
            if (_pool.TryTake(out var connection))
            {
                if (connection.State == ConnectionState.Open)
                    return new PooledConnection(connection, this);

                // Connection is dead, dispose it
                connection.Dispose();
                Interlocked.Decrement(ref _currentSize);
            }

            // Create new connection
            if (_currentSize < _maxSize)
            {
                var newConnection = _connectionFactory();
                newConnection.ConnectionString = _connectionString;
                await newConnection.OpenAsync();
                Interlocked.Increment(ref _currentSize);
                return new PooledConnection(newConnection, this);
            }

            throw new InvalidOperationException("Connection pool exhausted");
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    private void ReturnConnection(DbConnection connection)
    {
        if (!_disposed && connection.State == ConnectionState.Open)
        {
            _pool.Add(connection);
        }
        else
        {
            connection.Dispose();
            Interlocked.Decrement(ref _currentSize);
        }

        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        while (_pool.TryTake(out var connection))
        {
            connection.Dispose();
        }

        _semaphore.Dispose();
    }

    // Wrapper to auto-return connection to pool
    private class PooledConnection : IDbConnection
    {
        private readonly DbConnection _connection;
        private readonly SimpleConnectionPool _pool;
        private bool _disposed;

        public PooledConnection(DbConnection connection, SimpleConnectionPool pool)
        {
            _connection = connection;
            _pool = pool;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _pool.ReturnConnection(_connection);
            }
        }

        // Delegate all IDbConnection methods to inner connection
        public string ConnectionString
        {
            get => _connection.ConnectionString;
            set => _connection.ConnectionString = value;
        }

        public int ConnectionTimeout => _connection.ConnectionTimeout;
        public string Database => _connection.Database;
        public ConnectionState State => _connection.State;

        public IDbTransaction BeginTransaction() => _connection.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _connection.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _connection.ChangeDatabase(databaseName);
        public void Close() => _connection.Close();
        public IDbCommand CreateCommand() => _connection.CreateCommand();
        public void Open() => _connection.Open();
    }

    // Simple stats
    public (int poolSize, int maxSize, int available) GetStats()
    {
        return (_currentSize, _maxSize, _pool.Count);
    }
}