using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Async;

/// <summary>
/// High-performance async utilities and extensions
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Execute multiple tasks with timeout
    /// </summary>
    public static async Task<T[]> WhenAllWithTimeout<T>(
        IEnumerable<Task<T>> tasks,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }

    /// <summary>
    /// Execute task with retry and exponential backoff
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        CancellationToken cancellationToken = default)
    {
        initialDelay ??= TimeSpan.FromMilliseconds(100);
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt == maxRetries)
                    break;

                var delay = TimeSpan.FromMilliseconds(
                    initialDelay.Value.TotalMilliseconds * Math.Pow(backoffMultiplier, attempt)
                );

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new AggregateException(
            $"Operation failed after {maxRetries + 1} attempts",
            lastException ?? new Exception("Unknown error"));
    }

    /// <summary>
    /// Execute with circuit breaker pattern
    /// </summary>
    public static async Task<T> WithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        CircuitBreakerState state,
        CancellationToken cancellationToken = default)
    {
        if (state.IsOpen)
        {
            if (state.ShouldAttemptReset)
            {
                state.HalfOpen();
            }
            else
            {
                throw new InvalidOperationException("Circuit breaker is open");
            }
        }

        try
        {
            var result = await operation().ConfigureAwait(false);
            state.RecordSuccess();
            return result;
        }
        catch (Exception)
        {
            state.RecordFailure();
            throw;
        }
    }

    /// <summary>
    /// Async lazy initialization
    /// </summary>
    public static AsyncLazy<T> CreateAsyncLazy<T>(Func<Task<T>> factory)
    {
        return new AsyncLazy<T>(factory);
    }

    /// <summary>
    /// Convert Task to ValueTask for hot path optimization
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> AsValueTask<T>(this Task<T> task)
    {
        // Always wrap in ValueTask to avoid .Result deadlock risk
        // ValueTask constructor handles completed tasks efficiently
        return new ValueTask<T>(task);
    }

    /// <summary>
    /// Parallel processing with degree of parallelism control
    /// </summary>
    public static async Task<T[]> ParallelAsync<TSource, T>(
        IEnumerable<TSource> source,
        Func<TSource, Task<T>> operation,
        int maxDegreeOfParallelism = 4,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task<T>>();

        foreach (var item in source)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            var task = Task.Run(async () =>
            {
                try
                {
                    return await operation(item).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Async enumerable batching
    /// </summary>
    public static async IAsyncEnumerable<T[]> BatchAsync<T>(
        this IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batch = new List<T>(batchSize);

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }

    /// <summary>
    /// Throttle async operations
    /// </summary>
    public static async IAsyncEnumerable<T> ThrottleAsync<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastYield = DateTime.UtcNow;

        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var elapsed = DateTime.UtcNow - lastYield;
            var delay = interval - elapsed;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            lastYield = DateTime.UtcNow;
            yield return item;
        }
    }
}

/// <summary>
/// Thread-safe async lazy initialization
/// </summary>
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _instance;

    public AsyncLazy(Func<Task<T>> factory)
    {
        _instance = new Lazy<Task<T>>(factory);
    }

    public Task<T> Value => _instance.Value;

    public bool IsValueCreated => _instance.IsValueCreated;

    public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();
}

/// <summary>
/// Circuit breaker state management
/// </summary>
public class CircuitBreakerState
{
    private int _failureCount;
    private DateTime? _lastFailureTime;
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private CircuitState _state = CircuitState.Closed;

    public CircuitBreakerState(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(60);
    }

    public bool IsOpen => _state == CircuitState.Open;
    public bool IsClosed => _state == CircuitState.Closed;
    public bool IsHalfOpen => _state == CircuitState.HalfOpen;

    public bool ShouldAttemptReset
    {
        get
        {
            if (_state != CircuitState.Open || _lastFailureTime == null)
                return false;

            return DateTime.UtcNow - _lastFailureTime.Value >= _resetTimeout;
        }
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitState.Closed;
        _lastFailureTime = null;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitState.Open;
        }
    }

    public void HalfOpen()
    {
        _state = CircuitState.HalfOpen;
    }

    private enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }
}

/// <summary>
/// Async lock (SemaphoreSlim wrapper)
/// </summary>
public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Rate limiter for async operations
/// </summary>
public class AsyncRateLimiter
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _interval;
    private readonly Queue<DateTime> _timestamps = new();

    public AsyncRateLimiter(int requestsPerInterval, TimeSpan interval)
    {
        _semaphore = new SemaphoreSlim(requestsPerInterval, requestsPerInterval);
        _interval = interval;
    }

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_timestamps)
        {
            var now = DateTime.UtcNow;

            // Remove expired timestamps
            while (_timestamps.Count > 0 && now - _timestamps.Peek() > _interval)
            {
                _timestamps.Dequeue();
            }

            _timestamps.Enqueue(now);
        }

        return new RateLimitReleaser(_semaphore);
    }

    private class RateLimitReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public RateLimitReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}
