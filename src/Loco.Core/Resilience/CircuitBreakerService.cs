using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

namespace Loco.Core.Resilience
{
    public interface ICircuitBreakerService
    {
        Task<T> ExecuteAsync<T>(string operationKey, Func<Task<T>> operation, CircuitBreakerOptions options = null);
        Task ExecuteAsync(string operationKey, Func<Task> operation, CircuitBreakerOptions options = null);
        CircuitBreakerState GetState(string operationKey);
        void Reset(string operationKey);
        void ResetAll();
        CircuitBreakerStatistics GetStatistics(string operationKey);
    }

    public class CircuitBreakerOptions
    {
        public int FailureThreshold { get; set; } = 3;
        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
        public double FailureRatio { get; set; } = 0.5;
        public int MinimumThroughput { get; set; } = 10;
        
        // Retry options
        public int RetryCount { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool UseExponentialBackoff { get; set; } = true;
        
        // Timeout options
        public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);
        
        // Bulkhead options
        public int? MaxParallelization { get; set; } = 10;
        public int? MaxQueuingActions { get; set; } = 20;
    }

    public class CircuitBreakerState
    {
        public string OperationKey { get; set; }
        public string State { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? CircuitOpenTime { get; set; }
        public DateTime? ExpectedResetTime { get; set; }
    }

    public class CircuitBreakerStatistics
    {
        public string OperationKey { get; set; }
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long TimeoutRequests { get; set; }
        public long CircuitOpenRejections { get; set; }
        public double SuccessRate { get; set; }
        public double AverageResponseTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
    }

    public class AdvancedCircuitBreakerService : ICircuitBreakerService
    {
        private readonly ILogger<AdvancedCircuitBreakerService> _logger;
        private readonly ConcurrentDictionary<string, IAsyncPolicy> _policies;
        private readonly ConcurrentDictionary<string, CircuitBreakerStatistics> _statistics;
        private readonly ConcurrentDictionary<string, CircuitBreakerState> _states;

        public AdvancedCircuitBreakerService(ILogger<AdvancedCircuitBreakerService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _policies = new ConcurrentDictionary<string, IAsyncPolicy>();
            _statistics = new ConcurrentDictionary<string, CircuitBreakerStatistics>();
            _states = new ConcurrentDictionary<string, CircuitBreakerState>();
        }

