using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.Monitoring
{
    public interface IMonitoringMetricsService
    {
        void RecordCounter(string name, long value, TagList tags = default);
        void RecordHistogram(string name, double value, TagList tags = default);
        void RecordGauge(string name, double value, TagList tags = default);
        void IncrementCounter(string name, TagList tags = default);
        void DecrementCounter(string name, TagList tags = default);
        IDisposable StartTimer(string name, TagList tags = default);
        Task<MetricsSnapshot> GetMetricsSnapshotAsync();
        Task<PerformanceReport> GeneratePerformanceReportAsync(TimeSpan period);
        void RegisterCustomMetric(string name, MetricType type, string description = null);
        Task ExportMetricsAsync(MetricsExportFormat format, string destination);
        void SetupAlerts(string metricName, AlertRule rule);
        Task<List<Alert>> GetActiveAlertsAsync();
    }

    public class MonitoringMetricsService : IMonitoringMetricsService, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonitoringMetricsService> _logger;
        private readonly MonitoringConfiguration _configuration;
        private readonly Meter _meter;
        private readonly ConcurrentDictionary<string, Counter<long>> _counters;
        private readonly ConcurrentDictionary<string, Histogram<double>> _histograms;
        private readonly ConcurrentDictionary<string, ObservableGauge<double>> _gauges;
        private readonly ConcurrentDictionary<string, CustomMetric> _customMetrics;
        private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
        private readonly ConcurrentDictionary<string, Alert> _activeAlerts;
        private readonly Timer _metricsExportTimer;
        private readonly Timer _alertCheckTimer;

        // Built-in metrics
        private readonly Counter<long> _requestCounter;
        private readonly Counter<long> _errorCounter;
        private readonly Histogram<double> _requestDuration;
        private readonly Histogram<double> _databaseQueryDuration;
        private readonly Counter<long> _cacheHitCounter;
        private readonly Counter<long> _cacheMissCounter;

        public MonitoringMetricsService(
            IServiceProvider serviceProvider,
            IOptions<MonitoringConfiguration> configuration,
            ILogger<MonitoringMetricsService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration.Value;
            _logger = logger;
            _meter = new Meter("Loco.Core", "1.0.0");
            
            _counters = new ConcurrentDictionary<string, Counter<long>>();
            _histograms = new ConcurrentDictionary<string, Histogram<double>>();
            _gauges = new ConcurrentDictionary<string, ObservableGauge<double>>();
            _customMetrics = new ConcurrentDictionary<string, CustomMetric>();
            _alertRules = new ConcurrentDictionary<string, AlertRule>();
            _activeAlerts = new ConcurrentDictionary<string, Alert>();

            // Initialize built-in metrics
            _requestCounter = _meter.CreateCounter<long>("http_requests_total", "requests", "Total number of HTTP requests");
            _errorCounter = _meter.CreateCounter<long>("http_errors_total", "errors", "Total number of HTTP errors");
            _requestDuration = _meter.CreateHistogram<double>("http_request_duration_seconds", "seconds", "HTTP request duration");
            _databaseQueryDuration = _meter.CreateHistogram<double>("database_query_duration_seconds", "seconds", "Database query duration");
            _cacheHitCounter = _meter.CreateCounter<long>("cache_hits_total", "hits", "Total cache hits");
            _cacheMissCounter = _meter.CreateCounter<long>("cache_misses_total", "misses", "Total cache misses");

            // Initialize system metrics
            InitializeSystemMetrics();

            // Setup timers
            _metricsExportTimer = new Timer(ExportMetricsCallback, null, 
                TimeSpan.FromSeconds(_configuration.ExportIntervalSeconds),
                TimeSpan.FromSeconds(_configuration.ExportIntervalSeconds));

            _alertCheckTimer = new Timer(CheckAlertsCallback, null,
                TimeSpan.FromSeconds(_configuration.AlertCheckIntervalSeconds),
                TimeSpan.FromSeconds(_configuration.AlertCheckIntervalSeconds));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await SetupDefaultAlerts();
            _logger.LogInformation("Monitoring metrics service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _metricsExportTimer?.Dispose();
            _alertCheckTimer?.Dispose();
            _meter?.Dispose();
            _logger.LogInformation("Monitoring metrics service stopped");
            await Task.CompletedTask;
        }

        public void RecordCounter(string name, long value, TagList tags = default)
        {
            var counter = _counters.GetOrAdd(name, _ => _meter.CreateCounter<long>(name));
            counter.Add(value, tags);
        }

        public void RecordHistogram(string name, double value, TagList tags = default)
        {
            var histogram = _histograms.GetOrAdd(name, _ => _meter.CreateHistogram<double>(name));
            histogram.Record(value, tags);
        }

        public void RecordGauge(string name, double value, TagList tags = default)
        {
            var customMetric = _customMetrics.GetOrAdd(name, _ => new CustomMetric 
            { 
                Name = name, 
                Type = MetricType.Gauge,
                LastValue = value,
                LastUpdated = DateTime.UtcNow,
                Tags = tags.ToArray().ToDictionary(kv => kv.Key, kv => kv.Value?.ToString())
            });
            
            customMetric.LastValue = value;
            customMetric.LastUpdated = DateTime.UtcNow;
        }

        public void IncrementCounter(string name, TagList tags = default)
        {
            RecordCounter(name, 1, tags);
        }

        public void DecrementCounter(string name, TagList tags = default)
        {
            RecordCounter(name, -1, tags);
        }

        public IDisposable StartTimer(string name, TagList tags = default)
        {
            return new MetricTimer(this, name, tags);
        }

        public async Task<MetricsSnapshot> GetMetricsSnapshotAsync()
        {
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Counters = await GetCounterValuesAsync(),
                Histograms = await GetHistogramValuesAsync(),
                Gauges = await GetGaugeValuesAsync(),
                CustomMetrics = _customMetrics.Values.ToList(),
                SystemMetrics = await GetSystemMetricsAsync()
            };

            return snapshot;
        }

        public async Task<PerformanceReport> GeneratePerformanceReportAsync(TimeSpan period)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - period;

            var report = new PerformanceReport
            {
                StartTime = startTime,
                EndTime = endTime,
                Period = period,
                GeneratedAt = DateTime.UtcNow
            };

            // Collect metrics for the period
            var snapshot = await GetMetricsSnapshotAsync();
            
            // Calculate performance indicators
            report.RequestRate = CalculateRate("http_requests_total", period);
            report.ErrorRate = CalculateRate("http_errors_total", period);
            report.AverageResponseTime = CalculateAverage("http_request_duration_seconds", period);
            report.P95ResponseTime = CalculatePercentile("http_request_duration_seconds", 95, period);
            report.P99ResponseTime = CalculatePercentile("http_request_duration_seconds", 99, period);
            report.CacheHitRate = CalculateCacheHitRate(period);
            report.DatabasePerformance = await CalculateDatabasePerformanceAsync(period);
            report.ResourceUtilization = await CalculateResourceUtilizationAsync();
            report.TopEndpoints = await GetTopEndpointsAsync(period);
            report.Recommendations = GeneratePerformanceRecommendations(report);

            return report;
        }

        public void RegisterCustomMetric(string name, MetricType type, string description = null)
        {
            var customMetric = new CustomMetric
            {
                Name = name,
                Type = type,
                Description = description,
                CreatedAt = DateTime.UtcNow
            };

            _customMetrics[name] = customMetric;
            _logger.LogInformation("Registered custom metric: {Name} ({Type})", name, type);
        }

        public async Task ExportMetricsAsync(MetricsExportFormat format, string destination)
        {
            var snapshot = await GetMetricsSnapshotAsync();
            
            switch (format)
            {
                case MetricsExportFormat.Prometheus:
                    await ExportPrometheusFormatAsync(snapshot, destination);
                    break;
                case MetricsExportFormat.Json:
                    await ExportJsonFormatAsync(snapshot, destination);
                    break;
                case MetricsExportFormat.InfluxDB:
                    await ExportInfluxDbFormatAsync(snapshot, destination);
                    break;
                case MetricsExportFormat.StatsD:
                    await ExportStatsDFormatAsync(snapshot, destination);
                    break;
                default:
                    throw new NotSupportedException($"Export format {format} is not supported");
            }

            _logger.LogInformation("Exported metrics in {Format} format to {Destination}", format, destination);
        }

        public void SetupAlerts(string metricName, AlertRule rule)
        {
            _alertRules[metricName] = rule;
            _logger.LogInformation("Setup alert for metric {MetricName}: {Rule}", metricName, rule.Description);
        }

        public async Task<List<Alert>> GetActiveAlertsAsync()
        {
            return await Task.FromResult(_activeAlerts.Values.ToList());
        }

        private void InitializeSystemMetrics()
        {
            // CPU Usage
            _gauges["system_cpu_usage"] = _meter.CreateObservableGauge<double>("system_cpu_usage", 
                () => GetCpuUsage(), "percent", "Current CPU usage percentage");

            // Memory Usage
            _gauges["system_memory_usage"] = _meter.CreateObservableGauge<double>("system_memory_usage",
                () => GetMemoryUsage(), "bytes", "Current memory usage in bytes");

            // GC Collections
            _gauges["gc_collections_gen0"] = _meter.CreateObservableGauge<double>("gc_collections_gen0",
                () => GC.CollectionCount(0), "collections", "Generation 0 garbage collections");

            _gauges["gc_collections_gen1"] = _meter.CreateObservableGauge<double>("gc_collections_gen1",
                () => GC.CollectionCount(1), "collections", "Generation 1 garbage collections");

            _gauges["gc_collections_gen2"] = _meter.CreateObservableGauge<double>("gc_collections_gen2",
                () => GC.CollectionCount(2), "collections", "Generation 2 garbage collections");

            // Thread Pool
            _gauges["threadpool_worker_threads"] = _meter.CreateObservableGauge<double>("threadpool_worker_threads",
                () => { ThreadPool.GetAvailableThreads(out var workerThreads, out _); return workerThreads; },
                "threads", "Available worker threads in thread pool");

            _gauges["threadpool_completion_port_threads"] = _meter.CreateObservableGauge<double>("threadpool_completion_port_threads",
                () => { ThreadPool.GetAvailableThreads(out _, out var completionPortThreads); return completionPortThreads; },
                "threads", "Available completion port threads in thread pool");
        }

        private async Task SetupDefaultAlerts()
        {
            // High error rate alert
            SetupAlerts("http_errors_total", new AlertRule
            {
                MetricName = "http_errors_total",
                Condition = AlertCondition.GreaterThan,
                Threshold = _configuration.ErrorRateThreshold,
                WindowMinutes = 5,
                Severity = AlertSeverity.High,
                Description = "High error rate detected"
            });

            // High response time alert
            SetupAlerts("http_request_duration_seconds", new AlertRule
            {
                MetricName = "http_request_duration_seconds",
                Condition = AlertCondition.GreaterThan,
                Threshold = _configuration.ResponseTimeThresholdSeconds,
                WindowMinutes = 5,
                Severity = AlertSeverity.Medium,
                Description = "High response time detected"
            });

            // High CPU usage alert
            SetupAlerts("system_cpu_usage", new AlertRule
            {
                MetricName = "system_cpu_usage",
                Condition = AlertCondition.GreaterThan,
                Threshold = _configuration.CpuUsageThreshold,
                WindowMinutes = 10,
                Severity = AlertSeverity.Medium,
                Description = "High CPU usage detected"
            });

            // High memory usage alert
            SetupAlerts("system_memory_usage", new AlertRule
            {
                MetricName = "system_memory_usage",
                Condition = AlertCondition.GreaterThan,
                Threshold = _configuration.MemoryUsageThreshold,
                WindowMinutes = 10,
                Severity = AlertSeverity.High,
                Description = "High memory usage detected"
            });

            await Task.CompletedTask;
        }

        private async void ExportMetricsCallback(object state)
        {
            try
            {
                if (_configuration.EnableAutoExport && !string.IsNullOrEmpty(_configuration.ExportEndpoint))
                {
                    await ExportMetricsAsync(_configuration.ExportFormat, _configuration.ExportEndpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic metrics export");
            }
        }

        private async void CheckAlertsCallback(object state)
        {
            try
            {
                await CheckAndTriggerAlerts();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert checking");
            }
        }

        private async Task CheckAndTriggerAlerts()
        {
            var snapshot = await GetMetricsSnapshotAsync();

            foreach (var alertRule in _alertRules.Values)
            {
                var currentValue = GetMetricValue(snapshot, alertRule.MetricName);
                var shouldAlert = EvaluateAlertCondition(alertRule, currentValue);

                var alertKey = $"{alertRule.MetricName}_{alertRule.Condition}_{alertRule.Threshold}";

                if (shouldAlert && !_activeAlerts.ContainsKey(alertKey))
                {
                    // Trigger new alert
                    var alert = new Alert
                    {
                        Id = Guid.NewGuid(),
                        MetricName = alertRule.MetricName,
                        Rule = alertRule,
                        CurrentValue = currentValue,
                        TriggeredAt = DateTime.UtcNow,
                        Severity = alertRule.Severity,
                        Message = $"{alertRule.Description}: {alertRule.MetricName} = {currentValue} (threshold: {alertRule.Threshold})"
                    };

                    _activeAlerts[alertKey] = alert;
                    await SendAlertNotificationAsync(alert);

                    _logger.LogWarning("Alert triggered: {AlertMessage}", alert.Message);
                }
                else if (!shouldAlert && _activeAlerts.TryRemove(alertKey, out var existingAlert))
                {
                    // Resolve alert
                    existingAlert.ResolvedAt = DateTime.UtcNow;
                    await SendAlertResolutionAsync(existingAlert);

                    _logger.LogInformation("Alert resolved: {AlertMessage}", existingAlert.Message);
                }
            }
        }

        private bool EvaluateAlertCondition(AlertRule rule, double currentValue)
        {
            return rule.Condition switch
            {
                AlertCondition.GreaterThan => currentValue > rule.Threshold,
                AlertCondition.LessThan => currentValue < rule.Threshold,
                AlertCondition.Equals => Math.Abs(currentValue - rule.Threshold) < 0.001,
                _ => false
            };
        }

        private double GetMetricValue(MetricsSnapshot snapshot, string metricName)
        {
            // Try to get value from different metric types
            if (snapshot.Counters.TryGetValue(metricName, out var counterValue))
                return counterValue;

            if (snapshot.Gauges.TryGetValue(metricName, out var gaugeValue))
                return gaugeValue;

            if (snapshot.Histograms.TryGetValue(metricName, out var histogramValue))
                return histogramValue.Mean;

            var customMetric = snapshot.CustomMetrics.FirstOrDefault(m => m.Name == metricName);
            return customMetric?.LastValue ?? 0;
        }

        private async Task SendAlertNotificationAsync(Alert alert)
        {
            try
            {
                // Send to configured notification channels
                if (!string.IsNullOrEmpty(_configuration.WebhookUrl))
                {
                    await SendWebhookNotificationAsync(alert);
                }

                if (!string.IsNullOrEmpty(_configuration.SlackWebhookUrl))
                {
                    await SendSlackNotificationAsync(alert);
                }

                // Store alert in database
                await StoreAlertAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending alert notification");
            }
        }

        private async Task SendAlertResolutionAsync(Alert alert)
        {
            // Send resolution notifications
            await Task.CompletedTask;
        }

        private async Task SendWebhookNotificationAsync(Alert alert)
        {
            using var httpClient = new HttpClient();
            var payload = new
            {
                type = "alert",
                alert = new
                {
                    id = alert.Id,
                    metric = alert.MetricName,
                    severity = alert.Severity.ToString(),
                    message = alert.Message,
                    currentValue = alert.CurrentValue,
                    threshold = alert.Rule.Threshold,
                    triggeredAt = alert.TriggeredAt
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await httpClient.PostAsync(_configuration.WebhookUrl, content);
        }

        private async Task SendSlackNotificationAsync(Alert alert)
        {
            using var httpClient = new HttpClient();
            var color = alert.Severity switch
            {
                AlertSeverity.Critical => "danger",
                AlertSeverity.High => "warning",
                AlertSeverity.Medium => "warning",
                AlertSeverity.Low => "good",
                _ => "good"
            };

            var payload = new
            {
                text = $"🚨 Alert: {alert.Message}",
                attachments = new[]
                {
                    new
                    {
                        color = color,
                        fields = new[]
                        {
                            new { title = "Metric", value = alert.MetricName, @short = true },
                            new { title = "Severity", value = alert.Severity.ToString(), @short = true },
                            new { title = "Current Value", value = alert.CurrentValue.ToString("F2"), @short = true },
                            new { title = "Threshold", value = alert.Rule.Threshold.ToString("F2"), @short = true }
                        },
                        ts = ((DateTimeOffset)alert.TriggeredAt).ToUnixTimeSeconds()
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await httpClient.PostAsync(_configuration.SlackWebhookUrl, content);
        }

        private async Task StoreAlertAsync(Alert alert)
        {
            // Store alert in database for audit trail
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<MonitoringDbContext>();
                
                if (dbContext != null)
                {
                    var alertEntity = new AlertEntity
                    {
                        Id = alert.Id,
                        MetricName = alert.MetricName,
                        Severity = alert.Severity,
                        Message = alert.Message,
                        CurrentValue = alert.CurrentValue,
                        Threshold = alert.Rule.Threshold,
                        TriggeredAt = alert.TriggeredAt,
                        ResolvedAt = alert.ResolvedAt
                    };

                    dbContext.Alerts.Add(alertEntity);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store alert in database");
            }
        }

        private async Task<Dictionary<string, long>> GetCounterValuesAsync()
        {
            // This is simplified - in a real implementation, you'd collect actual values
            return await Task.FromResult(new Dictionary<string, long>());
        }

        private async Task<Dictionary<string, HistogramValue>> GetHistogramValuesAsync()
        {
            return await Task.FromResult(new Dictionary<string, HistogramValue>());
        }

        private async Task<Dictionary<string, double>> GetGaugeValuesAsync()
        {
            return await Task.FromResult(new Dictionary<string, double>());
        }

        private async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            var process = Process.GetCurrentProcess();
            
            return await Task.FromResult(new SystemMetrics
            {
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                GcGen0Collections = GC.CollectionCount(0),
                GcGen1Collections = GC.CollectionCount(1),
                GcGen2Collections = GC.CollectionCount(2)
            });
        }

        private double GetCpuUsage()
        {
            // Simplified CPU usage calculation
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / Environment.TickCount * 100;
        }

        private double GetMemoryUsage()
        {
            var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }

        private double CalculateRate(string metricName, TimeSpan period)
        {
            // Calculate rate per second over the period
            return 0; // Simplified
        }

        private double CalculateAverage(string metricName, TimeSpan period)
        {
            // Calculate average value over the period
            return 0; // Simplified
        }

        private double CalculatePercentile(string metricName, int percentile, TimeSpan period)
        {
            // Calculate percentile value over the period
            return 0; // Simplified
        }

        private double CalculateCacheHitRate(TimeSpan period)
        {
            // Calculate cache hit rate
            return 0; // Simplified
        }

        private async Task<DatabasePerformance> CalculateDatabasePerformanceAsync(TimeSpan period)
        {
            return await Task.FromResult(new DatabasePerformance
            {
                AverageQueryTime = 0,
                SlowQueries = 0,
                TotalQueries = 0
            });
        }

        private async Task<ResourceUtilization> CalculateResourceUtilizationAsync()
        {
            return await Task.FromResult(new ResourceUtilization
            {
                CpuPercent = GetCpuUsage(),
                MemoryPercent = 0,
                DiskPercent = 0,
                NetworkBytesPerSecond = 0
            });
        }

        private async Task<List<EndpointMetrics>> GetTopEndpointsAsync(TimeSpan period)
        {
            return await Task.FromResult(new List<EndpointMetrics>());
        }

        private List<string> GeneratePerformanceRecommendations(PerformanceReport report)
        {
            var recommendations = new List<string>();

            if (report.ErrorRate > 0.05)
                recommendations.Add("Error rate is high. Investigate error causes and implement better error handling.");

            if (report.AverageResponseTime > 1000)
                recommendations.Add("Average response time is high. Consider optimizing database queries and adding caching.");

            if (report.CacheHitRate < 0.8)
                recommendations.Add("Cache hit rate is low. Review caching strategy and increase cache TTL where appropriate.");

            return recommendations;
        }

        private async Task ExportPrometheusFormatAsync(MetricsSnapshot snapshot, string destination)
        {
            var sb = new StringBuilder();
            
            // Export counters
            foreach (var counter in snapshot.Counters)
            {
                sb.AppendLine($"# TYPE {counter.Key} counter");
                sb.AppendLine($"{counter.Key} {counter.Value}");
            }

            // Export gauges
            foreach (var gauge in snapshot.Gauges)
            {
                sb.AppendLine($"# TYPE {gauge.Key} gauge");
                sb.AppendLine($"{gauge.Key} {gauge.Value}");
            }

            var content = sb.ToString();
            
            if (destination.StartsWith("http"))
            {
                using var httpClient = new HttpClient();
                await httpClient.PostAsync(destination, new StringContent(content, Encoding.UTF8, "text/plain"));
            }
            else
            {
                await System.IO.File.WriteAllTextAsync(destination, content);
            }
        }

        private async Task ExportJsonFormatAsync(MetricsSnapshot snapshot, string destination)
        {
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            
            if (destination.StartsWith("http"))
            {
                using var httpClient = new HttpClient();
                await httpClient.PostAsync(destination, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            else
            {
                await System.IO.File.WriteAllTextAsync(destination, json);
            }
        }

        private async Task ExportInfluxDbFormatAsync(MetricsSnapshot snapshot, string destination)
        {
            var lines = new List<string>();
            var timestamp = ((DateTimeOffset)snapshot.Timestamp).ToUnixTimeNanoseconds();

            foreach (var counter in snapshot.Counters)
            {
                lines.Add($"{counter.Key},host={Environment.MachineName} value={counter.Value} {timestamp}");
            }

            foreach (var gauge in snapshot.Gauges)
            {
                lines.Add($"{gauge.Key},host={Environment.MachineName} value={gauge.Value} {timestamp}");
            }

            var content = string.Join("\n", lines);
            
            using var httpClient = new HttpClient();
            await httpClient.PostAsync(destination, new StringContent(content, Encoding.UTF8, "text/plain"));
        }

        private async Task ExportStatsDFormatAsync(MetricsSnapshot snapshot, string destination)
        {
            // StatsD export implementation
            await Task.CompletedTask;
        }
    }

    // Timer helper class
    public class MetricTimer : IDisposable
    {
        private readonly IMonitoringMetricsService _metricsService;
        private readonly string _metricName;
        private readonly TagList _tags;
        private readonly Stopwatch _stopwatch;

        public MetricTimer(IMonitoringMetricsService metricsService, string metricName, TagList tags)
        {
            _metricsService = metricsService;
            _metricName = metricName;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metricsService.RecordHistogram(_metricName, _stopwatch.Elapsed.TotalSeconds, _tags);
        }
    }

    // Models and enums
    public class MetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, long> Counters { get; set; }
        public Dictionary<string, HistogramValue> Histograms { get; set; }
        public Dictionary<string, double> Gauges { get; set; }
        public List<CustomMetric> CustomMetrics { get; set; }
        public SystemMetrics SystemMetrics { get; set; }
    }

    public class HistogramValue
    {
        public double Mean { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
        public long Count { get; set; }
    }

    public class CustomMetric
    {
        public string Name { get; set; }
        public MetricType Type { get; set; }
        public string Description { get; set; }
        public double LastValue { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    public class SystemMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public long GcGen0Collections { get; set; }
        public long GcGen1Collections { get; set; }
        public long GcGen2Collections { get; set; }
    }

    public class PerformanceReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Period { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double RequestRate { get; set; }
        public double ErrorRate { get; set; }
        public double AverageResponseTime { get; set; }
        public double P95ResponseTime { get; set; }
        public double P99ResponseTime { get; set; }
        public double CacheHitRate { get; set; }
        public DatabasePerformance DatabasePerformance { get; set; }
        public ResourceUtilization ResourceUtilization { get; set; }
        public List<EndpointMetrics> TopEndpoints { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class DatabasePerformance
    {
        public double AverageQueryTime { get; set; }
        public int SlowQueries { get; set; }
        public int TotalQueries { get; set; }
    }

    public class ResourceUtilization
    {
        public double CpuPercent { get; set; }
        public double MemoryPercent { get; set; }
        public double DiskPercent { get; set; }
        public double NetworkBytesPerSecond { get; set; }
    }

    public class EndpointMetrics
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public int RequestCount { get; set; }
        public double AverageResponseTime { get; set; }
        public double ErrorRate { get; set; }
    }

    public class AlertRule
    {
        public string MetricName { get; set; }
        public AlertCondition Condition { get; set; }
        public double Threshold { get; set; }
        public int WindowMinutes { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Description { get; set; }
    }

    public class Alert
    {
        public Guid Id { get; set; }
        public string MetricName { get; set; }
        public AlertRule Rule { get; set; }
        public double CurrentValue { get; set; }
        public DateTime TriggeredAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
    }

    public class MonitoringConfiguration
    {
        public int ExportIntervalSeconds { get; set; } = 60;
        public int AlertCheckIntervalSeconds { get; set; } = 30;
        public bool EnableAutoExport { get; set; } = true;
        public string ExportEndpoint { get; set; }
        public MetricsExportFormat ExportFormat { get; set; } = MetricsExportFormat.Prometheus;
        public string WebhookUrl { get; set; }
        public string SlackWebhookUrl { get; set; }
        public double ErrorRateThreshold { get; set; } = 0.05;
        public double ResponseTimeThresholdSeconds { get; set; } = 2.0;
        public double CpuUsageThreshold { get; set; } = 80.0;
        public double MemoryUsageThreshold { get; set; } = 85.0;
    }

    public enum MetricType
    {
        Counter,
        Histogram,
        Gauge,
        Summary
    }

    public enum MetricsExportFormat
    {
        Prometheus,
        Json,
        InfluxDB,
        StatsD
    }

    public enum AlertCondition
    {
        GreaterThan,
        LessThan,
        Equals
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    // Database entities
    public class MonitoringDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public MonitoringDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<MonitoringDbContext> options) : base(options) { }

        public Microsoft.EntityFrameworkCore.DbSet<AlertEntity> Alerts { get; set; }
    }

    public class AlertEntity
    {
        public Guid Id { get; set; }
        public string MetricName { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
        public DateTime TriggeredAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}