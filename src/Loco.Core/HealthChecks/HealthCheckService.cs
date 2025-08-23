using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.HealthChecks
{
    public interface IHealthCheckService
    {
        Task<HealthReport> CheckHealthAsync(HealthCheckContext context = null, CancellationToken cancellationToken = default);
        Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool> predicate, CancellationToken cancellationToken = default);
        void RegisterHealthCheck(string name, IHealthCheck healthCheck, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null);
        void RegisterHealthCheck(string name, Func<CancellationToken, Task<HealthCheckResult>> check, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null);
    }

    public class HealthCheckService : IHealthCheckService
    {
        private readonly ILogger<HealthCheckService> _logger;
        private readonly HealthCheckServiceOptions _options;
        private readonly Dictionary<string, HealthCheckRegistration> _healthChecks;
        private readonly object _lock = new();

        public HealthCheckService(ILogger<HealthCheckService> logger, IOptions<HealthCheckServiceOptions> options)
        {
            _logger = logger;
            _options = options?.Value ?? new HealthCheckServiceOptions();
            _healthChecks = new Dictionary<string, HealthCheckRegistration>();
            
            RegisterDefaultHealthChecks();
        }

        public void RegisterHealthCheck(string name, IHealthCheck healthCheck, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null)
        {
            lock (_lock)
            {
                _healthChecks[name] = new HealthCheckRegistration(
                    name,
                    healthCheck,
                    failureStatus,
                    tags,
                    timeout ?? _options.DefaultTimeout);
            }
            
            _logger.LogInformation("Registered health check: {Name}", name);
        }

        public void RegisterHealthCheck(string name, Func<CancellationToken, Task<HealthCheckResult>> check, HealthStatus? failureStatus = null, IEnumerable<string> tags = null, TimeSpan? timeout = null)
        {
            var healthCheck = new DelegateHealthCheck(check);
            RegisterHealthCheck(name, healthCheck, failureStatus, tags, timeout);
        }

        public async Task<HealthReport> CheckHealthAsync(HealthCheckContext context = null, CancellationToken cancellationToken = default)
        {
            return await CheckHealthAsync(registration => true, cancellationToken);
        }

        public async Task<HealthReport> CheckHealthAsync(Func<HealthCheckRegistration, bool> predicate, CancellationToken cancellationToken = default)
        {
            var registrations = GetHealthChecks(predicate);
            var results = new Dictionary<string, HealthReportEntry>();
            var totalDuration = Stopwatch.StartNew();

            var tasks = registrations.Select(async registration =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(registration.Timeout);
                    
                    var context = new HealthCheckContext
                    {
                        Registration = registration
                    };
                    
                    var result = await registration.HealthCheck.CheckHealthAsync(context, cts.Token);
                    stopwatch.Stop();
                    
                    return (registration.Name, new HealthReportEntry(
                        result.Status,
                        result.Description,
                        stopwatch.Elapsed,
                        result.Exception,
                        result.Data,
                        registration.Tags));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    _logger.LogWarning("Health check {Name} timed out after {Timeout}", registration.Name, registration.Timeout);
                    
                    return (registration.Name, new HealthReportEntry(
                        registration.FailureStatus,
                        $"Health check timed out after {registration.Timeout}",
                        stopwatch.Elapsed,
                        new TimeoutException($"Health check timed out after {registration.Timeout}"),
                        null,
                        registration.Tags));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Health check {Name} failed", registration.Name);
                    
                    return (registration.Name, new HealthReportEntry(
                        registration.FailureStatus,
                        ex.Message,
                        stopwatch.Elapsed,
                        ex,
                        null,
                        registration.Tags));
                }
            });

            var checkResults = await Task.WhenAll(tasks);
            
            foreach (var (name, entry) in checkResults)
            {
                results[name] = entry;
            }

            totalDuration.Stop();

            var status = CalculateAggregateStatus(results.Values);

            return new HealthReport(results, status, totalDuration.Elapsed);
        }

        private List<HealthCheckRegistration> GetHealthChecks(Func<HealthCheckRegistration, bool> predicate)
        {
            lock (_lock)
            {
                return _healthChecks.Values.Where(predicate).ToList();
            }
        }

        private HealthStatus CalculateAggregateStatus(IEnumerable<HealthReportEntry> entries)
        {
            var statuses = entries.Select(e => e.Status).ToList();
            
            if (!statuses.Any())
                return HealthStatus.Healthy;
            
            if (statuses.Any(s => s == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;
            
            if (statuses.Any(s => s == HealthStatus.Degraded))
                return HealthStatus.Degraded;
            
            return HealthStatus.Healthy;
        }

        private void RegisterDefaultHealthChecks()
        {
            // System health checks
            RegisterHealthCheck("system:memory", new MemoryHealthCheck(_options.MemoryThreshold), tags: new[] { "system" });
            RegisterHealthCheck("system:cpu", new CpuHealthCheck(_options.CpuThreshold), tags: new[] { "system" });
            RegisterHealthCheck("system:disk", new DiskSpaceHealthCheck(_options.DiskSpaceThreshold), tags: new[] { "system" });
            
            // Application health checks
            RegisterHealthCheck("app:ready", new ReadinessHealthCheck(), tags: new[] { "readiness" });
            RegisterHealthCheck("app:live", new LivenessHealthCheck(), tags: new[] { "liveness" });
        }

        private class DelegateHealthCheck : IHealthCheck
        {
            private readonly Func<CancellationToken, Task<HealthCheckResult>> _check;

            public DelegateHealthCheck(Func<CancellationToken, Task<HealthCheckResult>> check)
            {
                _check = check;
            }

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return _check(cancellationToken);
            }
        }
    }

    // Built-in Health Checks
    public class MemoryHealthCheck : IHealthCheck
    {
        private readonly long _threshold;

        public MemoryHealthCheck(long thresholdBytes = 500_000_000) // 500MB default
        {
            _threshold = thresholdBytes;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var allocated = GC.GetTotalMemory(false);
            var data = new Dictionary<string, object>
            {
                ["Allocated"] = allocated,
                ["AllocatedMB"] = allocated / (1024.0 * 1024.0),
                ["Gen0Collections"] = GC.CollectionCount(0),
                ["Gen1Collections"] = GC.CollectionCount(1),
                ["Gen2Collections"] = GC.CollectionCount(2)
            };

            if (allocated > _threshold)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage is above threshold ({allocated / (1024.0 * 1024.0):F2} MB > {_threshold / (1024.0 * 1024.0):F2} MB)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage is healthy ({allocated / (1024.0 * 1024.0):F2} MB)",
                data: data));
        }
    }

    public class CpuHealthCheck : IHealthCheck
    {
        private readonly double _threshold;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private TimeSpan _lastCpuTime = TimeSpan.Zero;

        public CpuHealthCheck(double thresholdPercentage = 80)
        {
            _threshold = thresholdPercentage;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var process = Process.GetCurrentProcess();
            var currentTime = DateTime.UtcNow;
            var currentCpuTime = process.TotalProcessorTime;
            
            if (_lastCheckTime == DateTime.MinValue)
            {
                _lastCheckTime = currentTime;
                _lastCpuTime = currentCpuTime;
                return Task.FromResult(HealthCheckResult.Healthy("CPU check initialized"));
            }

            var timeDiff = currentTime - _lastCheckTime;
            var cpuDiff = currentCpuTime - _lastCpuTime;
            
            var cpuUsage = cpuDiff.TotalMilliseconds / (timeDiff.TotalMilliseconds * Environment.ProcessorCount) * 100;
            
            _lastCheckTime = currentTime;
            _lastCpuTime = currentCpuTime;

            var data = new Dictionary<string, object>
            {
                ["CpuUsage"] = cpuUsage,
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["ProcessId"] = process.Id,
                ["ThreadCount"] = process.Threads.Count
            };

            if (cpuUsage > _threshold)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"CPU usage is above threshold ({cpuUsage:F2}% > {_threshold:F2}%)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"CPU usage is healthy ({cpuUsage:F2}%)",
                data: data));
        }
    }

    public class DiskSpaceHealthCheck : IHealthCheck
    {
        private readonly long _threshold;

        public DiskSpaceHealthCheck(long thresholdBytes = 1_073_741_824) // 1GB default
        {
            _threshold = thresholdBytes;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed);

            var data = new Dictionary<string, object>();
            var unhealthyDrives = new List<string>();

            foreach (var drive in drives)
            {
                var freeSpace = drive.AvailableFreeSpace;
                var totalSpace = drive.TotalSize;
                var usedPercentage = (1 - (double)freeSpace / totalSpace) * 100;

                data[$"Drive_{drive.Name}_FreeGB"] = freeSpace / (1024.0 * 1024.0 * 1024.0);
                data[$"Drive_{drive.Name}_TotalGB"] = totalSpace / (1024.0 * 1024.0 * 1024.0);
                data[$"Drive_{drive.Name}_UsedPercent"] = usedPercentage;

                if (freeSpace < _threshold)
                {
                    unhealthyDrives.Add($"{drive.Name} ({freeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB free)");
                }
            }

            if (unhealthyDrives.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space on drives: {string.Join(", ", unhealthyDrives)}",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Disk space is healthy",
                data: data));
        }
    }

    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;
        private readonly string _testQuery;

        public DatabaseHealthCheck(string connectionString, string testQuery = "SELECT 1")
        {
            _connectionString = connectionString;
            _testQuery = testQuery;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                
                using var command = connection.CreateCommand();
                command.CommandText = _testQuery;
                await command.ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy("Database connection successful");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connection failed", ex);
            }
        }
    }

    public class HttpHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly string _uri;
        private readonly int _expectedStatusCode;

        public HttpHealthCheck(HttpClient httpClient, string uri, int expectedStatusCode = 200)
        {
            _httpClient = httpClient;
            _uri = uri;
            _expectedStatusCode = expectedStatusCode;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(_uri, cancellationToken);
                
                var data = new Dictionary<string, object>
                {
                    ["StatusCode"] = (int)response.StatusCode,
                    ["ReasonPhrase"] = response.ReasonPhrase
                };

                if ((int)response.StatusCode == _expectedStatusCode)
                {
                    return HealthCheckResult.Healthy($"HTTP endpoint returned {response.StatusCode}", data);
                }

                return HealthCheckResult.Degraded(
                    $"HTTP endpoint returned {response.StatusCode}, expected {_expectedStatusCode}",
                    data: data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"HTTP endpoint check failed: {ex.Message}", ex);
            }
        }
    }

    public class RedisHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;

        public RedisHealthCheck(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_connectionString);
                var database = connection.GetDatabase();
                
                var pingResult = await database.PingAsync();
                
                var data = new Dictionary<string, object>
                {
                    ["ResponseTime"] = pingResult.TotalMilliseconds,
                    ["IsConnected"] = connection.IsConnected
                };

                if (connection.IsConnected)
                {
                    return HealthCheckResult.Healthy($"Redis is responsive ({pingResult.TotalMilliseconds:F2}ms)", data);
                }

                return HealthCheckResult.Degraded("Redis connection established but not fully connected", data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Redis connection failed: {ex.Message}", ex);
            }
        }
    }

    public class ReadinessHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // Check if application is ready to serve requests
            // This could include checking if all services are initialized, etc.
            return Task.FromResult(HealthCheckResult.Healthy("Application is ready"));
        }
    }

    public class LivenessHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // Simple liveness check - if we can execute this, the app is alive
            return Task.FromResult(HealthCheckResult.Healthy("Application is alive"));
        }
    }

    // Configuration
    public class HealthCheckServiceOptions
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public long MemoryThreshold { get; set; } = 500_000_000; // 500MB
        public double CpuThreshold { get; set; } = 80; // 80%
        public long DiskSpaceThreshold { get; set; } = 1_073_741_824; // 1GB
    }

    // Custom Registration class
    public class HealthCheckRegistration
    {
        public string Name { get; }
        public IHealthCheck HealthCheck { get; }
        public HealthStatus FailureStatus { get; }
        public IEnumerable<string> Tags { get; }
        public TimeSpan Timeout { get; }

        public HealthCheckRegistration(
            string name,
            IHealthCheck healthCheck,
            HealthStatus? failureStatus,
            IEnumerable<string> tags,
            TimeSpan timeout)
        {
            Name = name;
            HealthCheck = healthCheck;
            FailureStatus = failureStatus ?? HealthStatus.Unhealthy;
            Tags = tags ?? Enumerable.Empty<string>();
            Timeout = timeout;
        }
    }
}