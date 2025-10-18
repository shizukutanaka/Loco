using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Triggers;

/// <summary>
/// Event types that can trigger workflow execution.
/// </summary>
public enum EventType
{
    Webhook,
    SystemCpu,
    SystemMemory,
    SystemDisk,
    Custom
}

/// <summary>
/// Event trigger configuration.
/// </summary>
public class EventTriggerConfig
{
    /// <summary>
    /// Type of event to trigger on.
    /// </summary>
    public EventType Type { get; set; }

    /// <summary>
    /// For webhook events: URL path to listen on (e.g., "/webhook/deploy").
    /// </summary>
    public string? WebhookPath { get; set; }

    /// <summary>
    /// For webhook events: HTTP methods to accept (default: POST).
    /// </summary>
    public List<string> HttpMethods { get; set; } = new() { "POST" };

    /// <summary>
    /// For webhook events: Required authentication token.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// For system events: Threshold value (CPU %, Memory %, Disk %).
    /// </summary>
    public double? Threshold { get; set; }

    /// <summary>
    /// For system events: Check interval in seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Whether threshold must be exceeded continuously.
    /// </summary>
    public bool RequireContinuous { get; set; } = false;

    /// <summary>
    /// Number of continuous checks required.
    /// </summary>
    public int ContinuousChecks { get; set; } = 2;

    /// <summary>
    /// Cooldown period after triggering (seconds).
    /// </summary>
    public int CooldownSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Custom event name.
    /// </summary>
    public string? CustomEventName { get; set; }

    /// <summary>
    /// Whether the trigger is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Event data passed to triggered workflows.
/// </summary>
public class TriggerEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public EventType Type { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// System metrics for monitoring.
/// </summary>
public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public Dictionary<string, double> DiskUsagePercent { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event-based trigger for workflows.
/// </summary>
public class EventTrigger : IDisposable
{
    private readonly EventTriggerConfig _config;
    private readonly ILogger? _logger;
    private readonly Timer? _systemCheckTimer;
    private readonly ConcurrentQueue<TriggerEvent> _eventQueue = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private DateTime _lastTriggerTime = DateTime.MinValue;
    private int _continuousThresholdHits = 0;
    private bool _disposed;

    public event Func<TriggerEvent, Task>? OnEventTriggered;

    public EventTrigger(EventTriggerConfig config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        // Start system monitoring if needed
        if (_config.Type is EventType.SystemCpu or EventType.SystemMemory or EventType.SystemDisk)
        {
            _systemCheckTimer = new Timer(
                async _ => await CheckSystemMetricsAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(_config.CheckIntervalSeconds));

            _logger?.LogInformation(
                "Started system monitoring for {Type} with threshold {Threshold}% every {Interval}s",
                _config.Type,
                _config.Threshold,
                _config.CheckIntervalSeconds);
        }
    }

    /// <summary>
    /// Manually triggers an event.
    /// </summary>
    public async Task TriggerAsync(TriggerEvent evt)
    {
        if (!_config.Enabled)
        {
            _logger?.LogDebug("Trigger is disabled, ignoring event");
            return;
        }

        // Check cooldown
        if (DateTime.UtcNow - _lastTriggerTime < TimeSpan.FromSeconds(_config.CooldownSeconds))
        {
            _logger?.LogDebug("Event ignored due to cooldown period");
            return;
        }

        _eventQueue.Enqueue(evt);
        await ProcessEventsAsync();
    }

    /// <summary>
    /// Handles webhook requests.
    /// </summary>
    public async Task<bool> HandleWebhookAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_config.Type != EventType.Webhook)
        {
            response.StatusCode = 404;
            return false;
        }

        // Check method
        if (!_config.HttpMethods.Contains(request.HttpMethod, StringComparer.OrdinalIgnoreCase))
        {
            response.StatusCode = 405;
            await WriteResponseAsync(response, new { error = "Method not allowed" });
            return false;
        }

        // Check auth token
        if (!string.IsNullOrEmpty(_config.AuthToken))
        {
            var authHeader = request.Headers["Authorization"];
            var expectedAuth = $"Bearer {_config.AuthToken}";

            if (authHeader != expectedAuth)
            {
                response.StatusCode = 401;
                await WriteResponseAsync(response, new { error = "Unauthorized" });
                return false;
            }
        }

        // Read request body
        string? body = null;
        Dictionary<string, object>? data = null;

        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            body = await reader.ReadToEndAsync();

            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
            }
            catch
            {
                data = new Dictionary<string, object> { ["raw"] = body };
            }
        }

        // Trigger event
        var evt = new TriggerEvent
        {
            Type = EventType.Webhook,
            Source = $"{request.HttpMethod} {request.Url?.PathAndQuery}",
            Data = data ?? new Dictionary<string, object>()
        };

        await TriggerAsync(evt);

        response.StatusCode = 200;
        await WriteResponseAsync(response, new { success = true, eventId = evt.Id });

