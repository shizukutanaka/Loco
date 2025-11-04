using Microsoft.Extensions.Logging;

namespace Loco.Core.Resilience;

/// <summary>
/// Resilience policy factory for creating Polly-like resilience policies
/// </summary>
public interface IResiliencePolicyFactory
{
    /// <summary>
    /// Creates a retry policy
    /// </summary>
    IResiliencePolicy CreateRetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null);

    /// <summary>
    /// Creates a circuit breaker policy
    /// </summary>
    IResiliencePolicy CreateCircuitBreakerPolicy(
        int failureThreshold = 5,
        TimeSpan? timeout = null,
        double failurePercentage = 0.5);

    /// <summary>
    /// Creates a timeout policy
    /// </summary>
    IResiliencePolicy CreateTimeoutPolicy(TimeSpan timeout);

    /// <summary>
    /// Creates a fallback policy
    /// </summary>
    IResiliencePolicy CreateFallbackPolicy<T>(Func<Task<T>> fallbackFactory);

    /// <summary>
    /// Creates a bulkhead isolation policy
    /// </summary>
    IResiliencePolicy CreateBulkheadPolicy(int maxParallelization = 10, int maxQueuingActions = 50);

    /// <summary>
    /// Creates a combined policy
    /// </summary>
    IResiliencePolicy CreateCombinedPolicy(params IResiliencePolicy[] policies);
}

/// <summary>
/// Resilience policy interface
/// </summary>
public interface IResiliencePolicy
{
    /// <summary>
    /// Policy name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Policy type
    /// </summary>
    ResiliencePolicyType PolicyType { get; }

    /// <summary>
    /// Executes an action with the policy
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action synchronously
    /// </summary>
    T Execute<T>(Func<T> action);

    /// <summary>
    /// Gets policy metrics
    /// </summary>
    ResiliencePolicyMetrics GetMetrics();

    /// <summary>
    /// Resets the policy
    /// </summary>
    void Reset();
}

/// <summary>
/// Resilience policy type
/// </summary>
public enum ResiliencePolicyType
{
    Retry,
    CircuitBreaker,
    Timeout,
    Fallback,
    Bulkhead,
    Combined
}

/// <summary>
/// Resilience policy metrics
/// </summary>
public class ResiliencePolicyMetrics
{
    /// <summary>
    /// Total executions
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Successful executions
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Failed executions
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Retries
    /// </summary>
    public long Retries { get; set; }

    /// <summary>
    /// Circuit breaker trips
    /// </summary>
    public long CircuitBreakerTrips { get; set; }

    /// <summary>
    /// Timeouts
    /// </summary>
    public long Timeouts { get; set; }

    /// <summary>
    /// Fallback invocations
    /// </summary>
    public long FallbackInvocations { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;

    /// <summary>
    /// Last execution time
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Average execution time (milliseconds)
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }
}

/// <summary>
/// Resilience policy implementation
/// </summary>
public class ResiliencePolicy : IResiliencePolicy
{
    private readonly ILogger<ResiliencePolicy> _logger;
    protected readonly ResiliencePolicyMetrics Metrics = new();

    public virtual string Name { get; protected set; } = "DefaultPolicy";
    public virtual ResiliencePolicyType PolicyType { get; protected set; } = ResiliencePolicyType.Retry;

    protected ResiliencePolicy(ILogger<ResiliencePolicy> logger)
    {
        _logger = logger;
    }

    public virtual async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Metrics.TotalExecutions++;

