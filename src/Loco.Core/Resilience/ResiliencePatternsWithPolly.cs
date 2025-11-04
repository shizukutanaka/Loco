#nullable enable

using Microsoft.Extensions.Logging;

namespace Loco.Core.Resilience;

/// <summary>
/// Resilience patterns using Polly
/// - Circuit Breaker: Prevents cascading failures
/// - Retry: Handles transient failures
/// - Bulkhead: Isolates resources
/// - Timeout: Prevents indefinite blocking
/// - Fallback: Provides alternative response
/// </summary>

/// <summary>
/// Resilience policy configuration
/// </summary>
public class ResiliencePolicyConfig
{
    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial retry delay (milliseconds)
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 100;

    /// <summary>
    /// Backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum jitter (milliseconds)
    /// </summary>
    public int MaxJitterMs { get; set; } = 50;

    /// <summary>
    /// Circuit breaker failure threshold (%)
    /// </summary>
    public double FailureThreshold { get; set; } = 50.0;

    /// <summary>
    /// Sampling duration for circuit breaker (seconds)
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput for circuit breaker evaluation
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Timeout duration (milliseconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Bulkhead max parallelization
    /// </summary>
    public int BulkheadMaxParallelization { get; set; } = 10;

    /// <summary>
    /// Bulkhead max queue depth
    /// </summary>
    public int BulkheadQueueDepth { get; set; } = 5;
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Normal operation
    /// </summary>
    Closed,

    /// <summary>
    /// Failure threshold exceeded, blocking requests
    /// </summary>
    Open,

    /// <summary>
    /// Testing if service recovered
    /// </summary>
    HalfOpen
}

/// <summary>
/// Circuit breaker implementation
/// Prevents cascading failures by stopping calls to failing services
/// </summary>
public class CircuitBreaker
{
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount = 0;
    private int _successCount = 0;
    private int _requestCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly ResiliencePolicyConfig _config;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly object _lock = new object();

    public CircuitBreakerState State => _state;

    public CircuitBreaker(ResiliencePolicyConfig config, ILogger<CircuitBreaker> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        lock (_lock)
        {
            // Check if circuit should open
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime > TimeSpan.FromSeconds(_config.SamplingDurationSeconds))
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _successCount = 0;
                    _failureCount = 0;
                    _logger.LogWarning("Circuit breaker transitioned to HalfOpen");
                }
                else
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }
            }

            _requestCount++;
        }

        try
        {
            var result = await operation().ConfigureAwait(false);

            lock (_lock)
            {
                _successCount++;

                if (_state == CircuitBreakerState.HalfOpen)
                {
                    _state = CircuitBreakerState.Closed;
                    _failureCount = 0;
                    _requestCount = 0;
                    _logger.LogInformation("Circuit breaker closed (recovered)");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                var failurePercentage = (_failureCount * 100.0) / Math.Max(_requestCount, 1);

                if (_state == CircuitBreakerState.HalfOpen)
                {
                    _state = CircuitBreakerState.Open;
                    _logger.LogWarning("Circuit breaker reopened (still failing)");
                }
                else if (_requestCount >= _config.MinimumThroughput &&
                         failurePercentage >= _config.FailureThreshold)
                {
                    _state = CircuitBreakerState.Open;
                    _logger.LogError(
                        "Circuit breaker opened: {FailurePercentage:F2}% failure rate",
                        failurePercentage);
                }
            }

            throw;
        }
    }
}

/// <summary>
/// Retry policy with exponential backoff
/// </summary>
public class RetryPolicy
{
    private readonly ResiliencePolicyConfig _config;
    private readonly ILogger<RetryPolicy> _logger;
    private readonly Random _random = new Random();

