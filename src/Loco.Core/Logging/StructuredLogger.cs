using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging
{
    /// <summary>
    /// High-performance structured logging system
    /// Following Rob Pike's principle: "Don't panic"
    /// </summary>
    public class StructuredLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly LogLevel _minLevel;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private readonly string _logDirectory;
        private readonly int _maxQueueSize;
        private readonly PerformanceCounters _performanceCounters;
        private bool _disposed;

        public StructuredLogger(string categoryName, LogLevel minLevel = LogLevel.Information)
        {
            _categoryName = categoryName;
            _minLevel = minLevel;
            _logQueue = new ConcurrentQueue<LogEntry>();
            _semaphore = new SemaphoreSlim(0);
            _cancellationTokenSource = new CancellationTokenSource();
            _maxQueueSize = 10000;
            _performanceCounters = new PerformanceCounters();
            
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco",
                "Logs");
            
            Directory.CreateDirectory(_logDirectory);
            
            _processingTask = Task.Run(ProcessLogsAsync);
        }

        public IDisposable BeginScope<TState>(TState state) => new LogScope<TState>(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (_logQueue.Count >= _maxQueueSize)
            {
                // Drop oldest logs if queue is full
                _logQueue.TryDequeue(out _);
            }

            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel,
                EventId = eventId,
                Category = _categoryName,
                Message = formatter(state, exception),
                Exception = exception,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                CorrelationId = GetCorrelationId(),
                Properties = ExtractProperties(state)
            };

            _logQueue.Enqueue(entry);
            _semaphore.Release();
            
            _performanceCounters.IncrementLogCount(logLevel);
        }

        // Structured logging helpers
        public void LogPerformance(string operation, double milliseconds, Dictionary<string, object> metadata = null)
        {
            var properties = metadata ?? new Dictionary<string, object>();
            properties["Operation"] = operation;
            properties["Duration"] = milliseconds;
            properties["Unit"] = "ms";
            
            Log(LogLevel.Information, new EventId(1000, "Performance"), 
                properties, null, 
                (state, ex) => $"Performance: {operation} completed in {milliseconds:F2}ms");
            
            _performanceCounters.RecordOperationTime(operation, milliseconds);
        }

        public void LogMetric(string name, double value, string unit = null, Dictionary<string, object> tags = null)
        {
            var properties = tags ?? new Dictionary<string, object>();
            properties["MetricName"] = name;
            properties["MetricValue"] = value;
            if (!string.IsNullOrEmpty(unit))
                properties["MetricUnit"] = unit;
            
            Log(LogLevel.Information, new EventId(1001, "Metric"),
                properties, null,
                (state, ex) => $"Metric: {name}={value}{(unit != null ? unit : "")}");
        }

        public void LogSecurityEvent(string eventType, string details, bool success = true)
        {
            var properties = new Dictionary<string, object>
            {
                ["SecurityEventType"] = eventType,
                ["Success"] = success,
                ["Details"] = details,
                ["Timestamp"] = DateTime.UtcNow
            };
            
            var level = success ? LogLevel.Information : LogLevel.Warning;
            Log(level, new EventId(2000, "Security"),
                properties, null,
                (state, ex) => $"Security Event: {eventType} - {(success ? "Success" : "Failed")} - {details}");
        }

        private async Task ProcessLogsAsync()
        {
            var buffer = new List<LogEntry>(100);
            var flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait for logs or timeout
                    var hasLogs = await _semaphore.WaitAsync(100, _cancellationTokenSource.Token);
                    
                    if (hasLogs)
                    {
                        // Batch process logs
                        while (_logQueue.TryDequeue(out var entry) && buffer.Count < 100)
                        {
                            buffer.Add(entry);
                        }
                    }
                    
                    // Flush on buffer full or timer
                    if (buffer.Count >= 100 || await flushTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
                    {
                        if (buffer.Count > 0)
                        {
                            await FlushLogsAsync(buffer);
                            buffer.Clear();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log to console as fallback
                    Console.Error.WriteLine($"Error processing logs: {ex}");
                }
            }
            
            // Final flush
            if (buffer.Count > 0)
            {
                await FlushLogsAsync(buffer);
            }
        }

        private async Task FlushLogsAsync(List<LogEntry> entries)
        {
            if (entries.Count == 0)
                return;
            
            var fileName = Path.Combine(_logDirectory, $"loco_{DateTime.UtcNow:yyyyMMdd}.log");
            var jsonFileName = Path.Combine(_logDirectory, $"loco_{DateTime.UtcNow:yyyyMMdd}.json");
            
            var textBuilder = new StringBuilder();
            var jsonBuilder = new StringBuilder();
            
            foreach (var entry in entries)
            {
                // Text format for human reading
                textBuilder.AppendLine(FormatTextLog(entry));
                
                // JSON format for analysis
                jsonBuilder.AppendLine(FormatJsonLog(entry));
            }
            
            // Async write to files
            var writeTextTask = File.AppendAllTextAsync(fileName, textBuilder.ToString());
            var writeJsonTask = File.AppendAllTextAsync(jsonFileName, jsonBuilder.ToString());
            
            await Task.WhenAll(writeTextTask, writeJsonTask);
        }

        private string FormatTextLog(LogEntry entry)
        {
            var levelStr = GetLevelString(entry.Level);
            var message = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] [{entry.Category}] {entry.Message}";
            
            if (entry.Exception != null)
            {
                message += $"\n  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}\n  Stack: {entry.Exception.StackTrace}";
            }
            
            if (entry.Properties?.Count > 0)
            {
                message += $"\n  Properties: {string.Join(", ", entry.Properties.Select(p => $"{p.Key}={p.Value}"))}";
            }
            
            return message;
        }

        private string FormatJsonLog(LogEntry entry)
        {
            // Manual JSON serialization for performance
            var json = new StringBuilder();
            json.Append("{");
            json.Append($"\"timestamp\":\"{entry.Timestamp:O}\",");
            json.Append($"\"level\":\"{entry.Level}\",");
            json.Append($"\"category\":\"{EscapeJson(entry.Category)}\",");
            json.Append($"\"message\":\"{EscapeJson(entry.Message)}\",");
            json.Append($"\"threadId\":{entry.ThreadId},");
            json.Append($"\"correlationId\":\"{entry.CorrelationId}\"");
            
            if (entry.Exception != null)
            {
                json.Append($",\"exception\":{{");
                json.Append($"\"type\":\"{EscapeJson(entry.Exception.GetType().Name)}\",");
                json.Append($"\"message\":\"{EscapeJson(entry.Exception.Message)}\",");
                json.Append($"\"stackTrace\":\"{EscapeJson(entry.Exception.StackTrace)}\"");
                json.Append("}");
            }
            
            if (entry.Properties?.Count > 0)
            {
                json.Append(",\"properties\":{");
                json.Append(string.Join(",", entry.Properties.Select(p => 
                    $"\"{EscapeJson(p.Key)}\":\"{EscapeJson(p.Value?.ToString() ?? "null")}\"")));
                json.Append("}");
            }
            
            json.Append("}");
            return json.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string GetLevelString(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERRO",
            LogLevel.Critical => "CRIT",
            _ => "NONE"
        };

        private static string GetCorrelationId()
        {
            // Get or create correlation ID for request tracking
            return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        }

        private static Dictionary<string, object> ExtractProperties<TState>(TState state)
        {
            if (state is Dictionary<string, object> dict)
                return dict;
            
            if (state is IEnumerable<KeyValuePair<string, object>> kvps)
                return kvps.ToDictionary(k => k.Key, k => k.Value);
            
            return new Dictionary<string, object>();
        }

        public PerformanceReport GetPerformanceReport() => _performanceCounters.GetReport();

        public void Dispose()
        {
            if (_disposed)
                return;
            
            _disposed = true;
            _cancellationTokenSource.Cancel();
            
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
            
            _cancellationTokenSource.Dispose();
            _semaphore.Dispose();
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public LogLevel Level { get; set; }
            public EventId EventId { get; set; }
            public string Category { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
            public int ThreadId { get; set; }
            public string CorrelationId { get; set; }
            public Dictionary<string, object> Properties { get; set; }
        }

        private class LogScope<TState> : IDisposable
        {
            private readonly TState _state;
            
            public LogScope(TState state)
            {
                _state = state;
            }
            
            public void Dispose() { }
        }

        private class PerformanceCounters
        {
            private readonly ConcurrentDictionary<LogLevel, long> _logCounts;
            private readonly ConcurrentDictionary<string, List<double>> _operationTimes;
            private readonly DateTime _startTime;
            
            public PerformanceCounters()
            {
                _logCounts = new ConcurrentDictionary<LogLevel, long>();
                _operationTimes = new ConcurrentDictionary<string, List<double>>();
                _startTime = DateTime.UtcNow;
            }
            
            public void IncrementLogCount(LogLevel level)
            {
                _logCounts.AddOrUpdate(level, 1, (_, count) => count + 1);
            }
            
            public void RecordOperationTime(string operation, double milliseconds)
            {
                _operationTimes.AddOrUpdate(operation,
                    new List<double> { milliseconds },
                    (_, times) =>
                    {
                        times.Add(milliseconds);
                        // Keep only last 1000 measurements
                        if (times.Count > 1000)
                            times.RemoveAt(0);
                        return times;
                    });
            }
            
            public PerformanceReport GetReport()
            {
                return new PerformanceReport
                {
                    UpTime = DateTime.UtcNow - _startTime,
                    LogCounts = new Dictionary<LogLevel, long>(_logCounts),
                    OperationStats = _operationTimes.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new OperationStatistics
                        {
                            Count = kvp.Value.Count,
                            Average = kvp.Value.Average(),
                            Min = kvp.Value.Min(),
                            Max = kvp.Value.Max(),
                            Median = GetMedian(kvp.Value)
                        })
                };
            }
            
            private static double GetMedian(List<double> values)
            {
                if (values.Count == 0)
                    return 0;
                
                var sorted = values.OrderBy(v => v).ToList();
                var mid = sorted.Count / 2;
                
                if (sorted.Count % 2 == 0)
                    return (sorted[mid - 1] + sorted[mid]) / 2.0;
                
                return sorted[mid];
            }
        }
    }

    public class PerformanceReport
    {
        public TimeSpan UpTime { get; set; }
        public Dictionary<LogLevel, long> LogCounts { get; set; }
        public Dictionary<string, OperationStatistics> OperationStats { get; set; }
    }

    public class OperationStatistics
    {
        public int Count { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Median { get; set; }
    }
}
