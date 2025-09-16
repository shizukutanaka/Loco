using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loco.Core.Monitoring
{
    public interface IMetricsCollector
    {
        void IncrementCounter(string name, double value = 1, Dictionary<string, string> tags = null);
        void SetGauge(string name, double value, Dictionary<string, string> tags = null);
        void RecordTimer(string name, TimeSpan duration, Dictionary<string, string> tags = null);
        void RecordHistogram(string name, double value, Dictionary<string, string> tags = null);
        MetricsSnapshot GetSnapshot();
        void Reset();
    }

    public class MetricValue
    {
        public string Name { get; set; }
        public MetricType Type { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    public enum MetricType
    {
        Counter,
        Gauge,
        Timer,
        Histogram
    }

    public class MetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, CounterMetric> Counters { get; set; } = new();
        public Dictionary<string, GaugeMetric> Gauges { get; set; } = new();
        public Dictionary<string, TimerMetric> Timers { get; set; } = new();
        public Dictionary<string, HistogramMetric> Histograms { get; set; } = new();
        public SystemMetrics System { get; set; }
    }

    public class CounterMetric
    {
        public double Value { get; set; }
        public double Rate { get; set; } // per second
        public DateTime LastUpdated { get; set; }
    }

    public class GaugeMetric
    {
        public double Value { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Average { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class TimerMetric
    {
        public long Count { get; set; }
        public double TotalMilliseconds { get; set; }
        public double MinMilliseconds { get; set; }
        public double MaxMilliseconds { get; set; }
        public double AverageMilliseconds { get; set; }
        public double Rate { get; set; } // per second
    }

    public class HistogramMetric
    {
        public long Count { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Average { get; set; }
        public double Sum { get; set; }
        public double[] Percentiles { get; set; } // 50th, 75th, 95th, 99th
    }

    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long MemoryTotalBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long GcCollections { get; set; }
        public long ThreadCount { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    public class SimpleMetricsCollector : IMetricsCollector, IDisposable
    {
        private readonly ILogger<SimpleMetricsCollector> _logger;
        private readonly ConcurrentDictionary<string, CounterState> _counters = new();
        private readonly ConcurrentDictionary<string, GaugeState> _gauges = new();
        private readonly ConcurrentDictionary<string, TimerState> _timers = new();
        private readonly ConcurrentDictionary<string, HistogramState> _histograms = new();
        private readonly Timer _systemMetricsTimer;
        private readonly Process _currentProcess;
        private readonly DateTime _startTime;
        private SystemMetrics _lastSystemMetrics;

        public SimpleMetricsCollector(ILogger<SimpleMetricsCollector> logger = null)
        {
            _logger = logger ?? NullLogger<SimpleMetricsCollector>.Instance;
            _currentProcess = Process.GetCurrentProcess();
            _startTime = DateTime.UtcNow;

            // Collect system metrics every 5 seconds
            _systemMetricsTimer = new Timer(CollectSystemMetrics, null,
                TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void IncrementCounter(string name, double value = 1, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            var key = GetMetricKey(name, tags);
            _counters.AddOrUpdate(key,
                new CounterState { Value = value, LastUpdated = DateTime.UtcNow },
                (k, existing) =>
                {
                    existing.Value += value;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public void SetGauge(string name, double value, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            var key = GetMetricKey(name, tags);
            _gauges.AddOrUpdate(key,
                new GaugeState
                {
                    Value = value,
                    Min = value,
                    Max = value,
                    Sum = value,
                    Count = 1,
                    LastUpdated = DateTime.UtcNow
                },
                (k, existing) =>
                {
                    existing.Value = value;
                    existing.Min = Math.Min(existing.Min, value);
                    existing.Max = Math.Max(existing.Max, value);
                    existing.Sum += value;
                    existing.Count++;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public void RecordTimer(string name, TimeSpan duration, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            var milliseconds = duration.TotalMilliseconds;
            var key = GetMetricKey(name, tags);

            _timers.AddOrUpdate(key,
                new TimerState
                {
                    Count = 1,
                    TotalMs = milliseconds,
                    MinMs = milliseconds,
                    MaxMs = milliseconds,
                    LastUpdated = DateTime.UtcNow
                },
                (k, existing) =>
                {
                    existing.Count++;
                    existing.TotalMs += milliseconds;
                    existing.MinMs = Math.Min(existing.MinMs, milliseconds);
                    existing.MaxMs = Math.Max(existing.MaxMs, milliseconds);
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public void RecordHistogram(string name, double value, Dictionary<string, string> tags = null)
        {
            if (string.IsNullOrEmpty(name)) return;

            var key = GetMetricKey(name, tags);
            _histograms.AddOrUpdate(key,
                new HistogramState
                {
                    Values = new List<double> { value },
                    Sum = value,
                    LastUpdated = DateTime.UtcNow
                },
                (k, existing) =>
                {
                    lock (existing.Values)
                    {
                        existing.Values.Add(value);
                        existing.Sum += value;

                        // Keep only last 1000 values for percentile calculation
                        if (existing.Values.Count > 1000)
                        {
                            existing.Values.RemoveAt(0);
                        }
                    }
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public MetricsSnapshot GetSnapshot()
        {
            var snapshot = new MetricsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                System = _lastSystemMetrics ?? new SystemMetrics()
            };

            // Counters
            foreach (var kvp in _counters)
            {
                var name = ExtractMetricName(kvp.Key);
                var state = kvp.Value;
                var timeDiff = (DateTime.UtcNow - state.LastUpdated).TotalSeconds;

                snapshot.Counters[name] = new CounterMetric
                {
                    Value = state.Value,
                    Rate = timeDiff > 0 ? state.Value / timeDiff : 0,
                    LastUpdated = state.LastUpdated
                };
            }

            // Gauges
            foreach (var kvp in _gauges)
            {
                var name = ExtractMetricName(kvp.Key);
                var state = kvp.Value;

                snapshot.Gauges[name] = new GaugeMetric
                {
                    Value = state.Value,
                    Min = state.Min,
                    Max = state.Max,
                    Average = state.Count > 0 ? state.Sum / state.Count : 0,
                    LastUpdated = state.LastUpdated
                };
            }

            // Timers
            foreach (var kvp in _timers)
            {
                var name = ExtractMetricName(kvp.Key);
                var state = kvp.Value;
                var timeDiff = (DateTime.UtcNow - state.LastUpdated).TotalSeconds;

                snapshot.Timers[name] = new TimerMetric
                {
                    Count = state.Count,
                    TotalMilliseconds = state.TotalMs,
                    MinMilliseconds = state.MinMs,
                    MaxMilliseconds = state.MaxMs,
                    AverageMilliseconds = state.Count > 0 ? state.TotalMs / state.Count : 0,
                    Rate = timeDiff > 0 ? state.Count / timeDiff : 0
                };
            }

            // Histograms
            foreach (var kvp in _histograms)
            {
                var name = ExtractMetricName(kvp.Key);
                var state = kvp.Value;

                lock (state.Values)
                {
                    if (state.Values.Any())
                    {
                        var sorted = state.Values.OrderBy(x => x).ToArray();
                        snapshot.Histograms[name] = new HistogramMetric
                        {
                            Count = sorted.Length,
                            Min = sorted.First(),
                            Max = sorted.Last(),
                            Average = state.Sum / sorted.Length,
                            Sum = state.Sum,
                            Percentiles = CalculatePercentiles(sorted)
                        };
                    }
                }
            }

            return snapshot;
        }

        public void Reset()
        {
            _counters.Clear();
            _gauges.Clear();
            _timers.Clear();
            _histograms.Clear();
            _logger.LogDebug("Metrics reset");
        }

        private string GetMetricKey(string name, Dictionary<string, string> tags)
        {
            if (tags == null || !tags.Any())
                return name;

            var tagString = string.Join(",", tags.OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return $"{name}#{tagString}";
        }

        private string ExtractMetricName(string key)
        {
            var index = key.IndexOf('#');
            return index > 0 ? key.Substring(0, index) : key;
        }

        private double[] CalculatePercentiles(double[] sorted)
        {
            if (!sorted.Any()) return new double[4];

            return new[]
            {
                GetPercentile(sorted, 50),  // 50th percentile
                GetPercentile(sorted, 75),  // 75th percentile
                GetPercentile(sorted, 95),  // 95th percentile
                GetPercentile(sorted, 99)   // 99th percentile
            };
        }

        private double GetPercentile(double[] sorted, double percentile)
        {
            if (!sorted.Any()) return 0;

            var index = (percentile / 100.0) * (sorted.Length - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);

            if (lower == upper) return sorted[lower];

            var weight = index - lower;
            return sorted[lower] * (1 - weight) + sorted[upper] * weight;
        }

        private void CollectSystemMetrics(object state)
        {
            try
            {
                _currentProcess.Refresh();

                var totalMemory = GC.GetTotalMemory(false);
                var uptime = DateTime.UtcNow - _startTime;

                _lastSystemMetrics = new SystemMetrics
                {
                    MemoryUsedBytes = totalMemory,
                    GcCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
                    ThreadCount = _currentProcess.Threads.Count,
                    Uptime = uptime
                };

                // Set gauge metrics for system stats
                SetGauge("system.memory.used", totalMemory);
                SetGauge("system.gc.collections", _lastSystemMetrics.GcCollections);
                SetGauge("system.threads", _lastSystemMetrics.ThreadCount);
                SetGauge("system.uptime.seconds", uptime.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect system metrics");
            }
        }

        public void Dispose()
        {
            _systemMetricsTimer?.Dispose();
            _currentProcess?.Dispose();
        }

        private class CounterState
        {
            public double Value { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        private class GaugeState
        {
            public double Value { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public double Sum { get; set; }
            public long Count { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        private class TimerState
        {
            public long Count { get; set; }
            public double TotalMs { get; set; }
            public double MinMs { get; set; }
            public double MaxMs { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        private class HistogramState
        {
            public List<double> Values { get; set; } = new();
            public double Sum { get; set; }
            public DateTime LastUpdated { get; set; }
        }
    }
}