using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.FileWatcher;

/// <summary>
/// High-performance file watcher with debouncing and intelligent change detection
/// </summary>
public class SmartFileWatcher : IDisposable
{
    private readonly ILogger<SmartFileWatcher> _logger;
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, FileSnapshot> _snapshots;
    private readonly ConcurrentDictionary<string, DateTime> _lastChangeTimes;
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers;
    private readonly TimeSpan _debounceInterval;
    private readonly int _maxConcurrentWatchers;
    private bool _disposed;

    // Events
    public event EventHandler<FileChangedEventArgs> FileChanged;
    public event EventHandler<FileChangedEventArgs> FileCreated;
    public event EventHandler<FileChangedEventArgs> FileDeleted;
    public event EventHandler<FileRenamedEventArgs> FileRenamed;
    public event EventHandler<DirectoryChangedEventArgs> DirectoryChanged;

    public SmartFileWatcher(ILogger<SmartFileWatcher> logger = null, TimeSpan? debounceInterval = null)
    {
        _logger = logger;
        _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
        _snapshots = new ConcurrentDictionary<string, FileSnapshot>();
        _lastChangeTimes = new ConcurrentDictionary<string, DateTime>();
        _debounceTimers = new ConcurrentDictionary<string, Timer>();
        _debounceInterval = debounceInterval ?? TimeSpan.FromMilliseconds(500);
        _maxConcurrentWatchers = Environment.ProcessorCount * 2;
    }

