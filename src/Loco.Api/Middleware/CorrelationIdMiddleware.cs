using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;

namespace Loco.Api.Middleware;

/// <summary>
/// Middleware for managing correlation IDs following W3C Trace Context standard
/// Supports both X-Correlation-ID and W3C traceparent headers
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ExtractOrGenerateCorrelationId(context);
        var traceId = ExtractOrGenerateTraceId(context);
        var spanId = GenerateSpanId();

        // Store in context for access by downstream handlers
        context.Items["CorrelationId"] = correlationId;
        context.Items["TraceId"] = traceId;
        context.Items["SpanId"] = spanId;
        context.Items["RequestStartTime"] = DateTime.UtcNow;

        // Add to response headers for client tracking
        context.Response.Headers.Add("X-Correlation-ID", correlationId);
        context.Response.Headers.Add("X-Trace-ID", traceId);
        context.Response.Headers.Add("X-Span-ID", spanId);

        // Set Serilog context for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("SpanId", spanId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            // Log request start
            _logger.LogInformation(
                "Request started: {Method} {Path} (CorrelationId: {CorrelationId}, TraceId: {TraceId})",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                traceId);

            try
            {
                await _next(context);

                var duration = DateTime.UtcNow - (DateTime)context.Items["RequestStartTime"]!;

                _logger.LogInformation(
                    "Request completed: {Method} {Path} -> {StatusCode} ({DurationMs}ms)",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - (DateTime)context.Items["RequestStartTime"]!;

                _logger.LogError(ex,
                    "Request failed: {Method} {Path} after {DurationMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    duration.TotalMilliseconds);

                throw;
            }
        }
    }

    /// <summary>
    /// Extracts correlation ID from headers or generates new one
    /// Priority: X-Correlation-ID > traceparent > generated
    /// </summary>
    private string ExtractOrGenerateCorrelationId(HttpContext context)
    {
        // Try X-Correlation-ID header first (most common)
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationIdHeader))
        {
            return correlationIdHeader.ToString();
        }

        // Try W3C traceparent header
        if (context.Request.Headers.TryGetValue("traceparent", out var traceParentHeader))
        {
            var parts = traceParentHeader.ToString().Split('-');
            if (parts.Length >= 2)
            {
                return parts[1]; // Extract trace-id from traceparent
            }
        }

        // Generate new UUID
        return Guid.NewGuid().ToString("D");
    }

    /// <summary>
    /// Extracts or generates W3C trace ID
    /// </summary>
    private string ExtractOrGenerateTraceId(HttpContext context)
    {
        // Try W3C traceparent header
        if (context.Request.Headers.TryGetValue("traceparent", out var traceParentHeader))
        {
            var parts = traceParentHeader.ToString().Split('-');
            if (parts.Length >= 2)
            {
                return parts[1]; // Extract trace-id
            }
        }

        // Use Activity.TraceId from ASP.NET Core
        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("D");
    }

    /// <summary>
    /// Generates a new span ID for this request
    /// </summary>
    private string GenerateSpanId()
    {
        var bytes = new byte[8];
        using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToHexString(bytes);
    }
}

/// <summary>
/// Context class for accessing correlation/trace IDs
/// </summary>
public class CorrelationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current correlation ID
    /// </summary>
    public string CorrelationId =>
        _httpContextAccessor.HttpContext?.Items["CorrelationId"] as string
        ?? Guid.NewGuid().ToString("D");

    /// <summary>
    /// Gets the current trace ID
    /// </summary>
    public string TraceId =>
        _httpContextAccessor.HttpContext?.Items["TraceId"] as string
        ?? Guid.NewGuid().ToString("D");

    /// <summary>
    /// Gets the current span ID
    /// </summary>
    public string SpanId =>
        _httpContextAccessor.HttpContext?.Items["SpanId"] as string
        ?? Guid.NewGuid().ToString("D");

    /// <summary>
    /// Gets request start time
    /// </summary>
    public DateTime RequestStartTime =>
        _httpContextAccessor.HttpContext?.Items["RequestStartTime"] as DateTime?
        ?? DateTime.UtcNow;

    /// <summary>
    /// Gets request duration
    /// </summary>
    public TimeSpan RequestDuration =>
        DateTime.UtcNow - RequestStartTime;
}

/// <summary>
/// Extension methods for correlation ID
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Adds correlation ID services and middleware
    /// </summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CorrelationContext>();
        return services;
    }

    /// <summary>
    /// Uses correlation ID middleware
    /// Must be called early in the middleware pipeline
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Gets correlation context from HTTP context
    /// </summary>
    public static CorrelationContext GetCorrelationContext(this HttpContext context)
    {
        return context.RequestServices.GetRequiredService<CorrelationContext>();
    }
}

/// <summary>
/// HTTP client factory extension for automatic correlation ID propagation
/// </summary>
public class CorrelationIdHttpClientHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdHttpClientHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString("D");

        request.Headers.Add("X-Correlation-ID", correlationId);

        // Add W3C traceparent header if available
        var traceId = _httpContextAccessor.HttpContext?.Items["TraceId"]?.ToString();
        var spanId = _httpContextAccessor.HttpContext?.Items["SpanId"]?.ToString();

        if (!string.IsNullOrEmpty(traceId) && !string.IsNullOrEmpty(spanId))
        {
            // Format: 00-trace-id-span-id-01
            var traceParent = $"00-{traceId}-{spanId}-01";
            request.Headers.Add("traceparent", traceParent);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Extension method to configure HTTP client with correlation ID
/// </summary>
public static class CorrelationIdHttpClientExtensions
{
    /// <summary>
    /// Adds correlation ID handler to HTTP client
    /// </summary>
    public static IHttpClientBuilder AddCorrelationIdHandler(
        this IHttpClientBuilder clientBuilder)
    {
        return clientBuilder.AddHttpMessageHandler<CorrelationIdHttpClientHandler>();
    }
}
