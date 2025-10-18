using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Triggers;

/// <summary>
/// Centralized manager for all workflow triggers.
/// </summary>
public class TriggerManager : IDisposable
{
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, FileWatcherTrigger> _fileWatchers = new();
    private readonly ConcurrentDictionary<string, EventTrigger> _eventTriggers = new();
    private readonly CronScheduler _scheduler;
    private readonly HttpListener? _webhookListener;
    private readonly int _webhookPort;
    private bool _disposed;
    private bool _started;

    public event Func<string, TriggerContext, Task>? OnWorkflowTriggered;

    public TriggerManager(ILogger? logger = null, int webhookPort = 8080)
    {
        _logger = logger;
        _webhookPort = webhookPort;
        _scheduler = new CronScheduler(logger);

        // Setup webhook listener
        try
        {
            _webhookListener = new HttpListener();
            _webhookListener.Prefixes.Add($"http://localhost:{webhookPort}/");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize webhook listener on port {Port}", webhookPort);
        }
    }

    /// <summary>
    /// Registers a file watcher for a workflow.
    /// </summary>
    public void RegisterFileWatcher(string workflowId, FileWatchConfig config)
    {
        var watcherId = $"{workflowId}:{config.Path}";
        var watcher = new FileWatcherTrigger(config, _logger);

        watcher.OnFileChanged += async (evt) =>
        {
            var context = new TriggerContext
            {
                TriggerType = TriggerType.FileChange,
                TriggeredAt = evt.Timestamp,
                Data = new Dictionary<string, object>
                {
                    ["filePath"] = evt.FilePath,
                    ["changeType"] = evt.ChangeType.ToString(),
                    ["oldPath"] = evt.OldFilePath ?? ""
                }
            };

            if (OnWorkflowTriggered != null)
            {
                await OnWorkflowTriggered.Invoke(workflowId, context);
            }
        };

        _fileWatchers[watcherId] = watcher;
        watcher.Start();

        _logger?.LogInformation(
            "Registered file watcher for workflow {WorkflowId}: {Path} ({Filter})",
            workflowId,
            config.Path,
            config.Filter);
    }

    /// <summary>
    /// Registers a cron schedule for a workflow.
    /// </summary>
    public void RegisterSchedule(string workflowId, CronSchedule schedule)
    {
        _scheduler.AddSchedule(workflowId, schedule);

        _logger?.LogInformation(
            "Registered cron schedule for workflow {WorkflowId}: {Expression}",
            workflowId,
            schedule.Expression);
    }

    /// <summary>
    /// Registers an event trigger for a workflow.
    /// </summary>
    public void RegisterEventTrigger(string workflowId, EventTriggerConfig config)
    {
        var triggerId = $"{workflowId}:{config.Type}:{config.WebhookPath ?? config.CustomEventName ?? Guid.NewGuid().ToString()}";
        var trigger = new EventTrigger(config, _logger);

        trigger.OnEventTriggered += async (evt) =>
        {
            var context = new TriggerContext
            {
                TriggerType = TriggerType.Event,
                TriggeredAt = evt.Timestamp,
                Data = evt.Data
            };

            context.Data["eventId"] = evt.Id;
            context.Data["eventType"] = evt.Type.ToString();
            context.Data["eventSource"] = evt.Source ?? "";

            if (OnWorkflowTriggered != null)
            {
                await OnWorkflowTriggered.Invoke(workflowId, context);
            }
        };

        _eventTriggers[triggerId] = trigger;

        _logger?.LogInformation(
            "Registered event trigger for workflow {WorkflowId}: {Type} ({Details})",
            workflowId,
            config.Type,
            config.WebhookPath ?? config.CustomEventName ?? "system");
    }

    /// <summary>
    /// Unregisters all triggers for a workflow.
    /// </summary>
    public void UnregisterWorkflow(string workflowId)
    {
        _logger?.LogInformation("Unregistering triggers for workflow: {WorkflowId}", workflowId);

        // Remove file watchers
        var watchersToRemove = _fileWatchers.Keys.Where(k => k.StartsWith(workflowId + ":")).ToList();
        foreach (var key in watchersToRemove)
        {
            if (_fileWatchers.TryRemove(key, out var watcher))
            {
                watcher.Stop();
                watcher.Dispose();
            }
        }

        // Remove cron schedule
        _scheduler.RemoveSchedule(workflowId);

        // Remove event triggers
        var triggersToRemove = _eventTriggers.Keys.Where(k => k.StartsWith(workflowId + ":")).ToList();
        foreach (var key in triggersToRemove)
        {
            if (_eventTriggers.TryRemove(key, out var trigger))
            {
                trigger.Dispose();
            }
        }
    }

    /// <summary>
    /// Starts all triggers.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started)
        {
            _logger?.LogWarning("TriggerManager already started");
            return;
        }

        _logger?.LogInformation("Starting TriggerManager");

