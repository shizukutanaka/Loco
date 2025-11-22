// Phase 5: Cost Analytics Engine
// Track resource usage per workflow, calculate execution costs, and provide cost optimization insights
// Enables cost attribution and FinOps optimization

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Analytics;

/// <summary>
/// Resource consumption metrics
/// </summary>
public class ResourceConsumption
{
    public double ComputeTimeSeconds { get; set; }
    public double MemoryGbSeconds { get; set; }    // Memory in GB * seconds
    public double NetworkGbOut { get; set; }       // Network egress in GB
    public long StorageGb { get; set; }            // Storage in GB-hours
    public int DatabaseQueries { get; set; }
    public int ApiCalls { get; set; }
}

/// <summary>
/// Cost breakdown for a single execution
/// </summary>
public class ExecutionCost
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public ResourceConsumption Resources { get; set; } = new();
    public double ComputeCost { get; set; }        // $ per compute time
    public double MemoryCost { get; set; }         // $ per memory usage
    public double NetworkCost { get; set; }        // $ per network egress
    public double StorageCost { get; set; }        // $ per storage
    public double DatabaseCost { get; set; }       // $ per database query
    public double ApiCost { get; set; }            // $ per API call
    public double TotalCost { get; set; }          // Total $ for this execution
}

/// <summary>
/// Workflow cost summary (aggregated)
/// </summary>
public class WorkflowCostAnalysis
{
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public double TotalCost { get; set; }
    public double AverageCostPerExecution { get; set; }
    public double MinimumExecutionCost { get; set; }
    public double MaximumExecutionCost { get; set; }
    public Dictionary<string, double> CostBreakdown { get; set; } = new();
    public List<string> CostOptimizationOpportunities { get; set; } = new();
    public double ProjectedMonthlyCost => AverageCostPerExecution * 2880; // ~2880 executions/month (1/min)
}

/// <summary>
/// Cost pricing configuration
/// </summary>
public class CostPricing
{
    public double ComputePricePerHour { get; set; } = 0.05; // $ per vCPU-hour
    public double MemoryPricePerGbHour { get; set; } = 0.01; // $ per GB-hour
    public double NetworkPricePerGbOut { get; set; } = 0.12; // $ per GB egress
    public double StoragePricePerGbMonth { get; set; } = 0.023; // $ per GB-month
    public double DatabaseQueryPrice { get; set; } = 0.000001; // $ per query
    public double ApiCallPrice { get; set; } = 0.00001; // $ per API call
}

/// <summary>
/// Cost analytics interface
/// </summary>
public interface ICostAnalyticsEngine
{
    Task<ExecutionCost> CalculateExecutionCostAsync(
        string executionId,
        string workflowId,
        ResourceConsumption resources,
        CancellationToken ct = default);

    Task<WorkflowCostAnalysis> AnalyzeWorkflowCostsAsync(
        string workflowId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> GetCostTrendAsync(
        string workflowId,
        int days = 30,
        CancellationToken ct = default);

    Task<List<WorkflowCostAnalysis>> GetMostExpensiveWorkflowsAsync(
        int limit = 10,
        CancellationToken ct = default);

    Task RecordExecutionCostAsync(
        ExecutionCost cost,
        CancellationToken ct = default);

    void SetPricing(CostPricing pricing);
}

/// <summary>
/// Cost analytics engine implementation
/// </summary>
public class CostAnalyticsEngine : ICostAnalyticsEngine
{
    private readonly ILogger<CostAnalyticsEngine> _logger;
    private CostPricing _pricing;
    private readonly Dictionary<string, List<ExecutionCost>> _executionCosts;
    private readonly Dictionary<string, WorkflowCostAnalysis> _workflowAnalysis;

    public CostAnalyticsEngine(ILogger<CostAnalyticsEngine> logger)
    {
        _logger = logger;
        _pricing = new CostPricing();
        _executionCosts = new Dictionary<string, List<ExecutionCost>>();
        _workflowAnalysis = new Dictionary<string, WorkflowCostAnalysis>();
    }