        return true;
    }

    /// <summary>
    /// Checks system metrics and triggers if threshold exceeded.
    /// </summary>
    private async Task CheckSystemMetricsAsync()
    {
        try
        {
            var metrics = await GetSystemMetricsAsync();
            double? currentValue = null;

            currentValue = _config.Type switch
            {
                EventType.SystemCpu => metrics.CpuUsagePercent,
                EventType.SystemMemory => metrics.MemoryUsagePercent,
                EventType.SystemDisk => metrics.DiskUsagePercent.Values.DefaultIfEmpty(0).Max(),
                _ => null
            };

            if (!currentValue.HasValue || !_config.Threshold.HasValue)
                return;

            bool thresholdExceeded = currentValue.Value >= _config.Threshold.Value;

            if (thresholdExceeded)
            {
                _continuousThresholdHits++;

                _logger?.LogDebug(
                    "{Type} at {Value:F1}% (threshold: {Threshold}%, hits: {Hits})",
                    _config.Type,
                    currentValue.Value,
                    _config.Threshold.Value,
                    _continuousThresholdHits);

                // Check if continuous requirement met
                if (!_config.RequireContinuous || _continuousThresholdHits >= _config.ContinuousChecks)
                {
                    var evt = new TriggerEvent
                    {
                        Type = _config.Type,
                        Source = "SystemMonitor",
                        Data = new Dictionary<string, object>
                        {
                            ["value"] = currentValue.Value,
                            ["threshold"] = _config.Threshold.Value,
                            ["cpuPercent"] = metrics.CpuUsagePercent,
                            ["memoryPercent"] = metrics.MemoryUsagePercent,
                            ["diskPercent"] = metrics.DiskUsagePercent
                        }
                    };

                    await TriggerAsync(evt);
                    _continuousThresholdHits = 0;
                }
            }
            else
            {
                _continuousThresholdHits = 0;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking system metrics");
        }
    }

    /// <summary>
    /// Gets current system metrics.
    /// </summary>
    private async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        var metrics = new SystemMetrics();

        // CPU usage - simplified approach without PerformanceCounter
        try
        {
            var process = Process.GetCurrentProcess();
            var startTime = process.TotalProcessorTime;
            var startTimestamp = DateTime.UtcNow;

            await Task.Delay(100);

            var endTime = process.TotalProcessorTime;
            var endTimestamp = DateTime.UtcNow;

            var cpuUsedMs = (endTime - startTime).TotalMilliseconds;
            var totalMsPassed = (endTimestamp - startTimestamp).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            metrics.CpuUsagePercent = cpuUsageTotal * 100;
        }
        catch
        {
            metrics.CpuUsagePercent = 0;
        }

        // Memory usage
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            metrics.MemoryUsagePercent = (double)gcInfo.HeapSizeBytes / gcInfo.TotalAvailableMemoryBytes * 100;
        }
        catch
        {
            metrics.MemoryUsagePercent = 0;
        }

        // Disk usage
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            foreach (var drive in drives)
            {
                var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
                var usagePercent = (double)usedSpace / drive.TotalSize * 100;
                metrics.DiskUsagePercent[drive.Name] = usagePercent;
            }
        }
        catch
        {
            // Ignore disk errors
        }

        return metrics;
    }

    /// <summary>
    /// Processes queued events.
    /// </summary>
    private async Task ProcessEventsAsync()
    {
        if (!await _processingLock.WaitAsync(0))
            return; // Already processing

        try
        {
            while (_eventQueue.TryDequeue(out var evt))
            {
                try
                {
                    _logger?.LogInformation(
                        "Triggering workflow for event {EventId} ({Type}) from {Source}",
                        evt.Id,
                        evt.Type,
                        evt.Source);

                    if (OnEventTriggered != null)
                    {
                        await OnEventTriggered.Invoke(evt);
                    }

                    _lastTriggerTime = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing trigger event {EventId}", evt.Id);
                }
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    /// <summary>
    /// Writes JSON response.
    /// </summary>
    private async Task WriteResponseAsync(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        await response.OutputStream.WriteAsync(buffer);
    }

    /// <summary>
    /// Gets trigger statistics.
    /// </summary>
    public EventTriggerStats GetStats()
    {
        return new EventTriggerStats
        {
            Type = _config.Type,
            Enabled = _config.Enabled,
            LastTriggerTime = _lastTriggerTime == DateTime.MinValue ? null : _lastTriggerTime,
            QueuedEvents = _eventQueue.Count,
            ContinuousHits = _continuousThresholdHits
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _systemCheckTimer?.Dispose();
            _processingLock?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Event trigger statistics.
/// </summary>
public class EventTriggerStats
{
    public EventType Type { get; set; }
    public bool Enabled { get; set; }
    public DateTime? LastTriggerTime { get; set; }
    public int QueuedEvents { get; set; }
    public int ContinuousHits { get; set; }
}

