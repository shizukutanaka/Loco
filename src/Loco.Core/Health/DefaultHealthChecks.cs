using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Health;

/// <summary>
/// Default health checks for Loco platform components
/// </summary>

/// <summary>
/// Health check for disk space availability
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly string _path;
    private readonly long _thresholdBytes;

    public string Name => "DiskSpace";

    public DiskSpaceHealthCheck(string path, long thresholdBytes = 1_073_741_824) // 1GB
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _thresholdBytes = thresholdBytes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = Name };

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(_path) ?? _path);
            var availableSpace = driveInfo.AvailableFreeSpace;

            result.Data["AvailableBytes"] = availableSpace;
            result.Data["TotalBytes"] = driveInfo.TotalSize;
            result.Data["ThresholdBytes"] = _thresholdBytes;
            result.Data["UsedPercent"] = Math.Round(100.0 * (driveInfo.TotalSize - availableSpace) / driveInfo.TotalSize, 2);

            if (availableSpace < _thresholdBytes)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Disk space below threshold: {availableSpace:N0} bytes available";
            }
            else if (availableSpace < _thresholdBytes * 2)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Disk space low: {availableSpace:N0} bytes available";
            }
            else
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Disk space available: {availableSpace:N0} bytes";
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Failed to check disk space: {ex.Message}";
            result.Exception = ex;
            return result;
        }
    }
}

/// <summary>
/// Health check for memory availability
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;

    public string Name => "Memory";

    public MemoryHealthCheck(long thresholdBytes = 536_870_912) // 512MB
    {
        _thresholdBytes = thresholdBytes;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = Name };

        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var usedMemory = currentProcess.WorkingSet64;
            var totalMemory = GC.GetTotalMemory(false);

            result.Data["UsedBytes"] = usedMemory;
            result.Data["TotalManagedBytes"] = totalMemory;
            result.Data["ThresholdBytes"] = _thresholdBytes;

            if (usedMemory > _thresholdBytes)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"High memory usage: {usedMemory / 1024 / 1024}MB";
            }
            else
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Memory usage normal: {usedMemory / 1024 / 1024}MB";
            }

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Failed to check memory: {ex.Message}";
            result.Exception = ex;
            return result;
        }
    }
}

/// <summary>
/// Health check for directory accessibility
/// </summary>
public class DirectoryAccessibilityHealthCheck : IHealthCheck
{
    private readonly string _path;
    private readonly string _checkName;

    public string Name => _checkName;

    public DirectoryAccessibilityHealthCheck(string path, string checkName = "DirectoryAccess")
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _checkName = checkName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = Name };

        try
        {
            if (!Directory.Exists(_path))
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Directory does not exist: {_path}";
                result.Data["Path"] = _path;
                return await Task.FromResult(result);
            }

            // Try to write a test file
            var testFile = Path.Combine(_path, ".health-check-" + Guid.NewGuid().ToString("N"));
            try
            {
                await System.IO.File.WriteAllTextAsync(testFile, "health-check", cancellationToken);
                System.IO.File.Delete(testFile);

                result.Status = HealthStatus.Healthy;
                result.Description = $"Directory is accessible and writable: {_path}";
                result.Data["Path"] = _path;
                return result;
            }
            catch (Exception writeEx)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Description = $"Directory is not writable: {writeEx.Message}";
                result.Data["Path"] = _path;
                result.Exception = writeEx;
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Failed to check directory: {ex.Message}";
            result.Data["Path"] = _path;
            result.Exception = ex;
            return result;
        }
    }
}

/// <summary>
/// Health check for process responsiveness
/// </summary>
public class ProcessResponsivenessHealthCheck : IHealthCheck
{
    public string Name => "ProcessResponsiveness";

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult { Name = Name };

        try
        {
            // Simple health check: measure if we can complete an async operation quickly
            var startTime = DateTime.UtcNow;
            await Task.Delay(10, cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            result.Data["ResponseTimeMs"] = duration.TotalMilliseconds;

            if (duration.TotalMilliseconds > 1000)
            {
                result.Status = HealthStatus.Degraded;
                result.Description = $"Process is slow to respond: {duration.TotalMilliseconds:F2}ms";
            }
            else
            {
                result.Status = HealthStatus.Healthy;
                result.Description = $"Process is responsive: {duration.TotalMilliseconds:F2}ms";
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"Failed to check responsiveness: {ex.Message}";
            result.Exception = ex;
            return result;
        }
    }
}
