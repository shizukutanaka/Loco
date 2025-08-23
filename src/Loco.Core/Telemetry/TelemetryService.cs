using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Telemetry;

/// <summary>
/// High-performance telemetry service for production monitoring
/// Inspired by Carmack's measurement-driven development
/// </summary>
public sealed class TelemetryService : IDisposable
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly ConcurrentDictionary<string, MetricData> _metrics;
    private readonly ConcurrentQueue<Event> _events;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock;
    private long _totalEvents;
    private bool _disposed;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
        _metrics = new ConcurrentDictionary<string, MetricData>();
        _events = new ConcurrentQueue<Event>();
        _flushLock = new SemaphoreSlim(1, 1);
        
        // Flush metrics every 30 seconds
        _flushTimer = new Timer(async _ => await FlushMetricsAsync(), null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordMetric(string name, double value, Dictionary<string, string> tags = null)
    {
        var metric = _metrics.GetOrAdd(name, _ => new MetricData(name));
        metric.Record(value, tags);
        Interlocked.Increment(ref _totalEvents);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDuration(string name, TimeSpan duration, Dictionary<string, string> tags = null)
    {
        RecordMetric(name, duration.TotalMilliseconds, tags);
    }

    public IDisposable MeasureDuration(string name, Dictionary<string, string> tags = null)
    {
        return new DurationMeasurement(this, name, tags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordEvent(string name, EventLevel level, string message, Dictionary<string, object> properties = null)
    {
        var evt = new Event
        {
            Name = name,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow,
            Properties = properties
        };

        _events.Enqueue(evt);
        
        // Keep only last 10000 events in memory
        while (_events.Count > 10000)
        {
            _events.TryDequeue(out _);
        }
    }

    public MetricSummary GetMetricSummary(string name)
    {
        if (!_metrics.TryGetValue(name, out var metric))
            return null;

        return metric.GetSummary();
    }

    public Dictionary<string, MetricSummary> GetAllMetrics()
    {
        return _metrics.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetSummary());
    }

    public SystemHealth GetSystemHealth()
    {
        var process = Process.GetCurrentProcess();
        
        return new SystemHealth
        {
            CpuUsage = GetCpuUsage(),
            MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            GcGen0Collections = GC.CollectionCount(0),
            GcGen1Collections = GC.CollectionCount(1),
            GcGen2Collections = GC.CollectionCount(2),
            TotalEvents = Interlocked.Read(ref _totalEvents),
            Uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime()
        };
    }

    private double GetCpuUsage()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            Thread.Sleep(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return Math.Round(cpuUsageTotal * 100, 2);
        }
        catch
        {
            return 0;
        }
    }

    private async Task FlushMetricsAsync()
    {
        if (!await _flushLock.WaitAsync(0))
            return; // Skip if already flushing

        try
        {
            var metrics = GetAllMetrics();
            var health = GetSystemHealth();
            
            // Log critical metrics
            if (health.CpuUsage > 80)
            {
                _logger.LogWarning("High CPU usage detected: {CpuUsage}%", health.CpuUsage);
            }
            
            if (health.MemoryUsageMB > 500)
            {
                _logger.LogWarning("High memory usage detected: {MemoryUsage} MB", health.MemoryUsageMB);
            }

            // Export metrics (could send to external service)
            await ExportMetricsAsync(metrics, health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private async Task ExportMetricsAsync(Dictionary<string, MetricSummary> metrics, SystemHealth health)
    {
        // TODO: Send to telemetry service (Application Insights, Datadog, etc.)
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _flushTimer?.Dispose();
        _flushLock?.Dispose();
        _disposed = true;
    }

    private class MetricData
    {
        private readonly string _name;
        private readonly object _lock = new();
        private readonly List<double> _values = new();
        private readonly Dictionary<string, long> _tagCounts = new();
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private double _sum;
        private long _count;

        public MetricData(string name)
        {
            _name = name;
        }

        public void Record(double value, Dictionary<string, string> tags)
        {
            lock (_lock)
            {
                _values.Add(value);
                if (_values.Count > 1000) // Keep last 1000 values
                    _values.RemoveAt(0);
                
                _sum += value;
                _count++;
                _min = Math.Min(_min, value);
                _max = Math.Max(_max, value);

                if (tags != null)
                {
                    var tagKey = string.Join(",", tags.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    _tagCounts.TryGetValue(tagKey, out var count);
                    _tagCounts[tagKey] = count + 1;
                }
            }
        }

        public MetricSummary GetSummary()
        {
            lock (_lock)
            {
                if (_count == 0)
                    return new MetricSummary { Name = _name };

                var sortedValues = _values.OrderBy(v => v).ToList();
                
                return new MetricSummary
                {
                    Name = _name,
                    Count = _count,
                    Min = _min,
                    Max = _max,
                    Average = _sum / _count,
                    Median = sortedValues.Count > 0 ? sortedValues[sortedValues.Count / 2] : 0,
                    P95 = sortedValues.Count > 0 ? sortedValues[(int)(sortedValues.Count * 0.95)] : 0,
                    P99 = sortedValues.Count > 0 ? sortedValues[(int)(sortedValues.Count * 0.99)] : 0,
                    TagDistribution = new Dictionary<string, long>(_tagCounts)
                };
            }
        }
    }

    private class DurationMeasurement : IDisposable
    {
        private readonly TelemetryService _service;
        private readonly string _name;
        private readonly Dictionary<string, string> _tags;
        private readonly Stopwatch _stopwatch;

        public DurationMeasurement(TelemetryService service, string name, Dictionary<string, string> tags)
        {
            _service = service;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _service.RecordDuration(_name, _stopwatch.Elapsed, _tags);
        }
    }
}

public class MetricSummary
{
    public string Name { get; set; }
    public long Count { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Average { get; set; }
    public double Median { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public Dictionary<string, long> TagDistribution { get; set; }
}

public class SystemHealth
{
    public double CpuUsage { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public int GcGen0Collections { get; set; }
    public int GcGen1Collections { get; set; }
    public int GcGen2Collections { get; set; }
    public long TotalEvents { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class Event
{
    public string Name { get; set; }
    public EventLevel Level { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Properties { get; set; }
}

public enum EventLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}