        // Start cron scheduler
        _scheduler.OnScheduledExecution += async (workflowId, scheduledTime) =>
        {
            var context = new TriggerContext
            {
                TriggerType = TriggerType.Schedule,
                TriggeredAt = scheduledTime,
                Data = new Dictionary<string, object>
                {
                    ["scheduledTime"] = scheduledTime
                }
            };

            if (OnWorkflowTriggered != null)
            {
                await OnWorkflowTriggered.Invoke(workflowId, context);
            }
        };

        // Start webhook listener
        if (_webhookListener != null)
        {
            try
            {
                _webhookListener.Start();
                _logger?.LogInformation("Webhook listener started on port {Port}", _webhookPort);

                // Start accepting requests
                _ = Task.Run(async () => await ProcessWebhookRequestsAsync());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start webhook listener");
            }
        }

        _started = true;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops all triggers.
    /// </summary>
    public void Stop()
    {
        if (!_started)
            return;

        _logger?.LogInformation("Stopping TriggerManager");

        // Stop file watchers
        foreach (var watcher in _fileWatchers.Values)
        {
            watcher.Stop();
        }

        // Stop webhook listener
        _webhookListener?.Stop();

        _started = false;
    }

    /// <summary>
    /// Processes incoming webhook requests.
    /// </summary>
    private async Task ProcessWebhookRequestsAsync()
    {
        if (_webhookListener == null)
            return;

        while (_webhookListener.IsListening)
        {
            try
            {
                var context = await _webhookListener.GetContextAsync();
                _ = Task.Run(async () => await HandleWebhookRequestAsync(context));
            }
            catch (HttpListenerException)
            {
                // Listener stopped
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing webhook request");
            }
        }
    }

    /// <summary>
    /// Handles a single webhook request.
    /// </summary>
    private async Task HandleWebhookRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";

            _logger?.LogDebug("Received webhook: {Method} {Path}", request.HttpMethod, path);

            // Find matching event trigger
            var matchingTrigger = _eventTriggers.Values.FirstOrDefault(t =>
            {
                var config = t.GetStats();
                return config.Type == EventType.Webhook &&
                       _eventTriggers.Keys.Any(k => k.Contains(path));
            });

            if (matchingTrigger != null)
            {
                await matchingTrigger.HandleWebhookAsync(request, response);
            }
            else
            {
                response.StatusCode = 404;
                var buffer = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"No matching webhook trigger\"}");
                await response.OutputStream.WriteAsync(buffer);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling webhook request");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    /// <summary>
    /// Manually triggers a custom event.
    /// </summary>
    public async Task TriggerCustomEventAsync(string eventName, Dictionary<string, object>? data = null)
    {
        _logger?.LogInformation("Triggering custom event: {EventName}", eventName);

        var matchingTriggers = _eventTriggers
            .Where(kvp => kvp.Key.Contains($":{EventType.Custom}:") && kvp.Key.EndsWith(eventName))
            .Select(kvp => kvp.Value);

        var evt = new TriggerEvent
        {
            Type = EventType.Custom,
            Source = "Manual",
            Data = data ?? new Dictionary<string, object>()
        };

        foreach (var trigger in matchingTriggers)
        {
            await trigger.TriggerAsync(evt);
        }
    }

    /// <summary>
    /// Gets the next scheduled execution for a workflow.
    /// </summary>
    public DateTime? GetNextScheduledExecution(string workflowId)
    {
        return _scheduler.GetNextExecution(workflowId);
    }

    /// <summary>
    /// Gets upcoming scheduled executions.
    /// </summary>
    public List<ScheduledExecution> GetUpcomingExecutions(TimeSpan window)
    {
        return _scheduler.GetUpcomingExecutions(window);
    }

    /// <summary>
    /// Gets comprehensive trigger statistics.
    /// </summary>
    public TriggerManagerStats GetStats()
    {
        return new TriggerManagerStats
        {
            Started = _started,
            FileWatcherCount = _fileWatchers.Count,
            EventTriggerCount = _eventTriggers.Count,
            SchedulerStats = _scheduler.GetStats(),
            WebhookPort = _webhookPort,
            WebhookListenerActive = _webhookListener?.IsListening ?? false
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();

            foreach (var watcher in _fileWatchers.Values)
            {
                watcher.Dispose();
            }
            _fileWatchers.Clear();

            foreach (var trigger in _eventTriggers.Values)
            {
                trigger.Dispose();
            }
            _eventTriggers.Clear();

            _scheduler.Dispose();
            _webhookListener?.Close();

            _disposed = true;
        }
    }
}

/// <summary>
/// Trigger types.
/// </summary>
public enum TriggerType
{
    Manual,
    Schedule,
    FileChange,
    Event
}

/// <summary>
/// Context information about what triggered a workflow.
/// </summary>
public class TriggerContext
{
    public TriggerType TriggerType { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Trigger manager statistics.
/// </summary>
public class TriggerManagerStats
{
    public bool Started { get; set; }
    public int FileWatcherCount { get; set; }
    public int EventTriggerCount { get; set; }
    public CronSchedulerStats SchedulerStats { get; set; } = new();
    public int WebhookPort { get; set; }
    public bool WebhookListenerActive { get; set; }
}
