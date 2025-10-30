using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Versioning;

/// <summary>
/// Manages version compatibility and migration for workflow definitions
/// Based on 2024/2025 research:
/// - GitOps framework for version control (Argo CD, Flux patterns)
/// - Semantic versioning best practices (WorkOS 2025)
/// - Soft deprecation strategy (mark deprecated but continue supporting)
/// - Automated migration with dependency mapping
/// - Clear changelog and migration path communication
/// Solves Issue #6: Version compatibility across different Loco versions
/// </summary>
public class VersionCompatibilityManager
{
    private readonly ILogger<VersionCompatibilityManager> _logger;
    private readonly Dictionary<string, WorkflowMigration> _migrations;
    private const string CurrentVersion = "1.0.0";

    public VersionCompatibilityManager(ILogger<VersionCompatibilityManager> logger)
    {
        _logger = logger;
        _migrations = new Dictionary<string, WorkflowMigration>();
        RegisterMigrations();
    }

    /// <summary>
    /// Register all available migrations from older versions to newer versions
    /// Migration path: 0.1.0 -> 0.2.0 -> 1.0.0
    /// </summary>
    private void RegisterMigrations()
    {
        // Migration from 0.1.0 to 0.2.0
        _migrations["0.1.0->0.2.0"] = new WorkflowMigration
        {
            FromVersion = "0.1.0",
            ToVersion = "0.2.0",
            Description = "Adds support for platform-specific actions (Android, iOS)",
            BreakingChanges = new List<string>
            {
                "ActionResult.Success/Error renamed to ActionResult.Succeeded/Failed",
                "IPlatformProvider now requires GetPlatformInfo() method"
            },
            MigrationSteps = new List<string>
            {
                "Replace ActionResult.Success() with ActionResult.Succeeded()",
                "Replace ActionResult.Error() with ActionResult.Failed()",
                "Add 'platform' field to workflow definition if using platform-specific actions"
            },
            AutoMigrate = (workflow) =>
            {
                // Add version field if missing
                if (workflow.RootElement.TryGetProperty("version", out _) == false)
                {
                    var updatedWorkflow = new Dictionary<string, object>();
                    foreach (var prop in workflow.RootElement.EnumerateObject())
                    {
                        updatedWorkflow[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                    }
                    updatedWorkflow["version"] = "0.2.0";
                    return JsonDocument.Parse(JsonSerializer.Serialize(updatedWorkflow));
                }
                return workflow;
            }
        };

        // Migration from 0.2.0 to 1.0.0
        _migrations["0.2.0->1.0.0"] = new WorkflowMigration
        {
            FromVersion = "0.2.0",
            ToVersion = "1.0.0",
            Description = "Adds performance optimization, battery management, and visual debugging",
            BreakingChanges = new List<string>
            {
                "WorkflowExecutor now requires performance optimizer injection",
                "Platform providers must implement battery constraint evaluation"
            },
            MigrationSteps = new List<string>
            {
                "Add 'performance_settings' section for battery-aware execution",
                "Add 'debug_settings' for visual debugger integration",
                "Update platform field to specify exact platform version (e.g., 'android:34', 'ios:15.0')"
            },
            AutoMigrate = (workflow) =>
            {
                var updatedWorkflow = new Dictionary<string, object>();
                foreach (var prop in workflow.RootElement.EnumerateObject())
                {
                    updatedWorkflow[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
                }

                // Update version
                updatedWorkflow["version"] = "1.0.0";

                // Add default performance settings if not present
                if (!workflow.RootElement.TryGetProperty("performance_settings", out _))
                {
                    updatedWorkflow["performance_settings"] = new Dictionary<string, object>
                    {
                        { "battery_aware", true },
                        { "power_mode", "auto" },
                        { "optimize_network", true }
                    };
                }

                return JsonDocument.Parse(JsonSerializer.Serialize(updatedWorkflow));
            }
        };
    }

    /// <summary>
    /// Check if a workflow version is compatible with the current Loco version
    /// </summary>
    public CompatibilityCheckResult CheckCompatibility(JsonDocument workflow)
    {
        var workflowVersion = GetWorkflowVersion(workflow);
        var result = new CompatibilityCheckResult
        {
            WorkflowVersion = workflowVersion,
            CurrentLocoVersion = CurrentVersion,
            IsCompatible = true,
            RequiresMigration = false
        };

        // Check if workflow version matches current version
        if (workflowVersion == CurrentVersion)
        {
            result.Message = "Workflow is fully compatible with current Loco version";
            return result;
        }

        // Check if migration path exists
        var migrationPath = FindMigrationPath(workflowVersion, CurrentVersion);
        if (migrationPath.Count == 0)
        {
            result.IsCompatible = false;
            result.Message = $"No migration path found from version {workflowVersion} to {CurrentVersion}";
            result.Warnings.Add($"Workflow version {workflowVersion} is not supported");
            return result;
        }

        // Workflow can be migrated
        result.RequiresMigration = true;
        result.MigrationPath = migrationPath;
        result.Message = $"Workflow requires migration from {workflowVersion} to {CurrentVersion}";

        // Collect all breaking changes and migration steps
        foreach (var step in migrationPath)
        {
            if (_migrations.TryGetValue(step, out var migration))
            {
                result.BreakingChanges.AddRange(migration.BreakingChanges);
                result.MigrationSteps.AddRange(migration.MigrationSteps);
            }
        }

        return result;
    }

    /// <summary>
    /// Automatically migrate workflow to current version
    /// </summary>
    public async Task<MigrationResult> MigrateWorkflowAsync(
        JsonDocument workflow,
        CancellationToken cancellationToken = default)
    {
        var startVersion = GetWorkflowVersion(workflow);
        _logger.LogInformation("Starting workflow migration from version {FromVersion} to {ToVersion}",
            startVersion, CurrentVersion);

        var result = new MigrationResult
        {
            OriginalVersion = startVersion,
            TargetVersion = CurrentVersion,
            Success = false
        };

        try
        {
            // Find migration path
            var migrationPath = FindMigrationPath(startVersion, CurrentVersion);
            if (migrationPath.Count == 0)
            {
                result.ErrorMessage = $"No migration path found from {startVersion} to {CurrentVersion}";
                _logger.LogError("Migration failed: {Error}", result.ErrorMessage);
                return result;
            }

            // Apply migrations in sequence
            var currentWorkflow = workflow;
            foreach (var step in migrationPath)
            {
                if (_migrations.TryGetValue(step, out var migration))
                {
                    _logger.LogInformation("Applying migration: {Description}", migration.Description);

                    // Apply auto-migration
                    currentWorkflow = migration.AutoMigrate(currentWorkflow);

                    // Log migration details
                    result.AppliedMigrations.Add(new MigrationStepResult
                    {
                        FromVersion = migration.FromVersion,
                        ToVersion = migration.ToVersion,
                        Description = migration.Description,
                        Success = true
                    });

                    await Task.Delay(10, cancellationToken); // Simulate async work
                }
            }

            result.MigratedWorkflow = currentWorkflow;
            result.Success = true;
            result.FinalVersion = CurrentVersion;

            _logger.LogInformation("Workflow migration completed successfully: {FromVersion} -> {ToVersion}",
                startVersion, CurrentVersion);

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Migration failed: {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "Workflow migration failed");
            return result;
        }
    }

    /// <summary>
    /// Get deprecation warnings for workflow features
    /// Based on soft deprecation strategy (2024 best practices)
    /// </summary>
    public List<DeprecationWarning> GetDeprecationWarnings(JsonDocument workflow)
    {
        var warnings = new List<DeprecationWarning>();
        var version = GetWorkflowVersion(workflow);

        // Check for deprecated features
        if (workflow.RootElement.TryGetProperty("actions", out var actions))
        {
            foreach (var action in actions.EnumerateArray())
            {
                if (action.TryGetProperty("type", out var actionType))
                {
                    var type = actionType.GetString();

                    // Example: ActionResult.Success/Error deprecated in 0.2.0
                    if (version == "0.1.0" && type?.Contains("notification") == true)
                    {
                        warnings.Add(new DeprecationWarning
                        {
                            Feature = "ActionResult.Success/Error",
                            DeprecatedInVersion = "0.2.0",
                            WillBeRemovedInVersion = "2.0.0",
                            Replacement = "ActionResult.Succeeded/Failed",
                            Message = "ActionResult.Success() and ActionResult.Error() are deprecated. Use ActionResult.Succeeded() and ActionResult.Failed() instead.",
                            Severity = DeprecationSeverity.Warning
                        });
                    }
                }
            }
        }

        return warnings;
    }

    /// <summary>
    /// Generate migration report with detailed changelog
    /// Based on 2025 best practices: clear changelog communication
    /// </summary>
    public string GenerateMigrationReport(CompatibilityCheckResult compatibility)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== Loco Workflow Migration Report ===");
        report.AppendLine($"Workflow Version: {compatibility.WorkflowVersion}");
        report.AppendLine($"Current Loco Version: {compatibility.CurrentLocoVersion}");
        report.AppendLine($"Compatible: {compatibility.IsCompatible}");
        report.AppendLine($"Requires Migration: {compatibility.RequiresMigration}");
        report.AppendLine();

        if (compatibility.RequiresMigration)
        {
            report.AppendLine("Migration Path:");
            foreach (var step in compatibility.MigrationPath)
            {
                report.AppendLine($"  - {step}");
            }
            report.AppendLine();

            if (compatibility.BreakingChanges.Count > 0)
            {
                report.AppendLine("Breaking Changes:");
                foreach (var change in compatibility.BreakingChanges)
                {
                    report.AppendLine($"  ⚠️  {change}");
                }
                report.AppendLine();
            }

            if (compatibility.MigrationSteps.Count > 0)
            {
                report.AppendLine("Migration Steps:");
                for (int i = 0; i < compatibility.MigrationSteps.Count; i++)
                {
                    report.AppendLine($"  {i + 1}. {compatibility.MigrationSteps[i]}");
                }
                report.AppendLine();
            }
        }

        if (compatibility.Warnings.Count > 0)
        {
            report.AppendLine("Warnings:");
            foreach (var warning in compatibility.Warnings)
            {
                report.AppendLine($"  ⚠️  {warning}");
            }
        }

        return report.ToString();
    }

    private string GetWorkflowVersion(JsonDocument workflow)
    {
        if (workflow.RootElement.TryGetProperty("version", out var versionElement))
        {
            return versionElement.GetString() ?? "0.1.0";
        }
        return "0.1.0"; // Default to earliest version if not specified
    }

    private List<string> FindMigrationPath(string fromVersion, string toVersion)
    {
        // Simple sequential migration path finder
        // In production, this could use graph traversal for complex migration graphs
        var path = new List<string>();

        var versions = new[] { "0.1.0", "0.2.0", "1.0.0" };
        var fromIndex = Array.IndexOf(versions, fromVersion);
        var toIndex = Array.IndexOf(versions, toVersion);

        if (fromIndex == -1 || toIndex == -1 || fromIndex >= toIndex)
        {
            return path; // No migration needed or invalid versions
        }

        // Build sequential migration path
        for (int i = fromIndex; i < toIndex; i++)
        {
            var migrationKey = $"{versions[i]}->{versions[i + 1]}";
            if (_migrations.ContainsKey(migrationKey))
            {
                path.Add(migrationKey);
            }
        }

        return path;
    }
}

/// <summary>
/// Represents a migration from one version to another
/// </summary>
public class WorkflowMigration
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> BreakingChanges { get; set; } = new();
    public List<string> MigrationSteps { get; set; } = new();

