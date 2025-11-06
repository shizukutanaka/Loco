// John Carmack: "You can't improve what you don't measure"
// Rob Pike: "Measure twice, optimize once"

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Loco.Core.Practical;

/// <summary>
/// Simple monitoring system - Track application metrics, performance, errors
/// Real-time dashboards, alerts, aggregation
/// </summary>
public class SimpleMonitor
{
    private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
    private readonly ConcurrentQueue<Event> _events = new();
    private readonly SimpleLogger _logger;
    private readonly int _maxEvents;

    public SimpleMonitor(int maxEvents = 10000, SimpleLogger? logger = null)
    {
        _maxEvents = maxEvents;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleMonitor));
    }

    // Record metric
    public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        var metric = _metrics.GetOrAdd(name, _ => new MetricData { Name = name });

        lock (metric)
        {
            metric.Count++;
            metric.Sum += value;
            metric.Min = Math.Min(metric.Min, value);
            metric.Max = Math.Max(metric.Max, value);
            metric.LastValue = value;
            metric.LastUpdated = DateTime.UtcNow;

            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (!metric.Tags.ContainsKey(tag.Key))
                        metric.Tags[tag.Key] = new HashSet<string>();
                    metric.Tags[tag.Key].Add(tag.Value);
                }
            }
        }
    }

    // Increment counter
    public void Increment(string name, long value = 1)
    {
        var metric = _metrics.GetOrAdd(name, _ => new MetricData { Name = name });
        lock (metric)
        {
            metric.Count += value;
            metric.LastUpdated = DateTime.UtcNow;
        }
    }

    // Record timing
    public void RecordTiming(string name, TimeSpan duration)
    {
        RecordMetric(name, duration.TotalMilliseconds);
    }

    // Record event
    public void RecordEvent(string type, string message, Dictionary<string, string>? data = null)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Message = message,
            Data = data ?? new Dictionary<string, string>(),
            Timestamp = DateTime.UtcNow
        };

        _events.Enqueue(evt);

        // Keep queue size under limit
        while (_events.Count > _maxEvents)
        {
            _events.TryDequeue(out _);
        }

        _logger.Debug($"Event recorded: {type} - {message}");
    }

    // Get metric
    public MetricData? GetMetric(string name)
    {
        return _metrics.TryGetValue(name, out var metric) ? metric : null;
    }

    // Get all metrics
    public List<MetricData> GetAllMetrics()
    {
        return _metrics.Values.ToList();
    }

    // Get recent events
    public List<Event> GetRecentEvents(int count = 100)
    {
        return _events.TakeLast(count).ToList();
    }

    // Get snapshot
    public MonitorSnapshot GetSnapshot()
    {
        return new MonitorSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Metrics = GetAllMetrics(),
            RecentEvents = GetRecentEvents(50)
        };
    }

    // Clear all data
    public void Clear()
    {
        _metrics.Clear();
        while (_events.TryDequeue(out _)) { }
        _logger.Info("Monitor data cleared");
    }
}

public class MetricData
{
    public string Name { get; set; } = "";
    public long Count { get; set; }
    public double Sum { get; set; }
    public double Min { get; set; } = double.MaxValue;
    public double Max { get; set; } = double.MinValue;
    public double LastValue { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, HashSet<string>> Tags { get; set; } = new();

    public double Average => Count > 0 ? Sum / Count : 0;
}

public class Event
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public Dictionary<string, string> Data { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class MonitorSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<MetricData> Metrics { get; set; } = new();
    public List<Event> RecentEvents { get; set; } = new();
}

/// <summary>
/// Performance monitor for timing operations
/// </summary>
public class PerformanceMonitor
{
    private readonly SimpleMonitor _monitor;
    private readonly Stopwatch _sw = new();

    public PerformanceMonitor(SimpleMonitor monitor)
    {
        _monitor = monitor;
    }

    // Time an operation
    public T Time<T>(string name, Func<T> operation)
    {
        _sw.Restart();
        try
        {
            return operation();
        }
        finally
        {
            _sw.Stop();
            _monitor.RecordTiming(name, _sw.Elapsed);
        }
    }

