using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Security;

/// <summary>
/// Token bucket rate limiter for preventing abuse and DoS attacks
/// Thread-safe, high-performance implementation for production use
/// </summary>
public class RateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets;
    private readonly int _maxRequestsPerMinute;
    private readonly ILogger? _logger;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _bucketExpiration = TimeSpan.FromMinutes(10);

    public RateLimiter(int maxRequestsPerMinute, ILogger? logger = null)
    {
        if (maxRequestsPerMinute <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute));

        _maxRequestsPerMinute = maxRequestsPerMinute;
        _logger = logger;
        _buckets = new ConcurrentDictionary<string, TokenBucket>();

        // Cleanup expired buckets every minute
        _cleanupTimer = new Timer(CleanupExpiredBuckets, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Check if request is allowed under rate limit
    /// </summary>
    public RateLimitResult AllowRequest(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(identifier));

        var bucket = _buckets.GetOrAdd(identifier, _ => new TokenBucket(_maxRequestsPerMinute));

        var allowed = bucket.TryConsume();

        if (!allowed)
        {
            _logger?.LogWarning("Rate limit exceeded for: {Identifier}", identifier);
        }

        return new RateLimitResult
        {
            IsAllowed = allowed,
            Identifier = identifier,
            RemainingTokens = bucket.AvailableTokens,
            ResetTime = bucket.NextRefillTime
        };
    }

    /// <summary>
    /// Get rate limit status without consuming a token
    /// </summary>
    public RateLimitStatus GetStatus(string identifier)
    {
        if (_buckets.TryGetValue(identifier, out var bucket))
        {
            return new RateLimitStatus
            {
                Identifier = identifier,
                AvailableTokens = bucket.AvailableTokens,
                MaxTokens = _maxRequestsPerMinute,
                NextRefillTime = bucket.NextRefillTime
            };
        }

        return new RateLimitStatus
        {
            Identifier = identifier,
            AvailableTokens = _maxRequestsPerMinute,
            MaxTokens = _maxRequestsPerMinute,
            NextRefillTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Reset rate limit for specific identifier (admin operation)
    /// </summary>
    public void Reset(string identifier)
    {
        _buckets.TryRemove(identifier, out _);
        _logger?.LogInformation("Rate limit reset for: {Identifier}", identifier);
    }

    /// <summary>
    /// Get statistics about all rate limiters
    /// </summary>
    public RateLimiterStatistics GetStatistics()
    {
        var buckets = _buckets.Values.ToList();

        return new RateLimiterStatistics
        {
            TotalIdentifiers = buckets.Count,
            AverageTokensAvailable = buckets.Any() ? buckets.Average(b => b.AvailableTokens) : 0,
            IdentifiersAtLimit = buckets.Count(b => b.AvailableTokens == 0),
            MaxRequestsPerMinute = _maxRequestsPerMinute
        };
    }

    private void CleanupExpiredBuckets(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _buckets
            .Where(kvp => (now - kvp.Value.LastAccessTime) > _bucketExpiration)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _buckets.TryRemove(key, out _);
        }

        if (expiredKeys.Any())
        {
            _logger?.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Token bucket implementation for rate limiting
/// </summary>
internal class TokenBucket
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private int _tokens;
    private DateTime _lastRefill;

    public DateTime LastAccessTime { get; private set; }
    public int AvailableTokens => _tokens;
    public DateTime NextRefillTime => _lastRefill.AddMinutes(1);

    public TokenBucket(int capacity)
    {
        _capacity = capacity;
        _tokens = capacity;
        _lastRefill = DateTime.UtcNow;
        LastAccessTime = DateTime.UtcNow;
    }

    public bool TryConsume()
    {
        lock (_lock)
        {
            Refill();
            LastAccessTime = DateTime.UtcNow;

            if (_tokens > 0)
            {
                _tokens--;
                return true;
            }

            return false;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefill;

        if (elapsed.TotalMinutes >= 1)
        {
            // Refill tokens based on elapsed time
            var minutesPassed = (int)elapsed.TotalMinutes;
            _tokens = Math.Min(_capacity, _tokens + (_capacity * minutesPassed));
            _lastRefill = now;
        }
    }
}

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public int RemainingTokens { get; set; }
    public DateTime ResetTime { get; set; }
}

/// <summary>
/// Current status of rate limit for an identifier
/// </summary>
public class RateLimitStatus
{
    public string Identifier { get; set; } = string.Empty;
    public int AvailableTokens { get; set; }
    public int MaxTokens { get; set; }
    public DateTime NextRefillTime { get; set; }
}

/// <summary>
/// Statistics about the rate limiter
/// </summary>
public class RateLimiterStatistics
{
    public int TotalIdentifiers { get; set; }
    public double AverageTokensAvailable { get; set; }
    public int IdentifiersAtLimit { get; set; }
    public int MaxRequestsPerMinute { get; set; }
}
