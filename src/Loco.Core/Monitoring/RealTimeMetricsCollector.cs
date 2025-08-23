using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Loco.Core.Monitoring;

/// <summary>
/// Real-time metrics collection and analysis system
/// Optimized for minimal overhead following John Carmack's principles
/// </summary>
public sealed class RealTimeMetricsCollector : BackgroundService
{
    private readonly ILogger<RealTimeMetricsCollector> _logger;
    private readonly Channel<MetricEvent> _metricsChannel;
    private readonly ConcurrentDictionary<string, MetricAggregator> _aggregators;
    private readonly ConcurrentDictionary<string, TimeSeries> _timeSeries;
    private readonly Timer _aggregationTimer;
    private readonly Timer _cleanupTimer;
    
    // Performance counters
    private long _eventsProcessed;
    private long _eventsDropped;
    private readonly Stopwatch _uptime;
    
    // Configuration
    private readonly int _maxChannelSize;
    private readonly TimeSpan _aggregationInterval;
    private readonly TimeSpan _retentionPeriod;
    
    public class MetricEvent
    {
        public string Name { get; init; }
        public double Value { get; init; }
        public DateTime Timestamp { get; init; }
        public Dictionary<string, string> Tags { get; init; }
        public MetricType Type { get; init; }
    }
    
    public enum MetricType
    {
        Counter,
        Gauge,
        Histogram,
        Timer
    }
    
    private class MetricAggregator
    {
        private readonly object _lock = new();
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private long _count;
        private readonly List<double> _values = new();
        
        public void Add(double value)
        {
            lock (_lock)
            {
                _sum += value;
                _min = Math.Min(_min, value);
                _max = Math.Max(_max, value);
                _count++;
                _values.Add(value);
            }
        }
        
        public MetricSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    return new MetricSnapshot();
                }
                
                _values.Sort();
                
