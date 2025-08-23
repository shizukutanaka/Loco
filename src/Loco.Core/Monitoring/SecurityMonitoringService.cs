using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;

namespace Loco.Core.Monitoring
{
    public interface ISecurityMonitoringService
    {
        Task StartMonitoringAsync(CancellationToken cancellationToken = default);
        Task StopMonitoringAsync();
        void RegisterSecurityAlert(SecurityAlert alert);
        Task<SecurityDashboard> GetSecurityDashboardAsync();
        Task<List<SecurityIncident>> GetActiveIncidentsAsync();
        Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime from, DateTime to);
        void TriggerSecurityIncident(SecurityIncident incident);
        Task<bool> IsSystemUnderAttackAsync();
        Task GenerateSecurityReportAsync(DateTime from, DateTime to);
    }

    public class SecurityAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public SecurityAlertSeverity Severity { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }
        public List<string> Actions { get; set; } = new List<string>();
    }

    public class SecurityIncident
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public SecurityIncidentSeverity Severity { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IncidentStatus Status { get; set; } = IncidentStatus.Active;
        public List<SecurityAlert> RelatedAlerts { get; set; } = new List<SecurityAlert>();
        public List<string> AffectedSystems { get; set; } = new List<string>();
        public Dictionary<string, object> Evidence { get; set; } = new Dictionary<string, object>();
        public List<string> ResponseActions { get; set; } = new List<string>();
        public string AssignedTo { get; set; }
        public int ImpactScore { get; set; }
    }

    public class SecurityDashboard
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public SecurityStatus OverallStatus { get; set; }
        public int ActiveIncidents { get; set; }
        public int CriticalAlerts { get; set; }
        public int HighAlerts { get; set; }
        public int MediumAlerts { get; set; }
        public int LowAlerts { get; set; }
        public double ThreatLevel { get; set; }
        public List<string> TopThreats { get; set; } = new List<string>();
        public SystemHealthMetrics SystemHealth { get; set; } = new SystemHealthMetrics();
        public List<SecurityEvent> RecentEvents { get; set; } = new List<SecurityEvent>();
    }

    public class SecurityMetrics
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalAlerts { get; set; }
        public int TotalIncidents { get; set; }
        public int BlockedAttacks { get; set; }
        public int FailedLogins { get; set; }
        public int MaliciousRequests { get; set; }
        public Dictionary<string, int> AttackTypes { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TopAttackerIps { get; set; } = new Dictionary<string, int>();
        public double AverageResponseTime { get; set; }
        public double SystemUptime { get; set; }
        public List<TrendData> ThreatTrends { get; set; } = new List<TrendData>();
    }

    public class SystemHealthMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkLoad { get; set; }
        public int ActiveConnections { get; set; }
        public TimeSpan Uptime { get; set; }
        public Dictionary<string, bool> ServiceStatus { get; set; } = new Dictionary<string, bool>();
    }

    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; }
        public SecurityAlertSeverity Severity { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    public class TrendData
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public int Count { get; set; }
        public double Value { get; set; }
    }

    public enum SecurityAlertSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum SecurityIncidentSeverity
    {
        Minor = 1,
        Major = 2,
        Critical = 3
    }

    public enum IncidentStatus
    {
        Active,
        Investigating,
        Mitigating,
        Resolved,
        Closed
    }

    public enum SecurityStatus
    {
        Secure,
        Warning,
        Alert,
        Critical
    }

    public class SecurityMonitoringService : ISecurityMonitoringService, IHostedService
    {
        private readonly ILogger<SecurityMonitoringService> _logger;
        private readonly List<SecurityAlert> _alerts;
        private readonly List<SecurityIncident> _incidents;
        private readonly List<SecurityEvent> _events;
        private readonly object _lockObject = new object();
        private readonly Timer _monitoringTimer;
        private readonly Timer _cleanupTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isMonitoring;

        // Threat detection parameters
        private const int AlertRetentionDays = 30;
        private const int IncidentRetentionDays = 90;
        private const int EventRetentionHours = 24;
        private const double HighThreatThreshold = 7.0;
        private const double CriticalThreatThreshold = 9.0;

        public SecurityMonitoringService(ILogger<SecurityMonitoringService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _alerts = new List<SecurityAlert>();
            _incidents = new List<SecurityIncident>();
            _events = new List<SecurityEvent>();
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize performance counters");
            }

            _monitoringTimer = new Timer(MonitoringCallback, null, Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer = new Timer(CleanupCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await StartMonitoringAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopMonitoringAsync();
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isMonitoring = true;

            _logger.LogInformation("Starting security monitoring service");

            // Start monitoring timer (every 30 seconds)
            _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            // Start cleanup timer (every hour)
            _cleanupTimer.Change(TimeSpan.FromHours(1), TimeSpan.FromHours(1));

            // Initial system health check
            await PerformSystemHealthCheckAsync();

            RegisterSecurityAlert(new SecurityAlert
            {
                Severity = SecurityAlertSeverity.Low,
                Type = "System",
                Title = "Security Monitoring Started",
                Description = "Security monitoring service has been started successfully",
                Source = "SecurityMonitoringService"
            });

            await Task.CompletedTask;
        }

        public async Task StopMonitoringAsync()
        {
            _isMonitoring = false;
            _cancellationTokenSource?.Cancel();

            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _logger.LogInformation("Security monitoring service stopped");

            RegisterSecurityAlert(new SecurityAlert
            {
                Severity = SecurityAlertSeverity.Medium,
                Type = "System",
                Title = "Security Monitoring Stopped",
                Description = "Security monitoring service has been stopped",
                Source = "SecurityMonitoringService"
            });

            await Task.CompletedTask;
        }

        public void RegisterSecurityAlert(SecurityAlert alert)
        {
            if (alert == null)
                return;

            lock (_lockObject)
            {
                _alerts.Add(alert);
                
                // Log security event
                _events.Add(new SecurityEvent
                {
                    Timestamp = alert.Timestamp,
                    EventType = alert.Type,
                    Severity = alert.Severity,
                    Description = alert.Title,
                    Source = alert.Source,
                    Details = alert.Metadata
                });

                // Check if alert should trigger an incident
                if (alert.Severity >= SecurityAlertSeverity.High)
                {
                    CheckForIncidentEscalation(alert);
                }
            }

            var logLevel = alert.Severity switch
            {
                SecurityAlertSeverity.Critical => LogLevel.Critical,
                SecurityAlertSeverity.High => LogLevel.Error,
                SecurityAlertSeverity.Medium => LogLevel.Warning,
                _ => LogLevel.Information
            };

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["AlertId"] = alert.Id,
                ["AlertSeverity"] = alert.Severity.ToString(),
                ["AlertType"] = alert.Type
            }))
            {
                _logger.Log(logLevel, "Security Alert: {Title} - {Description}", alert.Title, alert.Description);
            }
        }

        public async Task<SecurityDashboard> GetSecurityDashboardAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var dashboard = new SecurityDashboard
                    {
                        ActiveIncidents = _incidents.Count(i => i.Status == IncidentStatus.Active),
                        CriticalAlerts = _alerts.Count(a => !a.IsResolved && a.Severity == SecurityAlertSeverity.Critical),
                        HighAlerts = _alerts.Count(a => !a.IsResolved && a.Severity == SecurityAlertSeverity.High),
                        MediumAlerts = _alerts.Count(a => !a.IsResolved && a.Severity == SecurityAlertSeverity.Medium),
                        LowAlerts = _alerts.Count(a => !a.IsResolved && a.Severity == SecurityAlertSeverity.Low),
                        SystemHealth = GetSystemHealthMetrics(),
                        RecentEvents = _events.OrderByDescending(e => e.Timestamp).Take(20).ToList()
                    };

                    // Calculate threat level
                    dashboard.ThreatLevel = CalculateThreatLevel();
                    
                    // Set overall status
                    dashboard.OverallStatus = dashboard.ThreatLevel switch
                    {
                        >= CriticalThreatThreshold => SecurityStatus.Critical,
                        >= HighThreatThreshold => SecurityStatus.Alert,
                        >= 5.0 => SecurityStatus.Warning,
                        _ => SecurityStatus.Secure
                    };

                    // Get top threats
                    dashboard.TopThreats = _alerts
                        .Where(a => !a.IsResolved && a.Severity >= SecurityAlertSeverity.Medium)
                        .GroupBy(a => a.Type)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => $"{g.Key} ({g.Count()})")
                        .ToList();

                    return dashboard;
                }
            });
        }

        public async Task<List<SecurityIncident>> GetActiveIncidentsAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    return _incidents
                        .Where(i => i.Status != IncidentStatus.Closed)
                        .OrderByDescending(i => i.StartTime)
                        .ToList();
                }
            });
        }

        public async Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime from, DateTime to)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var alertsInRange = _alerts.Where(a => a.Timestamp >= from && a.Timestamp <= to).ToList();
                    var incidentsInRange = _incidents.Where(i => i.StartTime >= from && i.StartTime <= to).ToList();

                    var metrics = new SecurityMetrics
                    {
                        From = from,
                        To = to,
                        TotalAlerts = alertsInRange.Count,
                        TotalIncidents = incidentsInRange.Count,
                        BlockedAttacks = alertsInRange.Count(a => a.Type.Contains("Attack") || a.Type.Contains("Blocked")),
                        FailedLogins = alertsInRange.Count(a => a.Type.Contains("Login") && a.Type.Contains("Failed")),
                        MaliciousRequests = alertsInRange.Count(a => a.Type.Contains("Malicious") || a.Type.Contains("Injection")),
                        SystemUptime = CalculateUptime(from, to)
                    };

                    // Attack types distribution
                    metrics.AttackTypes = alertsInRange
                        .GroupBy(a => a.Type)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Top attacker IPs (from metadata)
                    metrics.TopAttackerIps = alertsInRange
                        .Where(a => a.Metadata.ContainsKey("IpAddress"))
                        .GroupBy(a => a.Metadata["IpAddress"].ToString())
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Threat trends (hourly)
                    var hours = (int)(to - from).TotalHours;
                    for (int i = 0; i < hours; i++)
                    {
                        var hourStart = from.AddHours(i);
                        var hourEnd = hourStart.AddHours(1);
                        var hourlyAlerts = alertsInRange.Count(a => a.Timestamp >= hourStart && a.Timestamp < hourEnd);
                        
                        metrics.ThreatTrends.Add(new TrendData
                        {
                            Timestamp = hourStart,
                            Category = "Alerts",
                            Count = hourlyAlerts,
                            Value = hourlyAlerts
                        });
                    }

                    return metrics;
                }
            });
        }

        public void TriggerSecurityIncident(SecurityIncident incident)
        {
            if (incident == null)
                return;

            lock (_lockObject)
            {
                _incidents.Add(incident);
            }

            _logger.LogCritical("Security Incident Triggered: {Title} - {Description}", 
                incident.Title, incident.Description);

            // Auto-register critical alert for incident
            RegisterSecurityAlert(new SecurityAlert
            {
                Severity = SecurityAlertSeverity.Critical,
                Type = "Incident",
                Title = $"Security Incident: {incident.Title}",
                Description = incident.Description,
                Source = "SecurityMonitoringService",
                Metadata = new Dictionary<string, object> { ["IncidentId"] = incident.Id }
            });
        }

        public async Task<bool> IsSystemUnderAttackAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    var recentAlerts = _alerts
                        .Where(a => a.Timestamp > DateTime.UtcNow.AddMinutes(-10))
                        .ToList();

                    // Consider system under attack if:
                    // - More than 50 alerts in last 10 minutes
                    // - More than 5 critical alerts in last 10 minutes
                    // - Active critical incident exists
                    
                    var attackIndicators = new[]
                    {
                        recentAlerts.Count > 50,
                        recentAlerts.Count(a => a.Severity == SecurityAlertSeverity.Critical) > 5,
                        _incidents.Any(i => i.Status == IncidentStatus.Active && i.Severity == SecurityIncidentSeverity.Critical)
                    };

                    return attackIndicators.Any(indicator => indicator);
                }
            });
        }

        public async Task GenerateSecurityReportAsync(DateTime from, DateTime to)
        {
            var metrics = await GetSecurityMetricsAsync(from, to);
            var dashboard = await GetSecurityDashboardAsync();

            var report = new
            {
                GeneratedAt = DateTime.UtcNow,
                ReportPeriod = new { From = from, To = to },
                Summary = new
                {
                    TotalAlerts = metrics.TotalAlerts,
                    TotalIncidents = metrics.TotalIncidents,
                    ThreatLevel = dashboard.ThreatLevel,
                    SystemUptime = metrics.SystemUptime
                },
                TopThreats = dashboard.TopThreats,
                AttackTypes = metrics.AttackTypes,
                ThreatTrends = metrics.ThreatTrends,
                SystemHealth = dashboard.SystemHealth,
                Recommendations = GenerateRecommendations(metrics, dashboard)
            };

            var reportJson = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            var fileName = $"security-report-{from:yyyyMMdd}-{to:yyyyMMdd}.json";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "reports", fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            await File.WriteAllTextAsync(filePath, reportJson);

            _logger.LogInformation("Security report generated: {ReportPath}", filePath);
        }

        private void MonitoringCallback(object state)
        {
            if (!_isMonitoring)
                return;

            try
            {
                _ = Task.Run(async () =>
                {
                    await PerformSecurityChecksAsync();
                    await AnalyzeThreatPatternsAsync();
                    await CheckSystemHealthAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security monitoring cycle");
            }
        }

        private void CleanupCallback(object state)
        {
            try
            {
                CleanupOldData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup cycle");
            }
        }

        private async Task PerformSecurityChecksAsync()
        {
            // Check for suspicious patterns
            await CheckForSuspiciousActivityAsync();
            
            // Validate system integrity
            await ValidateSystemIntegrityAsync();
            
            // Check for configuration drift
            await CheckConfigurationDriftAsync();
        }

        private async Task CheckForSuspiciousActivityAsync()
        {
            lock (_lockObject)
            {
                var recentHighSeverityAlerts = _alerts
                    .Where(a => a.Timestamp > DateTime.UtcNow.AddMinutes(-5) && 
                               a.Severity >= SecurityAlertSeverity.High)
                    .Count();

                if (recentHighSeverityAlerts >= 3)
                {
                    RegisterSecurityAlert(new SecurityAlert
                    {
                        Severity = SecurityAlertSeverity.Critical,
                        Type = "Suspicious Activity",
                        Title = "Elevated Threat Activity Detected",
                        Description = $"Detected {recentHighSeverityAlerts} high-severity alerts in the last 5 minutes",
                        Source = "AutomatedThreatDetection"
                    });
                }
            }

            await Task.CompletedTask;
        }

        private async Task ValidateSystemIntegrityAsync()
        {
            // Check critical system files and configurations
            var criticalFiles = new[]
            {
                "appsettings.json",
                "appsettings.Production.json"
            };

            foreach (var file in criticalFiles)
            {
                if (!File.Exists(file))
                {
                    RegisterSecurityAlert(new SecurityAlert
                    {
                        Severity = SecurityAlertSeverity.High,
                        Type = "System Integrity",
                        Title = "Critical File Missing",
                        Description = $"Critical configuration file missing: {file}",
                        Source = "IntegrityChecker"
                    });
                }
            }

            await Task.CompletedTask;
        }

        private async Task CheckConfigurationDriftAsync()
        {
            // In production, this would check for unauthorized configuration changes
            await Task.CompletedTask;
        }

        private async Task AnalyzeThreatPatternsAsync()
        {
            lock (_lockObject)
            {
                // Analyze patterns in recent alerts
                var recentAlerts = _alerts
                    .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-1))
                    .ToList();

                // Group by type and check for spikes
                var alertGroups = recentAlerts
                    .GroupBy(a => a.Type)
                    .Where(g => g.Count() > 10) // More than 10 alerts of same type in 1 hour
                    .ToList();

                foreach (var group in alertGroups)
                {
                    RegisterSecurityAlert(new SecurityAlert
                    {
                        Severity = SecurityAlertSeverity.Medium,
                        Type = "Pattern Analysis",
                        Title = $"Alert Spike Detected: {group.Key}",
                        Description = $"Detected {group.Count()} alerts of type '{group.Key}' in the last hour",
                        Source = "PatternAnalyzer"
                    });
                }
            }

            await Task.CompletedTask;
        }

        private async Task CheckSystemHealthAsync()
        {
            var health = GetSystemHealthMetrics();

            if (health.CpuUsage > 90)
            {
                RegisterSecurityAlert(new SecurityAlert
                {
                    Severity = SecurityAlertSeverity.Medium,
                    Type = "System Health",
                    Title = "High CPU Usage",
                    Description = $"CPU usage is at {health.CpuUsage:F1}%",
                    Source = "SystemHealthMonitor"
                });
            }

            if (health.MemoryUsage > 90)
            {
                RegisterSecurityAlert(new SecurityAlert
                {
                    Severity = SecurityAlertSeverity.Medium,
                    Type = "System Health",
                    Title = "High Memory Usage",
                    Description = $"Memory usage is at {health.MemoryUsage:F1}%",
                    Source = "SystemHealthMonitor"
                });
            }

            await Task.CompletedTask;
        }

        private async Task PerformSystemHealthCheckAsync()
        {
            var health = GetSystemHealthMetrics();
            
            RegisterSecurityAlert(new SecurityAlert
            {
                Severity = SecurityAlertSeverity.Low,
                Type = "System Health",
                Title = "System Health Check",
                Description = $"CPU: {health.CpuUsage:F1}%, Memory: {health.MemoryUsage:F1}%, Uptime: {health.Uptime}",
                Source = "SystemHealthMonitor"
            });

            await Task.CompletedTask;
        }

        private SystemHealthMetrics GetSystemHealthMetrics()
        {
            var health = new SystemHealthMetrics
            {
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                ActiveConnections = 0, // Would be populated from actual connection metrics
                ServiceStatus = new Dictionary<string, bool>
                {
                    ["Database"] = true,
                    ["Cache"] = true,
                    ["Authentication"] = true,
                    ["Logging"] = true
                }
            };

            try
            {
                health.CpuUsage = _cpuCounter?.NextValue() ?? 0;
                health.MemoryUsage = _memoryCounter?.NextValue() ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get performance metrics");
            }

            // Simulate disk usage
            try
            {
                var drive = new DriveInfo(Directory.GetCurrentDirectory());
                health.DiskUsage = ((double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize) * 100;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get disk usage");
            }

            return health;
        }

        private double CalculateThreatLevel()
        {
            lock (_lockObject)
            {
                var recentAlerts = _alerts
                    .Where(a => a.Timestamp > DateTime.UtcNow.AddHours(-1))
                    .ToList();

                var threatScore = 0.0;

                // Weight alerts by severity
                threatScore += recentAlerts.Count(a => a.Severity == SecurityAlertSeverity.Critical) * 4.0;
                threatScore += recentAlerts.Count(a => a.Severity == SecurityAlertSeverity.High) * 2.0;
                threatScore += recentAlerts.Count(a => a.Severity == SecurityAlertSeverity.Medium) * 1.0;
                threatScore += recentAlerts.Count(a => a.Severity == SecurityAlertSeverity.Low) * 0.5;

                // Factor in active incidents
                var activeIncidents = _incidents.Count(i => i.Status == IncidentStatus.Active);
                threatScore += activeIncidents * 3.0;

                // Normalize to 0-10 scale
                return Math.Min(10.0, threatScore / 10.0);
            }
        }

        private double CalculateUptime(DateTime from, DateTime to)
        {
            // Simple uptime calculation - in production would track actual downtime
            var totalTime = to - from;
            var downtime = TimeSpan.Zero; // Would track actual downtime
            
            return ((totalTime - downtime).TotalSeconds / totalTime.TotalSeconds) * 100.0;
        }

        private void CheckForIncidentEscalation(SecurityAlert alert)
        {
            // Check if this alert should escalate to an incident
            var relatedAlerts = _alerts
                .Where(a => a.Type == alert.Type && 
                           a.Timestamp > DateTime.UtcNow.AddMinutes(-10) &&
                           a.Severity >= SecurityAlertSeverity.High)
                .ToList();

            if (relatedAlerts.Count >= 3) // 3 or more high-severity alerts of same type
            {
                var incident = new SecurityIncident
                {
                    Severity = SecurityIncidentSeverity.Major,
                    Type = alert.Type,
                    Title = $"Multiple {alert.Type} Alerts",
                    Description = $"Escalated from {relatedAlerts.Count} related alerts",
                    RelatedAlerts = relatedAlerts,
                    ImpactScore = relatedAlerts.Sum(a => (int)a.Severity)
                };

                TriggerSecurityIncident(incident);
            }
        }

        private List<string> GenerateRecommendations(SecurityMetrics metrics, SecurityDashboard dashboard)
        {
            var recommendations = new List<string>();

            if (dashboard.ThreatLevel > 7.0)
            {
                recommendations.Add("Consider implementing additional DDoS protection measures");
                recommendations.Add("Review and strengthen access controls");
            }

            if (metrics.FailedLogins > 100)
            {
                recommendations.Add("Consider implementing account lockout policies");
                recommendations.Add("Review authentication mechanisms");
            }

            if (dashboard.SystemHealth.CpuUsage > 80)
            {
                recommendations.Add("Consider scaling resources to handle increased load");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Security posture is good. Continue monitoring.");
            }

            return recommendations;
        }

        private void CleanupOldData()
        {
            lock (_lockObject)
            {
                var alertCutoff = DateTime.UtcNow.AddDays(-AlertRetentionDays);
                var incidentCutoff = DateTime.UtcNow.AddDays(-IncidentRetentionDays);
                var eventCutoff = DateTime.UtcNow.AddHours(-EventRetentionHours);

                _alerts.RemoveAll(a => a.Timestamp < alertCutoff && a.IsResolved);
                _incidents.RemoveAll(i => i.StartTime < incidentCutoff && i.Status == IncidentStatus.Closed);
                _events.RemoveAll(e => e.Timestamp < eventCutoff);

                _logger.LogInformation("Completed cleanup of old security data");
            }
        }

        public void Dispose()
        {
            _monitoringTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}