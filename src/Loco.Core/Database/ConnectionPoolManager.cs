using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace Loco.Core.Database
{
    public interface IConnectionPoolManager
    {
        Task<IDbConnection> GetConnectionAsync(string connectionName = "default");
        Task<T> ExecuteAsync<T>(Func<IDbConnection, Task<T>> operation, string connectionName = "default");
        Task ExecuteAsync(Func<IDbConnection, Task> operation, string connectionName = "default");
        void ConfigurePool(string connectionName, ConnectionPoolOptions options);
        ConnectionPoolStatistics GetStatistics(string connectionName = "default");
        Task<Dictionary<string, ConnectionPoolStatistics>> GetAllStatisticsAsync();
        Task WarmupPoolAsync(string connectionName = "default", int connections = 5);
        void ClearPool(string connectionName = "default");
        void ClearAllPools();
    }

    public class ConnectionPoolManager : IConnectionPoolManager, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConnectionPoolManager> _logger;
        private readonly ConcurrentDictionary<string, ConnectionPool> _pools;
        private readonly Timer _monitoringTimer;
        private readonly Timer _cleanupTimer;

        public ConnectionPoolManager(
            IConfiguration configuration,
            ILogger<ConnectionPoolManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _pools = new ConcurrentDictionary<string, ConnectionPool>();
            
            InitializePools();
            
            _monitoringTimer = new Timer(MonitorPools, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _cleanupTimer = new Timer(CleanupIdleConnections, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private void InitializePools()
        {
            var connectionStrings = _configuration.GetSection("ConnectionStrings").GetChildren();
            
            foreach (var connString in connectionStrings)
            {
                var name = connString.Key;
                var connectionString = connString.Value;
                var provider = _configuration[$"DatabaseProviders:{name}"] ?? DetectProvider(connectionString);
                
                var options = new ConnectionPoolOptions
                {
                    ConnectionString = connectionString,
                    Provider = provider,
                    MinPoolSize = _configuration.GetValue<int>($"ConnectionPools:{name}:MinPoolSize", 5),
                    MaxPoolSize = _configuration.GetValue<int>($"ConnectionPools:{name}:MaxPoolSize", 100),
                    ConnectionTimeout = TimeSpan.FromSeconds(_configuration.GetValue<int>($"ConnectionPools:{name}:ConnectionTimeoutSeconds", 30)),
                    IdleTimeout = TimeSpan.FromMinutes(_configuration.GetValue<int>($"ConnectionPools:{name}:IdleTimeoutMinutes", 10)),
                    MaxRetries = _configuration.GetValue<int>($"ConnectionPools:{name}:MaxRetries", 3),
                    EnableStatistics = _configuration.GetValue<bool>($"ConnectionPools:{name}:EnableStatistics", true)
                };
                
                ConfigurePool(name, options);
                _logger.LogInformation("Initialized connection pool '{Name}' with provider {Provider}", name, provider);
            }
        }

        private string DetectProvider(string connectionString)
        {
            var lowerConn = connectionString.ToLower();
            
            if (lowerConn.Contains("server=") && lowerConn.Contains("database="))
                return "SqlServer";
            if (lowerConn.Contains("host=") && lowerConn.Contains("database="))
                return "PostgreSQL";
            if (lowerConn.Contains("server=") && lowerConn.Contains("uid="))
                return "MySQL";
            if (lowerConn.Contains("data source=") && lowerConn.Contains(".db"))
                return "SQLite";
                
            return "SqlServer";
        }

        public void ConfigurePool(string connectionName, ConnectionPoolOptions options)
        {
            var pool = new ConnectionPool(connectionName, options, _logger);
            _pools[connectionName] = pool;
            
            UpdateProviderSpecificSettings(options);
        }

        private void UpdateProviderSpecificSettings(ConnectionPoolOptions options)
        {
            switch (options.Provider.ToLower())
            {
                case "sqlserver":
                    var sqlBuilder = new SqlConnectionStringBuilder(options.ConnectionString)
                    {
                        MinPoolSize = options.MinPoolSize,
                        MaxPoolSize = options.MaxPoolSize,
                        ConnectTimeout = (int)options.ConnectionTimeout.TotalSeconds,
                        Pooling = true,
                        MultipleActiveResultSets = true,
                        ApplicationName = "LocoApp"
                    };
                    options.ConnectionString = sqlBuilder.ToString();
                    break;
                    
                case "postgresql":
                    var npgsqlBuilder = new NpgsqlConnectionStringBuilder(options.ConnectionString)
                    {
                        MinPoolSize = options.MinPoolSize,
                        MaxPoolSize = options.MaxPoolSize,
                        ConnectionIdleLifetime = (int)options.IdleTimeout.TotalSeconds,
                        Timeout = (int)options.ConnectionTimeout.TotalSeconds,
                        Pooling = true,
                        ApplicationName = "LocoApp"
                    };
                    options.ConnectionString = npgsqlBuilder.ToString();
                    break;
                    
                case "mysql":
                    var mysqlBuilder = new MySqlConnectionStringBuilder(options.ConnectionString)
                    {
                        MinimumPoolSize = (uint)options.MinPoolSize,
                        MaximumPoolSize = (uint)options.MaxPoolSize,
                        ConnectionTimeout = (uint)options.ConnectionTimeout.TotalSeconds,
                        ConnectionIdleTimeout = (uint)options.IdleTimeout.TotalSeconds,
                        Pooling = true,
                        ApplicationName = "LocoApp"
                    };
                    options.ConnectionString = mysqlBuilder.ToString();
                    break;
                    
                case "sqlite":
                    var sqliteBuilder = new SqliteConnectionStringBuilder(options.ConnectionString)
                    {
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        Cache = SqliteCacheMode.Shared,
                        Pooling = true
                    };
                    options.ConnectionString = sqliteBuilder.ToString();
                    break;
            }
        }

        public async Task<IDbConnection> GetConnectionAsync(string connectionName = "default")
        {
            if (!_pools.TryGetValue(connectionName, out var pool))
            {
                throw new InvalidOperationException($"Connection pool '{connectionName}' not found");
            }
            
            return await pool.GetConnectionAsync();
        }

        public async Task<T> ExecuteAsync<T>(Func<IDbConnection, Task<T>> operation, string connectionName = "default")
        {
            var connection = await GetConnectionAsync(connectionName);
            try
            {
                return await operation(connection);
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();
                    
                if (_pools.TryGetValue(connectionName, out var pool))
                {
                    pool.ReturnConnection(connection);
                }
            }
        }

        public async Task ExecuteAsync(Func<IDbConnection, Task> operation, string connectionName = "default")
        {
            await ExecuteAsync<object>(async conn =>
            {
                await operation(conn);
                return null;
            }, connectionName);
        }

        public ConnectionPoolStatistics GetStatistics(string connectionName = "default")
        {
            if (_pools.TryGetValue(connectionName, out var pool))
            {
                return pool.GetStatistics();
            }
            
            return new ConnectionPoolStatistics { PoolName = connectionName };
        }

        public async Task<Dictionary<string, ConnectionPoolStatistics>> GetAllStatisticsAsync()
        {
            var stats = new Dictionary<string, ConnectionPoolStatistics>();
            
            foreach (var kvp in _pools)
            {
                stats[kvp.Key] = kvp.Value.GetStatistics();
            }
            
            return await Task.FromResult(stats);
        }

        public async Task WarmupPoolAsync(string connectionName = "default", int connections = 5)
        {
            if (!_pools.TryGetValue(connectionName, out var pool))
            {
                throw new InvalidOperationException($"Connection pool '{connectionName}' not found");
            }
            
            _logger.LogInformation("Warming up connection pool '{Name}' with {Count} connections", 
                connectionName, connections);
            
            var tasks = new List<Task>();
            for (int i = 0; i < connections; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var conn = await pool.GetConnectionAsync();
                    await Task.Delay(100);
                    pool.ReturnConnection(conn);
                }));
            }
            
            await Task.WhenAll(tasks);
            _logger.LogInformation("Connection pool '{Name}' warmup completed", connectionName);
        }

        public void ClearPool(string connectionName = "default")
        {
            if (_pools.TryGetValue(connectionName, out var pool))
            {
                pool.Clear();
                _logger.LogInformation("Cleared connection pool '{Name}'", connectionName);
            }
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _logger.LogInformation("Cleared all connection pools");
        }

        private void MonitorPools(object state)
        {
            try
            {
                foreach (var kvp in _pools)
                {
                    var stats = kvp.Value.GetStatistics();
                    
                    if (stats.ActiveConnections > stats.MaxPoolSize * 0.9)
                    {
                        _logger.LogWarning("Connection pool '{Name}' is near capacity: {Active}/{Max}", 
                            kvp.Key, stats.ActiveConnections, stats.MaxPoolSize);
                    }
                    
                    if (stats.FailedConnections > 10)
                    {
                        _logger.LogError("Connection pool '{Name}' has {Failed} failed connections", 
                            kvp.Key, stats.FailedConnections);
                    }
                    
                    _logger.LogDebug("Pool '{Name}' stats: Active={Active}, Idle={Idle}, Total={Total}", 
                        kvp.Key, stats.ActiveConnections, stats.IdleConnections, stats.TotalConnections);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring connection pools");
            }
        }

        private void CleanupIdleConnections(object state)
        {
            try
            {
                foreach (var pool in _pools.Values)
                {
                    var cleaned = pool.CleanupIdleConnections();
                    if (cleaned > 0)
                    {
                        _logger.LogInformation("Cleaned up {Count} idle connections from pool '{Name}'", 
                            cleaned, pool.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up idle connections");
            }
        }

        public void Dispose()
        {
            _monitoringTimer?.Dispose();
            _cleanupTimer?.Dispose();
            
            foreach (var pool in _pools.Values)
            {
                pool.Dispose();
            }
        }
    }

    internal class ConnectionPool : IDisposable
    {
        private readonly string _name;
        private readonly ConnectionPoolOptions _options;
        private readonly ILogger _logger;
        private readonly ConcurrentBag<PooledConnection> _idleConnections;
        private readonly ConcurrentDictionary<string, PooledConnection> _activeConnections;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConnectionPoolStatistics _statistics;
        private int _totalCreated;

        public string Name => _name;

        public ConnectionPool(string name, ConnectionPoolOptions options, ILogger logger)
        {
            _name = name;
            _options = options;
            _logger = logger;
            _idleConnections = new ConcurrentBag<PooledConnection>();
            _activeConnections = new ConcurrentDictionary<string, PooledConnection>();
            _semaphore = new SemaphoreSlim(options.MaxPoolSize, options.MaxPoolSize);
            _statistics = new ConnectionPoolStatistics
            {
                PoolName = name,
                MinPoolSize = options.MinPoolSize,
                MaxPoolSize = options.MaxPoolSize
            };
        }

        public async Task<IDbConnection> GetConnectionAsync()
        {
            await _semaphore.WaitAsync(_options.ConnectionTimeout);
            
            try
            {
                _statistics.IncrementRequests();
                
                PooledConnection pooledConn = null;
                
                while (_idleConnections.TryTake(out pooledConn))
                {
                    if (!pooledConn.IsExpired(_options.IdleTimeout) && await ValidateConnection(pooledConn.Connection))
                    {
                        break;
                    }
                    
                    pooledConn.Connection?.Dispose();
                    pooledConn = null;
                }
                
                if (pooledConn == null)
                {
                    var connection = await CreateConnectionAsync();
                    pooledConn = new PooledConnection
                    {
                        Id = Guid.NewGuid().ToString(),
                        Connection = connection,
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow
                    };
                    
                    Interlocked.Increment(ref _totalCreated);
                    _statistics.IncrementCreated();
                }
                
                pooledConn.LastUsedAt = DateTime.UtcNow;
                _activeConnections[pooledConn.Id] = pooledConn;
                _statistics.UpdateActive(_activeConnections.Count);
                
                return pooledConn.Connection;
            }
            catch (Exception ex)
            {
                _statistics.IncrementFailed();
                _logger.LogError(ex, "Failed to get connection from pool '{Name}'", _name);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<IDbConnection> CreateConnectionAsync()
        {
            IDbConnection connection = _options.Provider.ToLower() switch
            {
                "sqlserver" => new SqlConnection(_options.ConnectionString),
                "postgresql" => new NpgsqlConnection(_options.ConnectionString),
                "mysql" => new MySqlConnection(_options.ConnectionString),
                "sqlite" => new SqliteConnection(_options.ConnectionString),
                _ => throw new NotSupportedException($"Provider {_options.Provider} not supported")
            };

            for (int retry = 0; retry <= _options.MaxRetries; retry++)
            {
                try
                {
                    await ((DbConnection)connection).OpenAsync();
                    _logger.LogDebug("Created new connection for pool '{Name}'", _name);
                    return connection;
                }
                catch (Exception ex) when (retry < _options.MaxRetries)
                {
                    _logger.LogWarning(ex, "Failed to create connection, retry {Retry}/{Max}", 
                        retry + 1, _options.MaxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retry)));
                }
            }
            
            throw new InvalidOperationException($"Failed to create connection after {_options.MaxRetries} retries");
        }

        private async Task<bool> ValidateConnection(IDbConnection connection)
        {
            if (connection == null || connection.State != ConnectionState.Open)
                return false;
                
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = _options.Provider.ToLower() switch
                {
                    "sqlserver" => "SELECT 1",
                    "postgresql" => "SELECT 1",
                    "mysql" => "SELECT 1",
                    "sqlite" => "SELECT 1",
                    _ => "SELECT 1"
                };
                command.CommandTimeout = 1;
                
                await ((DbCommand)command).ExecuteScalarAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ReturnConnection(IDbConnection connection)
        {
            var pooledConn = _activeConnections.Values.FirstOrDefault(pc => pc.Connection == connection);
            if (pooledConn != null)
            {
                _activeConnections.TryRemove(pooledConn.Id, out _);
                
                if (connection.State == ConnectionState.Open && _idleConnections.Count < _options.MaxPoolSize)
                {
                    pooledConn.LastUsedAt = DateTime.UtcNow;
                    _idleConnections.Add(pooledConn);
                    _statistics.UpdateIdle(_idleConnections.Count);
                }
                else
                {
                    connection.Dispose();
                    Interlocked.Decrement(ref _totalCreated);
                }
                
                _statistics.UpdateActive(_activeConnections.Count);
            }
        }

        public int CleanupIdleConnections()
        {
            var cleaned = 0;
            var toKeep = new List<PooledConnection>();
            
            while (_idleConnections.TryTake(out var pooledConn))
            {
                if (pooledConn.IsExpired(_options.IdleTimeout))
                {
                    pooledConn.Connection?.Dispose();
                    cleaned++;
                    Interlocked.Decrement(ref _totalCreated);
                }
                else if (_idleConnections.Count > _options.MinPoolSize)
                {
                    pooledConn.Connection?.Dispose();
                    cleaned++;
                    Interlocked.Decrement(ref _totalCreated);
                }
                else
                {
                    toKeep.Add(pooledConn);
                }
            }
            
            foreach (var conn in toKeep)
            {
                _idleConnections.Add(conn);
            }
            
            _statistics.UpdateIdle(_idleConnections.Count);
            return cleaned;
        }

        public ConnectionPoolStatistics GetStatistics()
        {
            _statistics.TotalConnections = _totalCreated;
            _statistics.ActiveConnections = _activeConnections.Count;
            _statistics.IdleConnections = _idleConnections.Count;
            return _statistics.Clone();
        }

        public void Clear()
        {
            foreach (var conn in _activeConnections.Values)
            {
                conn.Connection?.Dispose();
            }
            _activeConnections.Clear();
            
            while (_idleConnections.TryTake(out var conn))
            {
                conn.Connection?.Dispose();
            }
            
            _totalCreated = 0;
            _statistics.Reset();
        }

        public void Dispose()
        {
            Clear();
            _semaphore?.Dispose();
        }
    }

    internal class PooledConnection
    {
        public string Id { get; set; }
        public IDbConnection Connection { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
        
        public bool IsExpired(TimeSpan idleTimeout)
        {
            return DateTime.UtcNow - LastUsedAt > idleTimeout;
        }
    }

    public class ConnectionPoolOptions
    {
        public string ConnectionString { get; set; }
        public string Provider { get; set; }
        public int MinPoolSize { get; set; } = 5;
        public int MaxPoolSize { get; set; } = 100;
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public int MaxRetries { get; set; } = 3;
        public bool EnableStatistics { get; set; } = true;
    }

    public class ConnectionPoolStatistics
    {
        private long _totalRequests;
        private long _totalCreated;
        private long _failedConnections;

        public string PoolName { get; set; }
        public int MinPoolSize { get; set; }
        public int MaxPoolSize { get; set; }
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public long TotalRequests => _totalRequests;
        public long TotalCreated => _totalCreated;
        public long FailedConnections => _failedConnections;
        public double UtilizationPercent => MaxPoolSize > 0 ? (double)ActiveConnections / MaxPoolSize * 100 : 0;

        public void IncrementRequests() => Interlocked.Increment(ref _totalRequests);
        public void IncrementCreated() => Interlocked.Increment(ref _totalCreated);
        public void IncrementFailed() => Interlocked.Increment(ref _failedConnections);
        
        public void UpdateActive(int count) => ActiveConnections = count;
        public void UpdateIdle(int count) => IdleConnections = count;
        
        public void Reset()
        {
            _totalRequests = 0;
            _totalCreated = 0;
            _failedConnections = 0;
            TotalConnections = 0;
            ActiveConnections = 0;
            IdleConnections = 0;
        }
        
        public ConnectionPoolStatistics Clone()
        {
            return new ConnectionPoolStatistics
            {
                PoolName = PoolName,
                MinPoolSize = MinPoolSize,
                MaxPoolSize = MaxPoolSize,
                TotalConnections = TotalConnections,
                ActiveConnections = ActiveConnections,
                IdleConnections = IdleConnections,
                _totalRequests = _totalRequests,
                _totalCreated = _totalCreated,
                _failedConnections = _failedConnections
            };
        }
    }
}