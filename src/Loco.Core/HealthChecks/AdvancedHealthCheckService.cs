using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Loco.Core.HealthChecks
{
    public interface IAdvancedHealthCheckService
    {
        Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default);
        Task<ComponentHealth> GetComponentHealthAsync(string componentName, CancellationToken cancellationToken = default);
        Task<SystemMetrics> GetSystemMetricsAsync();
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
        Task RegisterHealthCheckAsync(string name, IHealthCheckProvider provider);
        Task<HealthTrend> GetHealthTrendAsync(TimeSpan period);
        void StartContinuousMonitoring();
        void StopContinuousMonitoring();
        event EventHandler<HealthStatusChangedEventArgs> HealthStatusChanged;
    }

    public class AdvancedHealthCheckService : IAdvancedHealthCheckService, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AdvancedHealthCheckService> _logger;
        private readonly HealthCheckConfiguration _configuration;
        private readonly ConcurrentDictionary<string, IHealthCheckProvider> _healthCheckProviders;
        private readonly ConcurrentDictionary<string, HealthCheckResult> _lastResults;
        private readonly ConcurrentDictionary<string, List<HealthCheckHistory>> _healthHistory;
        private readonly SemaphoreSlim _checkSemaphore;
        private Timer _continuousMonitoringTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;

        public event EventHandler<HealthStatusChangedEventArgs> HealthStatusChanged;

        public AdvancedHealthCheckService(
            IServiceProvider serviceProvider,
            IOptions<HealthCheckConfiguration> configuration,
            ILogger<AdvancedHealthCheckService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration.Value;
            _logger = logger;
            _healthCheckProviders = new ConcurrentDictionary<string, IHealthCheckProvider>();
            _lastResults = new ConcurrentDictionary<string, HealthCheckResult>();
            _healthHistory = new ConcurrentDictionary<string, List<HealthCheckHistory>>();
            _checkSemaphore = new SemaphoreSlim(_configuration.MaxConcurrentChecks);

            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

            RegisterDefaultHealthChecks();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RegisterBuiltInHealthChecksAsync();
            StartContinuousMonitoring();
            _logger.LogInformation("Advanced health check service started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            StopContinuousMonitoring();
            _logger.LogInformation("Advanced health check service stopped");
            await Task.CompletedTask;
        }

        public async Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var healthReport = new HealthReport
            {
                Id = Guid.NewGuid(),
                Timestamp = startTime,
                MachineName = Environment.MachineName,
                ApplicationVersion = GetApplicationVersion(),
                Components = new Dictionary<string, ComponentHealth>()
            };

            var tasks = _healthCheckProviders.Select(async kvp =>
            {
                try
                {
                    await _checkSemaphore.WaitAsync(cancellationToken);
                    var componentHealth = await GetComponentHealthAsync(kvp.Key, cancellationToken);
                    healthReport.Components[kvp.Key] = componentHealth;
                }
                finally
                {
                    _checkSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Calculate overall status
            healthReport.OverallStatus = CalculateOverallStatus(healthReport.Components.Values);
            healthReport.Duration = DateTime.UtcNow - startTime;

            // Add system metrics
            healthReport.SystemMetrics = await GetSystemMetricsAsync();

            // Store in history
            await StoreHealthReportAsync(healthReport);

            _logger.LogInformation("Health report generated: {Status} in {Duration}ms", 
                healthReport.OverallStatus, healthReport.Duration.TotalMilliseconds);

            return healthReport;
        }

        public async Task<ComponentHealth> GetComponentHealthAsync(string componentName, CancellationToken cancellationToken = default)
        {
            if (!_healthCheckProviders.TryGetValue(componentName, out var provider))
            {
                return new ComponentHealth
                {
                    Name = componentName,
                    Status = HealthStatus.Unknown,
                    Message = "Health check provider not found",
                    Timestamp = DateTime.UtcNow
                };
            }

            var stopwatch = Stopwatch.StartNew();
            ComponentHealth componentHealth;

            try
            {
                var timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout);

                var result = await provider.CheckHealthAsync(timeoutCts.Token);
                
                componentHealth = new ComponentHealth
                {
                    Name = componentName,
                    Status = result.Status,
                    Message = result.Description,
                    Data = result.Data,
                    Timestamp = DateTime.UtcNow,
                    ResponseTime = stopwatch.Elapsed,
                    Tags = result.Tags?.ToList() ?? new List<string>()
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                componentHealth = new ComponentHealth
                {
                    Name = componentName,
                    Status = HealthStatus.Unhealthy,
                    Message = "Health check was cancelled",
                    Timestamp = DateTime.UtcNow,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (OperationCanceledException)
            {
                componentHealth = new ComponentHealth
                {
                    Name = componentName,
                    Status = HealthStatus.Unhealthy,
                    Message = $"Health check timed out after {_configuration.TimeoutSeconds} seconds",
                    Timestamp = DateTime.UtcNow,
                    ResponseTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                componentHealth = new ComponentHealth
                {
                    Name = componentName,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow,
                    ResponseTime = stopwatch.Elapsed
                };

                _logger.LogError(ex, "Health check failed for component {ComponentName}", componentName);
            }

            // Update last result and check for status changes
            var previousResult = _lastResults.GetValueOrDefault(componentName);
            _lastResults[componentName] = new HealthCheckResult(componentHealth.Status, componentHealth.Message);

            if (previousResult != null && previousResult.Status != componentHealth.Status)
            {
                OnHealthStatusChanged(new HealthStatusChangedEventArgs
                {
                    ComponentName = componentName,
                    PreviousStatus = previousResult.Status,
                    CurrentStatus = componentHealth.Status,
                    Timestamp = componentHealth.Timestamp
                });
            }

            // Store in history
            StoreComponentHealthHistory(componentName, componentHealth);

            return componentHealth;
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            var process = Process.GetCurrentProcess();
            
            return new SystemMetrics
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = await GetCpuUsageAsync(),
                MemoryUsageMB = process.WorkingSet64 / 1024 / 1024,
                AvailableMemoryMB = GetAvailableMemoryMB(),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                GcCollections = new Dictionary<int, long>
                {
                    [0] = GC.CollectionCount(0),
                    [1] = GC.CollectionCount(1),
                    [2] = GC.CollectionCount(2)
                },
                TotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
                DiskUsage = await GetDiskUsageAsync(),
                NetworkConnections = GetNetworkConnectionCount(),
                Uptime = DateTime.UtcNow - process.StartTime
            };
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            var report = await GetHealthReportAsync(cancellationToken);
            return report.OverallStatus == HealthStatus.Healthy;
        }

        public async Task RegisterHealthCheckAsync(string name, IHealthCheckProvider provider)
        {
            _healthCheckProviders[name] = provider;
            _logger.LogInformation("Registered health check provider: {Name}", name);
            await Task.CompletedTask;
        }

        public async Task<HealthTrend> GetHealthTrendAsync(TimeSpan period)
        {
            var cutoffTime = DateTime.UtcNow - period;
            var trend = new HealthTrend
            {
                Period = period,
                StartTime = cutoffTime,
                EndTime = DateTime.UtcNow,
                ComponentTrends = new Dictionary<string, ComponentTrend>()
            };

            foreach (var kvp in _healthHistory)
            {
                var componentName = kvp.Key;
                var history = kvp.Value.Where(h => h.Timestamp >= cutoffTime).ToList();

                if (history.Any())
                {
                    var componentTrend = new ComponentTrend
                    {
                        ComponentName = componentName,
                        TotalChecks = history.Count,
                        HealthyChecks = history.Count(h => h.Status == HealthStatus.Healthy),
                        DegradedChecks = history.Count(h => h.Status == HealthStatus.Degraded),
                        UnhealthyChecks = history.Count(h => h.Status == HealthStatus.Unhealthy),
                        AverageResponseTimeMs = history.Average(h => h.ResponseTime.TotalMilliseconds),
                        UpTimePercentage = (double)history.Count(h => h.Status != HealthStatus.Unhealthy) / history.Count * 100
                    };

                    trend.ComponentTrends[componentName] = componentTrend;
                }
            }

            return await Task.FromResult(trend);
        }

        public void StartContinuousMonitoring()
        {
            if (_continuousMonitoringTimer != null)
                return;

            _continuousMonitoringTimer = new Timer(async _ =>
            {
                try
                {
                    await GetHealthReportAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during continuous health monitoring");
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_configuration.MonitoringIntervalSeconds));

            _logger.LogInformation("Continuous health monitoring started with interval {Interval}s", 
                _configuration.MonitoringIntervalSeconds);
        }

        public void StopContinuousMonitoring()
        {
            _continuousMonitoringTimer?.Dispose();
            _continuousMonitoringTimer = null;
            _logger.LogInformation("Continuous health monitoring stopped");
        }

        private void RegisterDefaultHealthChecks()
        {
            // Register built-in health checks
            _healthCheckProviders["System"] = new SystemHealthCheckProvider();
            _healthCheckProviders["Database"] = new DatabaseHealthCheckProvider(_serviceProvider);
            _healthCheckProviders["Redis"] = new RedisHealthCheckProvider(_serviceProvider);
            _healthCheckProviders["FileSystem"] = new FileSystemHealthCheckProvider(_configuration);
            _healthCheckProviders["Network"] = new NetworkHealthCheckProvider(_configuration);
            _healthCheckProviders["Memory"] = new MemoryHealthCheckProvider(_configuration);
            _healthCheckProviders["Disk"] = new DiskHealthCheckProvider(_configuration);
        }

        private async Task RegisterBuiltInHealthChecksAsync()
        {
            // Register additional health checks based on configuration
            if (_configuration.EnableExternalDependencyChecks)
            {
                foreach (var dependency in _configuration.ExternalDependencies)
                {
                    _healthCheckProviders[dependency.Name] = new HttpHealthCheckProvider(dependency);
                }
            }

            await Task.CompletedTask;
        }

        private HealthStatus CalculateOverallStatus(IEnumerable<ComponentHealth> components)
        {
            if (!components.Any())
                return HealthStatus.Unknown;

            if (components.Any(c => c.Status == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;

            if (components.Any(c => c.Status == HealthStatus.Degraded))
                return HealthStatus.Degraded;

            if (components.All(c => c.Status == HealthStatus.Healthy))
                return HealthStatus.Healthy;

            return HealthStatus.Unknown;
        }

        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                // First call returns 0, need to wait and call again
                _cpuCounter.NextValue();
                await Task.Delay(100);
                return _cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private long GetAvailableMemoryMB()
        {
            try
            {
                return (long)_memoryCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private async Task<Dictionary<string, DiskInfo>> GetDiskUsageAsync()
        {
            var diskInfo = new Dictionary<string, DiskInfo>();

            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives.Where(d => d.IsReady))
                {
                    diskInfo[drive.Name] = new DiskInfo
                    {
                        TotalSpaceGB = drive.TotalSize / 1024 / 1024 / 1024,
                        FreeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024,
                        UsagePercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving disk usage information");
            }

            return await Task.FromResult(diskInfo);
        }

        private int GetNetworkConnectionCount()
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                return properties.GetActiveTcpConnections().Length;
            }
            catch
            {
                return 0;
            }
        }

        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                return assembly?.GetName().Version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private async Task StoreHealthReportAsync(HealthReport report)
        {
            // Store health report for trending and analysis
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<HealthCheckDbContext>();
                
                if (dbContext != null)
                {
                    var entity = new HealthReportEntity
                    {
                        Id = report.Id,
                        Timestamp = report.Timestamp,
                        OverallStatus = report.OverallStatus,
                        MachineName = report.MachineName,
                        ApplicationVersion = report.ApplicationVersion,
                        Duration = report.Duration,
                        Data = System.Text.Json.JsonSerializer.Serialize(report)
                    };

                    dbContext.HealthReports.Add(entity);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store health report");
            }
        }

        private void StoreComponentHealthHistory(string componentName, ComponentHealth health)
        {
            var history = _healthHistory.GetOrAdd(componentName, _ => new List<HealthCheckHistory>());
            
            lock (history)
            {
                history.Add(new HealthCheckHistory
                {
                    Timestamp = health.Timestamp,
                    Status = health.Status,
                    ResponseTime = health.ResponseTime,
                    Message = health.Message
                });

                // Keep only recent history to prevent memory issues
                var cutoffTime = DateTime.UtcNow.AddHours(-_configuration.HistoryRetentionHours);
                history.RemoveAll(h => h.Timestamp < cutoffTime);
            }
        }

        private void OnHealthStatusChanged(HealthStatusChangedEventArgs args)
        {
            HealthStatusChanged?.Invoke(this, args);
            
            _logger.LogWarning("Health status changed for {ComponentName}: {PreviousStatus} -> {CurrentStatus}",
                args.ComponentName, args.PreviousStatus, args.CurrentStatus);
        }
    }

    // Health check providers
    public interface IHealthCheckProvider
    {
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    }

    public class SystemHealthCheckProvider : IHealthCheckProvider
    {
        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var process = Process.GetCurrentProcess();
            var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;
            
            var data = new Dictionary<string, object>
            {
                ["MemoryUsageMB"] = memoryUsageMB,
                ["ThreadCount"] = process.Threads.Count,
                ["HandleCount"] = process.HandleCount,
                ["Uptime"] = DateTime.UtcNow - process.StartTime
            };

            var status = memoryUsageMB > 1000 ? HealthStatus.Degraded : HealthStatus.Healthy;
            var message = status == HealthStatus.Healthy ? "System is healthy" : "High memory usage detected";

            return await Task.FromResult(new HealthCheckResult(status, message, null, data));
        }
    }

    public class DatabaseHealthCheckProvider : IHealthCheckProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseHealthCheckProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<DbContext>();
                
                if (dbContext == null)
                {
                    return new HealthCheckResult(HealthStatus.Unhealthy, "Database context not available");
                }

                var stopwatch = Stopwatch.StartNew();
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds,
                    ["DatabaseProvider"] = dbContext.Database.ProviderName
                };

                var status = stopwatch.ElapsedMilliseconds > 1000 ? HealthStatus.Degraded : HealthStatus.Healthy;
                var message = status == HealthStatus.Healthy ? "Database is responding" : "Database response is slow";

                return new HealthCheckResult(status, message, null, data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"Database check failed: {ex.Message}", ex);
            }
        }
    }

    public class RedisHealthCheckProvider : IHealthCheckProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public RedisHealthCheckProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();
                
                if (redis == null)
                {
                    return new HealthCheckResult(HealthStatus.Degraded, "Redis not configured");
                }

                var database = redis.GetDatabase();
                var stopwatch = Stopwatch.StartNew();
                await database.PingAsync();
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds,
                    ["IsConnected"] = redis.IsConnected
                };

                var status = redis.IsConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                var message = status == HealthStatus.Healthy ? "Redis is responding" : "Redis connection failed";

                return new HealthCheckResult(status, message, null, data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"Redis check failed: {ex.Message}", ex);
            }
        }
    }

    public class FileSystemHealthCheckProvider : IHealthCheckProvider
    {
        private readonly HealthCheckConfiguration _configuration;

        public FileSystemHealthCheckProvider(HealthCheckConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var testFile = Path.Combine(Path.GetTempPath(), $"healthcheck_{Guid.NewGuid()}.tmp");
                
                var stopwatch = Stopwatch.StartNew();
                await File.WriteAllTextAsync(testFile, "health check test", cancellationToken);
                var content = await File.ReadAllTextAsync(testFile, cancellationToken);
                File.Delete(testFile);
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["WriteReadTimeMs"] = stopwatch.ElapsedMilliseconds,
                    ["TempPath"] = Path.GetTempPath()
                };

                var status = content == "health check test" ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                var message = status == HealthStatus.Healthy ? "File system is accessible" : "File system access failed";

                return new HealthCheckResult(status, message, null, data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"File system check failed: {ex.Message}", ex);
            }
        }
    }

    public class NetworkHealthCheckProvider : IHealthCheckProvider
    {
        private readonly HealthCheckConfiguration _configuration;

        public NetworkHealthCheckProvider(HealthCheckConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 5000);

                var data = new Dictionary<string, object>
                {
                    ["PingStatus"] = reply.Status.ToString(),
                    ["RoundTripTimeMs"] = reply.RoundtripTime
                };

                var status = reply.Status == IPStatus.Success ? HealthStatus.Healthy : HealthStatus.Degraded;
                var message = reply.Status == IPStatus.Success ? "Network connectivity is good" : $"Network issue: {reply.Status}";

                return new HealthCheckResult(status, message, null, data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"Network check failed: {ex.Message}", ex);
            }
        }
    }

    public class MemoryHealthCheckProvider : IHealthCheckProvider
    {
        private readonly HealthCheckConfiguration _configuration;

        public MemoryHealthCheckProvider(HealthCheckConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var totalMemory = GC.GetTotalMemory(false);
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;

            var data = new Dictionary<string, object>
            {
                ["TotalMemoryMB"] = totalMemory / 1024 / 1024,
                ["WorkingSetMB"] = workingSet / 1024 / 1024,
                ["GcCollections"] = new { Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2) }
            };

            var memoryUsageMB = workingSet / 1024 / 1024;
            HealthStatus status;
            string message;

            if (memoryUsageMB > _configuration.MemoryThresholdMB)
            {
                status = HealthStatus.Unhealthy;
                message = $"Memory usage is critical: {memoryUsageMB}MB";
            }
            else if (memoryUsageMB > _configuration.MemoryThresholdMB * 0.8)
            {
                status = HealthStatus.Degraded;
                message = $"Memory usage is high: {memoryUsageMB}MB";
            }
            else
            {
                status = HealthStatus.Healthy;
                message = $"Memory usage is normal: {memoryUsageMB}MB";
            }

            return await Task.FromResult(new HealthCheckResult(status, message, null, data));
        }
    }

    public class DiskHealthCheckProvider : IHealthCheckProvider
    {
        private readonly HealthCheckConfiguration _configuration;

        public DiskHealthCheckProvider(HealthCheckConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                var diskInfo = new Dictionary<string, object>();
                var worstStatus = HealthStatus.Healthy;

                foreach (var drive in drives)
                {
                    var usagePercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
                    
                    diskInfo[drive.Name] = new
                    {
                        TotalGB = drive.TotalSize / 1024 / 1024 / 1024,
                        FreeGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024,
                        UsagePercent = Math.Round(usagePercent, 2)
                    };

                    if (usagePercent > 95)
                        worstStatus = HealthStatus.Unhealthy;
                    else if (usagePercent > 85 && worstStatus == HealthStatus.Healthy)
                        worstStatus = HealthStatus.Degraded;
                }

                var message = worstStatus switch
                {
                    HealthStatus.Unhealthy => "Disk space is critically low",
                    HealthStatus.Degraded => "Disk space is running low",
                    _ => "Disk space is adequate"
                };

                return new HealthCheckResult(worstStatus, message, null, diskInfo);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, $"Disk check failed: {ex.Message}", ex);
            }
        }
    }

    public class HttpHealthCheckProvider : IHealthCheckProvider
    {
        private readonly ExternalDependency _dependency;
        private static readonly HttpClient _httpClient = new HttpClient();

        public HttpHealthCheckProvider(ExternalDependency dependency)
        {
            _dependency = dependency;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(_dependency.Url, cancellationToken);
                stopwatch.Stop();

                var data = new Dictionary<string, object>
                {
                    ["Url"] = _dependency.Url,
                    ["StatusCode"] = (int)response.StatusCode,
                    ["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds
                };

                var status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                var message = response.IsSuccessStatusCode ? 
                    $"External dependency {_dependency.Name} is responding" : 
                    $"External dependency {_dependency.Name} returned {response.StatusCode}";

                return new HealthCheckResult(status, message, null, data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, 
                    $"External dependency {_dependency.Name} check failed: {ex.Message}", ex);
            }
        }
    }

    // Models and configuration
    public class HealthReport
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public HealthStatus OverallStatus { get; set; }
        public string MachineName { get; set; }
        public string ApplicationVersion { get; set; }
        public TimeSpan Duration { get; set; }
        public Dictionary<string, ComponentHealth> Components { get; set; }
        public SystemMetrics SystemMetrics { get; set; }
    }

    public class ComponentHealth
    {
        public string Name { get; set; }
        public HealthStatus Status { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public IReadOnlyDictionary<string, object> Data { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class SystemMetrics
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public long AvailableMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public Dictionary<int, long> GcCollections { get; set; }
        public long TotalMemoryMB { get; set; }
        public Dictionary<string, DiskInfo> DiskUsage { get; set; }
        public int NetworkConnections { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    public class DiskInfo
    {
        public long TotalSpaceGB { get; set; }
        public long FreeSpaceGB { get; set; }
        public double UsagePercent { get; set; }
    }

    public class HealthTrend
    {
        public TimeSpan Period { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, ComponentTrend> ComponentTrends { get; set; }
    }

    public class ComponentTrend
    {
        public string ComponentName { get; set; }
        public int TotalChecks { get; set; }
        public int HealthyChecks { get; set; }
        public int DegradedChecks { get; set; }
        public int UnhealthyChecks { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double UpTimePercentage { get; set; }
    }

    public class HealthCheckHistory
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus Status { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string Message { get; set; }
    }

    public class HealthStatusChangedEventArgs : EventArgs
    {
        public string ComponentName { get; set; }
        public HealthStatus PreviousStatus { get; set; }
        public HealthStatus CurrentStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class HealthCheckConfiguration
    {
        public int TimeoutSeconds { get; set; } = 30;
        public int MonitoringIntervalSeconds { get; set; } = 60;
        public int MaxConcurrentChecks { get; set; } = 10;
        public int HistoryRetentionHours { get; set; } = 24;
        public long MemoryThresholdMB { get; set; } = 1024;
        public bool EnableExternalDependencyChecks { get; set; } = true;
        public List<ExternalDependency> ExternalDependencies { get; set; } = new();
    }

    public class ExternalDependency
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
    }

    // DbContext for storing health reports
    public class HealthCheckDbContext : DbContext
    {
        public HealthCheckDbContext(DbContextOptions<HealthCheckDbContext> options) : base(options) { }

        public DbSet<HealthReportEntity> HealthReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HealthReportEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.OverallStatus);
                entity.Property(e => e.Data).HasColumnType("nvarchar(max)");
            });
        }
    }

    public class HealthReportEntity
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public HealthStatus OverallStatus { get; set; }
        public string MachineName { get; set; }
        public string ApplicationVersion { get; set; }
        public TimeSpan Duration { get; set; }
        public string Data { get; set; }
    }
}