// Phase 8: AI Workflow Recommendations Engine
// Machine learning-based workflow analysis and intelligent suggestions
// Learns from historical patterns to recommend optimizations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// Recommendation category
/// </summary>
public enum RecommendationCategory
{
    Performance = 0,
    Reliability = 1,
    CostOptimization = 2,
    Security = 3,
    Scalability = 4,
    Maintainability = 5,
    BestPractice = 6,
}

/// <summary>
/// Recommendation priority
/// </summary>
public enum RecommendationPriority
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Info = 4,
}

/// <summary>
/// AI-powered workflow recommendation
/// </summary>
public class WorkflowRecommendation
{
    public string RecommendationId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public RecommendationCategory Category { get; set; }
    public RecommendationPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReasonsExplained { get; set; }

    // Impact metrics
    public double? ImpactScore { get; set; }        // 0.0-1.0
    public double? EstimatedTimeReduction { get; set; } // percentage
    public double? EstimatedCostReduction { get; set; } // percentage
    public int? EstimatedFailureReduction { get; set; } // percentage

    // Implementation
    public string? ImplementationSteps { get; set; }
    public int? EstimatedImplementationTimeMinutes { get; set; }
    public string? RiskLevel { get; set; }          // low, medium, high

    // Metadata
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public bool IsDismissed { get; set; }
    public string? DismissalReason { get; set; }

    // Confidence
    public double ConfidenceScore { get; set; }    // 0.0-1.0
    public string? SupportingData { get; set; }    // JSON with evidence
}

/// <summary>
/// Workflow learning profile
/// </summary>
public class WorkflowLearningProfile
{
    public string WorkflowId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    // Execution patterns
    public int TotalExecutions { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double P95ExecutionTimeMs { get; set; }
    public double SuccessRate { get; set; }
    public double ErrorRate { get; set; }

    // Step-level statistics
    public Dictionary<string, StepStatistics>? StepStats { get; set; }

    // Time patterns
    public Dictionary<string, int>? ExecutionsByHour { get; set; }
    public Dictionary<string, int>? ExecutionsByDay { get; set; }
    public double? PeakHour { get; set; }

    // Failure patterns
    public Dictionary<string, int>? FailuresByStep { get; set; }
    public Dictionary<string, int>? FailuresByError { get; set; }

    // Resource usage
    public double AverageMemoryMb { get; set; }
    public double AverageNetworkMb { get; set; }
    public double AverageCpuPercent { get; set; }

    // Correlations (what affects what)
    public Dictionary<string, double>? StepDurationCorrelations { get; set; }
    public Dictionary<string, double>? InputParameterCorrelations { get; set; }

    // ML-derived features
    public List<string>? AnomalyPatterns { get; set; }
    public List<string>? OptimizationOpportunities { get; set; }
    public DateTime LastAnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-step statistics
/// </summary>
public class StepStatistics
{
    public string StepId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public double AverageDurationMs { get; set; }
    public double P95DurationMs { get; set; }
    public double P99DurationMs { get; set; }
    public double SuccessRate { get; set; }
    public List<string>? CommonErrors { get; set; }
    public int? RetryCount { get; set; }
    public double? RetrySuccessRate { get; set; }
}

/// <summary>
/// Recommendation engine interface
/// </summary>
public interface IWorkflowRecommendationEngine
{
    // Recommendations
    Task<List<WorkflowRecommendation>> GetRecommendationsAsync(
        string workflowId,
        RecommendationPriority? minPriority = null,
        CancellationToken ct = default);

    Task<WorkflowRecommendation?> GetRecommendationAsync(
        string recommendationId,
        CancellationToken ct = default);

    Task<List<WorkflowRecommendation>> GetTopRecommendationsAsync(
        string tenantId,
        int limit = 10,
        CancellationToken ct = default);

    Task<bool> ApplyRecommendationAsync(
        string recommendationId,
        CancellationToken ct = default);

    Task<bool> DismissRecommendationAsync(
        string recommendationId,
        string? reason = null,
        CancellationToken ct = default);

    // Learning profiles
    Task<WorkflowLearningProfile> AnalyzeWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<WorkflowLearningProfile?> GetLearningProfileAsync(
        string workflowId,
        CancellationToken ct = default);

    // Batch analysis
    Task<Dictionary<string, List<WorkflowRecommendation>>> AnalyzeTenantWorkflowsAsync(
        string tenantId,
        CancellationToken ct = default);

    // Statistics
    Task<Dictionary<string, int>> GetRecommendationStatisticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// AI workflow recommendation engine implementation
/// </summary>
public class WorkflowRecommendationEngine : IWorkflowRecommendationEngine
{
    private readonly ILogger<WorkflowRecommendationEngine> _logger;
    private readonly Dictionary<string, List<WorkflowRecommendation>> _recommendations;
    private readonly Dictionary<string, WorkflowLearningProfile> _learningProfiles;
    private readonly Dictionary<string, List<(DateTime Time, long DurationMs, bool Success)>> _executionHistory;

    public WorkflowRecommendationEngine(ILogger<WorkflowRecommendationEngine> logger)
    {
        _logger = logger;
        _recommendations = new Dictionary<string, List<WorkflowRecommendation>>();
        _learningProfiles = new Dictionary<string, WorkflowLearningProfile>();
        _executionHistory = new Dictionary<string, List<(DateTime, long, bool)>>();
    }

    // Recommendations
    public async Task<List<WorkflowRecommendation>> GetRecommendationsAsync(
        string workflowId,
        RecommendationPriority? minPriority = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_recommendations.TryGetValue(workflowId, out var recs))
        {
            return new List<WorkflowRecommendation>();
        }

        var results = recs
            .Where(r => minPriority == null || r.Priority <= minPriority)
            .Where(r => !r.IsDismissed)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.ImpactScore ?? 0)
            .ToList();

        return results;
    }

    public async Task<WorkflowRecommendation?> GetRecommendationAsync(
        string recommendationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var recs in _recommendations.Values)
        {
            var rec = recs.FirstOrDefault(r => r.RecommendationId == recommendationId);
            if (rec != null)
                return rec;
        }

        return null;
    }

