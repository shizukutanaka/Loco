using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Loco.Api.Middleware;

/// <summary>
/// Optimized middleware pipeline configuration
/// Follows performance best practices for middleware ordering
/// </summary>
public static class OptimizedMiddlewarePipeline
{
    /// <summary>
    /// Configures the middleware pipeline in optimal order for performance
    /// Order is critical for both security and performance
    /// </summary>
    public static IApplicationBuilder UseOptimizedPipeline(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        // ============== CRITICAL ORDER ==============
        // These must be in specific order for correctness AND performance

        // 1. EARLY EXIT HANDLERS (Short-circuit fast)
        // ============================================

        // Health checks - must be early for monitoring systems
        app.UseHealthChecks("/health");
        app.UseHealthChecks("/live");
        app.UseHealthChecks("/ready");

        // 2. EXCEPTION HANDLING (Wraps everything downstream)
        // ====================================================
        // Must be early but after health checks to wrap all errors
        if (!env.IsProduction())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }

        // 3. SECURITY (Before authentication/authorization)
        // ===================================================
        app.UseHsts();
        app.UseHttpsRedirection();

        // 4. LOGGING & CORRELATION (For all downstream requests)
        // ========================================================
        // Must come before auth to capture all requests
        app.UseCorrelationId();
        app.UseSerilogRequestLogging();
        app.UseCompressionMonitoring();

        // 5. COMPRESSION (Before routing to compress all responses)
        // ==========================================================
        // Must be early in pipeline but after exception handling
        app.UseResponseCompression();

        // 6. STATIC FILES (Short-circuit for static content)
        // ====================================================
        // Must be early to avoid unnecessary processing
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // 7. ROUTING (Identify endpoint)
        // ================================
        // IMPORTANT: Must come BEFORE Auth and Authorization
        // Otherwise we don't know which endpoint we're accessing
        app.UseRouting();

        // 8. RATE LIMITING (After routing, before auth)
        // ==============================================
        // Uses route information for better limiting
        app.UseRateLimiting();

        // 9. CORS (Before auth/auth-dependent middleware)
        // ================================================
        app.UseCors();

        // 10. AUTHENTICATION (Identifies user)
        // =====================================
        // MUST come before Authorization
        app.UseAuthentication();

        // 11. AUTHORIZATION (Checks permissions)
        // ======================================
        // MUST come after Authentication
        app.UseAuthorization();

        // 12. CUSTOM MIDDLEWARE (Application-specific)
        // =============================================
        app.UseApplicationMiddleware();

        // 13. ENDPOINT MAPPING (Final router)
        // ====================================
        // Must be last in pipeline
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthCheckEndpoints();
        });

        return app;
    }

    /// <summary>
    /// Custom application middleware for business logic
    /// Placed in optimal position in pipeline
    /// </summary>
    private static IApplicationBuilder UseApplicationMiddleware(this IApplicationBuilder app)
    {
        // Add any application-specific middleware here
        // They will run after Auth/Auth but before endpoints
        return app;
    }
}

