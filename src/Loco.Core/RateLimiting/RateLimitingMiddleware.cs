using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Loco.Core.RateLimiting;

/// <summary>
/// Rate limiting strategies
/// </summary>
public enum RateLimitStrategy
{
    /// <summary>
    /// Token Bucket: Smooth traffic handling with burst capability
    /// </summary>
    TokenBucket,

    /// <summary>
    /// Sliding Window: Accurate but more memory intensive
    /// </summary>
    SlidingWindow,

    /// <summary>
    /// Fixed Window: Simple but can allow bursts at boundaries
    /// </summary>
    FixedWindow,

    /// <summary>
    /// Leaky Bucket: Consistent traffic flow
    /// </summary>
    LeakyBucket
}

/// <summary>
/// Rate limit configuration
/// </summary>
public class RateLimitConfig
{
    /// <summary>
    /// Strategy to use
    /// </summary>
    public RateLimitStrategy Strategy { get; set; } = RateLimitStrategy.TokenBucket;

    /// <summary>
    /// Requests allowed per time window
    /// </summary>
    public int RequestsPerWindow { get; set; } = 100;

    /// <summary>
    /// Time window in seconds
    /// </summary>
    public int WindowSizeSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use per-user rate limiting
    /// </summary>
    public bool PerUserLimiting { get; set; } = true;

    /// <summary>
    /// HTTP status code to return when rate limit exceeded
    /// </summary>
    public int TooManyRequestsStatusCode { get; set; } = StatusCodes.Status429TooManyRequests;

    /// <summary>
    /// Exclude paths from rate limiting (e.g., /health)
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new() { "/health", "/live", "/ready" };
}

/// <summary>
/// Rate limiter interface
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if request is allowed
    /// </summary>
    Task<bool> IsRequestAllowedAsync(string identifier);

    /// <summary>
    /// Gets current usage
    /// </summary>
    RateLimitInfo GetRateLimitInfo(string identifier);

    /// <summary>
    /// Resets rate limit for identifier
    /// </summary>
    void Reset(string identifier);
}

/// <summary>
/// Rate limit information
/// </summary>
public class RateLimitInfo
{
    public int RequestsRemaining { get; set; }
    public int RequestLimit { get; set; }
    public long ResetTime { get; set; } // Unix timestamp
    public double RetryAfter { get; set; } // Seconds
}

/// <summary>
/// Token Bucket rate limiter
/// Allows burst traffic while maintaining average rate
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly RateLimitConfig _config;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets;

    private class TokenBucket
    {
        public double Tokens { get; set; }
        public DateTime LastRefillTime { get; set; }
    }

    public TokenBucketRateLimiter(RateLimitConfig config, ILogger<TokenBucketRateLimiter> logger)
    {
        _config = config;
        _logger = logger;
        _buckets = new ConcurrentDictionary<string, TokenBucket>();
    }

    public async Task<bool> IsRequestAllowedAsync(string identifier)
    {
        var bucket = _buckets.GetOrAdd(identifier, _ => new TokenBucket
        {
            Tokens = _config.RequestsPerWindow,
            LastRefillTime = DateTime.UtcNow
        });

        lock (bucket)
        {
            // Refill tokens based on time elapsed
            var timePassed = (DateTime.UtcNow - bucket.LastRefillTime).TotalSeconds;
            var refillRate = (double)_config.RequestsPerWindow / _config.WindowSizeSeconds;
            var tokensToAdd = timePassed * refillRate;

            bucket.Tokens = Math.Min(_config.RequestsPerWindow, bucket.Tokens + tokensToAdd);
            bucket.LastRefillTime = DateTime.UtcNow;

            // Check if request is allowed
            if (bucket.Tokens >= 1)
            {
                bucket.Tokens--;
                return true;
            }

            _logger.LogWarning("Rate limit exceeded for identifier: {Identifier}", identifier);
            return false;
        }
    }

    public RateLimitInfo GetRateLimitInfo(string identifier)
    {
        var bucket = _buckets.GetOrAdd(identifier, _ => new TokenBucket
        {
            Tokens = _config.RequestsPerWindow,
            LastRefillTime = DateTime.UtcNow
        });

        lock (bucket)
        {
            var timePassed = (DateTime.UtcNow - bucket.LastRefillTime).TotalSeconds;
            var refillRate = (double)_config.RequestsPerWindow / _config.WindowSizeSeconds;
            var tokensToAdd = timePassed * refillRate;

            var currentTokens = Math.Min(_config.RequestsPerWindow, bucket.Tokens + tokensToAdd);
            var resetTime = bucket.LastRefillTime.AddSeconds(_config.WindowSizeSeconds);

            return new RateLimitInfo
            {
                RequestsRemaining = (int)currentTokens,
                RequestLimit = _config.RequestsPerWindow,
                ResetTime = new DateTimeOffset(resetTime).ToUnixTimeSeconds(),
                RetryAfter = (resetTime - DateTime.UtcNow).TotalSeconds
            };
        }
    }

    public void Reset(string identifier)
    {
        _buckets.TryRemove(identifier, out _);
    }
}