                return new MetricSnapshot
                {
                    Count = _count,
                    Sum = _sum,
                    Min = _min,
                    Max = _max,
                    Mean = _sum / _count,
                    Median = GetPercentile(50),
                    P95 = GetPercentile(95),
                    P99 = GetPercentile(99),
                    StdDev = CalculateStdDev()
                };
            }
        }
        
        public void Reset()
        {
            lock (_lock)
            {
                _sum = 0;
                _min = double.MaxValue;
                _max = double.MinValue;
                _count = 0;
                _values.Clear();
            }
        }
        
        private double GetPercentile(int percentile)
        {
            if (_values.Count == 0) return 0;
            
            var index = (int)Math.Ceiling(percentile / 100.0 * _values.Count) - 1;
            index = Math.Max(0, Math.Min(index, _values.Count - 1));
            return _values[index];
        }
        
        private double CalculateStdDev()
        {
            if (_count <= 1) return 0;
            
            var mean = _sum / _count;
            var variance = _values.Sum(v => Math.Pow(v - mean, 2)) / (_count - 1);
            return Math.Sqrt(variance);
        }
    }
    
    public class MetricSnapshot
    {
        public long Count { get; init; }
        public double Sum { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double Mean { get; init; }
        public double Median { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
        public double StdDev { get; init; }
    }
    
    private class TimeSeries
    {
        private readonly CircularBuffer<TimeSeriesPoint> _points;
        private readonly object _lock = new();
        
        public TimeSeries(int maxPoints = 1000)
        {
            _points = new CircularBuffer<TimeSeriesPoint>(maxPoints);
        }
        
        public void Add(double value, DateTime timestamp)
        {
            lock (_lock)
            {
                _points.Add(new TimeSeriesPoint { Value = value, Timestamp = timestamp });
            }
        }
        
        public List<TimeSeriesPoint> GetPoints(DateTime since)
        {
            lock (_lock)
            {
                return _points.Where(p => p.Timestamp >= since).ToList();
            }
        }
        
        public void Cleanup(DateTime before)
        {
            lock (_lock)
            {
                while (_points.Count > 0 && _points.Peek().Timestamp < before)
                {
                    _points.Dequeue();
                }
            }
        }
    }
    
    public struct TimeSeriesPoint
    {
        public double Value { get; init; }
        public DateTime Timestamp { get; init; }
    }
    
    private class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;
        
        public int Count => _count;
        public int Capacity { get; }
        
        public CircularBuffer(int capacity)
        {
            Capacity = capacity;
            _buffer = new T[capacity];
        }
        
        public void Add(T item)
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;
            
            if (_count < Capacity)
            {
                _count++;
            }
            else
            {
                _head = (_head + 1) % Capacity;
            }
        }
        
        public T Dequeue()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
            
            var item = _buffer[_head];
            _head = (_head + 1) % Capacity;
            _count--;
            return item;
        }
        
        public T Peek()
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");
            
            return _buffer[_head];
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            var index = _head;
            for (int i = 0; i < _count; i++)
            {
                yield return _buffer[index];
                index = (index + 1) % Capacity;
            }
        }
        
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    public RealTimeMetricsCollector(
        ILogger<RealTimeMetricsCollector> logger,
        int maxChannelSize = 10000,
        TimeSpan? aggregationInterval = null,
        TimeSpan? retentionPeriod = null)
    {
        _logger = logger;
        _maxChannelSize = maxChannelSize;
        _aggregationInterval = aggregationInterval ?? TimeSpan.FromSeconds(10);
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromHours(1);
        
        _metricsChannel = Channel.CreateBounded<MetricEvent>(new BoundedChannelOptions(maxChannelSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        
        _aggregators = new ConcurrentDictionary<string, MetricAggregator>();
        _timeSeries = new ConcurrentDictionary<string, TimeSeries>();
        _uptime = Stopwatch.StartNew();
        
        // Setup timers
        _aggregationTimer = new Timer(AggregateMetrics, null, _aggregationInterval, _aggregationInterval);
        _cleanupTimer = new Timer(CleanupOldData, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        _logger.LogInformation("Real-time metrics collector initialized");
    }
    
    /// <summary>
    /// Record a metric event (non-blocking)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordMetric(string name, double value, MetricType type = MetricType.Gauge, Dictionary<string, string> tags = null)
    {
        var metricEvent = new MetricEvent
        {
            Name = name,
            Value = value,
            Timestamp = DateTime.UtcNow,
            Type = type,
            Tags = tags ?? new Dictionary<string, string>()
        };
        
        if (!_metricsChannel.Writer.TryWrite(metricEvent))
        {
            Interlocked.Increment(ref _eventsDropped);
        }
    }
    
    /// <summary>
    /// Record a counter increment
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCounter(string name, double value = 1, Dictionary<string, string> tags = null)
    {
        RecordMetric(name, value, MetricType.Counter, tags);
    }
    
    /// <summary>
    /// Record a timing
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTiming(string name, long milliseconds, Dictionary<string, string> tags = null)
    {
        RecordMetric(name, milliseconds, MetricType.Timer, tags);
    }
    
    /// <summary>
    /// Create a timer scope for automatic timing
    /// </summary>
    public IDisposable StartTimer(string name, Dictionary<string, string> tags = null)
    {
        return new TimerScope(this, name, tags);
    }
    
    private class TimerScope : IDisposable
    {
        private readonly RealTimeMetricsCollector _collector;
        private readonly string _name;
        private readonly Dictionary<string, string> _tags;
        private readonly Stopwatch _stopwatch;
        
        public TimerScope(RealTimeMetricsCollector collector, string name, Dictionary<string, string> tags)
        {
            _collector = collector;
            _name = name;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _collector.RecordTiming(_name, _stopwatch.ElapsedMilliseconds, _tags);
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting metrics processing loop");
        
        await foreach (var metricEvent in _metricsChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                ProcessMetricEvent(metricEvent);
                Interlocked.Increment(ref _eventsProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing metric event");
            }
        }
        
        _logger.LogInformation("Metrics processing loop stopped");
    }
    
    private void ProcessMetricEvent(MetricEvent metricEvent)
    {
        var key = GenerateKey(metricEvent.Name, metricEvent.Tags);
        
        // Add to aggregator
        var aggregator = _aggregators.GetOrAdd(key, _ => new MetricAggregator());
        aggregator.Add(metricEvent.Value);
        
        // Add to time series
        var series = _timeSeries.GetOrAdd(key, _ => new TimeSeries());
        series.Add(metricEvent.Value, metricEvent.Timestamp);
    }
    
    private string GenerateKey(string name, Dictionary<string, string> tags)
    {
        if (tags == null || tags.Count == 0)
            return name;
        
        var sortedTags = tags.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");
        
        return $"{name},{string.Join(",", sortedTags)}";
    }
    
    private void AggregateMetrics(object state)
    {
        try
        {
            var snapshots = new Dictionary<string, MetricSnapshot>();
            
            foreach (var kvp in _aggregators)
            {
                var snapshot = kvp.Value.GetSnapshot();
                snapshots[kvp.Key] = snapshot;
                kvp.Value.Reset();
            }
            
            if (snapshots.Count > 0)
            {
                _logger.LogDebug("Aggregated {Count} metrics", snapshots.Count);
                
                // Publish aggregated metrics
                OnMetricsAggregated?.Invoke(snapshots);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating metrics");
        }
    }
    
    private void CleanupOldData(object state)
    {
        try
        {
            var cutoff = DateTime.UtcNow - _retentionPeriod;
            
            foreach (var series in _timeSeries.Values)
            {
                series.Cleanup(cutoff);
            }
            
            _logger.LogDebug("Cleaned up metrics older than {Cutoff}", cutoff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old metrics");
        }
    }
    
    /// <summary>
    /// Get current metrics snapshot
    /// </summary>
    public Dictionary<string, MetricSnapshot> GetCurrentMetrics()
    {
        var result = new Dictionary<string, MetricSnapshot>();
        
        foreach (var kvp in _aggregators)
        {
            result[kvp.Key] = kvp.Value.GetSnapshot();
        }
        
        return result;
    }
    
    /// <summary>
    /// Get time series data
    /// </summary>
    public Dictionary<string, List<TimeSeriesPoint>> GetTimeSeries(DateTime since)
    {
        var result = new Dictionary<string, List<TimeSeriesPoint>>();
        
        foreach (var kvp in _timeSeries)
        {
            result[kvp.Key] = kvp.Value.GetPoints(since);
        }
        
        return result;
    }
    
    /// <summary>
    /// Get collector statistics
    /// </summary>
    public CollectorStatistics GetStatistics()
    {
        return new CollectorStatistics
        {
            EventsProcessed = _eventsProcessed,
            EventsDropped = _eventsDropped,
            DropRate = _eventsProcessed > 0 ? (double)_eventsDropped / (_eventsProcessed + _eventsDropped) : 0,
            UptimeSeconds = _uptime.Elapsed.TotalSeconds,
            MetricCount = _aggregators.Count,
            TimeSeriesCount = _timeSeries.Count,
            EventsPerSecond = _uptime.Elapsed.TotalSeconds > 0 ? _eventsProcessed / _uptime.Elapsed.TotalSeconds : 0
        };
    }
    
    /// <summary>
    /// Event fired when metrics are aggregated
    /// </summary>
    public event Action<Dictionary<string, MetricSnapshot>> OnMetricsAggregated;
    
    public override void Dispose()
    {
        _aggregationTimer?.Dispose();
        _cleanupTimer?.Dispose();
        _metricsChannel.Writer.TryComplete();
        base.Dispose();
    }
}

public class CollectorStatistics
{
    public long EventsProcessed { get; set; }
    public long EventsDropped { get; set; }
    public double DropRate { get; set; }
    public double UptimeSeconds { get; set; }
    public int MetricCount { get; set; }
    public int TimeSeriesCount { get; set; }
    public double EventsPerSecond { get; set; }
}
