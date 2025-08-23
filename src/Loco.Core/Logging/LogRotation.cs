using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging;

/// <summary>
/// Log rotation service with compression and retention management
/// </summary>
public sealed class LogRotationService : IDisposable
{
    private readonly string _logDirectory;
    private readonly string _logFilePattern;
    private readonly LogRotationOptions _options;
    private readonly ILogger<LogRotationService> _logger;
    private readonly Timer _rotationTimer;
    private readonly SemaphoreSlim _rotationSemaphore;
    private StreamWriter _currentWriter;
    private string _currentFilePath;
    private DateTime _currentFileDate;
    private long _currentFileSize;
    private bool _disposed;
    
    public LogRotationService(
        string logDirectory,
        string logFilePattern = "app-{date}.log",
        LogRotationOptions options = null,
        ILogger<LogRotationService> logger = null)
    {
        _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
        _logFilePattern = logFilePattern;
        _options = options ?? new LogRotationOptions();
        _logger = logger;
        _rotationSemaphore = new SemaphoreSlim(1, 1);
        
        // Ensure directory exists
        Directory.CreateDirectory(_logDirectory);
        
        // Initialize current log file
        InitializeCurrentFile();
        
        // Start rotation timer
        var interval = GetTimerInterval();
        _rotationTimer = new Timer(CheckRotation, null, interval, interval);
    }
    
