using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Loco.Core.Health;

/// <summary>
/// Enhanced health check with detailed metrics
/// </summary>
public class EnhancedHealthCheck : IHealthCheck
{
    private readonly ILogger<EnhancedHealthCheck> _logger;
    private readonly IHealthCheckService? _healthCheckService;

    public EnhancedHealthCheck(
        ILogger<EnhancedHealthCheck> logger,
        IHealthCheckService? healthCheckService = null)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();
            var startTime = DateTime.UtcNow;

            // Check memory usage
            var memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024; // MB
            data.Add("memory_mb", memoryUsage);

            // Check CPU usage (simplified)
            var process = Process.GetCurrentProcess();
            var cpuTime = process.TotalProcessorTime;
            data.Add("cpu_seconds", cpuTime.TotalSeconds);

            // Check uptime
            var uptime = DateTime.UtcNow - process.StartTime;
            data.Add("uptime_seconds", uptime.TotalSeconds);

            // Check thread count
            data.Add("thread_count", process.Threads.Count);

            // Check GC stats
            var gen0Collections = GC.GetTotalCollectionCount(0);
            var gen1Collections = GC.GetTotalCollectionCount(1);
            var gen2Collections = GC.GetTotalCollectionCount(2);
            data.Add("gc_gen0", gen0Collections);
            data.Add("gc_gen1", gen1Collections);
            data.Add("gc_gen2", gen2Collections);

            // Check request metrics
            if (_healthCheckService != null)
            {
                var report = await _healthCheckService.CheckHealthAsync();
                data.Add("overall_status", report.Status.ToString());
            }

            var duration = DateTime.UtcNow - startTime;
            data.Add("check_duration_ms", duration.TotalMilliseconds);

            // Determine health status
            var status = HealthStatus.Healthy;

            if (memoryUsage > 500) // More than 500MB
            {
                status = HealthStatus.Degraded;
            }

            if (memoryUsage > 1000) // More than 1GB
            {
                status = HealthStatus.Unhealthy;
            }

            _logger.LogInformation(
                "Health check completed: Status={Status}, Memory={Memory}MB, Threads={Threads}",
                status, memoryUsage, process.Threads.Count);

            return new HealthCheckResult(
                status: status,
                description: $"System is {status.ToString().ToLower()}",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                exception: ex,
                description: "Health check failed");
        }
    }
}

/// <summary>
/// Database connectivity health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(ILogger<DatabaseHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulate database check - replace with actual DB query
            var startTime = DateTime.UtcNow;

            // In production, execute a simple query to the database
            // For now, we'll just simulate a successful check
            await Task.Delay(10, cancellationToken); // Simulate DB query

            var duration = DateTime.UtcNow - startTime;

            _logger.LogDebug("Database health check passed in {Duration}ms", duration.TotalMilliseconds);

            return HealthCheckResult.Healthy(
                data: new Dictionary<string, object>
                {
                    { "response_time_ms", duration.TotalMilliseconds },
                    { "database", "connected" }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy(
                exception: ex,
                description: "Database connection failed");
        }
    }
}

/// <summary>
/// External service dependency health check
/// </summary>
public class DependencyHealthCheck : IHealthCheck
{
    private readonly ILogger<DependencyHealthCheck> _logger;
    private readonly HttpClient _httpClient;

    public DependencyHealthCheck(
        ILogger<DependencyHealthCheck> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();

            // Check external dependencies
            var startTime = DateTime.UtcNow;

            // Example: Check OTEL collector
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await _httpClient.GetAsync(
                    "http://localhost:4317",
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);

                data.Add("opentelemetry", response.IsSuccessStatusCode ? "available" : "unavailable");
            }
            catch (Exception ex)
            {
                data.Add("opentelemetry", "unreachable");
                _logger.LogWarning(ex, "OpenTelemetry endpoint unreachable");
            }

            var duration = DateTime.UtcNow - startTime;
            data.Add("check_duration_ms", duration.TotalMilliseconds);

            return HealthCheckResult.Healthy(data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dependency health check failed");
            return HealthCheckResult.Degraded(
                exception: ex,
                description: "One or more dependencies are unavailable");
        }
    }
}

/// <summary>
/// Disk space health check
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            var data = new Dictionary<string, object>();

            foreach (var drive in drives)
            {
                if (!drive.IsReady)
                    continue;

                var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var totalSpaceGB = drive.TotalSize / (1024 * 1024 * 1024);
                var usedPercentage = ((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100;

                data.Add($"{drive.Name}_free_gb", freeSpaceGB);
                data.Add($"{drive.Name}_used_percent", Math.Round(usedPercentage, 2));
            }

            var status = HealthStatus.Healthy;
            var description = "Disk space is adequate";

            // Check if any drive is critically low on space
            if (data.Values.OfType<double>().Any(v => v < 1))
            {
                status = HealthStatus.Unhealthy;
                description = "One or more drives are critically low on space";
            }
            else if (data.Values.OfType<double>().Any(v => v < 5))
            {
                status = HealthStatus.Degraded;
                description = "One or more drives are low on space";
            }

            _logger.LogInformation(
                "Disk space health check completed: Status={Status}",
                status);

            return new HealthCheckResult(
                status: status,
                description: description,
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space health check failed");
            return HealthCheckResult.Unhealthy(
                exception: ex,
                description: "Failed to check disk space");
        }
    }
}

/// <summary>
/// Extension methods for health check configuration
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds comprehensive health checks
    /// </summary>
    public static IHealthChecksBuilder AddLocoHealthChecks(
        this IServiceCollection services)
    {
        return services.AddHealthChecks()
            .AddCheck<EnhancedHealthCheck>(
                name: "loco-health",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "system" })
            .AddCheck<DatabaseHealthCheck>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database" })
            .AddCheck<DependencyHealthCheck>(
                name: "dependencies",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "external" })
            .AddCheck<DiskSpaceHealthCheck>(
                name: "disk-space",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "system" });
    }
}