    public RetryPolicy(ResiliencePolicyConfig config, ILogger<RetryPolicy> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool>? shouldRetry = null)
    {
        var lastException = default(Exception);
        var delay = _config.InitialRetryDelayMs;

        for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _config.MaxRetries)
            {
                // Check if we should retry this exception
                if (shouldRetry != null && !shouldRetry(ex))
                {
                    throw;
                }

                lastException = ex;

                var jitter = _random.Next(0, _config.MaxJitterMs);
                var actualDelay = delay + jitter;

                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt} failed, retrying after {Delay}ms",
                    attempt + 1,
                    actualDelay);

                await Task.Delay(actualDelay).ConfigureAwait(false);

                // Exponential backoff
                delay = (int)(delay * _config.BackoffMultiplier);
            }
            catch
            {
                throw;
            }
        }

        throw lastException ?? new InvalidOperationException("Retry operation failed");
    }

    public async Task ExecuteAsync(
        Func<Task> operation,
        Func<Exception, bool>? shouldRetry = null)
    {
        await ExecuteAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }, shouldRetry).ConfigureAwait(false);
    }
}

/// <summary>
/// Bulkhead pattern: Isolates resources
/// Prevents one failing component from exhausting shared resources
/// </summary>
public class BulkheadIsolation
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<BulkheadIsolation> _logger;
    private readonly ResiliencePolicyConfig _config;

    public BulkheadIsolation(ResiliencePolicyConfig config, ILogger<BulkheadIsolation> logger)
    {
        _config = config;
        _logger = logger;
        _semaphore = new SemaphoreSlim(
            config.BulkheadMaxParallelization,
            config.BulkheadMaxParallelization);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!_semaphore.Wait(TimeSpan.Zero))
        {
            throw new BulkheadIsolationException(
                $"Bulkhead at capacity ({_config.BulkheadMaxParallelization} max)");
        }

        try
        {
            _logger.LogDebug("Bulkhead: executing operation ({Available} available)",
                _semaphore.CurrentCount);

            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ExecuteAsync(Func<Task> operation)
    {
        await ExecuteAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }
}

/// <summary>
/// Timeout policy
/// </summary>
public class TimeoutPolicy
{
    private readonly ResiliencePolicyConfig _config;
    private readonly ILogger<TimeoutPolicy> _logger;

    public TimeoutPolicy(ResiliencePolicyConfig config, ILogger<TimeoutPolicy> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_config.TimeoutMs));

        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            _logger.LogError("Operation timed out after {Timeout}ms", _config.TimeoutMs);
            throw new TimeoutException($"Operation timed out after {_config.TimeoutMs}ms");
        }
    }
}

/// <summary>
/// Fallback policy: Provides alternative response on failure
/// </summary>
public class FallbackPolicy<T>
{
    private readonly Func<Exception, T> _fallbackFactory;
    private readonly ILogger<FallbackPolicy<T>> _logger;

    public FallbackPolicy(Func<Exception, T> fallbackFactory, ILogger<FallbackPolicy<T>> logger)
    {
        _fallbackFactory = fallbackFactory;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operation failed, using fallback");
            return _fallbackFactory(ex);
        }
    }
}

/// <summary>
/// Combined resilience policy: Wraps multiple patterns
/// Order: Timeout → Retry → Circuit Breaker → Bulkhead → Fallback
/// </summary>
public class CombinedResiliencePolicy<T>
{
    private readonly TimeoutPolicy _timeoutPolicy;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly BulkheadIsolation _bulkhead;
    private readonly FallbackPolicy<T>? _fallback;
    private readonly ILogger<CombinedResiliencePolicy<T>> _logger;

