using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Loco.Core.Middleware;

namespace Loco.Core.RateLimiting
{
    public interface IRateLimitService
    {
        Task<bool> IsAllowedAsync(string key, int limit = 100, TimeSpan? period = null);
        Task<RateLimitResult> CheckRateLimitAsync(string key, int limit = 100, TimeSpan? period = null);
        void Reset(string key);
        void ResetAll();
        RateLimitStatus GetStatus(string key);
    }


    public class AdvancedRateLimitService : IRateLimitService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<AdvancedRateLimitService> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private readonly TimeSpan _defaultPeriod = TimeSpan.FromMinutes(1);

        // Advanced rate limiting strategies
        private readonly ConcurrentDictionary<string, TokenBucket> _tokenBuckets;
        private readonly ConcurrentDictionary<string, SlidingWindow> _slidingWindows;
        private readonly ConcurrentDictionary<string, LeakyBucket> _leakyBuckets;

        public AdvancedRateLimitService(
            IMemoryCache cache,
            ILogger<AdvancedRateLimitService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _tokenBuckets = new ConcurrentDictionary<string, TokenBucket>();
            _slidingWindows = new ConcurrentDictionary<string, SlidingWindow>();
            _leakyBuckets = new ConcurrentDictionary<string, LeakyBucket>();
        }