/// <summary>
/// Sliding Window rate limiter
/// More accurate but more memory intensive
/// </summary>
public class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly RateLimitConfig _config;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps;

    public SlidingWindowRateLimiter(RateLimitConfig config, ILogger<SlidingWindowRateLimiter> logger)
    {
        _config = config;
        _logger = logger;
        _requestTimestamps = new ConcurrentDictionary<string, Queue<DateTime>>();
    }

    public async Task<bool> IsRequestAllowedAsync(string identifier)
    {
        var queue = _requestTimestamps.GetOrAdd(identifier, _ => new Queue<DateTime>());

        lock (queue)
        {
            var windowStart = DateTime.UtcNow.AddSeconds(-_config.WindowSizeSeconds);

            // Remove old requests outside the window
            while (queue.Count > 0 && queue.Peek() < windowStart)
            {
                queue.Dequeue();
            }

            // Check if request is allowed
            if (queue.Count < _config.RequestsPerWindow)
            {
                queue.Enqueue(DateTime.UtcNow);
                return true;
            }

            _logger.LogWarning("Rate limit exceeded for identifier: {Identifier}", identifier);
            return false;
        }
    }

    public RateLimitInfo GetRateLimitInfo(string identifier)
    {
        var queue = _requestTimestamps.GetOrAdd(identifier, _ => new Queue<DateTime>());

        lock (queue)
        {
            var windowStart = DateTime.UtcNow.AddSeconds(-_config.WindowSizeSeconds);
            var count = queue.Count(t => t >= windowStart);
            var oldestRequest = queue.FirstOrDefault();
            var resetTime = oldestRequest.AddSeconds(_config.WindowSizeSeconds);

            return new RateLimitInfo
            {
                RequestsRemaining = Math.Max(0, _config.RequestsPerWindow - count),
                RequestLimit = _config.RequestsPerWindow,
                ResetTime = new DateTimeOffset(resetTime).ToUnixTimeSeconds(),
                RetryAfter = Math.Max(0, (resetTime - DateTime.UtcNow).TotalSeconds)
            };
        }
    }

    public void Reset(string identifier)
    {
        _requestTimestamps.TryRemove(identifier, out _);
    }
}

/// <summary>
/// Rate limiting middleware
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly RateLimitConfig _config;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        RateLimitConfig config,
        ILogger<RateLimitingMiddleware> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for excluded paths
        if (_config.ExcludedPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        // Get identifier (user ID or IP address)
        var identifier = GetIdentifier(context);

        // Check rate limit
        var allowed = await _rateLimiter.IsRequestAllowedAsync(identifier);

        // Get rate limit info
        var rateLimitInfo = _rateLimiter.GetRateLimitInfo(identifier);

        // Add rate limit headers to response
        context.Response.Headers.Add("X-RateLimit-Limit", rateLimitInfo.RequestLimit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", rateLimitInfo.RequestsRemaining.ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", rateLimitInfo.ResetTime.ToString());

        if (!allowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for identifier: {Identifier}, Path: {Path}",
                identifier,
                context.Request.Path);

            context.Response.StatusCode = _config.TooManyRequestsStatusCode;
            context.Response.Headers.Add("Retry-After", Math.Ceiling(rateLimitInfo.RetryAfter).ToString());
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too Many Requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = Math.Ceiling(rateLimitInfo.RetryAfter),
                resetTime = new DateTimeOffset(rateLimitInfo.ResetTime * 1000).DateTime.ToUniversalTime()
            });

            return;
        }

        // Store rate limit info in context for logging
        context.Items["RateLimitInfo"] = rateLimitInfo;

        await _next(context);
    }

    private string GetIdentifier(HttpContext context)
    {
        if (_config.PerUserLimiting)
        {
            // Try to get user ID from claims
            var userId = context.User?.FindFirst("sub")?.Value
                ?? context.User?.FindFirst("oid")?.Value
                ?? context.User?.Identity?.Name;

            if (!string.IsNullOrEmpty(userId))
            {
                return $"user:{userId}";
            }

            // Try API key
            if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
            {
                return $"key:{apiKey}";
            }
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString()
            ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? "unknown";

        return $"ip:{ipAddress}";
    }
}

/// <summary>
/// Extension methods for rate limiting
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds rate limiting services
    /// </summary>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        RateLimitConfig? config = null)
    {
        config ??= new RateLimitConfig();

        services.AddSingleton(config);
        services.AddHttpContextAccessor();

        // Register rate limiter based on strategy
        switch (config.Strategy)
        {
            case RateLimitStrategy.TokenBucket:
                services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
                break;
            case RateLimitStrategy.SlidingWindow:
                services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
                break;
            default:
                services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Uses rate limiting middleware
    /// </summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>
    /// Gets rate limit info from context
    /// </summary>
    public static RateLimitInfo? GetRateLimitInfo(this HttpContext context)
    {
        return context.Items.TryGetValue("RateLimitInfo", out var info) ? (RateLimitInfo)info : null;
    }
}