    public CombinedResiliencePolicy(
        TimeoutPolicy timeoutPolicy,
        RetryPolicy retryPolicy,
        CircuitBreaker circuitBreaker,
        BulkheadIsolation bulkhead,
        FallbackPolicy<T>? fallback,
        ILogger<CombinedResiliencePolicy<T>> logger)
    {
        _timeoutPolicy = timeoutPolicy;
        _retryPolicy = retryPolicy;
        _circuitBreaker = circuitBreaker;
        _bulkhead = bulkhead;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool>? shouldRetry = null)
    {
        try
        {
            // Order matters: most outer to most inner
            // Bulkhead → CircuitBreaker → Retry → Timeout

            return await _bulkhead.ExecuteAsync(async () =>
                await _circuitBreaker.ExecuteAsync(async () =>
                    await _retryPolicy.ExecuteAsync(
                        ct => _timeoutPolicy.ExecuteAsync(operation),
                        shouldRetry)
                )).ConfigureAwait(false);
        }
        catch (Exception ex) when (_fallback != null)
        {
            _logger.LogWarning(ex, "All resilience policies failed, using fallback");
            return await _fallback.ExecuteAsync(() => Task.FromResult(
                _fallback.ExecuteAsync(async () => throw ex))).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Resilience policy builder
/// </summary>
public class ResiliencePolicyBuilder<T>
{
    private readonly ResiliencePolicyConfig _config;
    private readonly ILogger _logger;
    private Func<Exception, T>? _fallbackFactory;
    private Func<Exception, bool>? _shouldRetry;

    public ResiliencePolicyBuilder(ResiliencePolicyConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public ResiliencePolicyBuilder<T> WithFallback(Func<Exception, T> fallbackFactory)
    {
        _fallbackFactory = fallbackFactory;
        return this;
    }

    public ResiliencePolicyBuilder<T> WithShouldRetry(Func<Exception, bool> predicate)
    {
        _shouldRetry = predicate;
        return this;
    }

    public CombinedResiliencePolicy<T> Build(IServiceProvider serviceProvider)
    {
        var timeoutPolicy = new TimeoutPolicy(_config, serviceProvider.GetRequiredService<ILogger<TimeoutPolicy>>());
        var retryPolicy = new RetryPolicy(_config, serviceProvider.GetRequiredService<ILogger<RetryPolicy>>());
        var circuitBreaker = new CircuitBreaker(_config, serviceProvider.GetRequiredService<ILogger<CircuitBreaker>>());
        var bulkhead = new BulkheadIsolation(_config, serviceProvider.GetRequiredService<ILogger<BulkheadIsolation>>());

        FallbackPolicy<T>? fallback = null;
        if (_fallbackFactory != null)
        {
            fallback = new FallbackPolicy<T>(_fallbackFactory, serviceProvider.GetRequiredService<ILogger<FallbackPolicy<T>>>());
        }

        return new CombinedResiliencePolicy<T>(
            timeoutPolicy,
            retryPolicy,
            circuitBreaker,
            bulkhead,
            fallback,
            serviceProvider.GetRequiredService<ILogger<CombinedResiliencePolicy<T>>>());
    }
}

/// <summary>
/// Exceptions
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message) { }
}

public class BulkheadIsolationException : Exception
{
    public BulkheadIsolationException(string message) : base(message) { }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ResilienceExtensions
{
    public static IServiceCollection AddResiliencePolicies(
        this IServiceCollection services,
        ResiliencePolicyConfig? config = null)
    {
        config ??= new ResiliencePolicyConfig();
        services.AddSingleton(config);

        return services;
    }

    public static ResiliencePolicyBuilder<T> CreateResiliencePolicyBuilder<T>(
        this IServiceProvider serviceProvider)
    {
        var config = serviceProvider.GetRequiredService<ResiliencePolicyConfig>();
        var logger = serviceProvider.GetRequiredService<ILogger>();
        return new ResiliencePolicyBuilder<T>(config, logger);
    }
}

/// <summary>
/// Example usage
/// </summary>
public class ResilientHttpClientExample
{
    private readonly CombinedResiliencePolicy<string> _policy;

    public ResilientHttpClientExample(
        HttpClient httpClient,
        IServiceProvider serviceProvider)
    {
        _policy = serviceProvider
            .CreateResiliencePolicyBuilder<string>()
            .WithFallback(ex => $"Service unavailable: {ex.Message}")
            .WithShouldRetry(ex => ex is HttpRequestException)
            .Build(serviceProvider);
    }

    public async Task<string> GetDataAsync(string url)
    {
        return await _policy.ExecuteAsync(async ct =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var response = await new HttpClient().GetStringAsync(url, cts.Token);
            return response;
        }).ConfigureAwait(false);
    }
}