        try
        {
            var result = await action();
            Metrics.SuccessfulExecutions++;
            return result;
        }
        catch (Exception ex)
        {
            Metrics.FailedExecutions++;
            _logger.LogError(ex, "Policy {PolicyName} execution failed", Name);
            throw;
        }
        finally
        {
            Metrics.LastExecutionTime = DateTime.UtcNow;
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Metrics.AverageExecutionTimeMs = (Metrics.AverageExecutionTimeMs + duration) / 2;
        }
    }

    public virtual T Execute<T>(Func<T> action)
    {
        var startTime = DateTime.UtcNow;
        Metrics.TotalExecutions++;

        try
        {
            var result = action();
            Metrics.SuccessfulExecutions++;
            return result;
        }
        catch (Exception ex)
        {
            Metrics.FailedExecutions++;
            _logger.LogError(ex, "Policy {PolicyName} execution failed", Name);
            throw;
        }
        finally
        {
            Metrics.LastExecutionTime = DateTime.UtcNow;
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Metrics.AverageExecutionTimeMs = (Metrics.AverageExecutionTimeMs + duration) / 2;
        }
    }

    public ResiliencePolicyMetrics GetMetrics()
    {
        return new ResiliencePolicyMetrics
        {
            TotalExecutions = Metrics.TotalExecutions,
            SuccessfulExecutions = Metrics.SuccessfulExecutions,
            FailedExecutions = Metrics.FailedExecutions,
            Retries = Metrics.Retries,
            CircuitBreakerTrips = Metrics.CircuitBreakerTrips,
            Timeouts = Metrics.Timeouts,
            FallbackInvocations = Metrics.FallbackInvocations,
            LastExecutionTime = Metrics.LastExecutionTime,
            AverageExecutionTimeMs = Metrics.AverageExecutionTimeMs
        };
    }

    public virtual void Reset()
    {
        Metrics.TotalExecutions = 0;
        Metrics.SuccessfulExecutions = 0;
        Metrics.FailedExecutions = 0;
        Metrics.Retries = 0;
        Metrics.CircuitBreakerTrips = 0;
        Metrics.Timeouts = 0;
        Metrics.FallbackInvocations = 0;
        _logger.LogInformation("Policy {PolicyName} metrics reset", Name);
    }
}

/// <summary>
/// Retry policy implementation
/// </summary>
public class RetryPolicy : ResiliencePolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;

    public RetryPolicy(int maxRetries, TimeSpan? initialDelay, ILogger<ResiliencePolicy> logger)
        : base(logger)
    {
        Name = "RetryPolicy";
        PolicyType = ResiliencePolicyType.Retry;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
    }

    public override async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i <= _maxRetries; i++)
        {
            try
            {
                Metrics.TotalExecutions++;
                var result = await action();
                Metrics.SuccessfulExecutions++;
                return result;
            }
            catch (Exception ex) when (i < _maxRetries)
            {
                Metrics.Retries++;
                var delay = TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * Math.Pow(2, i));
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception)
            {
                Metrics.FailedExecutions++;
                throw;
            }
            finally
            {
                Metrics.LastExecutionTime = DateTime.UtcNow;
            }
        }

        throw new InvalidOperationException("Retry policy execution failed");
    }
}

/// <summary>
/// Circuit breaker policy implementation
/// </summary>
public class CircuitBreakerPolicy : ResiliencePolicy
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;

    public CircuitBreakerPolicy(int failureThreshold, TimeSpan? timeout, ILogger<ResiliencePolicy> logger)
        : base(logger)
    {
        Name = "CircuitBreakerPolicy";
        PolicyType = ResiliencePolicyType.CircuitBreaker;
        _failureThreshold = failureThreshold;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    public override async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitState.HalfOpen;
            }
            else
            {
                Metrics.CircuitBreakerTrips++;
                throw new InvalidOperationException("Circuit breaker is open");
            }
        }

        try
        {
            Metrics.TotalExecutions++;
            var result = await action();
            Metrics.SuccessfulExecutions++;

            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
            }

            return result;
        }
        catch (Exception)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold)
            {
                _state = CircuitState.Open;
                Metrics.CircuitBreakerTrips++;
            }

            Metrics.FailedExecutions++;
            throw;
        }
        finally
        {
            Metrics.LastExecutionTime = DateTime.UtcNow;
        }
    }

    public override void Reset()
    {
        base.Reset();
        _state = CircuitState.Closed;
        _failureCount = 0;
    }
}

/// <summary>
/// Circuit state enumeration
/// </summary>
public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}