    /// <summary>
    /// Calculate cost for single execution
    /// </summary>
    public async Task<ExecutionCost> CalculateExecutionCostAsync(
        string executionId,
        string workflowId,
        ResourceConsumption resources,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var cost = new ExecutionCost
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            ExecutedAt = DateTime.UtcNow,
            Resources = resources,
        };

        // Calculate individual costs
        cost.ComputeCost = resources.ComputeTimeSeconds / 3600.0 * _pricing.ComputePricePerHour;
        cost.MemoryCost = resources.MemoryGbSeconds / 3600.0 * _pricing.MemoryPricePerGbHour;
        cost.NetworkCost = resources.NetworkGbOut * _pricing.NetworkPricePerGbOut;
        cost.StorageCost = resources.StorageGb * _pricing.StoragePricePerGbMonth / 30.0;
        cost.DatabaseCost = resources.DatabaseQueries * _pricing.DatabaseQueryPrice;
        cost.ApiCost = resources.ApiCalls * _pricing.ApiCallPrice;

        // Total cost
        cost.TotalCost = cost.ComputeCost + cost.MemoryCost + cost.NetworkCost +
                        cost.StorageCost + cost.DatabaseCost + cost.ApiCost;

        _logger.LogInformation(
            "Calculated cost for {ExecutionId}: ${TotalCost:F4} ({Compute:F4} compute, {Memory:F4} memory, {Db:F4} DB)",
            executionId, cost.TotalCost, cost.ComputeCost, cost.MemoryCost, cost.DatabaseCost);

        return cost;
    }

    /// <summary>
    /// Analyze workflow costs
    /// </summary>
    public async Task<WorkflowCostAnalysis> AnalyzeWorkflowCostsAsync(
        string workflowId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate analysis

        if (!_executionCosts.TryGetValue(workflowId, out var costs))
        {
            return new WorkflowCostAnalysis { WorkflowId = workflowId };
        }

        // Filter by date range
        var filteredCosts = costs;
        if (startDate.HasValue || endDate.HasValue)
        {
            filteredCosts = costs.Where(c =>
                (startDate == null || c.ExecutedAt >= startDate) &&
                (endDate == null || c.ExecutedAt <= endDate)
            ).ToList();
        }

        if (filteredCosts.Count == 0)
        {
            return new WorkflowCostAnalysis { WorkflowId = workflowId };
        }

        var analysis = new WorkflowCostAnalysis
        {
            WorkflowId = workflowId,
            ExecutionCount = filteredCosts.Count,
            TotalCost = filteredCosts.Sum(c => c.TotalCost),
            AverageCostPerExecution = filteredCosts.Average(c => c.TotalCost),
            MinimumExecutionCost = filteredCosts.Min(c => c.TotalCost),
            MaximumExecutionCost = filteredCosts.Max(c => c.TotalCost),
        };

        // Cost breakdown
        analysis.CostBreakdown["compute"] = filteredCosts.Sum(c => c.ComputeCost);
        analysis.CostBreakdown["memory"] = filteredCosts.Sum(c => c.MemoryCost);
        analysis.CostBreakdown["network"] = filteredCosts.Sum(c => c.NetworkCost);
        analysis.CostBreakdown["storage"] = filteredCosts.Sum(c => c.StorageCost);
        analysis.CostBreakdown["database"] = filteredCosts.Sum(c => c.DatabaseCost);
        analysis.CostBreakdown["api_calls"] = filteredCosts.Sum(c => c.ApiCost);

        // Identify optimization opportunities
        IdentifyOptimizationOpportunities(analysis, filteredCosts);

        _logger.LogInformation(
            "Analyzed costs for {WorkflowId}: ${Total:F2} total, ${Avg:F4} average per execution",
            workflowId, analysis.TotalCost, analysis.AverageCostPerExecution);

        return analysis;
    }

    /// <summary>
    /// Get cost trend over time
    /// </summary>
    public async Task<Dictionary<string, double>> GetCostTrendAsync(
        string workflowId,
        int days = 30,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct);

        var trend = new Dictionary<string, double>();

        if (!_executionCosts.TryGetValue(workflowId, out var costs))
        {
            return trend;
        }

