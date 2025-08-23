using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Components.Triggers;

/// <summary>
/// Time-based trigger - fires at specific times
/// Following Rob Pike's simplicity principle
/// </summary>
public class TimeTrigger : ComponentBase, ITrigger
{
    private Timer _timer;
    private readonly ILogger<TimeTrigger> _logger;
    private bool _isRunning;
    
    public event EventHandler<TriggerEventArgs> Triggered;
    public bool IsRunning => _isRunning;
    
    public TimeTrigger(ILogger<TimeTrigger> logger = null) 
        : base("time.schedule", "Time Trigger", "Triggers at specified times", ComponentType.Trigger)
    {
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return Task.CompletedTask;
        
        var hour = GetConfig<int>("hour", 0);
        var minute = GetConfig<int>("minute", 0);
        var interval = GetConfig<int>("interval", 0); // in minutes
        var runOnce = GetConfig<bool>("runOnce", false);
        
        if (interval > 0)
        {
            // Interval-based trigger
            var intervalMs = interval * 60 * 1000;
            _timer = new Timer(OnTimerElapsed, null, 0, intervalMs);
            _logger?.LogInformation($"Started interval trigger: every {interval} minutes");
        }
        else
        {
            // Daily schedule trigger
            var now = DateTime.Now;
            var scheduledTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            
            if (scheduledTime < now)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }
            
            var delay = scheduledTime - now;
            var period = runOnce ? Timeout.InfiniteTimeSpan : TimeSpan.FromDays(1);
            
            _timer = new Timer(OnTimerElapsed, null, delay, period);
            _logger?.LogInformation($"Started schedule trigger: {hour:D2}:{minute:D2} daily");
        }
        
        _isRunning = true;
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _timer?.Dispose();
        _timer = null;
        _isRunning = false;
        _logger?.LogInformation("Stopped time trigger");
        return Task.CompletedTask;
    }
    
    private void OnTimerElapsed(object state)
    {
        try
        {
            var args = new TriggerEventArgs
            {
                TriggerId = Id,
                Data = { ["time"] = DateTime.Now }
            };
            
            Triggered?.Invoke(this, args);
            _logger?.LogDebug($"Time trigger fired at {DateTime.Now}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in time trigger");
        }
    }
}

/// <summary>
/// File system watcher trigger
/// </summary>
public class FileWatcherTrigger : ComponentBase, ITrigger
{
    private FileSystemWatcher _watcher;
    private readonly ILogger<FileWatcherTrigger> _logger;
    private bool _isRunning;
    
    public event EventHandler<TriggerEventArgs> Triggered;
    public bool IsRunning => _isRunning;
    
    public FileWatcherTrigger(ILogger<FileWatcherTrigger> logger = null)
        : base("file.watch", "File Watcher", "Monitors file system changes", ComponentType.Trigger)
    {
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return Task.CompletedTask;
        
        var path = GetConfig<string>("path", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        var filter = GetConfig<string>("filter", "*.*");
        var includeSubdirectories = GetConfig<bool>("includeSubdirectories", false);
        var events = GetConfig<string>("events", "created,changed,deleted,renamed");
        
        _watcher = new FileSystemWatcher(path, filter)
        {
            IncludeSubdirectories = includeSubdirectories,
            EnableRaisingEvents = false
        };
        
        // Subscribe to events based on configuration
        if (events.Contains("created", StringComparison.OrdinalIgnoreCase))
            _watcher.Created += OnFileSystemEvent;
        if (events.Contains("changed", StringComparison.OrdinalIgnoreCase))
            _watcher.Changed += OnFileSystemEvent;
        if (events.Contains("deleted", StringComparison.OrdinalIgnoreCase))
            _watcher.Deleted += OnFileSystemEvent;
        if (events.Contains("renamed", StringComparison.OrdinalIgnoreCase))
            _watcher.Renamed += OnFileSystemEvent;
        
        _watcher.EnableRaisingEvents = true;
        _isRunning = true;
        
        _logger?.LogInformation($"Started file watcher on {path} with filter {filter}");
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        
        _isRunning = false;
        _logger?.LogInformation("Stopped file watcher");
        return Task.CompletedTask;
    }
    
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        try
        {
            var args = new TriggerEventArgs
            {
                TriggerId = Id,
                Data =
                {
                    ["path"] = e.FullPath,
                    ["changeType"] = e.ChangeType.ToString(),
                    ["name"] = e.Name
                }
            };
            
            if (e is RenamedEventArgs renamed)
            {
                args.Data["oldPath"] = renamed.OldFullPath;
                args.Data["oldName"] = renamed.OldName;
            }
            
            Triggered?.Invoke(this, args);
            _logger?.LogDebug($"File system event: {e.ChangeType} - {e.FullPath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in file watcher trigger");
        }
    }
}

