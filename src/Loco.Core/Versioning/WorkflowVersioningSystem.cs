// Phase 6: Workflow Versioning System
// Version management, rollback, release notes, and deployment tracking
// Enterprise-grade version control with semantic versioning and audit trails

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Versioning;

/// <summary>
/// Semantic version (Major.Minor.Patch)
/// </summary>
public class SemanticVersion : IComparable<SemanticVersion>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public string? PreRelease { get; set; } // alpha, beta, rc

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}{(PreRelease != null ? $"-{PreRelease}" : "")}";

    public int CompareTo(SemanticVersion? other)
    {
        if (other == null) return 1;
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }
}

/// <summary>
/// Workflow version release status
/// </summary>
public enum ReleaseStatus
{
    Draft = 0,         // Work in progress
    Beta = 1,          // Testing version
    Released = 2,      // Production version
    Deprecated = 3,    // No longer recommended
    Archived = 4,      // Retired version
}

/// <summary>
/// Workflow version metadata
/// </summary>
public class WorkflowVersion
{
    public string VersionId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public SemanticVersion Version { get; set; } = new();
    public ReleaseStatus Status { get; set; }
    public string Definition { get; set; } = string.Empty; // Serialized workflow definition
    public string? ReleaseNotes { get; set; }
    public string? Changelog { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public int ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
    public List<string> Tags { get; set; } = new(); // e.g., "stable", "performance", "security-fix"
}

/// <summary>
/// Workflow version comparison
/// </summary>
public class VersionComparison
{
    public SemanticVersion VersionA { get; set; } = new();
    public SemanticVersion VersionB { get; set; } = new();
    public List<string> AddedSteps { get; set; } = new();
    public List<string> RemovedSteps { get; set; } = new();
    public List<string> ModifiedSteps { get; set; } = new();
    public List<string> ParameterChanges { get; set; } = new();
    public string? BreakingChanges { get; set; }
}

/// <summary>
/// Deployment record
/// </summary>
public class Deployment
{
    public string DeploymentId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public SemanticVersion Version { get; set; } = new();
    public string Environment { get; set; } = string.Empty; // staging, production
    public DateTime DeployedAt { get; set; }
    public string DeployedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "success"; // success, failed, rolled_back
    public int DeployedInstances { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Workflow versioning interface
/// </summary>
public interface IWorkflowVersioningSystem
{
    Task<WorkflowVersion> CreateVersionAsync(
        string workflowId,
        string definition,
        SemanticVersion version,
        string releasNotes,
        string changedBy,
        CancellationToken ct = default);

    Task<WorkflowVersion?> GetVersionAsync(
        string workflowId,
        SemanticVersion version,
        CancellationToken ct = default);

    Task<List<WorkflowVersion>> GetVersionHistoryAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<WorkflowVersion?> GetCurrentVersionAsync(
        string workflowId,
        CancellationToken ct = default);

    Task ReleaseVersionAsync(
        string versionId,
        string releasedBy,
        CancellationToken ct = default);

    Task RollbackAsync(
        string workflowId,
        SemanticVersion targetVersion,
        string rolledBackBy,
        CancellationToken ct = default);

    Task<VersionComparison> CompareVersionsAsync(
        string workflowId,
        SemanticVersion versionA,
        SemanticVersion versionB,
        CancellationToken ct = default);

    Task<Deployment> DeployAsync(
        string workflowId,
        SemanticVersion version,
        string environment,
        string deployedBy,
        CancellationToken ct = default);

    Task<List<Deployment>> GetDeploymentHistoryAsync(
        string workflowId,
        CancellationToken ct = default);

    Task PromoteVersionAsync(
        string workflowId,
        SemanticVersion version,
        string fromEnv,
        string toEnv,
        CancellationToken ct = default);
}

/// <summary>
/// Workflow versioning system implementation
/// </summary>
public class WorkflowVersioningSystem : IWorkflowVersioningSystem
{
    private readonly ILogger<WorkflowVersioningSystem> _logger;
    private readonly Dictionary<string, List<WorkflowVersion>> _versions;
    private readonly Dictionary<string, Deployment> _deployments;
    private readonly Dictionary<string, SemanticVersion> _currentVersions;

    public WorkflowVersioningSystem(ILogger<WorkflowVersioningSystem> logger)
    {
        _logger = logger;
        _versions = new Dictionary<string, List<WorkflowVersion>>();
        _deployments = new Dictionary<string, Deployment>();
        _currentVersions = new Dictionary<string, SemanticVersion>();
    }

    /// <summary>
    /// Create new workflow version
    /// </summary>
    public async Task<WorkflowVersion> CreateVersionAsync(
        string workflowId,
        string definition,
        SemanticVersion version,
        string releaseNotes,
        string changedBy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_versions.ContainsKey(workflowId))
        {
            _versions[workflowId] = new List<WorkflowVersion>();
        }

        // Check for duplicate version
        if (_versions[workflowId].Any(v => v.Version.CompareTo(version) == 0))
        {
            throw new InvalidOperationException($"Version {version} already exists");
        }

        var workflowVersion = new WorkflowVersion
        {
            WorkflowId = workflowId,
            Version = version,
            Status = ReleaseStatus.Draft,
            Definition = definition,
            ReleaseNotes = releaseNotes,
            ChangedBy = changedBy,
            CreatedAt = DateTime.UtcNow,
        };

        _versions[workflowId].Add(workflowVersion);

        _logger.LogInformation(
            "Workflow version created: {WorkflowId}, Version: {Version}, Status: Draft",
            workflowId, version);

        return workflowVersion;
    }

    /// <summary>
    /// Get specific version
    /// </summary>
    public async Task<WorkflowVersion?> GetVersionAsync(
        string workflowId,
        SemanticVersion version,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_versions.TryGetValue(workflowId, out var versions))
        {
            return versions.FirstOrDefault(v => v.Version.CompareTo(version) == 0);
        }

        return null;
    }

