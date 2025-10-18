using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Persistence;

/// <summary>
/// Lock acquisition result.
/// </summary>
public class LockResult : IDisposable
{
    public bool Acquired { get; set; }
    public string LockId { get; set; } = "";
    public string ResourceName { get; set; } = "";
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Action? OnRelease { get; set; }

    public void Dispose()
    {
        OnRelease?.Invoke();
    }
}

/// <summary>
/// Lock information.
/// </summary>
public class LockInfo
{
    public string ResourceName { get; set; } = "";
    public string LockId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int RenewalCount { get; set; }
}

/// <summary>
/// Distributed lock manager for preventing concurrent workflow execution.
/// Supports file-based locking with automatic expiration and renewal.
/// </summary>
public class DistributedLockManager : IDisposable
{
    private readonly string _lockDirectory;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, LockInfo> _activeLocks = new();
    private readonly Timer _expirationTimer;
    private readonly SemaphoreSlim _lockSemaphore = new(1, 1);
    private bool _disposed;

    public DistributedLockManager(string lockDirectory, ILogger? logger = null)
    {
        _lockDirectory = lockDirectory;
        _logger = logger;

        Directory.CreateDirectory(_lockDirectory);

        // Timer to check for expired locks
        _expirationTimer = new Timer(
            _ => CleanupExpiredLocks(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));

        _logger?.LogInformation("DistributedLockManager initialized at {Directory}", _lockDirectory);
    }

