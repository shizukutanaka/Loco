// Phase 7: Comprehensive Monitoring Dashboard
// Real-time metrics, alerting, and dashboarding for workflow operations
// Provides visibility into platform health and tenant operations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Monitoring;

/// <summary>
/// Dashboard metric
/// </summary>
public class DashboardMetric
{
    public string MetricId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // executions, performance, reliability
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    public double? Threshold { get; set; }
    public string? Status { get; set; } // healthy, warning, critical
}

/// <summary>
/// Monitoring alert
/// </summary>
public class MonitoringAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string AlertName { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium"; // low, medium, high, critical
    public bool IsActive { get; set; } = true;
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public List<string> NotificationChannels { get; set; } = new(); // email, slack, webhook
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public string ComponentName { get; set; } = string.Empty;
    public string Status { get; set; } = "healthy"; // healthy, degraded, unhealthy
    public string? Message { get; set; }
    public double? ResponseTimeMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tenant dashboard view
/// </summary>
public class TenantDashboardView
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // KPIs
    public int TotalWorkflows { get; set; }
    public int ActiveWorkflows { get; set; }
    public int ExecutionsToday { get; set; }
    public double SuccessRatePercent { get; set; }
    public long AverageDurationMs { get; set; }
    public long P95DurationMs { get; set; }
    public long P99DurationMs { get; set; }

    // Resource Usage
    public int CurrentConcurrentExecutions { get; set; }
    public double ExecutionQuotaUsedPercent { get; set; }
    public double StorageUsedGb { get; set; }
    public double StorageQuotaUsedPercent { get; set; }

    // Metrics
    public List<DashboardMetric> Metrics { get; set; } = new();
    public List<MonitoringAlert> ActiveAlerts { get; set; } = new();
    public List<HealthCheckResult> ComponentHealth { get; set; } = new();
}

/// <summary>
/// Monitoring dashboard interface
/// </summary>
public interface IMonitoringDashboard
{
    // Metrics Collection
    Task RecordMetricAsync(
        string tenantId,
        DashboardMetric metric,
        CancellationToken ct = default);

    Task<List<DashboardMetric>> GetMetricsAsync(
        string tenantId,
        string? category = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    // Alerting
    Task<MonitoringAlert> CreateAlertAsync(
        string tenantId,
        MonitoringAlert alert,
        CancellationToken ct = default);

    Task<List<MonitoringAlert>> GetAlertsAsync(
        string tenantId,
        bool activeOnly = true,
        CancellationToken ct = default);

    Task<bool> TriggerAlertAsync(
        string alertId,
        CancellationToken ct = default);

    Task<bool> ResolveAlertAsync(
        string alertId,
        CancellationToken ct = default);

    // Health Checks
    Task RecordHealthCheckAsync(
        HealthCheckResult result,
        CancellationToken ct = default);

    Task<List<HealthCheckResult>> GetHealthStatusAsync(
        CancellationToken ct = default);

    // Dashboard Views
    Task<TenantDashboardView> GetDashboardAsync(
        string tenantId,
        CancellationToken ct = default);

    // Historical Analysis
    Task<Dictionary<string, double>> GetMetricTrendsAsync(
        string tenantId,
        string metricName,
        int days = 7,
        CancellationToken ct = default);

    Task<Dictionary<string, int>> GetExecutionBreakdownAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

/// <summary>
/// Monitoring dashboard implementation
/// </summary>
public class MonitoringDashboard : IMonitoringDashboard
{
    private readonly ILogger<MonitoringDashboard> _logger;
    private readonly Dictionary<string, List<DashboardMetric>> _metrics;
    private readonly Dictionary<string, MonitoringAlert> _alerts;
    private readonly Dictionary<string, HealthCheckResult> _healthChecks;

    public MonitoringDashboard(ILogger<MonitoringDashboard> logger)
    {
        _logger = logger;
        _metrics = new Dictionary<string, List<DashboardMetric>>();
        _alerts = new Dictionary<string, MonitoringAlert>();
        _healthChecks = new Dictionary<string, HealthCheckResult>();
    }

    public async Task RecordMetricAsync(
        string tenantId,
        DashboardMetric metric,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_metrics.ContainsKey(tenantId))
        {
            _metrics[tenantId] = new List<DashboardMetric>();
        }

        _metrics[tenantId].Add(metric);

        // Trigger alerts if threshold exceeded
        if (metric.Threshold.HasValue && metric.Value > metric.Threshold.Value)
        {
            _logger.LogWarning(
                "Metric threshold exceeded: {TenantId}, {MetricName}, Value: {Value}, Threshold: {Threshold}",
                tenantId, metric.Name, metric.Value, metric.Threshold);
        }
    }

    public async Task<List<DashboardMetric>> GetMetricsAsync(
        string tenantId,
        string? category = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_metrics.TryGetValue(tenantId, out var metrics))
        {
            return new List<DashboardMetric>();
        }

        var results = metrics
            .Where(m => category == null || m.Category == category)
            .Where(m => from == null || m.MeasuredAt >= from)
            .Where(m => to == null || m.MeasuredAt <= to)
            .OrderByDescending(m => m.MeasuredAt)
            .ToList();

        return results;
    }

