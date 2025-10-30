using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Health;

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    /// <summary>Service is running normally</summary>
    Healthy = 0,

    /// <summary>Service is running but with warnings</summary>
    Degraded = 1,

    /// <summary>Service is not running or critical failure</summary>
    Unhealthy = 2
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheckResult
{
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public TimeSpan ResponseTime { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Overall health check response
/// </summary>
public class HealthCheckResponse
{
    public HealthStatus Status { get; set; }
    public DateTime Timestamp { get; set; }
    public List<HealthCheckResult> Checks { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    public HealthCheckResponse()
    {
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Interface for health check providers
/// </summary>
public interface IHealthCheck
{
    /// <summary>Gets the name of this health check</summary>
    string Name { get; }

    /// <summary>Executes the health check</summary>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Centralized health check service for production monitoring
/// </summary>
public class HealthCheckService
{
    private readonly List<IHealthCheck> _checks = new();
    private readonly ILogger? _logger;
    private DateTime _lastCheckTime = DateTime.UtcNow;
    private HealthCheckResponse? _cachedResponse;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30);

    public HealthCheckService(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a health check
    /// </summary>
    public void RegisterCheck(IHealthCheck check)
    {
        if (check == null)
            throw new ArgumentNullException(nameof(check));

        _checks.Add(check);
        _logger?.LogInformation("Health check registered: {CheckName}", check.Name);
    }

    /// <summary>
    /// Executes all health checks and returns aggregate status
    /// </summary>
    public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            // Return cached response if still valid
            if (_cachedResponse != null && DateTime.UtcNow - _lastCheckTime < _cacheTimeout)
            {
                return _cachedResponse;
            }

            var response = new HealthCheckResponse();
            var startTime = DateTime.UtcNow;

            // Execute all checks in parallel
            var checkTasks = _checks.Select(async check =>
            {
                var checkStartTime = DateTime.UtcNow;
                try
                {
                    var result = await check.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
                    result.ResponseTime = DateTime.UtcNow - checkStartTime;
                    return result;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Health check failed: {CheckName}", check.Name);
                    return new HealthCheckResult
                    {
                        Name = check.Name,
                        Status = HealthStatus.Unhealthy,
                        Description = $"Check failed with exception: {ex.Message}",
                        Exception = ex,
                        ResponseTime = DateTime.UtcNow - checkStartTime
                    };
                }
            }).ToList();

            var results = await Task.WhenAll(checkTasks).ConfigureAwait(false);

            response.Checks.AddRange(results.OrderBy(r => r.Name));

            // Determine overall status
            if (results.Any(r => r.Status == HealthStatus.Unhealthy))
                response.Status = HealthStatus.Unhealthy;
            else if (results.Any(r => r.Status == HealthStatus.Degraded))
                response.Status = HealthStatus.Degraded;
            else
                response.Status = HealthStatus.Healthy;

            // Add metadata
            response.Metadata["TotalChecks"] = _checks.Count;
            response.Metadata["ChecksDuration"] = (DateTime.UtcNow - startTime).TotalMilliseconds;
            response.Metadata["HealthyChecks"] = results.Count(r => r.Status == HealthStatus.Healthy);
            response.Metadata["DegradedChecks"] = results.Count(r => r.Status == HealthStatus.Degraded);
            response.Metadata["UnhealthyChecks"] = results.Count(r => r.Status == HealthStatus.Unhealthy);

            _cachedResponse = response;
            _lastCheckTime = DateTime.UtcNow;

            return response;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <summary>
    /// Gets a specific health check result
    /// </summary>
    public HealthCheckResult? GetCheckResult(string checkName)
    {
        return _cachedResponse?.Checks.FirstOrDefault(c => c.Name == checkName);
    }

    /// <summary>
    /// Clears the cached response
    /// </summary>
    public void InvalidateCache()
    {
        _cachedResponse = null;
        _lastCheckTime = DateTime.MinValue;
    }
}
