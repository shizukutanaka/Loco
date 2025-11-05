// John Carmack: "If you can't measure it, you can't improve it"
// Rob Pike: "Errors are values"

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple health check - Monitor system health, dependencies, performance
/// Fast checks, clear status, zero dependencies
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public class HealthCheckResult
{
    public string Name { get; set; } = "";
    public HealthStatus Status { get; set; }
    public string? Message { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public Exception? Exception { get; set; }
}

public interface IHealthCheck
{
    string Name { get; }
    Task<HealthCheckResult> CheckAsync();
}

/// <summary>
/// Simple health check service
/// </summary>
public class SimpleHealthCheckService
{
    private readonly List<IHealthCheck> _checks = new();
    private readonly ConcurrentDictionary<string, HealthCheckResult> _results = new();
    private readonly SimpleLogger _logger;
    private readonly TimeSpan _timeout;

    public SimpleHealthCheckService(TimeSpan? timeout = null, SimpleLogger? logger = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleHealthCheckService));
    }

    // Register health check
    public void AddCheck(IHealthCheck check)
    {
        _checks.Add(check);
        _logger.Info($"Health check registered: {check.Name}");
    }

    // Register simple health check
    public void AddCheck(string name, Func<Task<bool>> check)
    {
        _checks.Add(new SimpleHealthCheck(name, check));
    }

    // Register detailed health check
    public void AddCheck(string name, Func<Task<HealthCheckResult>> check)
    {
        _checks.Add(new DetailedHealthCheck(name, check));
    }

    // Run all health checks
    public async Task<HealthReport> CheckHealthAsync()
    {
        var tasks = _checks.Select(async check =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(_timeout);
                var task = check.CheckAsync();

                if (await Task.WhenAny(task, Task.Delay(_timeout, cts.Token)) == task)
                {
                    cts.Cancel();
                    var result = await task;
                    result.Duration = sw.Elapsed;
                    _results[check.Name] = result;
                    return result;
                }
                else
                {
                    return new HealthCheckResult
                    {
                        Name = check.Name,
                        Status = HealthStatus.Unhealthy,
                        Message = "Health check timed out",
                        Duration = sw.Elapsed
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Health check failed: {check.Name}", ex);
                return new HealthCheckResult
                {
                    Name = check.Name,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message,
                    Duration = sw.Elapsed,
                    Exception = ex
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        return new HealthReport(results.ToList());
    }

    // Get cached results
    public HealthReport GetCachedResults()
    {
        return new HealthReport(_results.Values.ToList());
    }

    // Simple health check implementation
    private class SimpleHealthCheck : IHealthCheck
    {
        private readonly Func<Task<bool>> _check;
        public string Name { get; }

        public SimpleHealthCheck(string name, Func<Task<bool>> check)
        {
            Name = name;
            _check = check;
        }

        public async Task<HealthCheckResult> CheckAsync()
        {
            var isHealthy = await _check();
            return new HealthCheckResult
            {
                Name = Name,
                Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy
            };
        }
    }

    // Detailed health check implementation
    private class DetailedHealthCheck : IHealthCheck
    {
        private readonly Func<Task<HealthCheckResult>> _check;
        public string Name { get; }

        public DetailedHealthCheck(string name, Func<Task<HealthCheckResult>> check)
        {
            Name = name;
            _check = check;
        }

        public Task<HealthCheckResult> CheckAsync() => _check();
    }
}

/// <summary>
/// Health report
/// </summary>
public class HealthReport
{
    public HealthStatus OverallStatus { get; }
    public List<HealthCheckResult> Results { get; }
    public DateTime Timestamp { get; }
    public TimeSpan TotalDuration { get; }

    public HealthReport(List<HealthCheckResult> results)
    {
        Results = results;
        Timestamp = DateTime.UtcNow;
        TotalDuration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds));

        // Calculate overall status
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
        {
            OverallStatus = HealthStatus.Unhealthy;
        }
        else if (results.Any(r => r.Status == HealthStatus.Degraded))
        {
            OverallStatus = HealthStatus.Degraded;
        }
        else
        {
            OverallStatus = HealthStatus.Healthy;
        }
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            status = OverallStatus.ToString(),
            timestamp = Timestamp,
            duration = TotalDuration.TotalMilliseconds,
            checks = Results.Select(r => new
            {
                name = r.Name,
                status = r.Status.ToString(),
                message = r.Message,
                duration = r.Duration.TotalMilliseconds,
                data = r.Data
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Health Report - {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Overall Status: {OverallStatus}");
        sb.AppendLine($"Total Duration: {TotalDuration.TotalMilliseconds:F2}ms");
        sb.AppendLine();

        foreach (var result in Results.OrderBy(r => r.Name))
        {
            var icon = result.Status switch
            {
                HealthStatus.Healthy => "✓",
                HealthStatus.Degraded => "⚠",
                HealthStatus.Unhealthy => "✗",
                _ => "?"
            };

            sb.AppendLine($"{icon} {result.Name}: {result.Status} ({result.Duration.TotalMilliseconds:F2}ms)");
            if (!string.IsNullOrEmpty(result.Message))
            {
                sb.AppendLine($"  {result.Message}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Common health checks
/// </summary>
public static class CommonHealthChecks
{
    // Memory health check
    public class MemoryHealthCheck : IHealthCheck
    {
        private readonly long _maxBytes;
        public string Name => "Memory";

        public MemoryHealthCheck(long maxBytesGB = 2)
        {
            _maxBytes = maxBytesGB * 1024 * 1024 * 1024;
        }

        public Task<HealthCheckResult> CheckAsync()
        {
            var used = GC.GetTotalMemory(false);
            var status = used > _maxBytes ? HealthStatus.Unhealthy :
                         used > _maxBytes * 0.8 ? HealthStatus.Degraded :
                         HealthStatus.Healthy;

            return Task.FromResult(new HealthCheckResult
            {
                Name = Name,
                Status = status,
                Message = $"Memory usage: {used / (1024 * 1024)}MB",
                Data = new Dictionary<string, object>
                {
                    ["usedBytes"] = used,
                    ["maxBytes"] = _maxBytes,
                    ["percentage"] = (used * 100.0) / _maxBytes
                }
            });
        }
    }

    // Disk space health check
    public class DiskSpaceHealthCheck : IHealthCheck
    {
        private readonly string _path;
        private readonly long _minFreeBytes;
        public string Name => "DiskSpace";

        public DiskSpaceHealthCheck(string path = "C:\\", long minFreeGB = 1)
        {
            _path = path;
            _minFreeBytes = minFreeGB * 1024 * 1024 * 1024;
        }

        public Task<HealthCheckResult> CheckAsync()
        {
            try
            {
                var drive = new DriveInfo(_path);
                var freeBytes = drive.AvailableFreeSpace;
                var status = freeBytes < _minFreeBytes ? HealthStatus.Unhealthy :
                            freeBytes < _minFreeBytes * 2 ? HealthStatus.Degraded :
                            HealthStatus.Healthy;

                return Task.FromResult(new HealthCheckResult
                {
                    Name = Name,
                    Status = status,
                    Message = $"Free space: {freeBytes / (1024 * 1024 * 1024)}GB",
                    Data = new Dictionary<string, object>
                    {
                        ["freeBytes"] = freeBytes,
                        ["totalBytes"] = drive.TotalSize,
                        ["percentage"] = (freeBytes * 100.0) / drive.TotalSize
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message,
                    Exception = ex
                });
            }
        }
    }

    // HTTP endpoint health check
    public class HttpHealthCheck : IHealthCheck
    {
        private readonly string _url;
        private readonly HttpClient _httpClient;
        public string Name { get; }

        public HttpHealthCheck(string name, string url, HttpClient? httpClient = null)
        {
            Name = name;
            _url = url;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task<HealthCheckResult> CheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_url);
                var status = response.IsSuccessStatusCode ? HealthStatus.Healthy : HealthStatus.Unhealthy;

                return new HealthCheckResult
                {
                    Name = Name,
                    Status = status,
                    Message = $"HTTP {(int)response.StatusCode} {response.StatusCode}",
                    Data = new Dictionary<string, object>
                    {
                        ["statusCode"] = (int)response.StatusCode,
                        ["url"] = _url
                    }
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message,
                    Exception = ex
                };
            }
        }
    }

    // Database health check
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly Func<Task<bool>> _checkConnection;
        public string Name => "Database";

        public DatabaseHealthCheck(Func<Task<bool>> checkConnection)
        {
            _checkConnection = checkConnection;
        }

        public async Task<HealthCheckResult> CheckAsync()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var connected = await _checkConnection();
                sw.Stop();

                var status = connected ? HealthStatus.Healthy : HealthStatus.Unhealthy;
                if (connected && sw.ElapsedMilliseconds > 1000)
                {
                    status = HealthStatus.Degraded;
                }

                return new HealthCheckResult
                {
                    Name = Name,
                    Status = status,
                    Message = connected ? "Connected" : "Connection failed",
                    Data = new Dictionary<string, object>
                    {
                        ["responseTime"] = sw.ElapsedMilliseconds
                    }
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Unhealthy,
                    Message = ex.Message,
                    Exception = ex
                };
            }
        }
    }
}

/// <summary>
/// Example: Application with health checks
/// </summary>
public class HealthyApplication
{
    private readonly SimpleHealthCheckService _healthService;
    private readonly SimpleBackgroundTaskRunner _taskRunner;

    public HealthyApplication()
    {
        _healthService = new SimpleHealthCheckService();
        _taskRunner = new SimpleBackgroundTaskRunner();

        // Register health checks
        _healthService.AddCheck(new CommonHealthChecks.MemoryHealthCheck());
        _healthService.AddCheck(new CommonHealthChecks.DiskSpaceHealthCheck());

        // Custom health checks
        _healthService.AddCheck("Cache", async () =>
        {
            // Check if cache is responsive
            await Task.Delay(10);
            return true;
        });

        _healthService.AddCheck("Queue", async () =>
        {
            return new HealthCheckResult
            {
                Name = "Queue",
                Status = HealthStatus.Healthy,
                Message = "Queue is processing",
                Data = new Dictionary<string, object>
                {
                    ["queueLength"] = 42,
                    ["processingRate"] = 100
                }
            };
        });

        // Start periodic health check
        _taskRunner.RunPeriodic(async ct =>
        {
            var report = await _healthService.CheckHealthAsync();
            Console.WriteLine(report.ToString());
        }, TimeSpan.FromMinutes(1), "HealthCheck");
    }

    public async Task<string> GetHealthStatusAsync()
    {
        var report = await _healthService.CheckHealthAsync();
        return report.ToJson();
    }

    public void Dispose()
    {
        _taskRunner.Dispose();
    }
}

/// <summary>
/// Health check middleware
/// </summary>
public class HealthCheckMiddleware
{
    private readonly SimpleHealthCheckService _healthService;

    public HealthCheckMiddleware(SimpleHealthCheckService healthService)
    {
        _healthService = healthService;
    }

    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (context.Path == "/health")
        {
            var report = await _healthService.CheckHealthAsync();
            context.Response = report.ToJson();
            context.Headers["Content-Type"] = "application/json";
            return;
        }

        if (context.Path == "/health/live")
        {
            // Simple liveness check
            context.Response = "OK";
            return;
        }

        if (context.Path == "/health/ready")
        {
            // Readiness check
            var report = await _healthService.CheckHealthAsync();
            context.Response = report.OverallStatus == HealthStatus.Healthy ? "Ready" : "Not Ready";
            return;
        }

        await next();
    }
}