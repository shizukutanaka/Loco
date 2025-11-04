using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Loco.Core.Exceptions;

namespace Loco.Core.ErrorHandling;

/// <summary>
/// Base exception handler for .NET 8+ IExceptionHandler pattern
/// </summary>
public abstract class BaseExceptionHandler<T> : IExceptionHandler where T : Exception
{
    protected readonly ILogger<BaseExceptionHandler<T>> Logger;

    protected BaseExceptionHandler(ILogger<BaseExceptionHandler<T>> logger)
    {
        Logger = logger;
    }

    public virtual async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not T typedException)
        {
            return false;
        }

        httpContext.Response.StatusCode = GetStatusCode(typedException);
        httpContext.Response.ContentType = "application/json";

        var response = new ProblemDetails
        {
            Title = GetTitle(typedException),
            Detail = GetDetail(typedException),
            Status = httpContext.Response.StatusCode,
            Instance = httpContext.Request.Path
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }

    protected abstract int GetStatusCode(T exception);
    protected abstract string GetTitle(T exception);
    protected abstract string GetDetail(T exception);
}

/// <summary>
/// Handler for LocoException and derived types
/// </summary>
public class LocoExceptionHandler : BaseExceptionHandler<LocoException>
{
    public LocoExceptionHandler(ILogger<LocoExceptionHandler> logger) : base(logger)
    {
    }

    protected override int GetStatusCode(LocoException exception)
    {
        return exception switch
        {
            WorkflowExecutionException => StatusCodes.Status500InternalServerError,
            WorkflowValidationException => StatusCodes.Status400BadRequest,
            ActionException => StatusCodes.Status400BadRequest,
            EngineException => StatusCodes.Status500InternalServerError,
            ResourceException => StatusCodes.Status404NotFound,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            SecurityException => StatusCodes.Status403Forbidden,
            LocoConfigurationException => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    protected override string GetTitle(LocoException exception)
    {
        return exception switch
        {
            WorkflowExecutionException => "Workflow Execution Error",
            WorkflowValidationException => "Workflow Validation Error",
            ActionException => "Action Error",
            EngineException => "Engine Error",
            ResourceException => "Resource Not Found",
            TimeoutException => "Operation Timeout",
            SecurityException => "Security Error",
            LocoConfigurationException => "Configuration Error",
            _ => "Loco Error"
        };
    }

    protected override string GetDetail(LocoException exception)
    {
        var baseMessage = exception.Message;
        if (exception.Context != null && exception.Context.Count > 0)
        {
            var contextStr = string.Join("; ", exception.Context.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            return $"{baseMessage} (Context: {contextStr})";
        }
        return baseMessage;
    }
}

/// <summary>
/// Handler for validation exceptions
/// </summary>
public class ValidationExceptionHandler : BaseExceptionHandler<ValidationException>
{
    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : base(logger)
    {
    }

    protected override int GetStatusCode(ValidationException exception)
    {
        return StatusCodes.Status400BadRequest;
    }

    protected override string GetTitle(ValidationException exception)
    {
        return "Validation Error";
    }

    protected override string GetDetail(ValidationException exception)
    {
        return exception.Message;
    }

    public override async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/json";

        var response = new ValidationProblemDetails
        {
            Title = "Validation Error",
            Detail = validationException.Message,
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path,
            Errors = validationException.Errors
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}

/// <summary>
/// Handler for general exceptions
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred");

        httpContext.Response.StatusCode = exception switch
        {
            ArgumentNullException or ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            TimeoutException => StatusCodes.Status504GatewayTimeout,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.ContentType = "application/json";

        var isDevelopment = httpContext.RequestServices
            .GetService<IHostEnvironment>()
            ?.IsDevelopment() ?? false;

        var response = new ProblemDetails
        {
            Title = "An error occurred",
            Detail = isDevelopment ? exception.Message : "An internal error occurred",
            Status = httpContext.Response.StatusCode,
            Instance = httpContext.Request.Path,
            Extensions = isDevelopment ? new Dictionary<string, object?>
            {
                { "stackTrace", exception.StackTrace },
                { "exceptionType", exception.GetType().FullName }
            } : null
        };

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}

/// <summary>
/// Custom validation exception
/// </summary>
public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message, Dictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }

    public static ValidationException FromValidationErrors(Dictionary<string, List<string>> errors)
    {
        var errorDict = errors.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray());

        var message = $"Validation failed with {errors.Count} error(s)";
        return new ValidationException(message, errorDict);
    }
}

/// <summary>
/// RFC 7807 ProblemDetails standard response
/// </summary>
public class ProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// A short, human-readable summary of the problem type
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The HTTP status code
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public int? Status { get; set; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence of the problem
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence of the problem
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("instance")]
    public string? Instance { get; set; }

    /// <summary>
    /// Additional properties as per RFC 7807
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("extensions")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Extensions { get; set; }

    /// <summary>
    /// Trace ID for error tracking
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("traceId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }
}

/// <summary>
/// RFC 7807 ValidationProblemDetails for validation errors
/// </summary>
public class ValidationProblemDetails : ProblemDetails
{
    /// <summary>
    /// Validation errors dictionary
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }
}

/// <summary>
/// Extension methods for exception handler registration
/// </summary>
public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// Adds modern .NET 8+ exception handlers
    /// </summary>
    public static IServiceCollection AddLocoExceptionHandlers(this IServiceCollection services)
    {
        services.AddExceptionHandler<LocoExceptionHandler>();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
