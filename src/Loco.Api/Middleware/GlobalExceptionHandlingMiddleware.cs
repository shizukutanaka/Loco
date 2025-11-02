using System.Text.Json;
using Loco.Core;

namespace Loco.Api.Middleware;

/// <summary>
/// Global exception handling middleware for standardized error responses
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. TraceId: {TraceId}", context.TraceIdentifier);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow,
            Code = GetErrorCode(exception),
            Message = GetErrorMessage(exception),
            Details = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? exception.StackTrace
                : null
        };

        context.Response.StatusCode = GetStatusCode(exception);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsJsonAsync(response, options);
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentNullException => StatusCodes.Status400BadRequest,
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        TimeoutException => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        ArgumentNullException => "INVALID_ARGUMENT",
        ArgumentException => "INVALID_ARGUMENT",
        InvalidOperationException => "INVALID_OPERATION",
        UnauthorizedAccessException => "UNAUTHORIZED",
        KeyNotFoundException => "NOT_FOUND",
        TimeoutException => "TIMEOUT",
        _ => "INTERNAL_ERROR"
    };

    private static string GetErrorMessage(Exception exception) => exception switch
    {
        ArgumentNullException or ArgumentException => exception.Message,
        InvalidOperationException => exception.Message,
        UnauthorizedAccessException => "You do not have permission to access this resource.",
        KeyNotFoundException => "The requested resource was not found.",
        TimeoutException => "The request timed out. Please try again later.",
        _ => "An unexpected error occurred. Please try again later."
    };
}

public class ApiErrorResponse
{
    /// <summary>
    /// Error code identifier
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// User-friendly error message
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Detailed error information (only in development)
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Field validation errors
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Unique trace identifier for log correlation
    /// </summary>
    public string TraceId { get; set; } = "";

    /// <summary>
    /// UTC timestamp of the error
    /// </summary>
    public DateTime Timestamp { get; set; }
}
