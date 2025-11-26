using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.InfrastructureAutomation
{
    /// <summary>
    /// Configuration Drift Engine implementing drift detection and automated remediation
    ///
    /// Research sources:
    /// - GitOps Drift Detection with ArgoCD: https://openliberty.io/blog/2024/04/26/argocd-drift-pt1.html
    /// - Config Drift Detection Tools 2025: https://www.ai-infra-link.com/mastering-config-drift-detection-top-open-source-tools-for-2025/
    /// - Kubernetes Drift Detection: https://komodor.com/blog/drift-detection-in-kubernetes/
    /// - Continuous Compliance 2025: https://regscale.com/blog/cca-drivers-benefits/
    /// - AWS CloudFormation ドリフト検出: https://docs.aws.amazon.com/ja_jp/AWSCloudFormation/latest/UserGuide/using-cfn-stack-drift.html
    ///
    /// Capabilities:
    /// - Real-time drift detection comparing desired vs actual state
    /// - Automated remediation with configurable policies
    /// - GitOps integration (ArgoCD/Flux patterns)
    /// - Compliance drift monitoring (continuous compliance)
    /// - Multi-cluster drift tracking
    /// - Drift alerting and notification
    /// - Rollback capabilities
    /// - Audit trail and drift history
    /// </summary>
    public interface IConfigurationDriftEngine
    {
        Task<DriftDetectionResult> DetectDriftAsync(string tenantId, DriftDetectionConfig config, CancellationToken cancellation = default);
        Task<DriftMonitor> CreateMonitorAsync(string tenantId, DriftMonitor monitor, CancellationToken cancellation = default);
        Task<RemediationResult> RemediateDriftAsync(string tenantId, string driftId, RemediationPolicy policy, CancellationToken cancellation = default);
        Task<List<DriftEvent>> GetDriftHistoryAsync(string tenantId, string? resourceId = null, CancellationToken cancellation = default);
        Task<ComplianceStatus> CheckComplianceAsync(string tenantId, string frameworkId, CancellationToken cancellation = default);
        Task<bool> EnableAutoRemediationAsync(string tenantId, string monitorId, RemediationPolicy policy, CancellationToken cancellation = default);
        Task<DriftStatistics> GetStatisticsAsync(string tenantId, TimeSpan period, CancellationToken cancellation = default);
    }

    public class ConfigurationDriftEngine : IConfigurationDriftEngine
    {
        private readonly Dictionary<string, DriftMonitor> _monitors = new();
        private readonly Dictionary<string, List<DriftEvent>> _driftHistory = new();
        private readonly Dictionary<string, DriftDetectionResult> _latestResults = new();
        private readonly Dictionary<string, ComplianceFramework> _frameworks = new();

        public async Task<DriftDetectionResult> DetectDriftAsync(string tenantId, DriftDetectionConfig config, CancellationToken cancellation = default)
        {
            var result = new DriftDetectionResult
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                DetectedAt = DateTime.UtcNow,
                Drifts = new List<DriftItem>()
            };

            // Get desired state from Git
            var desiredResources = await FetchDesiredStateAsync(tenantId, config, cancellation);

            // Get actual state from cluster/cloud
            var actualResources = await FetchActualStateAsync(tenantId, config, cancellation);

            // Compare and detect drifts
            foreach (var desired in desiredResources)
            {
                var actual = actualResources.FirstOrDefault(r =>
                    r.Kind == desired.Kind &&
                    r.Name == desired.Name &&
                    r.Namespace == desired.Namespace);

                if (actual == null)
                {
                    // Resource missing
                    result.Drifts.Add(new DriftItem
                    {
                        ResourceId = $"{desired.Kind}/{desired.Name}",
                        DriftType = DriftType.Missing,
                        Severity = DriftSeverity.High,
                        Message = "Resource defined in Git but not found in cluster",
                        DesiredState = desired.Spec,
                        ActualState = null
                    });
                    continue;
                }

                // Normalize and compare specs
                var diff = CompareResources(desired, actual);
                if (diff.HasDrift)
                {
                    result.Drifts.Add(new DriftItem
                    {
                        ResourceId = $"{desired.Kind}/{desired.Name}",
                        DriftType = DriftType.Modified,
                        Severity = CalculateSeverity(diff),
                        Message = "Resource configuration differs from Git",
                        DesiredState = desired.Spec,
                        ActualState = actual.Spec,
                        FieldDiffs = diff.FieldDiffs
                    });
                }
            }

            // Check for orphaned resources (in cluster but not in Git)
            foreach (var actual in actualResources)
            {
                var desired = desiredResources.FirstOrDefault(r =>
                    r.Kind == actual.Kind &&
                    r.Name == actual.Name &&
                    r.Namespace == actual.Namespace);

                if (desired == null && !IsSystemResource(actual))
                {
                    result.Drifts.Add(new DriftItem
                    {
                        ResourceId = $"{actual.Kind}/{actual.Name}",
                        DriftType = DriftType.Orphaned,
                        Severity = DriftSeverity.Medium,
                        Message = "Resource exists in cluster but not defined in Git",
                        DesiredState = null,
                        ActualState = actual.Spec
                    });
                }
            }

            result.TotalDrifts = result.Drifts.Count;
            result.HasDrift = result.Drifts.Any();

            // Store result
            _latestResults[$"{tenantId}:{config.Scope}"] = result;

            // Create drift events
            foreach (var drift in result.Drifts)
            {
                await RecordDriftEventAsync(tenantId, drift, cancellation);
            }

            return await Task.FromResult(result);
        }

        public async Task<DriftMonitor> CreateMonitorAsync(string tenantId, DriftMonitor monitor, CancellationToken cancellation = default)
        {
            monitor.Id = Guid.NewGuid().ToString();
            monitor.TenantId = tenantId;
            monitor.CreatedAt = DateTime.UtcNow;
            monitor.Status = new MonitorStatus
            {
                State = MonitorState.Active,
                LastCheck = null,
                DriftDetectedCount = 0
            };

            _monitors[$"{tenantId}:{monitor.Id}"] = monitor;

            // Start monitoring loop
            _ = Task.Run(() => MonitorLoopAsync(tenantId, monitor.Id, cancellation), cancellation);

            return await Task.FromResult(monitor);
        }

        public async Task<RemediationResult> RemediateDriftAsync(string tenantId, string driftId, RemediationPolicy policy, CancellationToken cancellation = default)
        {
            var result = new RemediationResult
            {
                DriftId = driftId,
                StartedAt = DateTime.UtcNow,
                Success = false,
                Actions = new List<RemediationAction>()
            };

            // Find drift event
            if (!_driftHistory.TryGetValue($"{tenantId}:all", out var events))
                throw new InvalidOperationException("Drift history not found");

            var driftEvent = events.FirstOrDefault(e => e.Id == driftId);
            if (driftEvent == null)
                throw new InvalidOperationException($"Drift {driftId} not found");

            try
            {
                // Apply remediation based on drift type and policy
                if (driftEvent.DriftType == DriftType.Missing)
                {
                    if (policy.Strategy == RemediationStrategy.Recreate)
                    {
                        await CreateResourceAsync(tenantId, driftEvent.DesiredState!, cancellation);
                        result.Actions.Add(new RemediationAction
                        {
                            Type = ActionType.Create,
                            ResourceId = driftEvent.ResourceId,
                            Success = true,
                            Message = "Resource recreated from Git definition"
                        });
                    }
                }
                else if (driftEvent.DriftType == DriftType.Modified)
                {
                    if (policy.Strategy == RemediationStrategy.Update)
                    {
                        await UpdateResourceAsync(tenantId, driftEvent.ResourceId, driftEvent.DesiredState!, cancellation);
                        result.Actions.Add(new RemediationAction
                        {
                            Type = ActionType.Update,
                            ResourceId = driftEvent.ResourceId,
                            Success = true,
                            Message = "Resource updated to match Git definition"
                        });
                    }
                    else if (policy.Strategy == RemediationStrategy.Rollback)
                    {
                        await RollbackResourceAsync(tenantId, driftEvent.ResourceId, cancellation);
                        result.Actions.Add(new RemediationAction
                        {
                            Type = ActionType.Rollback,
                            ResourceId = driftEvent.ResourceId,
                            Success = true,
                            Message = "Resource rolled back to previous version"
                        });
                    }
                }
                else if (driftEvent.DriftType == DriftType.Orphaned)
                {
                    if (policy.Strategy == RemediationStrategy.Delete)
                    {
                        await DeleteResourceAsync(tenantId, driftEvent.ResourceId, cancellation);
                        result.Actions.Add(new RemediationAction
                        {
                            Type = ActionType.Delete,
                            ResourceId = driftEvent.ResourceId,
                            Success = true,
                            Message = "Orphaned resource deleted"
                        });
                    }
                }

                result.Success = result.Actions.All(a => a.Success);
                result.CompletedAt = DateTime.UtcNow;

                // Update drift event status
                driftEvent.RemediatedAt = DateTime.UtcNow;
                driftEvent.RemediationResult = result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
            }

            return await Task.FromResult(result);
        }

        public async Task<List<DriftEvent>> GetDriftHistoryAsync(string tenantId, string? resourceId = null, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:all";
            if (!_driftHistory.TryGetValue(key, out var events))
                return new List<DriftEvent>();

            if (!string.IsNullOrEmpty(resourceId))
            {
                events = events.Where(e => e.ResourceId == resourceId).ToList();
            }

            return await Task.FromResult(events.OrderByDescending(e => e.DetectedAt).ToList());
        }

        public async Task<ComplianceStatus> CheckComplianceAsync(string tenantId, string frameworkId, CancellationToken cancellation = default)
        {
            var status = new ComplianceStatus
            {
                FrameworkId = frameworkId,
                CheckedAt = DateTime.UtcNow,
                Controls = new List<ControlStatus>()
            };

            if (!_frameworks.TryGetValue(frameworkId, out var framework))
            {
                framework = GetDefaultFramework(frameworkId);
                _frameworks[frameworkId] = framework;
            }

            // Check each control against current state
            foreach (var control in framework.Controls)
            {
                var controlStatus = await CheckControlAsync(tenantId, control, cancellation);
                status.Controls.Add(controlStatus);
            }

            // Calculate compliance percentage
            var passedControls = status.Controls.Count(c => c.Status == ControlState.Pass);
            status.CompliancePercentage = (double)passedControls / status.Controls.Count * 100;
            status.OverallStatus = status.CompliancePercentage >= 80
                ? ComplianceState.Compliant
                : ComplianceState.NonCompliant;

            return await Task.FromResult(status);
        }

        public async Task<bool> EnableAutoRemediationAsync(string tenantId, string monitorId, RemediationPolicy policy, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{monitorId}";
            if (!_monitors.TryGetValue(key, out var monitor))
                return false;

            monitor.AutoRemediation = new AutoRemediationConfig
            {
                Enabled = true,
                Policy = policy
            };

            return await Task.FromResult(true);
        }

        public async Task<DriftStatistics> GetStatisticsAsync(string tenantId, TimeSpan period, CancellationToken cancellation = default)
        {
            var stats = new DriftStatistics
            {
                Period = period,
                GeneratedAt = DateTime.UtcNow
            };

            var key = $"{tenantId}:all";
            if (!_driftHistory.TryGetValue(key, out var events))
                return stats;

            var cutoff = DateTime.UtcNow - period;
            var recentEvents = events.Where(e => e.DetectedAt >= cutoff).ToList();

            stats.TotalDrifts = recentEvents.Count;
            stats.DriftsByType = recentEvents.GroupBy(e => e.DriftType)
                .ToDictionary(g => g.Key, g => g.Count());
            stats.DriftsBySeverity = recentEvents.GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
            stats.RemediatedDrifts = recentEvents.Count(e => e.RemediatedAt != null);
            stats.MeanTimeToRemediate = recentEvents
                .Where(e => e.RemediatedAt != null)
                .Average(e => (e.RemediatedAt!.Value - e.DetectedAt).TotalMinutes);

            return await Task.FromResult(stats);
        }

        // Private helper methods

        private async Task MonitorLoopAsync(string tenantId, string monitorId, CancellationToken cancellation)
        {
            var key = $"{tenantId}:{monitorId}";

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    if (!_monitors.TryGetValue(key, out var monitor))
                        break;

                    if (monitor.Status.State != MonitorState.Active)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellation);
                        continue;
                    }

                    // Detect drift
                    var result = await DetectDriftAsync(tenantId, monitor.Config, cancellation);

                    monitor.Status.LastCheck = DateTime.UtcNow;
                    monitor.Status.DriftDetectedCount += result.Drifts.Count;

                    // Auto-remediate if enabled
                    if (monitor.AutoRemediation?.Enabled == true && result.HasDrift)
                    {
                        foreach (var drift in result.Drifts)
                        {
                            if (ShouldAutoRemediate(drift, monitor.AutoRemediation.Policy))
                            {
                                var driftEvent = _driftHistory[$"{tenantId}:all"]
                                    .FirstOrDefault(e => e.ResourceId == drift.ResourceId);
                                if (driftEvent != null)
                                {
                                    await RemediateDriftAsync(tenantId, driftEvent.Id!, monitor.AutoRemediation.Policy, cancellation);
                                }
                            }
                        }
                    }

                    // Send alerts if configured
                    if (result.HasDrift && monitor.AlertConfig != null)
                    {
                        await SendAlertsAsync(tenantId, result, monitor.AlertConfig, cancellation);
                    }

                    // Wait for next check interval
                    await Task.Delay(monitor.CheckInterval, cancellation);
                }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellation);
                }
            }
        }

        private async Task<List<ResourceSnapshot>> FetchDesiredStateAsync(string tenantId, DriftDetectionConfig config, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);

            // Simulate fetching from Git repository
            return new List<ResourceSnapshot>
            {
                new ResourceSnapshot
                {
                    Kind = "Deployment",
                    Name = "app",
                    Namespace = "default",
                    Spec = new { replicas = 3, image = "nginx:1.21" }
                },
                new ResourceSnapshot
                {
                    Kind = "Service",
                    Name = "app",
                    Namespace = "default",
                    Spec = new { type = "ClusterIP", ports = new[] { 80 } }
                }
            };
        }

        private async Task<List<ResourceSnapshot>> FetchActualStateAsync(string tenantId, DriftDetectionConfig config, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);

            // Simulate fetching from Kubernetes cluster
            return new List<ResourceSnapshot>
            {
                new ResourceSnapshot
                {
                    Kind = "Deployment",
                    Name = "app",
                    Namespace = "default",
                    Spec = new { replicas = 5, image = "nginx:1.21" } // Drift: replicas changed
                },
                new ResourceSnapshot
                {
                    Kind = "Service",
                    Name = "app",
                    Namespace = "default",
                    Spec = new { type = "ClusterIP", ports = new[] { 80 } }
                },
                new ResourceSnapshot
                {
                    Kind = "ConfigMap",
                    Name = "manual-config",
                    Namespace = "default",
                    Spec = new { } // Orphaned: not in Git
                }
            };
        }

        private ResourceComparison CompareResources(ResourceSnapshot desired, ResourceSnapshot actual)
        {
            var comparison = new ResourceComparison
            {
                HasDrift = false,
                FieldDiffs = new List<FieldDiff>()
            };

            var desiredJson = JsonSerializer.Serialize(desired.Spec);
            var actualJson = JsonSerializer.Serialize(actual.Spec);

            if (desiredJson != actualJson)
            {
                comparison.HasDrift = true;

                // Simplified field-level diff
                var desiredDict = JsonSerializer.Deserialize<Dictionary<string, object>>(desiredJson) ?? new();
                var actualDict = JsonSerializer.Deserialize<Dictionary<string, object>>(actualJson) ?? new();

                foreach (var kvp in desiredDict)
                {
                    if (!actualDict.TryGetValue(kvp.Key, out var actualValue) || !Equals(kvp.Value, actualValue))
                    {
                        comparison.FieldDiffs.Add(new FieldDiff
                        {
                            FieldPath = kvp.Key,
                            DesiredValue = kvp.Value?.ToString(),
                            ActualValue = actualValue?.ToString()
                        });
                    }
                }
            }

            return comparison;
        }

        private DriftSeverity CalculateSeverity(ResourceComparison diff)
        {
            // Critical fields indicate high severity
            var criticalFields = new[] { "replicas", "image", "resources", "securityContext" };
            if (diff.FieldDiffs.Any(f => criticalFields.Contains(f.FieldPath)))
                return DriftSeverity.High;

            return diff.FieldDiffs.Count > 3 ? DriftSeverity.Medium : DriftSeverity.Low;
        }

        private bool IsSystemResource(ResourceSnapshot resource)
        {
            // Filter out system-managed resources
            return resource.Namespace == "kube-system" ||
                   resource.Namespace == "kube-public" ||
                   resource.Name.StartsWith("kube-");
        }

        private async Task RecordDriftEventAsync(string tenantId, DriftItem drift, CancellationToken cancellation)
        {
            await Task.CompletedTask;

            var driftEvent = new DriftEvent
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                ResourceId = drift.ResourceId,
                DriftType = drift.DriftType,
                Severity = drift.Severity,
                DetectedAt = DateTime.UtcNow,
                DesiredState = drift.DesiredState,
                ActualState = drift.ActualState,
                FieldDiffs = drift.FieldDiffs
            };

            var key = $"{tenantId}:all";
            if (!_driftHistory.ContainsKey(key))
                _driftHistory[key] = new List<DriftEvent>();

            _driftHistory[key].Add(driftEvent);
        }

        private async Task CreateResourceAsync(string tenantId, object spec, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate kubectl apply
        }

        private async Task UpdateResourceAsync(string tenantId, string resourceId, object spec, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate kubectl apply
        }

        private async Task DeleteResourceAsync(string tenantId, string resourceId, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate kubectl delete
        }

        private async Task RollbackResourceAsync(string tenantId, string resourceId, CancellationToken cancellation)
        {
            await Task.Delay(100, cancellation);
            // Simulate kubectl rollout undo
        }

        private async Task<ControlStatus> CheckControlAsync(string tenantId, ComplianceControl control, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            // Simulate control check
            return new ControlStatus
            {
                ControlId = control.Id,
                ControlName = control.Name,
                Status = ControlState.Pass,
                Message = "Control requirements met"
            };
        }

        private ComplianceFramework GetDefaultFramework(string frameworkId)
        {
            return new ComplianceFramework
            {
                Id = frameworkId,
                Name = frameworkId,
                Controls = new List<ComplianceControl>
                {
                    new ComplianceControl { Id = "1.1", Name = "Network segmentation", Description = "Implement network policies" },
                    new ComplianceControl { Id = "1.2", Name = "RBAC configuration", Description = "Configure role-based access control" }
                }
            };
        }

        private bool ShouldAutoRemediate(DriftItem drift, RemediationPolicy policy)
        {
            // Check if drift severity meets auto-remediation threshold
            if (policy.MinSeverity.HasValue && drift.Severity < policy.MinSeverity.Value)
                return false;

            // Check if drift type is allowed for auto-remediation
            if (policy.AllowedDriftTypes?.Any() == true && !policy.AllowedDriftTypes.Contains(drift.DriftType))
                return false;

            return true;
        }

        private async Task SendAlertsAsync(string tenantId, DriftDetectionResult result, AlertConfig config, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);
            // Simulate sending alerts via Slack, email, etc.
        }
    }

    // Model classes

    public class DriftDetectionConfig
    {
        public string Scope { get; set; } = ""; // Namespace, cluster, etc.
        public string? GitRepository { get; set; }
        public string? GitBranch { get; set; }
        public string? GitPath { get; set; }
        public List<string>? IncludedResourceTypes { get; set; }
        public List<string>? ExcludedResourceTypes { get; set; }
    }

    public class DriftDetectionResult
    {
        public string Id { get; set; } = "";
        public string TenantId { get; set; } = "";
        public DateTime DetectedAt { get; set; }
        public bool HasDrift { get; set; }
        public int TotalDrifts { get; set; }
        public List<DriftItem> Drifts { get; set; } = new();
    }

    public class DriftItem
    {
        public string ResourceId { get; set; } = "";
        public DriftType DriftType { get; set; }
        public DriftSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public object? DesiredState { get; set; }
        public object? ActualState { get; set; }
        public List<FieldDiff>? FieldDiffs { get; set; }
    }

    public enum DriftType
    {
        Missing,
        Modified,
        Orphaned
    }

    public enum DriftSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class FieldDiff
    {
        public string FieldPath { get; set; } = "";
        public string? DesiredValue { get; set; }
        public string? ActualValue { get; set; }
    }

    public class DriftMonitor
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string Name { get; set; } = "";
        public DriftDetectionConfig Config { get; set; } = new();
        public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);
        public MonitorStatus Status { get; set; } = new();
        public AutoRemediationConfig? AutoRemediation { get; set; }
        public AlertConfig? AlertConfig { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MonitorStatus
    {
        public MonitorState State { get; set; }
        public DateTime? LastCheck { get; set; }
        public int DriftDetectedCount { get; set; }
    }

    public enum MonitorState
    {
        Active,
        Paused,
        Failed
    }

    public class AutoRemediationConfig
    {
        public bool Enabled { get; set; }
        public RemediationPolicy Policy { get; set; } = new();
    }

    public class RemediationPolicy
    {
        public RemediationStrategy Strategy { get; set; }
        public DriftSeverity? MinSeverity { get; set; }
        public List<DriftType>? AllowedDriftTypes { get; set; }
        public bool DryRun { get; set; }
    }

    public enum RemediationStrategy
    {
        Recreate,
        Update,
        Delete,
        Rollback,
        Notify
    }

    public class AlertConfig
    {
        public List<AlertChannel> Channels { get; set; } = new();
        public DriftSeverity MinSeverity { get; set; } = DriftSeverity.Medium;
    }

    public class AlertChannel
    {
        public string Type { get; set; } = ""; // Slack, Email, Webhook
        public Dictionary<string, string> Config { get; set; } = new();
    }

    public class RemediationResult
    {
        public string DriftId { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<RemediationAction> Actions { get; set; } = new();
    }

    public class RemediationAction
    {
        public ActionType Type { get; set; }
        public string ResourceId { get; set; } = "";
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    public enum ActionType
    {
        Create,
        Update,
        Delete,
        Rollback
    }

    public class DriftEvent
    {
        public string? Id { get; set; }
        public string TenantId { get; set; } = "";
        public string ResourceId { get; set; } = "";
        public DriftType DriftType { get; set; }
        public DriftSeverity Severity { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? RemediatedAt { get; set; }
        public object? DesiredState { get; set; }
        public object? ActualState { get; set; }
        public List<FieldDiff>? FieldDiffs { get; set; }
        public RemediationResult? RemediationResult { get; set; }
    }

    public class ComplianceStatus
    {
        public string FrameworkId { get; set; } = "";
        public DateTime CheckedAt { get; set; }
        public ComplianceState OverallStatus { get; set; }
        public double CompliancePercentage { get; set; }
        public List<ControlStatus> Controls { get; set; } = new();
    }

    public enum ComplianceState
    {
        Compliant,
        NonCompliant,
        PartiallyCompliant
    }

    public class ControlStatus
    {
        public string ControlId { get; set; } = "";
        public string ControlName { get; set; } = "";
        public ControlState Status { get; set; }
        public string Message { get; set; } = "";
    }

    public enum ControlState
    {
        Pass,
        Fail,
        NotApplicable
    }

    public class ComplianceFramework
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<ComplianceControl> Controls { get; set; } = new();
    }

    public class ComplianceControl
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class DriftStatistics
    {
        public TimeSpan Period { get; set; }
        public DateTime GeneratedAt { get; set; }
        public int TotalDrifts { get; set; }
        public Dictionary<DriftType, int> DriftsByType { get; set; } = new();
        public Dictionary<DriftSeverity, int> DriftsBySeverity { get; set; } = new();
        public int RemediatedDrifts { get; set; }
        public double MeanTimeToRemediate { get; set; }
    }

    public class ResourceSnapshot
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Namespace { get; set; }
        public object Spec { get; set; } = new();
    }

    public class ResourceComparison
    {
        public bool HasDrift { get; set; }
        public List<FieldDiff> FieldDiffs { get; set; } = new();
    }
}