    public async Task<List<WorkflowRecommendation>> GetTopRecommendationsAsync(
        string tenantId,
        int limit = 10,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allRecs = _recommendations.Values
            .SelectMany(r => r)
            .Where(r => !r.IsDismissed && !r.IsApplied)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.ImpactScore ?? 0)
            .Take(limit)
            .ToList();

        return allRecs;
    }

    public async Task<bool> ApplyRecommendationAsync(
        string recommendationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var recs in _recommendations.Values)
        {
            var rec = recs.FirstOrDefault(r => r.RecommendationId == recommendationId);
            if (rec != null)
            {
                rec.IsApplied = true;
                rec.AppliedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Recommendation applied: {RecommendationId}, Workflow: {WorkflowId}",
                    recommendationId, rec.WorkflowId);

                return true;
            }
        }

        return false;
    }

    public async Task<bool> DismissRecommendationAsync(
        string recommendationId,
        string? reason = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var recs in _recommendations.Values)
        {
            var rec = recs.FirstOrDefault(r => r.RecommendationId == recommendationId);
            if (rec != null)
            {
                rec.IsDismissed = true;
                rec.DismissalReason = reason;

                _logger.LogInformation(
                    "Recommendation dismissed: {RecommendationId}",
                    recommendationId);

                return true;
            }
        }

        return false;
    }

    // Learning profiles
    public async Task<WorkflowLearningProfile> AnalyzeWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate ML analysis

        if (!_executionHistory.TryGetValue(workflowId, out var history) || history.Count == 0)
        {
            return new WorkflowLearningProfile { WorkflowId = workflowId };
        }

        var profile = new WorkflowLearningProfile
        {
            WorkflowId = workflowId,
            TotalExecutions = history.Count,
            AverageExecutionTimeMs = history.Average(h => h.DurationMs),
            P95ExecutionTimeMs = GetPercentile(history.Select(h => (double)h.DurationMs).ToList(), 0.95),
            SuccessRate = history.Count(h => h.Success) / (double)history.Count,
            ErrorRate = 1.0 - (history.Count(h => h.Success) / (double)history.Count),
            LastAnalyzedAt = DateTime.UtcNow,
        };

        // Generate recommendations based on analysis
        await GenerateRecommendationsAsync(workflowId, profile, ct);

        _learningProfiles[workflowId] = profile;

        _logger.LogInformation(
            "Workflow analyzed: {WorkflowId}, Executions: {Count}, SuccessRate: {SuccessRate:P}",
            workflowId, history.Count, profile.SuccessRate);

        return profile;
    }

    public async Task<WorkflowLearningProfile?> GetLearningProfileAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _learningProfiles.TryGetValue(workflowId, out var profile);
        return profile;
    }

    public async Task<Dictionary<string, List<WorkflowRecommendation>>> AnalyzeTenantWorkflowsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.Delay(500, ct); // Simulate bulk analysis

        var results = new Dictionary<string, List<WorkflowRecommendation>>();

        foreach (var workflowId in _executionHistory.Keys)
        {
            var recs = await GetRecommendationsAsync(workflowId, ct: ct);
            results[workflowId] = recs;
        }

        return results;
    }

    public async Task<Dictionary<string, int>> GetRecommendationStatisticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allRecs = _recommendations.Values.SelectMany(r => r).ToList();

        return new Dictionary<string, int>
        {
            ["total_recommendations"] = allRecs.Count,
            ["critical_priority"] = allRecs.Count(r => r.Priority == RecommendationPriority.Critical),
            ["high_priority"] = allRecs.Count(r => r.Priority == RecommendationPriority.High),
            ["performance_category"] = allRecs.Count(r => r.Category == RecommendationCategory.Performance),
            ["cost_optimization"] = allRecs.Count(r => r.Category == RecommendationCategory.CostOptimization),
            ["security_category"] = allRecs.Count(r => r.Category == RecommendationCategory.Security),
            ["applied_recommendations"] = allRecs.Count(r => r.IsApplied),
            ["dismissed_recommendations"] = allRecs.Count(r => r.IsDismissed),
        };
    }

    // Private helpers
    private async Task GenerateRecommendationsAsync(
        string workflowId,
        WorkflowLearningProfile profile,
        CancellationToken ct)
    {
        var recs = new List<WorkflowRecommendation>();

        // Performance recommendations
        if (profile.AverageExecutionTimeMs > 5000)
        {
            recs.Add(new WorkflowRecommendation
            {
                WorkflowId = workflowId,
                Category = RecommendationCategory.Performance,
                Priority = RecommendationPriority.High,
                Title = "Slow Workflow Execution",
                Description = $"Workflow takes {profile.AverageExecutionTimeMs:F0}ms on average. Consider parallelizing steps.",
                EstimatedTimeReduction = 0.30, // 30% improvement
                ConfidenceScore = 0.85,
            });
        }

        // Reliability recommendations
        if (profile.ErrorRate > 0.05)
        {
            recs.Add(new WorkflowRecommendation
            {
                WorkflowId = workflowId,
                Category = RecommendationCategory.Reliability,
                Priority = RecommendationPriority.High,
                Title = "High Error Rate Detected",
                Description = $"Failure rate is {profile.ErrorRate:P}. Implement error handling and retries.",
                EstimatedFailureReduction = 50,
                ConfidenceScore = 0.90,
            });
        }

        // Cost optimization
        if (profile.AverageMemoryMb > 1000)
        {
            recs.Add(new WorkflowRecommendation
            {
                WorkflowId = workflowId,
                Category = RecommendationCategory.CostOptimization,
                Priority = RecommendationPriority.Medium,
                Title = "High Memory Usage",
                Description = "Consider optimizing data structures or streaming large datasets.",
                EstimatedCostReduction = 0.25,
                ConfidenceScore = 0.75,
            });
        }

        // Best practices
        recs.Add(new WorkflowRecommendation
        {
            WorkflowId = workflowId,
            Category = RecommendationCategory.BestPractice,
            Priority = RecommendationPriority.Low,
            Title = "Add Execution Timeout",
            Description = "Set explicit timeouts on long-running steps to prevent hanging.",
            ConfidenceScore = 0.70,
        });

        if (!_recommendations.ContainsKey(workflowId))
        {
            _recommendations[workflowId] = new List<WorkflowRecommendation>();
        }

        _recommendations[workflowId].AddRange(recs);
        await Task.CompletedTask;
    }

    private double GetPercentile(List<double> values, double percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)((percentile * sorted.Count) - 1);
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