    public async Task<MonitoringAlert> CreateAlertAsync(
        string tenantId,
        MonitoringAlert alert,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        alert.TenantId = tenantId;
        _alerts[alert.AlertId] = alert;

        _logger.LogInformation(
            "Alert created: {AlertId}, Tenant: {TenantId}, Name: {AlertName}",
            alert.AlertId, tenantId, alert.AlertName);

        return alert;
    }

    public async Task<List<MonitoringAlert>> GetAlertsAsync(
        string tenantId,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _alerts.Values
            .Where(a => a.TenantId == tenantId)
            .Where(a => !activeOnly || a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
    }

    public async Task<bool> TriggerAlertAsync(
        string alertId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_alerts.TryGetValue(alertId, out var alert))
        {
            return false;
        }

        alert.IsTriggered = true;
        alert.TriggeredAt = DateTime.UtcNow;

        _logger.LogWarning(
            "Alert triggered: {AlertId}, Alert: {AlertName}",
            alertId, alert.AlertName);

        // Send notifications
        _ = SendAlertNotificationsAsync(alert);

        return true;
    }

    public async Task<bool> ResolveAlertAsync(
        string alertId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_alerts.TryGetValue(alertId, out var alert))
        {
            return false;
        }

        alert.IsTriggered = false;

        _logger.LogInformation(
            "Alert resolved: {AlertId}",
            alertId);

        return true;
    }

    public async Task RecordHealthCheckAsync(
        HealthCheckResult result,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _healthChecks[result.ComponentName] = result;

        if (result.Status != "healthy")
        {
            _logger.LogWarning(
                "Health check failed: {ComponentName}, Status: {Status}",
                result.ComponentName, result.Status);
        }
    }

    public async Task<List<HealthCheckResult>> GetHealthStatusAsync(
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _healthChecks.Values.OrderBy(h => h.CheckedAt).ToList();
    }

    public async Task<TenantDashboardView> GetDashboardAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate aggregation

        var metrics = await GetMetricsAsync(tenantId, ct: ct);
        var alerts = await GetAlertsAsync(tenantId, activeOnly: true, ct: ct);

        var dashboard = new TenantDashboardView
        {
            TenantId = tenantId,
            GeneratedAt = DateTime.UtcNow,
            ExecutionsToday = metrics
                .Where(m => m.Category == "executions")
                .Sum(m => (int)m.Value),
            SuccessRatePercent = metrics
                .Where(m => m.Name == "success_rate")
                .FirstOrDefault()?.Value ?? 0,
            AverageDurationMs = (long)(metrics
                .Where(m => m.Name == "avg_duration_ms")
                .FirstOrDefault()?.Value ?? 0),
            Metrics = metrics.Take(10).ToList(),
            ActiveAlerts = alerts,
            ComponentHealth = _healthChecks.Values.ToList(),
        };

        return dashboard;
    }

    public async Task<Dictionary<string, double>> GetMetricTrendsAsync(
        string tenantId,
        string metricName,
        int days = 7,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var metrics = await GetMetricsAsync(
            tenantId,
            from: DateTime.UtcNow.AddDays(-days),
            ct: ct);

        var trends = metrics
            .Where(m => m.Name == metricName)
            .GroupBy(m => m.MeasuredAt.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Average(m => m.Value));

        return trends;
    }

    public async Task<Dictionary<string, int>> GetExecutionBreakdownAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var metrics = await GetMetricsAsync(tenantId, from: from, to: to, ct: ct);

        return new Dictionary<string, int>
        {
            ["total_executions"] = (int)metrics
                .Where(m => m.Category == "executions")
                .Sum(m => m.Value),
            ["successful"] = (int)metrics
                .Where(m => m.Name == "successful_executions")
                .Sum(m => m.Value),
            ["failed"] = (int)metrics
                .Where(m => m.Name == "failed_executions")
                .Sum(m => m.Value),
        };
    }

    private async Task SendAlertNotificationsAsync(MonitoringAlert alert)
    {
        foreach (var channel in alert.NotificationChannels)
        {
            _logger.LogInformation(
                "Sending alert notification via {Channel}: {AlertName}",
                channel, alert.AlertName);

            // Simulate notification sending
            await Task.Delay(100);
        }
    }
}