        public async Task<T> ExecuteAsync<T>(string operationKey, Func<Task<T>> operation, CircuitBreakerOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
                throw new ArgumentNullException(nameof(operationKey));
            
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            options ??= new CircuitBreakerOptions();
            var policy = GetOrCreatePolicy(operationKey, options);
            var stats = GetOrCreateStatistics(operationKey);
            
            var startTime = DateTime.UtcNow;
            
            try
            {
                stats.TotalRequests++;
                
                var result = await policy.ExecuteAsync(async () =>
                {
                    return await operation();
                });
                
                stats.SuccessfulRequests++;
                stats.LastSuccessTime = DateTime.UtcNow;
                UpdateAverageResponseTime(stats, startTime);
                
                return result;
            }
            catch (BrokenCircuitException ex)
            {
                stats.CircuitOpenRejections++;
                _logger.LogWarning($"Circuit breaker is open for operation: {operationKey}. {ex.Message}");
                throw;
            }
            catch (TimeoutRejectedException ex)
            {
                stats.TimeoutRequests++;
                stats.FailedRequests++;
                stats.LastFailureTime = DateTime.UtcNow;
                _logger.LogWarning($"Operation timed out: {operationKey}. {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                stats.LastFailureTime = DateTime.UtcNow;
                _logger.LogError(ex, $"Operation failed: {operationKey}");
                throw;
            }
            finally
            {
                UpdateStatistics(stats);
            }
        }

        public async Task ExecuteAsync(string operationKey, Func<Task> operation, CircuitBreakerOptions options = null)
        {
            await ExecuteAsync<object>(operationKey, async () =>
            {
                await operation();
                return null;
            }, options);
        }

        public CircuitBreakerState GetState(string operationKey)
        {
            if (_states.TryGetValue(operationKey, out var state))
            {
                return state;
            }

            return new CircuitBreakerState
            {
                OperationKey = operationKey,
                State = "Closed",
                ConsecutiveFailures = 0
            };
        }

        public void Reset(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
                return;

            _policies.TryRemove(operationKey, out _);
            _statistics.TryRemove(operationKey, out _);
            _states.TryRemove(operationKey, out _);
            
            _logger.LogInformation($"Circuit breaker reset for operation: {operationKey}");
        }

        public void ResetAll()
        {
            _policies.Clear();
            _statistics.Clear();
            _states.Clear();
            
            _logger.LogInformation("All circuit breakers reset");
        }

        public CircuitBreakerStatistics GetStatistics(string operationKey)
        {
            if (_statistics.TryGetValue(operationKey, out var stats))
            {
                return stats;
            }

            return new CircuitBreakerStatistics
            {
                OperationKey = operationKey,
                TotalRequests = 0,
                SuccessfulRequests = 0,
                FailedRequests = 0,
                TimeoutRequests = 0,
                CircuitOpenRejections = 0,
                SuccessRate = 0,
                AverageResponseTime = 0
            };
        }

        private IAsyncPolicy GetOrCreatePolicy(string operationKey, CircuitBreakerOptions options)
        {
            return _policies.GetOrAdd(operationKey, key =>
            {
                var policies = new List<IAsyncPolicy>();

                // Add retry policy
                if (options.RetryCount > 0)
                {
                    var retryPolicy = options.UseExponentialBackoff
                        ? Policy.Handle<Exception>(ex => !(ex is BrokenCircuitException))
                            .WaitAndRetryAsync(
                                options.RetryCount,
                                retryAttempt => TimeSpan.FromMilliseconds(
                                    options.RetryDelay.TotalMilliseconds * Math.Pow(2, retryAttempt)),
                                onRetry: (outcome, timespan, retryCount, context) =>
                                {
                                    _logger.LogWarning($"Retry {retryCount} after {timespan}ms for operation: {key}");
                                })
                        : Policy.Handle<Exception>(ex => !(ex is BrokenCircuitException))
                            .WaitAndRetryAsync(
                                options.RetryCount,
                                retryAttempt => options.RetryDelay,
                                onRetry: (outcome, timespan, retryCount, context) =>
                                {
                                    _logger.LogWarning($"Retry {retryCount} after {timespan}ms for operation: {key}");
                                });
                    
                    policies.Add(retryPolicy);
                }

                // Add circuit breaker policy
                var circuitBreakerPolicy = Policy
                    .Handle<Exception>()
                    .AdvancedCircuitBreakerAsync(
                        options.FailureRatio,
                        options.SamplingDuration,
                        options.MinimumThroughput,
                        options.DurationOfBreak,
                        onBreak: (result, breakDuration) =>
                        {
                            var state = GetOrCreateState(key);
                            state.State = "Open";
                            state.CircuitOpenTime = DateTime.UtcNow;
                            state.ExpectedResetTime = DateTime.UtcNow.Add(breakDuration);
                            
                            _logger.LogWarning($"Circuit breaker opened for operation: {key}. Duration: {breakDuration}");
                        },
                        onReset: () =>
                        {
                            var state = GetOrCreateState(key);
                            state.State = "Closed";
                            state.ConsecutiveFailures = 0;
                            state.CircuitOpenTime = null;
                            state.ExpectedResetTime = null;
                            
                            _logger.LogInformation($"Circuit breaker reset for operation: {key}");
                        },
                        onHalfOpen: () =>
                        {
                            var state = GetOrCreateState(key);
                            state.State = "HalfOpen";
                            
                            _logger.LogInformation($"Circuit breaker half-open for operation: {key}");
                        });
                
                policies.Add(circuitBreakerPolicy);

                // Add timeout policy
                if (options.Timeout.HasValue)
                {
                    var timeoutPolicy = Policy.TimeoutAsync(
                        options.Timeout.Value,
                        TimeoutStrategy.Pessimistic,
                        onTimeoutAsync: async (context, timespan, task) =>
                        {
                            _logger.LogWarning($"Operation timed out after {timespan}ms: {key}");
                        });
                    
                    policies.Add(timeoutPolicy);
                }

                // Add bulkhead policy
                if (options.MaxParallelization.HasValue)
                {
                    var bulkheadPolicy = Policy.BulkheadAsync(
                        options.MaxParallelization.Value,
                        options.MaxQueuingActions ?? 0,
                        onBulkheadRejectedAsync: async context =>
                        {
                            _logger.LogWarning($"Bulkhead rejected operation: {key}");
                        });
                    
                    policies.Add(bulkheadPolicy);
                }

                // Combine all policies
                return policies.Count > 1 ? Policy.WrapAsync(policies.ToArray()) : policies.First();
            });
        }

        private CircuitBreakerStatistics GetOrCreateStatistics(string operationKey)
        {
            return _statistics.GetOrAdd(operationKey, key => new CircuitBreakerStatistics
            {
                OperationKey = key
            });
        }

        private CircuitBreakerState GetOrCreateState(string operationKey)
        {
            return _states.GetOrAdd(operationKey, key => new CircuitBreakerState
            {
                OperationKey = key,
                State = "Closed"
            });
        }

        private void UpdateAverageResponseTime(CircuitBreakerStatistics stats, DateTime startTime)
        {
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            if (stats.AverageResponseTime == 0)
            {
                stats.AverageResponseTime = responseTime;
            }
            else
            {
                // Exponential moving average
                const double alpha = 0.2;
                stats.AverageResponseTime = (alpha * responseTime) + ((1 - alpha) * stats.AverageResponseTime);
            }
        }

        private void UpdateStatistics(CircuitBreakerStatistics stats)
        {
            if (stats.TotalRequests > 0)
            {
                stats.SuccessRate = (double)stats.SuccessfulRequests / stats.TotalRequests * 100;
            }
        }
    }
}