// Phase 4: Redis Distributed Caching Manager
// High-performance distributed caching for workflow execution and metrics
// Enables horizontal scaling and reduces database load

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Caching;

/// <summary>
/// Distributed Redis cache manager for Loco workflow data
/// Caches frequently accessed workflows, execution history, and metrics
/// </summary>
public interface IRedisCacheManager
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default);
    Task InvalidatePatternAsync(string pattern, CancellationToken ct = default);
}

/// <summary>
/// Redis cache implementation with automatic serialization/deserialization
/// </summary>
public class RedisCacheManager : IRedisCacheManager
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Cache key patterns
    public static class CacheKeys
    {
        public const string WorkflowPrefix = "workflow:";
        public const string ExecutionPrefix = "execution:";
        public const string MetricsPrefix = "metrics:";
        public const string SessionPrefix = "session:";
        public const string RateLimitPrefix = "ratelimit:";

        public static string Workflow(string workflowId) => $"{WorkflowPrefix}{workflowId}";
        public static string Execution(string executionId) => $"{ExecutionPrefix}{executionId}";
        public static string Metrics(string workflowId) => $"{MetricsPrefix}{workflowId}";
        public static string Session(string sessionId) => $"{SessionPrefix}{sessionId}";
        public static string RateLimit(string userId) => $"{RateLimitPrefix}{userId}";
        public static string WorkflowList(int page, int pageSize) => $"{WorkflowPrefix}list:page={page}:size={pageSize}";
    }

    public RedisCacheManager(
        IDistributedCache cache,
        ILogger<RedisCacheManager> logger)
    {
        _cache = cache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };
    }

    /// <summary>
    /// Get value from cache
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var cacheEntry = await _cache.GetStringAsync(key, ct);

            if (cacheEntry == null)
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(cacheEntry, _jsonOptions);
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache for key: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Set value in cache
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var options = new DistributedCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                // Default: 1 hour for workflows, 30 minutes for metrics
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            }

            // Sliding window: Reset expiration on access
            options.SlidingExpiration = TimeSpan.FromMinutes(30);

            await _cache.SetStringAsync(key, json, options, ct);
            _logger.LogDebug("Cache set for key: {Key}, TTL: {TTL}s", key, expiration?.TotalSeconds ?? 3600);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache for key: {Key}", key);
            // Non-blocking: Don't fail request if cache write fails
        }
    }

    /// <summary>
    /// Remove value from cache
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogDebug("Cache entry removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing cache for key: {Key}", key);
        }
    }

    /// <summary>
    /// Get from cache or create if not exists
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        // Try get from cache first
        var cached = await GetAsync<T>(key, ct);
        if (cached != null)
        {
            return cached;
        }

        // Not in cache, create via factory
        _logger.LogDebug("Cache miss for key: {Key}, creating from factory", key);
        var value = await factory(ct);

        if (value != null)
        {
            // Store in cache for future requests
            await SetAsync(key, value, expiration, ct);
        }

        return value;
    }

    /// <summary>
    /// Invalidate all cache entries matching pattern (requires Redis Scan)
    /// </summary>
    public async Task InvalidatePatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Invalidating cache pattern: {Pattern}", pattern);
            // Note: This requires Redis adapter that supports SCAN
            // For StackExchange.Redis, use: IServer.Keys() with pattern
            await _cache.RemoveAsync($"{pattern}:*", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating cache pattern: {Pattern}", pattern);
        }
    }
}

/// <summary>
/// Cache-aside pattern helper for workflow operations
/// </summary>
public class WorkflowCacheHelper
{
    private readonly IRedisCacheManager _cache;
    private readonly ILogger<WorkflowCacheHelper> _logger;

