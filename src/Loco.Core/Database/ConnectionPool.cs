using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using MySqlConnector;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Database;

/// <summary>
/// High-performance database connection pool with automatic management
/// </summary>
public sealed class ConnectionPool : IDisposable
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;
    private readonly ConcurrentBag<PooledConnection> _availableConnections;
    private readonly ConcurrentDictionary<int, PooledConnection> _activeConnections;
    private readonly SemaphoreSlim _semaphore;
    private readonly Timer _maintenanceTimer;
    private readonly ILogger<ConnectionPool> _logger;
    
    private int _currentSize;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _idleTimeout;
    private bool _disposed;
    
    // Performance counters
    private long _totalRequests;
    private long _totalHits;
    private long _totalMisses;
    private long _totalTimeouts;
    private long _totalErrors;
    
    public ConnectionPool(
        string connectionString,
        DatabaseProvider provider,
        int minSize = 5,
        int maxSize = 100,
        TimeSpan? connectionTimeout = null,
        TimeSpan? idleTimeout = null,
        ILogger<ConnectionPool> logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _provider = provider;
        _minSize = Math.Max(1, minSize);
        _maxSize = Math.Max(_minSize, maxSize);
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(30);
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
        _logger = logger;
        
        _availableConnections = new ConcurrentBag<PooledConnection>();
        _activeConnections = new ConcurrentDictionary<int, PooledConnection>();
        _semaphore = new SemaphoreSlim(_maxSize, _maxSize);
        
        // Initialize minimum connections
        Task.Run(() => InitializeMinimumConnections());
        
        // Start maintenance timer
        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    
    /// <summary>
    /// Get a connection from the pool
    /// </summary>
    public async Task<IDbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConnectionPool));
        
        Interlocked.Increment(ref _totalRequests);
        
        // Wait for available slot
        if (!await _semaphore.WaitAsync(_connectionTimeout, cancellationToken))
        {
            Interlocked.Increment(ref _totalTimeouts);
            throw new TimeoutException($"Failed to acquire connection within {_connectionTimeout}");
        }
        
        try
        {
            PooledConnection pooledConnection = null;
            
            // Try to get existing connection
            while (_availableConnections.TryTake(out pooledConnection))
            {
                if (IsConnectionValid(pooledConnection))
                {
                    Interlocked.Increment(ref _totalHits);
                    break;
                }
                
                // Dispose invalid connection
                DisposeConnection(pooledConnection);
                pooledConnection = null;
            }
            
            // Create new connection if needed
            if (pooledConnection == null)
            {
                Interlocked.Increment(ref _totalMisses);
                pooledConnection = await CreateConnectionAsync(cancellationToken);
                Interlocked.Increment(ref _currentSize);
            }
            
            // Mark as active
            pooledConnection.LastUsedTime = DateTime.UtcNow;
            pooledConnection.UsageCount++;
            _activeConnections[pooledConnection.Id] = pooledConnection;
            
            // Return wrapper that returns to pool on dispose
            return new PooledConnectionWrapper(this, pooledConnection);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalErrors);
            _semaphore.Release();
            _logger?.LogError(ex, "Error getting connection from pool");
            throw;
        }
    }
    
    /// <summary>
    /// Return connection to pool
    /// </summary>
    internal void ReturnConnection(PooledConnection connection)
    {
        if (_disposed || connection == null)
            return;
        
        try
        {
            _activeConnections.TryRemove(connection.Id, out _);
            
            if (IsConnectionValid(connection) && _currentSize <= _maxSize)
            {
                connection.LastUsedTime = DateTime.UtcNow;
                _availableConnections.Add(connection);
            }
            else
            {
                DisposeConnection(connection);
                Interlocked.Decrement(ref _currentSize);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    /// <summary>
    /// Execute a query with automatic connection management
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<IDbConnection, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(cancellationToken);
        return await operation(connection);
    }
    
    /// <summary>
    /// Execute a command with automatic connection management
    /// </summary>
    public async Task ExecuteAsync(Func<IDbConnection, Task> operation, CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync(cancellationToken);
        await operation(connection);
    }
    
    /// <summary>
    /// Get pool statistics
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            CurrentSize = _currentSize,
            AvailableConnections = _availableConnections.Count,
            ActiveConnections = _activeConnections.Count,
            MinSize = _minSize,
            MaxSize = _maxSize,
            TotalRequests = _totalRequests,
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            TotalTimeouts = _totalTimeouts,
            TotalErrors = _totalErrors,
            HitRate = _totalRequests > 0 ? (double)_totalHits / _totalRequests : 0
        };
    }
    
    private async Task InitializeMinimumConnections()
    {
        var tasks = new Task[_minSize];
        for (int i = 0; i < _minSize; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var connection = await CreateConnectionAsync();
                    connection.LastUsedTime = DateTime.UtcNow;
                    _availableConnections.Add(connection);
                    Interlocked.Increment(ref _currentSize);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to create initial connection");
                }
            });
        }
        
        await Task.WhenAll(tasks);
        _logger?.LogInformation("Initialized connection pool with {Count} connections", _currentSize);
    }
    
    private async Task<PooledConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = CreateDbConnection();
        connection.ConnectionString = _connectionString;
        
        await connection.OpenAsync(cancellationToken);
        
        return new PooledConnection
        {
            Id = Guid.NewGuid().GetHashCode(),
            Connection = connection,
            CreatedTime = DateTime.UtcNow,
            LastUsedTime = DateTime.UtcNow
        };
    }
    
    private DbConnection CreateDbConnection()
    {
        return _provider switch
        {
            DatabaseProvider.SqlServer => new SqlConnection(),
            DatabaseProvider.PostgreSQL => new NpgsqlConnection(),
            DatabaseProvider.MySQL => new MySqlConnection(),
            DatabaseProvider.SQLite => new SqliteConnection(),
            _ => throw new NotSupportedException($"Provider {_provider} not supported")
        };
    }
    
    private bool IsConnectionValid(PooledConnection pooledConnection)
    {
        if (pooledConnection?.Connection == null)
            return false;
        
        // Check if connection is still open
        if (pooledConnection.Connection.State != ConnectionState.Open)
            return false;
        
        // Check idle timeout
        if (DateTime.UtcNow - pooledConnection.LastUsedTime > _idleTimeout)
            return false;
        
        // Check max usage count
        if (pooledConnection.UsageCount > 1000)
            return false;
        
        // Perform provider-specific validation
        try
        {
            using var cmd = pooledConnection.Connection.CreateCommand();
            cmd.CommandText = GetValidationQuery();
            cmd.CommandTimeout = 1;
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private string GetValidationQuery()
    {
        return _provider switch
        {
            DatabaseProvider.SqlServer => "SELECT 1",
            DatabaseProvider.PostgreSQL => "SELECT 1",
            DatabaseProvider.MySQL => "SELECT 1",
            DatabaseProvider.SQLite => "SELECT 1",
            _ => "SELECT 1"
        };
    }
    
    private void DisposeConnection(PooledConnection pooledConnection)
    {
        try
        {
            pooledConnection?.Connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error disposing connection");
        }
    }
    
    private void PerformMaintenance(object state)
    {
        if (_disposed)
            return;
        
        try
        {
            // Remove idle connections above minimum
            while (_currentSize > _minSize && _availableConnections.TryTake(out var connection))
            {
                if (DateTime.UtcNow - connection.LastUsedTime > _idleTimeout)
                {
                    DisposeConnection(connection);
                    Interlocked.Decrement(ref _currentSize);
                }
                else
                {
                    _availableConnections.Add(connection);
                }
            }
            
            // Ensure minimum connections
            if (_currentSize < _minSize)
            {
                _ = Task.Run(() => InitializeMinimumConnections());
            }
            
            _logger?.LogDebug("Pool maintenance completed. Current size: {Size}", _currentSize);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during pool maintenance");
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _maintenanceTimer?.Dispose();
        
        // Dispose all connections
        while (_availableConnections.TryTake(out var connection))
        {
            DisposeConnection(connection);
        }
        
        foreach (var connection in _activeConnections.Values)
        {
            DisposeConnection(connection);
        }
        
        _activeConnections.Clear();
        _semaphore?.Dispose();
    }
}

/// <summary>
/// Pooled connection wrapper
/// </summary>
internal sealed class PooledConnectionWrapper : IDbConnection
{
    private readonly ConnectionPool _pool;
    private readonly PooledConnection _pooledConnection;
    private bool _disposed;
    
    public PooledConnectionWrapper(ConnectionPool pool, PooledConnection pooledConnection)
    {
        _pool = pool;
        _pooledConnection = pooledConnection;
    }
    
    public IDbConnection Connection => _pooledConnection.Connection;
    
    public string ConnectionString
    {
        get => Connection.ConnectionString;
        set => Connection.ConnectionString = value;
    }
    
    public int ConnectionTimeout => Connection.ConnectionTimeout;
    public string Database => Connection.Database;
    public ConnectionState State => Connection.State;
    
    public IDbTransaction BeginTransaction() => Connection.BeginTransaction();
    public IDbTransaction BeginTransaction(IsolationLevel il) => Connection.BeginTransaction(il);
    public void ChangeDatabase(string databaseName) => Connection.ChangeDatabase(databaseName);
    public void Close() => Dispose();
    public IDbCommand CreateCommand() => Connection.CreateCommand();
    public void Open() => Connection.Open();
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _pool.ReturnConnection(_pooledConnection);
    }
}

/// <summary>
/// Pooled connection metadata
/// </summary>
internal sealed class PooledConnection
{
    public int Id { get; set; }
    public DbConnection Connection { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastUsedTime { get; set; }
    public int UsageCount { get; set; }
}

/// <summary>
/// Database providers
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSQL,
    MySQL,
    SQLite
}

/// <summary>
/// Pool statistics
/// </summary>
public sealed class PoolStatistics
{
    public int CurrentSize { get; set; }
    public int AvailableConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int MinSize { get; set; }
    public int MaxSize { get; set; }
    public long TotalRequests { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalTimeouts { get; set; }
    public long TotalErrors { get; set; }
    public double HitRate { get; set; }
}

/// <summary>
/// Connection pool manager for multiple databases
/// </summary>
public sealed class ConnectionPoolManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ConnectionPool> _pools;
    private readonly ILogger<ConnectionPoolManager> _logger;
    private bool _disposed;
    
    public ConnectionPoolManager(ILogger<ConnectionPoolManager> logger = null)
    {
        _pools = new ConcurrentDictionary<string, ConnectionPool>();
        _logger = logger;
    }
    
    /// <summary>
    /// Register a connection pool
    /// </summary>
    public void RegisterPool(string name, string connectionString, DatabaseProvider provider, int minSize = 5, int maxSize = 100)
    {
        if (_pools.ContainsKey(name))
            throw new InvalidOperationException($"Pool '{name}' already registered");
        
        var pool = new ConnectionPool(connectionString, provider, minSize, maxSize);
        _pools[name] = pool;
        
        _logger?.LogInformation("Registered connection pool '{Name}' with provider {Provider}", name, provider);
    }
    
    /// <summary>
    /// Get a connection pool
    /// </summary>
    public ConnectionPool GetPool(string name)
    {
        if (!_pools.TryGetValue(name, out var pool))
            throw new InvalidOperationException($"Pool '{name}' not found");
        
        return pool;
    }
    
    /// <summary>
    /// Get connection from named pool
    /// </summary>
    public async Task<IDbConnection> GetConnectionAsync(string poolName, CancellationToken cancellationToken = default)
    {
        var pool = GetPool(poolName);
        return await pool.GetConnectionAsync(cancellationToken);
    }
    
    /// <summary>
    /// Get all pool statistics
    /// </summary>
    public Dictionary<string, PoolStatistics> GetAllStatistics()
    {
        var stats = new Dictionary<string, PoolStatistics>();
        
        foreach (var kvp in _pools)
        {
            stats[kvp.Key] = kvp.Value.GetStatistics();
        }
        
        return stats;
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        foreach (var pool in _pools.Values)
        {
            pool.Dispose();
        }
        
        _pools.Clear();
    }
}
