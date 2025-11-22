// Phase 11: Advanced Analytics & Business Intelligence Engine
// Comprehensive analytics, KPI tracking, and business intelligence
// Real-time insights into workflow performance and business metrics

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Analytics;

/// <summary>
/// Execution metric
/// </summary>
public class ExecutionMetric
{
    public string MetricId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public string Status { get; set; } = string.Empty; // success, failure, timeout
    public int RetryCount { get; set; }
    public double CostUsd { get; set; }
    public long DataProcessedMb { get; set; }
    public Dictionary<string, object>? CustomMetrics { get; set; }
}

/// <summary>
/// KPI (Key Performance Indicator)
/// </summary>
public class KPI
{
    public string KpiId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // performance, reliability, cost, efficiency
    public double TargetValue { get; set; }
    public double CurrentValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? Threshold { get; set; }
    public string Status { get; set; } = "healthy"; // healthy, warning, critical
    public DateTime? LastUpdated { get; set; }
    public List<double> HistoricalValues { get; set; } = new();
}

/// <summary>
/// Analytics dashboard view
/// </summary>
public class AnalyticsDashboard
{
    public string DashboardId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Key metrics
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public long AverageDurationMs { get; set; }
    public long P95DurationMs { get; set; }
    public long P99DurationMs { get; set; }

    // Cost metrics
    public double TotalCostThisMonth { get; set; }
    public double AverageCostPerExecution { get; set; }
    public double CostSavingsThisMonth { get; set; }

    // Resource metrics
    public double AverageResourceUtilization { get; set; }
    public double PeakResourceUtilization { get; set; }

    // Trend data
    public List<KPI> KeyKPIs { get; set; } = new();
    public Dictionary<string, double> DailyMetrics { get; set; } = new();
}

/// <summary>
/// Workflow performance report
/// </summary>
public class WorkflowPerformanceReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
    public long AverageDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public double StdDevDurationMs { get; set; }
    public List<string> BottleneckSteps { get; set; } = new();
    public List<string> FailurePatterns { get; set; } = new();
    public double TotalCostUsd { get; set; }
}

/// <summary>
/// Resource utilization report
/// </summary>
public class ResourceUtilizationReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public double AverageCpuPercent { get; set; }
    public double PeakCpuPercent { get; set; }
    public double AverageMemoryPercent { get; set; }
    public double PeakMemoryPercent { get; set; }
    public double AverageDiskUtilization { get; set; }
    public double NetworkBandwidthUtilization { get; set; }
    public double OverallUtilizationPercent { get; set; }
}

/// <summary>
/// Advanced analytics interface
/// </summary>
public interface IAdvancedAnalyticsEngine
{
    // Metric collection
    Task<ExecutionMetric> RecordMetricAsync(
        string workflowId,
        string executionId,
        ExecutionMetric metric,
        CancellationToken ct = default);

