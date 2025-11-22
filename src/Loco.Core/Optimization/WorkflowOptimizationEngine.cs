using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Optimization
{
    /// <summary>
    /// Workflow optimization and performance engine
    /// Phase 25: Bottleneck detection, performance profiling, optimization recommendations, resource tuning
    /// </summary>
    public interface IWorkflowOptimizationEngine
    {
        Task<PerformanceProfile> ProfileWorkflowAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<List<BottleneckAnalysis>> AnalyzeBottlenecksAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<OptimizationPlan> GenerateOptimizationPlanAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<bool> ApplyOptimizationAsync(string tenantId, string workflowId, string optimizationId, CancellationToken ct = default);
        Task<ResourceAllocation> OptimizeResourcesAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<ParallelizationAnalysis> AnalyzeParallelizationAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<CacheAnalysis> AnalyzeCacheAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<List<PerformanceRecommendation>> GetRecommendationsAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<OptimizationHistory> GetOptimizationHistoryAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<OptimizationMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class WorkflowOptimizationEngine : IWorkflowOptimizationEngine
    {
        private readonly ILogger<WorkflowOptimizationEngine> _logger;
        private readonly Dictionary<string, PerformanceProfile> _profiles = new();
        private readonly Dictionary<string, List<Optimization>> _optimizations = new();
        private readonly Dictionary<string, List<PerformanceRecommendation>> _recommendations = new();
        private readonly Dictionary<string, OptimizationHistory> _history = new();
        private readonly Random _random = new(42);

        public WorkflowOptimizationEngine(ILogger<WorkflowOptimizationEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PerformanceProfile> ProfileWorkflowAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Profiling workflow {WorkflowId}", workflowId);
            await Task.Delay(50, ct);

            var profile = new PerformanceProfile
            {
                ProfileId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TenantId = tenantId,
                ProfiledAt = DateTimeOffset.UtcNow,
                ExecutionCount = _random.Next(100, 10000),
                TotalExecutionTimeMs = _random.Next(100000, 10000000),
                AverageExecutionTimeMs = _random.Next(100, 10000),
                MinExecutionTimeMs = _random.Next(50, 1000),
                MaxExecutionTimeMs = _random.Next(5000, 50000),
                P50ExecutionTimeMs = _random.Next(100, 5000),
                P95ExecutionTimeMs = _random.Next(500, 20000),
                P99ExecutionTimeMs = _random.Next(1000, 50000),
                CPUUsagePercent = _random.Next(10, 90),
                MemoryUsageMB = _random.Next(50, 2000),
                DiskIOPercent = _random.Next(5, 80),
                NetworkIOPercent = _random.Next(5, 60),
                FailureRate = _random.NextDouble() * 5, // 0-5%
                Steps = GenerateStepProfiles(),
                DataVolume = _random.Next(1, 10000), // MB
                CacheHitRate = _random.Next(40, 95),
                ThrottledCount = _random.Next(0, 100)
            };

            var key = $"{tenantId}:{workflowId}";
            _profiles[key] = profile;

            return profile;
        }

        public async Task<List<BottleneckAnalysis>> AnalyzeBottlenecksAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Analyzing bottlenecks for workflow {WorkflowId}", workflowId);
            await Task.Delay(40, ct);

            var bottlenecks = new List<BottleneckAnalysis>
            {
                new()
                {
                    BottleneckId = "bn-1",
                    WorkflowId = workflowId,
                    Type = "step",
                    Location = "step-3-api-call",
                    ImpactPercent = _random.Next(10, 50),
                    Severity = "high",
                    CurrentDuration = _random.Next(1000, 5000),
                    Cause = "Slow external API response times",
                    Frequency = _random.Next(50, 95),
                    AffectedExecutions = _random.Next(100, 1000),
                    EstimatedImpactMs = _random.Next(500, 3000)
                },
                new()
                {
                    BottleneckId = "bn-2",
                    WorkflowId = workflowId,
                    Type = "resource",
                    Location = "database-queries",
                    ImpactPercent = _random.Next(5, 30),
                    Severity = "medium",
                    CurrentDuration = _random.Next(500, 2000),
                    Cause = "Unoptimized database query without indexes",
                    Frequency = _random.Next(30, 80),
                    AffectedExecutions = _random.Next(50, 500),
                    EstimatedImpactMs = _random.Next(200, 1000)
                },
                new()
                {
                    BottleneckId = "bn-3",
                    WorkflowId = workflowId,
                    Type = "parallelization",
                    Location = "sequential-processing",
                    ImpactPercent = _random.Next(10, 40),
                    Severity = "high",
                    CurrentDuration = _random.Next(2000, 8000),
                    Cause = "Steps executing sequentially that could run in parallel",
                    Frequency = 100,
                    AffectedExecutions = _random.Next(500, 5000),
                    EstimatedImpactMs = _random.Next(1000, 6000)
                }
            };

            return bottlenecks;
        }

        public async Task<OptimizationPlan> GenerateOptimizationPlanAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Generating optimization plan for workflow {WorkflowId}", workflowId);
            await Task.Delay(45, ct);

            var bottlenecks = await AnalyzeBottlenecksAsync(tenantId, workflowId, ct);
            var profile = await ProfileWorkflowAsync(tenantId, workflowId, ct);

            var plan = new OptimizationPlan
            {
                PlanId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TenantId = tenantId,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = "pending-approval",
                CurrentAvgExecutionTimeMs = profile.AverageExecutionTimeMs,
                EstimatedAvgExecutionTimeMs = profile.AverageExecutionTimeMs - (profile.AverageExecutionTimeMs / 3),
                ExpectedImprovementPercent = _random.Next(15, 60),
                ExpectedCostSavings = _random.Next(5000, 50000),
                Optimizations = GenerateOptimizations(bottlenecks),
                RequiredChanges = new List<string>
                {
                    "Enable connection pooling for database",
                    "Implement caching layer for API responses",
                    "Parallelize independent workflow steps",
                    "Add database indexes on lookup columns",
                    "Implement circuit breaker for external APIs"
                },
                RiskLevel = "low",
                EstimatedImplementationDays = _random.Next(2, 10),
                AppliedOptimizations = new List<string>()
            };

            return plan;
        }

        public async Task<bool> ApplyOptimizationAsync(string tenantId, string workflowId, string optimizationId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Applying optimization {OptimizationId}", optimizationId);
            await Task.Delay(30, ct);

            var key = $"{tenantId}:{workflowId}";
            if (!_optimizations.ContainsKey(key))
                _optimizations[key] = new List<Optimization>();

            var optimization = new Optimization
            {
                OptimizationId = optimizationId,
                WorkflowId = workflowId,
                AppliedAt = DateTimeOffset.UtcNow,
                Status = "applied"
            };

            _optimizations[key].Add(optimization);
            return true;
        }

        public async Task<ResourceAllocation> OptimizeResourcesAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Optimizing resources for workflow {WorkflowId}", workflowId);
            await Task.Delay(40, ct);

            var allocation = new ResourceAllocation
            {
                AllocationId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TenantId = tenantId,
                OptimizedAt = DateTimeOffset.UtcNow,
                CPUAllocation = _random.Next(500, 4000), // mCPU
                MemoryAllocation = _random.Next(256, 2048), // MB
                MaxConcurrentExecutions = _random.Next(10, 1000),
                CPUThrottle = _random.Next(20, 80), // %
                MemoryThrottle = _random.Next(10, 70), // %
                RecommendedCPU = _random.Next(1000, 2000),
                RecommendedMemory = _random.Next(512, 1024),
                CostOptimization = _random.Next(5, 40), // % savings
                SpotInstanceCompatible = _random.NextDouble() > 0.3,
                AutoScalingEnabled = true,
                MinReplicas = _random.Next(1, 3),
                MaxReplicas = _random.Next(5, 20),
                TargetCPUUtilization = 70,
                TargetMemoryUtilization = 80
            };

            return allocation;
        }

        public async Task<ParallelizationAnalysis> AnalyzeParallelizationAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Analyzing parallelization for workflow {WorkflowId}", workflowId);
            await Task.Delay(35, ct);

            var analysis = new ParallelizationAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                AnalyzedAt = DateTimeOffset.UtcNow,
                TotalSteps = _random.Next(5, 20),
                SequentialSteps = _random.Next(2, 10),
                ParallelizableSteps = _random.Next(2, 8),
                CurrentCriticalPath = _random.Next(1000, 5000), // ms
                OptimizedCriticalPath = _random.Next(500, 2500), // ms
                Speedup = _random.Next(2, 6),
                ParallelizationPercent = _random.Next(30, 80),
                DependencyChains = new List<string>
                {
                    "step-1 → step-3 → step-5",
                    "step-2 → step-4 → step-6",
                    "step-7 → step-8"
                },
                ParallelizableGroups = new List<string[]>
                {
                    new[] { "step-2", "step-3" },
                    new[] { "step-4", "step-5", "step-6" }
                },
                Recommendation = "Parallelize steps 2-3 and 4-6 for estimated 3.5x speedup",
                EstimatedTimeReduction = _random.Next(40, 80) // %
            };

            return analysis;
        }

        public async Task<CacheAnalysis> AnalyzeCacheAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Analyzing cache opportunities for workflow {WorkflowId}", workflowId);
            await Task.Delay(30, ct);

            var analysis = new CacheAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                AnalyzedAt = DateTimeOffset.UtcNow,
                CurrentCacheHitRate = _random.Next(20, 60), // %
                PotentialCacheHitRate = _random.Next(70, 95), // %
                CacheableDataSize = _random.Next(10, 500), // MB
                CacheEfficiency = _random.Next(40, 80), // %
                RedundantComputations = _random.Next(5, 50),
                DataReusability = _random.Next(30, 80), // %
                CacheTTLRecommendations = new List<string>
                {
                    "Cache API responses for 5 minutes",
                    "Cache database query results for 10 minutes",
                    "Cache transformation results for 30 minutes"
                },
                EstimatedSpeedup = _random.Next(1.5m, 4.0m),
                EstimatedCostReduction = _random.Next(10, 50) // %
            };

            return analysis;
        }

        public async Task<List<PerformanceRecommendation>> GetRecommendationsAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting performance recommendations");
            await Task.Delay(25, ct);

            var recommendations = new List<PerformanceRecommendation>
            {
                new()
                {
                    RecommendationId = "rec-1",
                    Title = "Implement Result Caching",
                    Description = "Cache API responses for 5 minutes to avoid redundant calls",
                    Category = "caching",
                    Priority = "high",
                    Impact = _random.Next(20, 40),
                    ImplementationEffort = "low",
                    EstimatedBenefit = "30% execution time reduction",
                    Implementation = "Add cache layer with 5-minute TTL"
                },
                new()
                {
                    RecommendationId = "rec-2",
                    Title = "Parallelize Independent Steps",
                    Description = "Execute non-dependent workflow steps concurrently",
                    Category = "parallelization",
                    Priority = "high",
                    Impact = _random.Next(25, 50),
                    ImplementationEffort = "medium",
                    EstimatedBenefit = "3.5x faster execution",
                    Implementation = "Refactor workflow to use parallel execution groups"
                },
                new()
                {
                    RecommendationId = "rec-3",
                    Title = "Optimize Database Queries",
                    Description = "Add indexes and optimize slow database queries",
                    Category = "database",
                    Priority = "high",
                    Impact = _random.Next(15, 35),
                    ImplementationEffort = "medium",
                    EstimatedBenefit = "25% database query speedup",
                    Implementation = "Create indexes on lookup columns and use query hints"
                },
                new()
                {
                    RecommendationId = "rec-4",
                    Title = "Implement Circuit Breaker",
                    Description = "Add circuit breaker pattern for external API calls",
                    Category = "resilience",
                    Priority = "medium",
                    Impact = _random.Next(10, 20),
                    ImplementationEffort = "low",
                    EstimatedBenefit = "Improved failure handling",
                    Implementation = "Add circuit breaker with fallback mechanism"
                }
            };

            var key = $"{tenantId}:{workflowId}";
            _recommendations[key] = recommendations;

            return recommendations;
        }

        public async Task<OptimizationHistory> GetOptimizationHistoryAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting optimization history for workflow {WorkflowId}", workflowId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{workflowId}";
            if (_history.ContainsKey(key))
                return _history[key];

            var history = new OptimizationHistory
            {
                HistoryId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TenantId = tenantId,
                TotalOptimizations = _random.Next(0, 10),
                AppliedOptimizations = _random.Next(0, 8),
                TotalImprovementPercent = _random.Next(0, 80),
                Events = GenerateOptimizationEvents()
            };

            _history[key] = history;
            return history;
        }

        public async Task<OptimizationMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating optimization metrics");
            await Task.Delay(30, ct);

            var metrics = new OptimizationMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                WorkflowsProfiled = _profiles.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                WorkflowsOptimized = _optimizations.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                TotalOptimizationsApplied = _optimizations.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0),
                AverageExecutionTimeImprovement = _random.Next(10, 60), // %
                AverageCostReduction = _random.Next(5, 40), // %
                HotWorkflows = _random.Next(1, 20),
                RecommendationsPending = _random.Next(0, 50),
                RecommendationsImplemented = _random.Next(0, 100),
                CacheOptimizationPotential = _random.Next(20, 80), // %
                ParallelizationOpportunities = _random.Next(5, 50),
                ResourceUtilizationAverage = _random.Next(40, 90), // %
                BottlenecksIdentified = _random.Next(10, 100)
            };

            return metrics;
        }

        private List<StepProfile> GenerateStepProfiles()
        {
            var steps = new List<StepProfile>();
            for (int i = 1; i <= 5; i++)
            {
                steps.Add(new StepProfile
                {
                    StepId = $"step-{i}",
                    StepName = $"Process Step {i}",
                    AverageDurationMs = _random.Next(100, 3000),
                    MinDurationMs = _random.Next(50, 500),
                    MaxDurationMs = _random.Next(1000, 10000),
                    ExecutionCount = _random.Next(100, 10000),
                    FailureCount = _random.Next(0, 100),
                    CPUPercent = _random.Next(5, 80),
                    MemoryMB = _random.Next(10, 500)
                });
            }
            return steps;
        }

        private List<OptimizationDetail> GenerateOptimizations(List<BottleneckAnalysis> bottlenecks)
        {
            var optimizations = new List<OptimizationDetail>();

            foreach (var bottleneck in bottlenecks)
            {
                optimizations.Add(new OptimizationDetail
                {
                    OptimizationId = Guid.NewGuid().ToString("N"),
                    BottleneckId = bottleneck.BottleneckId,
                    Name = $"Optimize {bottleneck.Location}",
                    Description = $"Address {bottleneck.Cause}",
                    Priority = bottleneck.Severity == "high" ? "high" : "medium",
                    EstimatedBenefit = bottleneck.EstimatedImpactMs,
                    Complexity = "medium"
                });
            }

            return optimizations;
        }

        private List<OptimizationEvent> GenerateOptimizationEvents()
        {
            var events = new List<OptimizationEvent>();
            for (int i = 0; i < 5; i++)
            {
                events.Add(new OptimizationEvent
                {
                    EventId = $"evt-{i}",
                    Type = i % 2 == 0 ? "optimization_applied" : "improvement_measured",
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-i),
                    Description = i % 2 == 0 ? "Cache layer implemented" : "Performance improved 25%"
                });
            }
            return events;
        }
    }

    public class PerformanceProfile
    {
        public string ProfileId { get; set; }
        public string WorkflowId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset ProfiledAt { get; set; }
        public int ExecutionCount { get; set; }
        public int TotalExecutionTimeMs { get; set; }
        public int AverageExecutionTimeMs { get; set; }
        public int MinExecutionTimeMs { get; set; }
        public int MaxExecutionTimeMs { get; set; }
        public int P50ExecutionTimeMs { get; set; }
        public int P95ExecutionTimeMs { get; set; }
        public int P99ExecutionTimeMs { get; set; }
        public int CPUUsagePercent { get; set; }
        public int MemoryUsageMB { get; set; }
        public int DiskIOPercent { get; set; }
        public int NetworkIOPercent { get; set; }
        public double FailureRate { get; set; }
        public List<StepProfile> Steps { get; set; } = new();
        public int DataVolume { get; set; }
        public int CacheHitRate { get; set; }
        public int ThrottledCount { get; set; }
    }

    public class StepProfile
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public int AverageDurationMs { get; set; }
        public int MinDurationMs { get; set; }
        public int MaxDurationMs { get; set; }
        public int ExecutionCount { get; set; }
        public int FailureCount { get; set; }
        public int CPUPercent { get; set; }
        public int MemoryMB { get; set; }
    }

    public class BottleneckAnalysis
    {
        public string BottleneckId { get; set; }
        public string WorkflowId { get; set; }
        public string Type { get; set; }
        public string Location { get; set; }
        public int ImpactPercent { get; set; }
        public string Severity { get; set; }
        public int CurrentDuration { get; set; }
        public string Cause { get; set; }
        public int Frequency { get; set; }
        public int AffectedExecutions { get; set; }
        public int EstimatedImpactMs { get; set; }
    }

    public class OptimizationPlan
    {
        public string PlanId { get; set; }
        public string WorkflowId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Status { get; set; }
        public int CurrentAvgExecutionTimeMs { get; set; }
        public int EstimatedAvgExecutionTimeMs { get; set; }
        public int ExpectedImprovementPercent { get; set; }
        public int ExpectedCostSavings { get; set; }
        public List<OptimizationDetail> Optimizations { get; set; } = new();
        public List<string> RequiredChanges { get; set; } = new();
        public string RiskLevel { get; set; }
        public int EstimatedImplementationDays { get; set; }
        public List<string> AppliedOptimizations { get; set; } = new();
    }

    public class OptimizationDetail
    {
        public string OptimizationId { get; set; }
        public string BottleneckId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public int EstimatedBenefit { get; set; }
        public string Complexity { get; set; }
    }

    public class Optimization
    {
        public string OptimizationId { get; set; }
        public string WorkflowId { get; set; }
        public DateTimeOffset AppliedAt { get; set; }
        public string Status { get; set; }
    }

    public class ResourceAllocation
    {
        public string AllocationId { get; set; }
        public string WorkflowId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset OptimizedAt { get; set; }
        public int CPUAllocation { get; set; }
        public int MemoryAllocation { get; set; }
        public int MaxConcurrentExecutions { get; set; }
        public int CPUThrottle { get; set; }
        public int MemoryThrottle { get; set; }
        public int RecommendedCPU { get; set; }
        public int RecommendedMemory { get; set; }
        public int CostOptimization { get; set; }
        public bool SpotInstanceCompatible { get; set; }
        public bool AutoScalingEnabled { get; set; }
        public int MinReplicas { get; set; }
        public int MaxReplicas { get; set; }
        public int TargetCPUUtilization { get; set; }
        public int TargetMemoryUtilization { get; set; }
    }

    public class ParallelizationAnalysis
    {
        public string AnalysisId { get; set; }
        public string WorkflowId { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public int TotalSteps { get; set; }
        public int SequentialSteps { get; set; }
        public int ParallelizableSteps { get; set; }
        public int CurrentCriticalPath { get; set; }
        public int OptimizedCriticalPath { get; set; }
        public int Speedup { get; set; }
        public int ParallelizationPercent { get; set; }
        public List<string> DependencyChains { get; set; } = new();
        public List<string[]> ParallelizableGroups { get; set; } = new();
        public string Recommendation { get; set; }
        public int EstimatedTimeReduction { get; set; }
    }

    public class CacheAnalysis
    {
        public string AnalysisId { get; set; }
        public string WorkflowId { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public int CurrentCacheHitRate { get; set; }
        public int PotentialCacheHitRate { get; set; }
        public int CacheableDataSize { get; set; }
        public int CacheEfficiency { get; set; }
        public int RedundantComputations { get; set; }
        public int DataReusability { get; set; }
        public List<string> CacheTTLRecommendations { get; set; } = new();
        public decimal EstimatedSpeedup { get; set; }
        public int EstimatedCostReduction { get; set; }
    }

    public class PerformanceRecommendation
    {
        public string RecommendationId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }
        public int Impact { get; set; }
        public string ImplementationEffort { get; set; }
        public string EstimatedBenefit { get; set; }
        public string Implementation { get; set; }
    }

    public class OptimizationHistory
    {
        public string HistoryId { get; set; }
        public string WorkflowId { get; set; }
        public string TenantId { get; set; }
        public int TotalOptimizations { get; set; }
        public int AppliedOptimizations { get; set; }
        public int TotalImprovementPercent { get; set; }
        public List<OptimizationEvent> Events { get; set; } = new();
    }

    public class OptimizationEvent
    {
        public string EventId { get; set; }
        public string Type { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Description { get; set; }
    }

    public class OptimizationMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int WorkflowsProfiled { get; set; }
        public int WorkflowsOptimized { get; set; }
        public int TotalOptimizationsApplied { get; set; }
        public int AverageExecutionTimeImprovement { get; set; }
        public int AverageCostReduction { get; set; }
        public int HotWorkflows { get; set; }
        public int RecommendationsPending { get; set; }
        public int RecommendationsImplemented { get; set; }
        public int CacheOptimizationPotential { get; set; }
        public int ParallelizationOpportunities { get; set; }
        public int ResourceUtilizationAverage { get; set; }
        public int BottlenecksIdentified { get; set; }
    }
}
