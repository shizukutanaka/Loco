using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Diagnostics;

/// <summary>
/// Helper class for structured logging with correlation IDs and context tracking.
/// Provides standardized logging across the platform for production observability.
/// </summary>
public static class StructuredLoggingHelper
{
    /// <summary>
    /// AsyncLocal storage for correlation ID context
    /// </summary>
    private static readonly AsyncLocal<string> _correlationId = new();

    /// <summary>
    /// AsyncLocal storage for user context
    /// </summary>
    private static readonly AsyncLocal<string> _userId = new();

    /// <summary>
    /// AsyncLocal storage for tenant context
    /// </summary>
    private static readonly AsyncLocal<string> _tenantId = new();

    /// <summary>
    /// Sets the correlation ID for the current execution context
    /// </summary>
    public static void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Gets the correlation ID for the current execution context
    /// </summary>
    public static string GetCorrelationId()
    {
        return _correlationId.Value ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Sets the user ID for the current execution context
    /// </summary>
    public static void SetUserId(string userId)
    {
        _userId.Value = userId;
    }

    /// <summary>
    /// Gets the user ID for the current execution context
    /// </summary>
    public static string? GetUserId()
    {
        return _userId.Value;
    }

    /// <summary>
    /// Sets the tenant ID for the current execution context
    /// </summary>
    public static void SetTenantId(string tenantId)
    {
        _tenantId.Value = tenantId;
    }

    /// <summary>
    /// Gets the tenant ID for the current execution context
    /// </summary>
    public static string? GetTenantId()
    {
        return _tenantId.Value;
    }

    /// <summary>
    /// Gets the execution context as a dictionary for logging
    /// </summary>
    public static Dictionary<string, object> GetContextDictionary()
    {
        var context = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(_correlationId.Value))
            context["CorrelationId"] = _correlationId.Value;

        if (!string.IsNullOrEmpty(_userId.Value))
            context["UserId"] = _userId.Value;

        if (!string.IsNullOrEmpty(_tenantId.Value))
            context["TenantId"] = _tenantId.Value;

        return context;
    }

    /// <summary>
    /// Clears all context information (useful for cleanup)
    /// </summary>
    public static void ClearContext()
    {
        _correlationId.Value = null;
        _userId.Value = null;
        _tenantId.Value = null;
    }

    /// <summary>
    /// Logs an operation start with structured context
    /// </summary>
    public static void LogOperationStart(ILogger? logger, string operationName, string operationId)
    {
        if (logger == null) return;

        var context = GetContextDictionary();
        context["OperationName"] = operationName;
        context["OperationId"] = operationId;
        context["Timestamp"] = DateTime.UtcNow;

        logger.LogInformation(
            "Operation started: {OperationName} (ID: {OperationId}, CorrelationId: {CorrelationId})",
            operationName,
            operationId,
            GetCorrelationId());
    }

    /// <summary>
    /// Logs an operation completion with structured context and metrics
    /// </summary>
    public static void LogOperationComplete(
        ILogger? logger,
        string operationName,
        string operationId,
        bool success,
        long? durationMs = null,
        string? errorMessage = null)
    {
        if (logger == null) return;

        var context = GetContextDictionary();
        context["OperationName"] = operationName;
        context["OperationId"] = operationId;
        context["Success"] = success;
        context["Timestamp"] = DateTime.UtcNow;

        if (durationMs.HasValue)
            context["DurationMs"] = durationMs.Value;

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            logger.LogError(
                "Operation failed: {OperationName} (ID: {OperationId}, Duration: {DurationMs}ms, Error: {ErrorMessage}, CorrelationId: {CorrelationId})",
                operationName,
                operationId,
                durationMs ?? -1,
                errorMessage,
                GetCorrelationId());
        }
        else
        {
            logger.LogInformation(
                "Operation completed: {OperationName} (ID: {OperationId}, Duration: {DurationMs}ms, CorrelationId: {CorrelationId})",
                operationName,
                operationId,
                durationMs ?? -1,
                GetCorrelationId());
        }
    }

    /// <summary>
    /// Logs a warning with structured context
    /// </summary>
    public static void LogWarningWithContext(
        ILogger? logger,
        string message,
        params object?[] args)
    {
        if (logger == null) return;

        var context = GetContextDictionary();
        context["Message"] = message;
        context["Timestamp"] = DateTime.UtcNow;

        var allArgs = new List<object?>(args) { GetCorrelationId() };
        logger.LogWarning(
            message + " (CorrelationId: {CorrelationId})",
            allArgs.ToArray());
    }

    /// <summary>
    /// Logs an error with structured context
    /// </summary>
    public static void LogErrorWithContext(
        ILogger? logger,
        Exception? exception,
        string message,
        params object?[] args)
    {
        if (logger == null) return;

        var context = GetContextDictionary();
        context["Message"] = message;
        context["Timestamp"] = DateTime.UtcNow;

        if (exception != null)
            context["Exception"] = exception.ToString();

        var allArgs = new List<object?>(args) { GetCorrelationId() };
        logger.LogError(
            exception,
            message + " (CorrelationId: {CorrelationId})",
            allArgs.ToArray());
    }
}