    // Time an async operation
    public async Task<T> TimeAsync<T>(string name, Func<Task<T>> operation)
    {
        _sw.Restart();
        try
        {
            return await operation();
        }
        finally
        {
            _sw.Stop();
            _monitor.RecordTiming(name, _sw.Elapsed);
        }
    }

    // Auto-timing using disposable
    public IDisposable StartTimer(string name)
    {
        return new Timer(_monitor, name);
    }

    private class Timer : IDisposable
    {
        private readonly SimpleMonitor _monitor;
        private readonly string _name;
        private readonly Stopwatch _sw;

        public Timer(SimpleMonitor monitor, string name)
        {
            _monitor = monitor;
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            _monitor.RecordTiming(_name, _sw.Elapsed);
        }
    }
}

/// <summary>
/// Alert system
/// </summary>
public class AlertSystem
{
    private readonly SimpleMonitor _monitor;
    private readonly List<AlertRule> _rules = new();
    private readonly SimpleNotificationService? _notificationService;
    private readonly SimpleLogger _logger;

    public AlertSystem(SimpleMonitor monitor, SimpleNotificationService? notificationService = null, SimpleLogger? logger = null)
    {
        _monitor = monitor;
        _notificationService = notificationService;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(AlertSystem));
    }

    // Add alert rule
    public void AddRule(AlertRule rule)
    {
        _rules.Add(rule);
        _logger.Info($"Alert rule added: {rule.Name}");
    }

    // Check all rules
    public async Task CheckRulesAsync()
    {
        foreach (var rule in _rules)
        {
            var metric = _monitor.GetMetric(rule.MetricName);
            if (metric == null) continue;

            var triggered = rule.Condition(metric);
            if (triggered && !rule.IsTriggered)
            {
                rule.IsTriggered = true;
                rule.LastTriggered = DateTime.UtcNow;
                await OnAlertTriggeredAsync(rule, metric);
            }
            else if (!triggered && rule.IsTriggered)
            {
                rule.IsTriggered = false;
            }
        }
    }

    private async Task OnAlertTriggeredAsync(AlertRule rule, MetricData metric)
    {
        _logger.Warning($"Alert triggered: {rule.Name}");

        if (_notificationService != null)
        {
            await _notificationService.SendToChannelAsync(
                "console",
                $"Alert: {rule.Name}",
                $"Metric '{metric.Name}': {metric.LastValue:F2} (avg: {metric.Average:F2})"
            );
        }

        _monitor.RecordEvent("alert", rule.Name, new Dictionary<string, string>
        {
            ["metric"] = metric.Name,
            ["value"] = metric.LastValue.ToString()
        });
    }
}

public class AlertRule
{
    public string Name { get; set; } = "";
    public string MetricName { get; set; } = "";
    public Func<MetricData, bool> Condition { get; set; } = null!;
    public bool IsTriggered { get; set; }
    public DateTime? LastTriggered { get; set; }
}

/// <summary>
/// System resource monitor
/// </summary>
public class ResourceMonitor
{
    private readonly SimpleMonitor _monitor;
    private readonly SimpleBackgroundTaskRunner _taskRunner;

    public ResourceMonitor(SimpleMonitor monitor)
    {
        _monitor = monitor;
        _taskRunner = new SimpleBackgroundTaskRunner();

        // Start monitoring
        _taskRunner.RunPeriodic(async ct =>
        {
            RecordSystemMetrics();
            await Task.CompletedTask;
        }, TimeSpan.FromSeconds(5), "ResourceMonitor");
    }

    private void RecordSystemMetrics()
    {
        // CPU and Memory
        var process = Process.GetCurrentProcess();

        _monitor.RecordMetric("system.memory.bytes", process.WorkingSet64);
        _monitor.RecordMetric("system.memory.mb", process.WorkingSet64 / 1024.0 / 1024.0);
        _monitor.RecordMetric("system.cpu.time", process.TotalProcessorTime.TotalMilliseconds);
        _monitor.RecordMetric("system.threads.count", process.Threads.Count);

        // GC stats
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        _monitor.RecordMetric("gc.gen0.collections", gen0);
        _monitor.RecordMetric("gc.gen1.collections", gen1);
        _monitor.RecordMetric("gc.gen2.collections", gen2);
        _monitor.RecordMetric("gc.memory.bytes", GC.GetTotalMemory(false));
    }

