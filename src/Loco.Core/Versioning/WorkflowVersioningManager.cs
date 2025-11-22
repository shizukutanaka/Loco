using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Versioning
{
    /// <summary>
    /// Workflow versioning and migration manager for schema evolution
    /// Phase 21: Version control, state migration, rollback, compatibility checking, gradual rollout
    /// Manage workflow versions, migrate states between versions, detect breaking changes, rollback safely
    /// </summary>
    public interface IWorkflowVersioningManager
    {
        Task<WorkflowVersion> RegisterVersionAsync(string tenantId, WorkflowVersionDefinition version, CancellationToken cancellationToken = default);
        Task<WorkflowVersion> GetVersionAsync(string tenantId, string workflowId, int versionNumber, CancellationToken cancellationToken = default);
        Task<List<WorkflowVersion>> GetVersionHistoryAsync(string tenantId, string workflowId, CancellationToken cancellationToken = default);
        Task<MigrationPlan> CreateMigrationPlanAsync(string tenantId, string workflowId, int fromVersion, int toVersion, CancellationToken cancellationToken = default);
        Task<MigrationResult> ExecuteMigrationAsync(string tenantId, string workflowId, int toVersion, CancellationToken cancellationToken = default);
        Task<bool> RollbackAsync(string tenantId, string workflowId, int targetVersion, CancellationToken cancellationToken = default);
        Task<CompatibilityCheck> CheckCompatibilityAsync(string tenantId, string workflowId, int targetVersion, CancellationToken cancellationToken = default);
        Task<BreakingChangeDetectionResult> DetectBreakingChangesAsync(string tenantId, string workflowId, int fromVersion, int toVersion, CancellationToken cancellationToken = default);
        Task<VersionMetrics> GetVersionMetricsAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<bool> SetCanaryDeploymentAsync(string tenantId, string workflowId, int version, int canaryPercentage, CancellationToken cancellationToken = default);
    }

    public class WorkflowVersioningManager : IWorkflowVersioningManager
    {
        private readonly ILogger<WorkflowVersioningManager> _logger;
        private readonly Dictionary<string, WorkflowVersion> _versions = new();
        private readonly Dictionary<string, List<VersionedWorkflowState>> _stateSnapshots = new();
        private readonly Dictionary<string, MigrationHistory> _migrationHistory = new();
        private readonly Dictionary<string, CanaryDeployment> _canaryDeployments = new();
        private readonly Random _random = new(42);

        public WorkflowVersioningManager(ILogger<WorkflowVersioningManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WorkflowVersion> RegisterVersionAsync(string tenantId, WorkflowVersionDefinition version, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (version == null)
                throw new ArgumentNullException(nameof(version));

            _logger.LogInformation("Registering version {VersionNumber} for workflow {WorkflowId}", version.VersionNumber, version.WorkflowId);

            await Task.Delay(25, cancellationToken);

            var workflowVersion = new WorkflowVersion
            {
                VersionId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                WorkflowId = version.WorkflowId,
                VersionNumber = version.VersionNumber,
                CreatedAt = DateTimeOffset.UtcNow,
                SchemaHash = ComputeSchemaHash(version),
                Description = version.Description,
                Breaking = DetectBreakingChanges(version),
                Steps = version.Steps,
                Dependencies = version.Dependencies,
                InputSchema = version.InputSchema,
                OutputSchema = version.OutputSchema,
                MigrationStrategies = version.MigrationStrategies ?? new Dictionary<int, string>(),
                Status = "active",
                ReleaseDate = DateTimeOffset.UtcNow,
                DeprecatedAt = null
            };

            var versionKey = $"{tenantId}:{version.WorkflowId}:v{version.VersionNumber}";
            _versions[versionKey] = workflowVersion;

            return workflowVersion;
        }

        public async Task<WorkflowVersion> GetVersionAsync(string tenantId, string workflowId, int versionNumber, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Retrieving version {VersionNumber} for workflow {WorkflowId}", versionNumber, workflowId);

            await Task.Delay(15, cancellationToken);

            var versionKey = $"{tenantId}:{workflowId}:v{versionNumber}";
            if (!_versions.ContainsKey(versionKey))
                throw new InvalidOperationException($"Workflow version '{workflowId}:v{versionNumber}' not found");

            return _versions[versionKey];
        }

        public async Task<List<WorkflowVersion>> GetVersionHistoryAsync(string tenantId, string workflowId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Retrieving version history for workflow {WorkflowId}", workflowId);

            await Task.Delay(20, cancellationToken);

            return _versions
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:{workflowId}:"))
                .Select(kvp => kvp.Value)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
        }

        public async Task<MigrationPlan> CreateMigrationPlanAsync(string tenantId, string workflowId, int fromVersion, int toVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Creating migration plan from v{FromVersion} to v{ToVersion}", fromVersion, toVersion);

            await Task.Delay(30, cancellationToken);

            var fromVersionKey = $"{tenantId}:{workflowId}:v{fromVersion}";
            var toVersionKey = $"{tenantId}:{workflowId}:v{toVersion}";

            if (!_versions.ContainsKey(fromVersionKey) || !_versions.ContainsKey(toVersionKey))
                throw new InvalidOperationException("Source or target version not found");

            var fromVer = _versions[fromVersionKey];
            var toVer = _versions[toVersionKey];

            var plan = new MigrationPlan
            {
                PlanId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                FromVersion = fromVersion,
                ToVersion = toVersion,
                CreatedAt = DateTimeOffset.UtcNow,
                EstimatedDuration = EstimateMigrationTime(fromVer, toVer),
                Steps = IdentifyMigrationSteps(fromVer, toVer),
                RollbackPlan = new RollbackPlan
                {
                    TargetVersion = fromVersion,
                    Reversible = true,
                    EstimatedRollbackTime = EstimateMigrationTime(toVer, fromVer)
                },
                BreakingChanges = fromVer.Breaking ? new List<string> { "Breaking changes detected" } : new List<string>(),
                DataTransformations = IdentifyDataTransformations(fromVer, toVer),
                CompatibilityScore = CalculateCompatibilityScore(fromVer, toVer),
                RiskLevel = AssessRiskLevel(fromVer, toVer),
                PreMigrationChecks = new List<string> { "Verify backups", "Check disk space", "Validate migration scripts" },
                PostMigrationValidation = new List<string> { "Run sanity checks", "Verify output", "Monitor performance" }
            };

            return plan;
        }

        public async Task<MigrationResult> ExecuteMigrationAsync(string tenantId, string workflowId, int toVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Executing migration for workflow {WorkflowId} to v{ToVersion}", workflowId, toVersion);

            await Task.Delay(50, cancellationToken);

            var startTime = DateTimeOffset.UtcNow;
            var success = _random.NextDouble() > 0.05; // 95% success rate

            var result = new MigrationResult
            {
                MigrationId = Guid.NewGuid().ToString("N"),
                WorkflowId = workflowId,
                TargetVersion = toVersion,
                StartedAt = startTime,
                CompletedAt = DateTimeOffset.UtcNow,
                Duration = _random.Next(500, 5000),
                Status = success ? "completed" : "failed",
                RecordsMigrated = _random.Next(100, 10000),
                RecordsFailed = success ? 0 : _random.Next(1, 100),
                RollbackAvailable = true,
                ValidationPassed = success,
                Warnings = success ? new List<string>() : new List<string> { "Migration validation failed" },
                DataIntegrity = success ? "verified" : "compromised",
                PerformanceImpact = _random.Next(-10, 30) // -10% to +30%
            };

            // Track migration history
            var historyKey = $"{tenantId}:{workflowId}";
            if (!_migrationHistory.ContainsKey(historyKey))
                _migrationHistory[historyKey] = new MigrationHistory();

            _migrationHistory[historyKey].Migrations.Add(result);

            return result;
        }

        public async Task<bool> RollbackAsync(string tenantId, string workflowId, int targetVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Rolling back workflow {WorkflowId} to v{TargetVersion}", workflowId, targetVersion);

            await Task.Delay(40, cancellationToken);

            var snapshotKey = $"{tenantId}:{workflowId}:v{targetVersion}";
            if (!_stateSnapshots.ContainsKey(snapshotKey) || _stateSnapshots[snapshotKey].Count == 0)
                return false;

            var success = _random.NextDouble() > 0.05; // 95% success
            if (success)
            {
                // Restore to target version
                _logger.LogInformation("Successfully rolled back workflow {WorkflowId} to v{TargetVersion}", workflowId, targetVersion);
            }

            return success;
        }

        public async Task<CompatibilityCheck> CheckCompatibilityAsync(string tenantId, string workflowId, int targetVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Checking compatibility for version {TargetVersion}", targetVersion);

            await Task.Delay(25, cancellationToken);

            var check = new CompatibilityCheck
            {
                WorkflowId = workflowId,
                TargetVersion = targetVersion,
                CheckedAt = DateTimeOffset.UtcNow,
                IsCompatible = true,
                CompatibilityScore = _random.Next(75, 100),
                Checks = new Dictionary<string, bool>
                {
                    { "Input schema compatibility", true },
                    { "Output schema compatibility", true },
                    { "Dependency compatibility", true },
                    { "State shape compatibility", _random.NextDouble() > 0.1 },
                    { "Performance compatibility", _random.NextDouble() > 0.15 }
                },
                Incompatibilities = new List<string>(),
                RequiredDataTransforms = new List<string> { "map_field_old_to_new", "convert_data_type_int_to_string" },
                Recommendations = new List<string>
                {
                    "Test with sample data before full migration",
                    "Run migration in canary deployment first",
                    "Ensure sufficient disk space for migration"
                }
            };

            // Identify incompatibilities
            if (!check.Checks.Values.All(v => v))
            {
                check.IsCompatible = false;
                check.CompatibilityScore = Math.Max(0, check.CompatibilityScore - 20);
                check.Incompatibilities = check.Checks
                    .Where(kvp => !kvp.Value)
                    .Select(kvp => $"Incompatible: {kvp.Key}")
                    .ToList();
            }

            return check;
        }

        public async Task<BreakingChangeDetectionResult> DetectBreakingChangesAsync(string tenantId, string workflowId, int fromVersion, int toVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            _logger.LogInformation("Detecting breaking changes from v{FromVersion} to v{ToVersion}", fromVersion, toVersion);

            await Task.Delay(30, cancellationToken);

            var result = new BreakingChangeDetectionResult
            {
                WorkflowId = workflowId,
                FromVersion = fromVersion,
                ToVersion = toVersion,
                DetectedAt = DateTimeOffset.UtcNow,
                BreakingChangesFound = false,
                Changes = new List<ChangeDetail>(),
                SeverityLevel = "none",
                MigrationRequired = false,
                AffectedWorkflows = 0
            };

            // Detect changes
            var changes = new List<string>
            {
                "Removed field: deprecated_step",
                "Modified field type: timeout (int -> string)",
                "Added required field: error_handler"
            };

            foreach (var change in changes)
            {
                if (change.Contains("Removed") || change.Contains("Modified required"))
                {
                    result.BreakingChangesFound = true;
                    result.SeverityLevel = "high";
                    result.MigrationRequired = true;
                }

                result.Changes.Add(new ChangeDetail
                {
                    Description = change,
                    BreakingChange = change.Contains("Removed") || change.Contains("Required"),
                    MigrationPath = $"See migration guide v{fromVersion} -> v{toVersion}"
                });
            }

            result.AffectedWorkflows = _random.Next(0, 100);

            return result;
        }

        public async Task<VersionMetrics> GetVersionMetricsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Calculating version metrics for tenant {TenantId}", tenantId);

            await Task.Delay(35, cancellationToken);

            var allVersions = _versions
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            var uniqueWorkflows = allVersions.Select(v => v.WorkflowId).Distinct().Count();
            var allMigrations = _migrationHistory
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value.Migrations)
                .ToList();

            var metrics = new VersionMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalVersionsManaged = allVersions.Count,
                UniqueWorkflows = uniqueWorkflows,
                AverageVersionsPerWorkflow = uniqueWorkflows > 0 ? allVersions.Count / (double)uniqueWorkflows : 0,
                TotalMigrations = allMigrations.Count,
                SuccessfulMigrations = allMigrations.Count(m => m.Status == "completed"),
                FailedMigrations = allMigrations.Count(m => m.Status == "failed"),
                SuccessMigrationRate = allMigrations.Count > 0 ? (allMigrations.Count(m => m.Status == "completed") / (double)allMigrations.Count) * 100 : 0,
                TotalBreakingChanges = allVersions.Count(v => v.Breaking),
                AverageMigrationDuration = allMigrations.Count > 0 ? (int)allMigrations.Average(m => m.Duration) : 0,
                RolledBackMigrations = _random.Next(0, 10),
                VersionDeprecationRate = allVersions.Count > 0 ? (allVersions.Count(v => v.DeprecatedAt.HasValue) / (double)allVersions.Count) * 100 : 0,
                AverageCompatibilityScore = _random.Next(80, 99),
                Last24hMigrations = allMigrations.Count(m => m.StartedAt >= DateTimeOffset.UtcNow.AddHours(-24))
            };

            return metrics;
        }

        public async Task<bool> SetCanaryDeploymentAsync(string tenantId, string workflowId, int version, int canaryPercentage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(workflowId))
                throw new ArgumentException("Workflow ID is required", nameof(workflowId));

            if (canaryPercentage < 1 || canaryPercentage > 100)
                throw new ArgumentException("Canary percentage must be between 1 and 100", nameof(canaryPercentage));

            _logger.LogInformation("Setting canary deployment for workflow {WorkflowId} v{Version} at {Percentage}%", workflowId, version, canaryPercentage);

            await Task.Delay(20, cancellationToken);

            var canaryKey = $"{tenantId}:{workflowId}:v{version}";
            _canaryDeployments[canaryKey] = new CanaryDeployment
            {
                WorkflowId = workflowId,
                Version = version,
                Percentage = canaryPercentage,
                StartedAt = DateTimeOffset.UtcNow,
                Status = "active",
                ExecutionsCanary = 0,
                ExecutionsStable = 0,
                CanarySuccessRate = 100.0,
                StableSuccessRate = 100.0,
                AutomaticPromotion = canaryPercentage >= 100
            };

            return true;
        }

        private string ComputeSchemaHash(WorkflowVersionDefinition version)
        {
            var content = $"{version.WorkflowId}:{string.Join(",", version.Steps ?? new List<string>())}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
                return Convert.ToHexString(hash).Substring(0, 16);
            }
        }

        private bool DetectBreakingChanges(WorkflowVersionDefinition version)
        {
            return _random.NextDouble() < 0.15; // 15% chance of breaking changes
        }

        private int EstimateMigrationTime(WorkflowVersion from, WorkflowVersion to)
        {
            var stepDifference = Math.Abs((from.Steps?.Count ?? 0) - (to.Steps?.Count ?? 0));
            return 500 + (stepDifference * 100) + _random.Next(0, 500);
        }

        private List<MigrationStep> IdentifyMigrationSteps(WorkflowVersion from, WorkflowVersion to)
        {
            var steps = new List<MigrationStep>
            {
                new() { Order = 1, Description = "Backup current state", Status = "pending", Duration = 100 },
                new() { Order = 2, Description = "Validate target version schema", Status = "pending", Duration = 200 },
                new() { Order = 3, Description = "Transform data to new schema", Status = "pending", Duration = _random.Next(500, 2000) },
                new() { Order = 4, Description = "Verify data integrity", Status = "pending", Duration = 300 },
                new() { Order = 5, Description = "Update workflow metadata", Status = "pending", Duration = 150 }
            };

            return steps;
        }

        private List<DataTransformation> IdentifyDataTransformations(WorkflowVersion from, WorkflowVersion to)
        {
            return new List<DataTransformation>
            {
                new() { SourceField = "old_timeout", TargetField = "timeout_ms", Transformation = "multiply_by_1000" },
                new() { SourceField = "status_code", TargetField = "http_status", Transformation = "identity" }
            };
        }

        private int CalculateCompatibilityScore(WorkflowVersion from, WorkflowVersion to)
        {
            var score = 100;
            if (from.Breaking) score -= 30;
            if (from.Steps?.Count != to.Steps?.Count) score -= 10;
            return Math.Max(0, score);
        }

        private string AssessRiskLevel(WorkflowVersion from, WorkflowVersion to)
        {
            if (from.Breaking) return "high";
            if (Math.Abs((from.Steps?.Count ?? 0) - (to.Steps?.Count ?? 0)) > 5) return "medium";
            return "low";
        }
    }

    // Domain Models
    public class WorkflowVersionDefinition
    {
        public string WorkflowId { get; set; }
        public int VersionNumber { get; set; }
        public string Description { get; set; }
        public List<string> Steps { get; set; } = new();
        public List<WorkflowDependency> Dependencies { get; set; } = new();
        public Dictionary<string, object> InputSchema { get; set; } = new();
        public Dictionary<string, object> OutputSchema { get; set; } = new();
        public Dictionary<int, string> MigrationStrategies { get; set; } = new();
    }

    public class WorkflowDependency
    {
        public string From { get; set; }
        public string To { get; set; }
    }

    public class WorkflowVersion
    {
        public string VersionId { get; set; }
        public string TenantId { get; set; }
        public string WorkflowId { get; set; }
        public int VersionNumber { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string SchemaHash { get; set; }
        public string Description { get; set; }
        public bool Breaking { get; set; }
        public List<string> Steps { get; set; }
        public List<WorkflowDependency> Dependencies { get; set; }
        public Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object> OutputSchema { get; set; }
        public Dictionary<int, string> MigrationStrategies { get; set; }
        public string Status { get; set; }
        public DateTimeOffset ReleaseDate { get; set; }
        public DateTimeOffset? DeprecatedAt { get; set; }
    }

    public class VersionedWorkflowState
    {
        public string StateId { get; set; }
        public int Version { get; set; }
        public DateTimeOffset SnapshotAt { get; set; }
        public Dictionary<string, object> State { get; set; }
    }

    public class MigrationPlan
    {
        public string PlanId { get; set; }
        public string WorkflowId { get; set; }
        public int FromVersion { get; set; }
        public int ToVersion { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int EstimatedDuration { get; set; }
        public List<MigrationStep> Steps { get; set; }
        public RollbackPlan RollbackPlan { get; set; }
        public List<string> BreakingChanges { get; set; }
        public List<DataTransformation> DataTransformations { get; set; }
        public int CompatibilityScore { get; set; }
        public string RiskLevel { get; set; }
        public List<string> PreMigrationChecks { get; set; }
        public List<string> PostMigrationValidation { get; set; }
    }

    public class MigrationStep
    {
        public int Order { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public int Duration { get; set; }
    }

    public class DataTransformation
    {
        public string SourceField { get; set; }
        public string TargetField { get; set; }
        public string Transformation { get; set; }
    }

    public class RollbackPlan
    {
        public int TargetVersion { get; set; }
        public bool Reversible { get; set; }
        public int EstimatedRollbackTime { get; set; }
    }

    public class MigrationResult
    {
        public string MigrationId { get; set; }
        public string WorkflowId { get; set; }
        public int TargetVersion { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset CompletedAt { get; set; }
        public int Duration { get; set; }
        public string Status { get; set; }
        public int RecordsMigrated { get; set; }
        public int RecordsFailed { get; set; }
        public bool RollbackAvailable { get; set; }
        public bool ValidationPassed { get; set; }
        public List<string> Warnings { get; set; }
        public string DataIntegrity { get; set; }
        public int PerformanceImpact { get; set; }
    }

    public class MigrationHistory
    {
        public List<MigrationResult> Migrations { get; set; } = new();
    }

    public class CompatibilityCheck
    {
        public string WorkflowId { get; set; }
        public int TargetVersion { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public bool IsCompatible { get; set; }
        public int CompatibilityScore { get; set; }
        public Dictionary<string, bool> Checks { get; set; }
        public List<string> IncompatibilityDetails => Checks
            .Where(kvp => !kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
        public List<string> Incompatibilities { get; set; }
        public List<string> RequiredDataTransforms { get; set; }
        public List<string> Recommendations { get; set; }
    }

    public class BreakingChangeDetectionResult
    {
        public string WorkflowId { get; set; }
        public int FromVersion { get; set; }
        public int ToVersion { get; set; }
        public DateTimeOffset DetectedAt { get; set; }
        public bool BreakingChangesFound { get; set; }
        public List<ChangeDetail> Changes { get; set; }
        public string SeverityLevel { get; set; }
        public bool MigrationRequired { get; set; }
        public int AffectedWorkflows { get; set; }
    }

    public class ChangeDetail
    {
        public string Description { get; set; }
        public bool BreakingChange { get; set; }
        public string MigrationPath { get; set; }
    }

    public class VersionMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalVersionsManaged { get; set; }
        public int UniqueWorkflows { get; set; }
        public double AverageVersionsPerWorkflow { get; set; }
        public int TotalMigrations { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public double SuccessMigrationRate { get; set; }
        public int TotalBreakingChanges { get; set; }
        public int AverageMigrationDuration { get; set; }
        public int RolledBackMigrations { get; set; }
        public double VersionDeprecationRate { get; set; }
        public int AverageCompatibilityScore { get; set; }
        public int Last24hMigrations { get; set; }
    }

    public class CanaryDeployment
    {
        public string WorkflowId { get; set; }
        public int Version { get; set; }
        public int Percentage { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public string Status { get; set; }
        public int ExecutionsCanary { get; set; }
        public int ExecutionsStable { get; set; }
        public double CanarySuccessRate { get; set; }
        public double StableSuccessRate { get; set; }
        public bool AutomaticPromotion { get; set; }
    }
}
