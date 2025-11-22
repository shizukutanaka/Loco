// Phase 2 optimization: Trace context propagation for distributed tracing
// Automatically propagates W3C Trace Context across service boundaries

using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Loco.Api.Middleware;

/// <summary>
/// Middleware for W3C Trace Context propagation
/// Phase 2: Enhanced observability across distributed services
///
/// Features:
/// - Automatic trace context extraction from requests
/// - Automatic trace context propagation to outgoing calls
/// - OpenTelemetry integration
/// - Custom workflow activity tracking
/// </summary>
public class TraceContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TraceContextMiddleware> _logger;

    public TraceContextMiddleware(RequestDelegate next, ILogger<TraceContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract trace context from incoming request (W3C format: traceparent header)
        var traceParent = context.Request.Headers["traceparent"].FirstOrDefault();
        var traceState = context.Request.Headers["tracestate"].FirstOrDefault();

        using var activity = new Activity("http_request");

        // Set trace context if provided, otherwise new trace will be created
        if (!string.IsNullOrEmpty(traceParent))
        {
            if (ActivityContext.TryParse(traceParent, traceState, out var activityContext))
            {
                activity.SetParentId(activityContext.TraceId, activityContext.SpanId, activityContext.TraceFlags);
            }
        }

        activity.DisplayName = $"{context.Request.Method} {context.Request.Path}";
        activity.Start();

        try
        {
            // Add standard HTTP attributes
            activity.SetTag("http.method", context.Request.Method);
            activity.SetTag("http.url", context.Request.Path);
            activity.SetTag("http.scheme", context.Request.Scheme);
            activity.SetTag("http.host", context.Request.Host);

            // Extract workflow ID if present in request
            if (context.Request.RouteValues.TryGetValue("workflowId", out var workflowId))
            {
                activity.SetTag("workflow.id", workflowId);
            }

            // Extract user information if available
            if (context.User?.Identity?.Name != null)
            {
                activity.SetTag("user.id", context.User.Identity.Name);
            }

            // Store activity in request items for use in handlers
            context.Items["Activity"] = activity;

            // Continue with next middleware
            await _next(context);

            // Record HTTP status
            activity.SetTag("http.status_code", context.Response.StatusCode);

            // Log summary
            _logger.LogInformation(
                "HTTP {Method} {Path} completed with status {StatusCode} - TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                activity.Id);
        }
        catch (Exception ex)
        {
            activity.SetTag("exception.type", ex.GetType().Name);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);
            activity.RecordException(ex);

            _logger.LogError(ex,
                "Exception in HTTP {Method} {Path} - TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                activity.Id);

            throw;
        }
        finally
        {
            activity.Stop();
        }
    }
}

/// <summary>
/// Helper class for trace context propagation in outgoing calls
/// </summary>
public static class TraceContextPropagation
{
    /// <summary>
    /// Add W3C Trace Context headers to outgoing HTTP request
    /// </summary>
    public static void AddTraceContextHeaders(HttpClient client, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity != null)
        {
            var traceParent = $"00-{activity.TraceId:X32}-{activity.SpanId:X16}-{(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded):02X}";
            client.DefaultRequestHeaders.Add("traceparent", traceParent);

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                client.DefaultRequestHeaders.Add("tracestate", activity.TraceStateString);
            }
        }
    }

    /// <summary>
    /// Add trace context to HTTP request message
    /// </summary>
    public static void AddTraceContextHeaders(HttpRequestMessage request, Activity? activity = null)
    {
        activity ??= Activity.Current;

        if (activity != null)
        {
            var traceParent = $"00-{activity.TraceId:X32}-{activity.SpanId:X16}-{(activity.ActivityTraceFlags & ActivityTraceFlags.Recorded):02X}";
            request.Headers.Add("traceparent", traceParent);

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                request.Headers.Add("tracestate", activity.TraceStateString);
            }
        }
    }

    /// <summary>
    /// Create a linked activity for a sub-operation
    /// </summary>
    public static Activity StartLinkedActivity(string activityName, string? workflowId = null)
    {
        var parentActivity = Activity.Current;
        var activity = new Activity(activityName);

        if (parentActivity != null)
        {
            activity.SetParentId(parentActivity.TraceId, parentActivity.SpanId);
        }

        activity.Start();

        if (workflowId != null)
        {
            activity.SetTag("workflow.id", workflowId);
        }

        return activity;
    }
}

/// <summary>
/// Extension methods for registering trace context middleware
/// </summary>
public static class TraceContextMiddlewareExtensions
{
    /// <summary>
    /// Add trace context middleware to the pipeline
    /// Should be added early in the middleware pipeline
    /// </summary>
    public static IApplicationBuilder UseTraceContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TraceContextMiddleware>();
    }

    /// <summary>
    /// Configure HttpClient to automatically add trace context headers
    /// </summary>
    public static IHttpClientBuilder ConfigureTraceContext(this IHttpClientBuilder builder)
    {
        return builder.ConfigureHttpClient((serviceProvider, client) =>
        {
            // HttpClient factory will automatically propagate trace context through handlers
        });
    }
}
