using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Scaling
{
    /// <summary>
    /// Intelligent auto-scaling service with predictive capabilities
    /// </summary>
    public class AutoScalingService : IDisposable
    {
        private readonly ILogger<AutoScalingService> _logger;
        private readonly ConcurrentDictionary<string, WorkerPool> _workerPools;
        private readonly ConcurrentDictionary<string, ScalingMetrics> _metrics;
        private readonly Timer _scalingTimer;
        private readonly Timer _metricsTimer;
        
        // Scaling configuration
        private readonly ScalingConfiguration _configuration;
        private readonly PredictiveScaler _predictiveScaler;
        private readonly LoadBalancer _loadBalancer;
        
        // Performance counters
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        
        public AutoScalingService(
            ILogger<AutoScalingService> logger,
            ScalingConfiguration configuration = null)
        {
            _logger = logger;
            _configuration = configuration ?? new ScalingConfiguration();
            _workerPools = new ConcurrentDictionary<string, WorkerPool>();
            _metrics = new ConcurrentDictionary<string, ScalingMetrics>();
            
            _predictiveScaler = new PredictiveScaler();
            _loadBalancer = new LoadBalancer();
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters");
            }
            
            // Start monitoring
            _scalingTimer = new Timer(
                EvaluateScaling, 
                null, 
                TimeSpan.FromSeconds(30), 
                TimeSpan.FromSeconds(30));
                
            _metricsTimer = new Timer(
                CollectMetrics,
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Creates a new auto-scaled worker pool
        /// </summary>
        public WorkerPool CreateWorkerPool(string name, WorkerPoolOptions options)
        {
            var pool = new WorkerPool
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Options = options,
                Workers = new ConcurrentBag<Worker>(),
                CreatedAt = DateTime.UtcNow,
                Status = PoolStatus.Active
            };

            // Initialize with minimum workers
            for (int i = 0; i < options.MinWorkers; i++)
            {
                pool.Workers.Add(CreateWorker(pool));
            }

            _workerPools.TryAdd(pool.Id, pool);
            _metrics.TryAdd(pool.Id, new ScalingMetrics { PoolId = pool.Id });

            _logger.LogInformation($"Created worker pool '{name}' with {options.MinWorkers} initial workers");
            
            return pool;
        }

        /// <summary>
        /// Scales a worker pool based on current metrics
        /// </summary>
        public async Task<ScalingResult> ScalePool(string poolId, ScalingDirection direction, int count = 1)
        {
            if (!_workerPools.TryGetValue(poolId, out var pool))
            {
                return new ScalingResult 
                { 
                    Success = false, 
                    Message = "Pool not found" 
                };
            }

            var result = new ScalingResult
            {
                PoolId = poolId,
                PreviousCount = pool.Workers.Count,
                Direction = direction
            };

            try
            {
                switch (direction)
                {
                    case ScalingDirection.Up:
                        result = await ScaleUp(pool, count);
                        break;
                    case ScalingDirection.Down:
                        result = await ScaleDown(pool, count);
                        break;
                    case ScalingDirection.Auto:
                        result = await AutoScale(pool);
                        break;
                }

                result.Success = true;
                result.NewCount = pool.Workers.Count;
                
                // Update metrics
                if (_metrics.TryGetValue(poolId, out var metrics))
                {
                    metrics.LastScalingAction = DateTime.UtcNow;
                    metrics.ScalingEvents++;
                }

                _logger.LogInformation(
                    $"Scaled pool '{pool.Name}' {direction} from {result.PreviousCount} to {result.NewCount} workers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scaling pool {poolId}");
                result.Success = false;
                result.Message = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Gets current scaling metrics for all pools
        /// </summary>
        public Dictionary<string, ScalingMetrics> GetMetrics()
        {
            return new Dictionary<string, ScalingMetrics>(_metrics);
        }

        /// <summary>
        /// Predicts future scaling needs
        /// </summary>
        public async Task<ScalingPrediction> PredictScalingNeeds(string poolId, TimeSpan horizon)
        {
            if (!_metrics.TryGetValue(poolId, out var metrics))
            {
                return new ScalingPrediction { Success = false };
            }

            var prediction = await _predictiveScaler.Predict(metrics, horizon);
            
            // Add recommendations
            prediction.Recommendations = GenerateRecommendations(prediction, metrics);
            
            return prediction;
        }

        /// <summary>
        /// Optimizes resource allocation across pools
        /// </summary>
        public async Task<OptimizationResult> OptimizeResourceAllocation()
        {
            var result = new OptimizationResult
            {
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Collect current state
                var poolStates = _workerPools.Values
                    .Select(p => new PoolState
                    {
                        Pool = p,
                        Metrics = _metrics.GetValueOrDefault(p.Id) ?? new ScalingMetrics(),
                        Load = CalculatePoolLoad(p)
                    })
                    .ToList();

                // Calculate optimal distribution
                var distribution = CalculateOptimalDistribution(poolStates);

                // Apply changes
                foreach (var action in distribution.Actions)
                {
                    await ApplyOptimizationAction(action);
                }

                result.Success = true;
                result.OptimizationActions = distribution.Actions;
                result.EstimatedSavings = distribution.EstimatedSavings;
                result.EndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    $"Resource optimization completed with {distribution.Actions.Count} actions, " +
                    $"estimated savings: ${distribution.EstimatedSavings:F2}/hour");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing resource allocation");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Configures auto-scaling policies
        /// </summary>
        public void ConfigurePolicy(string poolId, ScalingPolicy policy)
        {
            if (_workerPools.TryGetValue(poolId, out var pool))
            {
                pool.ScalingPolicy = policy;
                _logger.LogInformation($"Updated scaling policy for pool '{pool.Name}'");
            }
        }

        /// <summary>
        /// Gets health status of all worker pools
        /// </summary>
        public HealthReport GetHealthReport()
        {
            var report = new HealthReport
            {
                Timestamp = DateTime.UtcNow,
                Pools = new List<PoolHealth>()
            };

            foreach (var pool in _workerPools.Values)
            {
                var health = new PoolHealth
                {
                    PoolId = pool.Id,
                    PoolName = pool.Name,
                    TotalWorkers = pool.Workers.Count,
                    HealthyWorkers = pool.Workers.Count(w => w.IsHealthy),
                    UnhealthyWorkers = pool.Workers.Count(w => !w.IsHealthy),
                    AverageLoad = CalculatePoolLoad(pool),
                    Status = pool.Status
                };

                if (_metrics.TryGetValue(pool.Id, out var metrics))
                {
                    health.CpuUsage = metrics.AverageCpuUsage;
                    health.MemoryUsage = metrics.AverageMemoryUsage;
                    health.RequestsPerSecond = metrics.RequestsPerSecond;
                    health.ErrorRate = metrics.ErrorRate;
                }

                report.Pools.Add(health);
            }

            report.OverallHealth = CalculateOverallHealth(report.Pools);
            
            return report;
        }

        private async Task<ScalingResult> ScaleUp(WorkerPool pool, int count)
        {
            var result = new ScalingResult
            {
                Direction = ScalingDirection.Up,
                PreviousCount = pool.Workers.Count
            };

            // Check maximum limit
            var currentCount = pool.Workers.Count;
            var targetCount = Math.Min(currentCount + count, pool.Options.MaxWorkers);
            var toAdd = targetCount - currentCount;

            if (toAdd <= 0)
            {
                result.Message = "Already at maximum capacity";
                return result;
            }

            // Add new workers
            var tasks = new List<Task<Worker>>();
            for (int i = 0; i < toAdd; i++)
            {
                tasks.Add(Task.Run(() => CreateWorker(pool)));
            }

            var newWorkers = await Task.WhenAll(tasks);
            foreach (var worker in newWorkers)
            {
                pool.Workers.Add(worker);
            }

            result.NewCount = pool.Workers.Count;
            result.ScaledCount = toAdd;
            
            return result;
        }

        private async Task<ScalingResult> ScaleDown(WorkerPool pool, int count)
        {
            var result = new ScalingResult
            {
                Direction = ScalingDirection.Down,
                PreviousCount = pool.Workers.Count
            };

            // Check minimum limit
            var currentCount = pool.Workers.Count;
            var targetCount = Math.Max(currentCount - count, pool.Options.MinWorkers);
            var toRemove = currentCount - targetCount;

            if (toRemove <= 0)
            {
                result.Message = "Already at minimum capacity";
                return result;
            }

            // Remove workers (prefer idle ones)
            var workersToRemove = pool.Workers
                .OrderBy(w => w.CurrentLoad)
                .Take(toRemove)
                .ToList();

            foreach (var worker in workersToRemove)
            {
                await ShutdownWorker(worker);
                pool.Workers.TryTake(out _);
            }

            result.NewCount = pool.Workers.Count;
            result.ScaledCount = toRemove;
            
            return result;
        }

        private async Task<ScalingResult> AutoScale(WorkerPool pool)
        {
            if (pool.ScalingPolicy == null)
            {
                return new ScalingResult 
                { 
                    Success = false, 
                    Message = "No scaling policy configured" 
                };
            }

            var metrics = _metrics.GetValueOrDefault(pool.Id);
            if (metrics == null)
            {
                return new ScalingResult 
                { 
                    Success = false, 
                    Message = "No metrics available" 
                };
            }

            // Evaluate scaling rules
            var decision = EvaluateScalingRules(pool, metrics);
            
            if (decision.ShouldScale)
            {
                return await ScalePool(pool.Id, decision.Direction, decision.Count);
            }

            return new ScalingResult 
            { 
                Success = true, 
                Message = "No scaling needed" 
            };
        }

        private Worker CreateWorker(WorkerPool pool)
        {
            return new Worker
            {
                Id = Guid.NewGuid().ToString(),
                PoolId = pool.Id,
                CreatedAt = DateTime.UtcNow,
                IsHealthy = true,
                CurrentLoad = 0,
                ProcessedRequests = 0
            };
        }

        private async Task ShutdownWorker(Worker worker)
        {
            worker.IsHealthy = false;
            worker.ShutdownAt = DateTime.UtcNow;
            
            // Graceful shutdown logic
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        private void EvaluateScaling(object state)
        {
            try
            {
                foreach (var pool in _workerPools.Values)
                {
                    if (pool.Options.AutoScaleEnabled)
                    {
                        var _ = AutoScale(pool);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scaling evaluation");
            }
        }

        private void CollectMetrics(object state)
        {
            try
            {
                foreach (var pool in _workerPools.Values)
                {
                    if (_metrics.TryGetValue(pool.Id, out var metrics))
                    {
                        // Update metrics
                        metrics.AverageCpuUsage = _cpuCounter?.NextValue() ?? 0;
                        metrics.AverageMemoryUsage = GetMemoryUsage();
                        metrics.ActiveWorkers = pool.Workers.Count(w => w.IsHealthy);
                        metrics.TotalWorkers = pool.Workers.Count;
                        metrics.RequestsPerSecond = CalculateRequestRate(pool);
                        metrics.AverageResponseTime = CalculateAverageResponseTime(pool);
                        metrics.QueueLength = GetQueueLength(pool);
                        metrics.LastUpdated = DateTime.UtcNow;
                        
                        // Store historical data
                        metrics.History.Add(new MetricSnapshot
                        {
                            Timestamp = DateTime.UtcNow,
                            CpuUsage = metrics.AverageCpuUsage,
                            MemoryUsage = metrics.AverageMemoryUsage,
                            RequestRate = metrics.RequestsPerSecond,
                            WorkerCount = metrics.TotalWorkers
                        });
                        
                        // Trim old history
                        var cutoff = DateTime.UtcNow.AddHours(-1);
                        metrics.History.RemoveAll(h => h.Timestamp < cutoff);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting metrics");
            }
        }

        private double CalculatePoolLoad(WorkerPool pool)
        {
            if (!pool.Workers.Any()) return 0;
            return pool.Workers.Average(w => w.CurrentLoad);
        }

        private ScalingDecision EvaluateScalingRules(WorkerPool pool, ScalingMetrics metrics)
        {
            var decision = new ScalingDecision();
            var policy = pool.ScalingPolicy ?? _configuration.DefaultPolicy;

            // CPU-based scaling
            if (metrics.AverageCpuUsage > policy.ScaleUpCpuThreshold)
            {
                decision.ShouldScale = true;
                decision.Direction = ScalingDirection.Up;
                decision.Count = CalculateScaleUpCount(pool, metrics);
                decision.Reason = $"CPU usage {metrics.AverageCpuUsage:F1}% exceeds threshold";
            }
            else if (metrics.AverageCpuUsage < policy.ScaleDownCpuThreshold)
            {
                decision.ShouldScale = true;
                decision.Direction = ScalingDirection.Down;
                decision.Count = CalculateScaleDownCount(pool, metrics);
                decision.Reason = $"CPU usage {metrics.AverageCpuUsage:F1}% below threshold";
            }

            // Queue-based scaling
            if (metrics.QueueLength > policy.ScaleUpQueueThreshold)
            {
                decision.ShouldScale = true;
                decision.Direction = ScalingDirection.Up;
                decision.Count = Math.Max(decision.Count, CalculateQueueBasedScaling(metrics));
                decision.Reason = $"Queue length {metrics.QueueLength} exceeds threshold";
            }

            // Response time-based scaling
            if (metrics.AverageResponseTime > policy.ScaleUpResponseTimeThreshold)
            {
                decision.ShouldScale = true;
                decision.Direction = ScalingDirection.Up;
                decision.Count = Math.Max(decision.Count, 2);
                decision.Reason = $"Response time {metrics.AverageResponseTime}ms exceeds threshold";
            }

            // Cooldown period check
            if (decision.ShouldScale && metrics.LastScalingAction != null)
            {
                var timeSinceLastScale = DateTime.UtcNow - metrics.LastScalingAction.Value;
                if (timeSinceLastScale < policy.CooldownPeriod)
                {
                    decision.ShouldScale = false;
                    decision.Reason = "In cooldown period";
                }
            }

            return decision;
        }

        private int CalculateScaleUpCount(WorkerPool pool, ScalingMetrics metrics)
        {
            var currentCount = pool.Workers.Count;
            var targetCount = (int)(currentCount * 1.5); // Scale up by 50%
            return Math.Min(targetCount - currentCount, pool.Options.MaxWorkers - currentCount);
        }

        private int CalculateScaleDownCount(WorkerPool pool, ScalingMetrics metrics)
        {
            var currentCount = pool.Workers.Count;
            var targetCount = (int)(currentCount * 0.75); // Scale down by 25%
            return Math.Max(currentCount - targetCount, 1);
        }

        private int CalculateQueueBasedScaling(ScalingMetrics metrics)
        {
            // Scale based on queue length
            return (int)Math.Ceiling(metrics.QueueLength / 100.0);
        }

        private OptimalDistribution CalculateOptimalDistribution(List<PoolState> poolStates)
        {
            var distribution = new OptimalDistribution
            {
                Actions = new List<OptimizationAction>()
            };

            // Rebalance workers based on load
            var totalWorkers = poolStates.Sum(p => p.Pool.Workers.Count);
            var totalLoad = poolStates.Sum(p => p.Load * p.Pool.Workers.Count);
            var averageLoad = totalLoad / totalWorkers;

            foreach (var state in poolStates)
            {
                var targetWorkers = (int)Math.Ceiling(
                    (state.Load / averageLoad) * state.Pool.Workers.Count);
                    
                var currentWorkers = state.Pool.Workers.Count;
                
                if (targetWorkers > currentWorkers)
                {
                    distribution.Actions.Add(new OptimizationAction
                    {
                        PoolId = state.Pool.Id,
                        Action = "ScaleUp",
                        Count = targetWorkers - currentWorkers
                    });
                }
                else if (targetWorkers < currentWorkers)
                {
                    distribution.Actions.Add(new OptimizationAction
                    {
                        PoolId = state.Pool.Id,
                        Action = "ScaleDown",
                        Count = currentWorkers - targetWorkers
                    });
                }
            }

            // Calculate estimated savings
            distribution.EstimatedSavings = CalculateEstimatedSavings(distribution.Actions);
            
            return distribution;
        }

        private async Task ApplyOptimizationAction(OptimizationAction action)
        {
            var direction = action.Action == "ScaleUp" 
                ? ScalingDirection.Up 
                : ScalingDirection.Down;
                
            await ScalePool(action.PoolId, direction, action.Count);
        }

        private List<string> GenerateRecommendations(ScalingPrediction prediction, ScalingMetrics metrics)
        {
            var recommendations = new List<string>();

            if (prediction.PredictedLoad > 0.8)
            {
                recommendations.Add("Consider pre-scaling before predicted peak load");
            }

            if (metrics.ErrorRate > 0.05)
            {
                recommendations.Add("High error rate detected - investigate before scaling");
            }

            if (metrics.History.Any())
            {
                var pattern = AnalyzeUsagePattern(metrics.History);
                if (pattern == UsagePattern.Periodic)
                {
                    recommendations.Add("Consider scheduled scaling based on periodic usage pattern");
                }
            }

            return recommendations;
        }

        private UsagePattern AnalyzeUsagePattern(List<MetricSnapshot> history)
        {
            // Simple pattern detection
            if (history.Count < 100) return UsagePattern.Unknown;
            
            // Check for periodicity
            // Implementation would include FFT or autocorrelation analysis
            return UsagePattern.Steady;
        }

        private double CalculateOverallHealth(List<PoolHealth> pools)
        {
            if (!pools.Any()) return 100;
            
            var healthScore = 100.0;
            
            foreach (var pool in pools)
            {
                if (pool.UnhealthyWorkers > 0)
                {
                    healthScore -= (pool.UnhealthyWorkers / (double)pool.TotalWorkers) * 20;
                }
                
                if (pool.ErrorRate > 0.05)
                {
                    healthScore -= 10;
                }
                
                if (pool.AverageLoad > 0.9)
                {
                    healthScore -= 15;
                }
            }
            
            return Math.Max(0, healthScore);
        }

        private double GetMemoryUsage()
        {
            try
            {
                var totalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                return totalMemory;
            }
            catch
            {
                return 0;
            }
        }

        private double CalculateRequestRate(WorkerPool pool)
        {
            var totalRequests = pool.Workers.Sum(w => w.ProcessedRequests);
            return totalRequests / Math.Max(1, (DateTime.UtcNow - pool.CreatedAt).TotalSeconds);
        }

        private double CalculateAverageResponseTime(WorkerPool pool)
        {
            // Implementation would track actual response times
            return 50 + pool.Workers.Count * 5; // Mock calculation
        }

        private int GetQueueLength(WorkerPool pool)
        {
            // Implementation would check actual queue
            return (int)(pool.Workers.Count * 10 * Math.Max(0, pool.Workers.Average(w => w.CurrentLoad)));
        }

        private double CalculateEstimatedSavings(List<OptimizationAction> actions)
        {
            // Calculate based on worker reduction
            var reducedWorkers = actions
                .Where(a => a.Action == "ScaleDown")
                .Sum(a => a.Count);
                
            return reducedWorkers * 0.10; // $0.10 per worker per hour
        }

        public void Dispose()
        {
            _scalingTimer?.Dispose();
            _metricsTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            
            // Shutdown all workers
            foreach (var pool in _workerPools.Values)
            {
                foreach (var worker in pool.Workers)
                {
                    var _ = ShutdownWorker(worker);
                }
            }
        }
    }

    // Supporting classes
    public class WorkerPool
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public WorkerPoolOptions Options { get; set; }
        public ConcurrentBag<Worker> Workers { get; set; }
        public DateTime CreatedAt { get; set; }
        public PoolStatus Status { get; set; }
        public ScalingPolicy ScalingPolicy { get; set; }
    }

    public class Worker
    {
        public string Id { get; set; }
        public string PoolId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ShutdownAt { get; set; }
        public bool IsHealthy { get; set; }
        public double CurrentLoad { get; set; }
        public long ProcessedRequests { get; set; }
    }

    public class WorkerPoolOptions
    {
        public int MinWorkers { get; set; } = 1;
        public int MaxWorkers { get; set; } = 10;
        public bool AutoScaleEnabled { get; set; } = true;
        public TimeSpan WorkerTimeout { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class ScalingPolicy
    {
        public double ScaleUpCpuThreshold { get; set; } = 70;
        public double ScaleDownCpuThreshold { get; set; } = 30;
        public int ScaleUpQueueThreshold { get; set; } = 100;
        public double ScaleUpResponseTimeThreshold { get; set; } = 1000;
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class ScalingConfiguration
    {
        public ScalingPolicy DefaultPolicy { get; set; } = new ScalingPolicy();
        public bool EnablePredictiveScaling { get; set; } = true;
        public bool EnableCostOptimization { get; set; } = true;
    }

    public class ScalingMetrics
    {
        public string PoolId { get; set; }
        public double AverageCpuUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public int TotalWorkers { get; set; }
        public int ActiveWorkers { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageResponseTime { get; set; }
        public int QueueLength { get; set; }
        public double ErrorRate { get; set; }
        public DateTime? LastScalingAction { get; set; }
        public int ScalingEvents { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<MetricSnapshot> History { get; set; } = new List<MetricSnapshot>();
    }

    public class MetricSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double RequestRate { get; set; }
        public int WorkerCount { get; set; }
    }

    public class ScalingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string PoolId { get; set; }
        public ScalingDirection Direction { get; set; }
        public int PreviousCount { get; set; }
        public int NewCount { get; set; }
        public int ScaledCount { get; set; }
    }

    public class ScalingPrediction
    {
        public bool Success { get; set; }
        public DateTime PredictionTime { get; set; }
        public TimeSpan Horizon { get; set; }
        public double PredictedLoad { get; set; }
        public int RecommendedWorkers { get; set; }
        public double Confidence { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class OptimizationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<OptimizationAction> OptimizationActions { get; set; }
        public double EstimatedSavings { get; set; }
    }

    public class OptimizationAction
    {
        public string PoolId { get; set; }
        public string Action { get; set; }
        public int Count { get; set; }
    }

    public class HealthReport
    {
        public DateTime Timestamp { get; set; }
        public List<PoolHealth> Pools { get; set; }
        public double OverallHealth { get; set; }
    }

    public class PoolHealth
    {
        public string PoolId { get; set; }
        public string PoolName { get; set; }
        public int TotalWorkers { get; set; }
        public int HealthyWorkers { get; set; }
        public int UnhealthyWorkers { get; set; }
        public double AverageLoad { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double RequestsPerSecond { get; set; }
        public double ErrorRate { get; set; }
        public PoolStatus Status { get; set; }
    }

    public enum ScalingDirection
    {
        Up,
        Down,
        Auto
    }

    public enum PoolStatus
    {
        Active,
        Scaling,
        Paused,
        Stopped
    }

    public enum UsagePattern
    {
        Unknown,
        Steady,
        Periodic,
        Trending,
        Bursty
    }

    // Helper classes
    internal class PredictiveScaler
    {
        public async Task<ScalingPrediction> Predict(ScalingMetrics metrics, TimeSpan horizon)
        {
            // Simple prediction based on historical data
            var prediction = new ScalingPrediction
            {
                Success = true,
                PredictionTime = DateTime.UtcNow,
                Horizon = horizon,
                Confidence = 0.75
            };

            if (metrics.History.Any())
            {
                var trend = CalculateTrend(metrics.History);
                prediction.PredictedLoad = Math.Min(1.0, metrics.AverageCpuUsage / 100 + trend);
                prediction.RecommendedWorkers = (int)Math.Ceiling(prediction.PredictedLoad * 10);
            }

            return prediction;
        }

        private double CalculateTrend(List<MetricSnapshot> history)
        {
            if (history.Count < 2) return 0;
            
            var recent = history.TakeLast(10).Average(h => h.CpuUsage);
            var older = history.SkipLast(10).TakeLast(10).Average(h => h.CpuUsage);
            
            return (recent - older) / 100;
        }
    }

    internal class LoadBalancer
    {
        private readonly Random _random = new Random();
        
        public Worker SelectWorker(WorkerPool pool)
        {
            var healthyWorkers = pool.Workers.Where(w => w.IsHealthy).ToList();
            
            if (!healthyWorkers.Any())
                return null;
            
            // Select worker with lowest load
            return healthyWorkers.OrderBy(w => w.CurrentLoad).First();
        }
    }

    internal class ScalingDecision
    {
        public bool ShouldScale { get; set; }
        public ScalingDirection Direction { get; set; }
        public int Count { get; set; }
        public string Reason { get; set; }
    }

    internal class PoolState
    {
        public WorkerPool Pool { get; set; }
        public ScalingMetrics Metrics { get; set; }
        public double Load { get; set; }
    }

    internal class OptimalDistribution
    {
        public List<OptimizationAction> Actions { get; set; }
        public double EstimatedSavings { get; set; }
    }
}
