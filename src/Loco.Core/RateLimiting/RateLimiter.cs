using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.RateLimiting
{
    /// <summary>
    /// Lightweight rate limiter implementation
    /// Following John Carmack's performance-first approach
    /// </summary>
    public interface IRateLimiter
    {
        Task<RateLimitResult> CheckRateLimitAsync(string key, int limit = 100, TimeSpan? window = null);
        Task<bool> TryAcquireAsync(string key, int tokens = 1);
        void Reset(string key);
        RateLimitStatus GetStatus(string key);
    }

    public class RateLimiter : IRateLimiter, IDisposable
    {
        private readonly ILogger<RateLimiter> _logger;
        private readonly ConcurrentDictionary<string, TokenBucket> _buckets;
        private readonly Timer _cleanupTimer;
        private readonly object _lock = new object();

        public RateLimiter(ILogger<RateLimiter> logger)
        {
            _logger = logger;
            _buckets = new ConcurrentDictionary<string, TokenBucket>();
            
            // Cleanup stale buckets every minute
            _cleanupTimer = new Timer(
                CleanupStaleBuckets, 
                null, 
                TimeSpan.FromMinutes(1), 
                TimeSpan.FromMinutes(1));
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, int limit = 100, TimeSpan? window = null)
        {
            var effectiveWindow = window ?? TimeSpan.FromHours(1);
            var bucket = GetOrCreateBucket(key, limit, effectiveWindow);
            
            var canProceed = bucket.TryConsume(1);
            
            return await Task.FromResult(new RateLimitResult
            {
                IsAllowed = canProceed,
                Limit = limit,
                Remaining = bucket.AvailableTokens,
                ResetAt = bucket.NextRefillTime,
                RetryAfter = canProceed ? null : bucket.TimeUntilNextToken
            });
        }

        public async Task<bool> TryAcquireAsync(string key, int tokens = 1)
        {
            if (tokens <= 0)
            {
                throw new ArgumentException("Tokens must be positive", nameof(tokens));
            }

            var bucket = GetOrCreateBucket(key, 100, TimeSpan.FromHours(1));
            return await Task.FromResult(bucket.TryConsume(tokens));
        }

        public void Reset(string key)
        {
            if (_buckets.TryRemove(key, out var bucket))
            {
                bucket.Dispose();
                _logger.LogDebug("Reset rate limit for key: {Key}", key);
            }
        }

        public RateLimitStatus GetStatus(string key)
        {
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                return new RateLimitStatus
                {
                    Key = key,
                    IsActive = false
                };
            }

            return new RateLimitStatus
            {
                Key = key,
                IsActive = true,
                CurrentTokens = bucket.AvailableTokens,
                MaxTokens = bucket.Capacity,
                NextRefillTime = bucket.NextRefillTime,
                RefillRate = bucket.RefillRate
            };
        }

        private TokenBucket GetOrCreateBucket(string key, int capacity, TimeSpan window)
        {
            return _buckets.GetOrAdd(key, k => new TokenBucket(capacity, window, _logger));
        }

        private void CleanupStaleBuckets(object state)
        {
            try
            {
                var staleTime = DateTime.UtcNow.AddHours(-1);
                var keysToRemove = new List<string>();

                foreach (var kvp in _buckets)
                {
                    if (kvp.Value.LastAccessTime < staleTime)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_buckets.TryRemove(key, out var bucket))
                    {
                        bucket.Dispose();
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} stale rate limit buckets", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rate limit bucket cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            foreach (var bucket in _buckets.Values)
            {
                bucket.Dispose();
            }
            _buckets.Clear();
        }
    }

    /// <summary>
    /// Token bucket algorithm implementation for rate limiting
    /// Efficient and simple, following Rob Pike's philosophy
    /// </summary>
    internal class TokenBucket : IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _lock = new object();
        private readonly Timer _refillTimer;
        
        private double _tokens;
        private DateTime _lastRefillTime;
        private DateTime _lastAccessTime;

        public int Capacity { get; }
        public TimeSpan Window { get; }
        public double RefillRate { get; }
        public int AvailableTokens => (int)Math.Floor(_tokens);
        public DateTime NextRefillTime { get; private set; }
        public DateTime LastAccessTime => _lastAccessTime;
        public TimeSpan? TimeUntilNextToken
        {
            get
            {
                if (_tokens >= 1) return null;
                var tokensNeeded = 1 - _tokens;
                var secondsNeeded = tokensNeeded / RefillRate;
                return TimeSpan.FromSeconds(secondsNeeded);
            }
        }

        public TokenBucket(int capacity, TimeSpan window, ILogger logger)
        {
            Capacity = capacity;
            Window = window;
            RefillRate = (double)capacity / window.TotalSeconds;
            _logger = logger;
            _tokens = capacity;
            _lastRefillTime = DateTime.UtcNow;
            _lastAccessTime = DateTime.UtcNow;
            NextRefillTime = DateTime.UtcNow.Add(TimeSpan.FromSeconds(1 / RefillRate));

            // Refill tokens periodically
            var refillInterval = TimeSpan.FromSeconds(Math.Max(1, window.TotalSeconds / capacity));
            _refillTimer = new Timer(Refill, null, refillInterval, refillInterval);
        }

        public bool TryConsume(int count)
        {
            lock (_lock)
            {
                _lastAccessTime = DateTime.UtcNow;
                Refill(null);

                if (_tokens >= count)
                {
                    _tokens -= count;
                    return true;
                }

                return false;
            }
        }

        private void Refill(object state)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var timePassed = now - _lastRefillTime;
                var tokensToAdd = timePassed.TotalSeconds * RefillRate;
                
                _tokens = Math.Min(Capacity, _tokens + tokensToAdd);
                _lastRefillTime = now;
                
                if (_tokens < Capacity)
                {
                    var tokensNeeded = Capacity - _tokens;
                    var secondsToFull = tokensNeeded / RefillRate;
                    NextRefillTime = now.AddSeconds(secondsToFull);
                }
                else
                {
                    NextRefillTime = now.Add(Window);
                }
            }
        }

        public void Dispose()
        {
            _refillTimer?.Dispose();
        }
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public DateTime ResetAt { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }

    public class RateLimitStatus
    {
        public string Key { get; set; }
        public bool IsActive { get; set; }
        public int CurrentTokens { get; set; }
        public int MaxTokens { get; set; }
        public DateTime NextRefillTime { get; set; }
        public double RefillRate { get; set; }
    }

    /// <summary>
    /// Sliding window rate limiter for more accurate rate limiting
    /// </summary>
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly ILogger<SlidingWindowRateLimiter> _logger;
        private readonly ConcurrentDictionary<string, SlidingWindow> _windows;
        private readonly Timer _cleanupTimer;

        public SlidingWindowRateLimiter(ILogger<SlidingWindowRateLimiter> logger)
        {
            _logger = logger;
            _windows = new ConcurrentDictionary<string, SlidingWindow>();
            _cleanupTimer = new Timer(
                CleanupStaleWindows,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, int limit = 100, TimeSpan? window = null)
        {
            var effectiveWindow = window ?? TimeSpan.FromHours(1);
            var slidingWindow = GetOrCreateWindow(key, limit, effectiveWindow);
            
            var result = slidingWindow.TryAdd();
            
            return await Task.FromResult(new RateLimitResult
            {
                IsAllowed = result.IsAllowed,
                Limit = limit,
                Remaining = result.Remaining,
                ResetAt = result.ResetAt,
                RetryAfter = result.RetryAfter
            });
        }

        public async Task<bool> TryAcquireAsync(string key, int tokens = 1)
        {
            var result = await CheckRateLimitAsync(key);
            return result.IsAllowed;
        }

        public void Reset(string key)
        {
            if (_windows.TryRemove(key, out var window))
            {
                _logger.LogDebug("Reset sliding window for key: {Key}", key);
            }
        }

        public RateLimitStatus GetStatus(string key)
        {
            if (!_windows.TryGetValue(key, out var window))
            {
                return new RateLimitStatus
                {
                    Key = key,
                    IsActive = false
                };
            }

            var count = window.GetCurrentCount();
            return new RateLimitStatus
            {
                Key = key,
                IsActive = true,
                CurrentTokens = window.Limit - count,
                MaxTokens = window.Limit,
                NextRefillTime = DateTime.UtcNow.Add(window.Window)
            };
        }

        private SlidingWindow GetOrCreateWindow(string key, int limit, TimeSpan window)
        {
            return _windows.GetOrAdd(key, k => new SlidingWindow(limit, window));
        }

        private void CleanupStaleWindows(object state)
        {
            try
            {
                var staleTime = DateTime.UtcNow.AddHours(-2);
                var keysToRemove = new List<string>();

                foreach (var kvp in _windows)
                {
                    if (kvp.Value.LastAccessTime < staleTime)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _windows.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} stale sliding windows", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sliding window cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _windows.Clear();
        }
    }

    internal class SlidingWindow
    {
        private readonly object _lock = new object();
        private readonly Queue<DateTime> _timestamps;
        
        public int Limit { get; }
        public TimeSpan Window { get; }
        public DateTime LastAccessTime { get; private set; }

        public SlidingWindow(int limit, TimeSpan window)
        {
            Limit = limit;
            Window = window;
            _timestamps = new Queue<DateTime>();
            LastAccessTime = DateTime.UtcNow;
        }

        public (bool IsAllowed, int Remaining, DateTime ResetAt, TimeSpan? RetryAfter) TryAdd()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                LastAccessTime = now;
                var windowStart = now - Window;

                // Remove expired timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count < Limit)
                {
                    _timestamps.Enqueue(now);
                    var remaining = Limit - _timestamps.Count;
                    var resetAt = _timestamps.Count > 0 ? _timestamps.Peek() + Window : now + Window;
                    
                    return (true, remaining, resetAt, null);
                }

                // Calculate retry after
                var oldestTimestamp = _timestamps.Peek();
                var retryAfter = (oldestTimestamp + Window) - now;
                
                return (false, 0, oldestTimestamp + Window, retryAfter);
            }
        }

        public int GetCurrentCount()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var windowStart = now - Window;

                // Remove expired timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                return _timestamps.Count;
            }
        }
    }
}