    /// <summary>
    /// Write log entry
    /// </summary>
    public async Task WriteAsync(string message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LogRotationService));
        
        await _rotationSemaphore.WaitAsync();
        try
        {
            // Check if rotation is needed
            if (ShouldRotate())
            {
                await RotateLogFileAsync();
            }
            
            // Write to current file
            await _currentWriter.WriteLineAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            await _currentWriter.FlushAsync();
            
            // Update file size
            _currentFileSize = new FileInfo(_currentFilePath).Length;
        }
        finally
        {
            _rotationSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Write log entry synchronously
    /// </summary>
    public void Write(string message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LogRotationService));
        
        _rotationSemaphore.Wait();
        try
        {
            // Check if rotation is needed
            if (ShouldRotate())
            {
                RotateLogFile();
            }
            
            // Write to current file
            _currentWriter.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            _currentWriter.Flush();
            
            // Update file size
            _currentFileSize = new FileInfo(_currentFilePath).Length;
        }
        finally
        {
            _rotationSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Force log rotation
    /// </summary>
    public async Task RotateAsync()
    {
        await _rotationSemaphore.WaitAsync();
        try
        {
            await RotateLogFileAsync();
        }
        finally
        {
            _rotationSemaphore.Release();
        }
    }
    
    /// <summary>
    /// Get all log files
    /// </summary>
    public IEnumerable<LogFileInfo> GetLogFiles()
    {
        var files = new List<LogFileInfo>();
        
        foreach (var file in Directory.GetFiles(_logDirectory, "*.log*"))
        {
            var info = new FileInfo(file);
            files.Add(new LogFileInfo
            {
                FilePath = file,
                FileName = info.Name,
                Size = info.Length,
                Created = info.CreationTimeUtc,
                Modified = info.LastWriteTimeUtc,
                IsCompressed = info.Extension == ".gz"
            });
        }
        
        return files.OrderByDescending(f => f.Modified);
    }
    
    /// <summary>
    /// Clean up old log files
    /// </summary>
    public async Task CleanupAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-_options.RetentionDays);
                var files = Directory.GetFiles(_logDirectory, "*.log*")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTimeUtc < cutoffDate)
                    .ToList();
                
                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                        _logger?.LogInformation("Deleted old log file: {FileName}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to delete log file: {FileName}", file.Name);
                    }
                }
                
                // Clean up based on max files
                if (_options.MaxFiles > 0)
                {
                    var allFiles = Directory.GetFiles(_logDirectory, "*.log*")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTimeUtc)
                        .Skip(_options.MaxFiles)
                        .ToList();
                    
                    foreach (var file in allFiles)
                    {
                        try
                        {
                            file.Delete();
                            _logger?.LogInformation("Deleted excess log file: {FileName}", file.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to delete log file: {FileName}", file.Name);
                        }
                    }
                }
                
                // Clean up based on total size
                if (_options.MaxTotalSize > 0)
                {
                    var allFiles = Directory.GetFiles(_logDirectory, "*.log*")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.CreationTimeUtc)
                        .ToList();
                    
                    long totalSize = 0;
                    var filesToDelete = new List<FileInfo>();
                    
                    foreach (var file in allFiles)
                    {
                        totalSize += file.Length;
                        if (totalSize > _options.MaxTotalSize)
                        {
                            filesToDelete.Add(file);
                        }
                    }
                    
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                            _logger?.LogInformation("Deleted log file to reduce total size: {FileName}", file.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to delete log file: {FileName}", file.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during log cleanup");
            }
        });
    }
    
    private void InitializeCurrentFile()
    {
        _currentFileDate = GetCurrentPeriodDate();
        _currentFilePath = GetLogFilePath(_currentFileDate);
        
        // Open or create file
        _currentWriter = new StreamWriter(_currentFilePath, append: true)
        {
            AutoFlush = false
        };
        
        // Get current file size
        if (File.Exists(_currentFilePath))
        {
            _currentFileSize = new FileInfo(_currentFilePath).Length;
        }
        
        _logger?.LogInformation("Initialized log file: {FilePath}", _currentFilePath);
    }
    
    private bool ShouldRotate()
    {
        // Check size-based rotation
        if (_options.MaxFileSize > 0 && _currentFileSize >= _options.MaxFileSize)
            return true;
        
        // Check time-based rotation
        var currentPeriod = GetCurrentPeriodDate();
        if (currentPeriod != _currentFileDate)
            return true;
        
        return false;
    }
    
    private async Task RotateLogFileAsync()
    {
        // Close current file
        await _currentWriter.FlushAsync();
        _currentWriter.Dispose();
        
        // Compress if enabled
        if (_options.CompressOnRotation && File.Exists(_currentFilePath))
        {
            await CompressLogFileAsync(_currentFilePath);
        }
        
        // Initialize new file
        InitializeCurrentFile();
        
        // Cleanup old files
        if (_options.AutoCleanup)
        {
            _ = Task.Run(() => CleanupAsync());
        }
        
        _logger?.LogInformation("Rotated log file to: {FilePath}", _currentFilePath);
    }
    
    private void RotateLogFile()
    {
        // Close current file
        _currentWriter.Flush();
        _currentWriter.Dispose();
        
        // Compress if enabled
        if (_options.CompressOnRotation && File.Exists(_currentFilePath))
        {
            CompressLogFile(_currentFilePath);
        }
        
        // Initialize new file
        InitializeCurrentFile();
        
        // Cleanup old files
        if (_options.AutoCleanup)
        {
            Task.Run(() => CleanupAsync());
        }
        
        _logger?.LogInformation("Rotated log file to: {FilePath}", _currentFilePath);
    }
    
    private async Task CompressLogFileAsync(string filePath)
    {
        var compressedPath = $"{filePath}.gz";
        
        try
        {
            using (var originalStream = File.OpenRead(filePath))
            using (var compressedStream = File.Create(compressedPath))
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                await originalStream.CopyToAsync(gzipStream);
            }
            
            // Delete original file
            File.Delete(filePath);
            
            _logger?.LogInformation("Compressed log file: {FileName} -> {CompressedFileName}",
                Path.GetFileName(filePath), Path.GetFileName(compressedPath));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compress log file: {FilePath}", filePath);
        }
    }
    
    private void CompressLogFile(string filePath)
    {
        var compressedPath = $"{filePath}.gz";
        
        try
        {
            using (var originalStream = File.OpenRead(filePath))
            using (var compressedStream = File.Create(compressedPath))
            using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                originalStream.CopyTo(gzipStream);
            }
            
            // Delete original file
            File.Delete(filePath);
            
            _logger?.LogInformation("Compressed log file: {FileName} -> {CompressedFileName}",
                Path.GetFileName(filePath), Path.GetFileName(compressedPath));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to compress log file: {FilePath}", filePath);
        }
    }
    
    private string GetLogFilePath(DateTime date)
    {
        var fileName = _logFilePattern
            .Replace("{date}", date.ToString(_options.DateFormat))
            .Replace("{yyyy}", date.ToString("yyyy"))
            .Replace("{MM}", date.ToString("MM"))
            .Replace("{dd}", date.ToString("dd"))
            .Replace("{HH}", date.ToString("HH"));
        
        // Add sequence number if file exists
        var basePath = Path.Combine(_logDirectory, fileName);
        var filePath = basePath;
        var sequence = 1;
        
        while (File.Exists(filePath) && new FileInfo(filePath).Length >= _options.MaxFileSize)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
            var extension = Path.GetExtension(basePath);
            filePath = Path.Combine(_logDirectory, $"{nameWithoutExtension}.{sequence:D3}{extension}");
            sequence++;
        }
        
        return filePath;
    }
    
    private DateTime GetCurrentPeriodDate()
    {
        var now = DateTime.UtcNow;
        
        return _options.RotationPeriod switch
        {
            RotationPeriod.Hourly => new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc),
            RotationPeriod.Daily => new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc),
            RotationPeriod.Weekly => now.AddDays(-(int)now.DayOfWeek).Date,
            RotationPeriod.Monthly => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => now.Date
        };
    }
    
    private TimeSpan GetTimerInterval()
    {
        return _options.RotationPeriod switch
        {
            RotationPeriod.Hourly => TimeSpan.FromMinutes(1),
            RotationPeriod.Daily => TimeSpan.FromMinutes(5),
            RotationPeriod.Weekly => TimeSpan.FromHours(1),
            RotationPeriod.Monthly => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(5)
        };
    }
    
    private void CheckRotation(object state)
    {
        if (_disposed)
            return;
        
        try
        {
            if (ShouldRotate())
            {
                _rotationSemaphore.Wait();
                try
                {
                    if (ShouldRotate()) // Double-check
                    {
                        RotateLogFile();
                    }
                }
                finally
                {
                    _rotationSemaphore.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during rotation check");
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        _rotationTimer?.Dispose();
        
        _rotationSemaphore.Wait();
        try
        {
            _currentWriter?.Flush();
            _currentWriter?.Dispose();
        }
        finally
        {
            _rotationSemaphore.Release();
        }
        
        _rotationSemaphore.Dispose();
    }
}

/// <summary>
/// Log rotation options
/// </summary>
public sealed class LogRotationOptions
{
    public RotationPeriod RotationPeriod { get; set; } = RotationPeriod.Daily;
    public long MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100MB
    public int RetentionDays { get; set; } = 30;
    public int MaxFiles { get; set; } = 100;
    public long MaxTotalSize { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB
    public bool CompressOnRotation { get; set; } = true;
    public bool AutoCleanup { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
}

/// <summary>
/// Rotation period
/// </summary>
public enum RotationPeriod
{
    Hourly,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Log file information
/// </summary>
public sealed class LogFileInfo
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public long Size { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public bool IsCompressed { get; set; }
}

/// <summary>
/// Unified logger with rotation support
/// </summary>
public sealed class UnifiedLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;
    private readonly LogRotationService _rotationService;
    private readonly FastLogger _fastLogger;
    
    public UnifiedLogger(
        string categoryName,
        string logDirectory,
        LogLevel minLevel = LogLevel.Information,
        LogRotationOptions rotationOptions = null)
    {
        _categoryName = categoryName;
        _minLevel = minLevel;
        
        // Setup rotation service
        _rotationService = new LogRotationService(
            logDirectory,
            $"{categoryName}-{{date}}.log",
            rotationOptions);
        
        // Setup fast logger for console output
        _fastLogger = new FastLogger(categoryName, minLevel);
    }
    
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;
    
    public IDisposable BeginScope<TState>(TState state) => _fastLogger.BeginScope(state);
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;
        
        // Log to console via fast logger
        _fastLogger.Log(logLevel, eventId, state, exception, formatter);
        
        // Log to file with rotation
        var message = FormatMessage(logLevel, eventId, state, exception, formatter);
        Task.Run(() => _rotationService.WriteAsync(message));
    }
    
    private string FormatMessage<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        var level = GetLogLevelString(logLevel);
        
        var result = $"[{level}] {_categoryName}: {message}";
        
        if (exception != null)
        {
            result += Environment.NewLine + $"  Exception: {exception}";
        }
        
        return result;
    }
    
    private static string GetLogLevelString(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "UNK"
    };
    
    public void Dispose()
    {
        _fastLogger?.Dispose();
        _rotationService?.Dispose();
    }
}