    /// <summary>
    /// Attempts to acquire a lock on a resource.
    /// </summary>
    public async Task<LockResult> AcquireLockAsync(
        string resourceName,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null)
    {
        var lockId = Guid.NewGuid().ToString();
        var ownerId = Environment.MachineName + "_" + Environment.ProcessId;
        var expiry = lockExpiry ?? TimeSpan.FromMinutes(5);
        var deadline = DateTime.UtcNow + timeout;

        _logger?.LogDebug("Attempting to acquire lock on {Resource} (timeout: {Timeout})",
            resourceName, timeout);

        while (DateTime.UtcNow < deadline)
        {
            await _lockSemaphore.WaitAsync();
            try
            {
                var lockFilePath = GetLockFilePath(resourceName);

                // Check if lock file exists and is valid
                if (File.Exists(lockFilePath))
                {
                    var existingLock = await ReadLockFileAsync(lockFilePath);

                    if (existingLock != null && existingLock.ExpiresAt > DateTime.UtcNow)
                    {
                        // Lock is held by someone else
                        _logger?.LogDebug(
                            "Lock on {Resource} is held by {Owner} until {Expires}",
                            resourceName, existingLock.OwnerId, existingLock.ExpiresAt);

                        await Task.Delay(100);
                        continue;
                    }

                    // Lock has expired, we can take it
                    _logger?.LogDebug("Found expired lock on {Resource}, taking over", resourceName);
                }

                // Acquire the lock
                var lockInfo = new LockInfo
                {
                    ResourceName = resourceName,
                    LockId = lockId,
                    OwnerId = ownerId,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow + expiry,
                    RenewalCount = 0
                };

                await WriteLockFileAsync(lockFilePath, lockInfo);
                _activeLocks[resourceName] = lockInfo;

                _logger?.LogInformation(
                    "Acquired lock on {Resource} (lockId: {LockId}, expires: {Expires})",
                    resourceName, lockId, lockInfo.ExpiresAt);

                return new LockResult
                {
                    Acquired = true,
                    LockId = lockId,
                    ResourceName = resourceName,
                    AcquiredAt = lockInfo.AcquiredAt,
                    ExpiresAt = lockInfo.ExpiresAt,
                    OnRelease = () => _ = ReleaseLockAsync(resourceName, lockId)
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error acquiring lock on {Resource}", resourceName);
                await Task.Delay(100);
            }
            finally
            {
                _lockSemaphore.Release();
            }
        }

        _logger?.LogWarning("Failed to acquire lock on {Resource} within timeout", resourceName);

        return new LockResult
        {
            Acquired = false,
            ResourceName = resourceName
        };
    }

    /// <summary>
    /// Releases a lock.
    /// </summary>
    public async Task<bool> ReleaseLockAsync(string resourceName, string lockId)
    {
        await _lockSemaphore.WaitAsync();
        try
        {
            if (!_activeLocks.TryGetValue(resourceName, out var lockInfo))
            {
                _logger?.LogWarning("Attempted to release non-existent lock: {Resource}", resourceName);
                return false;
            }

            if (lockInfo.LockId != lockId)
            {
                _logger?.LogWarning(
                    "Attempted to release lock {Resource} with wrong lockId (expected: {Expected}, got: {Got})",
                    resourceName, lockInfo.LockId, lockId);
                return false;
            }

            var lockFilePath = GetLockFilePath(resourceName);
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }

            _activeLocks.TryRemove(resourceName, out _);

            _logger?.LogInformation("Released lock on {Resource} (lockId: {LockId})", resourceName, lockId);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error releasing lock on {Resource}", resourceName);
            return false;
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <summary>
    /// Renews a lock to extend its expiration time.
    /// </summary>
    public async Task<bool> RenewLockAsync(string resourceName, string lockId, TimeSpan extension)
    {
        await _lockSemaphore.WaitAsync();
        try
        {
            if (!_activeLocks.TryGetValue(resourceName, out var lockInfo))
            {
                _logger?.LogWarning("Attempted to renew non-existent lock: {Resource}", resourceName);
                return false;
            }

            if (lockInfo.LockId != lockId)
            {
                _logger?.LogWarning("Attempted to renew lock with wrong lockId");
                return false;
            }

            lockInfo.ExpiresAt = DateTime.UtcNow + extension;
            lockInfo.RenewalCount++;

            var lockFilePath = GetLockFilePath(resourceName);
            await WriteLockFileAsync(lockFilePath, lockInfo);

            _logger?.LogDebug(
                "Renewed lock on {Resource} (lockId: {LockId}, new expiry: {Expires}, renewals: {Count})",
                resourceName, lockId, lockInfo.ExpiresAt, lockInfo.RenewalCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error renewing lock on {Resource}", resourceName);
            return false;
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <summary>
    /// Checks if a resource is currently locked.
    /// </summary>
    public async Task<bool> IsLockedAsync(string resourceName)
    {
        await _lockSemaphore.WaitAsync();
        try
        {
            var lockFilePath = GetLockFilePath(resourceName);

            if (!File.Exists(lockFilePath))
                return false;

            var lockInfo = await ReadLockFileAsync(lockFilePath);

            return lockInfo != null && lockInfo.ExpiresAt > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets lock information for a resource.
    /// </summary>
    public async Task<LockInfo?> GetLockInfoAsync(string resourceName)
    {
        await _lockSemaphore.WaitAsync();
        try
        {
            var lockFilePath = GetLockFilePath(resourceName);

            if (!File.Exists(lockFilePath))
                return null;

            return await ReadLockFileAsync(lockFilePath);
        }
        finally
        {
            _lockSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes an action while holding a lock.
    /// </summary>
    public async Task<T> ExecuteWithLockAsync<T>(
        string resourceName,
        Func<Task<T>> action,
        TimeSpan? timeout = null,
        TimeSpan? lockExpiry = null)
    {
        timeout ??= TimeSpan.FromSeconds(30);

        using var lockResult = await AcquireLockAsync(resourceName, timeout.Value, lockExpiry);

        if (!lockResult.Acquired)
        {
            throw new TimeoutException($"Failed to acquire lock on {resourceName} within {timeout}");
        }

        try
        {
            return await action();
        }
        finally
        {
            await ReleaseLockAsync(resourceName, lockResult.LockId);
        }
    }

    /// <summary>
    /// Executes an action while holding a lock (void version).
    /// </summary>
    public async Task ExecuteWithLockAsync(
        string resourceName,
        Func<Task> action,
        TimeSpan? timeout = null,
        TimeSpan? lockExpiry = null)
    {
        await ExecuteWithLockAsync<object>(resourceName, async () =>
        {
            await action();
            return null!;
        }, timeout, lockExpiry);
    }

    /// <summary>
    /// Cleans up expired locks.
    /// </summary>
    private void CleanupExpiredLocks()
    {
        try
        {
            var now = DateTime.UtcNow;
            var expiredLocks = _activeLocks.Values
                .Where(l => l.ExpiresAt < now)
                .ToList();

            foreach (var lockInfo in expiredLocks)
            {
                _activeLocks.TryRemove(lockInfo.ResourceName, out _);

                try
                {
                    var lockFilePath = GetLockFilePath(lockInfo.ResourceName);
                    if (File.Exists(lockFilePath))
                    {
                        File.Delete(lockFilePath);
                    }

                    _logger?.LogInformation(
                        "Cleaned up expired lock on {Resource} (lockId: {LockId})",
                        lockInfo.ResourceName, lockInfo.LockId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error cleaning up expired lock: {Resource}", lockInfo.ResourceName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during lock cleanup");
        }
    }

    /// <summary>
    /// Reads lock information from file.
    /// </summary>
    private async Task<LockInfo?> ReadLockFileAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<LockInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes lock information to file.
    /// </summary>
    private async Task WriteLockFileAsync(string filePath, LockInfo lockInfo)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(lockInfo, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Gets statistics about active locks.
    /// </summary>
    public DistributedLockStats GetStats()
    {
        var locks = _activeLocks.Values.ToList();
        var now = DateTime.UtcNow;

        return new DistributedLockStats
        {
            ActiveLocks = locks.Count,
            ExpiredLocks = locks.Count(l => l.ExpiresAt < now),
            LockDirectory = _lockDirectory,
            OldestLockAge = locks.Any() ? now - locks.Min(l => l.AcquiredAt) : TimeSpan.Zero,
            MostRenewals = locks.Any() ? locks.Max(l => l.RenewalCount) : 0
        };
    }

    private string GetLockFilePath(string resourceName)
    {
        var safeName = string.Join("_", resourceName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_lockDirectory, $"{safeName}.lock");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _expirationTimer?.Dispose();
            _lockSemaphore?.Dispose();

            // Release all active locks
            foreach (var lockInfo in _activeLocks.Values)
            {
                try
                {
                    var lockFilePath = GetLockFilePath(lockInfo.ResourceName);
                    if (File.Exists(lockFilePath))
                    {
                        File.Delete(lockFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error releasing lock during disposal: {Resource}", lockInfo.ResourceName);
                }
            }

            _activeLocks.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Distributed lock manager statistics.
/// </summary>
public class DistributedLockStats
{
    public int ActiveLocks { get; set; }
    public int ExpiredLocks { get; set; }
    public string LockDirectory { get; set; } = "";
    public TimeSpan OldestLockAge { get; set; }
    public int MostRenewals { get; set; }
}