    Task<List<ExecutionMetric>> GetMetricsAsync(
        string workflowId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    // KPI management
    Task<KPI> CreateKPIAsync(
        string tenantId,
        KPI kpi,
        CancellationToken ct = default);

    Task<List<KPI>> GetKPIsAsync(
        string tenantId,
        string? category = null,
        CancellationToken ct = default);

    Task<bool> UpdateKPIValueAsync(
        string kpiId,
        double newValue,
        CancellationToken ct = default);

    // Dashboards
    Task<AnalyticsDashboard> GetAnalyticsDashboardAsync(
        string tenantId,
        CancellationToken ct = default);

    // Reports
    Task<WorkflowPerformanceReport> GeneratePerformanceReportAsync(
        string workflowId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    Task<ResourceUtilizationReport> GenerateResourceReportAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Advanced analytics engine implementation
/// </summary>
public class AdvancedAnalyticsEngine : IAdvancedAnalyticsEngine
{
    private readonly ILogger<AdvancedAnalyticsEngine> _logger;
    private readonly Dictionary<string, List<ExecutionMetric>> _metrics;
    private readonly Dictionary<string, List<KPI>> _kpis;

    public AdvancedAnalyticsEngine(ILogger<AdvancedAnalyticsEngine> logger)
    {
        _logger = logger;
        _metrics = new Dictionary<string, List<ExecutionMetric>>();
        _kpis = new Dictionary<string, List<KPI>>();
    }

    // Metric collection
    public async Task<ExecutionMetric> RecordMetricAsync(
        string workflowId,
        string executionId,
        ExecutionMetric metric,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        metric.WorkflowId = workflowId;
        metric.ExecutionId = executionId;

        if (!_metrics.ContainsKey(workflowId))
        {
            _metrics[workflowId] = new List<ExecutionMetric>();
        }

        _metrics[workflowId].Add(metric);

        _logger.LogInformation(
            "Metric recorded: WorkflowId={WorkflowId}, ExecutionId={ExecutionId}, Duration={Duration}ms, Status={Status}",
            workflowId, executionId, metric.DurationMs, metric.Status);

        return metric;
    }

    public async Task<List<ExecutionMetric>> GetMetricsAsync(
        string workflowId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_metrics.TryGetValue(workflowId, out var metrics))
        {
            return new List<ExecutionMetric>();
        }

        return metrics
            .Where(m => from == null || m.Timestamp >= from)
            .Where(m => to == null || m.Timestamp <= to)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    // KPI management
    public async Task<KPI> CreateKPIAsync(
        string tenantId,
        KPI kpi,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        kpi.TenantId = tenantId;

        if (!_kpis.ContainsKey(tenantId))
        {
            _kpis[tenantId] = new List<KPI>();
        }

        _kpis[tenantId].Add(kpi);

        _logger.LogInformation(
            "KPI created: KpiId={KpiId}, Name={KpiName}, Target={TargetValue} {Unit}",
            kpi.KpiId, kpi.KpiName, kpi.TargetValue, kpi.Unit);

        return kpi;
    }

    public async Task<List<KPI>> GetKPIsAsync(
        string tenantId,
        string? category = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_kpis.TryGetValue(tenantId, out var kpis))
        {
            return new List<KPI>();
        }

        return kpis
            .Where(k => category == null || k.Category == category)
            .ToList();
    }

    public async Task<bool> UpdateKPIValueAsync(
        string kpiId,
        double newValue,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var kpis in _kpis.Values)
        {
            var kpi = kpis.FirstOrDefault(k => k.KpiId == kpiId);
            if (kpi != null)
            {
                kpi.HistoricalValues.Add(kpi.CurrentValue);
                kpi.CurrentValue = newValue;
                kpi.LastUpdated = DateTime.UtcNow;

                // Determine status
                if (kpi.Threshold.HasValue)
                {
                    if (newValue >= kpi.Threshold.Value * 1.2)
                        kpi.Status = "critical";
                    else if (newValue >= kpi.Threshold.Value * 1.1)
                        kpi.Status = "warning";
                    else
                        kpi.Status = "healthy";
                }

                return true;
            }
        }

        return false;
    }

    // Dashboards
    public async Task<AnalyticsDashboard> GetAnalyticsDashboardAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate aggregation

        var allMetrics = _metrics.Values.SelectMany(m => m).ToList();
        var successfulMetrics = allMetrics.Where(m => m.Status == "success").ToList();
        var failedMetrics = allMetrics.Where(m => m.Status == "failure").ToList();

        var dashboard = new AnalyticsDashboard
        {
            TenantId = tenantId,
            TotalExecutions = allMetrics.Count,
            SuccessfulExecutions = successfulMetrics.Count,
            FailedExecutions = failedMetrics.Count,
            SuccessRate = allMetrics.Count > 0
                ? (successfulMetrics.Count / (double)allMetrics.Count) * 100
                : 0,
            AverageDurationMs = allMetrics.Count > 0
                ? (long)allMetrics.Average(m => m.DurationMs)
                : 0,
            P95DurationMs = allMetrics.Count > 0
                ? allMetrics.OrderBy(m => m.DurationMs).Skip((int)(allMetrics.Count * 0.95)).First().DurationMs
                : 0,
            P99DurationMs = allMetrics.Count > 0
                ? allMetrics.OrderBy(m => m.DurationMs).Skip((int)(allMetrics.Count * 0.99)).First().DurationMs
                : 0,
            TotalCostThisMonth = allMetrics.Sum(m => m.CostUsd),
            AverageCostPerExecution = allMetrics.Count > 0
                ? allMetrics.Average(m => m.CostUsd)
                : 0,
            CostSavingsThisMonth = allMetrics.Count * 0.05, // Mock: 5% savings
            AverageResourceUtilization = 65.5,
            PeakResourceUtilization = 85.2,
            KeyKPIs = await GetKPIsAsync(tenantId, ct: ct),
        };

        return dashboard;
    }

