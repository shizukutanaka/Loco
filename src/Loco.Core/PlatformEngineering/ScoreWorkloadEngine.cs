// =============================================================================
// CNCF Score Workload Engine
// Platform-agnostic workload specification engine
// Based on: CNCF Score Spec, Humanitec Score Implementation
// Research: https://score.dev, https://github.com/score-spec/spec
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.PlatformEngineering
{
    #region Enums

    /// <summary>
    /// Score specification version
    /// </summary>
    public enum ScoreSpecVersion
    {
        V1Alpha1,
        V1Beta1,
        V1,
        V2Alpha1
    }

    /// <summary>
    /// Target platform for Score translation
    /// </summary>
    public enum ScoreTargetPlatform
    {
        Kubernetes,
        DockerCompose,
        Helm,
        HumanitecDelta,
        Nomad,
        CloudRun,
        AzureContainerApps,
        AWSECS,
        Custom
    }

    /// <summary>
    /// Resource type in Score specification
    /// </summary>
    public enum ScoreResourceType
    {
        PostgreSQL,
        MySQL,
        Redis,
        MongoDB,
        Kafka,
        RabbitMQ,
        S3Bucket,
        DNS,
        Volume,
        Secret,
        ConfigMap,
        Service,
        Route,
        Custom
    }

    /// <summary>
    /// Probe type for health checks
    /// </summary>
    public enum ScoreProbeType
    {
        HTTP,
        TCP,
        GRPC,
        Command
    }

    /// <summary>
    /// Volume mount type
    /// </summary>
    public enum ScoreVolumeType
    {
        Ephemeral,
        Persistent,
        ConfigMap,
        Secret,
        EmptyDir
    }

    /// <summary>
    /// Workload lifecycle state
    /// </summary>
    public enum WorkloadState
    {
        Draft,
        Validated,
        Translated,
        Deployed,
        Running,
        Suspended,
        Failed
    }

    #endregion

    #region Core Score Types

    /// <summary>
    /// Score workload specification - the main entity
    /// </summary>
    public class ScoreWorkload
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Score API version (e.g., "score.dev/v1b1")
        /// </summary>
        public string ApiVersion { get; set; } = "score.dev/v1b1";

        /// <summary>
        /// Metadata about the workload
        /// </summary>
        public ScoreMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Container specifications
        /// </summary>
        public Dictionary<string, ScoreContainer> Containers { get; set; } = new();

        /// <summary>
        /// Service endpoints
        /// </summary>
        public Dictionary<string, ScoreService> Service { get; set; } = new();

        /// <summary>
        /// Resource dependencies
        /// </summary>
        public Dictionary<string, ScoreResource> Resources { get; set; } = new();

        /// <summary>
        /// Platform-specific extensions
        /// </summary>
        public Dictionary<string, object> Extensions { get; set; } = new();

        /// <summary>
        /// Current workload state
        /// </summary>
        public WorkloadState State { get; set; } = WorkloadState.Draft;

        /// <summary>
        /// Validation results
        /// </summary>
        public List<ValidationIssue> ValidationIssues { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Workload metadata
    /// </summary>
    public class ScoreMetadata
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();

        /// <summary>
        /// Team ownership
        /// </summary>
        public string Team { get; set; } = string.Empty;

        /// <summary>
        /// Application grouping
        /// </summary>
        public string Application { get; set; } = string.Empty;

        /// <summary>
        /// Environment target
        /// </summary>
        public string Environment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Container specification in Score
    /// </summary>
    public class ScoreContainer
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Container image reference
        /// </summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// Command override
        /// </summary>
        public List<string> Command { get; set; } = new();

        /// <summary>
        /// Arguments to command
        /// </summary>
        public List<string> Args { get; set; } = new();

        /// <summary>
        /// Environment variables with placeholder support
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>
        /// Resource requirements
        /// </summary>
        public ScoreResourceRequirements Resources { get; set; } = new();

        /// <summary>
        /// Volume mounts
        /// </summary>
        public List<ScoreVolumeMount> Volumes { get; set; } = new();

        /// <summary>
        /// Readiness probe
        /// </summary>
        public ScoreProbe? ReadinessProbe { get; set; }

        /// <summary>
        /// Liveness probe
        /// </summary>
        public ScoreProbe? LivenessProbe { get; set; }

        /// <summary>
        /// Startup probe
        /// </summary>
        public ScoreProbe? StartupProbe { get; set; }

        /// <summary>
        /// Files to inject
        /// </summary>
        public Dictionary<string, ScoreFile> Files { get; set; } = new();
    }

    /// <summary>
    /// Resource requirements specification
    /// </summary>
    public class ScoreResourceRequirements
    {
        /// <summary>
        /// Limits (e.g., "500m" for CPU, "256Mi" for memory)
        /// </summary>
        public ScoreResourceSpec? Limits { get; set; }

        /// <summary>
        /// Requests (guaranteed resources)
        /// </summary>
        public ScoreResourceSpec? Requests { get; set; }
    }

    /// <summary>
    /// Resource specification
    /// </summary>
    public class ScoreResourceSpec
    {
        public string? CPU { get; set; }
        public string? Memory { get; set; }
        public string? EphemeralStorage { get; set; }
    }

    /// <summary>
    /// Volume mount specification
    /// </summary>
    public class ScoreVolumeMount
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public ScoreVolumeType Type { get; set; } = ScoreVolumeType.Ephemeral;
        public bool ReadOnly { get; set; }
        public string? Size { get; set; }
        public string? StorageClass { get; set; }
    }

    /// <summary>
    /// Probe specification
    /// </summary>
    public class ScoreProbe
    {
        public ScoreProbeType Type { get; set; } = ScoreProbeType.HTTP;

        /// <summary>
        /// HTTP probe configuration
        /// </summary>
        public ScoreHttpProbe? HttpGet { get; set; }

        /// <summary>
        /// TCP probe configuration
        /// </summary>
        public ScoreTcpProbe? TcpSocket { get; set; }

        /// <summary>
        /// GRPC probe configuration
        /// </summary>
        public ScoreGrpcProbe? Grpc { get; set; }

        /// <summary>
        /// Command probe
        /// </summary>
        public List<string>? Command { get; set; }

        public int InitialDelaySeconds { get; set; } = 0;
        public int PeriodSeconds { get; set; } = 10;
        public int TimeoutSeconds { get; set; } = 1;
        public int SuccessThreshold { get; set; } = 1;
        public int FailureThreshold { get; set; } = 3;
    }

    /// <summary>
    /// HTTP probe configuration
    /// </summary>
    public class ScoreHttpProbe
    {
        public string Path { get; set; } = "/";
        public int Port { get; set; } = 80;
        public string Scheme { get; set; } = "HTTP";
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    /// <summary>
    /// TCP probe configuration
    /// </summary>
    public class ScoreTcpProbe
    {
        public int Port { get; set; }
    }

    /// <summary>
    /// GRPC probe configuration
    /// </summary>
    public class ScoreGrpcProbe
    {
        public int Port { get; set; }
        public string? Service { get; set; }
    }

    /// <summary>
    /// File to inject into container
    /// </summary>
    public class ScoreFile
    {
        public string Target { get; set; } = string.Empty;
        public int? Mode { get; set; }
        public string? Content { get; set; }
        public string? Source { get; set; }
        public bool? NoExpand { get; set; }
    }

    /// <summary>
    /// Service specification (network endpoints)
    /// </summary>
    public class ScoreService
    {
        /// <summary>
        /// Ports exposed by this service
        /// </summary>
        public Dictionary<string, ScoreServicePort> Ports { get; set; } = new();
    }

    /// <summary>
    /// Service port specification
    /// </summary>
    public class ScoreServicePort
    {
        public int Port { get; set; }
        public int? TargetPort { get; set; }
        public string Protocol { get; set; } = "TCP";
    }

    /// <summary>
    /// Resource dependency specification
    /// </summary>
    public class ScoreResource
    {
        public ScoreResourceType Type { get; set; }

        /// <summary>
        /// Resource class (e.g., "default", "production", "development")
        /// </summary>
        public string? Class { get; set; }

        /// <summary>
        /// Resource ID for referencing existing resources
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Metadata about the resource
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Resource-specific parameters
        /// </summary>
        public Dictionary<string, object> Params { get; set; } = new();
    }

    #endregion

    #region Translation Types

    /// <summary>
    /// Translation result from Score to target platform
    /// </summary>
    public class TranslationResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkloadId { get; set; } = string.Empty;
        public ScoreTargetPlatform TargetPlatform { get; set; }

        /// <summary>
        /// Generated manifests by kind/name
        /// </summary>
        public Dictionary<string, GeneratedManifest> Manifests { get; set; } = new();

        /// <summary>
        /// Combined output (e.g., multi-document YAML)
        /// </summary>
        public string CombinedOutput { get; set; } = string.Empty;

        /// <summary>
        /// Resource outputs (connection strings, endpoints, etc.)
        /// </summary>
        public Dictionary<string, ResourceOutput> ResourceOutputs { get; set; } = new();

        /// <summary>
        /// Placeholder references that need resolution
        /// </summary>
        public List<PlaceholderReference> Placeholders { get; set; } = new();

        /// <summary>
        /// Warnings generated during translation
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Generated manifest output
    /// </summary>
    public class GeneratedManifest
    {
        public string Kind { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = "yaml";
    }

    /// <summary>
    /// Resource output value
    /// </summary>
    public class ResourceOutput
    {
        public string ResourceName { get; set; } = string.Empty;
        public string OutputKey { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsSecret { get; set; }
    }

    /// <summary>
    /// Placeholder reference in Score spec
    /// </summary>
    public class PlaceholderReference
    {
        /// <summary>
        /// Placeholder string (e.g., "${resources.db.host}")
        /// </summary>
        public string Placeholder { get; set; } = string.Empty;

        /// <summary>
        /// Referenced resource name
        /// </summary>
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Output key being referenced
        /// </summary>
        public string OutputKey { get; set; } = string.Empty;

        /// <summary>
        /// Location in the spec
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Resolved value (if available)
        /// </summary>
        public string? ResolvedValue { get; set; }
    }

    /// <summary>
    /// Translation context with provisioner configuration
    /// </summary>
    public class TranslationContext
    {
        public ScoreTargetPlatform TargetPlatform { get; set; } = ScoreTargetPlatform.Kubernetes;

        /// <summary>
        /// Namespace for Kubernetes targets
        /// </summary>
        public string? Namespace { get; set; }

        /// <summary>
        /// Resource provisioners by type
        /// </summary>
        public Dictionary<ScoreResourceType, ResourceProvisioner> Provisioners { get; set; } = new();

        /// <summary>
        /// Default resource classes
        /// </summary>
        public Dictionary<ScoreResourceType, string> DefaultClasses { get; set; } = new();

        /// <summary>
        /// Resolved resource outputs
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ResourceOutputs { get; set; } = new();

        /// <summary>
        /// Platform-specific extensions
        /// </summary>
        public Dictionary<string, object> PlatformExtensions { get; set; } = new();
    }

    /// <summary>
    /// Resource provisioner configuration
    /// </summary>
    public class ResourceProvisioner
    {
        public string Name { get; set; } = string.Empty;
        public ScoreResourceType ResourceType { get; set; }

        /// <summary>
        /// Provisioner type (e.g., "helm", "terraform", "crossplane")
        /// </summary>
        public string ProvisionerType { get; set; } = string.Empty;

        /// <summary>
        /// Output mapping (resource output -> actual output)
        /// </summary>
        public Dictionary<string, string> OutputMappings { get; set; } = new();

        /// <summary>
        /// Default parameters
        /// </summary>
        public Dictionary<string, object> DefaultParams { get; set; } = new();

        /// <summary>
        /// Class configurations
        /// </summary>
        public Dictionary<string, ProvisionerClass> Classes { get; set; } = new();
    }

    /// <summary>
    /// Provisioner class configuration
    /// </summary>
    public class ProvisionerClass
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Params { get; set; } = new();
        public string? Template { get; set; }
    }

    #endregion

    #region Validation Types

    /// <summary>
    /// Validation issue
    /// </summary>
    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Suggestion { get; set; }
    }

    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);
        public List<ValidationIssue> Issues { get; set; } = new();
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Extension Types

    /// <summary>
    /// Score extension definition
    /// </summary>
    public class ScoreExtension
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Extension URI (e.g., "score.humanitec.io/v1b1")
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// JSON schema for extension validation
        /// </summary>
        public string? Schema { get; set; }

        /// <summary>
        /// Description of the extension
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Target platforms this extension applies to
        /// </summary>
        public List<ScoreTargetPlatform> TargetPlatforms { get; set; } = new();

        /// <summary>
        /// Extension translator function name
        /// </summary>
        public string? TranslatorFunction { get; set; }
    }

    /// <summary>
    /// Humanitec-specific extension
    /// </summary>
    public class HumanitecExtension
    {
        /// <summary>
        /// Runtime configuration
        /// </summary>
        public HumanitecRuntime? Runtime { get; set; }

        /// <summary>
        /// Ingress configuration
        /// </summary>
        public HumanitecIngress? Ingress { get; set; }

        /// <summary>
        /// Service account configuration
        /// </summary>
        public string? ServiceAccount { get; set; }
    }

    public class HumanitecRuntime
    {
        public string? Class { get; set; }
        public int? Replicas { get; set; }
    }

    public class HumanitecIngress
    {
        public List<HumanitecIngressRule> Rules { get; set; } = new();
    }

    public class HumanitecIngressRule
    {
        public string Host { get; set; } = string.Empty;
        public string Path { get; set; } = "/";
        public string ServicePort { get; set; } = string.Empty;
    }

    #endregion

    #region Template Types

    /// <summary>
    /// Score workload template
    /// </summary>
    public class ScoreTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Base workload specification
        /// </summary>
        public ScoreWorkload BaseWorkload { get; set; } = new();

        /// <summary>
        /// Template parameters
        /// </summary>
        public List<TemplateParameter> Parameters { get; set; } = new();

        /// <summary>
        /// Resource class defaults by type
        /// </summary>
        public Dictionary<ScoreResourceType, string> ResourceClassDefaults { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;
    }

    /// <summary>
    /// Template parameter definition
    /// </summary>
    public class TemplateParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public bool Required { get; set; }
        public object? Default { get; set; }
        public List<object>? Enum { get; set; }
        public string? Pattern { get; set; }
    }

    #endregion

    #region Interfaces

    /// <summary>
    /// CNCF Score Workload Engine interface
    /// Provides platform-agnostic workload specification management
    /// </summary>
    public interface IScoreWorkloadEngine
    {
        /// <summary>
        /// Create a new Score workload specification
        /// </summary>
        Task<ScoreWorkload> CreateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get workload by ID
        /// </summary>
        Task<ScoreWorkload?> GetWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update existing workload
        /// </summary>
        Task<ScoreWorkload> UpdateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default);

        /// <summary>
        /// Delete workload
        /// </summary>
        Task<bool> DeleteWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Validate workload against Score schema
        /// </summary>
        Task<ValidationResult> ValidateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default);

        /// <summary>
        /// Translate workload to target platform
        /// </summary>
        Task<TranslationResult> TranslateAsync(
            string tenantId,
            string workloadId,
            TranslationContext context,
            CancellationToken cancellation = default);

        /// <summary>
        /// Parse Score YAML to workload object
        /// </summary>
        Task<ScoreWorkload> ParseScoreYamlAsync(
            string tenantId,
            string yamlContent,
            CancellationToken cancellation = default);

        /// <summary>
        /// Generate Score YAML from workload
        /// </summary>
        Task<string> GenerateScoreYamlAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Resolve placeholders in workload
        /// </summary>
        Task<ScoreWorkload> ResolvePlaceholdersAsync(
            string tenantId,
            string workloadId,
            TranslationContext context,
            CancellationToken cancellation = default);

        /// <summary>
        /// Register resource provisioner
        /// </summary>
        Task<ResourceProvisioner> RegisterProvisionerAsync(
            string tenantId,
            ResourceProvisioner provisioner,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get available provisioners
        /// </summary>
        Task<List<ResourceProvisioner>> GetProvisionersAsync(
            string tenantId,
            ScoreResourceType? resourceType = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Register Score extension
        /// </summary>
        Task<ScoreExtension> RegisterExtensionAsync(
            string tenantId,
            ScoreExtension extension,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create workload from template
        /// </summary>
        Task<ScoreWorkload> CreateFromTemplateAsync(
            string tenantId,
            string templateId,
            Dictionary<string, object> parameters,
            CancellationToken cancellation = default);

        /// <summary>
        /// List workload templates
        /// </summary>
        Task<List<ScoreTemplate>> ListTemplatesAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Compare two workloads for differences
        /// </summary>
        Task<WorkloadDiff> DiffWorkloadsAsync(
            string tenantId,
            string workloadId1,
            string workloadId2,
            CancellationToken cancellation = default);
    }

    #endregion

    #region Diff Types

    /// <summary>
    /// Workload difference result
    /// </summary>
    public class WorkloadDiff
    {
        public string WorkloadId1 { get; set; } = string.Empty;
        public string WorkloadId2 { get; set; } = string.Empty;
        public List<DiffEntry> Differences { get; set; } = new();
        public bool HasDifferences => Differences.Any();
    }

    /// <summary>
    /// Individual difference entry
    /// </summary>
    public class DiffEntry
    {
        public DiffOperation Operation { get; set; }
        public string Path { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    public enum DiffOperation
    {
        Add,
        Remove,
        Modify
    }

    #endregion

    #region Implementation

    /// <summary>
    /// CNCF Score Workload Engine implementation
    /// </summary>
    public class ScoreWorkloadEngine : IScoreWorkloadEngine
    {
        private readonly ILogger<ScoreWorkloadEngine> _logger;
        private readonly Dictionary<string, Dictionary<string, ScoreWorkload>> _workloads = new();
        private readonly Dictionary<string, Dictionary<string, ResourceProvisioner>> _provisioners = new();
        private readonly Dictionary<string, Dictionary<string, ScoreExtension>> _extensions = new();
        private readonly Dictionary<string, Dictionary<string, ScoreTemplate>> _templates = new();

        // Placeholder regex for Score format: ${resources.name.output} or ${metadata.name}
        private static readonly Regex PlaceholderRegex = new(
            @"\$\{(?<path>[a-zA-Z0-9_.]+)\}",
            RegexOptions.Compiled);

        public ScoreWorkloadEngine(ILogger<ScoreWorkloadEngine> logger)
        {
            _logger = logger;
            InitializeDefaultProvisioners();
            InitializeDefaultTemplates();
            InitializeDefaultExtensions();
        }

        #region Workload CRUD

        public Task<ScoreWorkload> CreateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default)
        {
            if (!_workloads.ContainsKey(tenantId))
                _workloads[tenantId] = new();

            workload.Id = workload.Id ?? Guid.NewGuid().ToString();
            workload.TenantId = tenantId;
            workload.CreatedAt = DateTime.UtcNow;
            workload.State = WorkloadState.Draft;

            _workloads[tenantId][workload.Id] = workload;

            _logger.LogInformation(
                "Created Score workload {WorkloadId} for tenant {TenantId}",
                workload.Id, tenantId);

            return Task.FromResult(workload);
        }

        public Task<ScoreWorkload?> GetWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            if (_workloads.TryGetValue(tenantId, out var tenantWorkloads) &&
                tenantWorkloads.TryGetValue(workloadId, out var workload))
            {
                return Task.FromResult<ScoreWorkload?>(workload);
            }

            return Task.FromResult<ScoreWorkload?>(null);
        }

        public Task<ScoreWorkload> UpdateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default)
        {
            if (!_workloads.ContainsKey(tenantId))
                throw new InvalidOperationException($"Tenant {tenantId} not found");

            if (!_workloads[tenantId].ContainsKey(workload.Id))
                throw new InvalidOperationException($"Workload {workload.Id} not found");

            workload.UpdatedAt = DateTime.UtcNow;
            workload.State = WorkloadState.Draft; // Reset to draft on update
            _workloads[tenantId][workload.Id] = workload;

            _logger.LogInformation(
                "Updated Score workload {WorkloadId} for tenant {TenantId}",
                workload.Id, tenantId);

            return Task.FromResult(workload);
        }

        public Task<bool> DeleteWorkloadAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            if (_workloads.TryGetValue(tenantId, out var tenantWorkloads))
            {
                var removed = tenantWorkloads.Remove(workloadId);
                if (removed)
                {
                    _logger.LogInformation(
                        "Deleted Score workload {WorkloadId} for tenant {TenantId}",
                        workloadId, tenantId);
                }
                return Task.FromResult(removed);
            }

            return Task.FromResult(false);
        }

        #endregion

        #region Validation

        public Task<ValidationResult> ValidateWorkloadAsync(
            string tenantId,
            ScoreWorkload workload,
            CancellationToken cancellation = default)
        {
            var result = new ValidationResult();

            // Validate API version
            if (string.IsNullOrEmpty(workload.ApiVersion))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE001",
                    Message = "apiVersion is required",
                    Path = "apiVersion",
                    Suggestion = "Set apiVersion to 'score.dev/v1b1'"
                });
            }

            // Validate metadata
            if (string.IsNullOrEmpty(workload.Metadata.Name))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE002",
                    Message = "metadata.name is required",
                    Path = "metadata.name"
                });
            }

            // Validate containers
            if (!workload.Containers.Any())
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE003",
                    Message = "At least one container is required",
                    Path = "containers"
                });
            }

            foreach (var (containerName, container) in workload.Containers)
            {
                ValidateContainer(result, containerName, container);
            }

            // Validate resources
            foreach (var (resourceName, resource) in workload.Resources)
            {
                ValidateResource(result, resourceName, resource);
            }

            // Validate placeholder references
            ValidatePlaceholders(result, workload);

            // Update workload state
            if (result.IsValid)
            {
                workload.State = WorkloadState.Validated;
                workload.ValidationIssues.Clear();
            }
            else
            {
                workload.ValidationIssues = result.Issues;
            }

            _logger.LogInformation(
                "Validated workload {WorkloadId}: {IsValid} with {IssueCount} issues",
                workload.Id, result.IsValid, result.Issues.Count);

            return Task.FromResult(result);
        }

        private void ValidateContainer(ValidationResult result, string name, ScoreContainer container)
        {
            if (string.IsNullOrEmpty(container.Image))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE010",
                    Message = $"Container '{name}' requires an image",
                    Path = $"containers.{name}.image"
                });
            }

            // Validate resource requirements format
            if (container.Resources.Limits != null)
            {
                ValidateResourceSpec(result, $"containers.{name}.resources.limits", container.Resources.Limits);
            }
            if (container.Resources.Requests != null)
            {
                ValidateResourceSpec(result, $"containers.{name}.resources.requests", container.Resources.Requests);
            }

            // Validate probes
            if (container.ReadinessProbe != null)
            {
                ValidateProbe(result, $"containers.{name}.readinessProbe", container.ReadinessProbe);
            }
            if (container.LivenessProbe != null)
            {
                ValidateProbe(result, $"containers.{name}.livenessProbe", container.LivenessProbe);
            }
        }

        private void ValidateResourceSpec(ValidationResult result, string path, ScoreResourceSpec spec)
        {
            if (!string.IsNullOrEmpty(spec.CPU) && !IsValidQuantity(spec.CPU))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE011",
                    Message = $"Invalid CPU quantity format: {spec.CPU}",
                    Path = $"{path}.cpu",
                    Suggestion = "Use format like '500m' or '2'"
                });
            }

            if (!string.IsNullOrEmpty(spec.Memory) && !IsValidQuantity(spec.Memory))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE012",
                    Message = $"Invalid memory quantity format: {spec.Memory}",
                    Path = $"{path}.memory",
                    Suggestion = "Use format like '256Mi' or '1Gi'"
                });
            }
        }

        private void ValidateProbe(ValidationResult result, string path, ScoreProbe probe)
        {
            if (probe.Type == ScoreProbeType.HTTP && probe.HttpGet == null)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE020",
                    Message = "HTTP probe requires httpGet configuration",
                    Path = path
                });
            }

            if (probe.Type == ScoreProbeType.TCP && probe.TcpSocket == null)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SCORE021",
                    Message = "TCP probe requires tcpSocket configuration",
                    Path = path
                });
            }
        }

        private void ValidateResource(ValidationResult result, string name, ScoreResource resource)
        {
            // Validate resource type is supported
            if (resource.Type == ScoreResourceType.Custom && string.IsNullOrEmpty(resource.Class))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "SCORE030",
                    Message = $"Custom resource '{name}' should specify a class",
                    Path = $"resources.{name}.class"
                });
            }
        }

        private void ValidatePlaceholders(ValidationResult result, ScoreWorkload workload)
        {
            foreach (var (containerName, container) in workload.Containers)
            {
                foreach (var (varName, varValue) in container.Variables)
                {
                    var matches = PlaceholderRegex.Matches(varValue);
                    foreach (Match match in matches)
                    {
                        var path = match.Groups["path"].Value;
                        var parts = path.Split('.');

                        if (parts.Length >= 2 && parts[0] == "resources")
                        {
                            var resourceName = parts[1];
                            if (!workload.Resources.ContainsKey(resourceName))
                            {
                                result.Issues.Add(new ValidationIssue
                                {
                                    Severity = ValidationSeverity.Error,
                                    Code = "SCORE040",
                                    Message = $"Placeholder references undefined resource: {resourceName}",
                                    Path = $"containers.{containerName}.variables.{varName}",
                                    Suggestion = $"Define resource '{resourceName}' in the resources section"
                                });
                            }
                        }
                    }
                }
            }
        }

        private bool IsValidQuantity(string quantity)
        {
            // Kubernetes quantity format: number + suffix
            // CPU: 100m, 0.1, 1, 2
            // Memory: 128Mi, 1Gi, etc.
            return Regex.IsMatch(quantity, @"^\d+(\.\d+)?(m|Ki|Mi|Gi|Ti|Pi|Ei|k|M|G|T|P|E)?$");
        }

        #endregion

        #region Translation

        public async Task<TranslationResult> TranslateAsync(
            string tenantId,
            string workloadId,
            TranslationContext context,
            CancellationToken cancellation = default)
        {
            var workload = await GetWorkloadAsync(tenantId, workloadId, cancellation);
            if (workload == null)
                throw new InvalidOperationException($"Workload {workloadId} not found");

            // Validate first
            var validationResult = await ValidateWorkloadAsync(tenantId, workload, cancellation);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Workload validation failed: {string.Join(", ", validationResult.Issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message))}");
            }

            var result = new TranslationResult
            {
                WorkloadId = workloadId,
                TargetPlatform = context.TargetPlatform
            };

            // Extract placeholders
            result.Placeholders = ExtractPlaceholders(workload);

            // Resolve placeholders if context provides outputs
            var resolvedWorkload = await ResolvePlaceholdersAsync(tenantId, workloadId, context, cancellation);

            // Translate based on target platform
            result = context.TargetPlatform switch
            {
                ScoreTargetPlatform.Kubernetes => await TranslateToKubernetesAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.DockerCompose => await TranslateToDockerComposeAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.Helm => await TranslateToHelmAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.HumanitecDelta => await TranslateToHumanitecAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.Nomad => await TranslateToNomadAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.CloudRun => await TranslateToCloudRunAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.AzureContainerApps => await TranslateToAzureContainerAppsAsync(resolvedWorkload, context, result),
                ScoreTargetPlatform.AWSECS => await TranslateToECSAsync(resolvedWorkload, context, result),
                _ => throw new NotSupportedException($"Target platform {context.TargetPlatform} not supported")
            };

            // Update workload state
            workload.State = WorkloadState.Translated;

            _logger.LogInformation(
                "Translated workload {WorkloadId} to {Platform}: {ManifestCount} manifests",
                workloadId, context.TargetPlatform, result.Manifests.Count);

            return result;
        }

        private List<PlaceholderReference> ExtractPlaceholders(ScoreWorkload workload)
        {
            var placeholders = new List<PlaceholderReference>();

            foreach (var (containerName, container) in workload.Containers)
            {
                foreach (var (varName, varValue) in container.Variables)
                {
                    var matches = PlaceholderRegex.Matches(varValue);
                    foreach (Match match in matches)
                    {
                        var path = match.Groups["path"].Value;
                        var parts = path.Split('.');

                        if (parts.Length >= 3 && parts[0] == "resources")
                        {
                            placeholders.Add(new PlaceholderReference
                            {
                                Placeholder = match.Value,
                                ResourceName = parts[1],
                                OutputKey = string.Join(".", parts.Skip(2)),
                                Location = $"containers.{containerName}.variables.{varName}"
                            });
                        }
                    }
                }
            }

            return placeholders;
        }

        public async Task<ScoreWorkload> ResolvePlaceholdersAsync(
            string tenantId,
            string workloadId,
            TranslationContext context,
            CancellationToken cancellation = default)
        {
            var workload = await GetWorkloadAsync(tenantId, workloadId, cancellation);
            if (workload == null)
                throw new InvalidOperationException($"Workload {workloadId} not found");

            // Deep copy workload
            var resolved = JsonSerializer.Deserialize<ScoreWorkload>(
                JsonSerializer.Serialize(workload))!;

            foreach (var (containerName, container) in resolved.Containers)
            {
                var resolvedVariables = new Dictionary<string, string>();

                foreach (var (varName, varValue) in container.Variables)
                {
                    var resolvedValue = PlaceholderRegex.Replace(varValue, match =>
                    {
                        var path = match.Groups["path"].Value;
                        var parts = path.Split('.');

                        if (parts.Length >= 3 && parts[0] == "resources")
                        {
                            var resourceName = parts[1];
                            var outputKey = string.Join(".", parts.Skip(2));

                            if (context.ResourceOutputs.TryGetValue(resourceName, out var outputs) &&
                                outputs.TryGetValue(outputKey, out var output))
                            {
                                return output;
                            }
                        }
                        else if (parts[0] == "metadata" && parts.Length >= 2)
                        {
                            return parts[1] switch
                            {
                                "name" => resolved.Metadata.Name,
                                "team" => resolved.Metadata.Team,
                                "application" => resolved.Metadata.Application,
                                "environment" => resolved.Metadata.Environment,
                                _ => match.Value
                            };
                        }

                        // Keep unresolved placeholders
                        return match.Value;
                    });

                    resolvedVariables[varName] = resolvedValue;
                }

                container.Variables = resolvedVariables;
            }

            return resolved;
        }

        #region Platform-Specific Translations

        private Task<TranslationResult> TranslateToKubernetesAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var ns = context.Namespace ?? "default";

            // Generate Deployment
            var deployment = GenerateKubernetesDeployment(workload, ns);
            result.Manifests[$"Deployment/{workload.Metadata.Name}"] = deployment;

            // Generate Service if ports are defined
            if (workload.Service.Any())
            {
                var service = GenerateKubernetesService(workload, ns);
                result.Manifests[$"Service/{workload.Metadata.Name}"] = service;
            }

            // Generate ConfigMaps for non-secret variables
            var configMap = GenerateKubernetesConfigMap(workload, ns);
            if (configMap != null)
            {
                result.Manifests[$"ConfigMap/{workload.Metadata.Name}"] = configMap;
            }

            // Generate resource claims (PVCs, etc.)
            foreach (var (resourceName, resource) in workload.Resources)
            {
                var resourceManifests = GenerateKubernetesResourceManifests(resourceName, resource, ns, context);
                foreach (var manifest in resourceManifests)
                {
                    result.Manifests[manifest.Key] = manifest.Value;
                }
            }

            // Combine all manifests into YAML
            result.CombinedOutput = CombineManifestsToYaml(result.Manifests.Values);

            return Task.FromResult(result);
        }

        private GeneratedManifest GenerateKubernetesDeployment(ScoreWorkload workload, string ns)
        {
            var containers = new List<string>();
            foreach (var (name, container) in workload.Containers)
            {
                containers.Add(GenerateKubernetesContainer(name, container));
            }

            var deploymentYaml = $@"apiVersion: apps/v1
kind: Deployment
metadata:
  name: {workload.Metadata.Name}
  namespace: {ns}
  labels:
    app.kubernetes.io/name: {workload.Metadata.Name}
    app.kubernetes.io/instance: {workload.Metadata.Name}
{GenerateLabels(workload.Metadata.Labels, 4)}
spec:
  replicas: 1
  selector:
    matchLabels:
      app.kubernetes.io/name: {workload.Metadata.Name}
      app.kubernetes.io/instance: {workload.Metadata.Name}
  template:
    metadata:
      labels:
        app.kubernetes.io/name: {workload.Metadata.Name}
        app.kubernetes.io/instance: {workload.Metadata.Name}
    spec:
      containers:
{string.Join("\n", containers)}";

            return new GeneratedManifest
            {
                Kind = "Deployment",
                Name = workload.Metadata.Name,
                ApiVersion = "apps/v1",
                Content = deploymentYaml
            };
        }

        private string GenerateKubernetesContainer(string name, ScoreContainer container)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"        - name: {name}");
            sb.AppendLine($"          image: {container.Image}");

            if (container.Command.Any())
            {
                sb.AppendLine("          command:");
                foreach (var cmd in container.Command)
                {
                    sb.AppendLine($"            - {cmd}");
                }
            }

            if (container.Args.Any())
            {
                sb.AppendLine("          args:");
                foreach (var arg in container.Args)
                {
                    sb.AppendLine($"            - {arg}");
                }
            }

            if (container.Variables.Any())
            {
                sb.AppendLine("          env:");
                foreach (var (varName, varValue) in container.Variables)
                {
                    sb.AppendLine($"            - name: {varName}");
                    sb.AppendLine($"              value: \"{varValue}\"");
                }
            }

            if (container.Resources.Limits != null || container.Resources.Requests != null)
            {
                sb.AppendLine("          resources:");
                if (container.Resources.Limits != null)
                {
                    sb.AppendLine("            limits:");
                    if (!string.IsNullOrEmpty(container.Resources.Limits.CPU))
                        sb.AppendLine($"              cpu: {container.Resources.Limits.CPU}");
                    if (!string.IsNullOrEmpty(container.Resources.Limits.Memory))
                        sb.AppendLine($"              memory: {container.Resources.Limits.Memory}");
                }
                if (container.Resources.Requests != null)
                {
                    sb.AppendLine("            requests:");
                    if (!string.IsNullOrEmpty(container.Resources.Requests.CPU))
                        sb.AppendLine($"              cpu: {container.Resources.Requests.CPU}");
                    if (!string.IsNullOrEmpty(container.Resources.Requests.Memory))
                        sb.AppendLine($"              memory: {container.Resources.Requests.Memory}");
                }
            }

            if (container.ReadinessProbe != null)
            {
                sb.AppendLine(GenerateKubernetesProbe("readinessProbe", container.ReadinessProbe));
            }
            if (container.LivenessProbe != null)
            {
                sb.AppendLine(GenerateKubernetesProbe("livenessProbe", container.LivenessProbe));
            }

            return sb.ToString();
        }

        private string GenerateKubernetesProbe(string probeName, ScoreProbe probe)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"          {probeName}:");

            if (probe.Type == ScoreProbeType.HTTP && probe.HttpGet != null)
            {
                sb.AppendLine("            httpGet:");
                sb.AppendLine($"              path: {probe.HttpGet.Path}");
                sb.AppendLine($"              port: {probe.HttpGet.Port}");
            }
            else if (probe.Type == ScoreProbeType.TCP && probe.TcpSocket != null)
            {
                sb.AppendLine("            tcpSocket:");
                sb.AppendLine($"              port: {probe.TcpSocket.Port}");
            }
            else if (probe.Type == ScoreProbeType.GRPC && probe.Grpc != null)
            {
                sb.AppendLine("            grpc:");
                sb.AppendLine($"              port: {probe.Grpc.Port}");
            }
            else if (probe.Type == ScoreProbeType.Command && probe.Command != null)
            {
                sb.AppendLine("            exec:");
                sb.AppendLine("              command:");
                foreach (var cmd in probe.Command)
                {
                    sb.AppendLine($"                - {cmd}");
                }
            }

            sb.AppendLine($"            initialDelaySeconds: {probe.InitialDelaySeconds}");
            sb.AppendLine($"            periodSeconds: {probe.PeriodSeconds}");
            sb.AppendLine($"            timeoutSeconds: {probe.TimeoutSeconds}");
            sb.AppendLine($"            failureThreshold: {probe.FailureThreshold}");

            return sb.ToString().TrimEnd();
        }

        private GeneratedManifest GenerateKubernetesService(ScoreWorkload workload, string ns)
        {
            var ports = new List<string>();
            foreach (var (serviceName, service) in workload.Service)
            {
                foreach (var (portName, port) in service.Ports)
                {
                    var targetPort = port.TargetPort ?? port.Port;
                    ports.Add($@"    - name: {portName}
      port: {port.Port}
      targetPort: {targetPort}
      protocol: {port.Protocol}");
                }
            }

            var serviceYaml = $@"apiVersion: v1
kind: Service
metadata:
  name: {workload.Metadata.Name}
  namespace: {ns}
spec:
  selector:
    app.kubernetes.io/name: {workload.Metadata.Name}
    app.kubernetes.io/instance: {workload.Metadata.Name}
  ports:
{string.Join("\n", ports)}";

            return new GeneratedManifest
            {
                Kind = "Service",
                Name = workload.Metadata.Name,
                ApiVersion = "v1",
                Content = serviceYaml
            };
        }

        private GeneratedManifest? GenerateKubernetesConfigMap(ScoreWorkload workload, string ns)
        {
            var data = new List<string>();
            foreach (var (_, container) in workload.Containers)
            {
                foreach (var (varName, varValue) in container.Variables)
                {
                    // Skip placeholders and sensitive data
                    if (!varValue.Contains("${") && !varName.ToLower().Contains("password") && !varName.ToLower().Contains("secret"))
                    {
                        data.Add($"  {varName}: \"{varValue}\"");
                    }
                }
            }

            if (!data.Any()) return null;

            var configMapYaml = $@"apiVersion: v1
kind: ConfigMap
metadata:
  name: {workload.Metadata.Name}
  namespace: {ns}
data:
{string.Join("\n", data)}";

            return new GeneratedManifest
            {
                Kind = "ConfigMap",
                Name = workload.Metadata.Name,
                ApiVersion = "v1",
                Content = configMapYaml
            };
        }

        private Dictionary<string, GeneratedManifest> GenerateKubernetesResourceManifests(
            string resourceName,
            ScoreResource resource,
            string ns,
            TranslationContext context)
        {
            var manifests = new Dictionary<string, GeneratedManifest>();

            // Check if we have a provisioner for this resource type
            if (context.Provisioners.TryGetValue(resource.Type, out var provisioner))
            {
                // Use provisioner template
                var manifest = GenerateFromProvisioner(resourceName, resource, provisioner, ns);
                if (manifest != null)
                {
                    manifests[$"{manifest.Kind}/{manifest.Name}"] = manifest;
                }
            }
            else
            {
                // Generate default manifests based on resource type
                switch (resource.Type)
                {
                    case ScoreResourceType.Volume:
                        var pvc = GeneratePVC(resourceName, resource, ns);
                        manifests[$"PersistentVolumeClaim/{resourceName}"] = pvc;
                        break;
                    case ScoreResourceType.Secret:
                        var secret = GenerateSecret(resourceName, resource, ns);
                        manifests[$"Secret/{resourceName}"] = secret;
                        break;
                    case ScoreResourceType.ConfigMap:
                        var cm = GenerateResourceConfigMap(resourceName, resource, ns);
                        manifests[$"ConfigMap/{resourceName}"] = cm;
                        break;
                }
            }

            return manifests;
        }

        private GeneratedManifest? GenerateFromProvisioner(
            string resourceName,
            ScoreResource resource,
            ResourceProvisioner provisioner,
            string ns)
        {
            var resourceClass = resource.Class ?? "default";
            if (provisioner.Classes.TryGetValue(resourceClass, out var classConfig) &&
                !string.IsNullOrEmpty(classConfig.Template))
            {
                // Replace template variables
                var content = classConfig.Template
                    .Replace("{{name}}", resourceName)
                    .Replace("{{namespace}}", ns);

                foreach (var (key, value) in resource.Params)
                {
                    content = content.Replace($"{{{{params.{key}}}}}", value?.ToString() ?? "");
                }

                return new GeneratedManifest
                {
                    Kind = "Custom",
                    Name = resourceName,
                    Content = content
                };
            }

            return null;
        }

        private GeneratedManifest GeneratePVC(string name, ScoreResource resource, string ns)
        {
            var size = resource.Params.GetValueOrDefault("size")?.ToString() ?? "1Gi";
            var storageClass = resource.Params.GetValueOrDefault("storageClass")?.ToString() ?? "";

            var yaml = $@"apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: {name}
  namespace: {ns}
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: {size}";

            if (!string.IsNullOrEmpty(storageClass))
            {
                yaml += $"\n  storageClassName: {storageClass}";
            }

            return new GeneratedManifest
            {
                Kind = "PersistentVolumeClaim",
                Name = name,
                ApiVersion = "v1",
                Content = yaml
            };
        }

        private GeneratedManifest GenerateSecret(string name, ScoreResource resource, string ns)
        {
            var yaml = $@"apiVersion: v1
kind: Secret
metadata:
  name: {name}
  namespace: {ns}
type: Opaque
stringData:
  # Add secret data here";

            return new GeneratedManifest
            {
                Kind = "Secret",
                Name = name,
                ApiVersion = "v1",
                Content = yaml
            };
        }

        private GeneratedManifest GenerateResourceConfigMap(string name, ScoreResource resource, string ns)
        {
            var data = new List<string>();
            foreach (var (key, value) in resource.Params)
            {
                data.Add($"  {key}: \"{value}\"");
            }

            var yaml = $@"apiVersion: v1
kind: ConfigMap
metadata:
  name: {name}
  namespace: {ns}
data:
{string.Join("\n", data)}";

            return new GeneratedManifest
            {
                Kind = "ConfigMap",
                Name = name,
                ApiVersion = "v1",
                Content = yaml
            };
        }

        private Task<TranslationResult> TranslateToDockerComposeAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var services = new List<string>();

            foreach (var (containerName, container) in workload.Containers)
            {
                var serviceYaml = GenerateDockerComposeService(containerName, container, workload);
                services.Add(serviceYaml);
            }

            // Generate resource services (databases, etc.)
            foreach (var (resourceName, resource) in workload.Resources)
            {
                var resourceService = GenerateDockerComposeResource(resourceName, resource);
                if (resourceService != null)
                {
                    services.Add(resourceService);
                }
            }

            var composeContent = $@"version: '3.8'

services:
{string.Join("\n\n", services)}";

            result.Manifests["docker-compose.yaml"] = new GeneratedManifest
            {
                Kind = "DockerCompose",
                Name = "docker-compose",
                Content = composeContent
            };

            result.CombinedOutput = composeContent;
            return Task.FromResult(result);
        }

        private string GenerateDockerComposeService(string name, ScoreContainer container, ScoreWorkload workload)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  {name}:");
            sb.AppendLine($"    image: {container.Image}");

            if (container.Command.Any())
            {
                sb.AppendLine($"    command: [{string.Join(", ", container.Command.Select(c => $"\"{c}\""))}]");
            }

            if (container.Variables.Any())
            {
                sb.AppendLine("    environment:");
                foreach (var (varName, varValue) in container.Variables)
                {
                    sb.AppendLine($"      - {varName}={varValue}");
                }
            }

            // Add ports from service definition
            if (workload.Service.Any())
            {
                sb.AppendLine("    ports:");
                foreach (var service in workload.Service.Values)
                {
                    foreach (var port in service.Ports.Values)
                    {
                        var target = port.TargetPort ?? port.Port;
                        sb.AppendLine($"      - \"{port.Port}:{target}\"");
                    }
                }
            }

            if (container.Volumes.Any())
            {
                sb.AppendLine("    volumes:");
                foreach (var vol in container.Volumes)
                {
                    sb.AppendLine($"      - {vol.Source}:{vol.Target}");
                }
            }

            // Add resource dependencies
            if (workload.Resources.Any())
            {
                sb.AppendLine("    depends_on:");
                foreach (var resourceName in workload.Resources.Keys)
                {
                    sb.AppendLine($"      - {resourceName}");
                }
            }

            return sb.ToString();
        }

        private string? GenerateDockerComposeResource(string name, ScoreResource resource)
        {
            return resource.Type switch
            {
                ScoreResourceType.PostgreSQL => $@"  {name}:
    image: postgres:15
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=postgres
      - POSTGRES_DB={name}
    volumes:
      - {name}_data:/var/lib/postgresql/data

volumes:
  {name}_data:",

                ScoreResourceType.Redis => $@"  {name}:
    image: redis:7-alpine
    volumes:
      - {name}_data:/data

volumes:
  {name}_data:",

                ScoreResourceType.MongoDB => $@"  {name}:
    image: mongo:6
    environment:
      - MONGO_INITDB_ROOT_USERNAME=root
      - MONGO_INITDB_ROOT_PASSWORD=password
    volumes:
      - {name}_data:/data/db

volumes:
  {name}_data:",

                _ => null
            };
        }

        private Task<TranslationResult> TranslateToHelmAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            // Generate values.yaml
            var valuesYaml = GenerateHelmValues(workload);
            result.Manifests["values.yaml"] = new GeneratedManifest
            {
                Kind = "HelmValues",
                Name = "values",
                Content = valuesYaml
            };

            // Generate Chart.yaml
            var chartYaml = $@"apiVersion: v2