        var startDate = DateTime.UtcNow.AddDays(-days);
        var recentCosts = costs.Where(c => c.ExecutedAt >= startDate).ToList();

        // Group by day
        for (int i = days; i >= 0; i--)
        {
            var dayDate = DateTime.UtcNow.AddDays(-i).Date;
            var dayCosts = recentCosts
                .Where(c => c.ExecutedAt.Date == dayDate)
                .Sum(c => c.TotalCost);

            trend[dayDate.ToString("yyyy-MM-dd")] = dayCosts;
        }

        return trend;
    }

    /// <summary>
    /// Get most expensive workflows
    /// </summary>
    public async Task<List<WorkflowCostAnalysis>> GetMostExpensiveWorkflowsAsync(
        int limit = 10,
        CancellationToken ct = default)
    {
        await Task.Delay(300, ct);

        var workflows = _workflowAnalysis
            .Values
            .OrderByDescending(w => w.TotalCost)
            .Take(limit)
            .ToList();

        _logger.LogInformation(
            "Top {Count} most expensive workflows retrieved",
            workflows.Count);

        return workflows;
    }

    /// <summary>
    /// Record execution cost
    /// </summary>
    public async Task RecordExecutionCostAsync(
        ExecutionCost cost,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_executionCosts.ContainsKey(cost.WorkflowId))
        {
            _executionCosts[cost.WorkflowId] = new List<ExecutionCost>();
        }

        _executionCosts[cost.WorkflowId].Add(cost);

        // Update workflow analysis
        if (_workflowAnalysis.TryGetValue(cost.WorkflowId, out var analysis))
        {
            analysis.ExecutionCount++;
            analysis.TotalCost += cost.TotalCost;
            analysis.AverageCostPerExecution = analysis.TotalCost / analysis.ExecutionCount;
        }
        else
        {
            _workflowAnalysis[cost.WorkflowId] = new WorkflowCostAnalysis
            {
                WorkflowId = cost.WorkflowId,
                ExecutionCount = 1,
                TotalCost = cost.TotalCost,
                AverageCostPerExecution = cost.TotalCost,
                MinimumExecutionCost = cost.TotalCost,
                MaximumExecutionCost = cost.TotalCost,
            };
        }
    }

    /// <summary>
    /// Set custom pricing
    /// </summary>
    public void SetPricing(CostPricing pricing)
    {
        _pricing = pricing;
        _logger.LogInformation("Cost pricing updated");
    }

    // Private helper methods
    private void IdentifyOptimizationOpportunities(
        WorkflowCostAnalysis analysis,
        List<ExecutionCost> costs)
    {
        // Database optimization
        if (analysis.CostBreakdown.TryGetValue("database", out var dbCost) &&
            dbCost > analysis.TotalCost * 0.30) // > 30% of total
        {
            analysis.CostOptimizationOpportunities.Add(
                "High database query cost - consider caching or query optimization");
        }

        // API call optimization
        if (analysis.CostBreakdown.TryGetValue("api_calls", out var apiCost) &&
            apiCost > analysis.TotalCost * 0.25) // > 25% of total
        {
            analysis.CostOptimizationOpportunities.Add(
                "High API call cost - consider batch operations or caching");
        }

        // Memory optimization
        if (analysis.CostBreakdown.TryGetValue("memory", out var memoryCost) &&
            memoryCost > analysis.TotalCost * 0.20) // > 20% of total
        {
            analysis.CostOptimizationOpportunities.Add(
                "High memory usage - consider optimizing data structures");
        }

        // Network optimization
        if (analysis.CostBreakdown.TryGetValue("network", out var networkCost) &&
            networkCost > analysis.TotalCost * 0.15) // > 15% of total
        {
            analysis.CostOptimizationOpportunities.Add(
                "High network egress - consider data compression or edge caching");
        }

        // Cost variance
        if (analysis.MaximumExecutionCost > analysis.AverageCostPerExecution * 2)
        {
            analysis.CostOptimizationOpportunities.Add(
                "High cost variance detected - investigate slow executions");
        }
    }
}
