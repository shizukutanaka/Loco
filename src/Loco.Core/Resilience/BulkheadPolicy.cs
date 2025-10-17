using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Resilience;

/// <summary>
/// Bulkhead isolation pattern to prevent resource exhaustion
/// Limits concurrent operations to prevent cascading failures
/// </summary>
public class BulkheadPolicy : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;
    private readonly int _maxQueueLength;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, ExecutionMetrics> _metrics;
    private int _currentExecutions;
    private int _queuedExecutions;
    private long _totalExecutions;
    private long _rejectedExecutions;

    public BulkheadPolicy(
        int maxConcurrency,
        int maxQueueLength = 0,
        ILogger? logger = null)
    {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        _maxConcurrency = maxConcurrency;
        _maxQueueLength = maxQueueLength;
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _metrics = new ConcurrentDictionary<string, ExecutionMetrics>();
    }

    /// <summary>
    /// Execute action within bulkhead constraints
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        string operationName = "operation",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // Check queue length
        if (_maxQueueLength > 0 && _queuedExecutions >= _maxQueueLength)
        {
            Interlocked.Increment(ref _rejectedExecutions);
            _logger?.LogWarning("Bulkhead rejected {Operation}: queue full ({Queued}/{MaxQueue})",
                operationName, _queuedExecutions, _maxQueueLength);

            throw new BulkheadRejectedException(
                $"Operation '{operationName}' rejected: bulkhead queue full ({_queuedExecutions}/{_maxQueueLength})");
        }

        Interlocked.Increment(ref _queuedExecutions);

        try
        {
            // Wait for available slot
            var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);
            var acquired = await _semaphore.WaitAsync(actualTimeout, cancellationToken);

            if (!acquired)
            {
                Interlocked.Increment(ref _rejectedExecutions);
                _logger?.LogWarning("Bulkhead timeout for {Operation} after {TimeoutMs}ms",
                    operationName, actualTimeout.TotalMilliseconds);

                throw new BulkheadRejectedException(
                    $"Operation '{operationName}' timeout: failed to acquire bulkhead slot within {actualTimeout.TotalMilliseconds}ms");
            }

            Interlocked.Decrement(ref _queuedExecutions);
            Interlocked.Increment(ref _currentExecutions);
            Interlocked.Increment(ref _totalExecutions);

            var metrics = _metrics.GetOrAdd(operationName, _ => new ExecutionMetrics());

            _logger?.LogDebug("Executing {Operation} (Active: {Active}/{Max}, Queued: {Queued})",
                operationName, _currentExecutions, _maxConcurrency, _queuedExecutions);

            try
            {
                var startTime = DateTime.UtcNow;
                var result = await action();
                var duration = DateTime.UtcNow - startTime;

                metrics.RecordSuccess(duration);

                _logger?.LogDebug("Completed {Operation} in {DurationMs}ms",
                    operationName, duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                metrics.RecordFailure();

                _logger?.LogError(ex, "Failed executing {Operation}: {ErrorMessage}",
                    operationName, ex.Message);

                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _currentExecutions);
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _queuedExecutions);
            Interlocked.Increment(ref _rejectedExecutions);
            throw;
        }
        catch (Exception ex) when (ex is not BulkheadRejectedException)
        {
            Interlocked.Decrement(ref _queuedExecutions);
            throw;
        }
    }

    /// <summary>
    /// Execute action within bulkhead constraints (void version)
    /// </summary>
    public async Task ExecuteAsync(
        Func<Task> action,
        string operationName = "operation",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, operationName, timeout, cancellationToken);
    }

    /// <summary>
    /// Get current bulkhead statistics
    /// </summary>
    public BulkheadStatistics GetStatistics()
    {
        return new BulkheadStatistics
        {
            MaxConcurrency = _maxConcurrency,
            CurrentExecutions = _currentExecutions,
            QueuedExecutions = _queuedExecutions,
            MaxQueueLength = _maxQueueLength,
            TotalExecutions = _totalExecutions,
            RejectedExecutions = _rejectedExecutions,
            AvailableSlots = _semaphore.CurrentCount,
            Utilization = (_currentExecutions / (double)_maxConcurrency) * 100
        };
    }

    /// <summary>
    /// Get metrics for specific operation
    /// </summary>
    public ExecutionMetrics? GetOperationMetrics(string operationName)
    {
        return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
    }

    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalExecutions, 0);
        Interlocked.Exchange(ref _rejectedExecutions, 0);
        _metrics.Clear();

        _logger?.LogInformation("Bulkhead statistics reset");
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}

