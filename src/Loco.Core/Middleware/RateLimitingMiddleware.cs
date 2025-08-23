using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly RateLimitOptions _options;

        public RateLimitingMiddleware(
            RequestDelegate next,
            ILogger<RateLimitingMiddleware> logger,
            IRateLimitService rateLimitService,
            IOptions<RateLimitOptions> options)
        {
            _next = next;
            _logger = logger;
            _rateLimitService = rateLimitService;
            _options = options?.Value ?? new RateLimitOptions();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldSkipRateLimiting(context))
            {
                await _next(context);
                return;
            }

            var clientId = GetClientIdentifier(context);
            var endpoint = GetEndpointIdentifier(context);
            var rule = GetApplicableRule(context);

            if (rule == null)
            {
                await _next(context);
                return;
            }

            var rateLimitKey = $"{rule.Name}:{endpoint}:{clientId}";
            var result = await _rateLimitService.CheckRateLimitAsync(rateLimitKey, rule);

            // Set rate limit headers
            SetRateLimitHeaders(context, result);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}", clientId, endpoint);
                await HandleRateLimitExceeded(context, result);
                return;
            }

            await _next(context);
        }

        private bool ShouldSkipRateLimiting(HttpContext context)
        {
            // Skip for excluded paths
            if (_options.ExcludedPaths?.Any(path => context.Request.Path.StartsWithSegments(path)) == true)
            {
                return true;
            }

            // Skip for whitelisted IPs
            var clientIp = GetClientIp(context);
            if (_options.WhitelistedIps?.Contains(clientIp) == true)
            {
                return true;
            }

            // Skip for specific user roles
            if (context.User?.Identity?.IsAuthenticated == true && _options.WhitelistedRoles != null)
            {
                var userRoles = context.User.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value);

                if (userRoles.Any(role => _options.WhitelistedRoles.Contains(role)))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Priority: User ID > API Key > IP Address
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                return $"user:{context.User.Identity.Name}";
            }

            if (context.Request.Headers.TryGetValue(_options.ApiKeyHeaderName, out var apiKey))
            {
                return $"apikey:{apiKey}";
            }

            return $"ip:{GetClientIp(context)}";
        }

        private string GetClientIp(HttpContext context)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string GetEndpointIdentifier(HttpContext context)
        {
            var method = context.Request.Method;
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
            
            // Normalize path parameters (e.g., /api/users/123 -> /api/users/{id})
            path = NormalizePath(path);
            
            return $"{method}:{path}";
        }

        private string NormalizePath(string path)
        {
            // Replace GUIDs
            path = System.Text.RegularExpressions.Regex.Replace(
                path, 
                @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", 
                "{id}", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Replace numbers
            path = System.Text.RegularExpressions.Regex.Replace(
                path,
                @"/\d+",
                "/{id}");
            
            return path;
        }

        private RateLimitRule GetApplicableRule(HttpContext context)
        {
            var endpoint = GetEndpointIdentifier(context);
            
            // Check for endpoint-specific rules
            var endpointRule = _options.Rules?.FirstOrDefault(r => 
                r.Endpoints?.Any(e => endpoint.Contains(e, StringComparison.OrdinalIgnoreCase)) == true);
            
            if (endpointRule != null)
            {
                return endpointRule;
            }

            // Check for user-specific rules
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var userRole = context.User.Claims
                    .FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

                if (!string.IsNullOrEmpty(userRole))
                {
                    var roleRule = _options.Rules?.FirstOrDefault(r => 
                        r.Roles?.Contains(userRole) == true);
                    
                    if (roleRule != null)
                    {
                        return roleRule;
                    }
                }

                // Return authenticated user default rule
                return _options.AuthenticatedUserRule ?? _options.DefaultRule;
            }

            // Return default rule
            return _options.DefaultRule;
        }

        private void SetRateLimitHeaders(HttpContext context, RateLimitResult result)
        {
            context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, result.Remaining).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = result.ResetTime.ToUnixTimeSeconds().ToString();

            if (result.RetryAfter.HasValue)
            {
                context.Response.Headers["Retry-After"] = result.RetryAfter.Value.TotalSeconds.ToString("0");
            }
        }

        private async Task HandleRateLimitExceeded(HttpContext context, RateLimitResult result)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            
            var response = new
            {
                error = "Rate limit exceeded",
                message = _options.ExceededMessage ?? "Too many requests. Please retry later.",
                retryAfter = result.RetryAfter?.TotalSeconds
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    }

    public interface IRateLimitService
    {
        Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitRule rule);
        Task ResetAsync(string key);
        Task<RateLimitStatistics> GetStatisticsAsync(string key);
    }

    public class MemoryRateLimitService : IRateLimitService
    {
        private readonly ConcurrentDictionary<string, RateLimitCounter> _counters = new();
        private readonly Timer _cleanupTimer;
        private readonly ILogger<MemoryRateLimitService> _logger;

        public MemoryRateLimitService(ILogger<MemoryRateLimitService> logger)
        {
            _logger = logger;
            _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitRule rule)
        {
            var now = DateTimeOffset.UtcNow;
            var counter = _counters.AddOrUpdate(key,
                k => new RateLimitCounter(rule.Period),
                (k, existing) => existing.Window != rule.Period ? new RateLimitCounter(rule.Period) : existing);

            var result = counter.Increment(now, rule.Limit);
            
            return Task.FromResult(result);
        }

        public Task ResetAsync(string key)
        {
            _counters.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<RateLimitStatistics> GetStatisticsAsync(string key)
        {
            if (_counters.TryGetValue(key, out var counter))
            {
                return Task.FromResult(counter.GetStatistics());
            }

            return Task.FromResult<RateLimitStatistics>(null);
        }

        private void Cleanup(object state)
        {
            var now = DateTimeOffset.UtcNow;
            var keysToRemove = new List<string>();

            foreach (var kvp in _counters)
            {
                if (kvp.Value.IsExpired(now))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _counters.TryRemove(key, out _);
            }

            if (keysToRemove.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit counters", keysToRemove.Count);
            }
        }
    }

    public class RedisRateLimitService : IRateLimitService
    {
        private readonly StackExchange.Redis.IDatabase _database;
        private readonly ILogger<RedisRateLimitService> _logger;

        public RedisRateLimitService(StackExchange.Redis.IConnectionMultiplexer redis, ILogger<RedisRateLimitService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, RateLimitRule rule)
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = GetWindowStart(now, rule.Period);
            var windowEnd = windowStart.Add(rule.Period);
            
            var redisKey = $"ratelimit:{key}:{windowStart.ToUnixTimeSeconds()}";
            
            var count = await _database.StringIncrementAsync(redisKey);
            
            if (count == 1)
            {
                await _database.KeyExpireAsync(redisKey, rule.Period.Add(TimeSpan.FromSeconds(1)));
            }

            var remaining = Math.Max(0, rule.Limit - count);
            var isAllowed = count <= rule.Limit;

            return new RateLimitResult
            {
                IsAllowed = isAllowed,
                Limit = rule.Limit,
                Remaining = (int)remaining,
                ResetTime = windowEnd,
                RetryAfter = isAllowed ? null : windowEnd - now
            };
        }

        public async Task ResetAsync(string key)
        {
            var pattern = $"ratelimit:{key}:*";
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (keys.Any())
            {
                await _database.KeyDeleteAsync(keys);
            }
        }

        public async Task<RateLimitStatistics> GetStatisticsAsync(string key)
        {
            var pattern = $"ratelimit:{key}:*";
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (!keys.Any())
            {
                return null;
            }

            var latestKey = keys.OrderByDescending(k => k.ToString()).First();
            var count = await _database.StringGetAsync(latestKey);
            
            return new RateLimitStatistics
            {
                CurrentCount = (long)count,
                WindowStart = DateTimeOffset.FromUnixTimeSeconds(
                    long.Parse(latestKey.ToString().Split(':').Last()))
            };
        }

        private DateTimeOffset GetWindowStart(DateTimeOffset time, TimeSpan period)
        {
            var periodSeconds = (long)period.TotalSeconds;
            var timeSeconds = time.ToUnixTimeSeconds();
            var windowStartSeconds = (timeSeconds / periodSeconds) * periodSeconds;
            return DateTimeOffset.FromUnixTimeSeconds(windowStartSeconds);
        }
    }

    // Internal rate limit counter for memory implementation
    internal class RateLimitCounter
    {
        private readonly object _lock = new();
        private readonly TimeSpan _window;
        private readonly Queue<DateTimeOffset> _timestamps = new();

        public TimeSpan Window => _window;

        public RateLimitCounter(TimeSpan window)
        {
            _window = window;
        }

        public RateLimitResult Increment(DateTimeOffset now, int limit)
        {
            lock (_lock)
            {
                // Remove expired timestamps
                var cutoff = now - _window;
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                {
                    _timestamps.Dequeue();
                }

                var count = _timestamps.Count;
                var isAllowed = count < limit;

                if (isAllowed)
                {
                    _timestamps.Enqueue(now);
                    count++;
                }

                var resetTime = _timestamps.Count > 0 
                    ? _timestamps.Peek() + _window 
                    : now + _window;

                return new RateLimitResult
                {
                    IsAllowed = isAllowed,
                    Limit = limit,
                    Remaining = Math.Max(0, limit - count),
                    ResetTime = resetTime,
                    RetryAfter = isAllowed ? null : resetTime - now
                };
            }
        }

        public bool IsExpired(DateTimeOffset now)
        {
            lock (_lock)
            {
                if (_timestamps.Count == 0)
                    return true;

                var newest = _timestamps.Max();
                return newest + _window < now;
            }
        }

        public RateLimitStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new RateLimitStatistics
                {
                    CurrentCount = _timestamps.Count,
                    WindowStart = _timestamps.Count > 0 ? _timestamps.Min() : DateTimeOffset.UtcNow
                };
            }
        }
    }

    // Configuration models
    public class RateLimitOptions
    {
        public RateLimitRule DefaultRule { get; set; } = new RateLimitRule
        {
            Name = "default",
            Limit = 100,
            Period = TimeSpan.FromMinutes(1)
        };

        public RateLimitRule AuthenticatedUserRule { get; set; } = new RateLimitRule
        {
            Name = "authenticated",
            Limit = 1000,
            Period = TimeSpan.FromMinutes(1)
        };

        public List<RateLimitRule> Rules { get; set; } = new();
        public List<string> ExcludedPaths { get; set; } = new() { "/health", "/metrics" };
        public List<string> WhitelistedIps { get; set; } = new();
        public List<string> WhitelistedRoles { get; set; } = new() { "Admin" };
        public string ApiKeyHeaderName { get; set; } = "X-API-Key";
        public string ExceededMessage { get; set; } = "Rate limit exceeded. Please try again later.";
    }

    public class RateLimitRule
    {
        public string Name { get; set; }
        public int Limit { get; set; }
        public TimeSpan Period { get; set; }
        public List<string> Endpoints { get; set; }
        public List<string> Roles { get; set; }
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public DateTimeOffset ResetTime { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }

    public class RateLimitStatistics
    {
        public long CurrentCount { get; set; }
        public DateTimeOffset WindowStart { get; set; }
    }
}