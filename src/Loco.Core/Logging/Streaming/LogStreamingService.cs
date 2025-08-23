using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging.Streaming
{
    /// <summary>
    /// High-performance real-time log streaming service
    /// Designed with John Carmack's performance-first approach
    /// </summary>
    public class LogStreamingService : IDisposable
    {
        private readonly Channel<LogEntry> _channel;
        private readonly ConcurrentDictionary<string, ILogSubscriber> _subscribers;
        private readonly ConcurrentDictionary<string, LogBuffer> _buffers;
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly LogStreamConfiguration _configuration;
        private readonly ILogger<LogStreamingService> _logger;
        
        // Performance counters
        private long _totalLogsProcessed;
        private long _totalBytesStreamed;
        private readonly object _statsLock = new object();

        public LogStreamingService(
            ILogger<LogStreamingService> logger,
            LogStreamConfiguration configuration = null)
        {
            _logger = logger;
            _configuration = configuration ?? new LogStreamConfiguration();
            _subscribers = new ConcurrentDictionary<string, ILogSubscriber>();
            _buffers = new ConcurrentDictionary<string, LogBuffer>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Create high-performance channel
            _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            
            // Start processing task
            _processingTask = ProcessLogsAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// Writes a log entry to the stream
        /// </summary>
        public async ValueTask WriteAsync(LogLevel level, string category, string message, 
            Exception exception = null, Dictionary<string, object> properties = null)
        {
            var entry = new LogEntry
            {
                Id = GenerateId(),
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                Exception = exception?.ToString(),
                Properties = properties,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                ProcessId = Environment.ProcessId
            };

            // Non-blocking write
            if (!_channel.Writer.TryWrite(entry))
            {
                // Channel is full (shouldn't happen with unbounded)
                await _channel.Writer.WriteAsync(entry);
            }
            
            Interlocked.Increment(ref _totalLogsProcessed);
        }

        /// <summary>
        /// Subscribes to log stream
        /// </summary>
        public string Subscribe(ILogSubscriber subscriber, LogFilter filter = null)
        {
            var subscriptionId = Guid.NewGuid().ToString();
            subscriber.SubscriptionId = subscriptionId;
            subscriber.Filter = filter ?? new LogFilter();
            
            _subscribers.TryAdd(subscriptionId, subscriber);
            
            // Create buffer for subscriber
            _buffers.TryAdd(subscriptionId, new LogBuffer(_configuration.BufferSize));
            
            _logger.LogInformation($"Subscriber {subscriptionId} added");
            
            return subscriptionId;
        }

        /// <summary>
        /// Unsubscribes from log stream
        /// </summary>
        public bool Unsubscribe(string subscriptionId)
        {
            if (_subscribers.TryRemove(subscriptionId, out var subscriber))
            {
                _buffers.TryRemove(subscriptionId, out _);
                subscriber.Dispose();
                
                _logger.LogInformation($"Subscriber {subscriptionId} removed");
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Gets stream statistics
        /// </summary>
        public StreamStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new StreamStatistics
                {
                    TotalLogsProcessed = _totalLogsProcessed,
                    TotalBytesStreamed = _totalBytesStreamed,
                    ActiveSubscribers = _subscribers.Count,
                    BufferedLogs = _buffers.Sum(b => b.Value.Count),
                    ChannelSize = _channel.Reader.Count
                };
            }
        }

        /// <summary>
        /// Queries historical logs
        /// </summary>
        public async Task<List<LogEntry>> QueryAsync(LogQuery query)
        {
            var results = new List<LogEntry>();
            
            // Query from persistent storage if configured
            if (_configuration.EnablePersistence)
            {
                results = await QueryPersistentLogsAsync(query);
            }
            
            // Apply filters
            if (query.Filter != null)
            {
                results = results.Where(log => MatchesFilter(log, query.Filter)).ToList();
            }
            
            // Apply sorting
            results = query.SortOrder == SortOrder.Ascending
                ? results.OrderBy(l => l.Timestamp).ToList()
                : results.OrderByDescending(l => l.Timestamp).ToList();
            
            // Apply pagination
            if (query.Skip > 0)
                results = results.Skip(query.Skip).ToList();
            if (query.Take > 0)
                results = results.Take(query.Take).ToList();
            
            return results;
        }

        /// <summary>
        /// Exports logs to file
        /// </summary>
        public async Task ExportAsync(string filePath, LogExportFormat format, LogFilter filter = null)
        {
            var logs = await QueryAsync(new LogQuery { Filter = filter });
            
            switch (format)
            {
                case LogExportFormat.Json:
                    await ExportJsonAsync(filePath, logs);
                    break;
                case LogExportFormat.Csv:
                    await ExportCsvAsync(filePath, logs);
                    break;
                case LogExportFormat.Plain:
                    await ExportPlainAsync(filePath, logs);
                    break;
            }
        }

        private async Task ProcessLogsAsync(CancellationToken cancellationToken)
        {
            var reader = _channel.Reader;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for data
                    await reader.WaitToReadAsync(cancellationToken);
                    
                    // Process all available items
                    while (reader.TryRead(out var entry))
                    {
                        await ProcessLogEntry(entry);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing logs");
                }
            }
        }

        private async Task ProcessLogEntry(LogEntry entry)
        {
            // Persist if enabled
            if (_configuration.EnablePersistence)
            {
                await PersistLogAsync(entry);
            }
            
            // Stream to subscribers
            var tasks = new List<Task>();
            
            foreach (var kvp in _subscribers)
            {
                var subscriber = kvp.Value;
                
                // Check filter
                if (!MatchesFilter(entry, subscriber.Filter))
                    continue;
                
                // Add to buffer
                if (_buffers.TryGetValue(kvp.Key, out var buffer))
                {
                    buffer.Add(entry);
                    
                    // Flush if buffer is full or timeout reached
                    if (buffer.ShouldFlush())
                    {
                        var entries = buffer.Flush();
                        tasks.Add(StreamToSubscriberAsync(subscriber, entries));
                    }
                }
            }
            
            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
            
            // Update statistics
            var entrySize = EstimateEntrySize(entry);
            Interlocked.Add(ref _totalBytesStreamed, entrySize);
        }

        private async Task StreamToSubscriberAsync(ILogSubscriber subscriber, List<LogEntry> entries)
        {
            try
            {
                await subscriber.OnLogsReceivedAsync(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error streaming to subscriber {subscriber.SubscriptionId}");
                
                // Remove failed subscriber
                if (_configuration.RemoveFailedSubscribers)
                {
                    Unsubscribe(subscriber.SubscriptionId);
                }
            }
        }

        private bool MatchesFilter(LogEntry entry, LogFilter filter)
        {
            if (filter == null)
                return true;
            
            // Level filter
            if (filter.MinLevel.HasValue && entry.Level < filter.MinLevel.Value)
                return false;
            if (filter.MaxLevel.HasValue && entry.Level > filter.MaxLevel.Value)
                return false;
            
            // Category filter
            if (filter.Categories?.Any() == true && !filter.Categories.Contains(entry.Category))
                return false;
            
            // Time range filter
            if (filter.StartTime.HasValue && entry.Timestamp < filter.StartTime.Value)
                return false;
            if (filter.EndTime.HasValue && entry.Timestamp > filter.EndTime.Value)
                return false;
            
            // Text search
            if (!string.IsNullOrEmpty(filter.SearchText))
            {
                if (!entry.Message.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !entry.Category.Contains(filter.SearchText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            
            return true;
        }

        private async Task PersistLogAsync(LogEntry entry)
        {
            // Simple file-based persistence
            var logFile = Path.Combine(
                _configuration.PersistencePath,
                $"log_{DateTime.UtcNow:yyyyMMdd}.txt");
            
            var logLine = FormatLogLine(entry);
            
            await File.AppendAllTextAsync(logFile, logLine + Environment.NewLine);
        }

        private async Task<List<LogEntry>> QueryPersistentLogsAsync(LogQuery query)
        {
            var results = new List<LogEntry>();
            
            // Read from log files
            var logFiles = Directory.GetFiles(_configuration.PersistencePath, "log_*.txt")
                .OrderByDescending(f => f);
            
            foreach (var file in logFiles)
            {
                var lines = await File.ReadAllLinesAsync(file);
                foreach (var line in lines)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null)
                        results.Add(entry);
                }
                
                if (results.Count >= query.Take && query.Take > 0)
                    break;
            }
            
            return results;
        }

        private string FormatLogLine(LogEntry entry)
        {
            return $"{entry.Timestamp:O}|{entry.Level}|{entry.Category}|{entry.Message}";
        }

        private LogEntry ParseLogLine(string line)
        {
            var parts = line.Split('|');
            if (parts.Length < 4)
                return null;
            
            return new LogEntry
            {
                Timestamp = DateTime.Parse(parts[0]),
                Level = Enum.Parse<LogLevel>(parts[1]),
                Category = parts[2],
                Message = parts[3]
            };
        }

        private async Task ExportJsonAsync(string filePath, List<LogEntry> logs)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(logs, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task ExportCsvAsync(string filePath, List<LogEntry> logs)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Level,Category,Message");
            
            foreach (var log in logs)
            {
                csv.AppendLine($"{log.Timestamp:O},{log.Level},{log.Category},\"{log.Message}\"");
            }
            
            await File.WriteAllTextAsync(filePath, csv.ToString());
        }

        private async Task ExportPlainAsync(string filePath, List<LogEntry> logs)
        {
            var text = new StringBuilder();
            
            foreach (var log in logs)
            {
                text.AppendLine(FormatLogLine(log));
            }
            
            await File.WriteAllTextAsync(filePath, text.ToString());
        }

        private string GenerateId()
        {
            // High-performance ID generation
            return $"{DateTime.UtcNow.Ticks}-{Thread.CurrentThread.ManagedThreadId}";
        }

        private long EstimateEntrySize(LogEntry entry)
        {
            // Rough estimate of entry size in bytes
            return 100 + (entry.Message?.Length ?? 0) + (entry.Exception?.Length ?? 0);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _channel.Writer.TryComplete();
            
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
            
            foreach (var subscriber in _subscribers.Values)
            {
                subscriber.Dispose();
            }
            
            _cancellationTokenSource.Dispose();
        }
    }

    // Supporting classes
    public class LogEntry
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public int ThreadId { get; set; }
        public int ProcessId { get; set; }
    }

    public class LogStreamConfiguration
    {
        public int BufferSize { get; set; } = 100;
        public TimeSpan BufferFlushInterval { get; set; } = TimeSpan.FromSeconds(1);
        public bool EnablePersistence { get; set; } = true;
        public string PersistencePath { get; set; } = @"C:\ProgramData\Loco\Logs";
        public bool RemoveFailedSubscribers { get; set; } = true;
    }

    public interface ILogSubscriber : IDisposable
    {
        string SubscriptionId { get; set; }
        LogFilter Filter { get; set; }
        Task OnLogsReceivedAsync(List<LogEntry> entries);
    }

    public class LogFilter
    {
        public LogLevel? MinLevel { get; set; }
        public LogLevel? MaxLevel { get; set; }
        public List<string> Categories { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string SearchText { get; set; }
    }

    public class LogQuery
    {
        public LogFilter Filter { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 100;
        public SortOrder SortOrder { get; set; } = SortOrder.Descending;
    }

    public class StreamStatistics
    {
        public long TotalLogsProcessed { get; set; }
        public long TotalBytesStreamed { get; set; }
        public int ActiveSubscribers { get; set; }
        public int BufferedLogs { get; set; }
        public int ChannelSize { get; set; }
    }

    public enum LogExportFormat
    {
        Json,
        Csv,
        Plain
    }

    public enum SortOrder
    {
        Ascending,
        Descending
    }

    internal class LogBuffer
    {
        private readonly List<LogEntry> _entries;
        private readonly int _maxSize;
        private DateTime _lastFlush;
        private readonly object _lock = new object();

        public LogBuffer(int maxSize)
        {
            _maxSize = maxSize;
            _entries = new List<LogEntry>(maxSize);
            _lastFlush = DateTime.UtcNow;
        }

        public int Count => _entries.Count;

        public void Add(LogEntry entry)
        {
            lock (_lock)
            {
                _entries.Add(entry);
            }
        }

        public bool ShouldFlush()
        {
            lock (_lock)
            {
                return _entries.Count >= _maxSize || 
                       (DateTime.UtcNow - _lastFlush) > TimeSpan.FromSeconds(1);
            }
        }

        public List<LogEntry> Flush()
        {
            lock (_lock)
            {
                var result = new List<LogEntry>(_entries);
                _entries.Clear();
                _lastFlush = DateTime.UtcNow;
                return result;
            }
        }
    }

    // Sample subscriber implementation
    public class WebSocketLogSubscriber : ILogSubscriber
    {
        public string SubscriptionId { get; set; }
        public LogFilter Filter { get; set; }
        private readonly Func<List<LogEntry>, Task> _onLogsReceived;

        public WebSocketLogSubscriber(Func<List<LogEntry>, Task> onLogsReceived)
        {
            _onLogsReceived = onLogsReceived;
        }

        public async Task OnLogsReceivedAsync(List<LogEntry> entries)
        {
            if (_onLogsReceived != null)
            {
                await _onLogsReceived(entries);
            }
        }

        public void Dispose()
        {
            // Cleanup
        }
    }
}
