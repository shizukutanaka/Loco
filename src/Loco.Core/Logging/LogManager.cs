using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging
{
    public interface ILogManager
    {
        void SetLogLevel(LogLevel level);
        LogLevel GetLogLevel();
        void AddLogTarget(ILogTarget target);
        void RemoveLogTarget(ILogTarget target);
        void LogEvent(LogLevel level, string message, Exception exception = null, params object[] args);
        void FlushAll();
        LogStatistics GetStatistics();
        Task<List<LogEntry>> QueryLogsAsync(LogQuery query);
    }

    public interface ILogTarget
    {
        string Name { get; }
        void WriteLog(LogEntry entry);
        void Flush();
        bool IsEnabled(LogLevel level);
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public string Category { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public string ThreadId { get; set; }
        public string MachineName { get; set; }
        public string ProcessName { get; set; }
    }

    public class LogQuery
    {
        public DateTime? FromTime { get; set; }
        public DateTime? ToTime { get; set; }
        public LogLevel? MinLevel { get; set; }
        public string Category { get; set; }
        public string MessageContains { get; set; }
        public int MaxResults { get; set; } = 1000;
        public bool IncludeExceptions { get; set; } = true;
    }

    public class LogStatistics
    {
        public Dictionary<LogLevel, long> CountsByLevel { get; set; } = new();
        public long TotalLogs { get; set; }
        public DateTime FirstLogTime { get; set; }
        public DateTime LastLogTime { get; set; }
        public long ErrorCount => CountsByLevel.GetValueOrDefault(LogLevel.Error, 0);
        public long WarningCount => CountsByLevel.GetValueOrDefault(LogLevel.Warning, 0);
    }

    public class LogManager : ILogManager, IDisposable
    {
        private readonly List<ILogTarget> _targets = new();
        private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
        private readonly Timer _flushTimer;
        private readonly object _lock = new();
        private LogLevel _minimumLevel = LogLevel.Information;
        private readonly LogStatistics _statistics = new();
        private volatile bool _disposed = false;

        public LogManager()
        {
            // Auto-flush every 5 seconds
            _flushTimer = new Timer(FlushAllInternal, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void SetLogLevel(LogLevel level)
        {
            _minimumLevel = level;
        }

        public LogLevel GetLogLevel()
        {
            return _minimumLevel;
        }

        public void AddLogTarget(ILogTarget target)
        {
            if (target == null) return;

            lock (_lock)
            {
                if (!_targets.Contains(target))
                {
                    _targets.Add(target);
                }
            }
        }

        public void RemoveLogTarget(ILogTarget target)
        {
            if (target == null) return;

            lock (_lock)
            {
                _targets.Remove(target);
            }
        }

        public void LogEvent(LogLevel level, string message, Exception exception = null, params object[] args)
        {
            if (_disposed || !IsEnabled(level)) return;

            try
            {
                var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;

                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Message = formattedMessage,
                    Exception = exception,
                    ThreadId = Thread.CurrentThread.ManagedThreadId.ToString(),
                    MachineName = Environment.MachineName,
                    ProcessName = Environment.ProcessPath ?? "Unknown"
                };

                // Update statistics
                UpdateStatistics(entry);

                // Queue for async processing
                _logBuffer.Enqueue(entry);

                // If it's a critical error, flush immediately
                if (level >= LogLevel.Error)
                {
                    ProcessPendingLogs();
                }
            }
            catch (Exception ex)
            {
                // Avoid infinite recursion - write to console as last resort
                Console.WriteLine($"LogManager failed to log: {ex.Message}");
            }
        }

        public void FlushAll()
        {
            FlushAllInternal();
        }

        private void FlushAllInternal(object state = null)
        {
            if (_disposed) return;

            ProcessPendingLogs();

            lock (_lock)
            {
                foreach (var target in _targets)
                {
                    try
                    {
                        target.Flush();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to flush log target {target.Name}: {ex.Message}");
                    }
                }
            }
        }

        public LogStatistics GetStatistics()
        {
            lock (_statistics)
            {
                return new LogStatistics
                {
                    CountsByLevel = new Dictionary<LogLevel, long>(_statistics.CountsByLevel),
                    TotalLogs = _statistics.TotalLogs,
                    FirstLogTime = _statistics.FirstLogTime,
                    LastLogTime = _statistics.LastLogTime
                };
            }
        }

        public async Task<List<LogEntry>> QueryLogsAsync(LogQuery query)
        {
            var results = new List<LogEntry>();

            // This is a simple in-memory implementation
            // In production, you'd query from persistent storage
            var entries = new List<LogEntry>();
            while (_logBuffer.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            // Re-queue the entries
            foreach (var entry in entries)
            {
                _logBuffer.Enqueue(entry);
            }

            // Apply filters
            foreach (var entry in entries)
            {
                if (query.FromTime.HasValue && entry.Timestamp < query.FromTime.Value) continue;
                if (query.ToTime.HasValue && entry.Timestamp > query.ToTime.Value) continue;
                if (query.MinLevel.HasValue && entry.Level < query.MinLevel.Value) continue;
                if (!string.IsNullOrEmpty(query.Category) && !entry.Category?.Contains(query.Category, StringComparison.OrdinalIgnoreCase) == true) continue;
                if (!string.IsNullOrEmpty(query.MessageContains) && !entry.Message.Contains(query.MessageContains, StringComparison.OrdinalIgnoreCase)) continue;
                if (!query.IncludeExceptions && entry.Exception != null) continue;

                results.Add(entry);
                if (results.Count >= query.MaxResults) break;
            }

            return results;
        }

        private bool IsEnabled(LogLevel level)
        {
            return level >= _minimumLevel;
        }

        private void ProcessPendingLogs()
        {
            var entries = new List<LogEntry>();

            // Dequeue all pending entries
            while (_logBuffer.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            if (!entries.Any()) return;

            // Write to all targets
            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    foreach (var target in _targets)
                    {
                        try
                        {
                            if (target.IsEnabled(entry.Level))
                            {
                                target.WriteLog(entry);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Log target {target.Name} failed: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void UpdateStatistics(LogEntry entry)
        {
            lock (_statistics)
            {
                _statistics.CountsByLevel.TryAdd(entry.Level, 0);
                _statistics.CountsByLevel[entry.Level]++;
                _statistics.TotalLogs++;

                if (_statistics.FirstLogTime == default)
                    _statistics.FirstLogTime = entry.Timestamp;

                _statistics.LastLogTime = entry.Timestamp;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _flushTimer?.Dispose();
            FlushAll();

            lock (_lock)
            {
                foreach (var target in _targets)
                {
                    if (target is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _targets.Clear();
            }
        }
    }

    // Built-in log targets
    public class ConsoleLogTarget : ILogTarget
    {
        public string Name => "Console";
        private readonly LogLevel _minimumLevel;

        public ConsoleLogTarget(LogLevel minimumLevel = LogLevel.Information)
        {
            _minimumLevel = minimumLevel;
        }

        public void WriteLog(LogEntry entry)
        {
            var color = GetConsoleColor(entry.Level);
            var originalColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}");

                if (entry.Exception != null)
                {
                    Console.WriteLine($"  Exception: {entry.Exception.Message}");
                    if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                    {
                        Console.WriteLine($"  Stack: {entry.Exception.StackTrace}");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        public void Flush()
        {
            Console.Out.Flush();
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minimumLevel;
        }

        private ConsoleColor GetConsoleColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
        }
    }

    public class FileLogTarget : ILogTarget, IDisposable
    {
        public string Name => $"File({_fileName})";

        private readonly string _fileName;
        private readonly LogLevel _minimumLevel;
        private readonly StreamWriter _writer;
        private readonly object _writeLock = new();
        private readonly JsonSerializerOptions _jsonOptions;

        public FileLogTarget(string fileName, LogLevel minimumLevel = LogLevel.Information)
        {
            _fileName = fileName;
            _minimumLevel = minimumLevel;

            var directory = Path.GetDirectoryName(_fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(_fileName, append: true) { AutoFlush = false };
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public void WriteLog(LogEntry entry)
        {
            lock (_writeLock)
            {
                var logLine = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";

                if (entry.Exception != null)
                {
                    logLine += $" | Exception: {entry.Exception.Message}";
                    if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                    {
                        logLine += $" | Stack: {entry.Exception.StackTrace}";
                    }
                }

                _writer.WriteLine(logLine);
            }
        }

        public void Flush()
        {
            lock (_writeLock)
            {
                _writer.Flush();
            }
        }

        public bool IsEnabled(LogLevel level)
        {
            return level >= _minimumLevel;
        }

        public void Dispose()
        {
            lock (_writeLock)
            {
                _writer?.Dispose();
            }
        }
    }
}