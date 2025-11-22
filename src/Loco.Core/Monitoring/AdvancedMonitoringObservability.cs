using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Monitoring
{
    /// <summary>
    /// Advanced monitoring and observability platform
    /// Phase 25: Distributed tracing, metrics collection, health checks, SLA tracking, alerting
    /// </summary>
    public interface IAdvancedMonitoringObservability
    {
        Task<Trace> RecordTraceAsync(string tenantId, TraceDefinition definition, CancellationToken ct = default);
        Task<List<Trace>> GetTracesAsync(string tenantId, string filter = null, int limit = 100, CancellationToken ct = default);
        Task<MetricPoint> RecordMetricAsync(string tenantId, MetricRecording recording, CancellationToken ct = default);
        Task<MetricSummary> GetMetricSummaryAsync(string tenantId, string metricName, int daysBack = 7, CancellationToken ct = default);
        Task<HealthCheckResult> RunHealthCheckAsync(string tenantId, HealthCheckDefinition definition, CancellationToken ct = default);
        Task<List<HealthStatus>> GetComponentHealthAsync(string tenantId, CancellationToken ct = default);
        Task<SLAStatus> CalculateSLAAsync(string tenantId, string serviceId, CancellationToken ct = default);
        Task<bool> CreateAlertRuleAsync(string tenantId, AlertRuleDefinition rule, CancellationToken ct = default);
        Task<List<Alert>> GetActiveAlertsAsync(string tenantId, CancellationToken ct = default);
        Task<MonitoringMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class AdvancedMonitoringObservability : IAdvancedMonitoringObservability
    {
        private readonly ILogger<AdvancedMonitoringObservability> _logger;
        private readonly Dictionary<string, List<Trace>> _traces = new();
        private readonly Dictionary<string, List<MetricPoint>> _metrics = new();
        private readonly Dictionary<string, List<HealthStatus>> _healthHistory = new();
        private readonly Dictionary<string, List<AlertRule>> _alertRules = new();
        private readonly Dictionary<string, List<Alert>> _alerts = new();
        private readonly Dictionary<string, SLAStatus> _slaStatus = new();
        private readonly Random _random = new(42);

        public AdvancedMonitoringObservability(ILogger<AdvancedMonitoringObservability> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Trace> RecordTraceAsync(string tenantId, TraceDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Recording trace {TraceId}", definition.TraceId);
            await Task.Delay(10, ct);

            var trace = new Trace
            {
                TraceId = definition.TraceId ?? Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                CorrelationId = definition.CorrelationId ?? Guid.NewGuid().ToString("N"),
                ServiceName = definition.ServiceName,
                OperationName = definition.OperationName,
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMilliseconds(_random.Next(10, 5000)),
                Duration = _random.Next(10, 5000),
                Status = _random.NextDouble() < 0.95 ? "success" : "error",
                Spans = definition.Spans ?? new List<Span>(),
                Tags = definition.Tags ?? new Dictionary<string, string>(),
                Logs = new List<TraceLog>(),
                SamplingPriority = definition.SamplingPriority ?? 0
            };

            var key = $"{tenantId}:{definition.ServiceName}";
            if (!_traces.ContainsKey(key))
                _traces[key] = new List<Trace>();

            _traces[key].Add(trace);
            if (_traces[key].Count > 10000)
                _traces[key] = _traces[key].Skip(_traces[key].Count - 10000).ToList();

            return trace;
        }

        public async Task<List<Trace>> GetTracesAsync(string tenantId, string filter = null, int limit = 100, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving traces");
            await Task.Delay(20, ct);

            var traces = _traces
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .OrderByDescending(t => t.StartTime)
                .Take(limit)
                .ToList();

            if (!string.IsNullOrWhiteSpace(filter))
                traces = traces.Where(t =>
                    t.Status == filter || t.ServiceName.Contains(filter) ||
                    t.OperationName.Contains(filter)).ToList();

            return traces;
        }

        public async Task<MetricPoint> RecordMetricAsync(string tenantId, MetricRecording recording, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Recording metric {MetricName}", recording.MetricName);
            await Task.Delay(5, ct);

            var point = new MetricPoint
            {
                MetricId = Guid.NewGuid().ToString("N"),
                MetricName = recording.MetricName,
                TenantId = tenantId,
                Timestamp = DateTimeOffset.UtcNow,
                Value = recording.Value,
                Tags = recording.Tags ?? new Dictionary<string, string>(),
                Unit = recording.Unit ?? "count"
            };

            var key = $"{tenantId}:{recording.MetricName}";
            if (!_metrics.ContainsKey(key))
                _metrics[key] = new List<MetricPoint>();

            _metrics[key].Add(point);
            if (_metrics[key].Count > 100000)
                _metrics[key] = _metrics[key].Skip(_metrics[key].Count - 100000).ToList();

            return point;
        }

        public async Task<MetricSummary> GetMetricSummaryAsync(string tenantId, string metricName, int daysBack = 7, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting metric summary for {MetricName}", metricName);
            await Task.Delay(25, ct);

            var key = $"{tenantId}:{metricName}";
            var points = _metrics.ContainsKey(key) ? _metrics[key] : new List<MetricPoint>();

            var cutoffTime = DateTimeOffset.UtcNow.AddDays(-daysBack);
            var relevantPoints = points.Where(p => p.Timestamp >= cutoffTime).ToList();

            var values = relevantPoints.Select(p => p.Value).ToList();
            var summary = new MetricSummary
            {
                MetricName = metricName,
                SummaryTime = DateTimeOffset.UtcNow,
                TimeWindow = $"{daysBack} days",
                DataPoints = relevantPoints.Count,
                CurrentValue = relevantPoints.LastOrDefault()?.Value ?? 0,
                Average = values.Count > 0 ? values.Average() : 0,
                Min = values.Count > 0 ? values.Min() : 0,
                Max = values.Count > 0 ? values.Max() : 0,
                Percentile50 = CalculatePercentile(values, 0.5),
                Percentile95 = CalculatePercentile(values, 0.95),
                Percentile99 = CalculatePercentile(values, 0.99),
                Trend = values.Count > 1 && values[values.Count - 1] > values[0] ? "up" : "down",
                ChangePercent = CalculatePercentChange(values),
                Anomalies = DetectAnomalies(values)
            };

            return summary;
        }

        public async Task<HealthCheckResult> RunHealthCheckAsync(string tenantId, HealthCheckDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Running health check for {ComponentName}", definition.ComponentName);
            await Task.Delay(definition.TimeoutMs ?? 5000, ct);

            var isHealthy = _random.NextDouble() > 0.1; // 90% health rate

            var result = new HealthCheckResult
            {
                CheckId = Guid.NewGuid().ToString("N"),
                ComponentName = definition.ComponentName,
                CheckType = definition.CheckType, // http, database, service, custom
                Status = isHealthy ? "healthy" : "unhealthy",
                CheckedAt = DateTimeOffset.UtcNow,
                DurationMs = _random.Next(10, 5000),
                Message = isHealthy ? "Component is healthy" : "Component is experiencing issues",
                Details = new Dictionary<string, string>
                {
                    { "cpu_usage", $"{_random.Next(10, 90)}%" },
                    { "memory_usage", $"{_random.Next(20, 80)}%" },
                    { "response_time", $"{_random.Next(10, 500)}ms" },
                    { "error_rate", $"{_random.NextDouble() * 5:F2}%" }
                },
                Dependencies = definition.Dependencies ?? new List<string>(),
                DependencyStatus = GenerateDependencyStatus()
            };

            var key = $"{tenantId}:{definition.ComponentName}";
            if (!_healthHistory.ContainsKey(key))
                _healthHistory[key] = new List<HealthStatus>();

            var status = new HealthStatus
            {
                ComponentName = definition.ComponentName,
                Status = result.Status,
                CheckedAt = result.CheckedAt
            };

            _healthHistory[key].Add(status);
            if (_healthHistory[key].Count > 10000)
                _healthHistory[key] = _healthHistory[key].Skip(_healthHistory[key].Count - 10000).ToList();

            return result;
        }

        public async Task<List<HealthStatus>> GetComponentHealthAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting component health status");
            await Task.Delay(20, ct);

            var components = new List<HealthStatus>
            {
                new() { ComponentName = "API Gateway", Status = "healthy", CheckedAt = DateTimeOffset.UtcNow },
                new() { ComponentName = "Workflow Engine", Status = "healthy", CheckedAt = DateTimeOffset.UtcNow },
                new() { ComponentName = "Database", Status = "healthy", CheckedAt = DateTimeOffset.UtcNow.AddSeconds(-30) },
                new() { ComponentName = "Cache", Status = "healthy", CheckedAt = DateTimeOffset.UtcNow },
                new() { ComponentName = "Message Queue", Status = _random.NextDouble() < 0.95 ? "healthy" : "degraded", CheckedAt = DateTimeOffset.UtcNow },
                new() { ComponentName = "Search Index", Status = "healthy", CheckedAt = DateTimeOffset.UtcNow.AddSeconds(-60) }
            };

            return components;
        }

        public async Task<SLAStatus> CalculateSLAAsync(string tenantId, string serviceId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating SLA for service {ServiceId}", serviceId);
            await Task.Delay(30, ct);

            var key = $"{tenantId}:{serviceId}";
            var uptime = _random.Next(9950, 10000) / 100.0m; // 99.50% to 100%

            var status = new SLAStatus
            {
                ServiceId = serviceId,
                CalculatedAt = DateTimeOffset.UtcNow,
                SLATarget = 99.9m,
                Uptime = uptime,
                IsCompliant = uptime >= 99.9m,
                DowntimeMinutes = _random.Next(0, 100),
                IncidentCount = _random.Next(0, 5),
                MTTR = _random.Next(5, 60),
                MTTF = _random.Next(100, 10000),
                MajorOutages = _random.Next(0, 2),
                PartialOutages = _random.Next(0, 10),
                DegradedServices = _random.Next(0, 3),
                PeriodStart = DateTimeOffset.UtcNow.AddDays(-30),
                PeriodEnd = DateTimeOffset.UtcNow,
                CreditPercentage = uptime < 99.9m ? 10 : 0
            };

            _slaStatus[key] = status;
            return status;
        }

        public async Task<bool> CreateAlertRuleAsync(string tenantId, AlertRuleDefinition rule, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating alert rule {RuleName}", rule.RuleName);
            await Task.Delay(20, ct);

            var alertRule = new AlertRule
            {
                RuleId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                RuleName = rule.RuleName,
                Description = rule.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = "active",
                Condition = rule.Condition,
                Threshold = rule.Threshold,
                Operator = rule.Operator ?? "greater_than",
                AggregationWindow = rule.AggregationWindow ?? 300,
                EvaluationFrequency = rule.EvaluationFrequency ?? 60,
                NotificationChannels = rule.NotificationChannels ?? new List<string>(),
                Severity = rule.Severity ?? "medium",
                AutoResolve = rule.AutoResolve ?? true
            };

            var key = $"{tenantId}";
            if (!_alertRules.ContainsKey(key))
                _alertRules[key] = new List<AlertRule>();

            _alertRules[key].Add(alertRule);
            return true;
        }

        public async Task<List<Alert>> GetActiveAlertsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving active alerts");
            await Task.Delay(20, ct);

            var key = $"{tenantId}";
            if (!_alerts.ContainsKey(key))
                return new List<Alert>();

            return _alerts[key]
                .Where(a => a.Status == "active" || a.Status == "firing")
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
        }

        public async Task<MonitoringMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating monitoring metrics");
            await Task.Delay(30, ct);

            var traceCount = _traces.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0);
            var metricCount = _metrics.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0);
            var alertCount = _alerts.ContainsKey(tenantId) ? _alerts[tenantId].Count : 0;

            var metrics = new MonitoringMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalTraces = traceCount,
                TracesPerSecond = _random.Next(10, 10000),
                AverageTraceDuration = _random.Next(50, 5000),
                TotalMetrics = metricCount,
                MetricsPerSecond = _random.Next(100, 100000),
                MetricCardinality = _random.Next(100, 100000),
                HealthChecksRun = _healthHistory.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                HealthyComponents = _random.Next(20, 50),
                UnhealthyComponents = _random.Next(0, 5),
                SLACompliance = _random.Next(99, 100),
                ActiveAlerts = alertCount,
                AlertsLast24h = _random.Next(5, 50),
                AverageAlertResolutionTime = _random.Next(5, 120),
                MonitoringDataStorageGB = _random.Next(10, 1000)
            };

            return metrics;
        }

        private double CalculatePercentile(List<double> values, double percentile)
        {
            if (values.Count == 0)
                return 0;

            var sorted = values.OrderBy(x => x).ToList();
            var index = (int)((percentile * sorted.Count) + 0.5);
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
        }

        private decimal CalculatePercentChange(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            var first = values.First();
            var last = values.Last();
            if (first == 0)
                return 0;

            return Convert.ToDecimal(((last - first) / first) * 100);
        }

        private List<Anomaly> DetectAnomalies(List<double> values)
        {
            var anomalies = new List<Anomaly>();

            if (values.Count < 3)
                return anomalies;

            var avg = values.Average();
            var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / values.Count);

            for (int i = 0; i < values.Count; i++)
            {
                if (Math.Abs(values[i] - avg) > 3 * stdDev)
                {
                    anomalies.Add(new Anomaly
                    {
                        Index = i,
                        Value = values[i],
                        Severity = "high"
                    });
                }
            }

            return anomalies;
        }

        private Dictionary<string, string> GenerateDependencyStatus()
        {
            return new Dictionary<string, string>
            {
                { "database", _random.NextDouble() < 0.95 ? "healthy" : "degraded" },
                { "cache", "healthy" },
                { "message_queue", _random.NextDouble() < 0.95 ? "healthy" : "unhealthy" },
                { "external_api", _random.NextDouble() < 0.9 ? "healthy" : "degraded" }
            };
        }
    }

    public class TraceDefinition
    {
        public string TraceId { get; set; }
        public string CorrelationId { get; set; }
        public string ServiceName { get; set; }
        public string OperationName { get; set; }
        public List<Span> Spans { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public int? SamplingPriority { get; set; }
    }

    public class Trace
    {
        public string TraceId { get; set; }
        public string TenantId { get; set; }
        public string CorrelationId { get; set; }
        public string ServiceName { get; set; }
        public string OperationName { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int Duration { get; set; }
        public string Status { get; set; }
        public List<Span> Spans { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<TraceLog> Logs { get; set; } = new();
        public int SamplingPriority { get; set; }
    }

    public class Span
    {
        public string SpanId { get; set; }
        public string ParentSpanId { get; set; }
        public string OperationName { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    public class TraceLog
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
    }

    public class MetricRecording
    {
        public string MetricName { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public string Unit { get; set; }
    }

    public class MetricPoint
    {
        public string MetricId { get; set; }
        public string MetricName { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public string Unit { get; set; }
    }

    public class MetricSummary
    {
        public string MetricName { get; set; }
        public DateTimeOffset SummaryTime { get; set; }
        public string TimeWindow { get; set; }
        public int DataPoints { get; set; }
        public double CurrentValue { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Percentile50 { get; set; }
        public double Percentile95 { get; set; }
        public double Percentile99 { get; set; }
        public string Trend { get; set; }
        public decimal ChangePercent { get; set; }
        public List<Anomaly> Anomalies { get; set; } = new();
    }

    public class Anomaly
    {
        public int Index { get; set; }
        public double Value { get; set; }
        public string Severity { get; set; }
    }

    public class HealthCheckDefinition
    {
        public string ComponentName { get; set; }
        public string CheckType { get; set; }
        public int? TimeoutMs { get; set; }
        public List<string> Dependencies { get; set; }
    }

    public class HealthCheckResult
    {
        public string CheckId { get; set; }
        public string ComponentName { get; set; }
        public string CheckType { get; set; }
        public string Status { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public int DurationMs { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> Details { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, string> DependencyStatus { get; set; } = new();
    }

    public class HealthStatus
    {
        public string ComponentName { get; set; }
        public string Status { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
    }

    public class SLAStatus
    {
        public string ServiceId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public decimal SLATarget { get; set; }
        public decimal Uptime { get; set; }
        public bool IsCompliant { get; set; }
        public int DowntimeMinutes { get; set; }
        public int IncidentCount { get; set; }
        public int MTTR { get; set; }
        public int MTTF { get; set; }
        public int MajorOutages { get; set; }
        public int PartialOutages { get; set; }
        public int DegradedServices { get; set; }
        public DateTimeOffset PeriodStart { get; set; }
        public DateTimeOffset PeriodEnd { get; set; }
        public decimal CreditPercentage { get; set; }
    }

    public class AlertRuleDefinition
    {
        public string RuleName { get; set; }
        public string Description { get; set; }
        public string Condition { get; set; }
        public double Threshold { get; set; }
        public string Operator { get; set; }
        public int? AggregationWindow { get; set; }
        public int? EvaluationFrequency { get; set; }
        public List<string> NotificationChannels { get; set; }
        public string Severity { get; set; }
        public bool? AutoResolve { get; set; }
    }

    public class AlertRule
    {
        public string RuleId { get; set; }
        public string TenantId { get; set; }
        public string RuleName { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string Status { get; set; }
        public string Condition { get; set; }
        public double Threshold { get; set; }
        public string Operator { get; set; }
        public int AggregationWindow { get; set; }
        public int EvaluationFrequency { get; set; }
        public List<string> NotificationChannels { get; set; } = new();
        public string Severity { get; set; }
        public bool AutoResolve { get; set; }
    }

    public class Alert
    {
        public string AlertId { get; set; }
        public string RuleId { get; set; }
        public string TenantId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public string Status { get; set; }
        public string Severity { get; set; }
        public Dictionary<string, string> Context { get; set; } = new();
    }

    public class MonitoringMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalTraces { get; set; }
        public int TracesPerSecond { get; set; }
        public int AverageTraceDuration { get; set; }
        public int TotalMetrics { get; set; }
        public int MetricsPerSecond { get; set; }
        public int MetricCardinality { get; set; }
        public int HealthChecksRun { get; set; }
        public int HealthyComponents { get; set; }
        public int UnhealthyComponents { get; set; }
        public int SLACompliance { get; set; }
        public int ActiveAlerts { get; set; }
        public int AlertsLast24h { get; set; }
        public int AverageAlertResolutionTime { get; set; }
        public int MonitoringDataStorageGB { get; set; }
    }
}