name: {workload.Metadata.Name}
description: A Helm chart generated from Score specification
type: application
version: 0.1.0
appVersion: ""1.0.0""";

            result.Manifests["Chart.yaml"] = new GeneratedManifest
            {
                Kind = "HelmChart",
                Name = "Chart",
                Content = chartYaml
            };

            // Generate templates
            var deploymentTemplate = GenerateHelmDeploymentTemplate(workload);
            result.Manifests["templates/deployment.yaml"] = new GeneratedManifest
            {
                Kind = "HelmTemplate",
                Name = "deployment",
                Content = deploymentTemplate
            };

            if (workload.Service.Any())
            {
                var serviceTemplate = GenerateHelmServiceTemplate(workload);
                result.Manifests["templates/service.yaml"] = new GeneratedManifest
                {
                    Kind = "HelmTemplate",
                    Name = "service",
                    Content = serviceTemplate
                };
            }

            result.CombinedOutput = $"# Helm Chart: {workload.Metadata.Name}\n\n{valuesYaml}";
            return Task.FromResult(result);
        }

        private string GenerateHelmValues(ScoreWorkload workload)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Default values for the application");
            sb.AppendLine();
            sb.AppendLine($"replicaCount: 1");
            sb.AppendLine();

            foreach (var (containerName, container) in workload.Containers)
            {
                sb.AppendLine($"{containerName}:");
                sb.AppendLine($"  image:");

                var imageParts = container.Image.Split(':');
                sb.AppendLine($"    repository: {imageParts[0]}");
                sb.AppendLine($"    tag: {(imageParts.Length > 1 ? imageParts[1] : "latest")}");
                sb.AppendLine($"    pullPolicy: IfNotPresent");

                if (container.Resources.Limits != null || container.Resources.Requests != null)
                {
                    sb.AppendLine("  resources:");
                    if (container.Resources.Limits != null)
                    {
                        sb.AppendLine("    limits:");
                        if (!string.IsNullOrEmpty(container.Resources.Limits.CPU))
                            sb.AppendLine($"      cpu: {container.Resources.Limits.CPU}");
                        if (!string.IsNullOrEmpty(container.Resources.Limits.Memory))
                            sb.AppendLine($"      memory: {container.Resources.Limits.Memory}");
                    }
                    if (container.Resources.Requests != null)
                    {
                        sb.AppendLine("    requests:");
                        if (!string.IsNullOrEmpty(container.Resources.Requests.CPU))
                            sb.AppendLine($"      cpu: {container.Resources.Requests.CPU}");
                        if (!string.IsNullOrEmpty(container.Resources.Requests.Memory))
                            sb.AppendLine($"      memory: {container.Resources.Requests.Memory}");
                    }
                }
                sb.AppendLine();
            }

            // Service configuration
            if (workload.Service.Any())
            {
                sb.AppendLine("service:");
                sb.AppendLine("  type: ClusterIP");
                var firstPort = workload.Service.Values.First().Ports.Values.FirstOrDefault();
                if (firstPort != null)
                {
                    sb.AppendLine($"  port: {firstPort.Port}");
                }
            }

            return sb.ToString();
        }

        private string GenerateHelmDeploymentTemplate(ScoreWorkload workload)
        {
            return $@"apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{{{ include ""{workload.Metadata.Name}.fullname"" . }}}}
  labels:
    {{{{- include ""{workload.Metadata.Name}.labels"" . | nindent 4 }}}}
spec:
  replicas: {{{{ .Values.replicaCount }}}}
  selector:
    matchLabels:
      {{{{- include ""{workload.Metadata.Name}.selectorLabels"" . | nindent 6 }}}}
  template:
    metadata:
      labels:
        {{{{- include ""{workload.Metadata.Name}.selectorLabels"" . | nindent 8 }}}}
    spec:
      containers:
        {{{{- range $name, $container := .Values }}}}
        {{{{- if and (kindIs ""map"" $container) (hasKey $container ""image"") }}}}
        - name: {{{{ $name }}}}
          image: ""{{{{ $container.image.repository }}}}:{{{{ $container.image.tag }}}}""
          imagePullPolicy: {{{{ $container.image.pullPolicy }}}}
          {{{{- if $container.resources }}}}
          resources:
            {{{{- toYaml $container.resources | nindent 12 }}}}
          {{{{- end }}}}
        {{{{- end }}}}
        {{{{- end }}}}";
        }

        private string GenerateHelmServiceTemplate(ScoreWorkload workload)
        {
            return $@"apiVersion: v1
kind: Service
metadata:
  name: {{{{ include ""{workload.Metadata.Name}.fullname"" . }}}}
  labels:
    {{{{- include ""{workload.Metadata.Name}.labels"" . | nindent 4 }}}}
spec:
  type: {{{{ .Values.service.type }}}}
  ports:
    - port: {{{{ .Values.service.port }}}}
      targetPort: http
      protocol: TCP
      name: http
  selector:
    {{{{- include ""{workload.Metadata.Name}.selectorLabels"" . | nindent 4 }}}}";
        }

        private Task<TranslationResult> TranslateToHumanitecAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            // Humanitec Delta format
            var delta = new
            {
                metadata = new
                {
                    name = workload.Metadata.Name,
                    env_id = workload.Metadata.Environment
                },
                modules = new Dictionary<string, object>
                {
                    [workload.Metadata.Name] = new
                    {
                        profile = "humanitec/default-module",
                        spec = new
                        {
                            containers = workload.Containers.ToDictionary(
                                c => c.Key,
                                c => new
                                {
                                    image = c.Value.Image,
                                    variables = c.Value.Variables,
                                    resources = c.Value.Resources
                                }),
                            service = workload.Service
                        },
                        externals = workload.Resources.ToDictionary(
                            r => r.Key,
                            r => new
                            {
                                type = r.Value.Type.ToString().ToLower(),
                                @class = r.Value.Class ?? "default"
                            })
                    }
                }
            };

            var deltaJson = JsonSerializer.Serialize(delta, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            result.Manifests["delta.json"] = new GeneratedManifest
            {
                Kind = "HumanitecDelta",
                Name = "delta",
                Content = deltaJson,
                Format = "json"
            };

            result.CombinedOutput = deltaJson;
            return Task.FromResult(result);
        }

        private Task<TranslationResult> TranslateToNomadAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var tasks = new List<string>();
            foreach (var (containerName, container) in workload.Containers)
            {
                var task = GenerateNomadTask(containerName, container);
                tasks.Add(task);
            }

            var jobHcl = $@"job ""{workload.Metadata.Name}"" {{
  datacenters = [""dc1""]
  type = ""service""

  group ""{workload.Metadata.Name}"" {{
    count = 1

{string.Join("\n", tasks)}
  }}
}}";

            result.Manifests["job.hcl"] = new GeneratedManifest
            {
                Kind = "NomadJob",
                Name = workload.Metadata.Name,
                Content = jobHcl,
                Format = "hcl"
            };

            result.CombinedOutput = jobHcl;
            return Task.FromResult(result);
        }

        private string GenerateNomadTask(string name, ScoreContainer container)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($@"    task ""{name}"" {{
      driver = ""docker""

      config {{
        image = ""{container.Image}""
      }}");

            if (container.Variables.Any())
            {
                sb.AppendLine();
                sb.AppendLine("      env {");
                foreach (var (varName, varValue) in container.Variables)
                {
                    sb.AppendLine($"        {varName} = \"{varValue}\"");
                }
                sb.AppendLine("      }");
            }

            if (container.Resources.Limits != null || container.Resources.Requests != null)
            {
                sb.AppendLine();
                sb.AppendLine("      resources {");
                var cpu = ParseCpuToMHz(container.Resources.Requests?.CPU ?? container.Resources.Limits?.CPU ?? "100m");
                var memory = ParseMemoryToMB(container.Resources.Requests?.Memory ?? container.Resources.Limits?.Memory ?? "128Mi");
                sb.AppendLine($"        cpu = {cpu}");
                sb.AppendLine($"        memory = {memory}");
                sb.AppendLine("      }");
            }

            sb.AppendLine("    }");
            return sb.ToString();
        }

        private Task<TranslationResult> TranslateToCloudRunAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var container = workload.Containers.Values.First();
            var port = workload.Service.Values.FirstOrDefault()?.Ports.Values.FirstOrDefault()?.Port ?? 8080;

            var cloudRunYaml = $@"apiVersion: serving.knative.dev/v1
kind: Service
metadata:
  name: {workload.Metadata.Name}
  labels:
    cloud.googleapis.com/location: us-central1
spec:
  template:
    spec:
      containers:
        - image: {container.Image}
          ports:
            - containerPort: {port}
          resources:
            limits:
              cpu: {container.Resources.Limits?.CPU ?? "1000m"}
              memory: {container.Resources.Limits?.Memory ?? "512Mi"}
          env:
{string.Join("\n", container.Variables.Select(v => $"            - name: {v.Key}\n              value: \"{v.Value}\""))}";

            result.Manifests["service.yaml"] = new GeneratedManifest
            {
                Kind = "CloudRunService",
                Name = workload.Metadata.Name,
                Content = cloudRunYaml
            };

            result.CombinedOutput = cloudRunYaml;
            return Task.FromResult(result);
        }

        private Task<TranslationResult> TranslateToAzureContainerAppsAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var container = workload.Containers.Values.First();

            var acaJson = new
            {
                type = "Microsoft.App/containerApps",
                apiVersion = "2023-05-01",
                name = workload.Metadata.Name,
                location = "[resourceGroup().location]",
                properties = new
                {
                    configuration = new
                    {
                        ingress = workload.Service.Any() ? new
                        {
                            external = true,
                            targetPort = workload.Service.Values.First().Ports.Values.First().Port
                        } : null
                    },
                    template = new
                    {
                        containers = new[]
                        {
                            new
                            {
                                name = workload.Containers.Keys.First(),
                                image = container.Image,
                                resources = new
                                {
                                    cpu = ParseCpuToFloat(container.Resources.Requests?.CPU ?? "0.5"),
                                    memory = container.Resources.Requests?.Memory ?? "1Gi"
                                },
                                env = container.Variables.Select(v => new { name = v.Key, value = v.Value }).ToArray()
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(acaJson, new JsonSerializerOptions { WriteIndented = true });

            result.Manifests["containerapp.json"] = new GeneratedManifest
            {
                Kind = "AzureContainerApp",
                Name = workload.Metadata.Name,
                Content = jsonContent,
                Format = "json"
            };

            result.CombinedOutput = jsonContent;
            return Task.FromResult(result);
        }

        private Task<TranslationResult> TranslateToECSAsync(
            ScoreWorkload workload,
            TranslationContext context,
            TranslationResult result)
        {
            var container = workload.Containers.Values.First();
            var containerDefs = workload.Containers.Select(c => new
            {
                name = c.Key,
                image = c.Value.Image,
                essential = true,
                cpu = ParseCpuToUnits(c.Value.Resources.Requests?.CPU ?? "256m"),
                memory = ParseMemoryToMB(c.Value.Resources.Requests?.Memory ?? "512Mi"),
                environment = c.Value.Variables.Select(v => new { name = v.Key, value = v.Value }).ToArray(),
                portMappings = workload.Service.Any()
                    ? workload.Service.Values.SelectMany(s => s.Ports.Values.Select(p => new { containerPort = p.Port, protocol = p.Protocol.ToLower() })).ToArray()
                    : Array.Empty<object>()
            }).ToArray();

            var taskDef = new
            {
                family = workload.Metadata.Name,
                containerDefinitions = containerDefs,
                requiresCompatibilities = new[] { "FARGATE" },
                networkMode = "awsvpc",
                cpu = "256",
                memory = "512"
            };

            var jsonContent = JsonSerializer.Serialize(taskDef, new JsonSerializerOptions { WriteIndented = true });

            result.Manifests["task-definition.json"] = new GeneratedManifest
            {
                Kind = "ECSTaskDefinition",
                Name = workload.Metadata.Name,
                Content = jsonContent,
                Format = "json"
            };

            result.CombinedOutput = jsonContent;
            return Task.FromResult(result);
        }

        #endregion

        #region Helper Methods

        private string GenerateLabels(Dictionary<string, string> labels, int indent)
        {
            if (!labels.Any()) return "";
            var padding = new string(' ', indent);
            return string.Join("\n", labels.Select(l => $"{padding}{l.Key}: {l.Value}"));
        }

        private string CombineManifestsToYaml(IEnumerable<GeneratedManifest> manifests)
        {
            return string.Join("\n---\n", manifests.Where(m => m.Format == "yaml").Select(m => m.Content));
        }

        private int ParseCpuToMHz(string cpu)
        {
            if (cpu.EndsWith("m"))
            {
                return int.Parse(cpu.TrimEnd('m'));
            }
            return int.Parse(cpu) * 1000;
        }

        private int ParseMemoryToMB(string memory)
        {
            if (memory.EndsWith("Mi"))
            {
                return int.Parse(memory.Replace("Mi", ""));
            }
            if (memory.EndsWith("Gi"))
            {
                return int.Parse(memory.Replace("Gi", "")) * 1024;
            }
            return 128;
        }

        private double ParseCpuToFloat(string cpu)
        {
            if (cpu.EndsWith("m"))
            {
                return int.Parse(cpu.TrimEnd('m')) / 1000.0;
            }
            return double.Parse(cpu);
        }

        private int ParseCpuToUnits(string cpu)
        {
            if (cpu.EndsWith("m"))
            {
                return int.Parse(cpu.TrimEnd('m'));
            }
            return int.Parse(cpu) * 1000;
        }

        #endregion

        #endregion

        #region YAML Parsing

        public Task<ScoreWorkload> ParseScoreYamlAsync(
            string tenantId,
            string yamlContent,
            CancellationToken cancellation = default)
        {
            // Simplified YAML parsing (in production, use YamlDotNet)
            var workload = new ScoreWorkload
            {
                TenantId = tenantId,
                State = WorkloadState.Draft
            };

            // Parse apiVersion
            var apiVersionMatch = Regex.Match(yamlContent, @"apiVersion:\s*(.+)");
            if (apiVersionMatch.Success)
            {
                workload.ApiVersion = apiVersionMatch.Groups[1].Value.Trim();
            }

            // Parse metadata.name
            var nameMatch = Regex.Match(yamlContent, @"metadata:\s*\n\s+name:\s*(.+)");
            if (nameMatch.Success)
            {
                workload.Metadata.Name = nameMatch.Groups[1].Value.Trim();
            }

            // Store in memory
            if (!_workloads.ContainsKey(tenantId))
                _workloads[tenantId] = new();

            _workloads[tenantId][workload.Id] = workload;

            _logger.LogInformation(
                "Parsed Score YAML for workload {Name}",
                workload.Metadata.Name);

            return Task.FromResult(workload);
        }

        public async Task<string> GenerateScoreYamlAsync(
            string tenantId,
            string workloadId,
            CancellationToken cancellation = default)
        {
            var workload = await GetWorkloadAsync(tenantId, workloadId, cancellation);
            if (workload == null)
                throw new InvalidOperationException($"Workload {workloadId} not found");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"apiVersion: {workload.ApiVersion}");
            sb.AppendLine();
            sb.AppendLine("metadata:");
            sb.AppendLine($"  name: {workload.Metadata.Name}");

            if (workload.Metadata.Labels.Any())
            {
                sb.AppendLine("  labels:");
                foreach (var (key, value) in workload.Metadata.Labels)
                {
                    sb.AppendLine($"    {key}: {value}");
                }
            }

            if (workload.Containers.Any())
            {
                sb.AppendLine();
                sb.AppendLine("containers:");
                foreach (var (name, container) in workload.Containers)
                {
                    sb.AppendLine($"  {name}:");
                    sb.AppendLine($"    image: {container.Image}");

                    if (container.Variables.Any())
                    {
                        sb.AppendLine("    variables:");
                        foreach (var (varName, varValue) in container.Variables)
                        {
                            sb.AppendLine($"      {varName}: \"{varValue}\"");
                        }
                    }

                    if (container.Resources.Requests != null || container.Resources.Limits != null)
                    {
                        sb.AppendLine("    resources:");
                        if (container.Resources.Limits != null)
                        {
                            sb.AppendLine("      limits:");
                            if (!string.IsNullOrEmpty(container.Resources.Limits.CPU))
                                sb.AppendLine($"        cpu: {container.Resources.Limits.CPU}");
                            if (!string.IsNullOrEmpty(container.Resources.Limits.Memory))
                                sb.AppendLine($"        memory: {container.Resources.Limits.Memory}");
                        }
                    }
                }
            }

            if (workload.Service.Any())
            {
                sb.AppendLine();
                sb.AppendLine("service:");
                foreach (var (serviceName, service) in workload.Service)
                {
                    sb.AppendLine($"  {serviceName}:");
                    sb.AppendLine("    ports:");
                    foreach (var (portName, port) in service.Ports)
                    {
                        sb.AppendLine($"      {portName}:");
                        sb.AppendLine($"        port: {port.Port}");
                        if (port.TargetPort.HasValue)
                            sb.AppendLine($"        targetPort: {port.TargetPort}");
                    }
                }
            }

            if (workload.Resources.Any())
            {
                sb.AppendLine();
                sb.AppendLine("resources:");
                foreach (var (resourceName, resource) in workload.Resources)
                {
                    sb.AppendLine($"  {resourceName}:");
                    sb.AppendLine($"    type: {resource.Type.ToString().ToLower()}");
                    if (!string.IsNullOrEmpty(resource.Class))
                        sb.AppendLine($"    class: {resource.Class}");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Provisioner Management

        public Task<ResourceProvisioner> RegisterProvisionerAsync(
            string tenantId,
            ResourceProvisioner provisioner,
            CancellationToken cancellation = default)
        {
            if (!_provisioners.ContainsKey(tenantId))
                _provisioners[tenantId] = new();

            _provisioners[tenantId][provisioner.Name] = provisioner;

            _logger.LogInformation(
                "Registered provisioner {Name} for resource type {Type}",
                provisioner.Name, provisioner.ResourceType);

            return Task.FromResult(provisioner);
        }

        public Task<List<ResourceProvisioner>> GetProvisionersAsync(
            string tenantId,
            ScoreResourceType? resourceType = null,
            CancellationToken cancellation = default)
        {
            if (!_provisioners.TryGetValue(tenantId, out var provisioners))
                return Task.FromResult(new List<ResourceProvisioner>());

            var result = provisioners.Values.AsEnumerable();
            if (resourceType.HasValue)
            {
                result = result.Where(p => p.ResourceType == resourceType.Value);
            }

            return Task.FromResult(result.ToList());
        }

        #endregion

        #region Extension Management

        public Task<ScoreExtension> RegisterExtensionAsync(
            string tenantId,
            ScoreExtension extension,
            CancellationToken cancellation = default)
        {
            if (!_extensions.ContainsKey(tenantId))
                _extensions[tenantId] = new();

            _extensions[tenantId][extension.Id] = extension;

            _logger.LogInformation(
                "Registered Score extension {Name} ({Uri})",
                extension.Name, extension.Uri);

            return Task.FromResult(extension);
        }

        #endregion

        #region Templates

        public async Task<ScoreWorkload> CreateFromTemplateAsync(
            string tenantId,
            string templateId,
            Dictionary<string, object> parameters,
            CancellationToken cancellation = default)
        {
            if (!_templates.TryGetValue(tenantId, out var templates) ||
                !templates.TryGetValue(templateId, out var template))
            {
                throw new InvalidOperationException($"Template {templateId} not found");
            }

            // Deep copy base workload
            var workload = JsonSerializer.Deserialize<ScoreWorkload>(
                JsonSerializer.Serialize(template.BaseWorkload))!;

            workload.Id = Guid.NewGuid().ToString();
            workload.TenantId = tenantId;
            workload.CreatedAt = DateTime.UtcNow;

            // Apply parameters
            foreach (var param in template.Parameters)
            {
                if (parameters.TryGetValue(param.Name, out var value))
                {
                    ApplyParameterToWorkload(workload, param.Name, value);
                }
                else if (param.Required && param.Default == null)
                {
                    throw new InvalidOperationException($"Required parameter {param.Name} not provided");
                }
                else if (param.Default != null)
                {
                    ApplyParameterToWorkload(workload, param.Name, param.Default);
                }
            }

            // Save workload
            return await CreateWorkloadAsync(tenantId, workload, cancellation);
        }

        private void ApplyParameterToWorkload(ScoreWorkload workload, string paramName, object value)
        {
            // Apply parameter based on naming convention
            switch (paramName.ToLower())
            {
                case "name":
                    workload.Metadata.Name = value.ToString()!;
                    break;
                case "image":
                    if (workload.Containers.Any())
                    {
                        workload.Containers.Values.First().Image = value.ToString()!;
                    }
                    break;
                case "replicas":
                    workload.Extensions["replicas"] = value;
                    break;
            }
        }

        public Task<List<ScoreTemplate>> ListTemplatesAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_templates.TryGetValue(tenantId, out var templates))
                return Task.FromResult(new List<ScoreTemplate>());

            return Task.FromResult(templates.Values.ToList());
        }

        #endregion

        #region Diff

        public async Task<WorkloadDiff> DiffWorkloadsAsync(
            string tenantId,
            string workloadId1,
            string workloadId2,
            CancellationToken cancellation = default)
        {
            var workload1 = await GetWorkloadAsync(tenantId, workloadId1, cancellation);
            var workload2 = await GetWorkloadAsync(tenantId, workloadId2, cancellation);

            if (workload1 == null || workload2 == null)
                throw new InvalidOperationException("One or both workloads not found");

            var diff = new WorkloadDiff
            {
                WorkloadId1 = workloadId1,
                WorkloadId2 = workloadId2
            };

            // Compare containers
            foreach (var container1 in workload1.Containers)
            {
                if (!workload2.Containers.TryGetValue(container1.Key, out var container2))
                {
                    diff.Differences.Add(new DiffEntry
                    {
                        Operation = DiffOperation.Remove,
                        Path = $"containers.{container1.Key}",
                        OldValue = container1.Value
                    });
                }
                else
                {
                    // Compare images
                    if (container1.Value.Image != container2.Image)
                    {
                        diff.Differences.Add(new DiffEntry
                        {
                            Operation = DiffOperation.Modify,
                            Path = $"containers.{container1.Key}.image",
                            OldValue = container1.Value.Image,
                            NewValue = container2.Image
                        });
                    }
                }
            }

            foreach (var container2 in workload2.Containers)
            {
                if (!workload1.Containers.ContainsKey(container2.Key))
                {
                    diff.Differences.Add(new DiffEntry
                    {
                        Operation = DiffOperation.Add,
                        Path = $"containers.{container2.Key}",
                        NewValue = container2.Value
                    });
                }
            }

            // Compare resources
            foreach (var resource1 in workload1.Resources)
            {
                if (!workload2.Resources.ContainsKey(resource1.Key))
                {
                    diff.Differences.Add(new DiffEntry
                    {
                        Operation = DiffOperation.Remove,
                        Path = $"resources.{resource1.Key}",
                        OldValue = resource1.Value
                    });
                }
            }

            foreach (var resource2 in workload2.Resources)
            {
                if (!workload1.Resources.ContainsKey(resource2.Key))
                {
                    diff.Differences.Add(new DiffEntry
                    {
                        Operation = DiffOperation.Add,
                        Path = $"resources.{resource2.Key}",
                        NewValue = resource2.Value
                    });
                }
            }

            return diff;
        }

        #endregion

        #region Initialization

        private void InitializeDefaultProvisioners()
        {
            var defaultProvisioners = new[]
            {
                new ResourceProvisioner
                {
                    Name = "postgresql-crossplane",
                    ResourceType = ScoreResourceType.PostgreSQL,
                    ProvisionerType = "crossplane",
                    OutputMappings = new Dictionary<string, string>
                    {
                        ["host"] = "status.atProvider.endpoint",
                        ["port"] = "5432",
                        ["username"] = "status.atProvider.username",
                        ["password"] = "spec.forProvider.password"
                    },
                    Classes = new Dictionary<string, ProvisionerClass>
                    {
                        ["default"] = new ProvisionerClass
                        {
                            Name = "default",
                            Params = new Dictionary<string, object>
                            {
                                ["size"] = "small",
                                ["version"] = "15"
                            }
                        },
                        ["production"] = new ProvisionerClass
                        {
                            Name = "production",
                            Params = new Dictionary<string, object>
                            {
                                ["size"] = "large",
                                ["version"] = "15",
                                ["highAvailability"] = true
                            }
                        }
                    }
                },
                new ResourceProvisioner
                {
                    Name = "redis-helm",
                    ResourceType = ScoreResourceType.Redis,
                    ProvisionerType = "helm",
                    OutputMappings = new Dictionary<string, string>
                    {
                        ["host"] = "{{ .Release.Name }}-redis-master",
                        ["port"] = "6379"
                    }
                }
            };

            foreach (var prov in defaultProvisioners)
            {
                if (!_provisioners.ContainsKey("default"))
                    _provisioners["default"] = new();
                _provisioners["default"][prov.Name] = prov;
            }
        }

        private void InitializeDefaultTemplates()
        {
            var defaultTemplates = new[]
            {
                new ScoreTemplate
                {
                    Name = "web-service",
                    Description = "Standard web service template with HTTP endpoint",
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter
                        {
                            Name = "name",
                            Description = "Service name",
                            Type = "string",
                            Required = true
                        },
                        new TemplateParameter
                        {
                            Name = "image",
                            Description = "Container image",
                            Type = "string",
                            Required = true
                        },
                        new TemplateParameter
                        {
                            Name = "port",
                            Description = "HTTP port",
                            Type = "integer",
                            Default = 8080
                        }
                    },
                    BaseWorkload = new ScoreWorkload
                    {
                        ApiVersion = "score.dev/v1b1",
                        Containers = new Dictionary<string, ScoreContainer>
                        {
                            ["main"] = new ScoreContainer
                            {
                                Image = "{{image}}",
                                Resources = new ScoreResourceRequirements
                                {
                                    Requests = new ScoreResourceSpec { CPU = "100m", Memory = "128Mi" },
                                    Limits = new ScoreResourceSpec { CPU = "500m", Memory = "512Mi" }
                                },
                                ReadinessProbe = new ScoreProbe
                                {
                                    Type = ScoreProbeType.HTTP,
                                    HttpGet = new ScoreHttpProbe { Path = "/health", Port = 8080 }
                                }
                            }
                        },
                        Service = new Dictionary<string, ScoreService>
                        {
                            ["default"] = new ScoreService
                            {
                                Ports = new Dictionary<string, ScoreServicePort>
                                {
                                    ["http"] = new ScoreServicePort { Port = 8080 }
                                }
                            }
                        }
                    }
                },
                new ScoreTemplate
                {
                    Name = "api-with-database",
                    Description = "API service with PostgreSQL database",
                    Parameters = new List<TemplateParameter>
                    {
                        new TemplateParameter { Name = "name", Type = "string", Required = true },
                        new TemplateParameter { Name = "image", Type = "string", Required = true },
                        new TemplateParameter { Name = "dbClass", Type = "string", Default = "default" }
                    },
                    BaseWorkload = new ScoreWorkload
                    {
                        ApiVersion = "score.dev/v1b1",
                        Containers = new Dictionary<string, ScoreContainer>
                        {
                            ["api"] = new ScoreContainer
                            {
                                Image = "{{image}}",
                                Variables = new Dictionary<string, string>
                                {
                                    ["DATABASE_HOST"] = "${resources.db.host}",
                                    ["DATABASE_PORT"] = "${resources.db.port}",
                                    ["DATABASE_USER"] = "${resources.db.username}",
                                    ["DATABASE_PASSWORD"] = "${resources.db.password}"
                                }
                            }
                        },
                        Resources = new Dictionary<string, ScoreResource>
                        {
                            ["db"] = new ScoreResource
                            {
                                Type = ScoreResourceType.PostgreSQL,
                                Class = "{{dbClass}}"
                            }
                        }
                    }
                }
            };

            foreach (var template in defaultTemplates)
            {
                if (!_templates.ContainsKey("default"))
                    _templates["default"] = new();
                _templates["default"][template.Id] = template;
            }
        }

        private void InitializeDefaultExtensions()
        {
            var defaultExtensions = new[]
            {
                new ScoreExtension
                {
                    Name = "Humanitec",
                    Uri = "score.humanitec.io/v1b1",
                    Description = "Humanitec Platform Orchestrator extensions",
                    TargetPlatforms = new List<ScoreTargetPlatform> { ScoreTargetPlatform.HumanitecDelta }
                },
                new ScoreExtension
                {
                    Name = "Kubernetes",
                    Uri = "score.kubernetes.io/v1",
                    Description = "Kubernetes-specific extensions (ingress, annotations)",
                    TargetPlatforms = new List<ScoreTargetPlatform>
                    {
                        ScoreTargetPlatform.Kubernetes,
                        ScoreTargetPlatform.Helm
                    }
                }
            };

            foreach (var ext in defaultExtensions)
            {
                if (!_extensions.ContainsKey("default"))
                    _extensions["default"] = new();
                _extensions["default"][ext.Id] = ext;
            }
        }

        #endregion
    }

    #endregion
}