    public WorkflowCacheHelper(
        IRedisCacheManager cache,
        ILogger<WorkflowCacheHelper> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Cache workflow with 1-hour TTL
    /// </summary>
    public async Task CacheWorkflowAsync<T>(
        string workflowId,
        T workflow,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Workflow(workflowId);
        await _cache.SetAsync(key, workflow, TimeSpan.FromHours(1), ct);
        _logger.LogDebug("Cached workflow: {WorkflowId}", workflowId);
    }

    /// <summary>
    /// Get cached workflow
    /// </summary>
    public async Task<T?> GetCachedWorkflowAsync<T>(
        string workflowId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Workflow(workflowId);
        return await _cache.GetAsync<T>(key, ct);
    }

    /// <summary>
    /// Invalidate workflow cache (called on update/delete)
    /// </summary>
    public async Task InvalidateWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Workflow(workflowId);
        await _cache.RemoveAsync(key, ct);
        _logger.LogDebug("Invalidated workflow cache: {WorkflowId}", workflowId);

        // Also invalidate workflow list cache
        await _cache.InvalidatePatternAsync("workflow:list", ct);
    }

    /// <summary>
    /// Cache execution history with 30-minute TTL
    /// </summary>
    public async Task CacheExecutionAsync<T>(
        string executionId,
        T execution,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Execution(executionId);
        await _cache.SetAsync(key, execution, TimeSpan.FromMinutes(30), ct);
    }

    /// <summary>
    /// Get cached execution
    /// </summary>
    public async Task<T?> GetCachedExecutionAsync<T>(
        string executionId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Execution(executionId);
        return await _cache.GetAsync<T>(key, ct);
    }

    /// <summary>
    /// Cache metrics with 5-minute TTL (frequently updated)
    /// </summary>
    public async Task CacheMetricsAsync<T>(
        string workflowId,
        T metrics,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Metrics(workflowId);
        await _cache.SetAsync(key, metrics, TimeSpan.FromMinutes(5), ct);
    }

    /// <summary>
    /// Get cached metrics
    /// </summary>
    public async Task<T?> GetCachedMetricsAsync<T>(
        string workflowId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Metrics(workflowId);
        return await _cache.GetAsync<T>(key, ct);
    }

    /// <summary>
    /// Invalidate metrics when workflow completes
    /// </summary>
    public async Task InvalidateMetricsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.Metrics(workflowId);
        await _cache.RemoveAsync(key, ct);
    }
}

/// <summary>
/// Rate limiting helper using Redis
/// Tracks API rate limits per user/IP
/// </summary>
public class RateLimitHelper
{
    private readonly IRedisCacheManager _cache;
    private readonly ILogger<RateLimitHelper> _logger;

    public RateLimitHelper(
        IRedisCacheManager cache,
        ILogger<RateLimitHelper> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Check and increment rate limit counter
    /// </summary>
    public async Task<RateLimitResult> CheckRateLimitAsync(
        string userId,
        int maxRequests = 1000,
        TimeSpan? window = null,
        CancellationToken ct = default)
    {
        window ??= TimeSpan.FromMinutes(1);
        var key = RedisCacheManager.CacheKeys.RateLimit(userId);

        var current = await _cache.GetAsync<int>(key, ct);
        var newCount = (current ?? 0) + 1;

        // Store updated count
        await _cache.SetAsync(key, newCount, window, ct);

        var isAllowed = newCount <= maxRequests;

        if (!isAllowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for user: {UserId}, count: {Count}/{Max}",
                userId, newCount, maxRequests);
        }

        return new RateLimitResult
        {
            IsAllowed = isAllowed,
            CurrentCount = newCount,
            MaxRequests = maxRequests,
            WindowSize = window,
            ResetTime = DateTime.UtcNow.Add(window.Value),
        };
    }

    /// <summary>
    /// Reset rate limit for user
    /// </summary>
    public async Task ResetRateLimitAsync(
        string userId,
        CancellationToken ct = default)
    {
        var key = RedisCacheManager.CacheKeys.RateLimit(userId);
        await _cache.RemoveAsync(key, ct);
        _logger.LogDebug("Rate limit reset for user: {UserId}", userId);
    }
}

/// <summary>
/// Rate limit check result
/// </summary>
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int CurrentCount { get; set; }
    public int MaxRequests { get; set; }
    public TimeSpan WindowSize { get; set; }
    public DateTime ResetTime { get; set; }

    public int RemainingRequests => Math.Max(0, MaxRequests - CurrentCount);
    public double RemainingSeconds => (ResetTime - DateTime.UtcNow).TotalSeconds;
}
