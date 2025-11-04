using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Loco.Core.Logging;

/// <summary>
/// Serilog configuration for structured logging
/// </summary>
public static class SerilogSetup
{
    /// <summary>
    /// Configures Serilog for the application
    /// </summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment;

        Log.Logger = new LoggerConfiguration()
            // Set minimum level based on environment
            .MinimumLevel.Is(environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)

            // Filter out noisy frameworks
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)

            // Enrich logs with context
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            .Enrich.WithProperty("Application", "Loco")
            .Enrich.WithProperty("Version", "1.0.0")
            .Enrich.WithExceptionDetails()

            // Configure sinks
            .WriteTo.Console(outputTemplate: GetConsoleTemplate(environment.IsDevelopment()))
            .WriteTo.File(
                path: Path.Combine("Logs", "loco-.txt"),
                fileSizeLimitBytes: 104857600, // 100 MB
                retainedFileCountLimit: 30,
                rollingInterval: RollingInterval.Day,
                outputTemplate: GetFileTemplate())
            .WriteTo.File(
                formatter: new JsonFormatter(),
                path: Path.Combine("Logs", "loco-json-.txt"),
                fileSizeLimitBytes: 104857600,
                retainedFileCountLimit: 30,
                rollingInterval: RollingInterval.Day)

            // Create logger
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    /// <summary>
    /// Adds Serilog to an existing host builder
    /// </summary>
    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "Loco")
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine("Logs", "loco-.txt"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        return hostBuilder.UseSerilog();
    }

    /// <summary>
    /// Gets console output template for development
    /// </summary>
    private static string GetConsoleTemplate(bool isDevelopment)
    {
        return isDevelopment
            ? "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            : "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
    }

    /// <summary>
    /// Gets file output template
    /// </summary>
    private static string GetFileTemplate()
    {
        return "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";
    }
}

/// <summary>
/// Serilog context helpers for structured logging
/// </summary>
public static class SerilogContext
{
    /// <summary>
    /// Sets correlation ID for request tracing
    /// </summary>
    public static IDisposable SetCorrelationId(string correlationId)
    {
        return LogContext.PushProperty("CorrelationId", correlationId);
    }

    /// <summary>
    /// Sets user ID for audit logging
    /// </summary>
    public static IDisposable SetUserId(string userId)
    {
        return LogContext.PushProperty("UserId", userId);
    }

    /// <summary>
    /// Sets tenant ID for multi-tenant applications
    /// </summary>
    public static IDisposable SetTenantId(string tenantId)
    {
        return LogContext.PushProperty("TenantId", tenantId);
    }

    /// <summary>
    /// Sets request ID for request tracking
    /// </summary>
    public static IDisposable SetRequestId(string requestId)
    {
        return LogContext.PushProperty("RequestId", requestId);
    }

    /// <summary>
    /// Sets operation name for operation tracing
    /// </summary>
    public static IDisposable SetOperationName(string operationName)
    {
        return LogContext.PushProperty("OperationName", operationName);
    }

    /// <summary>
    /// Sets entity context for audit trail
    /// </summary>
    public static IDisposable SetEntityContext(string entityType, string entityId)
    {
        var disposable1 = LogContext.PushProperty("EntityType", entityType);
        var disposable2 = LogContext.PushProperty("EntityId", entityId);

        return new CompositeDisposable(disposable1, disposable2);
    }
}

/// <summary>
/// Composite disposable for multiple contexts
/// </summary>
internal class CompositeDisposable : IDisposable
{
    private readonly IDisposable[] _disposables;

    public CompositeDisposable(params IDisposable[] disposables)
    {
        _disposables = disposables;
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
    }
}

/// <summary>
/// Structured logging extensions for Serilog
/// </summary>
public static class SerilogLoggingExtensions
{
    /// <summary>
    /// Logs a security event
    /// </summary>
    public static void LogSecurityEvent(
        this ILogger logger,
        string @event,
        string userId,
        string details,
        Dictionary<string, object>? additionalProperties = null)
    {
        var props = additionalProperties ?? new Dictionary<string, object>();
        props["UserId"] = userId;
        props["SecurityEvent"] = @event;

        logger.Warning("Security event occurred: {@Event} - {@Details}", @event, details, props);
    }

    /// <summary>
    /// Logs a performance metric
    /// </summary>
    public static void LogPerformance(
        this ILogger logger,
        string operationName,
        long elapsedMilliseconds,
        bool success,
        Dictionary<string, object>? additionalProperties = null)
    {
        var level = elapsedMilliseconds > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;

        var props = additionalProperties ?? new Dictionary<string, object>();
        props["OperationName"] = operationName;
        props["ElapsedMs"] = elapsedMilliseconds;
        props["Success"] = success;

        var message = success
            ? "Operation {OperationName} completed in {ElapsedMs}ms"
            : "Operation {OperationName} failed after {ElapsedMs}ms";

        logger.Write(level, message, operationName, elapsedMilliseconds);
    }

