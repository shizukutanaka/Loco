using Serilog;
using Serilog.Context;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Loco.Api.Middleware;

/// <summary>
/// Middleware for comprehensive request/response logging with Serilog
/// </summary>
public class SerilogRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SerilogRequestLoggingMiddleware> _logger;

    public SerilogRequestLoggingMiddleware(RequestDelegate next, ILogger<SerilogRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or extract correlation ID
        var correlationId = ExtractOrGenerateCorrelationId(context);
        var requestId = context.TraceIdentifier;

        // Set Serilog context for this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("RemoteIP", context.Connection.RemoteIpAddress))
        using (LogContext.PushProperty("UserAgent", context.Request.Headers.UserAgent.ToString()))
        {
            // Add to response headers
            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            context.Response.Headers.Add("X-Request-ID", requestId);

            // Capture request body for logging (if small enough)
            var requestBody = string.Empty;
            if (IsLoggableContentType(context.Request.ContentType) && context.Request.ContentLength < 10000)
            {
                requestBody = await ReadRequestBodyAsync(context.Request);
            }

            // Time the operation
            var stopwatch = Stopwatch.StartNew();

            // Capture response
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    // Log incoming request
                    _logger.LogInformation(
                        "HTTP {Method} {Path} started - ClientIP: {ClientIP}",
                        context.Request.Method,
                        context.Request.Path,
                        context.Connection.RemoteIpAddress);

                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        _logger.LogDebug("Request body: {@RequestBody}", requestBody);
                    }

                    // Call next middleware
                    await _next(context);

                    stopwatch.Stop();

                    // Capture response body
                    var responseBodyContent = await ReadResponseBodyAsync(responseBody);

                    // Log response
                    var isSuccess = context.Response.StatusCode < 400;
                    var logLevel = context.Response.StatusCode switch
                    {
                        >= 500 => LogEventLevel.Error,
                        >= 400 => LogEventLevel.Warning,
                        _ => LogEventLevel.Information
                    };

                    var logMessage = $"HTTP {context.Request.Method} {context.Request.Path} completed " +
                        $"with status {context.Response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms";

                    Log.Write(logLevel, logMessage);

                    if (!isSuccess && !string.IsNullOrEmpty(responseBodyContent))
                    {
                        _logger.LogDebug("Response body: {@ResponseBody}", responseBodyContent);
                    }

                    // Copy response to original stream
                    await responseBody.CopyToAsync(originalBodyStream);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _logger.LogError(ex,
                        "HTTP {Method} {Path} failed with exception after {ElapsedMs}ms",
                        context.Request.Method,
                        context.Request.Path,
                        stopwatch.ElapsedMilliseconds);

                    // Copy response to original stream
                    await responseBody.CopyToAsync(originalBodyStream);
                    throw;
                }
                finally
                {
                    context.Response.Body = originalBodyStream;
                }
            }
        }
    }

    /// <summary>
    /// Extracts correlation ID from headers or generates new one
    /// </summary>
    private static string ExtractOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationIdHeader))
        {
            return correlationIdHeader.ToString();
        }

        return context.TraceIdentifier;
    }

    /// <summary>
    /// Determines if content type should be logged
    /// </summary>
    private static bool IsLoggableContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        return contentType.Contains("application/json") ||
               contentType.Contains("application/x-www-form-urlencoded") ||
               contentType.Contains("text/plain") ||
               contentType.Contains("text/xml");
    }

    /// <summary>
    /// Reads request body without consuming the stream
    /// </summary>
    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using (var reader = new StreamReader(
            request.Body,
            encoding: System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true))
        {
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }
    }

    /// <summary>
    /// Reads response body from memory stream
    /// </summary>
    private static async Task<string> ReadResponseBodyAsync(MemoryStream responseBody)
    {
        responseBody.Position = 0;

        using (var reader = new StreamReader(responseBody, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            return await reader.ReadToEndAsync();
        }
    }
}

/// <summary>
/// Extension methods for Serilog request logging middleware
/// </summary>
public static class SerilogRequestLoggingExtensions
{
    /// <summary>
    /// Adds Serilog request logging middleware
    /// </summary>
    public static IApplicationBuilder UseSerilogRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SerilogRequestLoggingMiddleware>();
    }
}
