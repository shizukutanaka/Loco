using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Memory;

namespace Loco.Core.Logging;

/// <summary>
/// High-performance async logger - Rob Pike's simplicity
/// Zero-allocation in hot path, async file writing
/// </summary>
public sealed class FastLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;
    private readonly Channel<LogEntry> _channel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;
    private readonly StreamWriter _writer;
    
    public FastLogger(string categoryName, LogLevel minLevel = LogLevel.Information, string logFile = null)
    {
        _categoryName = categoryName;
        _minLevel = minLevel;
        _cts = new CancellationTokenSource();
        
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        
        if (!string.IsNullOrEmpty(logFile))
        {
            var dir = Path.GetDirectoryName(logFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
                
            _writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
        }
        
        _processingTask = ProcessLogsAsync(_cts.Token);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;
    
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = logLevel,
            Category = _categoryName,
            Message = formatter(state, exception),
            Exception = exception
        };
        
        // Fire and forget - non-blocking
        _ = _channel.Writer.TryWrite(entry);
    }
    
    private async Task ProcessLogsAsync(CancellationToken cancellationToken)
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var message = FormatLogEntry(entry);
                
                // Write to console with color
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = GetLogColor(entry.Level);
                Console.WriteLine(message);
                Console.ForegroundColor = oldColor;
                
                // Write to file if configured
                if (_writer != null)
                {
                    await _writer.WriteLineAsync(message);
                }
            }
            catch
            {
                // Logging should never throw
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatLogEntry(LogEntry entry)
    {
        var sb = StringBuilderPool.Rent();
        
        sb.Append('[');
        sb.Append(entry.Timestamp.ToString("HH:mm:ss.fff"));
        sb.Append("] [");
        sb.Append(GetLogLevelString(entry.Level));
        sb.Append("] ");
        sb.Append(entry.Category);
        sb.Append(": ");
        sb.Append(entry.Message);
        
        if (entry.Exception != null)
        {
            sb.AppendLine();
            sb.Append("  Exception: ");
            sb.Append(entry.Exception.ToString());
        }
        
        return StringBuilderPool.GetStringAndReturn(sb);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConsoleColor GetLogColor(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.Gray,
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Information => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.DarkRed,
        _ => ConsoleColor.White
    };
    
    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }
        
        _writer?.Dispose();
        _cts.Dispose();
    }
    
    private struct LogEntry
    {
        public DateTime Timestamp;
        public LogLevel Level;
        public string Category;
        public string Message;
        public Exception Exception;
    }
    
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Fast logger provider
/// </summary>
public sealed class FastLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;
    private readonly string _logFile;
    private readonly ConcurrentDictionary<string, FastLogger> _loggers = new();
    
    public FastLoggerProvider(LogLevel minLevel = LogLevel.Information, string logFile = null)
    {
        _minLevel = minLevel;
        _logFile = logFile;
    }
    
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FastLogger(name, _minLevel, _logFile));
    }
    
    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }
        _loggers.Clear();
    }
}
