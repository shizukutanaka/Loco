using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Telemetry
{
    public interface IMetricsCollector
    {
        void RecordRequest(string endpoint, string method, int statusCode, double duration);
        void RecordDatabaseQuery(string query, double duration, bool success);
        void RecordCacheHit(string key, bool hit);
        void RecordQueueMessage(string queue, string operation, bool success);
        void RecordBusinessMetric(string name, double value, Dictionary<string, string> dimensions = null);
        Task<MetricsSummary> GetSummaryAsync(TimeSpan period);
        void StartCollection();
        void StopCollection();
    }

    public class MetricsCollector : IMetricsCollector, IHostedService, IDisposable
    {
        private readonly ILogger<MetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, MetricData> _metrics;
        private readonly ConcurrentQueue<TimedMetric> _timedMetrics;
        private readonly Timer _cleanupTimer;
        private readonly Timer _aggregationTimer;
        private readonly TimeSpan _retentionPeriod;
        private readonly int _maxMetricsCount;
        private bool _isCollecting;

        public MetricsCollector(ILogger<MetricsCollector> logger)
        {
            _logger = logger;
            _metrics = new ConcurrentDictionary<string, MetricData>();
            _timedMetrics = new ConcurrentQueue<TimedMetric>();
            _retentionPeriod = TimeSpan.FromHours(24);
            _maxMetricsCount = 100000;
            
            _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            _aggregationTimer = new Timer(AggregateMetrics, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCollection();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCollection();
            return Task.CompletedTask;
        }

        public void StartCollection()
        {
            _isCollecting = true;
            _logger.LogInformation("Metrics collection started");
        }

        public void StopCollection()
        {
            _isCollecting = false;
            _logger.LogInformation("Metrics collection stopped");
        }

        public void RecordRequest(string endpoint, string method, int statusCode, double duration)
        {
            if (!_isCollecting) return;

            var key = $"request:{endpoint}:{method}:{statusCode}";
            var metric = _metrics.GetOrAdd(key, k => new MetricData
            {
                Name = "http_request",
                Type = MetricType.Histogram,
                Tags = new Dictionary<string, string>
                {
                    ["endpoint"] = endpoint,
                    ["method"] = method,
                    ["status"] = statusCode.ToString()
                }
            });

            metric.RecordValue(duration);
            
            _timedMetrics.Enqueue(new TimedMetric
            {
                Timestamp = DateTime.UtcNow,
                Name = "http_request_duration",
                Value = duration,
                Tags = new Dictionary<string, string>
                {
                    ["endpoint"] = endpoint,
                    ["method"] = method,
                    ["status"] = statusCode.ToString()
                }
            });

            if (statusCode >= 400)
            {
                RecordError(endpoint, method, statusCode);
            }

            _logger.LogDebug("Recorded request: {Endpoint} {Method} {StatusCode} {Duration}ms", 
                endpoint, method, statusCode, duration);
        }

        public void RecordDatabaseQuery(string query, double duration, bool success)
        {
            if (!_isCollecting) return;

            var queryType = ExtractQueryType(query);
            var key = $"database:{queryType}:{success}";
            
            var metric = _metrics.GetOrAdd(key, k => new MetricData
            {
                Name = "database_query",
                Type = MetricType.Histogram,
                Tags = new Dictionary<string, string>
                {
                    ["type"] = queryType,
                    ["success"] = success.ToString()
                }
            });

            metric.RecordValue(duration);
            
            _timedMetrics.Enqueue(new TimedMetric
            {
                Timestamp = DateTime.UtcNow,
                Name = "database_query_duration",
                Value = duration,
                Tags = new Dictionary<string, string>
                {
                    ["type"] = queryType,
                    ["success"] = success.ToString()
                }
            });

            _logger.LogDebug("Recorded database query: {Type} {Success} {Duration}ms", 
                queryType, success, duration);
        }

        public void RecordCacheHit(string key, bool hit)
        {
            if (!_isCollecting) return;

            var metricKey = $"cache:{hit}";
            var metric = _metrics.GetOrAdd(metricKey, k => new MetricData
            {
                Name = "cache_access",
                Type = MetricType.Counter,
                Tags = new Dictionary<string, string>
                {
                    ["hit"] = hit.ToString()
                }
            });

            metric.Increment();
            
            var cacheRatio = CalculateCacheHitRatio();
            RecordBusinessMetric("cache_hit_ratio", cacheRatio);

            _logger.LogDebug("Recorded cache access: {Hit}", hit);
        }

        public void RecordQueueMessage(string queue, string operation, bool success)
        {
            if (!_isCollecting) return;

            var key = $"queue:{queue}:{operation}:{success}";
            var metric = _metrics.GetOrAdd(key, k => new MetricData
            {
                Name = "queue_operation",
                Type = MetricType.Counter,
                Tags = new Dictionary<string, string>
                {
                    ["queue"] = queue,
                    ["operation"] = operation,
                    ["success"] = success.ToString()
                }
            });

            metric.Increment();

            _logger.LogDebug("Recorded queue operation: {Queue} {Operation} {Success}", 
                queue, operation, success);
        }

        public void RecordBusinessMetric(string name, double value, Dictionary<string, string> dimensions = null)
        {
            if (!_isCollecting) return;

            var key = GenerateMetricKey(name, dimensions);
            var metric = _metrics.GetOrAdd(key, k => new MetricData
            {
                Name = name,
                Type = MetricType.Gauge,
                Tags = dimensions ?? new Dictionary<string, string>()
            });

            metric.RecordValue(value);
            
            _timedMetrics.Enqueue(new TimedMetric
            {
                Timestamp = DateTime.UtcNow,
                Name = name,
                Value = value,
                Tags = dimensions ?? new Dictionary<string, string>()
            });

            _logger.LogDebug("Recorded business metric: {Name} = {Value}", name, value);
        }

        public async Task<MetricsSummary> GetSummaryAsync(TimeSpan period)
        {
            var cutoff = DateTime.UtcNow.Subtract(period);
            var recentMetrics = new List<TimedMetric>();

            foreach (var metric in _timedMetrics)
            {
                if (metric.Timestamp >= cutoff)
                {
                    recentMetrics.Add(metric);
                }
            }

            var summary = new MetricsSummary
            {
                Period = period,
                StartTime = cutoff,
                EndTime = DateTime.UtcNow,
                TotalMetrics = recentMetrics.Count
            };

            var groupedMetrics = recentMetrics.GroupBy(m => m.Name);
            foreach (var group in groupedMetrics)
            {
                var values = group.Select(m => m.Value).ToList();
                summary.Metrics[group.Key] = new MetricStatistics
                {
                    Count = values.Count,
                    Sum = values.Sum(),
                    Average = values.Average(),
                    Min = values.Min(),
                    Max = values.Max(),
                    Percentile50 = CalculatePercentile(values, 0.5),
                    Percentile95 = CalculatePercentile(values, 0.95),
                    Percentile99 = CalculatePercentile(values, 0.99)
                };
            }

            var requestMetrics = recentMetrics.Where(m => m.Name == "http_request_duration").ToList();
            if (requestMetrics.Any())
            {
                summary.RequestsPerSecond = requestMetrics.Count / period.TotalSeconds;
                summary.AverageResponseTime = requestMetrics.Average(m => m.Value);
                
                var errorCount = requestMetrics.Count(m => 
                    m.Tags.TryGetValue("status", out var status) && int.Parse(status) >= 400);
                summary.ErrorRate = (double)errorCount / requestMetrics.Count;
            }

            var dbMetrics = recentMetrics.Where(m => m.Name == "database_query_duration").ToList();
            if (dbMetrics.Any())
            {
                summary.DatabaseQueriesPerSecond = dbMetrics.Count / period.TotalSeconds;
                summary.AverageDatabaseQueryTime = dbMetrics.Average(m => m.Value);
            }

            summary.CacheHitRatio = CalculateCacheHitRatio();

            _logger.LogInformation("Generated metrics summary for period: {Period}", period);
            return await Task.FromResult(summary);
        }

        private void RecordError(string endpoint, string method, int statusCode)
        {
            var key = $"error:{endpoint}:{method}:{statusCode}";
            var metric = _metrics.GetOrAdd(key, k => new MetricData
            {
                Name = "http_error",
                Type = MetricType.Counter,
                Tags = new Dictionary<string, string>
                {
                    ["endpoint"] = endpoint,
                    ["method"] = method,
                    ["status"] = statusCode.ToString()
                }
            });

            metric.Increment();
        }

        private string ExtractQueryType(string query)
        {
            var trimmedQuery = query.Trim().ToUpper();
            if (trimmedQuery.StartsWith("SELECT")) return "SELECT";
            if (trimmedQuery.StartsWith("INSERT")) return "INSERT";
            if (trimmedQuery.StartsWith("UPDATE")) return "UPDATE";
            if (trimmedQuery.StartsWith("DELETE")) return "DELETE";
            if (trimmedQuery.StartsWith("EXEC")) return "EXEC";
            return "OTHER";
        }

        private double CalculateCacheHitRatio()
        {
            var hitKey = "cache:True";
            var missKey = "cache:False";

            if (_metrics.TryGetValue(hitKey, out var hits) && 
                _metrics.TryGetValue(missKey, out var misses))
            {
                var totalAccesses = hits.Count + misses.Count;
                return totalAccesses > 0 ? (double)hits.Count / totalAccesses : 0;
            }

            return 0;
        }

        private string GenerateMetricKey(string name, Dictionary<string, string> dimensions)
        {
            if (dimensions == null || dimensions.Count == 0)
                return $"business:{name}";

            var sortedDimensions = dimensions.OrderBy(d => d.Key)
                .Select(d => $"{d.Key}={d.Value}");
            return $"business:{name}:{string.Join(",", sortedDimensions)}";
        }

        private double CalculatePercentile(List<double> values, double percentile)
        {
            if (!values.Any()) return 0;

            values.Sort();
            var index = (int)Math.Ceiling(percentile * values.Count) - 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        private void CleanupOldMetrics(object state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.Subtract(_retentionPeriod);
                var itemsToRemove = new List<TimedMetric>();

                while (_timedMetrics.TryPeek(out var metric) && metric.Timestamp < cutoff)
                {
                    _timedMetrics.TryDequeue(out _);
                }

                if (_timedMetrics.Count > _maxMetricsCount)
                {
                    var excess = _timedMetrics.Count - _maxMetricsCount;
                    for (int i = 0; i < excess; i++)
                    {
                        _timedMetrics.TryDequeue(out _);
                    }
                }

                _logger.LogDebug("Cleaned up old metrics. Current count: {Count}", _timedMetrics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics cleanup");
            }
        }

        private void AggregateMetrics(object state)
        {
            try
            {
                foreach (var metric in _metrics.Values)
                {
                    metric.Aggregate();
                }

                _logger.LogDebug("Aggregated metrics. Total metric types: {Count}", _metrics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics aggregation");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _aggregationTimer?.Dispose();
            _logger.LogInformation("Metrics collector disposed");
        }
    }

    public class MetricData
    {
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private readonly object _lock = new object();

        public string Name { get; set; }
        public MetricType Type { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public long Count => _count;
        public double Sum => _sum;
        public double Average => _count > 0 ? _sum / _count : 0;
        public double Min => _min == double.MaxValue ? 0 : _min;
        public double Max => _max == double.MinValue ? 0 : _max;

        public void RecordValue(double value)
        {
            lock (_lock)
            {
                _count++;
                _sum += value;
                _min = Math.Min(_min, value);
                _max = Math.Max(_max, value);
            }
        }

        public void Increment()
        {
            Interlocked.Increment(ref _count);
        }

        public void Aggregate()
        {
        }
    }

    public class TimedMetric
    {
        public DateTime Timestamp { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class MetricsSummary
    {
        public TimeSpan Period { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalMetrics { get; set; }
        public Dictionary<string, MetricStatistics> Metrics { get; set; } = new Dictionary<string, MetricStatistics>();
        public double RequestsPerSecond { get; set; }
        public double AverageResponseTime { get; set; }
        public double ErrorRate { get; set; }
        public double DatabaseQueriesPerSecond { get; set; }
        public double AverageDatabaseQueryTime { get; set; }
        public double CacheHitRatio { get; set; }
    }

    public class MetricStatistics
    {
        public long Count { get; set; }
        public double Sum { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Percentile50 { get; set; }
        public double Percentile95 { get; set; }
        public double Percentile99 { get; set; }
    }

    public enum MetricType
    {
        Counter,
        Gauge,
        Histogram
    }
}