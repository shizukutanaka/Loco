using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.ErrorHandling
{
    public class ErrorHandler
    {
        private readonly ILogger<ErrorHandler> _logger;
        private readonly List<IErrorProcessor> _processors = new();
        private readonly ErrorStatistics _statistics = new();

        public ErrorHandler(ILogger<ErrorHandler> logger)
        {
            _logger = logger;
            RegisterDefaultProcessors();
        }

        public void RegisterProcessor(IErrorProcessor processor)
        {
            _processors.Add(processor);
        }

        public async Task<ErrorResult> HandleAsync(
            Exception exception,
            ErrorContext context = null,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            context ??= new ErrorContext();
            context.Caller = caller;
            context.FilePath = filePath;
            context.LineNumber = lineNumber;
            context.Timestamp = DateTime.UtcNow;
            context.ThreadId = Environment.CurrentManagedThreadId;
            context.MachineName = Environment.MachineName;

            // Classify error
            var classification = ClassifyError(exception);
            context.Classification = classification;

            // Update statistics
            _statistics.RecordError(classification);

            // Create error info
            var errorInfo = new ErrorInfo
            {
                Id = Guid.NewGuid().ToString(),
                Exception = exception,
                Context = context,
                Classification = classification,
                StackTrace = GetEnhancedStackTrace(exception)
            };

            // Process through handlers
            foreach (var processor in _processors)
            {
                try
                {
                    await processor.ProcessAsync(errorInfo);
                }
                catch (Exception processorError)
                {
                    _logger.LogError(processorError, "Error processor failed: {Processor}", processor.GetType().Name);
                }
            }

            // Log based on severity
            LogError(errorInfo);

            // Determine action
            var action = DetermineAction(classification, exception);

            return new ErrorResult
            {
                ErrorId = errorInfo.Id,
                Handled = action != ErrorAction.Throw,
                Action = action,
                UserMessage = GetUserFriendlyMessage(exception, classification),
                TechnicalDetails = errorInfo.ToString()
            };
        }

        public T HandleWithRetry<T>(
            Func<T> operation,
            int maxAttempts = 3,
            TimeSpan? delay = null)
        {
            delay ??= TimeSpan.FromSeconds(1);
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex) when (IsRetryable(ex))
                {
                    lastException = ex;
                    _logger.LogWarning("Attempt {Attempt}/{Max} failed: {Error}", attempt, maxAttempts, ex.Message);

                    if (attempt < maxAttempts)
                    {
                        var waitTime = TimeSpan.FromMilliseconds(delay.Value.TotalMilliseconds * Math.Pow(2, attempt - 1));
                        Task.Delay(waitTime).Wait();
                    }
                }
            }

            throw new RetryExhaustedException($"Operation failed after {maxAttempts} attempts", lastException);
        }

        public async Task<T> HandleWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = 3,
            TimeSpan? delay = null)
        {
            delay ??= TimeSpan.FromSeconds(1);
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (IsRetryable(ex))
                {
                    lastException = ex;
                    _logger.LogWarning("Attempt {Attempt}/{Max} failed: {Error}", attempt, maxAttempts, ex.Message);

                    if (attempt < maxAttempts)
                    {
                        var waitTime = TimeSpan.FromMilliseconds(delay.Value.TotalMilliseconds * Math.Pow(2, attempt - 1));
                        await Task.Delay(waitTime);
                    }
                }
            }

            throw new RetryExhaustedException($"Operation failed after {maxAttempts} attempts", lastException);
        }

        public async Task<T> HandleWithFallbackAsync<T>(
            Func<Task<T>> primaryOperation,
            Func<Task<T>> fallbackOperation,
            string operationName = null)
        {
            try
            {
                return await primaryOperation();
            }
            catch (Exception primaryEx)
            {
                _logger.LogWarning(primaryEx, "Primary operation failed: {Operation}", operationName ?? "Unknown");

                try
                {
                    _logger.LogInformation("Attempting fallback for: {Operation}", operationName ?? "Unknown");
                    return await fallbackOperation();
                }
                catch (Exception fallbackEx)
                {
                    throw new AggregateException("Both primary and fallback operations failed",
                        primaryEx, fallbackEx);
                }
            }
        }

        private ErrorClassification ClassifyError(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => ErrorClassification.ValidationError,
                ArgumentException => ErrorClassification.ValidationError,
                InvalidOperationException => ErrorClassification.BusinessLogicError,
                UnauthorizedAccessException => ErrorClassification.SecurityError,
                System.Security.SecurityException => ErrorClassification.SecurityError,
                System.IO.IOException => ErrorClassification.IOError,
                System.Net.Http.HttpRequestException => ErrorClassification.NetworkError,
                TimeoutException => ErrorClassification.TimeoutError,
                OutOfMemoryException => ErrorClassification.ResourceError,
                StackOverflowException => ErrorClassification.CriticalError,
                _ => ErrorClassification.UnknownError
            };
        }

        private bool IsRetryable(Exception exception)
        {
            var classification = ClassifyError(exception);
            return classification == ErrorClassification.NetworkError ||
                   classification == ErrorClassification.TimeoutError ||
                   classification == ErrorClassification.IOError;
        }

        private ErrorAction DetermineAction(ErrorClassification classification, Exception exception)
        {
            return classification switch
            {
                ErrorClassification.CriticalError => ErrorAction.Shutdown,
                ErrorClassification.SecurityError => ErrorAction.Alert,
                ErrorClassification.ResourceError => ErrorAction.Throttle,
                ErrorClassification.ValidationError => ErrorAction.Return,
                _ => ErrorAction.Log
            };
        }

        private string GetUserFriendlyMessage(Exception exception, ErrorClassification classification)
        {
            return classification switch
            {
                ErrorClassification.ValidationError => "The provided input is invalid. Please check and try again.",
                ErrorClassification.NetworkError => "A network error occurred. Please check your connection.",
                ErrorClassification.TimeoutError => "The operation timed out. Please try again.",
                ErrorClassification.SecurityError => "Access denied. Please check your permissions.",
                ErrorClassification.ResourceError => "System resources are temporarily unavailable.",
                ErrorClassification.IOError => "A file or data access error occurred.",
                _ => "An unexpected error occurred. Please try again later."
            };
        }

        private string GetEnhancedStackTrace(Exception exception)
        {
            var sb = new StringBuilder();
            var ex = exception;
            int level = 0;

            while (ex != null)
            {
                if (level > 0)
                {
                    sb.AppendLine($"--- Inner Exception #{level} ---");
                }

                sb.AppendLine($"Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(ex.StackTrace);
                }

                if (ex.Data.Count > 0)
                {
                    sb.AppendLine("Additional Data:");
                    foreach (var key in ex.Data.Keys)
                    {
                        sb.AppendLine($"  {key}: {ex.Data[key]}");
                    }
                }

                ex = ex.InnerException;
                level++;
            }

            return sb.ToString();
        }

        private void LogError(ErrorInfo errorInfo)
        {
            var logLevel = errorInfo.Classification switch
            {
                ErrorClassification.CriticalError => LogLevel.Critical,
                ErrorClassification.SecurityError => LogLevel.Error,
                ErrorClassification.ResourceError => LogLevel.Error,
                ErrorClassification.BusinessLogicError => LogLevel.Warning,
                ErrorClassification.ValidationError => LogLevel.Information,
                _ => LogLevel.Warning
            };

            _logger.Log(logLevel, errorInfo.Exception,
                "Error {ErrorId} classified as {Classification} in {Caller} at {File}:{Line}",
                errorInfo.Id, errorInfo.Classification, errorInfo.Context.Caller,
                errorInfo.Context.FilePath, errorInfo.Context.LineNumber);
        }

        private void RegisterDefaultProcessors()
        {
            // Add default processors
            _processors.Add(new MetricsProcessor());
            _processors.Add(new AlertProcessor(_logger));
        }

        public ErrorStatistics GetStatistics() => _statistics;
    }

    // Supporting classes
    public interface IErrorProcessor
    {
        Task ProcessAsync(ErrorInfo errorInfo);
    }

    public class ErrorInfo
    {
        public string Id { get; set; }
        public Exception Exception { get; set; }
        public ErrorContext Context { get; set; }
        public ErrorClassification Classification { get; set; }
        public string StackTrace { get; set; }

        public override string ToString()
        {
            return $"[{Id}] {Classification}: {Exception?.Message}";
        }
    }

    public class ErrorContext
    {
        public string Caller { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public int ThreadId { get; set; }
        public string MachineName { get; set; }
        public ErrorClassification Classification { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    public class ErrorResult
    {
        public string ErrorId { get; set; }
        public bool Handled { get; set; }
        public ErrorAction Action { get; set; }
        public string UserMessage { get; set; }
        public string TechnicalDetails { get; set; }
    }

    public enum ErrorClassification
    {
        ValidationError,
        BusinessLogicError,
        SecurityError,
        NetworkError,
        TimeoutError,
        IOError,
        ResourceError,
        CriticalError,
        UnknownError
    }

    public enum ErrorAction
    {
        Log,
        Return,
        Retry,
        Throw,
        Alert,
        Throttle,
        Shutdown
    }

    public class ErrorStatistics
    {
        private readonly Dictionary<ErrorClassification, int> _counts = new();
        private readonly object _lock = new();

        public void RecordError(ErrorClassification classification)
        {
            lock (_lock)
            {
                if (!_counts.ContainsKey(classification))
                    _counts[classification] = 0;
                _counts[classification]++;
            }
        }

        public Dictionary<ErrorClassification, int> GetCounts()
        {
            lock (_lock)
            {
                return new Dictionary<ErrorClassification, int>(_counts);
            }
        }
    }

    public class RetryExhaustedException : Exception
    {
        public RetryExhaustedException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    // Default processors
    public class MetricsProcessor : IErrorProcessor
    {
        public Task ProcessAsync(ErrorInfo errorInfo)
        {
            // Record metrics
            Debug.WriteLine($"Metric: Error {errorInfo.Classification} occurred");
            return Task.CompletedTask;
        }
    }

    public class AlertProcessor : IErrorProcessor
    {
        private readonly ILogger _logger;

        public AlertProcessor(ILogger logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(ErrorInfo errorInfo)
        {
            if (errorInfo.Classification == ErrorClassification.CriticalError ||
                errorInfo.Classification == ErrorClassification.SecurityError)
            {
                _logger.LogCritical("ALERT: Critical error detected - {ErrorId}", errorInfo.Id);
                // In production, send alerts via email, SMS, etc.
            }
            return Task.CompletedTask;
        }
    }
}