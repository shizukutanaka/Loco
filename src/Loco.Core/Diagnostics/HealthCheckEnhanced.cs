using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Diagnostics
{
    /// <summary>
    /// Enhanced health check system with detailed diagnostics
    /// </summary>
    public class HealthCheckEnhanced
    {
        private readonly ILogger? _logger;

        public HealthCheckEnhanced(ILogger? logger = null)
        {
            _logger = logger;
        }

        public async Task<HealthCheckReport> PerformHealthCheckAsync()
        {
            var report = new HealthCheckReport
            {
                Timestamp = DateTime.UtcNow,
                Checks = new List<HealthCheckResult>()
            };

            // Run all health checks
            report.Checks.Add(CheckMemory());
            report.Checks.Add(CheckDiskSpace());
            report.Checks.Add(CheckConfiguration());
            report.Checks.Add(CheckDirectories());
            report.Checks.Add(await CheckSystemResourcesAsync());

            // Determine overall status
            if (report.Checks.Any(c => c.Status == HealthStatus.Critical))
            {
                report.OverallStatus = HealthStatus.Critical;
            }
            else if (report.Checks.Any(c => c.Status == HealthStatus.Warning))
            {
                report.OverallStatus = HealthStatus.Warning;
            }
            else
            {
                report.OverallStatus = HealthStatus.Healthy;
            }

            return report;
        }

        private HealthCheckResult CheckMemory()
        {
            var result = new HealthCheckResult { Name = "Memory" };

            try
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
                var gcMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                result.Details["ProcessMemory"] = $"{memoryMB:F1} MB";
                result.Details["GCMemory"] = $"{gcMemoryMB:F1} MB";
                result.Details["Gen0Collections"] = GC.CollectionCount(0).ToString();
                result.Details["Gen1Collections"] = GC.CollectionCount(1).ToString();
                result.Details["Gen2Collections"] = GC.CollectionCount(2).ToString();

                if (memoryMB > 1024)
                {
                    result.Status = HealthStatus.Critical;
                    result.Message = $"Memory usage very high ({memoryMB:F1} MB)";
                    result.Recommendations.Add("Reduce MaxConcurrentFlows in configuration");
                    result.Recommendations.Add("Enable memory optimization");
                    result.Recommendations.Add("Restart the application to free memory");
                }
                else if (memoryMB > 512)
                {
                    result.Status = HealthStatus.Warning;
                    result.Message = $"Memory usage elevated ({memoryMB:F1} MB)";
                    result.Recommendations.Add("Monitor memory usage closely");
                    result.Recommendations.Add("Consider reducing concurrent operations");
                }
                else
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = $"Memory usage normal ({memoryMB:F1} MB)";
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Warning;
                result.Message = $"Could not check memory: {ex.Message}";
            }

            return result;
        }

        private HealthCheckResult CheckDiskSpace()
        {
            var result = new HealthCheckResult { Name = "Disk Space" };

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
                var freeGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                var totalGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                var usedPercent = ((totalGB - freeGB) / totalGB) * 100;

                result.Details["Drive"] = drive.Name;
                result.Details["FreeSpace"] = $"{freeGB:F1} GB";
                result.Details["TotalSpace"] = $"{totalGB:F1} GB";
                result.Details["UsedPercent"] = $"{usedPercent:F1}%";

                if (freeGB < 1)
                {
                    result.Status = HealthStatus.Critical;
                    result.Message = $"Disk space critically low ({freeGB:F1} GB free)";
                    result.Recommendations.Add("Free up disk space immediately");
                    result.Recommendations.Add("Delete old log files: Loco.Cli.exe logs clear --confirm");
                    result.Recommendations.Add("Move data to another drive");
                }
                else if (freeGB < 5)
                {
                    result.Status = HealthStatus.Warning;
                    result.Message = $"Disk space low ({freeGB:F1} GB free)";
                    result.Recommendations.Add("Clean up temporary files");
                    result.Recommendations.Add("Archive or delete old logs");
                }
                else
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = $"Disk space sufficient ({freeGB:F1} GB free)";
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Warning;
                result.Message = $"Could not check disk space: {ex.Message}";
            }

            return result;
        }

        private HealthCheckResult CheckConfiguration()
        {
            var result = new HealthCheckResult { Name = "Configuration" };

            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Loco", "config", "loco.config.json"
                );

                if (File.Exists(configPath))
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = "Configuration file found";
                    result.Details["ConfigPath"] = configPath;
                    result.Details["LastModified"] = File.GetLastWriteTime(configPath).ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    result.Status = HealthStatus.Warning;
                    result.Message = "Configuration file not found, using defaults";
                    result.Details["ConfigPath"] = configPath;
                    result.Recommendations.Add("Run 'Loco.Cli.exe setup' to create configuration");
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Warning;
                result.Message = $"Could not check configuration: {ex.Message}";
            }

            return result;
        }

        private HealthCheckResult CheckDirectories()
        {
            var result = new HealthCheckResult { Name = "Directories" };

            try
            {
                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Loco"
                );

                var requiredDirs = new[] { "config", "logs", "cache", "workflows" };
                var missingDirs = new List<string>();
                var existingDirs = new List<string>();

                foreach (var dir in requiredDirs)
                {
                    var fullPath = Path.Combine(baseDir, dir);
                    if (Directory.Exists(fullPath))
                    {
                        existingDirs.Add(dir);
                    }
                    else
                    {
                        missingDirs.Add(dir);
                    }
                }

                result.Details["BaseDirectory"] = baseDir;
                result.Details["ExistingDirs"] = string.Join(", ", existingDirs);

                if (missingDirs.Any())
                {
                    result.Status = HealthStatus.Warning;
                    result.Message = $"Some directories missing: {string.Join(", ", missingDirs)}";
                    result.Details["MissingDirs"] = string.Join(", ", missingDirs);
                    result.Recommendations.Add("Run 'Loco.Cli.exe setup' to create missing directories");
                }
                else
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = "All required directories exist";
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Warning;
                result.Message = $"Could not check directories: {ex.Message}";
            }

            return result;
        }

        private async Task<HealthCheckResult> CheckSystemResourcesAsync()
        {
            var result = new HealthCheckResult { Name = "System Resources" };

            try
            {
                var process = Process.GetCurrentProcess();

                result.Details["ProcessorCount"] = Environment.ProcessorCount.ToString();
                result.Details["ThreadCount"] = process.Threads.Count.ToString();
                result.Details["HandleCount"] = process.HandleCount.ToString();
                result.Details["MachineName"] = Environment.MachineName;
                result.Details["OSVersion"] = Environment.OSVersion.ToString();
                result.Details["CLRVersion"] = Environment.Version.ToString();

                var threadCount = process.Threads.Count;
                if (threadCount > 1000)
                {
                    result.Status = HealthStatus.Critical;
                    result.Message = $"Thread count very high ({threadCount})";
                    result.Recommendations.Add("Check for thread leaks");
                    result.Recommendations.Add("Reduce concurrent operations");
                    result.Recommendations.Add("Restart the application");
                }
                else if (threadCount > 500)
                {
                    result.Status = HealthStatus.Warning;
                    result.Message = $"Thread count elevated ({threadCount})";
                    result.Recommendations.Add("Monitor thread usage");
                }
                else
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = $"System resources normal (threads: {threadCount})";
                }
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Warning;
                result.Message = $"Could not check system resources: {ex.Message}";
            }

            return result;
        }

        public class HealthCheckReport
        {
            public DateTime Timestamp { get; set; }
            public HealthStatus OverallStatus { get; set; }
            public List<HealthCheckResult> Checks { get; set; } = new();

            public string GetSummary()
            {
                var critical = Checks.Count(c => c.Status == HealthStatus.Critical);
                var warnings = Checks.Count(c => c.Status == HealthStatus.Warning);
                var healthy = Checks.Count(c => c.Status == HealthStatus.Healthy);

                return $"Health: {OverallStatus} - {healthy} healthy, {warnings} warnings, {critical} critical";
            }

            public List<string> GetAllRecommendations()
            {
                return Checks.SelectMany(c => c.Recommendations).Distinct().ToList();
            }
        }

        public class HealthCheckResult
        {
            public string Name { get; set; } = string.Empty;
            public HealthStatus Status { get; set; } = HealthStatus.Healthy;
            public string Message { get; set; } = string.Empty;
            public Dictionary<string, string> Details { get; set; } = new();
            public List<string> Recommendations { get; set; } = new();
        }
    }
}
