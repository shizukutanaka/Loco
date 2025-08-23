using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Loco.Core.Reliability
{
    public interface ISystemReliabilityService
    {
        Task<SystemHealthReport> GetHealthReportAsync();
        Task<bool> IsSystemHealthyAsync();
        void RegisterHealthCheck(string name, IHealthCheck healthCheck);
        void RegisterDependency(string name, IDependency dependency);
        Task<FailoverResult> TriggerFailoverAsync(string componentName, FailoverReason reason);
        Task<RecoveryResult> TriggerRecoveryAsync(string componentName);
        Task<List<SystemAlert>> GetActiveAlertsAsync();
        Task<SystemMetrics> GetSystemMetricsAsync();
        void LogSystemEvent(SystemEvent systemEvent);
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
        Task<BackupResult> CreateSystemBackupAsync();
        Task<RestoreResult> RestoreFromBackupAsync(string backupId);
    }

    public interface IHealthCheck
    {
        string Name { get; }
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);
        TimeSpan Timeout { get; }
        HealthCheckCriticality Criticality { get; }
    }

    public interface IDependency
    {
        string Name { get; }
        DependencyType Type { get; }
        Task<bool> IsAvailableAsync();
        Task<bool> TestConnectionAsync();
        Task<DependencyStatus> GetStatusAsync();
        bool SupportsFailover { get; }
        Task<bool> FailoverAsync();
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Unhealthy,
        Critical
    }

    public enum HealthCheckCriticality
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum DependencyType
    {
        Database,
        Cache,
        ExternalApi,
        FileSystem,
        MessageQueue,
        ServiceBus,
        WebService,
        LoadBalancer
    }

    public enum DependencyStatus
    {
        Available,
        Degraded,
        Unavailable,
        FailedOver
    }

    public enum FailoverReason
    {
        HealthCheckFailure,
        ManualTrigger,
        LoadThreshold,
        ErrorThreshold,
        Timeout,
        ExternalSignal
    }

    public enum SystemAlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class HealthCheckResult
    {
        public string Name { get; set; }
        public HealthStatus Status { get; set; }
        public string Description { get; set; }
        public TimeSpan Duration { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class SystemHealthReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public HealthStatus OverallStatus { get; set; }
        public TimeSpan Uptime { get; set; }
        public List<HealthCheckResult> HealthChecks { get; set; } = new();
        public List<DependencyStatusReport> Dependencies { get; set; } = new();
        public List<SystemAlert> Alerts { get; set; } = new();
        public SystemMetrics Metrics { get; set; } = new();
        public string Version { get; set; }
        public string Environment { get; set; }
        public Dictionary<string, object> SystemInfo { get; set; } = new();
    }

    public class DependencyStatusReport
    {
        public string Name { get; set; }
        public DependencyType Type { get; set; }
        public DependencyStatus Status { get; set; }
        public string Description { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public DateTime LastChecked { get; set; }
        public bool IsFailedOver { get; set; }
        public string FailoverTarget { get; set; }
    }

    public class SystemMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public int ActiveConnections { get; set; }
        public long RequestsPerSecond { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorRate { get; set; }
        public long TotalRequests { get; set; }
        public long TotalErrors { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new();
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    }

    public class SystemAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public SystemAlertSeverity Severity { get; set; }
        public string Component { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolutionNotes { get; set; }
        public List<string> Actions { get; set; } = new();
    }

    public class SystemEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string EventType { get; set; }
        public string Component { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public SystemAlertSeverity Severity { get; set; }
    }

    public class FailoverResult
    {
        public bool Success { get; set; }
        public string Component { get; set; }
        public FailoverReason Reason { get; set; }
        public string TargetInstance { get; set; }
        public TimeSpan Duration { get; set; }
        public string Message { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public List<string> ActionsPerformed { get; set; } = new();
    }

    public class RecoveryResult
    {
        public bool Success { get; set; }
        public string Component { get; set; }
        public TimeSpan Duration { get; set; }
        public string Message { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public List<string> StepsPerformed { get; set; } = new();
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; }
        public string BackupPath { get; set; }
        public long BackupSize { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Message { get; set; }
        public List<string> ComponentsBackedUp { get; set; } = new();
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string BackupId { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime RestoredAt { get; set; } = DateTime.UtcNow;
        public string Message { get; set; }
        public List<string> ComponentsRestored { get; set; } = new();
    }

    public class SystemReliabilityOptions
    {
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan DependencyCheckInterval { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan AlertRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
        public bool EnableAutoFailover { get; set; } = true;
        public bool EnableAutoRecovery { get; set; } = true;
        public int ConsecutiveFailuresBeforeFailover { get; set; } = 3;
        public TimeSpan FailoverCooldown { get; set; } = TimeSpan.FromMinutes(5);
        public string BackupPath { get; set; } = "backups";
        public int MaxBackupRetention { get; set; } = 30;
        public bool EnableDetailedMetrics { get; set; } = true;
        public double CpuThreshold { get; set; } = 80.0;
        public double MemoryThreshold { get; set; } = 85.0;
        public double DiskThreshold { get; set; } = 90.0;
        public int ErrorRateThreshold { get; set; } = 5; // errors per minute
        public TimeSpan ResponseTimeThreshold { get; set; } = TimeSpan.FromSeconds(5);
    }

    public class SystemReliabilityService : ISystemReliabilityService, IHostedService, IDisposable
    {
        private readonly ILogger<SystemReliabilityService> _logger;
        private readonly SystemReliabilityOptions _options;
        
        // Health checks and dependencies
        private readonly ConcurrentDictionary<string, IHealthCheck> _healthChecks;
        private readonly ConcurrentDictionary<string, IDependency> _dependencies;
        private readonly ConcurrentDictionary<string, HealthCheckResult> _lastHealthCheckResults;
        private readonly ConcurrentDictionary<string, DependencyStatusReport> _dependencyStatuses;
        
        // Monitoring and alerting
        private readonly ConcurrentDictionary<string, SystemAlert> _activeAlerts;
        private readonly ConcurrentQueue<SystemEvent> _systemEvents;
        private readonly ConcurrentDictionary<string, int> _componentFailureCounts;
        private readonly ConcurrentDictionary<string, DateTime> _lastFailoverAttempts;
        
        // Background services
        private readonly Timer _healthCheckTimer;
        private readonly Timer _dependencyCheckTimer;
        private readonly Timer _metricsTimer;
        private readonly Timer _alertCleanupTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        // System metrics
        private readonly SystemMetrics _currentMetrics;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly object _metricsLock = new object();
        
        private readonly DateTime _startTime;
        private volatile bool _isMonitoring;
        private volatile bool _disposed;

        public SystemReliabilityService(
            ILogger<SystemReliabilityService> logger,
            IOptions<SystemReliabilityOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new SystemReliabilityOptions();
            
            _healthChecks = new ConcurrentDictionary<string, IHealthCheck>();
            _dependencies = new ConcurrentDictionary<string, IDependency>();
            _lastHealthCheckResults = new ConcurrentDictionary<string, HealthCheckResult>();
            _dependencyStatuses = new ConcurrentDictionary<string, DependencyStatusReport>();
            
            _activeAlerts = new ConcurrentDictionary<string, SystemAlert>();
            _systemEvents = new ConcurrentQueue<SystemEvent>();
            _componentFailureCounts = new ConcurrentDictionary<string, int>();
            _lastFailoverAttempts = new ConcurrentDictionary<string, DateTime>();
            
            _cancellationTokenSource = new CancellationTokenSource();
            _currentMetrics = new SystemMetrics();
            _startTime = DateTime.UtcNow;
            
            // Initialize performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters");
            }
            
            // Initialize timers
            _healthCheckTimer = new Timer(HealthCheckCallback, null, Timeout.Infinite, Timeout.Infinite);
            _dependencyCheckTimer = new Timer(DependencyCheckCallback, null, Timeout.Infinite, Timeout.Infinite);
            _metricsTimer = new Timer(MetricsCallback, null, Timeout.Infinite, Timeout.Infinite);
            _alertCleanupTimer = new Timer(AlertCleanupCallback, null, Timeout.Infinite, Timeout.Infinite);
            
            // Register default health checks
            RegisterDefaultHealthChecks();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartMonitoringAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopMonitoringAsync();
        }

        public async Task StartMonitoringAsync()
        {
            _isMonitoring = true;
            
            // Start background timers
            _healthCheckTimer.Change(TimeSpan.Zero, _options.HealthCheckInterval);
            _dependencyCheckTimer.Change(TimeSpan.Zero, _options.DependencyCheckInterval);
            _metricsTimer.Change(TimeSpan.Zero, _options.MetricsCollectionInterval);
            _alertCleanupTimer.Change(_options.AlertRetentionPeriod, _options.AlertRetentionPeriod);
            
            LogSystemEvent(new SystemEvent
            {
                EventType = "SystemStart",
                Component = "ReliabilityService",
                Description = "System reliability monitoring started",
                Severity = SystemAlertSeverity.Info
            });
            
            _logger.LogInformation("System reliability monitoring started");
            await Task.CompletedTask;
        }

        public async Task StopMonitoringAsync()
        {
            _isMonitoring = false;
            _cancellationTokenSource.Cancel();
            
            // Stop timers
            _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _dependencyCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _alertCleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            LogSystemEvent(new SystemEvent
            {
                EventType = "SystemStop",
                Component = "ReliabilityService",
                Description = "System reliability monitoring stopped",
                Severity = SystemAlertSeverity.Warning
            });
            
            _logger.LogInformation("System reliability monitoring stopped");
            await Task.CompletedTask;
        }

        public async Task<SystemHealthReport> GetHealthReportAsync()
        {
            var healthChecks = new List<HealthCheckResult>();
            foreach (var result in _lastHealthCheckResults.Values)
            {
                healthChecks.Add(result);
            }
            
            var dependencies = new List<DependencyStatusReport>();
            foreach (var status in _dependencyStatuses.Values)
            {
                dependencies.Add(status);
            }
            
            var alerts = await GetActiveAlertsAsync();
            var metrics = await GetSystemMetricsAsync();
            
            var overallStatus = DetermineOverallHealthStatus(healthChecks, dependencies, alerts);
            
            return new SystemHealthReport
            {
                OverallStatus = overallStatus,
                Uptime = DateTime.UtcNow - _startTime,
                HealthChecks = healthChecks,
                Dependencies = dependencies,
                Alerts = alerts,
                Metrics = metrics,
                Version = GetVersion(),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                SystemInfo = GetSystemInfo()
            };
        }

        public async Task<bool> IsSystemHealthyAsync()
        {
            var report = await GetHealthReportAsync();
            return report.OverallStatus == HealthStatus.Healthy || report.OverallStatus == HealthStatus.Warning;
        }

        public void RegisterHealthCheck(string name, IHealthCheck healthCheck)
        {
            if (string.IsNullOrWhiteSpace(name) || healthCheck == null)
                throw new ArgumentException("Invalid health check registration");

            _healthChecks[name] = healthCheck;
            _logger.LogInformation("Registered health check: {HealthCheckName}", name);
        }

        public void RegisterDependency(string name, IDependency dependency)
        {
            if (string.IsNullOrWhiteSpace(name) || dependency == null)
                throw new ArgumentException("Invalid dependency registration");

            _dependencies[name] = dependency;
            _logger.LogInformation("Registered dependency: {DependencyName} ({Type})", name, dependency.Type);
        }

        public async Task<FailoverResult> TriggerFailoverAsync(string componentName, FailoverReason reason)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new FailoverResult
            {
                Component = componentName,
                Reason = reason
            };

            try
            {
                // Check cooldown period
                if (_lastFailoverAttempts.TryGetValue(componentName, out var lastAttempt) &&
                    DateTime.UtcNow - lastAttempt < _options.FailoverCooldown)
                {
                    result.Success = false;
                    result.Message = $"Failover cooldown period active (last attempt: {lastAttempt})";
                    return result;
                }

                _lastFailoverAttempts[componentName] = DateTime.UtcNow;

                // Execute failover if dependency supports it
                if (_dependencies.TryGetValue(componentName, out var dependency) && dependency.SupportsFailover)
                {
                    var failoverSuccess = await dependency.FailoverAsync();
                    if (failoverSuccess)
                    {
                        result.Success = true;
                        result.Message = "Failover executed successfully";
                        result.ActionsPerformed.Add("Executed dependency failover");
                        
                        // Update dependency status
                        if (_dependencyStatuses.TryGetValue(componentName, out var status))
                        {
                            status.Status = DependencyStatus.FailedOver;
                            status.IsFailedOver = true;
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Dependency failover failed";
                    }
                }
                else
                {
                    result.Success = false;
                    result.Message = "Component does not support failover or dependency not found";
                }

                // Log failover attempt
                LogSystemEvent(new SystemEvent
                {
                    EventType = "Failover",
                    Component = componentName,
                    Description = $"Failover triggered: {reason}",
                    Severity = SystemAlertSeverity.Critical,
                    Data = new Dictionary<string, object>
                    {
                        ["reason"] = reason.ToString(),
                        ["success"] = result.Success
                    }
                });

                // Create alert
                CreateAlert(componentName, SystemAlertSeverity.Critical, 
                    "Failover Executed", 
                    $"Failover triggered for {componentName} due to {reason}");

                _logger.LogCritical("Failover triggered for {Component}: {Reason} - Success: {Success}",
                    componentName, reason, result.Success);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failover failed with exception: {ex.Message}";
                _logger.LogError(ex, "Error during failover for component {Component}", componentName);
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        public async Task<RecoveryResult> TriggerRecoveryAsync(string componentName)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new RecoveryResult
            {
                Component = componentName
            };

            try
            {
                // Reset failure count
                _componentFailureCounts.TryRemove(componentName, out _);
                
                // Clear component alerts
                var componentAlerts = _activeAlerts.Values
                    .Where(a => a.Component == componentName && !a.IsResolved)
                    .ToList();
                
                foreach (var alert in componentAlerts)
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = DateTime.UtcNow;
                    alert.ResolutionNotes = "Auto-resolved by recovery process";
                }
                
                result.StepsPerformed.Add($"Reset failure count for {componentName}");
                result.StepsPerformed.Add($"Resolved {componentAlerts.Count} active alerts");
                
                // Test dependency if available
                if (_dependencies.TryGetValue(componentName, out var dependency))
                {
                    var isAvailable = await dependency.IsAvailableAsync();
                    if (isAvailable)
                    {
                        result.Success = true;
                        result.Message = "Component recovered successfully";
                        
                        // Update dependency status
                        if (_dependencyStatuses.TryGetValue(componentName, out var status))
                        {
                            status.Status = DependencyStatus.Available;
                            status.IsFailedOver = false;
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Component still unavailable after recovery attempt";
                    }
                }
                else
                {
                    result.Success = true;
                    result.Message = "Recovery steps completed (dependency not found)";
                }
                
                LogSystemEvent(new SystemEvent
                {
                    EventType = "Recovery",
                    Component = componentName,
                    Description = "Component recovery initiated",
                    Severity = SystemAlertSeverity.Info,
                    Data = new Dictionary<string, object>
                    {
                        ["success"] = result.Success,
                        ["stepsPerformed"] = result.StepsPerformed.Count
                    }
                });
                
                _logger.LogInformation("Recovery completed for {Component} - Success: {Success}",
                    componentName, result.Success);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Recovery failed with exception: {ex.Message}";
                _logger.LogError(ex, "Error during recovery for component {Component}", componentName);
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        public async Task<List<SystemAlert>> GetActiveAlertsAsync()
        {
            return await Task.Run(() =>
            {
                return _activeAlerts.Values
                    .Where(a => !a.IsResolved)
                    .OrderByDescending(a => a.Severity)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToList();
            });
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_metricsLock)
                {
                    return new SystemMetrics
                    {
                        CpuUsagePercent = _currentMetrics.CpuUsagePercent,
                        MemoryUsagePercent = _currentMetrics.MemoryUsagePercent,
                        DiskUsagePercent = _currentMetrics.DiskUsagePercent,
                        ActiveConnections = _currentMetrics.ActiveConnections,
                        RequestsPerSecond = _currentMetrics.RequestsPerSecond,
                        AverageResponseTime = _currentMetrics.AverageResponseTime,
                        ErrorRate = _currentMetrics.ErrorRate,
                        TotalRequests = _currentMetrics.TotalRequests,
                        TotalErrors = _currentMetrics.TotalErrors,
                        CustomMetrics = new Dictionary<string, double>(_currentMetrics.CustomMetrics)
                    };
                }
            });
        }

        public void LogSystemEvent(SystemEvent systemEvent)
        {
            if (systemEvent == null)
                return;

            _systemEvents.Enqueue(systemEvent);

            // Limit event queue size
            while (_systemEvents.Count > 10000)
            {
                _systemEvents.TryDequeue(out _);
            }

            var logLevel = systemEvent.Severity switch
            {
                SystemAlertSeverity.Critical => LogLevel.Critical,
                SystemAlertSeverity.Error => LogLevel.Error,
                SystemAlertSeverity.Warning => LogLevel.Warning,
                _ => LogLevel.Information
            };

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["SystemEvent"] = true,
                ["EventType"] = systemEvent.EventType,
                ["Component"] = systemEvent.Component,
                ["Severity"] = systemEvent.Severity.ToString()
            }))
            {
                _logger.Log(logLevel, "System Event - {Component}: {Description}", 
                    systemEvent.Component, systemEvent.Description);
            }
        }

        public async Task<BackupResult> CreateSystemBackupAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var backupId = $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var backupPath = Path.Combine(_options.BackupPath, backupId);
            
            var result = new BackupResult
            {
                BackupId = backupId,
                BackupPath = backupPath
            };

            try
            {
                Directory.CreateDirectory(backupPath);
                
                // Backup system configuration
                await BackupSystemConfigurationAsync(backupPath, result);
                
                // Backup application data
                await BackupApplicationDataAsync(backupPath, result);
                
                // Calculate backup size
                result.BackupSize = GetDirectorySize(backupPath);
                result.Success = true;
                result.Message = "System backup completed successfully";
                
                // Clean up old backups
                await CleanupOldBackupsAsync();
                
                _logger.LogInformation("System backup created: {BackupId} ({Size:N0} bytes)", 
                    backupId, result.BackupSize);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Backup failed: {ex.Message}";
                _logger.LogError(ex, "Error creating system backup");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        public async Task<RestoreResult> RestoreFromBackupAsync(string backupId)
        {
            var stopwatch = Stopwatch.StartNew();
            var backupPath = Path.Combine(_options.BackupPath, backupId);
            
            var result = new RestoreResult
            {
                BackupId = backupId
            };

            try
            {
                if (!Directory.Exists(backupPath))
                {
                    result.Success = false;
                    result.Message = "Backup not found";
                    return result;
                }
                
                // Restore system configuration
                await RestoreSystemConfigurationAsync(backupPath, result);
                
                // Restore application data
                await RestoreApplicationDataAsync(backupPath, result);
                
                result.Success = true;
                result.Message = "System restore completed successfully";
                
                _logger.LogInformation("System restored from backup: {BackupId}", backupId);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Restore failed: {ex.Message}";
                _logger.LogError(ex, "Error restoring from backup {BackupId}", backupId);
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        private void RegisterDefaultHealthChecks()
        {
            // Register basic system health checks
            RegisterHealthCheck("Memory", new MemoryHealthCheck(_options.MemoryThreshold));
            RegisterHealthCheck("Disk", new DiskHealthCheck(_options.DiskThreshold));
        }

        private void HealthCheckCallback(object state)
        {
            if (!_isMonitoring)
                return;

            _ = Task.Run(async () =>
            {
                foreach (var healthCheck in _healthChecks)
                {
                    try
                    {
                        var result = await healthCheck.Value.CheckHealthAsync(_cancellationTokenSource.Token);
                        _lastHealthCheckResults[healthCheck.Key] = result;
                        
                        // Handle failures
                        if (result.Status == HealthStatus.Unhealthy || result.Status == HealthStatus.Critical)
                        {
                            HandleHealthCheckFailure(healthCheck.Key, result);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing health check {HealthCheckName}", healthCheck.Key);
                        
                        _lastHealthCheckResults[healthCheck.Key] = new HealthCheckResult
                        {
                            Name = healthCheck.Key,
                            Status = HealthStatus.Critical,
                            Description = $"Health check failed with exception: {ex.Message}",
                            Exception = ex
                        };
                    }
                }
            });
        }

        private void DependencyCheckCallback(object state)
        {
            if (!_isMonitoring)
                return;

            _ = Task.Run(async () =>
            {
                foreach (var dependency in _dependencies)
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var status = await dependency.Value.GetStatusAsync();
                        stopwatch.Stop();
                        
                        _dependencyStatuses[dependency.Key] = new DependencyStatusReport
                        {
                            Name = dependency.Key,
                            Type = dependency.Value.Type,
                            Status = status,
                            ResponseTime = stopwatch.Elapsed,
                            LastChecked = DateTime.UtcNow,
                            Description = $"Status: {status}"
                        };
                        
                        // Handle dependency failures
                        if (status == DependencyStatus.Unavailable)
                        {
                            HandleDependencyFailure(dependency.Key, dependency.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking dependency {DependencyName}", dependency.Key);
                        
                        _dependencyStatuses[dependency.Key] = new DependencyStatusReport
                        {
                            Name = dependency.Key,
                            Type = dependency.Value.Type,
                            Status = DependencyStatus.Unavailable,
                            LastChecked = DateTime.UtcNow,
                            Description = $"Check failed: {ex.Message}"
                        };
                    }
                }
            });
        }

        private void MetricsCallback(object state)
        {
            if (!_isMonitoring)
                return;

            try
            {
                lock (_metricsLock)
                {
                    // Update system metrics
                    _currentMetrics.CpuUsagePercent = _cpuCounter?.NextValue() ?? 0;
                    _currentMetrics.MemoryUsagePercent = _memoryCounter?.NextValue() ?? 0;
                    _currentMetrics.DiskUsagePercent = GetDiskUsagePercent();
                    _currentMetrics.CollectedAt = DateTime.UtcNow;
                }

                // Check thresholds
                CheckMetricThresholds();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting system metrics");
            }
        }

        private void AlertCleanupCallback(object state)
        {
            try
            {
                var cutoff = DateTime.UtcNow - _options.AlertRetentionPeriod;
                var alertsToRemove = _activeAlerts.Values
                    .Where(a => a.IsResolved && a.ResolvedAt.HasValue && a.ResolvedAt.Value < cutoff)
                    .Select(a => a.Id)
                    .ToList();

                foreach (var alertId in alertsToRemove)
                {
                    _activeAlerts.TryRemove(alertId, out _);
                }

                if (alertsToRemove.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} old resolved alerts", alertsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert cleanup");
            }
        }

        private void HandleHealthCheckFailure(string healthCheckName, HealthCheckResult result)
        {
            var failureCount = _componentFailureCounts.AddOrUpdate(healthCheckName, 1, (k, v) => v + 1);
            
            CreateAlert(healthCheckName, SystemAlertSeverity.Error,
                "Health Check Failed",
                $"Health check '{healthCheckName}' failed: {result.Description}");
            
            // Trigger failover if configured and threshold reached
            if (_options.EnableAutoFailover && 
                failureCount >= _options.ConsecutiveFailuresBeforeFailover &&
                _dependencies.ContainsKey(healthCheckName))
            {
                _ = Task.Run(async () => await TriggerFailoverAsync(healthCheckName, FailoverReason.HealthCheckFailure));
            }
        }

        private void HandleDependencyFailure(string dependencyName, IDependency dependency)
        {
            var failureCount = _componentFailureCounts.AddOrUpdate(dependencyName, 1, (k, v) => v + 1);
            
            CreateAlert(dependencyName, SystemAlertSeverity.Critical,
                "Dependency Unavailable",
                $"Dependency '{dependencyName}' is unavailable");
            
            // Trigger failover if configured and threshold reached
            if (_options.EnableAutoFailover && 
                dependency.SupportsFailover &&
                failureCount >= _options.ConsecutiveFailuresBeforeFailover)
            {
                _ = Task.Run(async () => await TriggerFailoverAsync(dependencyName, FailoverReason.HealthCheckFailure));
            }
        }

        private void CheckMetricThresholds()
        {
            lock (_metricsLock)
            {
                if (_currentMetrics.CpuUsagePercent > _options.CpuThreshold)
                {
                    CreateAlert("CPU", SystemAlertSeverity.Warning,
                        "High CPU Usage",
                        $"CPU usage is {_currentMetrics.CpuUsagePercent:F1}% (threshold: {_options.CpuThreshold}%)");
                }

                if (_currentMetrics.MemoryUsagePercent > _options.MemoryThreshold)
                {
                    CreateAlert("Memory", SystemAlertSeverity.Warning,
                        "High Memory Usage",
                        $"Memory usage is {_currentMetrics.MemoryUsagePercent:F1}% (threshold: {_options.MemoryThreshold}%)");
                }

                if (_currentMetrics.DiskUsagePercent > _options.DiskThreshold)
                {
                    CreateAlert("Disk", SystemAlertSeverity.Critical,
                        "High Disk Usage",
                        $"Disk usage is {_currentMetrics.DiskUsagePercent:F1}% (threshold: {_options.DiskThreshold}%)");
                }
            }
        }

        private void CreateAlert(string component, SystemAlertSeverity severity, string title, string description)
        {
            var alert = new SystemAlert
            {
                Component = component,
                Severity = severity,
                Title = title,
                Description = description,
                Source = "SystemReliabilityService"
            };

            _activeAlerts[alert.Id] = alert;

            LogSystemEvent(new SystemEvent
            {
                EventType = "Alert",
                Component = component,
                Description = title,
                Severity = severity
            });
        }

        private HealthStatus DetermineOverallHealthStatus(
            List<HealthCheckResult> healthChecks,
            List<DependencyStatusReport> dependencies,
            List<SystemAlert> alerts)
        {
            // Critical if any critical health checks fail
            if (healthChecks.Any(h => h.Status == HealthStatus.Critical) ||
                dependencies.Any(d => d.Status == DependencyStatus.Unavailable) ||
                alerts.Any(a => a.Severity == SystemAlertSeverity.Critical))
            {
                return HealthStatus.Critical;
            }

            // Unhealthy if any health checks are unhealthy
            if (healthChecks.Any(h => h.Status == HealthStatus.Unhealthy) ||
                alerts.Any(a => a.Severity == SystemAlertSeverity.Error))
            {
                return HealthStatus.Unhealthy;
            }

            // Warning if any warnings exist
            if (healthChecks.Any(h => h.Status == HealthStatus.Warning) ||
                dependencies.Any(d => d.Status == DependencyStatus.Degraded) ||
                alerts.Any(a => a.Severity == SystemAlertSeverity.Warning))
            {
                return HealthStatus.Warning;
            }

            return HealthStatus.Healthy;
        }

        private double GetDiskUsagePercent()
        {
            try
            {
                var drive = new DriveInfo(Directory.GetCurrentDirectory());
                var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
                return (double)usedSpace / drive.TotalSize * 100;
            }
            catch
            {
                return 0;
            }
        }

        private string GetVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        }

        private Dictionary<string, object> GetSystemInfo()
        {
            return new Dictionary<string, object>
            {
                ["MachineName"] = Environment.MachineName,
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["ProcessorCount"] = Environment.ProcessorCount,
                ["RuntimeVersion"] = Environment.Version.ToString(),
                ["WorkingSet"] = Environment.WorkingSet,
                ["UserName"] = Environment.UserName,
                ["SystemDirectory"] = Environment.SystemDirectory,
                ["CurrentDirectory"] = Environment.CurrentDirectory
            };
        }

        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            return files.Sum(file => new FileInfo(file).Length);
        }

        private async Task BackupSystemConfigurationAsync(string backupPath, BackupResult result)
        {
            var configPath = Path.Combine(backupPath, "config");
            Directory.CreateDirectory(configPath);
            
            // Backup configuration files
            var configFiles = new[] { "appsettings.json", "appsettings.Production.json" };
            foreach (var configFile in configFiles)
            {
                if (File.Exists(configFile))
                {
                    File.Copy(configFile, Path.Combine(configPath, configFile));
                    result.ComponentsBackedUp.Add(configFile);
                }
            }
        }

        private async Task BackupApplicationDataAsync(string backupPath, BackupResult result)
        {
            // In a real implementation, this would backup databases, files, etc.
            var dataPath = Path.Combine(backupPath, "data");
            Directory.CreateDirectory(dataPath);
            result.ComponentsBackedUp.Add("ApplicationData");
        }

        private async Task RestoreSystemConfigurationAsync(string backupPath, RestoreResult result)
        {
            var configPath = Path.Combine(backupPath, "config");
            if (Directory.Exists(configPath))
            {
                var configFiles = Directory.GetFiles(configPath);
                foreach (var file in configFiles)
                {
                    var fileName = Path.GetFileName(file);
                    File.Copy(file, fileName, true);
                    result.ComponentsRestored.Add(fileName);
                }
            }
        }

        private async Task RestoreApplicationDataAsync(string backupPath, RestoreResult result)
        {
            // In a real implementation, this would restore databases, files, etc.
            result.ComponentsRestored.Add("ApplicationData");
        }

        private async Task CleanupOldBackupsAsync()
        {
            if (!Directory.Exists(_options.BackupPath))
                return;

            var backupDirs = Directory.GetDirectories(_options.BackupPath)
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .Skip(_options.MaxBackupRetention)
                .ToList();

            foreach (var dir in backupDirs)
            {
                try
                {
                    Directory.Delete(dir, true);
                    _logger.LogDebug("Deleted old backup: {BackupPath}", dir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {BackupPath}", dir);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            _healthCheckTimer?.Dispose();
            _dependencyCheckTimer?.Dispose();
            _metricsTimer?.Dispose();
            _alertCleanupTimer?.Dispose();
            
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();

            _logger.LogInformation("System reliability service disposed");
        }
    }

    // Example health check implementations
    public class MemoryHealthCheck : IHealthCheck
    {
        public string Name => "Memory";
        public TimeSpan Timeout => TimeSpan.FromSeconds(5);
        public HealthCheckCriticality Criticality => HealthCheckCriticality.High;
        
        private readonly double _threshold;

        public MemoryHealthCheck(double threshold)
        {
            _threshold = threshold;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var totalMemory = GC.GetTotalMemory(false);
                var workingSet = Environment.WorkingSet;
                var memoryUsage = (double)workingSet / (1024 * 1024 * 1024); // GB
                
                var status = memoryUsage > _threshold ? HealthStatus.Warning : HealthStatus.Healthy;
                
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = status,
                    Description = $"Memory usage: {memoryUsage:F2} GB",
                    Duration = stopwatch.Elapsed,
                    Data = new Dictionary<string, object>
                    {
                        ["totalMemory"] = totalMemory,
                        ["workingSet"] = workingSet,
                        ["memoryUsageGB"] = memoryUsage,
                        ["threshold"] = _threshold
                    }
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Critical,
                    Description = $"Memory check failed: {ex.Message}",
                    Duration = stopwatch.Elapsed,
                    Exception = ex
                };
            }
        }
    }

    public class DiskHealthCheck : IHealthCheck
    {
        public string Name => "Disk";
        public TimeSpan Timeout => TimeSpan.FromSeconds(5);
        public HealthCheckCriticality Criticality => HealthCheckCriticality.Critical;
        
        private readonly double _threshold;

        public DiskHealthCheck(double threshold)
        {
            _threshold = threshold;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var drive = new DriveInfo(Directory.GetCurrentDirectory());
                var usedSpace = drive.TotalSize - drive.AvailableFreeSpace;
                var usagePercent = (double)usedSpace / drive.TotalSize * 100;
                
                var status = usagePercent > _threshold ? HealthStatus.Critical : HealthStatus.Healthy;
                
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = status,
                    Description = $"Disk usage: {usagePercent:F1}%",
                    Duration = stopwatch.Elapsed,
                    Data = new Dictionary<string, object>
                    {
                        ["totalSize"] = drive.TotalSize,
                        ["availableSpace"] = drive.AvailableFreeSpace,
                        ["usedSpace"] = usedSpace,
                        ["usagePercent"] = usagePercent,
                        ["threshold"] = _threshold
                    }
                };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Name = Name,
                    Status = HealthStatus.Critical,
                    Description = $"Disk check failed: {ex.Message}",
                    Duration = stopwatch.Elapsed,
                    Exception = ex
                };
            }
        }
    }
}