    public void Dispose()
    {
        _taskRunner.Dispose();
    }
}

/// <summary>
/// Dashboard generator
/// </summary>
public class MonitorDashboard
{
    private readonly SimpleMonitor _monitor;

    public MonitorDashboard(SimpleMonitor monitor)
    {
        _monitor = monitor;
    }

    public string GenerateTextDashboard()
    {
        var snapshot = _monitor.GetSnapshot();
        var output = new System.Text.StringBuilder();

        output.AppendLine("=".PadRight(80, '='));
        output.AppendLine($"MONITORING DASHBOARD - {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss}");
        output.AppendLine("=".PadRight(80, '='));
        output.AppendLine();

        // Top metrics
        output.AppendLine("TOP METRICS:");
        foreach (var metric in snapshot.Metrics.OrderByDescending(m => m.Count).Take(10))
        {
            output.AppendLine($"  {metric.Name,-40} Count: {metric.Count,10:N0}  Avg: {metric.Average,10:F2}");
        }
        output.AppendLine();

        // Recent events
        output.AppendLine("RECENT EVENTS:");
        foreach (var evt in snapshot.RecentEvents.TakeLast(5))
        {
            output.AppendLine($"  [{evt.Timestamp:HH:mm:ss}] {evt.Type}: {evt.Message}");
        }
        output.AppendLine();

        return output.ToString();
    }

    public Dictionary<string, object> GenerateJsonDashboard()
    {
        var snapshot = _monitor.GetSnapshot();
        return new Dictionary<string, object>
        {
            ["timestamp"] = snapshot.Timestamp,
            ["metrics"] = snapshot.Metrics.Select(m => new
            {
                name = m.Name,
                count = m.Count,
                average = m.Average,
                min = m.Min,
                max = m.Max,
                last = m.LastValue
            }),
            ["events"] = snapshot.RecentEvents.Select(e => new
            {
                type = e.Type,
                message = e.Message,
                timestamp = e.Timestamp
            })
        };
    }
}

/// <summary>
/// Example usage
/// </summary>
public class MonitoringExamples
{
    public static async Task Examples()
    {
        var monitor = new SimpleMonitor();

        // Record metrics
        monitor.Increment("requests.total");
        monitor.RecordMetric("response.time", 150.5);
        monitor.RecordMetric("memory.usage", 1024.0);

        // Record with tags
        monitor.RecordMetric("api.latency", 45.2, new Dictionary<string, string>
        {
            ["endpoint"] = "/api/users",
            ["method"] = "GET"
        });

        // Record events
        monitor.RecordEvent("error", "Database connection failed", new Dictionary<string, string>
        {
            ["database"] = "production",
            ["error"] = "timeout"
        });

        // Performance monitoring
        var perfMonitor = new PerformanceMonitor(monitor);

        var result = perfMonitor.Time("database.query", () =>
        {
            // Simulate work
            System.Threading.Thread.Sleep(50);
            return 42;
        });

        // Using disposable timer
        using (perfMonitor.StartTimer("complex.operation"))
        {
            // Do work
            await Task.Delay(100);
        }

        // Resource monitoring
        var resourceMonitor = new ResourceMonitor(monitor);

        // Wait a bit for metrics
        await Task.Delay(1000);

        // Dashboard
        var dashboard = new MonitorDashboard(monitor);
        var textDashboard = dashboard.GenerateTextDashboard();
        Console.WriteLine(textDashboard);

        // Alerts
        var alertSystem = new AlertSystem(monitor);

        alertSystem.AddRule(new AlertRule
        {
            Name = "High Memory Usage",
            MetricName = "system.memory.mb",
            Condition = metric => metric.LastValue > 500
        });

        alertSystem.AddRule(new AlertRule
        {
            Name = "Slow Response Time",
            MetricName = "response.time",
            Condition = metric => metric.Average > 100
        });

        await alertSystem.CheckRulesAsync();

        // Get specific metric
        var memoryMetric = monitor.GetMetric("system.memory.mb");
        if (memoryMetric != null)
        {
            Console.WriteLine($"Memory: {memoryMetric.LastValue:F2} MB");
        }

        resourceMonitor.Dispose();
    }
}