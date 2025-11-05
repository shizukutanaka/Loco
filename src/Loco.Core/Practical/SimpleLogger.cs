// Rob Pike: "A little copying is better than a little dependency"
// Uncle Bob: "Don't repeat yourself, but also don't over-abstract"

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Loco.Core.Practical;

/// <summary>
/// Dead simple logger - no dependencies, just write to console/file
/// Structured but not complex
/// </summary>
public class SimpleLogger
{
    public enum Level { Debug, Info, Warning, Error }

    private readonly string _name;
    private readonly Level _minLevel;
    private readonly ConcurrentQueue<string> _buffer = new();
    private readonly int _maxBufferSize;
    private readonly object _writeLock = new();
    private readonly string? _logFile;

    public SimpleLogger(string name, Level minLevel = Level.Info, string? logFile = null, int maxBufferSize = 1000)
    {
        _name = name;
        _minLevel = minLevel;
        _logFile = logFile;
        _maxBufferSize = maxBufferSize;
    }

    // Simple logging methods with caller info
    public void Debug(string message, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        => Log(Level.Debug, message, method, line);

    public void Info(string message, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        => Log(Level.Info, message, method, line);

    public void Warning(string message, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        => Log(Level.Warning, message, method, line);

    public void Error(string message, Exception? ex = null, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
    {
        var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
        Log(Level.Error, fullMessage, method, line);

        if (ex?.StackTrace != null)
        {
            Log(Level.Error, ex.StackTrace, method, line);
        }
    }

    // Core logging method
    private void Log(Level level, string message, string method, int line)
    {
        if (level < _minLevel) return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper().PadRight(7);
        var logLine = $"[{timestamp}] {levelStr} [{_name}.{method}:{line}] {message}";

        // Buffer the log
        _buffer.Enqueue(logLine);
        if (_buffer.Count > _maxBufferSize)
        {
            _buffer.TryDequeue(out _); // Remove oldest
        }

        // Write to console with color
        WriteToConsole(level, logLine);

        // Write to file if configured
        if (_logFile != null)
        {
            WriteToFile(logLine);
        }
    }

    private void WriteToConsole(Level level, string logLine)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            Level.Debug => ConsoleColor.Gray,
            Level.Info => ConsoleColor.White,
            Level.Warning => ConsoleColor.Yellow,
            Level.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };

        Console.WriteLine(logLine);
        Console.ForegroundColor = originalColor;
    }

    private void WriteToFile(string logLine)
    {
        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(_logFile!, logLine + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors - don't crash the app for logging
            }
        }
    }

    // Measure and log execution time
    public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await operation();
            Info($"{operationName} completed in {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            Error($"{operationName} failed after {sw.ElapsedMilliseconds}ms", ex);
            throw;
        }
    }

    // Get buffered logs
    public string[] GetBufferedLogs() => _buffer.ToArray();

    // Clear buffer
    public void ClearBuffer() => _buffer.Clear();
}

// Global logger factory for simplicity
public static class SimpleLoggerFactory
{
    private static readonly ConcurrentDictionary<string, SimpleLogger> _loggers = new();
    private static SimpleLogger.Level _defaultLevel = SimpleLogger.Level.Info;
    private static string? _defaultLogFile;

    public static void Configure(SimpleLogger.Level defaultLevel, string? logFile = null)
    {
        _defaultLevel = defaultLevel;
        _defaultLogFile = logFile;
    }

    public static SimpleLogger GetLogger<T>() => GetLogger(typeof(T).Name);

    public static SimpleLogger GetLogger(string name)
    {
        return _loggers.GetOrAdd(name, n => new SimpleLogger(n, _defaultLevel, _defaultLogFile));
    }
}