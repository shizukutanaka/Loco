using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Triggers;

/// <summary>
/// File change types that can trigger workflows.
/// </summary>
[Flags]
public enum FileChangeType
{
    Created = 1,
    Modified = 2,
    Deleted = 4,
    Renamed = 8,
    All = Created | Modified | Deleted | Renamed
}

/// <summary>
/// Configuration for file watching trigger.
/// </summary>
public class FileWatchConfig
{
    /// <summary>
    /// Path to watch (file or directory).
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// File filter pattern (e.g., "*.txt", "*.cs").
    /// </summary>
    public string Filter { get; set; } = "*.*";

    /// <summary>
    /// Types of changes to watch for.
    /// </summary>
    public FileChangeType ChangeTypes { get; set; } = FileChangeType.All;

    /// <summary>
    /// Whether to watch subdirectories.
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = false;

    /// <summary>
    /// Debounce delay in milliseconds to prevent duplicate triggers.
    /// </summary>
    public int DebounceMs { get; set; } = 500;

    /// <summary>
    /// Minimum time between triggers in seconds.
    /// </summary>
    public int CooldownSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of queued events before dropping.
    /// </summary>
    public int MaxQueueSize { get; set; } = 100;
}

/// <summary>
/// File change event information.
/// </summary>
public class FileChangeEvent
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public FileChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? OldFilePath { get; set; }
}

/// <summary>
/// Watches file system changes and triggers workflows.
/// </summary>
public class FileWatcherTrigger : IDisposable
{
    private readonly FileWatchConfig _config;
    private readonly ILogger? _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentQueue<FileChangeEvent> _eventQueue = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastTriggerTime = new();
    private readonly Timer _debounceTimer;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    public event Func<FileChangeEvent, Task>? OnFileChanged;

    public FileWatcherTrigger(FileWatchConfig config, ILogger? logger = null)
    {
        _config = config;
        _logger = logger;

        // Validate path
        var fullPath = Path.GetFullPath(_config.Path);
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Watch path not found: {fullPath}");
        }

        // Setup watcher
        var watchPath = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath)! : fullPath;
        var watchFilter = File.Exists(fullPath) ? Path.GetFileName(fullPath) : _config.Filter;

        _watcher = new FileSystemWatcher(watchPath, watchFilter)
        {
            IncludeSubdirectories = _config.IncludeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        // Subscribe to events
        if ((_config.ChangeTypes & FileChangeType.Created) != 0)
            _watcher.Created += OnWatcherEvent;

        if ((_config.ChangeTypes & FileChangeType.Modified) != 0)
            _watcher.Changed += OnWatcherEvent;

        if ((_config.ChangeTypes & FileChangeType.Deleted) != 0)
            _watcher.Deleted += OnWatcherEvent;

        if ((_config.ChangeTypes & FileChangeType.Renamed) != 0)
            _watcher.Renamed += OnWatcherRenamed;

        // Setup debounce timer
        _debounceTimer = new Timer(
            _ => ProcessQueueAsync().GetAwaiter().GetResult(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
    }

    /// <summary>
    /// Starts watching for file changes.
    /// </summary>
    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        _logger?.LogInformation(
            "File watcher started: {Path} (filter: {Filter}, recursive: {Recursive})",
            _config.Path,
            _config.Filter,
            _config.IncludeSubdirectories);
    }

    /// <summary>
    /// Stops watching for file changes.
    /// </summary>
    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _logger?.LogInformation("File watcher stopped: {Path}", _config.Path);
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        var changeType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => FileChangeType.Created,
            WatcherChangeTypes.Changed => FileChangeType.Modified,
            WatcherChangeTypes.Deleted => FileChangeType.Deleted,
            _ => FileChangeType.Modified
        };

        var changeEvent = new FileChangeEvent
        {
            FilePath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = changeType,
            Timestamp = DateTime.UtcNow
        };

        EnqueueEvent(changeEvent);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        var changeEvent = new FileChangeEvent
        {
            FilePath = e.FullPath,
            FileName = e.Name ?? Path.GetFileName(e.FullPath),
            ChangeType = FileChangeType.Renamed,
            Timestamp = DateTime.UtcNow,
            OldFilePath = e.OldFullPath
        };

        EnqueueEvent(changeEvent);
    }

    private void EnqueueEvent(FileChangeEvent changeEvent)
    {
        // Check queue size
        if (_eventQueue.Count >= _config.MaxQueueSize)
        {
            _logger?.LogWarning("Event queue full, dropping oldest events");
            _eventQueue.TryDequeue(out _);
        }

        _eventQueue.Enqueue(changeEvent);

        // Reset debounce timer
        _debounceTimer.Change(_config.DebounceMs, Timeout.Infinite);

        _logger?.LogDebug(
            "File event queued: {ChangeType} - {FilePath}",
            changeEvent.ChangeType,
            changeEvent.FilePath);
    }

    private async Task ProcessQueueAsync()
    {
        var processedFiles = new HashSet<string>();

        while (_eventQueue.TryDequeue(out var changeEvent))
        {
            try
            {
                // Skip duplicates in same batch
                if (processedFiles.Contains(changeEvent.FilePath))
                    continue;

                // Check cooldown
                if (_lastTriggerTime.TryGetValue(changeEvent.FilePath, out var lastTime))
                {
                    var elapsed = (DateTime.UtcNow - lastTime).TotalSeconds;
                    if (elapsed < _config.CooldownSeconds)
                    {
                        _logger?.LogDebug(
                            "Skipping trigger due to cooldown: {FilePath} ({Elapsed:F1}s < {Cooldown}s)",
                            changeEvent.FilePath,
                            elapsed,
                            _config.CooldownSeconds);
                        continue;
                    }
                }

                processedFiles.Add(changeEvent.FilePath);
                _lastTriggerTime[changeEvent.FilePath] = DateTime.UtcNow;

                _logger?.LogInformation(
                    "File change detected: {ChangeType} - {FilePath}",
                    changeEvent.ChangeType,
                    changeEvent.FilePath);

                // Trigger event
                if (OnFileChanged != null)
                {
                    await OnFileChanged.Invoke(changeEvent);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing file change event: {FilePath}", changeEvent.FilePath);
            }
        }
    }

    /// <summary>
    /// Gets current statistics.
    /// </summary>
    public FileWatcherStats GetStats()
    {
        return new FileWatcherStats
        {
            IsRunning = _watcher.EnableRaisingEvents,
            QueuedEvents = _eventQueue.Count,
            TrackedFiles = _lastTriggerTime.Count,
            WatchPath = _config.Path,
            Filter = _config.Filter
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// File watcher statistics.
/// </summary>
public class FileWatcherStats
{
    public bool IsRunning { get; set; }
    public int QueuedEvents { get; set; }
    public int TrackedFiles { get; set; }
    public string WatchPath { get; set; } = "";
    public string Filter { get; set; } = "";
}