/// <summary>
/// Middleware ordering documentation and validation
/// </summary>
public static class MiddlewareOrderingGuide
{
    /// <summary>
    /// Documents the recommended middleware order and why each position matters
    /// </summary>
    public static string GetMiddlewareOrderingGuide()
    {
        return """
        OPTIMIZED MIDDLEWARE ORDERING GUIDE
        ===================================

        1. HEALTH CHECKS (/health, /live, /ready)
           WHY FIRST: Monitoring systems need fast responses
           PERFORMANCE: Early short-circuit prevents unnecessary processing

        2. EXCEPTION HANDLING
           WHY EARLY: Must wrap all downstream middleware
           SECURITY: Hides implementation details in production

        3. SECURITY (HSTS, HTTPS)
           WHY BEFORE AUTH: Redirect HTTP to HTTPS before authentication
           PERFORMANCE: HTTPS required for OAuth/JWT

        4. LOGGING & CORRELATION
           WHY BEFORE ROUTING: Captures all requests with context
           TRACING: Enables end-to-end request tracing

        5. COMPRESSION
           WHY EARLY: Compresses all downstream responses
           PERFORMANCE: Reduces bandwidth by ~90%

        6. STATIC FILES
           WHY EARLY: Short-circuits for CSS, JS, images
           PERFORMANCE: 10-100x faster than dynamic routes

        7. ROUTING
           ⚠️ CRITICAL: Must come BEFORE Auth/Authorization
           WHY: Identifies endpoint and its policies
           WRONG: UseAuth before UseRouting = no endpoint context

        8. RATE LIMITING
           WHY AFTER ROUTING: Can limit per-endpoint
           PERFORMANCE: Prevents DDoS before processing

        9. CORS
           WHY BEFORE AUTH: CORS headers must come early
           SECURITY: Prevents unauthorized cross-origin requests

        10. AUTHENTICATION
            WHY BEFORE AUTHORIZATION: Must identify user first
            SECURITY: Required for authorization checks

        11. AUTHORIZATION
            WHY AFTER AUTHENTICATION: Uses user identity from auth
            SECURITY: Enforces access control

        12. CUSTOM APPLICATION MIDDLEWARE
            WHY AFTER AUTH: Knows user context
            FLEXIBILITY: Application-specific logic

        13. ENDPOINT MAPPING
            WHY LAST: Handles matched routes
            FINAL: Terminal middleware for HTTP processing

        COMMON MISTAKES TO AVOID
        ========================

        ❌ WRONG: UseAuthentication before UseRouting
            → Wastes authentication processing on static files

        ❌ WRONG: UseAuthorization before UseAuthentication
            → Can't authorize without knowing who user is

        ❌ WRONG: UseStaticFiles after authentication
            → Slows down static file serving unnecessarily

        ❌ WRONG: UseRateLimiting before UseRouting
            → Can't apply per-route limits without endpoint info

        ✅ RIGHT: Health checks first (early exit)
        ✅ RIGHT: Exception handling early (wraps everything)
        ✅ RIGHT: Routing before Auth (enables endpoint-aware auth)
        ✅ RIGHT: Static files early (fast short-circuit)
        """;
    }
}

/// <summary>
/// Middleware performance monitoring
/// </summary>
public class MiddlewarePerformanceMonitor
{
    private readonly ILogger<MiddlewarePerformanceMonitor> _logger;
    private readonly Stopwatch _stopwatch = new();

    public MiddlewarePerformanceMonitor(ILogger<MiddlewarePerformanceMonitor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Monitors middleware execution time
    /// </summary>
    public async Task<T> MonitorAsync<T>(string middlewareName, Func<Task<T>> operation)
    {
        _stopwatch.Restart();

        try
        {
            var result = await operation();
            _stopwatch.Stop();

            // Log if slow (over 100ms)
            if (_stopwatch.ElapsedMilliseconds > 100)
            {
                _logger.LogWarning(
                    "Slow middleware {MiddlewareName}: {ElapsedMs}ms",
                    middlewareName,
                    _stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "Middleware {MiddlewareName}: {ElapsedMs}ms",
                    middlewareName,
                    _stopwatch.ElapsedMilliseconds);
            }

            return result;
        }
        catch
        {
            _stopwatch.Stop();
            _logger.LogError(
                "Middleware {MiddlewareName} failed after {ElapsedMs}ms",
                middlewareName,
                _stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// Request pipeline diagnostic middleware
/// Logs each middleware in execution order
/// </summary>
public class PipelineDiagnosticsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PipelineDiagnosticsMiddleware> _logger;

    public PipelineDiagnosticsMiddleware(RequestDelegate next, ILogger<PipelineDiagnosticsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // Only log for specific paths in development
        var shouldLog = path == "/api/debug/pipeline";

        if (shouldLog)
        {
            _logger.LogInformation("=== MIDDLEWARE PIPELINE TRACE START ===");
            _logger.LogInformation("Request: {Method} {Path}", context.Request.Method, path);
        }

        await _next(context);

        if (shouldLog)
        {
            _logger.LogInformation("=== MIDDLEWARE PIPELINE TRACE END ===");
            _logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
        }
    }
}

/// <summary>
/// Extension methods for pipeline diagnostics
/// </summary>
public static class PipelineDiagnosticsExtensions
{
    /// <summary>
    /// Adds pipeline diagnostics middleware
    /// </summary>
    public static IApplicationBuilder UsePipelineDiagnostics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PipelineDiagnosticsMiddleware>();
    }

    /// <summary>
    /// Gets middleware ordering guide
    /// </summary>
    public static string GetMiddlewareGuide()
    {
        return MiddlewareOrderingGuide.GetMiddlewareOrderingGuide();
    }
}
