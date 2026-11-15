using Loco.Core.Practical;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PracticalHealthStatus = Loco.Core.Practical.HealthStatus;

namespace Loco.Api.HealthChecks;

/// <summary>
/// Custom health check for Loco system.
///
/// Backed by <see cref="SimpleHealthCheckService"/> from Loco.Core.Practical.
/// This class previously depended on an <c>IHealthCheckService</c> in a
/// <c>Loco.Core.Health</c> namespace that does not exist anywhere in the
/// repository; it has been rewired to the health-check service that does.
/// </summary>
public class LocoHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly SimpleHealthCheckService _healthCheckService;
    private readonly ILogger<LocoHealthCheck> _logger;

    public LocoHealthCheck(SimpleHealthCheckService healthCheckService, ILogger<LocoHealthCheck> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _healthCheckService.CheckHealthAsync();

            return report.OverallStatus switch
            {
                PracticalHealthStatus.Healthy =>
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Loco system is healthy"),
                PracticalHealthStatus.Degraded =>
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"System has issues: {DescribeIssues(report)}"),
                _ =>
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                        $"System is unhealthy: {DescribeIssues(report)}"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }

    private static string DescribeIssues(Loco.Core.Practical.HealthReport report)
    {
        var issues = report.Results
            .Where(r => r.Status != PracticalHealthStatus.Healthy)
            .Select(r => $"{r.Name}: {r.Message ?? r.Status.ToString()}");
        return string.Join(", ", issues);
    }
}