using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Loco.Core.Caching
{
    public interface IDistributedCacheService
    {
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default);
        Task<byte[]> GetBytesAsync(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, CacheEntryOptions options = null, CancellationToken cancellationToken = default);
        Task SetStringAsync(string key, string value, CacheEntryOptions options = null, CancellationToken cancellationToken = default);
        Task SetBytesAsync(string key, byte[] value, CacheEntryOptions options = null, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions options = null, CancellationToken cancellationToken = default);
        Task<bool> RefreshAsync(string key, CancellationToken cancellationToken = default);
        Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default);
        Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
        Task<bool> UnlockAsync(string key, CancellationToken cancellationToken = default);
        Task FlushAsync(CancellationToken cancellationToken = default);
        Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    public class RedisCacheService : IDistributedCacheService, IDisposable
    {
        private readonly ILogger<RedisCacheService> _logger;
        private readonly RedisCacheOptions _options;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly SemaphoreSlim _connectionLock;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisCacheService(ILogger<RedisCacheService> logger, IOptions<RedisCacheOptions> options)
        {
            _logger = logger;
            _options = options?.Value ?? new RedisCacheOptions();
            _connectionLock = new SemaphoreSlim(1, 1);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            _redis = CreateConnection();
            _database = _redis.GetDatabase(_options.Database);
        }

        private IConnectionMultiplexer CreateConnection()
        {
            var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
            
            configOptions.AbortOnConnectFail = _options.AbortOnConnectFail;
            configOptions.ConnectTimeout = _options.ConnectTimeout;
            configOptions.SyncTimeout = _options.SyncTimeout;
            configOptions.AsyncTimeout = _options.AsyncTimeout;
            configOptions.ConnectRetry = _options.ConnectRetry;
            configOptions.DefaultDatabase = _options.Database;
            
            if (!string.IsNullOrEmpty(_options.Password))
            {
                configOptions.Password = _options.Password;
            }

            configOptions.ClientName = _options.InstanceName ?? "Loco";

            var connection = ConnectionMultiplexer.Connect(configOptions);
            
            connection.ConnectionFailed += (sender, args) =>
            {
                _logger.LogError("Redis connection failed: {FailureType} - {Exception}", 
                    args.FailureType, args.Exception?.Message);
            };

            connection.ConnectionRestored += (sender, args) =>
            {
                _logger.LogInformation("Redis connection restored");
            };

            connection.ErrorMessage += (sender, args) =>
            {
                _logger.LogError("Redis error: {Message}", args.Message);
            };

            return connection;
        }

        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var value = await _database.StringGetAsync(BuildKey(key));
                
                if (!value.HasValue)
                    return default;

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return default;
            }
        }

        public async Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var value = await _database.StringGetAsync(BuildKey(key));
                return value.HasValue ? value.ToString() : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting string cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return null;
            }
        }

        public async Task<byte[]> GetBytesAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var value = await _database.StringGetAsync(BuildKey(key));
                return value.HasValue ? (byte[])value : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bytes cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await SetStringAsync(key, json, options, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
            }
        }

        public async Task SetStringAsync(string key, string value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                options ??= new CacheEntryOptions();
                var expiry = GetExpiry(options);
                
                await _database.StringSetAsync(BuildKey(key), value, expiry);
                
                if (options.SlidingExpiration.HasValue)
                {
                    // Store sliding expiration metadata
                    await _database.StringSetAsync(
                        BuildSlidingExpirationKey(key), 
                        options.SlidingExpiration.Value.TotalSeconds, 
                        expiry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting string cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
            }
        }

        public async Task SetBytesAsync(string key, byte[] value, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                options ??= new CacheEntryOptions();
                var expiry = GetExpiry(options);
                
                await _database.StringSetAsync(BuildKey(key), value, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting bytes cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _database.KeyExistsAsync(BuildKey(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache key existence: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return false;
            }
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _database.KeyDeleteAsync(BuildKey(key));
                
                // Also remove sliding expiration metadata if exists
                await _database.KeyDeleteAsync(BuildSlidingExpirationKey(key));
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return false;
            }
        }

        public async Task<long> RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: BuildKey(pattern)).ToArray();
                
                if (keys.Any())
                {
                    return await _database.KeyDeleteAsync(keys);
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache keys by pattern: {Pattern}", pattern);
                
                if (_options.ThrowOnError)
                    throw;
                
                return 0;
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheEntryOptions options = null, CancellationToken cancellationToken = default)
        {
            // Try to get from cache
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null || !EqualityComparer<T>.Default.Equals(cached, default))
            {
                // Refresh sliding expiration if applicable
                await RefreshAsync(key, cancellationToken);
                return cached;
            }

            // Use distributed lock to prevent cache stampede
            var lockKey = $"{key}:lock";
            var lockAcquired = await LockAsync(lockKey, TimeSpan.FromSeconds(30), cancellationToken);
            
            try
            {
                // Double-check after acquiring lock
                cached = await GetAsync<T>(key, cancellationToken);
                if (cached != null || !EqualityComparer<T>.Default.Equals(cached, default))
                {
                    return cached;
                }

                // Create value
                var value = await factory();
                
                // Cache it
                await SetAsync(key, value, options, cancellationToken);
                
                return value;
            }
            finally
            {
                if (lockAcquired)
                {
                    await UnlockAsync(lockKey, cancellationToken);
                }
            }
        }

        public async Task<bool> RefreshAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var slidingKey = BuildSlidingExpirationKey(key);
                var slidingValue = await _database.StringGetAsync(slidingKey);
                
                if (slidingValue.HasValue)
                {
                    var seconds = (double)slidingValue;
                    var expiry = TimeSpan.FromSeconds(seconds);
                    
                    // Reset expiration
                    return await _database.KeyExpireAsync(BuildKey(key), expiry);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return false;
            }
        }

        public async Task<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _database.StringIncrementAsync(BuildKey(key), value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing cache key: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return 0;
            }
        }

        public async Task<bool> LockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = Guid.NewGuid().ToString();
                return await _database.StringSetAsync(BuildKey(key), token, expiry, When.NotExists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring lock: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return false;
            }
        }

        public async Task<bool> UnlockAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _database.KeyDeleteAsync(BuildKey(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lock: {Key}", key);
                
                if (_options.ThrowOnError)
                    throw;
                
                return false;
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                await server.FlushDatabaseAsync(_options.Database);
                
                _logger.LogWarning("Redis cache flushed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing cache");
                
                if (_options.ThrowOnError)
                    throw;
            }
        }

        public async Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var info = await server.InfoAsync("stats");
                
                var stats = new CacheStatistics();
                
                foreach (var group in info)
                {
                    foreach (var kvp in group)
                    {
                        switch (kvp.Key)
                        {
                            case "total_connections_received":
                                stats.TotalConnections = long.Parse(kvp.Value);
                                break;
                            case "total_commands_processed":
                                stats.TotalCommands = long.Parse(kvp.Value);
                                break;
                            case "instantaneous_ops_per_sec":
                                stats.OperationsPerSecond = long.Parse(kvp.Value);
                                break;
                            case "keyspace_hits":
                                stats.Hits = long.Parse(kvp.Value);
                                break;
                            case "keyspace_misses":
                                stats.Misses = long.Parse(kvp.Value);
                                break;
                        }
                    }
                }

                stats.HitRate = stats.Hits + stats.Misses > 0 
                    ? (double)stats.Hits / (stats.Hits + stats.Misses) 
                    : 0;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                
                if (_options.ThrowOnError)
                    throw;
                
                return new CacheStatistics();
            }
        }

        private string BuildKey(string key)
        {
            return string.IsNullOrEmpty(_options.InstanceName) 
                ? key 
                : $"{_options.InstanceName}:{key}";
        }

        private string BuildSlidingExpirationKey(string key)
        {
            return $"{BuildKey(key)}:sliding";
        }

        private TimeSpan? GetExpiry(CacheEntryOptions options)
        {
            if (options.AbsoluteExpiration.HasValue)
            {
                return options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
            }

            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                return options.AbsoluteExpirationRelativeToNow.Value;
            }

            if (options.SlidingExpiration.HasValue)
            {
                return options.SlidingExpiration.Value;
            }

            return _options.DefaultExpiration;
        }

        public void Dispose()
        {
            _connectionLock?.Dispose();
            _redis?.Dispose();
        }
    }

    public class RedisCacheOptions
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string Password { get; set; }
        public int Database { get; set; } = 0;
        public string InstanceName { get; set; }
        public TimeSpan? DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
        public bool AbortOnConnectFail { get; set; } = false;
        public int ConnectTimeout { get; set; } = 5000;
        public int SyncTimeout { get; set; } = 5000;
        public int AsyncTimeout { get; set; } = 5000;
        public int ConnectRetry { get; set; } = 3;
        public bool ThrowOnError { get; set; } = false;
    }

    public class CacheEntryOptions
    {
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
    }

    public class CacheStatistics
    {
        public long TotalConnections { get; set; }
        public long TotalCommands { get; set; }
        public long OperationsPerSecond { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public double HitRate { get; set; }
    }
}