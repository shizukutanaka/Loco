using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Web.Middleware;

public class EnhancedRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnhancedRateLimitingMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimits = new();

    private class RateLimitInfo
    {
        public int Requests { get; set; }
        public DateTime WindowStart { get; set; }
        public Queue<DateTime> RequestTimes { get; set; } = new();
        public int TotalRequests { get; set; }
        public DateTime? BlockedUntil { get; set; }
    }

    public class RateLimitOptions
    {
        public int RequestsPerMinute { get; set; } = 60;
        public int RequestsPerHour { get; set; } = 1000;
        public int RequestsPerDay { get; set; } = 10000;
        public bool EnableIpRateLimiting { get; set; } = true;
        public bool EnableUserRateLimiting { get; set; } = true;
        public bool EnableApiKeyRateLimiting { get; set; } = true;
        public int BurstSize { get; set; } = 10;
        public int BlockDurationMinutes { get; set; } = 15;
        public string[] WhitelistedIps { get; set; } = Array.Empty<string>();
        public string[] WhitelistedApiKeys { get; set; } = Array.Empty<string>();
        public Dictionary<string, int> CustomLimits { get; set; } = new();
        public bool EnableDistributedRateLimiting { get; set; } = false;
        public bool EnableAdaptiveRateLimiting { get; set; } = true;
    }

    public EnhancedRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<EnhancedRateLimitingMiddleware> logger,
        IMemoryCache cache,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        _options = configuration.GetSection("RateLimit").Get<RateLimitOptions>() ?? new RateLimitOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var identifier = GetClientIdentifier(context);
        
        if (IsWhitelisted(identifier, context))
        {
            await _next(context);
            return;
        }

        var rateLimitInfo = GetOrCreateRateLimitInfo(identifier);
        
        if (rateLimitInfo.BlockedUntil.HasValue && rateLimitInfo.BlockedUntil.Value > DateTime.UtcNow)
        {
            await WriteRateLimitResponse(context, rateLimitInfo, true);
            return;
        }

        if (!IsRequestAllowed(identifier, rateLimitInfo, context))
        {
            rateLimitInfo.BlockedUntil = DateTime.UtcNow.AddMinutes(_options.BlockDurationMinutes);
            await WriteRateLimitResponse(context, rateLimitInfo, false);
            
            _logger.LogWarning("Rate limit exceeded for {Identifier}. Blocked until {BlockedUntil}", 
                identifier, rateLimitInfo.BlockedUntil);
            return;
        }

        RecordRequest(rateLimitInfo);
        SetRateLimitHeaders(context, rateLimitInfo);
        
        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Priority: API Key > User ID > IP Address
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
            return $"apikey:{apiKey}";

        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    private bool IsWhitelisted(string identifier, HttpContext context)
    {
        if (identifier.StartsWith("ip:"))
        {
            var ip = identifier.Substring(3);
            if (_options.WhitelistedIps.Contains(ip))
                return true;
        }

        if (identifier.StartsWith("apikey:"))
        {
            var apiKey = identifier.Substring(7);
            if (_options.WhitelistedApiKeys.Contains(apiKey))
                return true;
        }

        // Check for internal health checks
        if (context.Request.Path.StartsWithSegments("/healthz") ||
            context.Request.Path.StartsWithSegments("/metrics"))
            return true;

        return false;
    }

    private RateLimitInfo GetOrCreateRateLimitInfo(string identifier)
    {
        return _rateLimits.GetOrAdd(identifier, _ => new RateLimitInfo
        {
            WindowStart = DateTime.UtcNow,
            RequestTimes = new Queue<DateTime>()
        });
    }

    private bool IsRequestAllowed(string identifier, RateLimitInfo info, HttpContext context)
    {
        var now = DateTime.UtcNow;
        
        // Clean up old request times
        while (info.RequestTimes.Count > 0 && info.RequestTimes.Peek() < now.AddMinutes(-1))
        {
            info.RequestTimes.Dequeue();
        }

        // Get rate limit for this identifier
        var limit = GetRateLimit(identifier, context);
        
        // Adaptive rate limiting
        if (_options.EnableAdaptiveRateLimiting)
        {
            limit = CalculateAdaptiveLimit(info, limit);
        }

        // Check burst
        if (info.RequestTimes.Count >= _options.BurstSize)
        {
            var oldestBurstRequest = info.RequestTimes.Skip(info.RequestTimes.Count - _options.BurstSize).First();
            if (now.Subtract(oldestBurstRequest).TotalSeconds < 1)
            {
                _logger.LogWarning("Burst limit exceeded for {Identifier}", identifier);
                return false;
            }
        }

        // Check rate limit
        if (info.RequestTimes.Count >= limit)
        {
            return false;
        }

        return true;
    }

    private int GetRateLimit(string identifier, HttpContext context)
    {
        // Check custom limits first
        if (_options.CustomLimits.TryGetValue(identifier, out var customLimit))
            return customLimit;

        // Check endpoint-specific limits
        var endpoint = context.Request.Path.Value;
        if (!string.IsNullOrEmpty(endpoint))
        {
            if (endpoint.Contains("/api/llm", StringComparison.OrdinalIgnoreCase))
                return 10; // Lower limit for LLM endpoints
            if (endpoint.Contains("/api/flows", StringComparison.OrdinalIgnoreCase))
                return 100; // Standard limit for flow endpoints
        }

        // Check identifier type
        if (identifier.StartsWith("apikey:"))
            return _options.RequestsPerMinute * 2; // Higher limit for API keys
        if (identifier.StartsWith("user:"))
            return _options.RequestsPerMinute;
        
        // Default IP-based limit
        return _options.RequestsPerMinute / 2;
    }

    private int CalculateAdaptiveLimit(RateLimitInfo info, int baseLimit)
    {
        // Increase limit for well-behaved clients
        if (info.TotalRequests > 1000 && info.BlockedUntil == null)
        {
            return (int)(baseLimit * 1.5);
        }

        // Decrease limit for previously blocked clients
        if (info.BlockedUntil.HasValue)
        {
            return (int)(baseLimit * 0.5);
        }

        return baseLimit;
    }

    private void RecordRequest(RateLimitInfo info)
    {
        var now = DateTime.UtcNow;
        info.RequestTimes.Enqueue(now);
        info.TotalRequests++;
        info.Requests++;
    }

    private void SetRateLimitHeaders(HttpContext context, RateLimitInfo info)
    {
        var limit = GetRateLimit(GetClientIdentifier(context), context);
        var remaining = Math.Max(0, limit - info.RequestTimes.Count);
        var resetTime = info.WindowStart.AddMinutes(1);
        var resetTimestamp = new DateTimeOffset(resetTime).ToUnixTimeSeconds();

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = resetTimestamp.ToString();
        
        if (remaining == 0)
        {
            context.Response.Headers["Retry-After"] = "60";
        }
    }

    private async Task WriteRateLimitResponse(HttpContext context, RateLimitInfo info, bool isBlocked)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "rate_limit_exceeded",
            message = isBlocked 
                ? $"Too many requests. You are blocked until {info.BlockedUntil:yyyy-MM-ddTHH:mm:ssZ}"
                : "Too many requests. Please retry after some time.",
            retryAfter = isBlocked 
                ? (int)(info.BlockedUntil!.Value - DateTime.UtcNow).TotalSeconds 
                : 60
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    // Cleanup method to be called periodically
    public void CleanupExpiredEntries()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var keysToRemove = _rateLimits
            .Where(kvp => kvp.Value.WindowStart < cutoff && !kvp.Value.BlockedUntil.HasValue)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _rateLimits.TryRemove(key, out _);
        }

        _logger.LogDebug("Cleaned up {Count} expired rate limit entries", keysToRemove.Count);
    }
}

// Extension method to add the middleware
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseEnhancedRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EnhancedRateLimitingMiddleware>();
    }
}
