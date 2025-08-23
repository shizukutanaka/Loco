using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Diagnostics;
using Loco.Core.Models;

namespace Loco.Core.Triggers;

/// <summary>
/// Runtime trigger interface (event-driven)
/// </summary>
public interface IRuntimeTrigger
{
    string Id { get; }
    string Type { get; }
    bool Enabled { get; set; }
    event EventHandler<TriggerEventArgs> Triggered;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}



/// <summary>
/// Time-based trigger
/// </summary>
public class TimeTrigger : IRuntimeTrigger
{
    private Timer _timer;
    private readonly TimeSpan _interval;
    private readonly TimeOnly? _specificTime;
    
    public string Id { get; }
    public string Type => "time";
    public bool Enabled { get; set; } = true;
    public event EventHandler<TriggerEventArgs> Triggered;
    
    public TimeTrigger(string id, TimeSpan interval)
    {
        Id = id;
        _interval = interval;
    }
    
    public TimeTrigger(string id, TimeOnly specificTime)
    {
        Id = id;
        _specificTime = specificTime;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_specificTime.HasValue)
        {
            var now = DateTime.Now;
            var targetTime = new DateTime(now.Year, now.Month, now.Day, 
                _specificTime.Value.Hour, _specificTime.Value.Minute, _specificTime.Value.Second);
            
            if (targetTime < now)
                targetTime = targetTime.AddDays(1);
            
            var initialDelay = targetTime - now;
            _timer = new Timer(OnTimerElapsed, null, initialDelay, TimeSpan.FromDays(1));
        }
        else
        {
            _timer = new Timer(OnTimerElapsed, null, _interval, _interval);
        }
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }
    
    private void OnTimerElapsed(object state)
    {
        if (!Enabled) return;
        
        Triggered?.Invoke(this, new TriggerEventArgs
        {
        TriggerId = Id,
        TriggerType = "time",
        Context = new Dictionary<string, object>
        {
        ["triggerType"] = "time",
            ["executionTime"] = DateTime.UtcNow
            }
            });
    }
}

/// <summary>
/// File system watcher trigger
/// </summary>
public class FileSystemTrigger : IRuntimeTrigger, IDisposable
{
    private FileSystemWatcher _watcher;
    private readonly string _path;
    private readonly string _filter;
    private readonly NotifyFilters _notifyFilters;
    
    public string Id { get; }
    public string Type => "fileSystem";
    public bool Enabled { get; set; } = true;
    public event EventHandler<TriggerEventArgs> Triggered;
    
    public FileSystemTrigger(string id, string path, string filter = "*.*", 
        NotifyFilters notifyFilters = NotifyFilters.LastWrite | NotifyFilters.FileName)
    {
        Id = id;
        _path = path;
        _filter = filter;
        _notifyFilters = notifyFilters;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _watcher = new FileSystemWatcher(_path, _filter)
        {
            NotifyFilter = _notifyFilters,
            EnableRaisingEvents = true
        };
        
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemRenamed;
        
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        
        return Task.CompletedTask;
    }
    
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (!Enabled) return;
        
        Triggered?.Invoke(this, new TriggerEventArgs
        {
            TriggerId = Id,
            TriggerType = "fileSystem",
            Context = new Dictionary<string, object>
            {
                ["triggerType"] = "fileSystem",
                ["changeType"] = e.ChangeType.ToString(),
                ["fullPath"] = e.FullPath,
                ["name"] = e.Name
            }
        });
    }
    
    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        if (!Enabled) return;
        
        Triggered?.Invoke(this, new TriggerEventArgs
        {
            TriggerId = Id,
            TriggerType = "fileSystem",
            Context = new Dictionary<string, object>
            {
                ["triggerType"] = "fileSystem",
                ["changeType"] = "Renamed",
                ["fullPath"] = e.FullPath,
                ["name"] = e.Name,
                ["oldFullPath"] = e.OldFullPath,
                ["oldName"] = e.OldName
            }
        });
    }
    
    public void Dispose()
    {
        try
        {
            var stopTask = StopAsync();
            if (!stopTask.IsCompleted)
            {
                _ = Task.Run(async () =>
                {
                    try { await stopTask.ConfigureAwait(false); }
                    catch { /* best-effort during dispose */ }
                });
            }
        }
        catch { /* suppress exceptions in Dispose */ }
    }
}

/// <summary>
/// HTTP webhook trigger
/// </summary>
public class WebhookTrigger : IRuntimeTrigger
{
    private readonly int _port;
    private HttpListener _listener;
    private CancellationTokenSource _cancellationTokenSource;
    
    public string Id { get; }
    public string Type => "webhook";
    public bool Enabled { get; set; } = true;
    public event EventHandler<TriggerEventArgs> Triggered;
    
    public WebhookTrigger(string id, int port = 8080)
    {
        Id = id;
        _port = port;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/webhook/");
        _listener.Start();
        
        _ = Task.Run(async () => await ListenAsync(_cancellationTokenSource.Token));
    }
    
    public Task StopAsync()
    {
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        return Task.CompletedTask;
    }
    
    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                
                if (!Enabled)
                {
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                    continue;
                }
                
                var request = context.Request;
                var body = "";
                
                using (var reader = new StreamReader(request.InputStream))
                {
                    body = await reader.ReadToEndAsync();
                }
                
