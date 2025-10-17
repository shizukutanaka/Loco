using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging
{
    /// <summary>
    /// Production-grade structured logger with automatic log rotation and retention policies.
    /// Thread-safe implementation for concurrent logging scenarios.
    /// </summary>
    public class StructuredLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly LogLevel _minLevel;
        private readonly int _retentionDays;
        private readonly long _maxFileSizeBytes;
        private readonly bool _enableConsole;
        private readonly SemaphoreSlim _writeLock;
        private StreamWriter? _currentWriter;
        private string? _currentLogFile;
        private long _currentFileSize;
        private DateTime _lastRotationCheck;
        private bool _disposed;

        public StructuredLogger(
            string categoryName,
            string logDirectory,
            LogLevel minLevel = LogLevel.Information,
            int retentionDays = 30,
            long maxFileSizeBytes = 10 * 1024 * 1024, // 10MB
            bool enableConsole = true)
        {
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _minLevel = minLevel;
            _retentionDays = retentionDays;
            _maxFileSizeBytes = maxFileSizeBytes;
            _enableConsole = enableConsole;
            _writeLock = new SemaphoreSlim(1, 1);
            _lastRotationCheck = DateTime.UtcNow;

            EnsureLogDirectory();
            InitializeLogFile();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null; // Scope not implemented for simplicity
        }
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel && !_disposed;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Fire-and-forget async logging to avoid blocking caller
            // Use TaskScheduler.UnobservedTaskException to handle any unobserved exceptions
            var task = LogAsyncInternal(logLevel, eventId, state, exception, formatter);

            // Ensure exceptions are observed (but don't block the caller)
            task.ContinueWith(t =>
            {
                // Exception will be observed here, preventing UnobservedTaskException
                _ = t.Exception;
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task LogAsyncInternal<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var logEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    Level = logLevel.ToString(),
                    Category = _categoryName,
                    EventId = eventId.Id,
                    Message = formatter(state, exception),
                    Exception = exception?.ToString()
                };

                var json = JsonSerializer.Serialize(logEntry);

                if (_enableConsole)
                {
                    Console.WriteLine(json);
                }

                WriteToFile(json);
                CheckRotation();
            }
            catch
            {
                // Log failure silently to avoid cascading errors
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private void EnsureLogDirectory()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        private void InitializeLogFile()
        {
            _currentLogFile = Path.Combine(_logDirectory, $"log-{DateTime.UtcNow:yyyy-MM-dd}.json");
            _currentWriter = new StreamWriter(_currentLogFile, append: true);
            _currentFileSize = new FileInfo(_currentLogFile).Length;
        }

        private void WriteToFile(string json)
        {
            if (_currentWriter != null)
            {
                _currentWriter.WriteLine(json);
                _currentWriter.Flush();
                _currentFileSize += json.Length + Environment.NewLine.Length;
            }
        }

        private void CheckRotation()
        {
            if ((DateTime.UtcNow - _lastRotationCheck).TotalMinutes < 5)
                return;

            _lastRotationCheck = DateTime.UtcNow;

            if (_currentFileSize > _maxFileSizeBytes)
            {
                RotateLogFile();
            }

            CleanupOldLogs();
        }

        private void RotateLogFile()
        {
            _currentWriter?.Dispose();
            _currentLogFile = Path.Combine(_logDirectory, $"log-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json");
            _currentWriter = new StreamWriter(_currentLogFile, append: true);
            _currentFileSize = 0;
        }

        private void CleanupOldLogs()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);
            foreach (var file in Directory.GetFiles(_logDirectory, "log-*.json"))
            {
                if (File.GetCreationTimeUtc(file) < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _currentWriter?.Dispose();
            _writeLock?.Dispose();
        }
    }
}