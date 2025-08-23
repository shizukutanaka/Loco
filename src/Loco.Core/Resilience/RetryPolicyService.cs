using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Loco.Core.Resilience
{
    public interface IRetryPolicyService
    {
        Task<T> ExecuteAsync<T>(string policyName, Func<Task<T>> operation, CancellationToken cancellationToken = default);
        Task ExecuteAsync(string policyName, Func<Task> operation, CancellationToken cancellationToken = default);
        Task<T> ExecuteWithCustomPolicyAsync<T>(Func<Task<T>> operation, RetryPolicyOptions options, CancellationToken cancellationToken = default);
        IAsyncPolicy<T> GetPolicy<T>(string policyName);
        void RegisterPolicy(string name, IAsyncPolicy policy);
        RetryMetrics GetMetrics(string policyName);
        Dictionary<string, RetryMetrics> GetAllMetrics();
    }

    public class RetryPolicyService : IRetryPolicyService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RetryPolicyService> _logger;
        private readonly Dictionary<string, IAsyncPolicy> _policies;
        private readonly Dictionary<string, RetryMetrics> _metrics;
        private readonly RetryPolicyOptions _defaultOptions;

        public RetryPolicyService(
            IConfiguration configuration,
            ILogger<RetryPolicyService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _policies = new Dictionary<string, IAsyncPolicy>();
            _metrics = new Dictionary<string, RetryMetrics>();
            _defaultOptions = LoadDefaultOptions();
            InitializeDefaultPolicies();
        }

        private RetryPolicyOptions LoadDefaultOptions()
        {
            return new RetryPolicyOptions
            {
                MaxRetryAttempts = _configuration.GetValue<int>("RetryPolicy:MaxRetryAttempts", 3),
                InitialDelay = TimeSpan.FromSeconds(_configuration.GetValue<double>("RetryPolicy:InitialDelaySeconds", 1)),
                MaxDelay = TimeSpan.FromSeconds(_configuration.GetValue<double>("RetryPolicy:MaxDelaySeconds", 30)),
                TimeoutPerAttempt = TimeSpan.FromSeconds(_configuration.GetValue<double>("RetryPolicy:TimeoutPerAttemptSeconds", 10)),
                OverallTimeout = TimeSpan.FromSeconds(_configuration.GetValue<double>("RetryPolicy:OverallTimeoutSeconds", 60)),
                BackoffType = Enum.Parse<BackoffType>(_configuration["RetryPolicy:BackoffType"] ?? "Exponential", true),
                JitterStrategy = Enum.Parse<JitterStrategy>(_configuration["RetryPolicy:JitterStrategy"] ?? "Decorrelated", true)
            };
        }

        private void InitializeDefaultPolicies()
        {
            RegisterPolicy("Default", CreateRetryPolicy(_defaultOptions));
            
            RegisterPolicy("Http", CreateHttpRetryPolicy());
            
            RegisterPolicy("Database", CreateDatabaseRetryPolicy());
            
            RegisterPolicy("MessageQueue", CreateMessageQueueRetryPolicy());
            
            RegisterPolicy("Critical", CreateCriticalOperationPolicy());
            
            RegisterPolicy("FastFail", CreateFastFailPolicy());
            
            RegisterPolicy("LongRunning", CreateLongRunningPolicy());
        }

        private IAsyncPolicy CreateRetryPolicy(RetryPolicyOptions options)
        {
            var retryPolicy = Policy
                .Handle<Exception>(ex => IsRetriableException(ex))
                .WaitAndRetryAsync(
                    retryCount: options.MaxRetryAttempts,
                    sleepDurationProvider: attempt => CalculateDelay(attempt, options),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var policyName = context.Values.ContainsKey("PolicyName") ? context["PolicyName"].ToString() : "Unknown";
                        LogRetryAttempt(policyName, retryCount, timespan, outcome.Exception);
                        UpdateMetrics(policyName, retryCount, timespan);
                    });

            var timeoutPolicy = Policy
                .TimeoutAsync(options.TimeoutPerAttempt, TimeoutStrategy.Pessimistic);

            var overallTimeoutPolicy = Policy
                .TimeoutAsync(options.OverallTimeout, TimeoutStrategy.Pessimistic);

            return Policy.WrapAsync(overallTimeoutPolicy, retryPolicy, timeoutPolicy);
        }

        private IAsyncPolicy CreateHttpRetryPolicy()
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(0.5),
                MaxDelay = TimeSpan.FromSeconds(10),
                BackoffType = BackoffType.Exponential,
                JitterStrategy = JitterStrategy.Full
            };

            return Policy
                .HandleResult<HttpResponseMessage>(r => IsRetriableHttpStatusCode(r.StatusCode))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    options.MaxRetryAttempts,
                    attempt => CalculateDelay(attempt, options),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var statusCode = outcome.Result?.StatusCode;
                        _logger.LogWarning("HTTP retry attempt {RetryCount} after {Delay}ms. Status: {StatusCode}",
                            retryCount, timespan.TotalMilliseconds, statusCode);
                        UpdateMetrics("Http", retryCount, timespan);
                    });
        }

        private IAsyncPolicy CreateDatabaseRetryPolicy()
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryAttempts = 5,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                MaxDelay = TimeSpan.FromSeconds(5),
                BackoffType = BackoffType.Linear,
                JitterStrategy = JitterStrategy.Decorrelated
            };

            return Policy
                .Handle<Exception>(ex => IsDatabaseTransientException(ex))
                .WaitAndRetryAsync(
                    options.MaxRetryAttempts,
                    attempt => CalculateDelay(attempt, options),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Database retry attempt {RetryCount} after {Delay}ms. Error: {Error}",
                            retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message);
                        UpdateMetrics("Database", retryCount, timespan);
                    });
        }

        private IAsyncPolicy CreateMessageQueueRetryPolicy()
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryAttempts = 10,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromMinutes(1),
                BackoffType = BackoffType.Exponential,
                JitterStrategy = JitterStrategy.Decorrelated
            };

            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    options.MaxRetryAttempts,
                    attempt => CalculateDelay(attempt, options),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Message queue retry attempt {RetryCount} after {Delay}ms",
                            retryCount, timespan.TotalMilliseconds);
                        UpdateMetrics("MessageQueue", retryCount, timespan);
                    });
        }

        private IAsyncPolicy CreateCriticalOperationPolicy()
        {
            var retryPolicy = Policy
                .Handle<Exception>(ex => IsRetriableException(ex))
                .WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 300)),
                    onRetry: (outcome, attempt, timespan) =>
                    {
                        _logger.LogError(outcome.Exception,
                            "Critical operation retry attempt {Attempt} after {Delay}s",
                            attempt, timespan.TotalSeconds);
                        UpdateMetrics("Critical", (int)attempt, timespan);
                    });

            var circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromMinutes(1));

            return Policy.WrapAsync(retryPolicy, circuitBreaker);
        }

        private IAsyncPolicy CreateFastFailPolicy()
        {
            return Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    1,
                    _ => TimeSpan.FromMilliseconds(100),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogDebug("Fast fail retry attempt {RetryCount}", retryCount);
                        UpdateMetrics("FastFail", retryCount, timespan);
                    });
        }

        private IAsyncPolicy CreateLongRunningPolicy()
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryAttempts = 3,
                InitialDelay = TimeSpan.FromMinutes(1),
                MaxDelay = TimeSpan.FromMinutes(10),
                TimeoutPerAttempt = TimeSpan.FromMinutes(30),
                OverallTimeout = TimeSpan.FromHours(2),
                BackoffType = BackoffType.Exponential
            };

            return CreateRetryPolicy(options);
        }

        public async Task<T> ExecuteAsync<T>(string policyName, Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (!_policies.TryGetValue(policyName, out var policy))
            {
                _logger.LogWarning("Policy {PolicyName} not found, using default policy", policyName);
                policy = _policies["Default"];
                policyName = "Default";
            }

            var context = new Context { ["PolicyName"] = policyName };
            
            if (!_metrics.ContainsKey(policyName))
            {
                _metrics[policyName] = new RetryMetrics { PolicyName = policyName };
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _metrics[policyName].IncrementAttempts();
                
                var result = await policy.ExecuteAsync(
                    async (ctx, ct) => await operation(),
                    context,
                    cancellationToken);
                
                stopwatch.Stop();
                _metrics[policyName].RecordSuccess(stopwatch.ElapsedMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _metrics[policyName].RecordFailure(stopwatch.ElapsedMilliseconds, ex.GetType().Name);
                _logger.LogError(ex, "Operation failed after all retry attempts with policy {PolicyName}", policyName);
                throw;
            }
        }

        public async Task ExecuteAsync(string policyName, Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync<object>(policyName, async () =>
            {
                await operation();
                return null;
            }, cancellationToken);
        }

        public async Task<T> ExecuteWithCustomPolicyAsync<T>(
            Func<Task<T>> operation,
            RetryPolicyOptions options,
            CancellationToken cancellationToken = default)
        {
            var policy = CreateRetryPolicy(options);
            var context = new Context { ["PolicyName"] = "Custom" };
            
            return await policy.ExecuteAsync(
                async (ctx, ct) => await operation(),
                context,
                cancellationToken);
        }

        public IAsyncPolicy<T> GetPolicy<T>(string policyName)
        {
            if (_policies.TryGetValue(policyName, out var policy))
            {
                return policy as IAsyncPolicy<T>;
            }
            
            return null;
        }

        public void RegisterPolicy(string name, IAsyncPolicy policy)
        {
            _policies[name] = policy;
            _metrics[name] = new RetryMetrics { PolicyName = name };
            _logger.LogInformation("Registered retry policy: {PolicyName}", name);
        }

        public RetryMetrics GetMetrics(string policyName)
        {
            return _metrics.TryGetValue(policyName, out var metrics) 
                ? metrics.Clone() 
                : new RetryMetrics { PolicyName = policyName };
        }

        public Dictionary<string, RetryMetrics> GetAllMetrics()
        {
            return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
        }

        private TimeSpan CalculateDelay(int attempt, RetryPolicyOptions options)
        {
            double baseDelay = options.InitialDelay.TotalMilliseconds;
            double calculatedDelay;

            switch (options.BackoffType)
            {
                case BackoffType.Linear:
                    calculatedDelay = baseDelay * attempt;
                    break;
                    
                case BackoffType.Exponential:
                    calculatedDelay = baseDelay * Math.Pow(2, attempt - 1);
                    break;
                    
                case BackoffType.Constant:
                    calculatedDelay = baseDelay;
                    break;
                    
                default:
                    calculatedDelay = baseDelay;
                    break;
            }

            calculatedDelay = Math.Min(calculatedDelay, options.MaxDelay.TotalMilliseconds);

            if (options.JitterStrategy != JitterStrategy.None)
            {
                calculatedDelay = ApplyJitter(calculatedDelay, options.JitterStrategy);
            }

            return TimeSpan.FromMilliseconds(calculatedDelay);
        }

        private double ApplyJitter(double delay, JitterStrategy strategy)
        {
            var random = new Random();
            
            switch (strategy)
            {
                case JitterStrategy.Full:
                    return delay * random.NextDouble();
                    
                case JitterStrategy.Decorrelated:
                    return delay * (0.5 + random.NextDouble());
                    
                case JitterStrategy.Equal:
                    return delay * 0.5 + delay * 0.5 * random.NextDouble();
                    
                default:
                    return delay;
            }
        }

        private bool IsRetriableException(Exception ex)
        {
            return ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   ex is HttpRequestException ||
                   ex is System.IO.IOException ||
                   (ex.InnerException != null && IsRetriableException(ex.InnerException));
        }

        private bool IsRetriableHttpStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.RequestTimeout ||
                   statusCode == HttpStatusCode.TooManyRequests ||
                   statusCode == HttpStatusCode.InternalServerError ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        private bool IsDatabaseTransientException(Exception ex)
        {
            var message = ex.Message?.ToLower() ?? "";
            return message.Contains("timeout") ||
                   message.Contains("deadlock") ||
                   message.Contains("connection") ||
                   message.Contains("network") ||
                   message.Contains("transport");
        }

        private void LogRetryAttempt(string policyName, int retryCount, TimeSpan delay, Exception exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception,
                    "Retry attempt {RetryCount} for policy {PolicyName} after {Delay}ms",
                    retryCount, policyName, delay.TotalMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Retry attempt {RetryCount} for policy {PolicyName} after {Delay}ms",
                    retryCount, policyName, delay.TotalMilliseconds);
            }
        }

        private void UpdateMetrics(string policyName, int retryCount, TimeSpan delay)
        {
            if (_metrics.TryGetValue(policyName, out var metrics))
            {
                metrics.IncrementRetries(retryCount);
                metrics.RecordDelay(delay.TotalMilliseconds);
            }
        }
    }

    public class RetryPolicyOptions
    {
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan TimeoutPerAttempt { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public BackoffType BackoffType { get; set; } = BackoffType.Exponential;
        public JitterStrategy JitterStrategy { get; set; } = JitterStrategy.Decorrelated;
    }

    public enum BackoffType
    {
        Constant,
        Linear,
        Exponential
    }

    public enum JitterStrategy
    {
        None,
        Full,
        Equal,
        Decorrelated
    }

    public class RetryMetrics
    {
        private long _totalAttempts;
        private long _successfulAttempts;
        private long _failedAttempts;
        private long _totalRetries;
        private double _totalDelayMs;
        private int _maxRetriesUsed;
        private readonly object _lock = new object();

        public string PolicyName { get; set; }
        public long TotalAttempts => _totalAttempts;
        public long SuccessfulAttempts => _successfulAttempts;
        public long FailedAttempts => _failedAttempts;
        public long TotalRetries => _totalRetries;
        public double AverageDelayMs => _totalRetries > 0 ? _totalDelayMs / _totalRetries : 0;
        public int MaxRetriesUsed => _maxRetriesUsed;
        public double SuccessRate => _totalAttempts > 0 ? (double)_successfulAttempts / _totalAttempts * 100 : 0;
        public Dictionary<string, long> FailureReasons { get; } = new Dictionary<string, long>();

        public void IncrementAttempts()
        {
            Interlocked.Increment(ref _totalAttempts);
        }

        public void RecordSuccess(double totalTimeMs)
        {
            Interlocked.Increment(ref _successfulAttempts);
        }

        public void RecordFailure(double totalTimeMs, string reason)
        {
            lock (_lock)
            {
                _failedAttempts++;
                
                if (FailureReasons.ContainsKey(reason))
                    FailureReasons[reason]++;
                else
                    FailureReasons[reason] = 1;
            }
        }

        public void IncrementRetries(int retryCount)
        {
            lock (_lock)
            {
                _totalRetries++;
                _maxRetriesUsed = Math.Max(_maxRetriesUsed, retryCount);
            }
        }

        public void RecordDelay(double delayMs)
        {
            lock (_lock)
            {
                _totalDelayMs += delayMs;
            }
        }

        public RetryMetrics Clone()
        {
            lock (_lock)
            {
                return new RetryMetrics
                {
                    PolicyName = PolicyName,
                    _totalAttempts = _totalAttempts,
                    _successfulAttempts = _successfulAttempts,
                    _failedAttempts = _failedAttempts,
                    _totalRetries = _totalRetries,
                    _totalDelayMs = _totalDelayMs,
                    _maxRetriesUsed = _maxRetriesUsed
                };
            }
        }
    }
}