/// <summary>
/// Exception thrown when bulkhead rejects an operation
/// </summary>
public class BulkheadRejectedException : Exception
{
    public BulkheadRejectedException(string message) : base(message) { }
}

/// <summary>
/// Statistics about bulkhead usage
/// </summary>
public class BulkheadStatistics
{
    public int MaxConcurrency { get; set; }
    public int CurrentExecutions { get; set; }
    public int QueuedExecutions { get; set; }
    public int MaxQueueLength { get; set; }
    public long TotalExecutions { get; set; }
    public long RejectedExecutions { get; set; }
    public int AvailableSlots { get; set; }
    public double Utilization { get; set; }

    public override string ToString()
    {
        return $"Active: {CurrentExecutions}/{MaxConcurrency} ({Utilization:F1}%), " +
               $"Queued: {QueuedExecutions}, Total: {TotalExecutions}, Rejected: {RejectedExecutions}";
    }
}

/// <summary>
/// Execution metrics for monitoring
/// </summary>
public class ExecutionMetrics
{
    private long _successCount;
    private long _failureCount;
    private double _totalDurationMs;
    private readonly object _lock = new();

    public long SuccessCount => _successCount;
    public long FailureCount => _failureCount;
    public long TotalCount => _successCount + _failureCount;
    public double AverageDurationMs => _totalDurationMs / Math.Max(1, _successCount);

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _successCount);
            _totalDurationMs += duration.TotalMilliseconds;
        }
    }

    public void RecordFailure()
    {
        Interlocked.Increment(ref _failureCount);
    }
}

/// <summary>
/// Bulkhead pool for managing multiple bulkheads by name
/// </summary>
public class BulkheadPool : IDisposable
{
    private readonly ConcurrentDictionary<string, BulkheadPolicy> _bulkheads;
    private readonly int _defaultMaxConcurrency;
    private readonly int _defaultMaxQueueLength;
    private readonly ILogger? _logger;

    public BulkheadPool(
        int defaultMaxConcurrency = 10,
        int defaultMaxQueueLength = 20,
        ILogger? logger = null)
    {
        _defaultMaxConcurrency = defaultMaxConcurrency;
        _defaultMaxQueueLength = defaultMaxQueueLength;
        _logger = logger;
        _bulkheads = new ConcurrentDictionary<string, BulkheadPolicy>();
    }

    /// <summary>
    /// Get or create bulkhead for operation category
    /// </summary>
    public BulkheadPolicy GetBulkhead(string category)
    {
        return _bulkheads.GetOrAdd(category, _ =>
        {
            _logger?.LogInformation("Creating bulkhead for category: {Category} (Max: {Max}, Queue: {Queue})",
                category, _defaultMaxConcurrency, _defaultMaxQueueLength);

            return new BulkheadPolicy(_defaultMaxConcurrency, _defaultMaxQueueLength, _logger);
        });
    }

    /// <summary>
    /// Execute action within named bulkhead
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string category,
        Func<Task<T>> action,
        string operationName = "operation",
        CancellationToken cancellationToken = default)
    {
        var bulkhead = GetBulkhead(category);
        return await bulkhead.ExecuteAsync(action, operationName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get statistics for all bulkheads
    /// </summary>
    public Dictionary<string, BulkheadStatistics> GetAllStatistics()
    {
        var stats = new Dictionary<string, BulkheadStatistics>();

        foreach (var kvp in _bulkheads)
        {
            stats[kvp.Key] = kvp.Value.GetStatistics();
        }

        return stats;
    }

    public void Dispose()
    {
        foreach (var bulkhead in _bulkheads.Values)
        {
            bulkhead.Dispose();
        }

        _bulkheads.Clear();
    }
}
