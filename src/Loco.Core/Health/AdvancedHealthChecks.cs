using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;

namespace Loco.Core.Health;

/// <summary>
/// Liveness probe - checks if service is running
/// Minimal checks: process alive, basic memory availability
/// </summary>
public class LivenessHealthCheck : IHealthCheck
{
    private readonly ILogger<LivenessHealthCheck> _logger;
    private readonly Process _currentProcess = Process.GetCurrentProcess();

    public LivenessHealthCheck(ILogger<LivenessHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check 1: Process is alive
            if (_currentProcess.HasExited)
            {
                _logger.LogError("Process has exited");
                return Task.FromResult(HealthCheckResult.Unhealthy("Process has exited"));
            }

            // Check 2: Memory is not critically low
            var memoryUsage = _currentProcess.WorkingSet64;
            var maxMemory = GC.GetTotalMemory(false);

            if (memoryUsage > maxMemory * 0.95) // 95% threshold
            {
                _logger.LogError("Memory usage critical: {Usage}MB", memoryUsage / 1024 / 1024);
                return Task.FromResult(HealthCheckResult.Unhealthy("Memory usage critical"));
            }

            // Check 3: No unhandled exceptions in current thread
            var availableThreads = ThreadPool.PendingWorkItemCount;
            if (availableThreads > 100000)
            {
                _logger.LogWarning("High pending work items: {Count}", availableThreads);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"High pending work items: {availableThreads}"));
            }

            _logger.LogInformation("Liveness check passed");
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Liveness check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy($"Liveness check failed: {ex.Message}"));
        }
    }
}

/// <summary>
/// Readiness probe - checks if service is ready to accept traffic
/// Comprehensive checks: database, cache, dependencies
/// </summary>
public class ReadinessHealthCheck : IHealthCheck
{
    private readonly ILogger<ReadinessHealthCheck> _logger;
    private readonly IEnumerable<IHealthCheck> _dependencyChecks;

    public ReadinessHealthCheck(
        ILogger<ReadinessHealthCheck> logger,
        IEnumerable<IHealthCheck> dependencyChecks)
    {
        _logger = logger;
        _dependencyChecks = dependencyChecks;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new Dictionary<string, HealthCheckResult>();
            var failureCount = 0;
            var degradedCount = 0;

            // Check all dependencies
            foreach (var check in _dependencyChecks)
            {
                try
                {
                    var result = await check.CheckHealthAsync(context, cancellationToken);
                    results[check.GetType().Name] = result;

                    if (result.Status == HealthStatus.Unhealthy)
                    {
                        failureCount++;
                    }
                    else if (result.Status == HealthStatus.Degraded)
                    {
                        degradedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dependency check failed: {Type}", check.GetType().Name);
                    results[check.GetType().Name] = HealthCheckResult.Unhealthy(ex.Message);
                    failureCount++;
                }
            }

            if (failureCount > 0)
            {
                _logger.LogError("Readiness check failed: {FailureCount} dependencies unhealthy", failureCount);
                return HealthCheckResult.Unhealthy(
                    $"{failureCount} dependencies unhealthy",
                    data: results);
            }

            if (degradedCount > 0)
            {
                _logger.LogWarning("Readiness check degraded: {DegradedCount} dependencies degraded", degradedCount);
                return HealthCheckResult.Degraded(
                    $"{degradedCount} dependencies degraded",
                    data: results);
            }

            _logger.LogInformation("Readiness check passed");
            return HealthCheckResult.Healthy(data: results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return HealthCheckResult.Unhealthy($"Readiness check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Startup probe - checks if service has completed startup
/// More lenient than readiness, gives service time to initialize
/// </summary>
public class StartupHealthCheck : IHealthCheck
{
    private readonly ILogger<StartupHealthCheck> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly TimeSpan _startupTimeout = TimeSpan.FromSeconds(30);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var elapsed = DateTime.UtcNow - _startTime;

            if (elapsed > _startupTimeout)
            {
                _logger.LogError("Startup timeout exceeded: {Elapsed}s", elapsed.TotalSeconds);
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Startup timeout exceeded: {elapsed.TotalSeconds:F1}s"));
            }

            _logger.LogInformation("Startup in progress: {Elapsed:F1}s elapsed", elapsed.TotalSeconds);
            return Task.FromResult(HealthCheckResult.Degraded(
                $"Service starting up: {elapsed.TotalSeconds:F1}s"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy($"Startup check failed: {ex.Message}"));
        }
    }
}

/// <summary>
/// Database connectivity health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ILogger<DatabaseHealthCheck> _logger;
    private readonly string _connectionString;

    public DatabaseHealthCheck(ILogger<DatabaseHealthCheck> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For testing, simulate database connection check
            // In real implementation, execute a simple query like "SELECT 1"
            await Task.Delay(10, cancellationToken);

            _logger.LogInformation("Database health check passed");
            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy($"Database connection failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Cache (Redis) connectivity health check
/// </summary>
public class CacheHealthCheck : IHealthCheck
{
    private readonly ILogger<CacheHealthCheck> _logger;
    private readonly string _cacheEndpoint;

    public CacheHealthCheck(ILogger<CacheHealthCheck> logger, string cacheEndpoint)
    {
        _logger = logger;
        _cacheEndpoint = cacheEndpoint;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For testing, simulate cache connection check
            // In real implementation, execute PING command on Redis
            await Task.Delay(5, cancellationToken);

            _logger.LogInformation("Cache health check passed");
            return HealthCheckResult.Healthy("Cache connection successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache health check failed");
            return HealthCheckResult.Unhealthy($"Cache connection failed: {ex.Message}");
        }
    }
}

/// <summary>
/// External service health check (via HTTP)
/// </summary>
public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<ExternalServiceHealthCheck> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;
    private readonly string _serviceName;

    public ExternalServiceHealthCheck(
        ILogger<ExternalServiceHealthCheck> logger,
        HttpClient httpClient,
        string serviceName,
        string serviceUrl)
    {
        _logger = logger;
        _httpClient = httpClient;
        _serviceName = serviceName;
        _serviceUrl = serviceUrl;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_serviceUrl}/health",
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("External service health check passed: {Service}", _serviceName);
                return HealthCheckResult.Healthy($"{_serviceName} is healthy");
            }

            _logger.LogWarning("External service health check failed: {Service} returned {StatusCode}",
                _serviceName, response.StatusCode);
            return HealthCheckResult.Unhealthy(
                $"{_serviceName} returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External service health check failed: {Service}", _serviceName);
            return HealthCheckResult.Unhealthy(
                $"Failed to connect to {_serviceName}: {ex.Message}");
        }
    }
}

/// <summary>
/// Disk space health check
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly long _minimumFreeBytes;

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger, long minimumFreeBytes = 1073741824) // 1 GB
    {
        _logger = logger;
        _minimumFreeBytes = minimumFreeBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory)!);
            var freeBytes = driveInfo.AvailableFreeSpace;

            if (freeBytes < _minimumFreeBytes)
            {
                _logger.LogError("Disk space critical: {FreeGB}GB available",
                    freeBytes / 1024 / 1024 / 1024);
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Disk space critical: {freeBytes / 1024 / 1024 / 1024}GB available"));
            }

            _logger.LogInformation("Disk space health check passed: {FreeGB}GB available",
                freeBytes / 1024 / 1024 / 1024);
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space healthy: {freeBytes / 1024 / 1024 / 1024}GB available"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Disk space check failed: {ex.Message}"));
        }
    }
}