    /// <summary>
    /// Logs an API call with response details
    /// </summary>
    public static void LogApiCall(
        this ILogger logger,
        string method,
        string path,
        int statusCode,
        long elapsedMilliseconds,
        Dictionary<string, object>? additionalProperties = null)
    {
        var level = statusCode >= 500 ? LogEventLevel.Error
                  : statusCode >= 400 ? LogEventLevel.Warning
                  : LogEventLevel.Information;

        var props = additionalProperties ?? new Dictionary<string, object>();
        props["HttpMethod"] = method;
        props["HttpPath"] = path;
        props["HttpStatusCode"] = statusCode;
        props["ElapsedMs"] = elapsedMilliseconds;

        logger.Write(
            level,
            "HTTP {HttpMethod} {HttpPath} -> {StatusCode} ({ElapsedMs}ms)",
            method,
            path,
            statusCode,
            elapsedMilliseconds);
    }

    /// <summary>
    /// Logs a business event (workflow, job, etc.)
    /// </summary>
    public static void LogBusinessEvent(
        this ILogger logger,
        string eventType,
        string entityType,
        string entityId,
        string details,
        Dictionary<string, object>? additionalProperties = null)
    {
        var props = additionalProperties ?? new Dictionary<string, object>();
        props["EventType"] = eventType;
        props["EntityType"] = entityType;
        props["EntityId"] = entityId;

        logger.Information(
            "Business event: {EventType} on {EntityType} {EntityId} - {Details}",
            eventType,
            entityType,
            entityId,
            details);
    }

    /// <summary>
    /// Logs an error with full context
    /// </summary>
    public static void LogErrorWithContext(
        this ILogger logger,
        Exception exception,
        string message,
        string operationName,
        Dictionary<string, object>? additionalProperties = null)
    {
        var props = additionalProperties ?? new Dictionary<string, object>();
        props["OperationName"] = operationName;
        props["ExceptionType"] = exception.GetType().Name;

        logger.Error(exception, "Error during {OperationName}: {Message}", operationName, message);
    }

    /// <summary>
    /// Logs a debug message with structured data
    /// </summary>
    public static void LogDebugData(
        this ILogger logger,
        string message,
        object data)
    {
        logger.Debug("{Message} - {@Data}", message, data);
    }

    /// <summary>
    /// Logs a validation error
    /// </summary>
    public static void LogValidationError(
        this ILogger logger,
        string entityName,
        Dictionary<string, string[]> errors)
    {
        var errorSummary = string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}"));
        logger.Warning("Validation failed for {Entity}: {@Errors}", entityName, errors);
    }

    /// <summary>
    /// Logs operation start and returns disposable for end logging
    /// </summary>
    public static IDisposable LogOperation(
        this ILogger logger,
        string operationName,
        Dictionary<string, object>? properties = null)
    {
        var startTime = DateTime.UtcNow;
        logger.Information("Operation started: {OperationName}", operationName);

        return new OperationLogger(logger, operationName, startTime);
    }
}

/// <summary>
/// Operation logger for timing and result tracking
/// </summary>
internal class OperationLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly DateTime _startTime;
    private bool _disposed;

    public OperationLogger(ILogger logger, string operationName, DateTime startTime)
    {
        _logger = logger;
        _operationName = operationName;
        _startTime = startTime;
    }

    public void Dispose()
    {
        if (_disposed) return;

        var elapsed = DateTime.UtcNow - _startTime;
        _logger.Information(
            "Operation completed: {OperationName} (elapsed: {ElapsedMs}ms)",
            _operationName,
            elapsed.TotalMilliseconds);

        _disposed = true;
    }
}

/// <summary>
/// Serilog configuration for specific scenarios
/// </summary>
public static class SerilogScenarios
{
    /// <summary>
    /// Configures Serilog for development with detailed logging
    /// </summary>
    public static LoggerConfiguration DevelopmentConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
    }

    /// <summary>
    /// Configures Serilog for production with JSON logging
    /// </summary>
    public static LoggerConfiguration ProductionConfiguration(string logsPath)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "Loco")
            .Enrich.WithExceptionDetails()
            .WriteTo.File(
                formatter: new JsonFormatter(),
                path: Path.Combine(logsPath, "loco-json-.txt"),
                fileSizeLimitBytes: 104857600,
                retainedFileCountLimit: 30,
                rollingInterval: RollingInterval.Day);
    }

    /// <summary>
    /// Configures Serilog for testing
    /// </summary>
    public static LoggerConfiguration TestingConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}");
    }
}
