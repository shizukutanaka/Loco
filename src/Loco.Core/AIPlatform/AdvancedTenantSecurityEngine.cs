// =============================================================================
// Advanced Tenant Security Engine - vNode & Policy-as-Code Integration
// =============================================================================
// Research Sources (2025):
// - https://www.vcluster.com/blog/vnode-kubernetes-node-isolation-multi-tenancy
// - https://kubernetes.io/docs/concepts/security/multi-tenancy/
// - https://www.openpolicyagent.org/docs/latest/kubernetes-introduction/
// - arXiv 2505.22864: "National Research Platform - Multi-Tenant Kubernetes Security"
//
// Key Concepts:
// - vNode: Virtual nodes for node-level isolation without physical separation
// - Policy-as-Code: OPA/Gatekeeper for admission control policies
// - Cost Allocation: Per-tenant resource tracking and billing
// - Security Posture: Real-time security scoring and compliance
// - Admission Control: Validating and mutating webhooks
//
// 2025 Insight:
// "Control plane isolation alone does not solve data plane issues like
//  noisy neighbors or security threats. These must be addressed separately."
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIPlatform
{
    #region Enums

    /// <summary>
    /// Policy enforcement mode
    /// </summary>
    public enum PolicyEnforcementMode
    {
        /// <summary>Log violations but don't block</summary>
        Audit,
        /// <summary>Warn users but allow</summary>
        Warn,
        /// <summary>Deny violations</summary>
        Deny
    }

    /// <summary>
    /// Policy category
    /// </summary>
    public enum PolicyCategory
    {
        Security,
        ResourceManagement,
        Networking,
        Compliance,
        CostControl,
        Governance,
        Custom
    }

    /// <summary>
    /// Compliance framework
    /// </summary>
    public enum ComplianceFramework
    {
        SOC2,
        HIPAA,
        GDPR,
        PCI_DSS,
        ISO27001,
        FedRAMP,
        NIST,
        CIS_Benchmark,
        Custom
    }

    /// <summary>
    /// Security severity level
    /// </summary>
    public enum SecuritySeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    /// <summary>
    /// vNode status
    /// </summary>
    public enum VNodeStatus
    {
        Pending,
        Creating,
        Ready,
        NotReady,
        Draining,
        Deleted
    }

    /// <summary>
    /// Cost allocation method
    /// </summary>
    public enum CostAllocationMethod
    {
        /// <summary>Actual resource usage</summary>
        ActualUsage,
        /// <summary>Requested resources</summary>
        RequestedResources,
        /// <summary>Fixed allocation per tenant</summary>
        FixedAllocation,
        /// <summary>Hybrid (min of actual and requested)</summary>
        Hybrid
    }

    #endregion

    #region Configuration Classes

    /// <summary>
    /// vNode configuration
    /// </summary>
    public class VNodeConfig
    {
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string PhysicalNodeName { get; set; } = string.Empty;
        public VNodeResourceConfig Resources { get; set; } = new();
        public VNodeSecurityConfig Security { get; set; } = new();
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();
        public List<string> Taints { get; set; } = new();
    }

    /// <summary>
    /// vNode resource configuration
    /// </summary>
    public class VNodeResourceConfig
    {
        public string CpuCapacity { get; set; } = "8";
        public string MemoryCapacity { get; set; } = "32Gi";
        public string EphemeralStorageCapacity { get; set; } = "100Gi";
        public int GPUCapacity { get; set; } = 0;
        public int MaxPods { get; set; } = 110;
    }

    /// <summary>
    /// vNode security configuration
    /// </summary>
    public class VNodeSecurityConfig
    {
        public bool EnableUserNamespaceIsolation { get; set; } = true;
        public bool EnableSeccompDefault { get; set; } = true;
        public bool EnableAppArmor { get; set; } = true;
        public string PodSecurityStandard { get; set; } = "restricted";
        public bool BlockHostNamespace { get; set; } = true;
        public bool BlockPrivilegedContainers { get; set; } = true;
    }

    /// <summary>
    /// Policy template configuration
    /// </summary>
    public class PolicyTemplateConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PolicyCategory Category { get; set; }
        public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Deny;
        public string RegoPolicy { get; set; } = string.Empty;
        public List<string> TargetKinds { get; set; } = new();
        public List<string> ExcludedNamespaces { get; set; } = new() { "kube-system", "gatekeeper-system" };
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<ComplianceFramework> ComplianceFrameworks { get; set; } = new();
        public SecuritySeverity Severity { get; set; } = SecuritySeverity.Medium;
    }

    /// <summary>
    /// Constraint template configuration (Gatekeeper)
    /// </summary>
    public class ConstraintTemplateConfig
    {
        public string Name { get; set; } = string.Empty;
        public string CRDName { get; set; } = string.Empty;
        public List<string> TargetKinds { get; set; } = new();
        public string RegoCode { get; set; } = string.Empty;
        public Dictionary<string, ParameterSchema> ParameterSchema { get; set; } = new();
    }

    /// <summary>
    /// Parameter schema for constraint templates
    /// </summary>
    public class ParameterSchema
    {
        public string Type { get; set; } = "string";
        public string Description { get; set; } = string.Empty;
        public object? Default { get; set; }
        public bool Required { get; set; } = false;
    }

    /// <summary>
    /// Constraint configuration
    /// </summary>
    public class ConstraintConfig
    {
        public string Name { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Deny;
        public ConstraintMatch Match { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Constraint match configuration
    /// </summary>
    public class ConstraintMatch
    {
        public List<string> Kinds { get; set; } = new();
        public List<string> Namespaces { get; set; } = new();
        public List<string> ExcludedNamespaces { get; set; } = new();
        public Dictionary<string, string> LabelSelector { get; set; } = new();
    }

    /// <summary>
    /// Cost allocation configuration
    /// </summary>
    public class CostAllocationConfig
    {
        public string TenantId { get; set; } = string.Empty;
        public CostAllocationMethod Method { get; set; } = CostAllocationMethod.ActualUsage;
        public CostRates Rates { get; set; } = new();
        public decimal MonthlyBudget { get; set; }
        public decimal AlertThresholdPercent { get; set; } = 80;
        public bool EnableChargeBack { get; set; } = true;
    }

    /// <summary>
    /// Cost rates per resource type
    /// </summary>
    public class CostRates
    {
        public decimal CpuPerCoreHour { get; set; } = 0.05m;
        public decimal MemoryPerGiBHour { get; set; } = 0.01m;
        public decimal StoragePerGiBMonth { get; set; } = 0.10m;
        public decimal GPUPerHour { get; set; } = 2.50m;
        public decimal NetworkEgressPerGB { get; set; } = 0.12m;
    }

    /// <summary>
    /// Security posture configuration
    /// </summary>
    public class SecurityPostureConfig
    {
        public string TenantId { get; set; } = string.Empty;
        public List<ComplianceFramework> RequiredFrameworks { get; set; } = new();
        public bool EnableContinuousScanning { get; set; } = true;
        public int ScanIntervalMinutes { get; set; } = 60;
        public SecurityAlertConfig Alerts { get; set; } = new();
    }

    /// <summary>
    /// Security alert configuration
    /// </summary>
    public class SecurityAlertConfig
    {
        public SecuritySeverity MinimumSeverity { get; set; } = SecuritySeverity.Medium;
        public List<string> NotificationChannels { get; set; } = new();
        public bool EnableAutoRemediation { get; set; } = false;
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// vNode information
    /// </summary>
    public class VNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string PhysicalNodeName { get; set; } = string.Empty;
        public VNodeStatus Status { get; set; }
        public VNodeResourceConfig Capacity { get; set; } = new();
        public VNodeResourceUsage Usage { get; set; } = new();
        public VNodeSecurityConfig Security { get; set; } = new();
        public int RunningPods { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<VNodeCondition> Conditions { get; set; } = new();
    }

    /// <summary>
    /// vNode resource usage
    /// </summary>
    public class VNodeResourceUsage
    {
        public string CpuUsed { get; set; } = "0";
        public string MemoryUsed { get; set; } = "0";
        public string StorageUsed { get; set; } = "0";
        public int GPUsUsed { get; set; }
        public double CpuUtilizationPercent { get; set; }
        public double MemoryUtilizationPercent { get; set; }
    }

    /// <summary>
    /// vNode condition
    /// </summary>
    public class VNodeCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime LastTransitionTime { get; set; }
    }

    /// <summary>
    /// Policy template
    /// </summary>
    public class PolicyTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PolicyCategory Category { get; set; }
        public List<string> TargetKinds { get; set; } = new();
        public string RegoPolicy { get; set; } = string.Empty;
        public List<ComplianceFramework> ComplianceFrameworks { get; set; } = new();
        public SecuritySeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ActiveConstraints { get; set; }
    }

    /// <summary>
    /// Constraint
    /// </summary>
    public class Constraint
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public PolicyEnforcementMode EnforcementMode { get; set; }
        public ConstraintMatch Match { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int TotalViolations { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAuditTime { get; set; }
    }

    /// <summary>
    /// Policy violation
    /// </summary>
    public class PolicyViolation
    {
        public string Id { get; set; } = string.Empty;
        public string ConstraintName { get; set; } = string.Empty;
        public string ResourceKind { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public SecuritySeverity Severity { get; set; }
        public PolicyEnforcementMode EnforcementAction { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool Resolved { get; set; }
    }

    /// <summary>
    /// Tenant cost report
    /// </summary>
    public class TenantCostReport
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalCost { get; set; }
        public CostBreakdown Breakdown { get; set; } = new();
        public decimal Budget { get; set; }
        public decimal BudgetUsedPercent { get; set; }
        public List<NamespaceCost> ByNamespace { get; set; } = new();
        public List<WorkloadCost> TopWorkloads { get; set; } = new();
    }

    /// <summary>
    /// Cost breakdown by resource type
    /// </summary>
    public class CostBreakdown
    {
        public decimal CpuCost { get; set; }
        public decimal MemoryCost { get; set; }
        public decimal StorageCost { get; set; }
        public decimal GPUCost { get; set; }
        public decimal NetworkCost { get; set; }
    }

    /// <summary>
    /// Namespace cost
    /// </summary>
    public class NamespaceCost
    {
        public string Namespace { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public double CpuCoreHours { get; set; }
        public double MemoryGiBHours { get; set; }
        public double GPUHours { get; set; }
    }

    /// <summary>
    /// Workload cost
    /// </summary>
    public class WorkloadCost
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public decimal Cost { get; set; }
    }

    /// <summary>
    /// Security posture score
    /// </summary>
    public class SecurityPostureScore
    {
        public string TenantId { get; set; } = string.Empty;
        public double OverallScore { get; set; }
        public string Grade { get; set; } = string.Empty;
        public Dictionary<PolicyCategory, double> CategoryScores { get; set; } = new();
        public Dictionary<ComplianceFramework, ComplianceStatus> ComplianceStatus { get; set; } = new();
        public int CriticalViolations { get; set; }
        public int HighViolations { get; set; }
        public int MediumViolations { get; set; }
        public int LowViolations { get; set; }
        public List<SecurityRecommendation> Recommendations { get; set; } = new();
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>
    /// Compliance status
    /// </summary>
    public class ComplianceStatus
    {
        public bool IsCompliant { get; set; }
        public double CompliancePercent { get; set; }
        public int PassedControls { get; set; }
        public int FailedControls { get; set; }
        public int TotalControls { get; set; }
    }

    /// <summary>
    /// Security recommendation
    /// </summary>
    public class SecurityRecommendation
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SecuritySeverity Severity { get; set; }
        public string RemediationSteps { get; set; } = string.Empty;
        public double ImpactOnScore { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Advanced Tenant Security Engine interface
    /// </summary>
    public interface IAdvancedTenantSecurityEngine
    {
        // vNode Management
        Task<VNode> CreateVNodeAsync(VNodeConfig config, CancellationToken cancellation = default);
        Task<VNode> GetVNodeAsync(string vnodeId, CancellationToken cancellation = default);
        Task<List<VNode>> ListVNodesAsync(string? tenantId = null, CancellationToken cancellation = default);
        Task<VNode> UpdateVNodeAsync(string vnodeId, VNodeConfig config, CancellationToken cancellation = default);
        Task DeleteVNodeAsync(string vnodeId, CancellationToken cancellation = default);
        Task DrainVNodeAsync(string vnodeId, CancellationToken cancellation = default);

        // Policy Template Management (OPA/Gatekeeper)
        Task<PolicyTemplate> CreatePolicyTemplateAsync(PolicyTemplateConfig config, CancellationToken cancellation = default);
        Task<PolicyTemplate> GetPolicyTemplateAsync(string templateId, CancellationToken cancellation = default);
        Task<List<PolicyTemplate>> ListPolicyTemplatesAsync(PolicyCategory? category = null, CancellationToken cancellation = default);
        Task DeletePolicyTemplateAsync(string templateId, CancellationToken cancellation = default);
        Task<List<PolicyTemplate>> GetBuiltInPolicyTemplatesAsync(CancellationToken cancellation = default);

        // Constraint Management
        Task<Constraint> CreateConstraintAsync(ConstraintConfig config, CancellationToken cancellation = default);
        Task<Constraint> GetConstraintAsync(string constraintId, CancellationToken cancellation = default);
        Task<List<Constraint>> ListConstraintsAsync(string? templateName = null, CancellationToken cancellation = default);
        Task<Constraint> UpdateConstraintAsync(string constraintId, ConstraintConfig config, CancellationToken cancellation = default);
        Task DeleteConstraintAsync(string constraintId, CancellationToken cancellation = default);

        // Policy Violations
        Task<List<PolicyViolation>> GetViolationsAsync(string? tenantId = null, SecuritySeverity? minSeverity = null, CancellationToken cancellation = default);
        Task<PolicyViolation> GetViolationAsync(string violationId, CancellationToken cancellation = default);
        Task ResolveViolationAsync(string violationId, string resolution, CancellationToken cancellation = default);
        Task<int> GetViolationCountAsync(string tenantId, CancellationToken cancellation = default);

        // Cost Allocation & Tracking
        Task<CostAllocationConfig> ConfigureCostAllocationAsync(CostAllocationConfig config, CancellationToken cancellation = default);
        Task<TenantCostReport> GetCostReportAsync(string tenantId, DateTime start, DateTime end, CancellationToken cancellation = default);
        Task<List<TenantCostReport>> GetAllTenantCostReportsAsync(DateTime start, DateTime end, CancellationToken cancellation = default);
        Task<decimal> GetCurrentSpendAsync(string tenantId, CancellationToken cancellation = default);

        // Security Posture
        Task<SecurityPostureScore> GetSecurityPostureAsync(string tenantId, CancellationToken cancellation = default);
        Task<SecurityPostureScore> ScanSecurityPostureAsync(string tenantId, CancellationToken cancellation = default);
        Task<List<SecurityRecommendation>> GetSecurityRecommendationsAsync(string tenantId, CancellationToken cancellation = default);
        Task ConfigureSecurityPostureAsync(SecurityPostureConfig config, CancellationToken cancellation = default);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Advanced Tenant Security Engine implementation
    /// </summary>
    public class AdvancedTenantSecurityEngine : IAdvancedTenantSecurityEngine
    {
        private readonly ILogger<AdvancedTenantSecurityEngine> _logger;
        private readonly ConcurrentDictionary<string, VNode> _vnodes = new();
        private readonly ConcurrentDictionary<string, PolicyTemplate> _policyTemplates = new();
        private readonly ConcurrentDictionary<string, Constraint> _constraints = new();
        private readonly ConcurrentDictionary<string, PolicyViolation> _violations = new();
        private readonly ConcurrentDictionary<string, CostAllocationConfig> _costConfigs = new();
        private readonly ConcurrentDictionary<string, SecurityPostureConfig> _securityConfigs = new();

        public AdvancedTenantSecurityEngine(ILogger<AdvancedTenantSecurityEngine> logger)
        {
            _logger = logger;
            InitializeBuiltInPolicies();
        }

        #region vNode Management

        public async Task<VNode> CreateVNodeAsync(VNodeConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating vNode: {VNodeName} for tenant: {TenantId}",
                config.Name, config.TenantId);

            var vnode = new VNode
            {
                Id = GenerateId("vnode"),
                Name = config.Name,
                TenantId = config.TenantId,
                PhysicalNodeName = config.PhysicalNodeName,
                Status = VNodeStatus.Creating,
                Capacity = config.Resources,
                Security = config.Security,
                Usage = new VNodeResourceUsage(),
                CreatedAt = DateTime.UtcNow,
                Conditions = new List<VNodeCondition>
                {
                    new VNodeCondition
                    {
                        Type = "Ready",
                        Status = "False",
                        Reason = "Creating",
                        LastTransitionTime = DateTime.UtcNow
                    }
                }
            };

            // Generate vNode YAML
            var vnodeYaml = GenerateVNodeYaml(config);
            _logger.LogDebug("Generated vNode YAML:\n{Yaml}", vnodeYaml);

            await Task.Delay(200, cancellation);

            vnode.Status = VNodeStatus.Ready;
            vnode.Conditions[0] = new VNodeCondition
            {
                Type = "Ready",
                Status = "True",
                Reason = "NodeReady",
                LastTransitionTime = DateTime.UtcNow
            };

            _vnodes[vnode.Id] = vnode;
            return vnode;
        }

        public Task<VNode> GetVNodeAsync(string vnodeId, CancellationToken cancellation = default)
        {
            if (!_vnodes.TryGetValue(vnodeId, out var vnode))
            {
                throw new KeyNotFoundException($"vNode not found: {vnodeId}");
            }
            return Task.FromResult(vnode);
        }

        public Task<List<VNode>> ListVNodesAsync(string? tenantId = null, CancellationToken cancellation = default)
        {
            var vnodes = _vnodes.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(tenantId))
            {
                vnodes = vnodes.Where(v => v.TenantId == tenantId);
            }
            return Task.FromResult(vnodes.ToList());
        }

        public async Task<VNode> UpdateVNodeAsync(string vnodeId, VNodeConfig config, CancellationToken cancellation = default)
        {
            if (!_vnodes.TryGetValue(vnodeId, out var vnode))
            {
                throw new KeyNotFoundException($"vNode not found: {vnodeId}");
            }

            vnode.Capacity = config.Resources;
            vnode.Security = config.Security;

            await Task.Delay(100, cancellation);
            return vnode;
        }

        public async Task DeleteVNodeAsync(string vnodeId, CancellationToken cancellation = default)
        {
            if (!_vnodes.TryGetValue(vnodeId, out var vnode))
            {
                throw new KeyNotFoundException($"vNode not found: {vnodeId}");
            }

            vnode.Status = VNodeStatus.Deleted;
            _vnodes.TryRemove(vnodeId, out _);

            _logger.LogInformation("Deleted vNode: {VNodeId}", vnodeId);
            await Task.Delay(100, cancellation);
        }

        public async Task DrainVNodeAsync(string vnodeId, CancellationToken cancellation = default)
        {
            if (!_vnodes.TryGetValue(vnodeId, out var vnode))
            {
                throw new KeyNotFoundException($"vNode not found: {vnodeId}");
            }

            vnode.Status = VNodeStatus.Draining;
            _logger.LogInformation("Draining vNode: {VNodeId}", vnodeId);

            await Task.Delay(500, cancellation);

            vnode.RunningPods = 0;
            vnode.Status = VNodeStatus.Ready;
        }

        #endregion

        #region Policy Template Management

        public async Task<PolicyTemplate> CreatePolicyTemplateAsync(PolicyTemplateConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating policy template: {TemplateName}", config.Name);

            var template = new PolicyTemplate
            {
                Id = GenerateId("pt"),
                Name = config.Name,
                Description = config.Description,
                Category = config.Category,
                TargetKinds = config.TargetKinds,
                RegoPolicy = config.RegoPolicy,
                ComplianceFrameworks = config.ComplianceFrameworks,
                Severity = config.Severity,
                CreatedAt = DateTime.UtcNow
            };

            // Generate ConstraintTemplate YAML for Gatekeeper
            var templateYaml = GenerateConstraintTemplateYaml(config);
            _logger.LogDebug("Generated ConstraintTemplate YAML:\n{Yaml}", templateYaml);

            await Task.Delay(100, cancellation);

            _policyTemplates[template.Id] = template;
            return template;
        }

        public Task<PolicyTemplate> GetPolicyTemplateAsync(string templateId, CancellationToken cancellation = default)
        {
            if (!_policyTemplates.TryGetValue(templateId, out var template))
            {
                throw new KeyNotFoundException($"Policy template not found: {templateId}");
            }
            return Task.FromResult(template);
        }

        public Task<List<PolicyTemplate>> ListPolicyTemplatesAsync(PolicyCategory? category = null, CancellationToken cancellation = default)
        {
            var templates = _policyTemplates.Values.AsEnumerable();
            if (category.HasValue)
            {
                templates = templates.Where(t => t.Category == category.Value);
            }
            return Task.FromResult(templates.ToList());
        }

        public async Task DeletePolicyTemplateAsync(string templateId, CancellationToken cancellation = default)
        {
            _policyTemplates.TryRemove(templateId, out _);
            await Task.Delay(50, cancellation);
        }

        public Task<List<PolicyTemplate>> GetBuiltInPolicyTemplatesAsync(CancellationToken cancellation = default)
        {
            return Task.FromResult(_policyTemplates.Values
                .Where(t => t.Name.StartsWith("builtin-"))
                .ToList());
        }

        #endregion

        #region Constraint Management

        public async Task<Constraint> CreateConstraintAsync(ConstraintConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating constraint: {ConstraintName} from template: {TemplateName}",
                config.Name, config.TemplateName);

            var constraint = new Constraint
            {
                Id = GenerateId("c"),
                Name = config.Name,
                TemplateName = config.TemplateName,
                EnforcementMode = config.EnforcementMode,
                Match = config.Match,
                Parameters = config.Parameters,
                CreatedAt = DateTime.UtcNow
            };

            // Generate Constraint YAML
            var constraintYaml = GenerateConstraintYaml(config);
            _logger.LogDebug("Generated Constraint YAML:\n{Yaml}", constraintYaml);

            await Task.Delay(100, cancellation);

            _constraints[constraint.Id] = constraint;

            // Update template's active constraint count
            var template = _policyTemplates.Values.FirstOrDefault(t => t.Name == config.TemplateName);
            if (template != null)
            {
                template.ActiveConstraints++;
            }

            return constraint;
        }

        public Task<Constraint> GetConstraintAsync(string constraintId, CancellationToken cancellation = default)
        {
            if (!_constraints.TryGetValue(constraintId, out var constraint))
            {
                throw new KeyNotFoundException($"Constraint not found: {constraintId}");
            }
            return Task.FromResult(constraint);
        }

        public Task<List<Constraint>> ListConstraintsAsync(string? templateName = null, CancellationToken cancellation = default)
        {
            var constraints = _constraints.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(templateName))
            {
                constraints = constraints.Where(c => c.TemplateName == templateName);
            }
            return Task.FromResult(constraints.ToList());
        }

        public async Task<Constraint> UpdateConstraintAsync(string constraintId, ConstraintConfig config, CancellationToken cancellation = default)
        {
            if (!_constraints.TryGetValue(constraintId, out var constraint))
            {
                throw new KeyNotFoundException($"Constraint not found: {constraintId}");
            }

            constraint.EnforcementMode = config.EnforcementMode;
            constraint.Match = config.Match;
            constraint.Parameters = config.Parameters;

            await Task.Delay(50, cancellation);
            return constraint;
        }

        public async Task DeleteConstraintAsync(string constraintId, CancellationToken cancellation = default)
        {
            if (_constraints.TryRemove(constraintId, out var constraint))
            {
                var template = _policyTemplates.Values.FirstOrDefault(t => t.Name == constraint.TemplateName);
                if (template != null)
                {
                    template.ActiveConstraints--;
                }
            }
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Policy Violations

        public Task<List<PolicyViolation>> GetViolationsAsync(string? tenantId = null, SecuritySeverity? minSeverity = null, CancellationToken cancellation = default)
        {
            var violations = _violations.Values.AsEnumerable();

            if (minSeverity.HasValue)
            {
                violations = violations.Where(v => v.Severity <= minSeverity.Value);
            }

            return Task.FromResult(violations.OrderByDescending(v => v.DetectedAt).ToList());
        }

        public Task<PolicyViolation> GetViolationAsync(string violationId, CancellationToken cancellation = default)
        {
            if (!_violations.TryGetValue(violationId, out var violation))
            {
                throw new KeyNotFoundException($"Violation not found: {violationId}");
            }
            return Task.FromResult(violation);
        }

        public async Task ResolveViolationAsync(string violationId, string resolution, CancellationToken cancellation = default)
        {
            if (!_violations.TryGetValue(violationId, out var violation))
            {
                throw new KeyNotFoundException($"Violation not found: {violationId}");
            }

            violation.Resolved = true;
            _logger.LogInformation("Resolved violation: {ViolationId} with resolution: {Resolution}",
                violationId, resolution);

            await Task.Delay(50, cancellation);
        }

        public Task<int> GetViolationCountAsync(string tenantId, CancellationToken cancellation = default)
        {
            var count = _violations.Values.Count(v => !v.Resolved);
            return Task.FromResult(count);
        }

        #endregion

        #region Cost Allocation & Tracking

        public async Task<CostAllocationConfig> ConfigureCostAllocationAsync(CostAllocationConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Configuring cost allocation for tenant: {TenantId}", config.TenantId);
            _costConfigs[config.TenantId] = config;
            await Task.Delay(50, cancellation);
            return config;
        }

        public Task<TenantCostReport> GetCostReportAsync(string tenantId, DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            var report = new TenantCostReport
            {
                TenantId = tenantId,
                PeriodStart = start,
                PeriodEnd = end,
                Breakdown = new CostBreakdown
                {
                    CpuCost = 150.00m,
                    MemoryCost = 75.00m,
                    StorageCost = 25.00m,
                    GPUCost = 500.00m,
                    NetworkCost = 10.00m
                },
                ByNamespace = new List<NamespaceCost>
                {
                    new NamespaceCost { Namespace = "production", Cost = 450.00m, CpuCoreHours = 2000, MemoryGiBHours = 4000, GPUHours = 100 },
                    new NamespaceCost { Namespace = "staging", Cost = 200.00m, CpuCoreHours = 800, MemoryGiBHours = 1600, GPUHours = 40 },
                    new NamespaceCost { Namespace = "development", Cost = 110.00m, CpuCoreHours = 400, MemoryGiBHours = 800, GPUHours = 20 }
                },
                TopWorkloads = new List<WorkloadCost>
                {
                    new WorkloadCost { Name = "ml-training-job", Namespace = "production", Kind = "Job", Cost = 300.00m },
                    new WorkloadCost { Name = "inference-service", Namespace = "production", Kind = "Deployment", Cost = 150.00m }
                }
            };

            report.TotalCost = report.Breakdown.CpuCost + report.Breakdown.MemoryCost +
                              report.Breakdown.StorageCost + report.Breakdown.GPUCost + report.Breakdown.NetworkCost;

            if (_costConfigs.TryGetValue(tenantId, out var config))
            {
                report.Budget = config.MonthlyBudget;
                report.BudgetUsedPercent = config.MonthlyBudget > 0
                    ? (report.TotalCost / config.MonthlyBudget) * 100
                    : 0;
            }

            return Task.FromResult(report);
        }

        public async Task<List<TenantCostReport>> GetAllTenantCostReportsAsync(DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            var reports = new List<TenantCostReport>();
            foreach (var tenantId in _costConfigs.Keys)
            {
                reports.Add(await GetCostReportAsync(tenantId, start, end, cancellation));
            }
            return reports;
        }

        public Task<decimal> GetCurrentSpendAsync(string tenantId, CancellationToken cancellation = default)
        {
            // Simulate current month spend
            return Task.FromResult(450.00m);
        }

        #endregion

        #region Security Posture

        public Task<SecurityPostureScore> GetSecurityPostureAsync(string tenantId, CancellationToken cancellation = default)
        {
            var score = CalculateSecurityPosture(tenantId);
            return Task.FromResult(score);
        }

        public async Task<SecurityPostureScore> ScanSecurityPostureAsync(string tenantId, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Scanning security posture for tenant: {TenantId}", tenantId);

            // Simulate scanning
            await Task.Delay(500, cancellation);

            // Generate some violations for demonstration
            var violation = new PolicyViolation
            {
                Id = GenerateId("v"),
                ConstraintName = "require-resource-limits",
                ResourceKind = "Deployment",
                ResourceName = "web-app",
                Namespace = "production",
                Message = "Container 'app' does not have resource limits set",
                Severity = SecuritySeverity.Medium,
                EnforcementAction = PolicyEnforcementMode.Warn,
                DetectedAt = DateTime.UtcNow
            };
            _violations[violation.Id] = violation;

            return CalculateSecurityPosture(tenantId);
        }

        public Task<List<SecurityRecommendation>> GetSecurityRecommendationsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var recommendations = new List<SecurityRecommendation>
            {
                new SecurityRecommendation
                {
                    Id = GenerateId("rec"),
                    Title = "Enable Pod Security Standards",
                    Description = "Enforce 'restricted' Pod Security Standard for production namespaces",
                    Severity = SecuritySeverity.High,
                    RemediationSteps = "Apply PodSecurityPolicy or use Pod Security Admission with 'restricted' profile",
                    ImpactOnScore = 15.0
                },
                new SecurityRecommendation
                {
                    Id = GenerateId("rec"),
                    Title = "Configure Network Policies",
                    Description = "Implement default-deny network policies for all namespaces",
                    Severity = SecuritySeverity.Medium,
                    RemediationSteps = "Create NetworkPolicy resources with default-deny ingress and egress rules",
                    ImpactOnScore = 10.0
                },
                new SecurityRecommendation
                {
                    Id = GenerateId("rec"),
                    Title = "Enable Container Image Scanning",
                    Description = "Scan container images for vulnerabilities before deployment",
                    Severity = SecuritySeverity.High,
                    RemediationSteps = "Integrate Trivy or Clair with your CI/CD pipeline",
                    ImpactOnScore = 12.0
                }
            };

            return Task.FromResult(recommendations);
        }

        public async Task ConfigureSecurityPostureAsync(SecurityPostureConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Configuring security posture for tenant: {TenantId}", config.TenantId);
            _securityConfigs[config.TenantId] = config;
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Private Helper Methods

        private string GenerateId(string prefix)
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return $"{prefix}-{Convert.ToHexString(bytes).ToLower()}";
        }

        private void InitializeBuiltInPolicies()
        {
            // Security policies
            var securityPolicies = new[]
            {
                new PolicyTemplate
                {
                    Id = GenerateId("pt"),
                    Name = "builtin-require-non-root",
                    Description = "Require containers to run as non-root user",
                    Category = PolicyCategory.Security,
                    TargetKinds = new List<string> { "Pod" },
                    Severity = SecuritySeverity.High,
                    ComplianceFrameworks = new List<ComplianceFramework> { ComplianceFramework.CIS_Benchmark, ComplianceFramework.SOC2 },
                    CreatedAt = DateTime.UtcNow
                },
                new PolicyTemplate
                {
                    Id = GenerateId("pt"),
                    Name = "builtin-block-privileged",
                    Description = "Block privileged containers",
                    Category = PolicyCategory.Security,
                    TargetKinds = new List<string> { "Pod" },
                    Severity = SecuritySeverity.Critical,
                    ComplianceFrameworks = new List<ComplianceFramework> { ComplianceFramework.CIS_Benchmark, ComplianceFramework.PCI_DSS },
                    CreatedAt = DateTime.UtcNow
                },
                new PolicyTemplate
                {
                    Id = GenerateId("pt"),
                    Name = "builtin-require-resource-limits",
                    Description = "Require CPU and memory limits on containers",
                    Category = PolicyCategory.ResourceManagement,
                    TargetKinds = new List<string> { "Pod" },
                    Severity = SecuritySeverity.Medium,
                    CreatedAt = DateTime.UtcNow
                },
                new PolicyTemplate
                {
                    Id = GenerateId("pt"),
                    Name = "builtin-allowed-repos",
                    Description = "Restrict container images to allowed repositories",
                    Category = PolicyCategory.Security,
                    TargetKinds = new List<string> { "Pod" },
                    Severity = SecuritySeverity.High,
                    ComplianceFrameworks = new List<ComplianceFramework> { ComplianceFramework.SOC2, ComplianceFramework.HIPAA },
                    CreatedAt = DateTime.UtcNow
                },
                new PolicyTemplate
                {
                    Id = GenerateId("pt"),
                    Name = "builtin-require-labels",
                    Description = "Require specific labels on resources",
                    Category = PolicyCategory.Governance,
                    TargetKinds = new List<string> { "Pod", "Deployment", "Service" },
                    Severity = SecuritySeverity.Low,
                    CreatedAt = DateTime.UtcNow
                }
            };

            foreach (var policy in securityPolicies)
            {
                _policyTemplates[policy.Id] = policy;
            }
        }

        private SecurityPostureScore CalculateSecurityPosture(string tenantId)
        {
            var violations = _violations.Values.Where(v => !v.Resolved).ToList();

            var score = new SecurityPostureScore
            {
                TenantId = tenantId,
                CriticalViolations = violations.Count(v => v.Severity == SecuritySeverity.Critical),
                HighViolations = violations.Count(v => v.Severity == SecuritySeverity.High),
                MediumViolations = violations.Count(v => v.Severity == SecuritySeverity.Medium),
                LowViolations = violations.Count(v => v.Severity == SecuritySeverity.Low),
                CalculatedAt = DateTime.UtcNow
            };

            // Calculate overall score (100 - penalties)
            var penalty = score.CriticalViolations * 25 +
                         score.HighViolations * 15 +
                         score.MediumViolations * 5 +
                         score.LowViolations * 1;
            score.OverallScore = Math.Max(0, 100 - penalty);

            score.Grade = score.OverallScore switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };

            // Category scores
            score.CategoryScores = new Dictionary<PolicyCategory, double>
            {
                [PolicyCategory.Security] = Math.Max(0, 100 - (score.CriticalViolations + score.HighViolations) * 10),
                [PolicyCategory.ResourceManagement] = 85,
                [PolicyCategory.Networking] = 90,
                [PolicyCategory.Compliance] = 80,
                [PolicyCategory.Governance] = 95
            };

            // Compliance status
            score.ComplianceStatus = new Dictionary<ComplianceFramework, ComplianceStatus>
            {
                [ComplianceFramework.SOC2] = new ComplianceStatus { IsCompliant = score.OverallScore >= 80, CompliancePercent = score.OverallScore, PassedControls = 45, FailedControls = 5, TotalControls = 50 },
                [ComplianceFramework.CIS_Benchmark] = new ComplianceStatus { IsCompliant = score.OverallScore >= 70, CompliancePercent = score.OverallScore - 5, PassedControls = 85, FailedControls = 15, TotalControls = 100 }
            };

            return score;
        }

        private string GenerateVNodeYaml(VNodeConfig config)
        {
            return $@"# vNode configuration for tenant isolation
apiVersion: vcluster.loft.sh/v1
kind: VNode
metadata:
  name: {config.Name}
  labels:
    tenant: {config.TenantId}
spec:
  physicalNode: {config.PhysicalNodeName}
  capacity:
    cpu: ""{config.Resources.CpuCapacity}""
    memory: {config.Resources.MemoryCapacity}
    ephemeral-storage: {config.Resources.EphemeralStorageCapacity}
    pods: ""{config.Resources.MaxPods}""
  security:
    userNamespaceIsolation: {config.Security.EnableUserNamespaceIsolation.ToString().ToLower()}
    seccompDefault: {config.Security.EnableSeccompDefault.ToString().ToLower()}
    podSecurityStandard: {config.Security.PodSecurityStandard}";
        }

        private string GenerateConstraintTemplateYaml(PolicyTemplateConfig config)
        {
            return $@"apiVersion: templates.gatekeeper.sh/v1
kind: ConstraintTemplate
metadata:
  name: {config.Name.ToLower().Replace("-", "")}
  annotations:
    description: {config.Description}
spec:
  crd:
    spec:
      names:
        kind: {config.Name.Replace("-", "")}
      validation:
        openAPIV3Schema:
          type: object
          properties: {{}}
  targets:
    - target: admission.k8s.gatekeeper.sh
      rego: |
        {config.RegoPolicy}";
        }

        private string GenerateConstraintYaml(ConstraintConfig config)
        {
            var enforcementAction = config.EnforcementMode switch
            {
                PolicyEnforcementMode.Audit => "dryrun",
                PolicyEnforcementMode.Warn => "warn",
                _ => "deny"
            };

            return $@"apiVersion: constraints.gatekeeper.sh/v1beta1
kind: {config.TemplateName.Replace("-", "")}
metadata:
  name: {config.Name}
spec:
  enforcementAction: {enforcementAction}
  match:
    kinds:
{string.Join("\n", config.Match.Kinds.Select(k => $"      - apiGroups: [\"\"]\n        kinds: [\"{k}\"]"))}
    excludedNamespaces: [{string.Join(", ", config.Match.ExcludedNamespaces.Select(n => $"\"{n}\""))}]";
        }

        #endregion
    }

    #endregion
}
