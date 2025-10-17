using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Diagnostics
{
    /// <summary>
    /// Advanced health check system for production monitoring.
    /// Provides detailed component health status for operational observability.
    /// </summary>
    public class AdvancedHealthCheck
    {
        private readonly Configuration.LocoConfig _config;

        public AdvancedHealthCheck(Configuration.LocoConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Performs a comprehensive health check of all system components.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Overall health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var result = new HealthCheckResult
            {
                Timestamp = DateTime.UtcNow,
                Status = HealthStatus.Healthy
            };

            // Run all health checks in parallel
            var checks = new List<Task>
            {
                CheckSystemResourcesAsync(result, cancellationToken),
                CheckDiskSpaceAsync(result, cancellationToken),
                CheckConfigurationAsync(result, cancellationToken),
                CheckDirectoriesAsync(result, cancellationToken)
            };

            await Task.WhenAll(checks).ConfigureAwait(false);

            // Determine overall status
            if (result.ComponentStatus.Values.Any(s => s == HealthStatus.Critical))
            {
                result.Status = HealthStatus.Critical;
            }
            else if (result.ComponentStatus.Values.Any(s => s == HealthStatus.Warning))
            {
                result.Status = HealthStatus.Warning;
            }

            result.Duration = DateTime.UtcNow - result.Timestamp;
            return result;
        }

        private async Task CheckSystemResourcesAsync(HealthCheckResult result, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            try
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024.0 * 1024.0);
                var memoryLimit = _config.MemoryLimitMB;

                result.Metrics["Memory_MB"] = memoryMB.ToString("F2");
                result.Metrics["Memory_Limit_MB"] = memoryLimit.ToString();

                if (memoryLimit > 0 && memoryMB > memoryLimit)
                {
                    result.ComponentStatus["Memory"] = HealthStatus.Critical;
                    result.Messages.Add($"Memory usage ({memoryMB:F2} MB) exceeded limit ({memoryLimit} MB)");
                }
                else if (memoryLimit > 0 && memoryMB > memoryLimit * 0.9)
                {
                    result.ComponentStatus["Memory"] = HealthStatus.Warning;
                    result.Messages.Add($"Memory usage ({memoryMB:F2} MB) approaching limit ({memoryLimit} MB)");
                }
                else
                {
                    result.ComponentStatus["Memory"] = HealthStatus.Healthy;
                }

                // CPU time
                var cpuTime = process.TotalProcessorTime.TotalSeconds;
                result.Metrics["CPU_Time_Seconds"] = cpuTime.ToString("F2");

                // Thread count
                result.Metrics["Thread_Count"] = process.Threads.Count.ToString();
            }
            catch (Exception ex)
            {
                result.ComponentStatus["Memory"] = HealthStatus.Critical;
                result.Messages.Add($"Failed to check memory: {ex.Message}");
            }
        }

        private async Task CheckDiskSpaceAsync(HealthCheckResult result, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            try
            {
                var logDir = _config.LogDirectory;
                if (!string.IsNullOrEmpty(logDir))
                {
                    var drive = new DriveInfo(Path.GetPathRoot(logDir) ?? "C:\\");
                    var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var totalSpaceGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);

                    result.Metrics["Disk_Free_GB"] = freeSpaceGB.ToString("F2");
                    result.Metrics["Disk_Total_GB"] = totalSpaceGB.ToString("F2");
                    result.Metrics["Disk_Usage_Percent"] = ((1 - freeSpaceGB / totalSpaceGB) * 100).ToString("F1");

                    if (freeSpaceGB < 1.0)
                    {
                        result.ComponentStatus["Disk"] = HealthStatus.Critical;
                        result.Messages.Add($"Critical disk space: {freeSpaceGB:F2} GB free");
                    }
                    else if (freeSpaceGB < 5.0)
                    {
                        result.ComponentStatus["Disk"] = HealthStatus.Warning;
                        result.Messages.Add($"Low disk space: {freeSpaceGB:F2} GB free");
                    }
                    else
                    {
                        result.ComponentStatus["Disk"] = HealthStatus.Healthy;
                    }
                }
            }
            catch (Exception ex)
            {
                result.ComponentStatus["Disk"] = HealthStatus.Critical;
                result.Messages.Add($"Failed to check disk space: {ex.Message}");
            }
        }

        private async Task CheckConfigurationAsync(HealthCheckResult result, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            try
            {
                // Check configuration validity
                if (_config.MaxConcurrentFlows <= 0)
                {
                    result.ComponentStatus["Configuration"] = HealthStatus.Critical;
                    result.Messages.Add("Invalid MaxConcurrentFlows configuration");
                    return;
                }

                if (_config.DefaultTimeoutSeconds <= 0)
                {
                    result.ComponentStatus["Configuration"] = HealthStatus.Warning;
                    result.Messages.Add("Invalid DefaultTimeoutSeconds configuration");
                    return;
                }

                result.ComponentStatus["Configuration"] = HealthStatus.Healthy;
                result.Metrics["Max_Concurrent_Flows"] = _config.MaxConcurrentFlows.ToString();
                result.Metrics["Default_Timeout_Sec"] = _config.DefaultTimeoutSeconds.ToString();
            }
            catch (Exception ex)
            {
                result.ComponentStatus["Configuration"] = HealthStatus.Critical;
                result.Messages.Add($"Failed to validate configuration: {ex.Message}");
            }
        }

        private async Task CheckDirectoriesAsync(HealthCheckResult result, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            try
            {
                var directories = new[]
                {
                    ("Logs", _config.LogDirectory)
                };

                int healthyDirs = 0;
                int totalDirs = 0;

                foreach (var (name, path) in directories)
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    totalDirs++;

                    try
                    {
                        if (Directory.Exists(path))
                        {
                            healthyDirs++;
                        }
                        else
                        {
                            result.Messages.Add($"{name} directory does not exist: {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Messages.Add($"Cannot access {name} directory: {ex.Message}");
                    }
                }

                if (healthyDirs == totalDirs && totalDirs > 0)
                {
                    result.ComponentStatus["Directories"] = HealthStatus.Healthy;
                }
                else if (healthyDirs > 0)
                {
                    result.ComponentStatus["Directories"] = HealthStatus.Warning;
                }
                else
                {
                    result.ComponentStatus["Directories"] = HealthStatus.Critical;
                }

                result.Metrics["Accessible_Directories"] = $"{healthyDirs}/{totalDirs}";
            }
            catch (Exception ex)
            {
                result.ComponentStatus["Directories"] = HealthStatus.Critical;
                result.Messages.Add($"Failed to check directories: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Represents the result of a health check operation.
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Timestamp when the health check was performed.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Overall health status.
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Duration of the health check.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Status of individual components.
        /// </summary>
        public Dictionary<string, HealthStatus> ComponentStatus { get; set; } = new();

        /// <summary>
        /// Metrics collected during health check.
        /// </summary>
        public Dictionary<string, string> Metrics { get; set; } = new();

        /// <summary>
        /// Health check messages (warnings, errors).
        /// </summary>
        public List<string> Messages { get; set; } = new();

        /// <summary>
        /// Gets a human-readable summary of the health check.
        /// </summary>
        public string GetSummary()
        {
            var status = Status switch
            {
                HealthStatus.Healthy => "✓ Healthy",
                HealthStatus.Warning => "⚠ Warning",
                HealthStatus.Critical => "✗ Critical",
                _ => "? Unknown"
            };

            return $"{status} ({Duration.TotalMilliseconds:F0}ms) - {ComponentStatus.Count} components checked";
        }

        /// <summary>
        /// Gets a detailed report of the health check.
        /// </summary>
        public string GetDetailedReport()
        {
            var report = $"Health Check Report\n";
            report += $"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n";
            report += $"Status: {Status}\n";
            report += $"Duration: {Duration.TotalMilliseconds:F0}ms\n\n";

            if (ComponentStatus.Any())
            {
                report += "Component Status:\n";
                foreach (var (component, status) in ComponentStatus)
                {
                    var icon = status switch
                    {
                        HealthStatus.Healthy => "✓",
                        HealthStatus.Warning => "⚠",
                        HealthStatus.Critical => "✗",
                        _ => "?"
                    };
                    report += $"  {icon} {component}: {status}\n";
                }
                report += "\n";
            }

            if (Metrics.Any())
            {
                report += "Metrics:\n";
                foreach (var (key, value) in Metrics)
                {
                    report += $"  {key}: {value}\n";
                }
                report += "\n";
            }

            if (Messages.Any())
            {
                report += "Messages:\n";
                foreach (var message in Messages)
                {
                    report += $"  - {message}\n";
                }
            }

            return report;
        }
    }
}