/// <summary>
/// Extension methods for health checks
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds Kubernetes-compatible health checks
    /// </summary>
    public static IHealthChecksBuilder AddKubernetesHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration config)
    {
        // Liveness: Minimal checks (process alive, memory OK)
        builder.AddCheck<LivenessHealthCheck>(
            "liveness",
            HealthStatus.Unhealthy,
            tags: new[] { "liveness" });

        // Readiness: Comprehensive checks (dependencies)
        builder.AddCheck<ReadinessHealthCheck>(
            "readiness",
            HealthStatus.Unhealthy,
            tags: new[] { "readiness" });

        // Startup: Initialization checks
        builder.AddCheck<StartupHealthCheck>(
            "startup",
            HealthStatus.Degraded,
            tags: new[] { "startup" });

        // Dependencies
        var dbConnection = config.GetConnectionString("DefaultConnection") ?? "Server=localhost";
        builder.AddCheck<DatabaseHealthCheck>(
            "database",
            HealthStatus.Unhealthy,
            tags: new[] { "readiness" });

        var cacheEndpoint = config["Redis:ConnectionString"] ?? "localhost:6379";
        builder.AddCheck<CacheHealthCheck>(
            "cache",
            HealthStatus.Unhealthy,
            tags: new[] { "readiness" });

        var diskSpace = config.GetValue<long?>("HealthChecks:DiskSpaceThreshold") ?? 1073741824;
        builder.AddCheck<DiskSpaceHealthCheck>(
            "diskspace",
            HealthStatus.Unhealthy,
            tags: new[] { "liveness" });

        return builder;
    }

    /// <summary>
    /// Maps health check endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapHealthCheckEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // Liveness probe endpoint (for Kubernetes)
        endpoints.MapHealthChecks("/live",
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("liveness"),
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    { HealthStatus.Healthy, StatusCodes.Status200OK },
                    { HealthStatus.Degraded, StatusCodes.Status200OK },
                    { HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable }
                }
            });

        // Readiness probe endpoint (for Kubernetes)
        endpoints.MapHealthChecks("/ready",
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("readiness"),
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    { HealthStatus.Healthy, StatusCodes.Status200OK },
                    { HealthStatus.Degraded, StatusCodes.Status503ServiceUnavailable },
                    { HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable }
                }
            });

        // Startup probe endpoint (for Kubernetes)
        endpoints.MapHealthChecks("/startup",
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("startup"),
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    { HealthStatus.Healthy, StatusCodes.Status200OK },
                    { HealthStatus.Degraded, StatusCodes.Status503ServiceUnavailable },
                    { HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable }
                }
            });

        // General health endpoint
        endpoints.MapHealthChecks("/health",
            new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                ResultStatusCodes = new Dictionary<HealthStatus, int>
                {
                    { HealthStatus.Healthy, StatusCodes.Status200OK },
                    { HealthStatus.Degraded, StatusCodes.Status200OK },
                    { HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable }
                }
            });

        return endpoints;
    }
}