    /// <summary>
    /// Get version history (all versions)
    /// </summary>
    public async Task<List<WorkflowVersion>> GetVersionHistoryAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_versions.TryGetValue(workflowId, out var versions))
        {
            return versions.OrderByDescending(v => v.Version).ToList();
        }

        return new List<WorkflowVersion>();
    }

    /// <summary>
    /// Get current (active) version
    /// </summary>
    public async Task<WorkflowVersion?> GetCurrentVersionAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_currentVersions.TryGetValue(workflowId, out var currentVersion) &&
            _versions.TryGetValue(workflowId, out var versions))
        {
            return versions.FirstOrDefault(v => v.Version.CompareTo(currentVersion) == 0 && v.Status == ReleaseStatus.Released);
        }

        // If no current version set, return latest released
        if (_versions.TryGetValue(workflowId, out var allVersions))
        {
            return allVersions
                .Where(v => v.Status == ReleaseStatus.Released)
                .OrderByDescending(v => v.Version)
                .FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Release version (move from draft to production)
    /// </summary>
    public async Task ReleaseVersionAsync(
        string versionId,
        string releasedBy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var versions in _versions.Values)
        {
            var version = versions.FirstOrDefault(v => v.VersionId == versionId);
            if (version != null)
            {
                version.Status = ReleaseStatus.Released;
                version.ReleasedAt = DateTime.UtcNow;
                _currentVersions[version.WorkflowId] = version.Version;

                _logger.LogInformation(
                    "Version released: {WorkflowId} v{Version}, ReleasedBy: {ReleasedBy}",
                    version.WorkflowId, version.Version, releasedBy);

                return;
            }
        }

        throw new KeyNotFoundException($"Version not found: {versionId}");
    }

    /// <summary>
    /// Rollback to previous version
    /// </summary>
    public async Task RollbackAsync(
        string workflowId,
        SemanticVersion targetVersion,
        string rolledBackBy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_versions.TryGetValue(workflowId, out var versions))
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        var targetVer = versions.FirstOrDefault(v => v.Version.CompareTo(targetVersion) == 0 && v.Status == ReleaseStatus.Released);
        if (targetVer == null)
        {
            throw new InvalidOperationException($"Target version not found or not released: {targetVersion}");
        }

        _currentVersions[workflowId] = targetVersion;

        _logger.LogWarning(
            "Workflow rolled back: {WorkflowId}, TargetVersion: {Version}, RolledBackBy: {User}",
            workflowId, targetVersion, rolledBackBy);
    }

    /// <summary>
    /// Compare two versions for changes
    /// </summary>
    public async Task<VersionComparison> CompareVersionsAsync(
        string workflowId,
        SemanticVersion versionA,
        SemanticVersion versionB,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var verA = await GetVersionAsync(workflowId, versionA, ct);
        var verB = await GetVersionAsync(workflowId, versionB, ct);

        if (verA == null || verB == null)
        {
            throw new KeyNotFoundException("One or both versions not found");
        }

        var comparison = new VersionComparison
        {
            VersionA = versionA,
            VersionB = versionB,
        };

        // In production, parse and compare workflow definitions
        // For now, mock the comparison
        comparison.AddedSteps = new List<string> { "new-validation-step" };
        comparison.ModifiedSteps = new List<string> { "payment-processing" };
        comparison.ParameterChanges = new List<string> { "timeout increased from 30s to 60s" };

        return comparison;
    }

    /// <summary>
    /// Deploy version to environment
    /// </summary>
    public async Task<Deployment> DeployAsync(
        string workflowId,
        SemanticVersion version,
        string environment,
        string deployedBy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var workflowVersion = await GetVersionAsync(workflowId, version, ct);
        if (workflowVersion == null)
        {
            throw new KeyNotFoundException($"Version not found: {version}");
        }

        var deployment = new Deployment
        {
            WorkflowId = workflowId,
            Version = version,
            Environment = environment,
            DeployedAt = DateTime.UtcNow,
            DeployedBy = deployedBy,
            Status = "success",
            DeployedInstances = 3, // Kubernetes replicas
        };

        var key = $"{workflowId}-{environment}";
        _deployments[key] = deployment;

        _logger.LogInformation(
            "Workflow deployed: {WorkflowId} v{Version} to {Environment}, DeployedBy: {User}",
            workflowId, version, environment, deployedBy);

        return deployment;
    }

    /// <summary>
    /// Get deployment history
    /// </summary>
    public async Task<List<Deployment>> GetDeploymentHistoryAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _deployments.Values
            .Where(d => d.WorkflowId == workflowId)
            .OrderByDescending(d => d.DeployedAt)
            .ToList();
    }

    /// <summary>
    /// Promote version across environments (staging → production)
    /// </summary>
    public async Task PromoteVersionAsync(
        string workflowId,
        SemanticVersion version,
        string fromEnv,
        string toEnv,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        // Get source environment deployment
        var sourceKey = $"{workflowId}-{fromEnv}";
        if (!_deployments.TryGetValue(sourceKey, out var sourceDeployment) ||
            sourceDeployment.Version.CompareTo(version) != 0)
        {
            throw new InvalidOperationException($"Version not deployed in {fromEnv}");
        }

        // Deploy to target environment
        var targetDeployment = new Deployment
        {
            WorkflowId = workflowId,
            Version = version,
            Environment = toEnv,
            DeployedAt = DateTime.UtcNow,
            DeployedBy = "system",
            Status = "success",
            DeployedInstances = 5, // Production has more replicas
        };

        var targetKey = $"{workflowId}-{toEnv}";
        _deployments[targetKey] = targetDeployment;

        _logger.LogInformation(
            "Version promoted: {WorkflowId} v{Version} from {FromEnv} to {ToEnv}",
            workflowId, version, fromEnv, toEnv);
    }
}
