using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Resilience
{
    /// <summary>
    /// Advanced retry policy for error handling
    /// Supports exponential backoff, jitter, and conditional retries
    /// Inspired by Polly library and enterprise automation patterns
    /// </summary>
    public class RetryPolicy
    {
        private readonly RetryPolicyConfig _config;
        private readonly ILogger? _logger;
        private static readonly Dictionary<int, double> _fibonacciCache = new();

        public RetryPolicy(RetryPolicyConfig config, ILogger? logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            ValidateConfig(_config);
            _logger = logger;
        }

        /// <summary>
        /// Execute action with retry logic
        /// </summary>
        public Task<T> ExecuteAsync<T>(Func<Task<T>> action, string? operationName = null, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return ExecuteAsync(_ => action(), operationName, cancellationToken);
        }

        /// <summary>
        /// Execute action with retry logic (void return)
        /// </summary>
        public async Task ExecuteAsync(Func<Task> action, string? operationName = null, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            await ExecuteAsync(async _ =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, operationName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute action with retry logic and timeout
        /// </summary>
        public async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<Task<T>> action,
            TimeSpan timeout,
            string? operationName = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return await ExecuteAsync(action, operationName, cts.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute action with retry logic and timeout (void return)
        /// </summary>
        public async Task ExecuteWithTimeoutAsync(
            Func<Task> action,
            TimeSpan timeout,
            string? operationName = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            await ExecuteAsync(action, operationName, cts.Token).ConfigureAwait(false);
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, string? operationName = null, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var attempt = 0;
            var exceptions = new List<Exception>();
            var maxAttempts = _config.MaxRetries + 1;

            while (attempt <= _config.MaxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var currentAttemptNumber = attempt + 1;

                    if (currentAttemptNumber > 1)
                    {
                        _logger?.LogInformation("Retry attempt {Attempt}/{Max} for operation: {Operation}",
                            currentAttemptNumber, maxAttempts, operationName ?? "unknown");
                    }

                    var result = await action(cancellationToken).ConfigureAwait(false);

                    if (currentAttemptNumber > 1)
                    {
                        _logger?.LogInformation("Operation succeeded on attempt {Attempt}/{Max}: {Operation}",
                            currentAttemptNumber, maxAttempts, operationName ?? "unknown");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    exceptions.Add(ex);
                    var failureAttemptNumber = attempt + 1;

                    if (!ShouldRetry(ex, attempt))
                    {
                        _logger?.LogError(ex, "Operation failed and will not retry: {Operation}", operationName ?? "unknown");
                        throw new RetryException($"Operation failed: {operationName}", exceptions);
                    }

                    if (attempt >= _config.MaxRetries)
                    {
                        _logger?.LogError(ex, "Operation failed after {Attempts} attempts: {Operation}",
                            failureAttemptNumber, operationName ?? "unknown");
                        throw new RetryException($"Operation failed after {_config.MaxRetries} retries: {operationName}", exceptions);
                    }

                    var delay = CalculateDelay(attempt);
                    var nextAttemptNumber = failureAttemptNumber + 1;
                    var retryContext = _config.OnRetryAsync != null
                        ? new RetryAttemptContext(failureAttemptNumber, nextAttemptNumber, maxAttempts, delay, ex, operationName)
                        : null;

                    if (retryContext != null)
                    {
                        await _config.OnRetryAsync!(retryContext, cancellationToken).ConfigureAwait(false);
                        delay = retryContext.NextDelay;
                    }

                    if (delay > _config.MaxDelay)
                        delay = _config.MaxDelay;

                    if (delay < TimeSpan.Zero)
                        delay = TimeSpan.Zero;

                    _logger?.LogWarning(ex, "Operation failed, retrying in {Delay}ms (attempt {Attempt}/{Max}): {Operation}",
                        delay.TotalMilliseconds, nextAttemptNumber, maxAttempts, operationName ?? "unknown");

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    attempt++;
                }
            }

            throw new RetryException($"Unexpected retry loop exit: {operationName}", exceptions);
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> action, string? operationName = null, CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            await ExecuteAsync(async token =>
            {
                await action(token).ConfigureAwait(false);
                return true;
            }, operationName, cancellationToken).ConfigureAwait(false);
        }

        public async Task<T> ExecuteWithTimeoutAsync<T>(
            Func<CancellationToken, Task<T>> action,
            TimeSpan timeout,
            string? operationName = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return await ExecuteAsync(action, operationName, cts.Token).ConfigureAwait(false);
        }

        public async Task ExecuteWithTimeoutAsync(
            Func<CancellationToken, Task> action,
            TimeSpan timeout,
            string? operationName = null,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            await ExecuteAsync(action, operationName, cts.Token).ConfigureAwait(false);
        }

        /// <summary>
        /// Check if exception should trigger retry
        /// </summary>
        private bool ShouldRetry(Exception ex, int attempt)
        {
            // Check retry condition
            if (_config.RetryCondition != null && !_config.RetryCondition(ex))
            {
                return false;
            }

            // Check retryable exceptions
            if (_config.RetryableExceptions.Count > 0)
            {
                var exceptionType = ex.GetType();
                var isRetryable = false;

                foreach (var retryableType in _config.RetryableExceptions)
                {
                    if (retryableType.IsAssignableFrom(exceptionType))
                    {
                        isRetryable = true;
                        break;
                    }
                }

                if (!isRetryable)
                    return false;
            }

            // Check non-retryable exceptions
            if (_config.NonRetryableExceptions.Count > 0)
            {
                var exceptionType = ex.GetType();

                foreach (var nonRetryableType in _config.NonRetryableExceptions)
                {
                    if (nonRetryableType.IsAssignableFrom(exceptionType))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Calculate retry delay with backoff strategy
        /// </summary>
        private TimeSpan CalculateDelay(int attempt)
        {
            TimeSpan delay;

            switch (_config.BackoffStrategy)
            {
                case BackoffStrategy.Constant:
                    delay = _config.InitialDelay;
                    break;

                case BackoffStrategy.Linear:
                    delay = TimeSpan.FromMilliseconds(_config.InitialDelay.TotalMilliseconds * (attempt + 1));
                    break;

                case BackoffStrategy.Exponential:
                    delay = TimeSpan.FromMilliseconds(_config.InitialDelay.TotalMilliseconds * Math.Pow(_config.ExponentialMultiplier, attempt));
                    break;

                case BackoffStrategy.Fibonacci:
                    delay = TimeSpan.FromMilliseconds(_config.InitialDelay.TotalMilliseconds * FibonacciMemoized(attempt + 1));
                    break;

                default:
                    delay = _config.InitialDelay;
                    break;
            }

            // Apply jitter
            if (_config.UseJitter && delay > TimeSpan.Zero && _config.JitterFactor > 0)
            {
                var jitterRangeMilliseconds = delay.TotalMilliseconds * _config.JitterFactor;
                if (jitterRangeMilliseconds > double.Epsilon)
                {
                    var jitterOffset = (Random.Shared.NextDouble() * 2 - 1) * jitterRangeMilliseconds;
                    delay = delay.Add(TimeSpan.FromMilliseconds(jitterOffset));
                }
            }

            // Ensure delay is within bounds
            return delay;
        }

        private static double FibonacciMemoized(int n)
        {
            if (n <= 1) return n;

            if (_fibonacciCache.TryGetValue(n, out var cachedResult))
            {
                return cachedResult;
            }

            var result = FibonacciMemoized(n - 1) + FibonacciMemoized(n - 2);
            _fibonacciCache[n] = result;
            return result;
        }

        private static void ValidateConfig(RetryPolicyConfig config)
        {
            if (config.MaxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxRetries), config.MaxRetries, "MaxRetries must be zero or greater.");

            if (config.InitialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(config.InitialDelay), config.InitialDelay, "InitialDelay must not be negative.");

            if (config.MaxDelay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(config.MaxDelay), config.MaxDelay, "MaxDelay must be greater than zero.");

            if (config.MaxDelay < config.InitialDelay)
                throw new ArgumentException("MaxDelay must be greater than or equal to InitialDelay.", nameof(config.MaxDelay));

            if (config.ExponentialMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.ExponentialMultiplier), config.ExponentialMultiplier, "ExponentialMultiplier must be greater than zero.");

            if (config.JitterFactor < 0)
                throw new ArgumentOutOfRangeException(nameof(config.JitterFactor), config.JitterFactor, "JitterFactor must be zero or greater.");

            config.RetryableExceptions ??= new List<Type>();
            config.NonRetryableExceptions ??= new List<Type>();
        }
    }

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicyConfig
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
        public bool UseJitter { get; set; } = true;
        public double ExponentialMultiplier { get; set; } = 2.0;
        public double JitterFactor { get; set; } = 0.3;
        public List<Type> RetryableExceptions { get; set; } = new();
        public List<Type> NonRetryableExceptions { get; set; } = new();
        public Func<Exception, bool>? RetryCondition { get; set; }
        public Func<RetryAttemptContext, CancellationToken, Task>? OnRetryAsync { get; set; }
    }

    /// <summary>
    /// Backoff strategy for retry delays
    /// </summary>
    public enum BackoffStrategy
    {
        Constant,       // Same delay every time
        Linear,         // Delay increases linearly
        Exponential,    // Delay doubles each time
        Fibonacci       // Delay follows Fibonacci sequence
    }

    /// <summary>
    /// Retry exception containing all retry attempts
    /// </summary>
    public class RetryException : Exception
    {
        public IReadOnlyList<Exception> Attempts { get; }

        public RetryException(string message, List<Exception> attempts) : base(message)
        {
            Attempts = attempts.AsReadOnly();
        }

        public override string ToString()
        {
            var details = string.Join("\n", Attempts.Select((ex, i) => $"Attempt {i + 1}: {ex.Message}"));
            return $"{Message}\n\nAttempt details:\n{details}";
        }
    }

    /// <summary>
    /// Retry policy builder for fluent configuration
    /// </summary>
    public class RetryPolicyBuilder
    {
        private readonly RetryPolicyConfig _config = new();

        public RetryPolicyBuilder WithMaxRetries(int maxRetries)
        {
            _config.MaxRetries = maxRetries;
            return this;
        }

        public RetryPolicyBuilder WithDelay(TimeSpan initialDelay, TimeSpan? maxDelay = null)
        {
            _config.InitialDelay = initialDelay;
            if (maxDelay.HasValue)
                _config.MaxDelay = maxDelay.Value;
            return this;
        }

        public RetryPolicyBuilder WithExponentialBackoff(double multiplier = 2.0)
        {
            _config.BackoffStrategy = BackoffStrategy.Exponential;
            _config.ExponentialMultiplier = multiplier;
            return this;
        }

        public RetryPolicyBuilder WithLinearBackoff()
        {
            _config.BackoffStrategy = BackoffStrategy.Linear;
            return this;
        }

        public RetryPolicyBuilder WithConstantDelay()
        {
            _config.BackoffStrategy = BackoffStrategy.Constant;
            return this;
        }

        public RetryPolicyBuilder WithJitter(bool useJitter = true)
        {
            _config.UseJitter = useJitter;
            if (!useJitter)
            {
                _config.JitterFactor = 0;
            }
            return this;
        }

        public RetryPolicyBuilder WithJitterFactor(double jitterFactor)
        {
            _config.JitterFactor = jitterFactor;
            _config.UseJitter = jitterFactor > 0;
            return this;
        }

        public RetryPolicyBuilder WithExponentialMultiplier(double multiplier)
        {
            _config.ExponentialMultiplier = multiplier;
            return this;
        }

        public RetryPolicyBuilder HandleException<TException>() where TException : Exception
        {
            _config.RetryableExceptions.Add(typeof(TException));
            return this;
        }

        public RetryPolicyBuilder DontHandleException<TException>() where TException : Exception
        {
            _config.NonRetryableExceptions.Add(typeof(TException));
            return this;
        }

        public RetryPolicyBuilder WithCondition(Func<Exception, bool> condition)
        {
            _config.RetryCondition = condition;
            return this;
        }

        public RetryPolicyBuilder OnRetryAsync(Func<RetryAttemptContext, CancellationToken, Task> onRetryAsync)
        {
            if (onRetryAsync == null) throw new ArgumentNullException(nameof(onRetryAsync));
            _config.OnRetryAsync = onRetryAsync;
            return this;
        }

        public RetryPolicy Build(ILogger? logger = null)
        {
            var clonedConfig = new RetryPolicyConfig
            {
                MaxRetries = _config.MaxRetries,
                InitialDelay = _config.InitialDelay,
                MaxDelay = _config.MaxDelay,
                BackoffStrategy = _config.BackoffStrategy,
                UseJitter = _config.UseJitter,
                ExponentialMultiplier = _config.ExponentialMultiplier,
                JitterFactor = _config.JitterFactor,
                RetryCondition = _config.RetryCondition,
                RetryableExceptions = new List<Type>(_config.RetryableExceptions ?? new List<Type>()),
                NonRetryableExceptions = new List<Type>(_config.NonRetryableExceptions ?? new List<Type>()),
                OnRetryAsync = _config.OnRetryAsync
            };

            return new RetryPolicy(clonedConfig, logger);
        }
    }

    /// <summary>
    /// Context provided to retry callbacks.
    /// </summary>
    public sealed class RetryAttemptContext
    {
        public RetryAttemptContext(int failedAttemptNumber, int nextAttemptNumber, int maxAttempts, TimeSpan nextDelay, Exception lastException, string? operationName)
        {
            if (failedAttemptNumber <= 0) throw new ArgumentOutOfRangeException(nameof(failedAttemptNumber));
            if (nextAttemptNumber <= 0) throw new ArgumentOutOfRangeException(nameof(nextAttemptNumber));
            if (maxAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            LastException = lastException ?? throw new ArgumentNullException(nameof(lastException));

            FailedAttemptNumber = failedAttemptNumber;
            NextAttemptNumber = nextAttemptNumber;
            MaxAttempts = maxAttempts;
            NextDelay = nextDelay;
            OperationName = operationName;
        }

        /// <summary>
        /// Gets the 1-based index of the failed attempt.
        /// </summary>
        public int FailedAttemptNumber { get; }

        /// <summary>
        /// Gets the 1-based index of the upcoming retry attempt.
        /// </summary>
        public int NextAttemptNumber { get; }

        /// <summary>
        /// Gets the maximum number of attempts permitted (including the initial attempt).
        /// </summary>
        public int MaxAttempts { get; }

        /// <summary>
        /// Gets or sets the delay to wait before the next retry attempt.
        /// </summary>
        public TimeSpan NextDelay { get; set; }

        /// <summary>
        /// Gets the exception raised by the failed attempt.
        /// </summary>
        public Exception LastException { get; }

        /// <summary>
        /// Gets the operation name when supplied.
        /// </summary>
        public string? OperationName { get; }

        /// <summary>
        /// Gets a value indicating whether the upcoming retry is the final permitted attempt.
        /// </summary>
        public bool IsFinalAttempt => NextAttemptNumber >= MaxAttempts;
    }
}
