using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Monitoring
{
    /// <summary>
    /// Comprehensive monitoring and alerting system
    /// High-performance design following John Carmack's principles
    /// </summary>
    public interface IMonitoringService
    {
        Task<MetricValue> RecordMetricAsync(string name, double value, Dictionary<string, string> tags = null);
        Task<Alert> CheckThresholdAsync(string metricName, double value);
        Task<List<MetricValue>> GetMetricsAsync(string name, DateTime from, DateTime to);
        Task<HealthCheckResult> PerformHealthCheckAsync();
        Task<bool> RegisterAlertRuleAsync(AlertRule rule);
        Task<List<Alert>> GetActiveAlertsAsync();
        Task<bool> AcknowledgeAlertAsync(string alertId);
        event EventHandler<AlertEventArgs> AlertTriggered;
        Task<MonitoringDashboard> GetDashboardAsync();
    }

    public class MonitoringService : IMonitoringService, IDisposable
    {
        private readonly ILogger<MonitoringService> _logger;
        private readonly ConcurrentDictionary<string, MetricBuffer> _metrics;
        private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
        private readonly ConcurrentDictionary<string, Alert> _activeAlerts;
        private readonly Timer _aggregationTimer;
        private readonly Timer _healthCheckTimer;
        private readonly object _lock = new object();

        public event EventHandler<AlertEventArgs> AlertTriggered;

        public MonitoringService(ILogger<MonitoringService> logger)
        {
            _logger = logger;
            _metrics = new ConcurrentDictionary<string, MetricBuffer>();
            _alertRules = new ConcurrentDictionary<string, AlertRule>();
            _activeAlerts = new ConcurrentDictionary<string, Alert>();

            // Aggregate metrics every minute
            _aggregationTimer = new Timer(
                AggregateMetrics,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1));

            // Health check every 30 seconds
            _healthCheckTimer = new Timer(
                async _ => await PerformHealthCheckAsync(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            InitializeDefaultAlertRules();
        }

        public async Task<MetricValue> RecordMetricAsync(string name, double value, Dictionary<string, string> tags = null)
        {
            var metric = new MetricValue
            {
                Name = name,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Tags = tags ?? new Dictionary<string, string>()
            };

            var buffer = _metrics.GetOrAdd(name, _ => new MetricBuffer(name));
            buffer.Add(metric);

            // Check alert rules
            await CheckThresholdAsync(name, value);

            return metric;
        }

        public async Task<Alert> CheckThresholdAsync(string metricName, double value)
        {
            if (!_alertRules.TryGetValue(metricName, out var rule))
            {
                return null;
            }

            bool shouldAlert = false;
            string reason = null;

            switch (rule.Condition)
            {
                case AlertCondition.GreaterThan:
                    shouldAlert = value > rule.Threshold;
                    reason = $"{metricName} ({value:F2}) exceeded threshold ({rule.Threshold:F2})";
                    break;

                case AlertCondition.LessThan:
                    shouldAlert = value < rule.Threshold;
                    reason = $"{metricName} ({value:F2}) below threshold ({rule.Threshold:F2})";
                    break;

                case AlertCondition.Equals:
                    shouldAlert = Math.Abs(value - rule.Threshold) < 0.001;
                    reason = $"{metricName} ({value:F2}) equals threshold ({rule.Threshold:F2})";
                    break;

                case AlertCondition.NotEquals:
                    shouldAlert = Math.Abs(value - rule.Threshold) >= 0.001;
                    reason = $"{metricName} ({value:F2}) not equals threshold ({rule.Threshold:F2})";
                    break;
            }

            if (shouldAlert)
            {
                var alert = new Alert
                {
                    Id = Guid.NewGuid().ToString(),
                    RuleId = rule.Id,
                    MetricName = metricName,
                    Value = value,
                    Threshold = rule.Threshold,
                    Severity = rule.Severity,
                    Message = reason,
                    TriggeredAt = DateTime.UtcNow,
                    Status = AlertStatus.Active
                };

                _activeAlerts[alert.Id] = alert;

                OnAlertTriggered(new AlertEventArgs
                {
                    Alert = alert,
                    Rule = rule
                });

                _logger.LogWarning("Alert triggered: {AlertMessage}", alert.Message);

                return alert;
            }

            return null;
        }

        public async Task<List<MetricValue>> GetMetricsAsync(string name, DateTime from, DateTime to)
        {
            if (!_metrics.TryGetValue(name, out var buffer))
            {
                return new List<MetricValue>();
            }

            var metrics = buffer.GetMetrics(from, to);
            return await Task.FromResult(metrics.ToList());
        }

        public async Task<HealthCheckResult> PerformHealthCheckAsync()
        {
            var result = new HealthCheckResult
            {
                Timestamp = DateTime.UtcNow,
                Checks = new List<HealthCheck>()
            };

            // CPU usage check
            var cpuCheck = await CheckCpuUsageAsync();
            result.Checks.Add(cpuCheck);

            // Memory usage check
            var memoryCheck = await CheckMemoryUsageAsync();
            result.Checks.Add(memoryCheck);

            // Disk space check
            var diskCheck = await CheckDiskSpaceAsync();
            result.Checks.Add(diskCheck);

            // Database connectivity check
            var dbCheck = await CheckDatabaseAsync();
            result.Checks.Add(dbCheck);

            // Custom health checks
            foreach (var customCheck in GetCustomHealthChecks())
            {
                result.Checks.Add(await customCheck());
            }

            // Determine overall status
            if (result.Checks.Any(c => c.Status == HealthStatus.Critical))
            {
                result.Status = HealthStatus.Critical;
            }
            else if (result.Checks.Any(c => c.Status == HealthStatus.Warning))
            {
                result.Status = HealthStatus.Warning;
            }
            else
            {
                result.Status = HealthStatus.Healthy;
            }

            // Record health metrics
            await RecordMetricAsync("health.status", (double)result.Status);

            return result;
        }

        public async Task<bool> RegisterAlertRuleAsync(AlertRule rule)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Id))
            {
                return false;
            }

            _alertRules[rule.MetricName] = rule;
            
            _logger.LogInformation("Registered alert rule: {RuleId} for metric {MetricName}",
                rule.Id, rule.MetricName);

            return await Task.FromResult(true);
        }

        public async Task<List<Alert>> GetActiveAlertsAsync()
        {
            var activeAlerts = _activeAlerts.Values
                .Where(a => a.Status == AlertStatus.Active)
                .OrderByDescending(a => a.Severity)
                .ThenByDescending(a => a.TriggeredAt)
                .ToList();

            return await Task.FromResult(activeAlerts);
        }

        public async Task<bool> AcknowledgeAlertAsync(string alertId)
        {
            if (_activeAlerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = AlertStatus.Acknowledged;
                alert.AcknowledgedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Alert acknowledged: {AlertId}", alertId);
                
                return await Task.FromResult(true);
            }

            return false;
        }

        public async Task<MonitoringDashboard> GetDashboardAsync()
        {
            var dashboard = new MonitoringDashboard
            {
                Timestamp = DateTime.UtcNow
            };

            // System metrics
            dashboard.SystemMetrics = new SystemMetrics
            {
                CpuUsage = await GetCurrentCpuUsageAsync(),
                MemoryUsage = await GetCurrentMemoryUsageAsync(),
                DiskUsage = await GetCurrentDiskUsageAsync(),
                NetworkLatency = await GetNetworkLatencyAsync()
            };

            // Application metrics
            dashboard.ApplicationMetrics = new ApplicationMetrics
            {
                ActiveRules = await GetMetricValueAsync("app.rules.active"),
                ExecutedRules = await GetMetricValueAsync("app.rules.executed"),
                FailedRules = await GetMetricValueAsync("app.rules.failed"),
                AverageExecutionTime = await GetMetricValueAsync("app.execution.avg_time"),
                TotalExecutions = await GetMetricValueAsync("app.execution.total")
            };

            // Active alerts
            dashboard.ActiveAlerts = await GetActiveAlertsAsync();

            // Recent events
            dashboard.RecentEvents = await GetRecentEventsAsync(10);

            return dashboard;
        }

        private void InitializeDefaultAlertRules()
        {
            // CPU usage alert
            RegisterAlertRuleAsync(new AlertRule
            {
                Id = "cpu-high",
                Name = "High CPU Usage",
                MetricName = "system.cpu.usage",
                Condition = AlertCondition.GreaterThan,
                Threshold = 80,
                Severity = AlertSeverity.Warning,
                Description = "CPU usage exceeds 80%"
            });

            // Memory usage alert
            RegisterAlertRuleAsync(new AlertRule
            {
                Id = "memory-high",
                Name = "High Memory Usage",
                MetricName = "system.memory.usage",
                Condition = AlertCondition.GreaterThan,
                Threshold = 90,
                Severity = AlertSeverity.Critical,
                Description = "Memory usage exceeds 90%"
            });

            // Disk space alert
            RegisterAlertRuleAsync(new AlertRule
            {
                Id = "disk-low",
                Name = "Low Disk Space",
                MetricName = "system.disk.free",
                Condition = AlertCondition.LessThan,
                Threshold = 1073741824, // 1GB
                Severity = AlertSeverity.Warning,
                Description = "Disk space below 1GB"
            });

            // Rule execution failure alert
            RegisterAlertRuleAsync(new AlertRule
            {
                Id = "rule-failures",
                Name = "High Rule Failure Rate",
                MetricName = "app.rules.failure_rate",
                Condition = AlertCondition.GreaterThan,
                Threshold = 10,
                Severity = AlertSeverity.Warning,
                Description = "Rule failure rate exceeds 10%"
            });
        }

        private async Task<HealthCheck> CheckCpuUsageAsync()
        {
            var cpuUsage = await GetCurrentCpuUsageAsync();
            
            return new HealthCheck
            {
                Name = "CPU Usage",
                Status = cpuUsage > 90 ? HealthStatus.Critical :
                         cpuUsage > 70 ? HealthStatus.Warning :
                         HealthStatus.Healthy,
                Value = cpuUsage,
                Unit = "%",
                Message = $"CPU usage: {cpuUsage:F1}%"
            };
        }

        private async Task<HealthCheck> CheckMemoryUsageAsync()
        {
            var memoryUsage = await GetCurrentMemoryUsageAsync();
            
            return new HealthCheck
            {
                Name = "Memory Usage",
                Status = memoryUsage > 90 ? HealthStatus.Critical :
                         memoryUsage > 80 ? HealthStatus.Warning :
                         HealthStatus.Healthy,
                Value = memoryUsage,
                Unit = "%",
                Message = $"Memory usage: {memoryUsage:F1}%"
            };
        }

        private async Task<HealthCheck> CheckDiskSpaceAsync()
        {
            var freeSpace = await GetCurrentDiskUsageAsync();
            var freeGB = freeSpace / (1024 * 1024 * 1024);
            
            return new HealthCheck
            {
                Name = "Disk Space",
                Status = freeGB < 1 ? HealthStatus.Critical :
                         freeGB < 5 ? HealthStatus.Warning :
                         HealthStatus.Healthy,
                Value = freeGB,
                Unit = "GB",
                Message = $"Free disk space: {freeGB:F1} GB"
            };
        }

        private async Task<HealthCheck> CheckDatabaseAsync()
        {
            // Simplified database check
            var isConnected = true; // Would actually test connection
            
            return await Task.FromResult(new HealthCheck
            {
                Name = "Database",
                Status = isConnected ? HealthStatus.Healthy : HealthStatus.Critical,
                Message = isConnected ? "Database connected" : "Database connection failed"
            });
        }

        private List<Func<Task<HealthCheck>>> GetCustomHealthChecks()
        {
            // Custom health checks can be registered here
            return new List<Func<Task<HealthCheck>>>();
        }

        private async Task<double> GetCurrentCpuUsageAsync()
        {
            // Simplified CPU usage calculation
            var process = Process.GetCurrentProcess();
            return await Task.FromResult(process.TotalProcessorTime.TotalMilliseconds % 100);
        }

        private async Task<double> GetCurrentMemoryUsageAsync()
        {
            var process = Process.GetCurrentProcess();
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = process.WorkingSet64;
            return await Task.FromResult((double)totalMemory / workingSet * 100);
        }

        private async Task<double> GetCurrentDiskUsageAsync()
        {
            // Simplified disk usage
            return await Task.FromResult(10737418240); // 10GB free
        }

        private async Task<double> GetNetworkLatencyAsync()
        {
            // Simplified network latency
            return await Task.FromResult(25.5); // 25.5ms
        }

        private async Task<double> GetMetricValueAsync(string metricName)
        {
            if (_metrics.TryGetValue(metricName, out var buffer))
            {
                var recent = buffer.GetRecentMetrics(1).FirstOrDefault();
                return recent?.Value ?? 0;
            }
            return await Task.FromResult(0);
        }

        private async Task<List<MonitoringEvent>> GetRecentEventsAsync(int count)
        {
            // Would fetch from event store
            return await Task.FromResult(new List<MonitoringEvent>());
        }

        private void AggregateMetrics(object state)
        {
            foreach (var buffer in _metrics.Values)
            {
                buffer.Aggregate();
            }
        }

        private void OnAlertTriggered(AlertEventArgs e)
        {
            AlertTriggered?.Invoke(this, e);
        }

        public void Dispose()
        {
            _aggregationTimer?.Dispose();
            _healthCheckTimer?.Dispose();
        }
    }

    public class MetricBuffer
    {
        private readonly string _name;
        private readonly Queue<MetricValue> _buffer;
        private readonly object _lock = new object();
        private const int MaxBufferSize = 10000;

        public MetricBuffer(string name)
        {
            _name = name;
            _buffer = new Queue<MetricValue>();
        }

        public void Add(MetricValue metric)
        {
            lock (_lock)
            {
                _buffer.Enqueue(metric);
                
                // Remove old metrics if buffer is full
                while (_buffer.Count > MaxBufferSize)
                {
                    _buffer.Dequeue();
                }
            }
        }

        public IEnumerable<MetricValue> GetMetrics(DateTime from, DateTime to)
        {
            lock (_lock)
            {
                return _buffer
                    .Where(m => m.Timestamp >= from && m.Timestamp <= to)
                    .ToList();
            }
        }

        public IEnumerable<MetricValue> GetRecentMetrics(int minutes)
        {
            var from = DateTime.UtcNow.AddMinutes(-minutes);
            return GetMetrics(from, DateTime.UtcNow);
        }

        public void Aggregate()
        {
            lock (_lock)
            {
                // Remove metrics older than 24 hours
                var cutoff = DateTime.UtcNow.AddHours(-24);
                while (_buffer.Count > 0 && _buffer.Peek().Timestamp < cutoff)
                {
                    _buffer.Dequeue();
                }
            }
        }
    }

    public class MetricValue
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class AlertRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string MetricName { get; set; }
        public AlertCondition Condition { get; set; }
        public double Threshold { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public Dictionary<string, object> Actions { get; set; } = new();
    }

    public enum AlertCondition
    {
        GreaterThan,
        LessThan,
        Equals,
        NotEquals
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class Alert
    {
        public string Id { get; set; }
        public string RuleId { get; set; }
        public string MetricName { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime TriggeredAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public AlertStatus Status { get; set; }
    }

    public enum AlertStatus
    {
        Active,
        Acknowledged,
        Resolved
    }

    public class HealthCheckResult
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus Status { get; set; }
        public List<HealthCheck> Checks { get; set; }
    }

    public class HealthCheck
    {
        public string Name { get; set; }
        public HealthStatus Status { get; set; }
        public double? Value { get; set; }
        public string Unit { get; set; }
        public string Message { get; set; }
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical
    }

    public class MonitoringDashboard
    {
        public DateTime Timestamp { get; set; }
        public SystemMetrics SystemMetrics { get; set; }
        public ApplicationMetrics ApplicationMetrics { get; set; }
        public List<Alert> ActiveAlerts { get; set; }
        public List<MonitoringEvent> RecentEvents { get; set; }
    }

    public class SystemMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkLatency { get; set; }
    }

    public class ApplicationMetrics
    {
        public double ActiveRules { get; set; }
        public double ExecutedRules { get; set; }
        public double FailedRules { get; set; }
        public double AverageExecutionTime { get; set; }
        public double TotalExecutions { get; set; }
    }

    public class MonitoringEvent
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    public class AlertEventArgs : EventArgs
    {
        public Alert Alert { get; set; }
        public AlertRule Rule { get; set; }
    }
}