/// <summary>
/// HTTP webhook trigger
/// </summary>
public class WebhookTrigger : ComponentBase, ITrigger
{
    private CancellationTokenSource _cts;
    private Task _listenerTask;
    private readonly ILogger<WebhookTrigger> _logger;
    private bool _isRunning;
    
    public event EventHandler<TriggerEventArgs> Triggered;
    public bool IsRunning => _isRunning;
    
    public WebhookTrigger(ILogger<WebhookTrigger> logger = null)
        : base("webhook.listener", "Webhook Trigger", "Listens for HTTP webhooks", ComponentType.Trigger)
    {
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return Task.CompletedTask;
        
        var port = GetConfig<int>("port", 8080);
        var path = GetConfig<string>("path", "/webhook");
        
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => StartHttpListener(port, path, _cts.Token));
        
        _isRunning = true;
        _logger?.LogInformation($"Started webhook listener on port {port} at path {path}");
        return Task.CompletedTask;
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning) return;
        
        _cts?.Cancel();
        
        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        
        _cts?.Dispose();
        _isRunning = false;
        _logger?.LogInformation("Stopped webhook listener");
    }
    
    private async Task StartHttpListener(int port, string path, CancellationToken cancellationToken)
    {
        // Simplified HTTP listener implementation
        // In production, use ASP.NET Core for proper HTTP handling
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}

/// <summary>
/// System event trigger (startup, idle, etc.)
/// </summary>
public class SystemEventTrigger : ComponentBase, ITrigger
{
    private Timer _idleTimer;
    private DateTime _lastActivity;
    private readonly ILogger<SystemEventTrigger> _logger;
    private bool _isRunning;
    
    public event EventHandler<TriggerEventArgs> Triggered;
    public bool IsRunning => _isRunning;
    
    public SystemEventTrigger(ILogger<SystemEventTrigger> logger = null)
        : base("system.event", "System Event", "Monitors system events", ComponentType.Trigger)
    {
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return Task.CompletedTask;
        
        var eventType = GetConfig<string>("eventType", "startup");
        
        switch (eventType.ToLower())
        {
            case "startup":
                // Fire immediately on startup
                Task.Run(() =>
                {
                    var args = new TriggerEventArgs
                    {
                        TriggerId = Id,
                        Data = { ["event"] = "startup" }
                    };
                    Triggered?.Invoke(this, args);
                });
                break;
                
            case "idle":
                var idleMinutes = GetConfig<int>("idleMinutes", 10);
                _lastActivity = DateTime.Now;
                _idleTimer = new Timer(CheckIdle, idleMinutes, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                break;
                
            case "shutdown":
                // Register for shutdown event
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                break;
        }
        
        _isRunning = true;
        _logger?.LogInformation($"Started system event trigger: {eventType}");
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _idleTimer?.Dispose();
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _isRunning = false;
        return Task.CompletedTask;
    }
    
    private void CheckIdle(object state)
    {
        var idleMinutes = (int)state;
        var idleTime = DateTime.Now - _lastActivity;
        
        if (idleTime.TotalMinutes >= idleMinutes)
        {
            var args = new TriggerEventArgs
            {
                TriggerId = Id,
                Data = 
                { 
                    ["event"] = "idle",
                    ["idleMinutes"] = idleTime.TotalMinutes
                }
            };
            Triggered?.Invoke(this, args);
            _lastActivity = DateTime.Now; // Reset to avoid repeated triggers
        }
    }
    
    private void OnProcessExit(object sender, EventArgs e)
    {
        var args = new TriggerEventArgs
        {
            TriggerId = Id,
            Data = { ["event"] = "shutdown" }
        };
        Triggered?.Invoke(this, args);
    }
}