                Triggered?.Invoke(this, new TriggerEventArgs
                {
                    TriggerId = Id,
                    TriggerType = "webhook",
                    Context = new Dictionary<string, object>
                    {
                        ["triggerType"] = "webhook",
                        ["method"] = request.HttpMethod,
                        ["url"] = request.Url.ToString(),
                        ["body"] = body,
                        ["headers"] = request.Headers.ToString()
                    }
                });
                
                context.Response.StatusCode = 200;
                var responseBytes = System.Text.Encoding.UTF8.GetBytes("OK");
                await context.Response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

/// <summary>
/// System event trigger (battery, network, etc.)
/// </summary>
public class SystemEventTrigger : IRuntimeTrigger
{
    private Timer _timer;
    private readonly SystemEventType _eventType;
    private readonly Dictionary<string, object> _parameters;
    private object _lastState;
    
    public string Id { get; }
    public string Type => "systemEvent";
    public bool Enabled { get; set; } = true;
    public event EventHandler<TriggerEventArgs> Triggered;
    
    public SystemEventTrigger(string id, SystemEventType eventType, Dictionary<string, object> parameters = null)
    {
        Id = id;
        _eventType = eventType;
        _parameters = parameters ?? new Dictionary<string, object>();
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _timer = new Timer(CheckSystemState, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }
    
    private void CheckSystemState(object state)
    {
        if (!Enabled) return;
        
        var currentState = GetSystemState();
        
        if (HasStateChanged(currentState))
        {
            _lastState = currentState;
            
            Triggered?.Invoke(this, new TriggerEventArgs
            {
                TriggerId = Id,
                TriggerType = "systemEvent",
                Context = new Dictionary<string, object>
                {
                    ["triggerType"] = "systemEvent",
                    ["eventType"] = _eventType.ToString(),
                    ["state"] = currentState
                }
            });
        }
    }
    
    private object GetSystemState()
    {
        return _eventType switch
        {
            SystemEventType.BatteryLow => GetBatteryLevel(),
            SystemEventType.NetworkStatus => GetNetworkStatus(),
            SystemEventType.DiskSpaceLow => GetDiskSpace(),
            _ => null
        };
    }
    
    private bool HasStateChanged(object newState)
    {
        if (_lastState == null) return true;
        
        return _eventType switch
        {
            SystemEventType.BatteryLow => CheckBatteryThreshold(newState),
            SystemEventType.NetworkStatus => !_lastState.Equals(newState),
            SystemEventType.DiskSpaceLow => CheckDiskSpaceThreshold(newState),
            _ => false
        };
    }
    
    private int GetBatteryLevel()
    {
        // Simplified - would use actual battery API
        return 100;
    }
    
    private bool GetNetworkStatus()
    {
        return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
    }
    
    private long GetDiskSpace()
    {
        var drive = new DriveInfo("C");
        return drive.AvailableFreeSpace / (1024 * 1024 * 1024); // GB
    }
    
    private bool CheckBatteryThreshold(object newState)
    {
        if (_parameters.TryGetValue("threshold", out var threshold) && threshold is int thresholdValue)
        {
            var lastLevel = (int)(_lastState ?? 100);
            var currentLevel = (int)newState;
            
            return (lastLevel > thresholdValue && currentLevel <= thresholdValue) ||
                   (lastLevel < thresholdValue && currentLevel >= thresholdValue);
        }
        
        return false;
    }
    
    private bool CheckDiskSpaceThreshold(object newState)
    {
        if (_parameters.TryGetValue("threshold", out var threshold) && threshold is long thresholdValue)
        {
            var currentSpace = (long)newState;
            return currentSpace < thresholdValue;
        }
        
        return false;
    }
}



/// <summary>
/// Application launch trigger
/// </summary>
public class ApplicationTrigger : IRuntimeTrigger
{
    private Timer _timer;
    private readonly string _processName;
    private bool _wasRunning;
    
    public string Id { get; }
    public string Type => "application";
    public bool Enabled { get; set; } = true;
    public event EventHandler<TriggerEventArgs> Triggered;
    
    public ApplicationTrigger(string id, string processName)
    {
        Id = id;
        _processName = processName;
    }
    
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _timer = new Timer(CheckProcess, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }
    
    public Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;
        return Task.CompletedTask;
    }
    
    private void CheckProcess(object state)
    {
        if (!Enabled) return;
        
        var isRunning = IsProcessRunning();
        
        if (isRunning && !_wasRunning)
        {
            Triggered?.Invoke(this, new TriggerEventArgs
            {
                TriggerId = Id,
                TriggerType = "application",
                Context = new Dictionary<string, object>
                {
                    ["triggerType"] = "application",
                    ["event"] = "started",
                    ["processName"] = _processName
                }
            });
        }
        else if (!isRunning && _wasRunning)
        {
            Triggered?.Invoke(this, new TriggerEventArgs
            {
                TriggerId = Id,
                TriggerType = "application",
                Context = new Dictionary<string, object>
                {
                    ["triggerType"] = "application",
                    ["event"] = "stopped",
                    ["processName"] = _processName
                }
            });
        }
        
        _wasRunning = isRunning;
    }
    
    private bool IsProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(_processName);
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}