    /// <summary>
    /// Start watching a directory with intelligent filtering
    /// </summary>
    public void WatchDirectory(string path, WatchOptions options = null)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SmartFileWatcher));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");

        options ??= new WatchOptions();
        
        if (_watchers.ContainsKey(path))
        {
            _logger?.LogWarning("Already watching directory: {Path}", path);
            return;
        }

        if (_watchers.Count >= _maxConcurrentWatchers)
        {
            _logger?.LogWarning("Maximum concurrent watchers reached ({Max})", _maxConcurrentWatchers);
            CleanupOldestWatcher();
        }

        var watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = options.NotifyFilter,
            Filter = options.Filter ?? "*.*",
            IncludeSubdirectories = options.IncludeSubdirectories,
            EnableRaisingEvents = false
        };

        // Configure buffer size for high-frequency changes
        watcher.InternalBufferSize = options.BufferSize;

        // Attach event handlers with debouncing
        watcher.Changed += (s, e) => HandleFileChange(e, options);
        watcher.Created += (s, e) => HandleFileCreated(e, options);
        watcher.Deleted += (s, e) => HandleFileDeleted(e, options);
        watcher.Renamed += (s, e) => HandleFileRenamed(e, options);
        watcher.Error += (s, e) => HandleWatcherError(path, e);

        // Take initial snapshot for change detection
        if (options.TrackContentChanges)
        {
            TakeDirectorySnapshot(path, options);
        }

        _watchers[path] = watcher;
        watcher.EnableRaisingEvents = true;

        _logger?.LogInformation("Started watching directory: {Path}", path);
    }

    /// <summary>
    /// Watch a specific file with content hash tracking
    /// </summary>
    public void WatchFile(string filePath, bool trackContent = true)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SmartFileWatcher));
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}");

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        var options = new WatchOptions
        {
            Filter = fileName,
            IncludeSubdirectories = false,
            TrackContentChanges = trackContent,
            IgnorePatterns = new List<string>()
        };

        if (trackContent)
        {
            _snapshots[filePath] = CreateFileSnapshot(filePath);
        }

        WatchDirectory(directory, options);
    }

    /// <summary>
    /// Stop watching a directory or file
    /// </summary>
    public void StopWatching(string path)
    {
        if (_watchers.TryRemove(path, out var watcher))
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _logger?.LogInformation("Stopped watching: {Path}", path);
        }

        // Clean up associated data
        _snapshots.TryRemove(path, out _);
        _lastChangeTimes.TryRemove(path, out _);
        
        if (_debounceTimers.TryRemove(path, out var timer))
        {
            timer?.Dispose();
        }
    }

    /// <summary>
    /// Get statistics about watched paths
    /// </summary>
    public WatcherStatistics GetStatistics()
    {
        return new WatcherStatistics
        {
            WatchedPaths = _watchers.Keys.ToList(),
            TotalWatchers = _watchers.Count,
            TrackedFiles = _snapshots.Count,
            PendingDebounces = _debounceTimers.Count,
            MaxConcurrentWatchers = _maxConcurrentWatchers
        };
    }

    private void HandleFileChange(FileSystemEventArgs e, WatchOptions options)
    {
        if (ShouldIgnore(e.FullPath, options)) return;

        var key = e.FullPath;
        
        // Debounce rapid changes
        if (_debounceTimers.TryGetValue(key, out var existingTimer))
        {
            existingTimer?.Dispose();
        }

        var timer = new Timer(_ =>
        {
            ProcessFileChange(e, options);
            _debounceTimers.TryRemove(key, out _);
        }, null, _debounceInterval, Timeout.InfiniteTimeSpan);

        _debounceTimers[key] = timer;
    }

    private void ProcessFileChange(FileSystemEventArgs e, WatchOptions options)
    {
        try
        {
            if (!File.Exists(e.FullPath)) return;

            // Check if content actually changed
            if (options.TrackContentChanges && _snapshots.TryGetValue(e.FullPath, out var oldSnapshot))
            {
                var newSnapshot = CreateFileSnapshot(e.FullPath);
                
                if (oldSnapshot.ContentHash == newSnapshot.ContentHash)
                {
                    _logger?.LogDebug("File modified but content unchanged: {Path}", e.FullPath);
                    return;
                }

                _snapshots[e.FullPath] = newSnapshot;
            }

            _lastChangeTimes[e.FullPath] = DateTime.UtcNow;

            FileChanged?.Invoke(this, new FileChangedEventArgs
            {
                FullPath = e.FullPath,
                ChangeType = e.ChangeType,
                Timestamp = DateTime.UtcNow
            });

            _logger?.LogDebug("File changed: {Path}", e.FullPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing file change: {Path}", e.FullPath);
        }
    }

    private void HandleFileCreated(FileSystemEventArgs e, WatchOptions options)
    {
        if (ShouldIgnore(e.FullPath, options)) return;

        if (options.TrackContentChanges && File.Exists(e.FullPath))
        {
            _snapshots[e.FullPath] = CreateFileSnapshot(e.FullPath);
        }

        FileCreated?.Invoke(this, new FileChangedEventArgs
        {
            FullPath = e.FullPath,
            ChangeType = e.ChangeType,
            Timestamp = DateTime.UtcNow
        });

        _logger?.LogDebug("File created: {Path}", e.FullPath);
    }

    private void HandleFileDeleted(FileSystemEventArgs e, WatchOptions options)
    {
        if (ShouldIgnore(e.FullPath, options)) return;

        _snapshots.TryRemove(e.FullPath, out _);
        _lastChangeTimes.TryRemove(e.FullPath, out _);

        FileDeleted?.Invoke(this, new FileChangedEventArgs
        {
            FullPath = e.FullPath,
            ChangeType = e.ChangeType,
            Timestamp = DateTime.UtcNow
        });

        _logger?.LogDebug("File deleted: {Path}", e.FullPath);
    }

    private void HandleFileRenamed(RenamedEventArgs e, WatchOptions options)
    {
        if (ShouldIgnore(e.FullPath, options)) return;

        // Update snapshot tracking
        if (_snapshots.TryRemove(e.OldFullPath, out var snapshot))
        {
            _snapshots[e.FullPath] = snapshot;
        }

        FileRenamed?.Invoke(this, new FileRenamedEventArgs
        {
            FullPath = e.FullPath,
            OldFullPath = e.OldFullPath,
            ChangeType = e.ChangeType,
            Timestamp = DateTime.UtcNow
        });

        _logger?.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
    }

    private void HandleWatcherError(string path, ErrorEventArgs e)
    {
        _logger?.LogError(e.GetException(), "Watcher error for path: {Path}", path);

        // Attempt to restart the watcher
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            
            if (_watchers.TryGetValue(path, out var watcher))
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.EnableRaisingEvents = true;
                    _logger?.LogInformation("Restarted watcher for path: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to restart watcher for path: {Path}", path);
                }
            }
        });
    }

    private bool ShouldIgnore(string path, WatchOptions options)
    {
        if (options.IgnorePatterns == null || !options.IgnorePatterns.Any())
            return false;

        var fileName = Path.GetFileName(path);
        
        return options.IgnorePatterns.Any(pattern =>
        {
            if (pattern.StartsWith("*"))
                return fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            if (pattern.EndsWith("*"))
                return fileName.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
            return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        });
    }

    private FileSnapshot CreateFileSnapshot(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        
        return new FileSnapshot
        {
            Path = filePath,
            Size = fileInfo.Length,
            LastModified = fileInfo.LastWriteTimeUtc,
            ContentHash = ComputeFileHash(filePath)
        };
    }

    private string ComputeFileHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            
            // For large files, only hash first and last chunks
            if (stream.Length > 10 * 1024 * 1024) // 10MB
            {
                var buffer = new byte[8192];
                var hash = new List<byte>();
                
                // Hash first 8KB
                stream.Read(buffer, 0, buffer.Length);
                hash.AddRange(sha256.ComputeHash(buffer));
                
                // Hash last 8KB
                stream.Seek(-8192, SeekOrigin.End);
                stream.Read(buffer, 0, buffer.Length);
                hash.AddRange(sha256.ComputeHash(buffer));
                
                // Include file size in hash
                hash.AddRange(BitConverter.GetBytes(stream.Length));
                
                return Convert.ToBase64String(sha256.ComputeHash(hash.ToArray()));
            }
            else
            {
                var hashBytes = sha256.ComputeHash(stream);
                return Convert.ToBase64String(hashBytes);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compute hash for file: {Path}", filePath);
            return string.Empty;
        }
    }

    private void TakeDirectorySnapshot(string path, WatchOptions options)
    {
        try
        {
            var searchOption = options.IncludeSubdirectories 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            var files = Directory.GetFiles(path, options.Filter ?? "*.*", searchOption)
                .Where(f => !ShouldIgnore(f, options));

            foreach (var file in files)
            {
                _snapshots[file] = CreateFileSnapshot(file);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to take directory snapshot: {Path}", path);
        }
    }

    private void CleanupOldestWatcher()
    {
        var oldestPath = _lastChangeTimes
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        if (oldestPath != null)
        {
            StopWatching(oldestPath);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        foreach (var timer in _debounceTimers.Values)
        {
            timer?.Dispose();
        }

        _watchers.Clear();
        _snapshots.Clear();
        _lastChangeTimes.Clear();
        _debounceTimers.Clear();

        _disposed = true;
    }
}

// Supporting classes
public class WatchOptions
{
    public string Filter { get; set; } = "*.*";
    public bool IncludeSubdirectories { get; set; } = false;
    public bool TrackContentChanges { get; set; } = true;
    public List<string> IgnorePatterns { get; set; } = new() { "*.tmp", "~*", ".git", "node_modules", "bin", "obj" };
    public NotifyFilters NotifyFilter { get; set; } = 
        NotifyFilters.FileName | 
        NotifyFilters.LastWrite | 
        NotifyFilters.Size;
    public int BufferSize { get; set; } = 64 * 1024; // 64KB
}

public class FileSnapshot
{
    public string Path { get; set; }
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ContentHash { get; set; }
}

public class FileChangedEventArgs : EventArgs
{
    public string FullPath { get; set; }
    public WatcherChangeTypes ChangeType { get; set; }
    public DateTime Timestamp { get; set; }
}

public class FileRenamedEventArgs : FileChangedEventArgs
{
    public string OldFullPath { get; set; }
}

public class DirectoryChangedEventArgs : EventArgs
{
    public string Path { get; set; }
    public List<string> AddedFiles { get; set; } = new();
    public List<string> ModifiedFiles { get; set; } = new();
    public List<string> DeletedFiles { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class WatcherStatistics
{
    public List<string> WatchedPaths { get; set; }
    public int TotalWatchers { get; set; }
    public int TrackedFiles { get; set; }
    public int PendingDebounces { get; set; }
    public int MaxConcurrentWatchers { get; set; }
}
