using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;

namespace Loco.Core.Middleware;

/// <summary>
/// Response compression setup for ASP.NET Core
/// Supports Brotli (preferred) and Gzip compression
/// </summary>
public static class ResponseCompressionSetup
{
    /// <summary>
    /// Adds response compression services
    /// Configures Brotli as primary, Gzip as fallback
    /// </summary>
    public static IServiceCollection AddResponseCompressionServices(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Enable compression for these MIME types
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                // Application types
                "application/json",
                "application/javascript",
                "application/xml+rss",
                "application/atom+xml",
                "application/x-www-form-urlencoded",

                // Text types
                "text/plain",
                "text/css",
                "text/javascript",
                "text/xml",
                "text/html",

                // Custom API types
                "application/vnd.api+json",
                "application/ld+json",

                // SVG and fonts
                "image/svg+xml",
                "font/woff2"
            });

            // Configure compression providers
            // Order matters: Brotli first (better compression), then Gzip, then no compression
            options.Providers.Clear();
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        // Configure Brotli compression level
        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal; // Balance between speed and compression
        });

        // Configure Gzip compression level
        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }

    /// <summary>
    /// Adds response compression middleware
    /// Must be called early in the pipeline, before any middleware that writes to response
    /// </summary>
    public static IApplicationBuilder UseResponseCompressionMiddleware(this IApplicationBuilder app)
    {
        // Add response compression middleware EARLY in the pipeline
        // Order is critical - must be before authentication, routing, etc.
        app.UseResponseCompression();
        return app;
    }

    /// <summary>
    /// Production configuration with aggressive compression
    /// </summary>
    public static IServiceCollection AddProductionResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;

            // Smaller content size threshold for production (compress more)
            options.MinimumCompressionSize = 256; // Instead of default 1024

            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "application/javascript",
                "text/plain",
                "text/css",
                "text/javascript",
                "text/xml",
                "text/html",
                "application/vnd.api+json",
                "image/svg+xml"
            });

            options.Providers.Clear();
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        return services;
    }

    /// <summary>
    /// Development configuration with faster compression
    /// </summary>
    public static IServiceCollection AddDevelopmentResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "text/plain",
                "text/html"
            });

            options.Providers.Clear();
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest; // Faster in dev for better experience
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        return services;
    }
}

/// <summary>
/// Compression statistics for monitoring
/// </summary>
public class CompressionStatistics
{
    public long TotalRequests { get; set; }
    public long CompressedRequests { get; set; }
    public long UncompressedSize { get; set; }
    public long CompressedSize { get; set; }

    public double CompressionPercentage =>
        CompressedSize > 0 ? (1 - (double)CompressedSize / UncompressedSize) * 100 : 0;

    public double AverageSavings =>
        CompressedRequests > 0 ? UncompressedSize - CompressedSize : 0;

    public string GetSummary() =>
        $"Requests: {TotalRequests}, Compressed: {CompressedRequests}, " +
        $"Ratio: {CompressionPercentage:F2}%, Savings: {AverageSavings:F0} bytes";
}

/// <summary>
/// Middleware for tracking compression statistics
/// </summary>
public class CompressionMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CompressionMonitoringMiddleware> _logger;
    private static CompressionStatistics _statistics = new();

    public CompressionMonitoringMiddleware(RequestDelegate next, ILogger<CompressionMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;

        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            await _next(context);

            // Check if response was compressed
            var contentEncoding = context.Response.Headers.ContentEncoding.FirstOrDefault();
            var uncompressedSize = responseBody.Length;
            var isCompressed = !string.IsNullOrEmpty(contentEncoding);

            // Log compression info
            _logger.LogDebug(
                "Response compression: {IsCompressed}, Encoding: {Encoding}, Size: {Size}",
                isCompressed,
                contentEncoding ?? "none",
                uncompressedSize);

            // Update statistics
            lock (_statistics)
            {
                _statistics.TotalRequests++;
                _statistics.UncompressedSize += uncompressedSize;

                if (isCompressed)
                {
                    _statistics.CompressedRequests++;
                    _statistics.CompressedSize += uncompressedSize; // Compressed size (approximation)
                }
            }

            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    /// <summary>
    /// Gets current compression statistics
    /// </summary>
    public static CompressionStatistics GetStatistics()
    {
        lock (_statistics)
        {
            return new CompressionStatistics
            {
                TotalRequests = _statistics.TotalRequests,
                CompressedRequests = _statistics.CompressedRequests,
                UncompressedSize = _statistics.UncompressedSize,
                CompressedSize = _statistics.CompressedSize
            };
        }
    }

    /// <summary>
    /// Resets statistics
    /// </summary>
    public static void ResetStatistics()
    {
        lock (_statistics)
        {
            _statistics = new CompressionStatistics();
        }
    }
}

/// <summary>
/// Extension methods for compression monitoring
/// </summary>
public static class CompressionMonitoringExtensions
{
    /// <summary>
    /// Adds compression monitoring middleware
    /// </summary>
    public static IApplicationBuilder UseCompressionMonitoring(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CompressionMonitoringMiddleware>();
    }

    /// <summary>
    /// Gets compression statistics
    /// </summary>
    public static CompressionStatistics GetCompressionStatistics()
    {
        return CompressionMonitoringMiddleware.GetStatistics();
    }
}