        public async Task<bool> IsAllowedAsync(string key, int limit = 100, TimeSpan? period = null)
        {
            var result = await CheckRateLimitAsync(key, limit, period);
            return result.IsAllowed;
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string key, int limit = 100, TimeSpan? period = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            if (limit <= 0)
                throw new ArgumentException("Limit must be greater than 0", nameof(limit));

            var effectivePeriod = period ?? _defaultPeriod;
            var lockKey = $"lock_{key}";
            var semaphore = _locks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                // Use sliding window algorithm for more accurate rate limiting
                var window = _slidingWindows.GetOrAdd(key, _ => new SlidingWindow(limit, effectivePeriod));
                var now = DateTime.UtcNow;
                
                window.CleanOldRequests(now);
                
                var currentCount = window.GetRequestCount();
                var isAllowed = currentCount < limit;
                
                if (isAllowed)
                {
                    window.AddRequest(now);
                    currentCount++;
                }

                var resetTime = window.GetResetTime(now);
                var remaining = Math.Max(0, limit - currentCount);
                var retryAfter = isAllowed ? TimeSpan.Zero : resetTime - now;

                var result = new RateLimitResult
                {
                    IsAllowed = isAllowed,
                    Remaining = remaining,
                    ResetTime = resetTime,
                    Limit = limit,
                    Key = key,
                    RetryAfter = retryAfter
                };

                if (!isAllowed)
                {
                    _logger.LogWarning($"Rate limit exceeded for key: {key}. Limit: {limit}, Period: {effectivePeriod}");
                }

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Reset(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _slidingWindows.TryRemove(key, out _);
            _tokenBuckets.TryRemove(key, out _);
            _leakyBuckets.TryRemove(key, out _);
            
            var cacheKey = $"ratelimit_{key}";
            _cache.Remove(cacheKey);
            
            _logger.LogInformation($"Rate limit reset for key: {key}");
        }

        public void ResetAll()
        {
            _slidingWindows.Clear();
            _tokenBuckets.Clear();
            _leakyBuckets.Clear();
            
            // Note: We can't easily clear all rate limit entries from cache
            // without affecting other cached items
            
            _logger.LogInformation("All rate limits reset");
        }

        public RateLimitStatus GetStatus(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (_slidingWindows.TryGetValue(key, out var window))
            {
                var now = DateTime.UtcNow;
                window.CleanOldRequests(now);
                
                return new RateLimitStatus
                {
                    Key = key,
                    Count = window.GetRequestCount(),
                    Limit = window.Limit,
                    WindowStart = now - window.Period,
                    WindowEnd = now,
                    IsThrottled = window.GetRequestCount() >= window.Limit
                };
            }

            return new RateLimitStatus
            {
                Key = key,
                Count = 0,
                Limit = 0,
                WindowStart = DateTime.UtcNow,
                WindowEnd = DateTime.UtcNow,
                IsThrottled = false
            };
        }

        // Token Bucket implementation for burst allowance
        public async Task<bool> IsAllowedTokenBucketAsync(string key, int capacity, int refillRate, TimeSpan refillPeriod)
        {
            var bucket = _tokenBuckets.GetOrAdd(key, _ => new TokenBucket(capacity, refillRate, refillPeriod));
            return await bucket.TryConsumeAsync();
        }

        // Leaky Bucket implementation for smooth rate limiting
        public async Task<bool> IsAllowedLeakyBucketAsync(string key, int capacity, int leakRate, TimeSpan leakPeriod)
        {
            var bucket = _leakyBuckets.GetOrAdd(key, _ => new LeakyBucket(capacity, leakRate, leakPeriod));
            return await bucket.TryAddAsync();
        }

        private class SlidingWindow
        {
            private readonly Queue<DateTime> _requests = new Queue<DateTime>();
            private readonly object _lock = new object();

            public int Limit { get; }
            public TimeSpan Period { get; }

            public SlidingWindow(int limit, TimeSpan period)
            {
                Limit = limit;
                Period = period;
            }

            public void AddRequest(DateTime timestamp)
            {
                lock (_lock)
                {
                    _requests.Enqueue(timestamp);
                }
            }

            public void CleanOldRequests(DateTime now)
            {
                lock (_lock)
                {
                    var cutoff = now - Period;
                    while (_requests.Count > 0 && _requests.Peek() < cutoff)
                    {
                        _requests.Dequeue();
                    }
                }
            }

            public int GetRequestCount()
            {
                lock (_lock)
                {
                    return _requests.Count;
                }
            }

            public DateTime GetResetTime(DateTime now)
            {
                lock (_lock)
                {
                    if (_requests.Count > 0)
                    {
                        return _requests.Peek() + Period;
                    }
                    return now + Period;
                }
            }
        }

        private class TokenBucket
        {
            private int _tokens;
            private DateTime _lastRefill;
            private readonly int _capacity;
            private readonly int _refillRate;
            private readonly TimeSpan _refillPeriod;
            private readonly SemaphoreSlim _semaphore;

            public TokenBucket(int capacity, int refillRate, TimeSpan refillPeriod)
            {
                _capacity = capacity;
                _tokens = capacity;
                _refillRate = refillRate;
                _refillPeriod = refillPeriod;
                _lastRefill = DateTime.UtcNow;
                _semaphore = new SemaphoreSlim(1, 1);
            }

            public async Task<bool> TryConsumeAsync(int tokens = 1)
            {
                await _semaphore.WaitAsync();
                try
                {
                    Refill();
                    
                    if (_tokens >= tokens)
                    {
                        _tokens -= tokens;
                        return true;
                    }
                    
                    return false;
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            private void Refill()
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastRefill;
                
                if (elapsed >= _refillPeriod)
                {
                    var periods = (int)(elapsed.TotalMilliseconds / _refillPeriod.TotalMilliseconds);
                    var tokensToAdd = periods * _refillRate;
                    _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                    _lastRefill = now;
                }
            }
        }

        private class LeakyBucket
        {
            private int _water;
            private DateTime _lastLeak;
            private readonly int _capacity;
            private readonly int _leakRate;
            private readonly TimeSpan _leakPeriod;
            private readonly SemaphoreSlim _semaphore;

            public LeakyBucket(int capacity, int leakRate, TimeSpan leakPeriod)
            {
                _capacity = capacity;
                _water = 0;
                _leakRate = leakRate;
                _leakPeriod = leakPeriod;
                _lastLeak = DateTime.UtcNow;
                _semaphore = new SemaphoreSlim(1, 1);
            }

            public async Task<bool> TryAddAsync(int amount = 1)
            {
                await _semaphore.WaitAsync();
                try
                {
                    Leak();
                    
                    if (_water + amount <= _capacity)
                    {
                        _water += amount;
                        return true;
                    }
                    
                    return false;
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            private void Leak()
            {
                var now = DateTime.UtcNow;
                var elapsed = now - _lastLeak;
                
                if (elapsed >= _leakPeriod)
                {
                    var periods = (int)(elapsed.TotalMilliseconds / _leakPeriod.TotalMilliseconds);
                    var amountToLeak = periods * _leakRate;
                    _water = Math.Max(0, _water - amountToLeak);
                    _lastLeak = now;
                }
            }
        }
    }

    // Rate limit attribute for MVC/API controllers
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RateLimitAttribute : Attribute
    {
        public int Limit { get; set; } = 100;
        public int PeriodInSeconds { get; set; } = 60;
        public string Key { get; set; }
        public RateLimitStrategy Strategy { get; set; } = RateLimitStrategy.SlidingWindow;
    }

    public enum RateLimitStrategy
    {
        SlidingWindow,
        TokenBucket,
        LeakyBucket,
        FixedWindow
    }

}