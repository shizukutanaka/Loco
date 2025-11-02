using Loco.Core.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Loco.Api.HealthChecks;

/// <summary>
/// Custom health check for Loco system
/// </summary>
public class LocoHealthCheck : IHealthCheck
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<LocoHealthCheck> _logger;

    public LocoHealthCheck(IHealthCheckService healthCheckService, ILogger<LocoHealthCheck> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _healthCheckService.CheckHealthAsync();

            if (health.IsHealthy)
            {
                return HealthCheckResult.Healthy("Loco system is healthy");
            }

            var issues = string.Join(", ", health.Issues);
            return HealthCheckResult.Degraded($"System has issues: {issues}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