    /// <summary>
    /// Automatic migration function that transforms the workflow JSON
    /// </summary>
    public Func<JsonDocument, JsonDocument> AutoMigrate { get; set; } = (doc) => doc;
}

/// <summary>
/// Result of compatibility check
/// </summary>
public class CompatibilityCheckResult
{
    public string WorkflowVersion { get; set; } = string.Empty;
    public string CurrentLocoVersion { get; set; } = string.Empty;
    public bool IsCompatible { get; set; }
    public bool RequiresMigration { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> MigrationPath { get; set; } = new();
    public List<string> BreakingChanges { get; set; } = new();
    public List<string> MigrationSteps { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of workflow migration
/// </summary>
public class MigrationResult
{
    public string OriginalVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string FinalVersion { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public JsonDocument? MigratedWorkflow { get; set; }
    public List<MigrationStepResult> AppliedMigrations { get; set; } = new();
}

public class MigrationStepResult
{
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Deprecation warning for workflow features
/// Based on soft deprecation strategy (2024 best practices)
/// </summary>
public class DeprecationWarning
{
    public string Feature { get; set; } = string.Empty;
    public string DeprecatedInVersion { get; set; } = string.Empty;
    public string WillBeRemovedInVersion { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DeprecationSeverity Severity { get; set; }
}

public enum DeprecationSeverity
{
    Info,
    Warning,
    Error
}
