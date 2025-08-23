using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Loco.Core.Locking
{
    public interface IDistributedLockService
    {
        Task<IDistributedLock> AcquireLockAsync(string resource, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
        Task<bool> TryAcquireLockAsync(string resource, TimeSpan timeout, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
        Task ReleaseLockAsync(string resource, string lockId);
        Task<bool> ExtendLockAsync(string resource, string lockId, TimeSpan extension);
        Task<bool> IsLockedAsync(string resource);
        Task<LockInfo> GetLockInfoAsync(string resource);
        Task ForceReleaseLockAsync(string resource);
    }

    public class DistributedLockService : IDistributedLockService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DistributedLockService> _logger;
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ConcurrentDictionary<string, LockInfo> _localLocks;
        private readonly string _keyPrefix;
        private readonly TimeSpan _defaultExpiry;
        private readonly bool _enableDistributed;

        public DistributedLockService(
            IConfiguration configuration,
            ILogger<DistributedLockService> logger,
            IConnectionMultiplexer redis = null)
        {
            _configuration = configuration;
            _logger = logger;
            _redis = redis;
            _database = redis?.GetDatabase();
            _localLocks = new ConcurrentDictionary<string, LockInfo>();
            _keyPrefix = _configuration["DistributedLock:KeyPrefix"] ?? "lock:";
            _defaultExpiry = TimeSpan.FromSeconds(_configuration.GetValue<int>("DistributedLock:DefaultExpirySeconds", 30));
            _enableDistributed = _configuration.GetValue<bool>("DistributedLock:EnableDistributed", true) && redis != null;
        }

        public async Task<IDistributedLock> AcquireLockAsync(
            string resource, 
            TimeSpan? expiry = null, 
            CancellationToken cancellationToken = default)
        {
            var lockId = Guid.NewGuid().ToString();
            var lockExpiry = expiry ?? _defaultExpiry;
            var redisKey = $"{_keyPrefix}{resource}";
            
            if (!_enableDistributed)
            {
                return await AcquireLocalLockAsync(resource, lockId, lockExpiry, cancellationToken);
            }

            var startTime = DateTime.UtcNow;
            var retryDelay = TimeSpan.FromMilliseconds(50);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var acquired = await _database.StringSetAsync(
                        redisKey, 
                        lockId, 
                        lockExpiry, 
                        When.NotExists);

                    if (acquired)
                    {
                        _logger.LogDebug("Acquired distributed lock for resource {Resource} with ID {LockId}", 
                            resource, lockId);
                        
                        return new DistributedLock(this, resource, lockId, lockExpiry);
                    }

                    await Task.Delay(retryDelay, cancellationToken);
                    
                    if (retryDelay < TimeSpan.FromSeconds(1))
                    {
                        retryDelay = retryDelay.Add(TimeSpan.FromMilliseconds(50));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error acquiring lock for resource {Resource}", resource);
                    throw new DistributedLockException($"Failed to acquire lock for resource {resource}", ex);
                }
            }

            throw new OperationCanceledException("Lock acquisition was cancelled");
        }

        public async Task<bool> TryAcquireLockAsync(
            string resource, 
            TimeSpan timeout, 
            TimeSpan? expiry = null, 
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                var lockObj = await AcquireLockAsync(resource, expiry, cts.Token);
                return lockObj != null;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public async Task ReleaseLockAsync(string resource, string lockId)
        {
            if (!_enableDistributed)
            {
                ReleaseLocalLock(resource, lockId);
                return;
            }

            var redisKey = $"{_keyPrefix}{resource}";
            
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            try
            {
                var result = await _database.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { redisKey },
                    new RedisValue[] { lockId });

                if ((int)result == 1)
                {
                    _logger.LogDebug("Released distributed lock for resource {Resource} with ID {LockId}", 
                        resource, lockId);
                }
                else
                {
                    _logger.LogWarning("Failed to release lock for resource {Resource} - lock ID mismatch", 
                        resource);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lock for resource {Resource}", resource);
            }
        }

        public async Task<bool> ExtendLockAsync(string resource, string lockId, TimeSpan extension)
        {
            if (!_enableDistributed)
            {
                return ExtendLocalLock(resource, lockId, extension);
            }

            var redisKey = $"{_keyPrefix}{resource}";
            
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('pexpire', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            try
            {
                var result = await _database.ScriptEvaluateAsync(
                    script,
                    new RedisKey[] { redisKey },
                    new RedisValue[] { lockId, (int)extension.TotalMilliseconds });

                var extended = (int)result == 1;
                
                if (extended)
                {
                    _logger.LogDebug("Extended lock for resource {Resource} by {Extension}", 
                        resource, extension);
                }
                
                return extended;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending lock for resource {Resource}", resource);
                return false;
            }
        }

        public async Task<bool> IsLockedAsync(string resource)
        {
            if (!_enableDistributed)
            {
                return _localLocks.ContainsKey(resource);
            }

            var redisKey = $"{_keyPrefix}{resource}";
            
            try
            {
                return await _database.KeyExistsAsync(redisKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking lock status for resource {Resource}", resource);
                return false;
            }
        }

        public async Task<LockInfo> GetLockInfoAsync(string resource)
        {
            if (!_enableDistributed)
            {
                return _localLocks.TryGetValue(resource, out var localInfo) ? localInfo : null;
            }

            var redisKey = $"{_keyPrefix}{resource}";
            
            try
            {
                var lockId = await _database.StringGetAsync(redisKey);
                if (lockId.IsNullOrEmpty)
                    return null;

                var ttl = await _database.KeyTimeToLiveAsync(redisKey);
                
                return new LockInfo
                {
                    Resource = resource,
                    LockId = lockId,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresIn = ttl ?? TimeSpan.Zero
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lock info for resource {Resource}", resource);
                return null;
            }
        }

        public async Task ForceReleaseLockAsync(string resource)
        {
            if (!_enableDistributed)
            {
                _localLocks.TryRemove(resource, out _);
                _logger.LogWarning("Force released local lock for resource {Resource}", resource);
                return;
            }

            var redisKey = $"{_keyPrefix}{resource}";
            
            try
            {
                await _database.KeyDeleteAsync(redisKey);
                _logger.LogWarning("Force released distributed lock for resource {Resource}", resource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error force releasing lock for resource {Resource}", resource);
            }
        }

        private async Task<IDistributedLock> AcquireLocalLockAsync(
            string resource, 
            string lockId, 
            TimeSpan expiry, 
            CancellationToken cancellationToken)
        {
            var lockInfo = new LockInfo
            {
                Resource = resource,
                LockId = lockId,
                AcquiredAt = DateTime.UtcNow,
                ExpiresIn = expiry,
                Semaphore = new SemaphoreSlim(1, 1)
            };

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_localLocks.TryAdd(resource, lockInfo))
                {
                    await lockInfo.Semaphore.WaitAsync(cancellationToken);
                    
                    var timer = new Timer(_ =>
                    {
                        _localLocks.TryRemove(resource, out _);
                        lockInfo.Semaphore?.Release();
                    }, null, expiry, Timeout.InfiniteTimeSpan);
                    
                    lockInfo.ExpiryTimer = timer;
                    
                    _logger.LogDebug("Acquired local lock for resource {Resource} with ID {LockId}", 
                        resource, lockId);
                    
                    return new DistributedLock(this, resource, lockId, expiry);
                }

                await Task.Delay(50, cancellationToken);
            }

            throw new OperationCanceledException("Lock acquisition was cancelled");
        }

        private void ReleaseLocalLock(string resource, string lockId)
        {
            if (_localLocks.TryRemove(resource, out var lockInfo))
            {
                if (lockInfo.LockId == lockId)
                {
                    lockInfo.ExpiryTimer?.Dispose();
                    lockInfo.Semaphore?.Release();
                    _logger.LogDebug("Released local lock for resource {Resource} with ID {LockId}", 
                        resource, lockId);
                }
            }
        }

        private bool ExtendLocalLock(string resource, string lockId, TimeSpan extension)
        {
            if (_localLocks.TryGetValue(resource, out var lockInfo))
            {
                if (lockInfo.LockId == lockId)
                {
                    lockInfo.ExpiryTimer?.Change(extension, Timeout.InfiniteTimeSpan);
                    lockInfo.ExpiresIn = extension;
                    _logger.LogDebug("Extended local lock for resource {Resource} by {Extension}", 
                        resource, extension);
                    return true;
                }
            }
            
            return false;
        }
    }

    public interface IDistributedLock : IDisposable
    {
        string Resource { get; }
        string LockId { get; }
        TimeSpan Expiry { get; }
        Task<bool> ExtendAsync(TimeSpan extension);
        Task ReleaseAsync();
    }

    public class DistributedLock : IDistributedLock
    {
        private readonly IDistributedLockService _lockService;
        private bool _disposed;

        public string Resource { get; }
        public string LockId { get; }
        public TimeSpan Expiry { get; }

        public DistributedLock(
            IDistributedLockService lockService, 
            string resource, 
            string lockId, 
            TimeSpan expiry)
        {
            _lockService = lockService;
            Resource = resource;
            LockId = lockId;
            Expiry = expiry;
        }

        public async Task<bool> ExtendAsync(TimeSpan extension)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DistributedLock));

            return await _lockService.ExtendLockAsync(Resource, LockId, extension);
        }

        public async Task ReleaseAsync()
        {
            if (_disposed)
                return;

            await _lockService.ReleaseLockAsync(Resource, LockId);
            _disposed = true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ReleaseAsync().GetAwaiter().GetResult();
            }
        }
    }

    public class LockInfo
    {
        public string Resource { get; set; }
        public string LockId { get; set; }
        public DateTime AcquiredAt { get; set; }
        public TimeSpan ExpiresIn { get; set; }
        public SemaphoreSlim Semaphore { get; set; }
        public Timer ExpiryTimer { get; set; }
    }

    public class DistributedLockException : Exception
    {
        public DistributedLockException(string message) : base(message) { }
        public DistributedLockException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class DistributedLockExtensions
    {
        public static async Task<T> ExecuteWithLockAsync<T>(
            this IDistributedLockService lockService,
            string resource,
            Func<Task<T>> operation,
            TimeSpan? lockExpiry = null,
            CancellationToken cancellationToken = default)
        {
            using var lockObj = await lockService.AcquireLockAsync(resource, lockExpiry, cancellationToken);
            return await operation();
        }

        public static async Task ExecuteWithLockAsync(
            this IDistributedLockService lockService,
            string resource,
            Func<Task> operation,
            TimeSpan? lockExpiry = null,
            CancellationToken cancellationToken = default)
        {
            using var lockObj = await lockService.AcquireLockAsync(resource, lockExpiry, cancellationToken);
            await operation();
        }

        public static async Task<T> TryExecuteWithLockAsync<T>(
            this IDistributedLockService lockService,
            string resource,
            Func<Task<T>> operation,
            TimeSpan timeout,
            T defaultValue = default,
            TimeSpan? lockExpiry = null,
            CancellationToken cancellationToken = default)
        {
            var acquired = await lockService.TryAcquireLockAsync(resource, timeout, lockExpiry, cancellationToken);
            if (!acquired)
                return defaultValue;

            try
            {
                return await operation();
            }
            finally
            {
                await lockService.ReleaseLockAsync(resource, string.Empty);
            }
        }
    }
}