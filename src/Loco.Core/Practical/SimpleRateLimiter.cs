// Rob Pike: "Don't optimize for the edge cases"
// John Carmack: "The right data structure is usually the key to performance"

using System.Collections.Concurrent;

namespace Loco.Core.Practical;

/// <summary>
/// Simple rate limiter - Token bucket algorithm
/// Fast, thread-safe, no external dependencies
/// </summary>
public class SimpleRateLimiter
{
    private readonly int _capacity;
    private readonly int _tokensPerInterval;
    private readonly TimeSpan _interval;
    private int _tokens;
    private DateTime _lastRefill;
    private readonly object _lock = new();

    public SimpleRateLimiter(int capacity, int tokensPerInterval, TimeSpan interval)
    {
        _capacity = capacity;
        _tokensPerInterval = tokensPerInterval;
        _interval = interval;
        _tokens = capacity;
        _lastRefill = DateTime.UtcNow;
    }

    public bool TryAcquire(int tokens = 1)
    {
        lock (_lock)
        {
            RefillTokens();

            if (_tokens >= tokens)
            {
                _tokens -= tokens;
                return true;
            }

            return false;
        }
    }

    private void RefillTokens()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefill;

        if (elapsed >= _interval)
        {
            var intervalsElapsed = (int)(elapsed.TotalMilliseconds / _interval.TotalMilliseconds);
            var tokensToAdd = intervalsElapsed * _tokensPerInterval;
            _tokens = Math.Min(_tokens + tokensToAdd, _capacity);
            _lastRefill = now;
        }
    }

    public int GetAvailableTokens()
    {
        lock (_lock)
        {
            RefillTokens();
            return _tokens;
        }
    }
}

/// <summary>
/// Sliding window rate limiter - More accurate but uses more memory
/// </summary>
public class SlidingWindowRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requests = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    public SlidingWindowRateLimiter(int maxRequests, TimeSpan window)
    {
        _maxRequests = maxRequests;
        _window = window;
    }

    public bool TryAcquire(string key)
    {
        var queue = _requests.GetOrAdd(key, _ => new Queue<DateTime>());

        lock (queue)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - _window;

            // Remove old requests outside window
            while (queue.Count > 0 && queue.Peek() <= cutoff)
            {
                queue.Dequeue();
            }

            if (queue.Count < _maxRequests)
            {
                queue.Enqueue(now);
                return true;
            }

            return false;
        }
    }

    public int GetRequestCount(string key)
    {
        if (!_requests.TryGetValue(key, out var queue))
            return 0;

        lock (queue)
        {
            var cutoff = DateTime.UtcNow - _window;
            while (queue.Count > 0 && queue.Peek() <= cutoff)
            {
                queue.Dequeue();
            }
            return queue.Count;
        }
    }

    // Cleanup old entries to prevent memory leak
    public void Cleanup()
    {
        var cutoff = DateTime.UtcNow - _window;
        var emptyKeys = new List<string>();

        foreach (var kvp in _requests)
        {
            lock (kvp.Value)
            {
                while (kvp.Value.Count > 0 && kvp.Value.Peek() <= cutoff)
                {
                    kvp.Value.Dequeue();
                }

                if (kvp.Value.Count == 0)
                {
                    emptyKeys.Add(kvp.Key);
                }
            }
        }

        foreach (var key in emptyKeys)
        {
            _requests.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Per-IP rate limiter for web applications
/// </summary>
public class IpRateLimiter
{
    private readonly SlidingWindowRateLimiter _limiter;
    private readonly Timer _cleanupTimer;

    public IpRateLimiter(int requestsPerMinute = 60)
    {
        _limiter = new SlidingWindowRateLimiter(requestsPerMinute, TimeSpan.FromMinutes(1));

        // Cleanup every minute
        _cleanupTimer = new Timer(_ => _limiter.Cleanup(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public bool IsAllowed(string ipAddress) => _limiter.TryAcquire(ipAddress);

    public int GetRequestCount(string ipAddress) => _limiter.GetRequestCount(ipAddress);
}

/// <summary>
/// Decorator for rate-limited operations
/// </summary>
public class RateLimitedOperation<T>
{
    private readonly SimpleRateLimiter _limiter;
    private readonly Func<Task<T>> _operation;
    private readonly SimpleLogger _logger;

    public RateLimitedOperation(
        Func<Task<T>> operation,
        int maxCallsPerSecond = 10,
        SimpleLogger? logger = null)
    {
        _operation = operation;
        _limiter = new SimpleRateLimiter(
            capacity: maxCallsPerSecond,
            tokensPerInterval: maxCallsPerSecond,
            interval: TimeSpan.FromSeconds(1));
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(RateLimitedOperation<T>));
    }

    public async Task<T?> ExecuteAsync()
    {
        if (_limiter.TryAcquire())
        {
            return await _operation();
        }

        _logger.Warning("Rate limit exceeded");
        return default;
    }

    public async Task<T> ExecuteWithWaitAsync(int maxWaitMs = 1000)
    {
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalMilliseconds < maxWaitMs)
        {
            if (_limiter.TryAcquire())
            {
                return await _operation();
            }

            await Task.Delay(10); // Small delay before retry
        }

        throw new InvalidOperationException("Rate limit exceeded after waiting");
    }
}