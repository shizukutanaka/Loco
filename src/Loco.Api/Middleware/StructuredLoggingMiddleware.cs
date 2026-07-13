using System.Diagnostics;

namespace Loco.Api.Middleware;

/// <summary>
/// Middleware for structured logging of HTTP requests/responses with correlation IDs
/// </summary>
public class StructuredLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StructuredLoggingMiddleware> _logger;

    public StructuredLoggingMiddleware(RequestDelegate next, ILogger<StructuredLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.TraceIdentifier;
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var value)
            ? value.ToString()
            : traceId;

        context.Items["CorrelationId"] = correlationId;
        // Indexer assignment: Headers.Add throws ArgumentException when the key
        // already exists (e.g. a retried pipeline or another component set it).
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            { "CorrelationId", correlationId },
            { "TraceId", traceId }
        }))
        {
            var stopwatch = Stopwatch.StartNew();

            // Log request
            _logger.LogInformation(
                "HTTP {Method} {Path} started. RemoteIP: {RemoteIp}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();

                // Log response
                _logger.LogInformation(
                    "HTTP {Method} {Path} completed with status {StatusCode} in {ElapsedMilliseconds}ms",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