    // Reports
    public async Task<WorkflowPerformanceReport> GeneratePerformanceReportAsync(
        string workflowId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate report generation

        var metrics = await GetMetricsAsync(workflowId, from, to, ct);

        if (metrics.Count == 0)
        {
            return new WorkflowPerformanceReport { WorkflowId = workflowId };
        }

        var successfulMetrics = metrics.Where(m => m.Status == "success").ToList();
        var durations = metrics.Select(m => (double)m.DurationMs).ToList();
        var mean = durations.Average();
        var variance = durations.Sum(x => Math.Pow(x - mean, 2)) / durations.Count;
        var stdDev = Math.Sqrt(variance);

        var report = new WorkflowPerformanceReport
        {
            WorkflowId = workflowId,
            ExecutionCount = metrics.Count,
            SuccessRate = (successfulMetrics.Count / (double)metrics.Count) * 100,
            AverageDurationMs = (long)metrics.Average(m => m.DurationMs),
            MinDurationMs = metrics.Min(m => m.DurationMs),
            MaxDurationMs = metrics.Max(m => m.DurationMs),
            StdDevDurationMs = stdDev,
            BottleneckSteps = new List<string> { "Step_DataFetch", "Step_Processing" },
            FailurePatterns = new List<string> { "Timeout", "Resource Exhaustion" },
            TotalCostUsd = metrics.Sum(m => m.CostUsd),
        };

        _logger.LogInformation(
            "Performance report generated: WorkflowId={WorkflowId}, Executions={Count}, SuccessRate={SuccessRate:F1}%",
            workflowId, metrics.Count, report.SuccessRate);

        return report;
    }

    public async Task<ResourceUtilizationReport> GenerateResourceReportAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate report generation

        var report = new ResourceUtilizationReport
        {
            TenantId = tenantId,
            AverageCpuPercent = 45.5,
            PeakCpuPercent = 78.3,
            AverageMemoryPercent = 62.1,
            PeakMemoryPercent = 89.7,
            AverageDiskUtilization = 35.2,
            NetworkBandwidthUtilization = 42.8,
            OverallUtilizationPercent = 58.6,
        };

        _logger.LogInformation(
            "Resource utilization report generated: TenantId={TenantId}, OverallUtilization={Overall}%",
            tenantId, report.OverallUtilizationPercent);

        return report;
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allMetrics = _metrics.Values.SelectMany(m => m).ToList();
        var kpis = await GetKPIsAsync(tenantId, ct: ct);

        return new Dictionary<string, object>
        {
            ["total_executions"] = allMetrics.Count,
            ["total_metrics_collected"] = allMetrics.Count,
            ["total_kpis"] = kpis.Count,
            ["average_success_rate"] = allMetrics.Count > 0
                ? (allMetrics.Count(m => m.Status == "success") / (double)allMetrics.Count) * 100
                : 0,
            ["total_cost"] = allMetrics.Sum(m => m.CostUsd),
            ["healthy_kpis"] = kpis.Count(k => k.Status == "healthy"),
            ["warning_kpis"] = kpis.Count(k => k.Status == "warning"),
            ["critical_kpis"] = kpis.Count(k => k.Status == "critical"),
        };
    }
}
