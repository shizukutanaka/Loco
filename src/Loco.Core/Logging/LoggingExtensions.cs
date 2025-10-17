using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Logging;

/// <summary>
/// Enhanced logging extensions for consistent logging across the application
/// </summary>
public static class LoggingExtensions
{
    // Log level constants for consistency
    private const LogLevel SecurityLevel = LogLevel.Warning;
    private const LogLevel PerformanceLevel = LogLevel.Information;
    private const LogLevel DebugDetailLevel = LogLevel.Debug;
    private const LogLevel TraceLevel = LogLevel.Trace;

    /// <summary>
    /// Log security-related events
    /// </summary>
    public static void LogSecurity(this ILogger logger, string message, params object[] args)
    {
        logger.Log(SecurityLevel, "SECURITY: " + message, args);
    }

    /// <summary>
    /// Log security violations
    /// </summary>
    public static void LogSecurityViolation(this ILogger logger, string violation, string details = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        logger.Log(LogLevel.Error, "SECURITY VIOLATION: {Violation} - {Details} at {Member} in {File}:{Line}",
            violation, details, memberName, Path.GetFileName(filePath), lineNumber);
    }

    /// <summary>
    /// Log performance metrics
    /// </summary>
    public static void LogPerformance(this ILogger logger, string operation, TimeSpan duration, string details = "")
    {
        logger.Log(PerformanceLevel, "PERFORMANCE: {Operation} completed in {Duration}ms {Details}",
            operation, duration.TotalMilliseconds, string.IsNullOrEmpty(details) ? "" : $"({details})");
    }

    /// <summary>
    /// Log operation start with timing
    /// </summary>
    public static IDisposable LogOperationStart(this ILogger logger, string operationName,
        [CallerMemberName] string memberName = "")
    {
        var startTime = DateTime.UtcNow;
        logger.Log(PerformanceLevel, "OPERATION START: {Operation} at {Member}", operationName, memberName);

        return new OperationTimer(logger, operationName, startTime, memberName);
    }

    /// <summary>
    /// Log configuration changes
    /// </summary>
    public static void LogConfigurationChange(this ILogger logger, string key, object? oldValue, object? newValue)
    {
        logger.Log(LogLevel.Information, "CONFIG CHANGE: {Key} changed from '{OldValue}' to '{NewValue}'",
            key, oldValue, newValue);
    }

    /// <summary>
    /// Log system health status
    /// </summary>
    public static void LogHealthStatus(this ILogger logger, string component, string status, string details = "")
    {
        var level = status.ToLowerInvariant() switch
        {
            "healthy" or "ok" => LogLevel.Information,
            "warning" or "degraded" => LogLevel.Warning,
            "critical" or "error" or "unhealthy" => LogLevel.Error,
            _ => LogLevel.Information
        };

        logger.Log(level, "HEALTH: {Component} is {Status} {Details}",
            component, status, string.IsNullOrEmpty(details) ? "" : $"({details})");
    }

    /// <summary>
    /// Log resource usage
    /// </summary>
    public static void LogResourceUsage(this ILogger logger, string resource, double usage, double threshold, string unit = "")
    {
        var level = usage > threshold * 1.2 ? LogLevel.Warning :
                   usage > threshold ? LogLevel.Information : LogLevel.Debug;

        var unitStr = string.IsNullOrEmpty(unit) ? "" : unit;
        logger.Log(level, "RESOURCE: {Resource} usage: {Usage:F1}{Unit} (threshold: {Threshold:F1}{Unit})",
            resource, usage, unitStr, threshold, unitStr);
    }

    /// <summary>
    /// Log API call details
    /// </summary>
    public static void LogApiCall(this ILogger logger, string method, string endpoint, int statusCode, TimeSpan duration)
    {
        var level = statusCode >= 200 && statusCode < 300 ? LogLevel.Debug :
                   statusCode >= 400 && statusCode < 500 ? LogLevel.Warning : LogLevel.Error;

        logger.Log(level, "API CALL: {Method} {Endpoint} -> {StatusCode} in {Duration}ms",
            method, endpoint, statusCode, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Log circuit breaker state changes
    /// </summary>
    public static void LogCircuitBreakerState(this ILogger logger, string operation, string oldState, string newState)
    {
        var level = newState.ToLowerInvariant() switch
        {
            "open" => LogLevel.Warning,
            "half-open" => LogLevel.Information,
            "closed" => LogLevel.Information,
            _ => LogLevel.Debug
        };

        logger.Log(level, "CIRCUIT BREAKER: {Operation} state changed from {OldState} to {NewState}",
            operation, oldState, newState);
    }

    /// <summary>
    /// Log retry attempts
    /// </summary>
    public static void LogRetryAttempt(this ILogger logger, string operation, int attempt, int maxAttempts, Exception? lastException = null)
    {
        if (lastException != null)
        {
            logger.Log(LogLevel.Warning, lastException, "RETRY: {Operation} attempt {Attempt}/{MaxAttempts} failed",
                operation, attempt, maxAttempts);
        }
        else
        {
            logger.Log(LogLevel.Information, "RETRY: Starting {Operation} attempt {Attempt}/{MaxAttempts}",
                operation, attempt, maxAttempts);
        }
    }

    /// <summary>
    /// Log validation errors
    /// </summary>
    public static void LogValidationError(this ILogger logger, string component, string error, params object[] args)
    {
        logger.Log(LogLevel.Error, "VALIDATION ERROR: {Component} - " + error, new object[] { component }.Concat(args).ToArray());
    }

    /// <summary>
    /// Log successful operations
    /// </summary>
    public static void LogSuccess(this ILogger logger, string operation, string details = "",
        [CallerMemberName] string memberName = "")
    {
        logger.Log(LogLevel.Information, "SUCCESS: {Operation} completed {Details} at {Member}",
            operation, string.IsNullOrEmpty(details) ? "" : $"({details})", memberName);
    }

    /// <summary>
    /// Operation timer for automatic performance logging
    /// </summary>
    private class OperationTimer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly DateTime _startTime;
        private readonly string _memberName;

        public OperationTimer(ILogger logger, string operationName, DateTime startTime, string memberName)
        {
            _logger = logger;
            _operationName = operationName;
            _startTime = startTime;
            _memberName = memberName;
        }

        public void Dispose()
        {
            var duration = DateTime.UtcNow - _startTime;
            _logger.LogPerformance(_operationName, duration, $"at {_memberName}");
        }
    }
}
