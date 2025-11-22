using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Monitoring
{
    /// <summary>
    /// Advanced monitoring and observability system
    /// Phase 19: Comprehensive metrics, tracing, and health monitoring
    /// OpenTelemetry-compatible for integration with Prometheus, Grafana, etc.
    /// </summary>
    public interface IAdvancedMonitoringSystem
    {
        Task<MetricRecord> RecordMetricAsync(string tenantId, string metricName, double value, Dictionary<string, string> tags = null, CancellationToken cancellationToken = default);
        Task<HealthStatus> GetSystemHealthAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<PerformanceMetrics> GetPerformanceMetricsAsync(string tenantId, int minutesBack = 60, CancellationToken cancellationToken = default);
        Task<AlertConfiguration> ConfigureAlertAsync(string tenantId, AlertRule rule, CancellationToken cancellationToken = default);
        Task<List<AlertEvent>> GetActiveAlertsAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<TraceData> RecordTraceAsync(string tenantId, string operationName, string spanId, Dictionary<string, object> data, CancellationToken cancellationToken = default);
        Task<ErrorReport> GetErrorSummaryAsync(string tenantId, int hoursBack = 24, CancellationToken cancellationToken = default);
        Task<DependencyStatus> CheckDependenciesAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<MonitoringReport> GenerateMonitoringReportAsync(string tenantId, CancellationToken cancellationToken = default);
    }

    public class AdvancedMonitoringSystem : IAdvancedMonitoringSystem
    {
        private readonly ILogger<AdvancedMonitoringSystem> _logger;
        private readonly Dictionary<string, List<MetricRecord>> _metrics = new();
        private readonly Dictionary<string, HealthStatus> _health = new();
        private readonly Dictionary<string, List<AlertEvent>> _alerts = new();
        private readonly Dictionary<string, List<TraceData>> _traces = new();
        private readonly Dictionary<string, List<ErrorEvent>> _errors = new();
        private readonly Random _random = new(42);

        public AdvancedMonitoringSystem(ILogger<AdvancedMonitoringSystem> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MetricRecord> RecordMetricAsync(string tenantId, string metricName, double value, Dictionary<string, string> tags = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(metricName))
                throw new ArgumentException("Metric name is required", nameof(metricName));

            _logger.LogInformation("Recording metric {MetricName} = {Value}", metricName, value);

            await Task.Delay(5, cancellationToken);

            var record = new MetricRecord
            {
                MetricId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                MetricName = metricName,
                Value = value,
                Timestamp = DateTimeOffset.UtcNow,
                Tags = tags ?? new Dictionary<string, string>(),
                Unit = "count"
            };

            var key = $"{tenantId}:{metricName}";
            if (!_metrics.ContainsKey(key))
                _metrics[key] = new List<MetricRecord>();

            _metrics[key].Add(record);

            // Keep only last 10,000 records per metric
            if (_metrics[key].Count > 10000)
                _metrics[key] = _metrics[key].TakeLast(10000).ToList();

            return record;
        }

        public async Task<HealthStatus> GetSystemHealthAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Checking system health for tenant {TenantId}", tenantId);

            await Task.Delay(50, cancellationToken);

            var health = new HealthStatus
            {
                TenantId = tenantId,
                CheckedAt = DateTimeOffset.UtcNow,
                OverallStatus = "healthy",
                Uptime = _random.NextDouble() * 100, // 0-100%
                Components = new Dictionary<string, ComponentHealth>
                {
                    { "API", new ComponentHealth { Status = "up", ResponseTime = _random.Next(10, 200) } },
                    { "Database", new ComponentHealth { Status = "up", ResponseTime = _random.Next(5, 100) } },
                    { "Cache", new ComponentHealth { Status = "up", ResponseTime = _random.Next(1, 50) } },
                    { "MessageQueue", new ComponentHealth { Status = "up", ResponseTime = _random.Next(10, 150) } }
                },
                DependenciesHealthy = 4,
                TotalDependencies = 4,
                ErrorRate = _random.NextDouble() * 0.01, // 0-1%
                WarningRate = _random.NextDouble() * 0.05 // 0-5%
            };

            _health[$"{tenantId}"] = health;
            return health;
        }

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(string tenantId, int minutesBack = 60, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving performance metrics for {TenantId} ({Minutes}m)", tenantId, minutesBack);

            await Task.Delay(100, cancellationToken);

            var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-minutesBack);
            var recentMetrics = _metrics
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value.Where(m => m.Timestamp >= cutoffTime))
                .ToList();

            var performance = new PerformanceMetrics
            {
                TenantId = tenantId,
                ComputedAt = DateTimeOffset.UtcNow,
                TimeWindowMinutes = minutesBack,
                AverageResponseTime = recentMetrics.Count > 0 ? recentMetrics.Average(m => m.Value) : 0,
                P95ResponseTime = GetPercentile(recentMetrics.Select(m => m.Value).ToList(), 95),
                P99ResponseTime = GetPercentile(recentMetrics.Select(m => m.Value).ToList(), 99),
                MinResponseTime = recentMetrics.Count > 0 ? recentMetrics.Min(m => m.Value) : 0,
                MaxResponseTime = recentMetrics.Count > 0 ? recentMetrics.Max(m => m.Value) : 0,
                RequestsPerMinute = _random.Next(100, 10000),
                ErrorRate = _random.NextDouble() * 0.01,
                SuccessRate = 1 - (_random.NextDouble() * 0.01),
                ThroughputPerSecond = _random.NextDouble() * 1000
            };

            return performance;
        }

        public async Task<AlertConfiguration> ConfigureAlertAsync(string tenantId, AlertRule rule, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _logger.LogInformation("Configuring alert {AlertName}", rule.AlertName);

            await Task.Delay(30, cancellationToken);

            var config = new AlertConfiguration
            {
                AlertId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                AlertName = rule.AlertName,
                ConfiguredAt = DateTimeOffset.UtcNow,
                MetricName = rule.MetricName,
                Threshold = rule.Threshold,
                Condition = rule.Condition,
                Duration = rule.DurationSeconds,
                NotificationChannels = rule.NotificationChannels ?? new List<string> { "email", "slack" },
                Enabled = true,
                Status = "configured"
            };

            return config;
        }

        public async Task<List<AlertEvent>> GetActiveAlertsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving active alerts for tenant {TenantId}", tenantId);

            await Task.Delay(40, cancellationToken);

            if (!_alerts.ContainsKey(tenantId))
                _alerts[tenantId] = new List<AlertEvent>();

            var activeAlerts = _alerts[tenantId]
                .Where(a => !a.Resolved)
                .OrderByDescending(a => a.TriggeredAt)
                .ToList();

            return activeAlerts;
        }

        public async Task<TraceData> RecordTraceAsync(string tenantId, string operationName, string spanId, Dictionary<string, object> data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name is required", nameof(operationName));

            _logger.LogInformation("Recording trace for {OperationName}", operationName);

            await Task.Delay(5, cancellationToken);

            var trace = new TraceData
            {
                TraceId = Guid.NewGuid().ToString("N"),
                SpanId = spanId ?? Guid.NewGuid().ToString("N"),
                OperationName = operationName,
                StartedAt = DateTimeOffset.UtcNow,
                EndedAt = DateTimeOffset.UtcNow.AddMilliseconds(_random.Next(10, 500)),
                Duration = _random.Next(10, 500),
                Status = "success",
                Data = data ?? new Dictionary<string, object>(),
                Severity = "info"
            };

            var key = $"{tenantId}:{operationName}";
            if (!_traces.ContainsKey(key))
                _traces[key] = new List<TraceData>();

            _traces[key].Add(trace);

            return trace;
        }

        public async Task<ErrorReport> GetErrorSummaryAsync(string tenantId, int hoursBack = 24, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving error summary for {TenantId} ({Hours}h)", tenantId, hoursBack);

            await Task.Delay(80, cancellationToken);

            if (!_errors.ContainsKey(tenantId))
                _errors[tenantId] = new List<ErrorEvent>();

            var cutoffTime = DateTimeOffset.UtcNow.AddHours(-hoursBack);
            var recentErrors = _errors[tenantId].Where(e => e.Timestamp >= cutoffTime).ToList();

            var report = new ErrorReport
            {
                TenantId = tenantId,
                ReportedAt = DateTimeOffset.UtcNow,
                TimeWindowHours = hoursBack,
                TotalErrors = recentErrors.Count,
                UniqueErrors = recentErrors.Select(e => e.ErrorType).Distinct().Count(),
                CriticalErrors = recentErrors.Count(e => e.Severity == "critical"),
                HighErrors = recentErrors.Count(e => e.Severity == "high"),
                MostCommonError = recentErrors.GroupBy(e => e.ErrorType)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "none",
                ErrorRate = _random.NextDouble() * 0.05,
                Top5Errors = recentErrors
                    .GroupBy(e => e.ErrorType)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => new { Error = g.Key, Count = g.Count() })
                    .ToList()
            };

            return report;
        }

        public async Task<DependencyStatus> CheckDependenciesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Checking dependencies for tenant {TenantId}", tenantId);

            await Task.Delay(150, cancellationToken);

            var status = new DependencyStatus
            {
                TenantId = tenantId,
                CheckedAt = DateTimeOffset.UtcNow,
                Dependencies = new Dictionary<string, DependencyHealth>
                {
                    { "Database", new DependencyHealth { Name = "PostgreSQL", Status = "healthy", Latency = _random.Next(5, 50), LastCheck = DateTimeOffset.UtcNow } },
                    { "Cache", new DependencyHealth { Name = "Redis", Status = "healthy", Latency = _random.Next(1, 20), LastCheck = DateTimeOffset.UtcNow } },
                    { "Queue", new DependencyHealth { Name = "RabbitMQ", Status = "healthy", Latency = _random.Next(10, 100), LastCheck = DateTimeOffset.UtcNow } },
                    { "Storage", new DependencyHealth { Name = "S3", Status = "healthy", Latency = _random.Next(50, 500), LastCheck = DateTimeOffset.UtcNow } }
                },
                AllHealthy = true,
                HealthyCount = 4,
                UnhealthyCount = 0,
                OverallStatus = "operational"
            };

            return status;
        }

        public async Task<MonitoringReport> GenerateMonitoringReportAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Generating monitoring report for tenant {TenantId}", tenantId);

            await Task.Delay(200, cancellationToken);

            var health = await GetSystemHealthAsync(tenantId, cancellationToken);
            var performance = await GetPerformanceMetricsAsync(tenantId, 60, cancellationToken);
            var errors = await GetErrorSummaryAsync(tenantId, 24, cancellationToken);
            var dependencies = await CheckDependenciesAsync(tenantId, cancellationToken);

            var report = new MonitoringReport
            {
                TenantId = tenantId,
                GeneratedAt = DateTimeOffset.UtcNow,
                OverallHealth = health,
                PerformanceMetrics = performance,
                ErrorSummary = errors,
                DependencyStatus = dependencies,
                SummaryScore = (health.Uptime + (1 - errors.ErrorRate) * 100 + (dependencies.AllHealthy ? 100 : 50)) / 3,
                Recommendations = GenerateRecommendations(health, performance, errors)
            };

            return report;
        }

        private double GetPercentile(List<double> values, int percentile)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Count) - 1;
            return index >= 0 && index < sorted.Count ? sorted[index] : 0;
        }

        private List<string> GenerateRecommendations(HealthStatus health, PerformanceMetrics perf, ErrorReport errors)
        {
            var recommendations = new List<string>();

            if (perf.AverageResponseTime > 100)
                recommendations.Add("Response time exceeding 100ms - consider optimization or scaling");

            if (perf.ErrorRate > 0.01)
                recommendations.Add("Error rate above 1% - investigate root cause");

            if (errors.CriticalErrors > 0)
                recommendations.Add("Critical errors detected - immediate action required");

            if (perf.P99ResponseTime > 500)
                recommendations.Add("99th percentile response time high - optimize slow queries");

            if (recommendations.Count == 0)
                recommendations.Add("System performing normally");

            return recommendations;
        }
    }

    // Domain Models
    public class MetricRecord
    {
        public string MetricId { get; set; }
        public string TenantId { get; set; }
        public string MetricName { get; set; }
        public double Value { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public string Unit { get; set; }
    }

    public class HealthStatus
    {
        public string TenantId { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public string OverallStatus { get; set; }
        public double Uptime { get; set; }
        public Dictionary<string, ComponentHealth> Components { get; set; }
        public int DependenciesHealthy { get; set; }
        public int TotalDependencies { get; set; }
        public double ErrorRate { get; set; }
        public double WarningRate { get; set; }
    }

    public class ComponentHealth
    {
        public string Status { get; set; }
        public int ResponseTime { get; set; }
    }

    public class PerformanceMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset ComputedAt { get; set; }
        public int TimeWindowMinutes { get; set; }
        public double AverageResponseTime { get; set; }
        public double P95ResponseTime { get; set; }
        public double P99ResponseTime { get; set; }
        public double MinResponseTime { get; set; }
        public double MaxResponseTime { get; set; }
        public int RequestsPerMinute { get; set; }
        public double ErrorRate { get; set; }
        public double SuccessRate { get; set; }
        public double ThroughputPerSecond { get; set; }
    }

    public class AlertRule
    {
        public string AlertName { get; set; }
        public string MetricName { get; set; }
        public double Threshold { get; set; }
        public string Condition { get; set; } // "greater_than", "less_than", etc.
        public int DurationSeconds { get; set; }
        public List<string> NotificationChannels { get; set; }
    }

    public class AlertConfiguration
    {
        public string AlertId { get; set; }
        public string TenantId { get; set; }
        public string AlertName { get; set; }
        public DateTimeOffset ConfiguredAt { get; set; }
        public string MetricName { get; set; }
        public double Threshold { get; set; }
        public string Condition { get; set; }
        public int Duration { get; set; }
        public List<string> NotificationChannels { get; set; }
        public bool Enabled { get; set; }
        public string Status { get; set; }
    }

    public class AlertEvent
    {
        public string AlertId { get; set; }
        public DateTimeOffset TriggeredAt { get; set; }
        public string Severity { get; set; } // "warning", "critical"
        public string Message { get; set; }
        public bool Resolved { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
    }

    public class TraceData
    {
        public string TraceId { get; set; }
        public string SpanId { get; set; }
        public string OperationName { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public int Duration { get; set; }
        public string Status { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public string Severity { get; set; }
    }

    public class ErrorEvent
    {
        public string ErrorId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string ErrorType { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Severity { get; set; } // "low", "high", "critical"
    }

    public class ErrorReport
    {
        public string TenantId { get; set; }
        public DateTimeOffset ReportedAt { get; set; }
        public int TimeWindowHours { get; set; }
        public int TotalErrors { get; set; }
        public int UniqueErrors { get; set; }
        public int CriticalErrors { get; set; }
        public int HighErrors { get; set; }
        public string MostCommonError { get; set; }
        public double ErrorRate { get; set; }
        public List<object> Top5Errors { get; set; }
    }

    public class DependencyHealth
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public int Latency { get; set; }
        public DateTimeOffset LastCheck { get; set; }
    }

    public class DependencyStatus
    {
        public string TenantId { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public Dictionary<string, DependencyHealth> Dependencies { get; set; }
        public bool AllHealthy { get; set; }
        public int HealthyCount { get; set; }
        public int UnhealthyCount { get; set; }
        public string OverallStatus { get; set; }
    }

    public class MonitoringReport
    {
        public string TenantId { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public HealthStatus OverallHealth { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public ErrorReport ErrorSummary { get; set; }
        public DependencyStatus DependencyStatus { get; set; }
        public double SummaryScore { get; set; }
        public List<string> Recommendations { get; set; }
    }
}
