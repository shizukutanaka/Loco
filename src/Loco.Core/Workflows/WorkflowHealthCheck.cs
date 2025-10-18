using System.Text;
using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Health check severity levels.
/// </summary>
public enum HealthSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// A single health check issue.
/// </summary>
public class HealthIssue
{
    public HealthSeverity Severity { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Location { get; set; }
    public string? Suggestion { get; set; }
}

/// <summary>
/// Health check result for a workflow.
/// </summary>
public class WorkflowHealthReport
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public bool IsHealthy { get; set; }
    public int Score { get; set; } // 0-100
    public List<HealthIssue> Issues { get; set; } = new();
    public Dictionary<string, int> IssueCountBySeverity { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performs comprehensive health checks on workflows.
/// </summary>
public class WorkflowHealthChecker
{
    private readonly List<HealthIssue> _issues = new();

    /// <summary>
    /// Performs a complete health check on a workflow.
    /// </summary>
    public WorkflowHealthReport CheckWorkflow(WorkflowDefinition workflow)
    {
        _issues.Clear();

        // Run all checks
        CheckBasicStructure(workflow);
        CheckSteps(workflow);
        CheckDependencies(workflow);
        CheckSchedule(workflow);
        CheckTiming(workflow);
        CheckHooks(workflow);
        CheckEnvironments(workflow);
        CheckVariables(workflow);
        CheckBestPractices(workflow);
        CheckPerformance(workflow);

        // Calculate score
        int score = CalculateScore();
        bool isHealthy = score >= 70 && !_issues.Any(i => i.Severity == HealthSeverity.Critical);

        var report = new WorkflowHealthReport
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            IsHealthy = isHealthy,
            Score = score,
            Issues = new List<HealthIssue>(_issues),
            IssueCountBySeverity = _issues
                .GroupBy(i => i.Severity)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };

        return report;
    }

    /// <summary>
    /// Checks basic workflow structure.
    /// </summary>
    private void CheckBasicStructure(WorkflowDefinition workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow.Id))
            AddIssue(HealthSeverity.Critical, "Structure", "Workflow ID is missing", suggestion: "Add a unique ID to the workflow");

        if (string.IsNullOrWhiteSpace(workflow.Name))
            AddIssue(HealthSeverity.Error, "Structure", "Workflow name is missing", suggestion: "Add a descriptive name");

        if (string.IsNullOrWhiteSpace(workflow.Description))
            AddIssue(HealthSeverity.Warning, "Documentation", "Workflow description is missing", suggestion: "Add a description explaining the workflow's purpose");

        if (workflow.Steps == null || workflow.Steps.Count == 0)
            AddIssue(HealthSeverity.Critical, "Structure", "Workflow has no steps", suggestion: "Add at least one step to the workflow");
    }

    /// <summary>
    /// Checks workflow steps for issues.
    /// </summary>
    private void CheckSteps(WorkflowDefinition workflow)
    {
        if (workflow.Steps == null) return;

        var stepIds = new HashSet<string>();

        for (int i = 0; i < workflow.Steps.Count; i++)
        {
            var step = workflow.Steps[i];
            var location = $"Step {i + 1} ({step.Id})";

            // Check step ID
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                AddIssue(HealthSeverity.Critical, "Steps", $"Step at index {i} has no ID", location, "Add a unique ID to each step");
            }
            else
            {
                if (stepIds.Contains(step.Id))
                    AddIssue(HealthSeverity.Critical, "Steps", $"Duplicate step ID: {step.Id}", location, "Ensure all step IDs are unique");
                else
                    stepIds.Add(step.Id);
            }

            // Check step name
            if (string.IsNullOrWhiteSpace(step.Name))
                AddIssue(HealthSeverity.Warning, "Steps", $"Step {step.Id} has no name", location, "Add a descriptive name");

            // Check step type
            if (string.IsNullOrWhiteSpace(step.Type))
                AddIssue(HealthSeverity.Critical, "Steps", $"Step {step.Id} has no type", location, "Specify a step type (log, http, etc.)");

            // Check HTTP steps
            if (step.Type == "http")
            {
                if (string.IsNullOrWhiteSpace(step.Url))
                    AddIssue(HealthSeverity.Critical, "Steps", $"HTTP step {step.Id} has no URL", location, "Add a URL for the HTTP request");

                if (!string.IsNullOrWhiteSpace(step.Url))
                {
                    if (!step.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !step.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        AddIssue(HealthSeverity.Error, "Steps", $"HTTP step {step.Id} has invalid URL format", location, "URL should start with http:// or https://");
                    }
                }
            }

            // Check timeouts
            if (step.TimeoutSeconds.HasValue && step.TimeoutSeconds.Value <= 0)
                AddIssue(HealthSeverity.Error, "Steps", $"Step {step.Id} has invalid timeout: {step.TimeoutSeconds}", location, "Timeout must be positive");

            // Check retry configuration
            if (step.RetryCount.HasValue && step.RetryCount.Value < 0)
                AddIssue(HealthSeverity.Error, "Steps", $"Step {step.Id} has invalid retry count: {step.RetryCount}", location, "Retry count must be non-negative");

            if (!string.IsNullOrWhiteSpace(step.RetryDelay) && !TimeSpan.TryParse(step.RetryDelay, out _))
                AddIssue(HealthSeverity.Error, "Steps", $"Step {step.Id} has invalid retry delay format: {step.RetryDelay}", location, "Retry delay must be in TimeSpan format (e.g., '00:00:02')");
        }

        // Check for excessive steps
        if (workflow.Steps.Count > 100)
            AddIssue(HealthSeverity.Warning, "Performance", $"Workflow has {workflow.Steps.Count} steps", suggestion: "Consider breaking down into smaller workflows or using includes");
    }

    /// <summary>
    /// Checks step dependencies for issues.
    /// </summary>
    private void CheckDependencies(WorkflowDefinition workflow)
    {
        if (workflow.Steps == null || workflow.Steps.Count == 0) return;

        var hasAnyDependencies = workflow.Steps.Any(s =>
            (s.DependsOn != null && s.DependsOn.Count > 0) ||
            (s.Dependencies != null && s.Dependencies.Count > 0));

        if (!hasAnyDependencies) return;

        try
        {
            var analyzer = new DependencyAnalyzer(workflow.Steps);
            var (isValid, errors) = analyzer.ValidateDependencies();

            if (!isValid)
            {
                foreach (var error in errors)
                {
                    AddIssue(HealthSeverity.Critical, "Dependencies", error, suggestion: "Fix dependency configuration");
                }
            }
        }
        catch (Exception ex)
        {
            AddIssue(HealthSeverity.Error, "Dependencies", $"Failed to validate dependencies: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks schedule configuration.
    /// </summary>
    private void CheckSchedule(WorkflowDefinition workflow)
    {
        if (workflow.Schedule == null) return;

        var schedule = workflow.Schedule;

        // Check for conflicting schedule configurations
        int configCount = 0;
        if (!string.IsNullOrWhiteSpace(schedule.CronExpression)) configCount++;
        if (schedule.IntervalSeconds.HasValue) configCount++;
        if (!string.IsNullOrWhiteSpace(schedule.TimeOfDay)) configCount++;
        if (schedule.RunAt.HasValue) configCount++;

        if (configCount == 0)
            AddIssue(HealthSeverity.Error, "Schedule", "Schedule is defined but has no configuration",
                suggestion: "Add cronExpression, intervalSeconds, timeOfDay, or runAt");

        if (configCount > 1)
            AddIssue(HealthSeverity.Warning, "Schedule", "Multiple schedule configurations found",
                suggestion: "Use only one scheduling method (cron, interval, timeOfDay, or runAt)");

        // Check interval
        if (schedule.IntervalSeconds.HasValue && schedule.IntervalSeconds.Value <= 0)
            AddIssue(HealthSeverity.Error, "Schedule", $"Invalid interval: {schedule.IntervalSeconds}",
                suggestion: "Interval must be positive");

        // Check timeOfDay format
        if (!string.IsNullOrWhiteSpace(schedule.TimeOfDay))
        {
            if (!TimeSpan.TryParse(schedule.TimeOfDay, out _))
                AddIssue(HealthSeverity.Error, "Schedule", $"Invalid timeOfDay format: {schedule.TimeOfDay}",
                    suggestion: "Use HH:mm format (e.g., '14:30')");
        }

        // Check runAt
        if (schedule.RunAt.HasValue && schedule.RunAt.Value < DateTime.UtcNow)
            AddIssue(HealthSeverity.Warning, "Schedule", "runAt time is in the past",
                suggestion: "Update runAt to a future time");
    }

    /// <summary>
    /// Checks timing configuration.
    /// </summary>
    private void CheckTiming(WorkflowDefinition workflow)
    {
        if (workflow.Timing == null) return;

        var timing = workflow.Timing;

        if (timing.MaxDurationSeconds.HasValue && timing.MaxDurationSeconds.Value <= 0)
            AddIssue(HealthSeverity.Error, "Timing", $"Invalid maxDurationSeconds: {timing.MaxDurationSeconds}",
                suggestion: "Max duration must be positive");

        if (timing.StepDelaySeconds.HasValue && timing.StepDelaySeconds.Value < 0)
            AddIssue(HealthSeverity.Error, "Timing", $"Invalid stepDelaySeconds: {timing.StepDelaySeconds}",
                suggestion: "Step delay must be non-negative");

        // Check time window
        if (!string.IsNullOrWhiteSpace(timing.EarliestStartTime) &&
            !string.IsNullOrWhiteSpace(timing.LatestStartTime))
        {
            if (TimeSpan.TryParse(timing.EarliestStartTime, out var earliest) &&
                TimeSpan.TryParse(timing.LatestStartTime, out var latest))
            {
                if (earliest >= latest)
                    AddIssue(HealthSeverity.Error, "Timing", "earliestStartTime must be before latestStartTime",
                        suggestion: "Adjust time window configuration");
            }
        }
    }

    /// <summary>
    /// Checks hooks configuration.
    /// </summary>
    private void CheckHooks(WorkflowDefinition workflow)
    {
        if (workflow.Hooks == null) return;

        CheckHookList(workflow.Hooks.PreExecution, "preExecution");
        CheckHookList(workflow.Hooks.PostSuccess, "postSuccess");
        CheckHookList(workflow.Hooks.PostFailure, "postFailure");
        CheckHookList(workflow.Hooks.PostExecution, "postExecution");
        CheckHookList(workflow.Hooks.PreStep, "preStep");
        CheckHookList(workflow.Hooks.PostStep, "postStep");
    }

    private void CheckHookList(List<WorkflowHook>? hooks, string hookType)
    {
        if (hooks == null || hooks.Count == 0) return;

        foreach (var hook in hooks)
        {
            if (string.IsNullOrWhiteSpace(hook.Type))
                AddIssue(HealthSeverity.Error, "Hooks", $"{hookType} hook has no type",
                    suggestion: "Specify hook type (log, http, etc.)");

            if (string.IsNullOrWhiteSpace(hook.Name))
                AddIssue(HealthSeverity.Warning, "Hooks", $"{hookType} hook has no name",
                    suggestion: "Add a descriptive name to the hook");
        }
    }

    /// <summary>
    /// Checks environment configuration.
    /// </summary>
    private void CheckEnvironments(WorkflowDefinition workflow)
    {
        if (workflow.Environments == null || workflow.Environments.Count == 0) return;

        foreach (var env in workflow.Environments)
        {
            if (string.IsNullOrWhiteSpace(env.Name))
                AddIssue(HealthSeverity.Error, "Environments", "Environment has empty name",
                    suggestion: "Each environment must have a name");
        }
    }

    /// <summary>
    /// Checks variable usage and references.
    /// </summary>
    private void CheckVariables(WorkflowDefinition workflow)
    {
        if (workflow.Variables == null || workflow.Variables.Count == 0) return;

        // Check for unused variables
        var json = JsonSerializer.Serialize(workflow);
        var usedVariables = new HashSet<string>();

        foreach (var variable in workflow.Variables.Keys)
        {
            // Check if variable is referenced in the JSON
            if (json.Contains($"${{var:{variable}}}") || json.Contains($"${{ctx:{variable}}}"))
                usedVariables.Add(variable);
        }

        foreach (var variable in workflow.Variables.Keys)
        {
            if (!usedVariables.Contains(variable))
                AddIssue(HealthSeverity.Info, "Variables", $"Variable '{variable}' is defined but never used",
                    suggestion: "Remove unused variable or add a reference to it");
        }
    }

    /// <summary>
    /// Checks for best practices.
    /// </summary>
    private void CheckBestPractices(WorkflowDefinition workflow)
    {
        // Check for descriptive names
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Name != null && step.Name.Length < 5)
                    AddIssue(HealthSeverity.Info, "Best Practices",
                        $"Step {step.Id} has a very short name: '{step.Name}'",
                        suggestion: "Use more descriptive step names");
            }
        }

        // Check for error handling
        if (workflow.Steps != null && workflow.Steps.Count > 3)
        {
            var hasErrorHandling = workflow.Steps.Any(s =>
                s.OnFailure != null ||
                s.ContinueOnError == true ||
                (s.RetryCount.HasValue && s.RetryCount.Value > 0));

            if (!hasErrorHandling)
                AddIssue(HealthSeverity.Warning, "Best Practices",
                    "No error handling configured",
                    suggestion: "Add retry logic or error handlers to critical steps");
        }

        // Check for timeouts on long-running operations
        if (workflow.Steps != null)
        {
            foreach (var step in workflow.Steps)
            {
                if (step.Type == "http" && !step.TimeoutSeconds.HasValue)
                    AddIssue(HealthSeverity.Info, "Best Practices",
                        $"HTTP step {step.Id} has no timeout",
                        suggestion: "Add a timeout to prevent hanging on network issues");
            }
        }
    }

    /// <summary>
    /// Checks for performance issues.
    /// </summary>
    private void CheckPerformance(WorkflowDefinition workflow)
    {
        if (workflow.Steps == null) return;

        // Check for excessive retries
        foreach (var step in workflow.Steps)
        {
            if (step.RetryCount.HasValue && step.RetryCount.Value > 10)
                AddIssue(HealthSeverity.Warning, "Performance",
                    $"Step {step.Id} has excessive retry count: {step.RetryCount}",
                    suggestion: "Consider reducing retry count or fixing the underlying issue");

            if (step.TimeoutSeconds.HasValue && step.TimeoutSeconds.Value > 3600)
                AddIssue(HealthSeverity.Warning, "Performance",
                    $"Step {step.Id} has very long timeout: {step.TimeoutSeconds}s",
                    suggestion: "Consider breaking down into smaller steps or reducing timeout");
        }

        // Check for sequential execution where parallel would be better
        if (workflow.Steps.Count > 5)
        {
            var hasParallelSteps = workflow.Steps.Any(s => s.AllowParallel == true);
            var hasDependencies = workflow.Steps.Any(s =>
                (s.DependsOn != null && s.DependsOn.Count > 0) ||
                (s.Dependencies != null && s.Dependencies.Count > 0));

            if (!hasParallelSteps && !hasDependencies)
                AddIssue(HealthSeverity.Info, "Performance",
                    "All steps execute sequentially",
                    suggestion: "Consider using dependencies and allowParallel for better performance");
        }
    }

    /// <summary>
    /// Adds an issue to the list.
    /// </summary>
    private void AddIssue(HealthSeverity severity, string category, string message, string? location = null, string? suggestion = null)
    {
        _issues.Add(new HealthIssue
        {
            Severity = severity,
            Category = category,
            Message = message,
            Location = location,
            Suggestion = suggestion
        });
    }

    /// <summary>
    /// Calculates overall health score (0-100).
    /// </summary>
    private int CalculateScore()
    {
        int baseScore = 100;

        foreach (var issue in _issues)
        {
            int penalty = issue.Severity switch
            {
                HealthSeverity.Critical => 30,
                HealthSeverity.Error => 15,
                HealthSeverity.Warning => 5,
                HealthSeverity.Info => 1,
                _ => 0
            };

            baseScore -= penalty;
        }

        return Math.Max(0, Math.Min(100, baseScore));
    }

    /// <summary>
    /// Generates a formatted health report.
    /// </summary>
    public static string GenerateHealthReport(WorkflowHealthReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║ WORKFLOW HEALTH CHECK REPORT                                                  ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Workflow: {report.WorkflowName,-67} ║");
        sb.AppendLine($"║ ID: {report.WorkflowId,-73} ║");
        sb.AppendLine($"║ Checked: {report.CheckedAt:yyyy-MM-dd HH:mm:ss UTC}                                        ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");

        // Status
        var statusIcon = report.IsHealthy ? "✅" : "❌";
        var statusText = report.IsHealthy ? "HEALTHY" : "UNHEALTHY";
        sb.AppendLine($"║ Status: {statusIcon} {statusText,-65} ║");
        sb.AppendLine($"║ Score: {report.Score}/100 {GetScoreBar(report.Score),-57} ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Issue summary
        if (report.Issues.Count > 0)
        {
            sb.AppendLine($"Found {report.Issues.Count} issue(s):");
            sb.AppendLine();

            foreach (var group in report.Issues.GroupBy(i => i.Severity).OrderByDescending(g => g.Key))
            {
                var icon = GetSeverityIcon(group.Key);
                sb.AppendLine($"{icon} {group.Key}: {group.Count()}");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine();

            // Detailed issues
            foreach (var issue in report.Issues.OrderByDescending(i => i.Severity))
            {
                var icon = GetSeverityIcon(issue.Severity);
                sb.AppendLine($"{icon} [{issue.Severity}] {issue.Category}");
                sb.AppendLine($"   {issue.Message}");

                if (!string.IsNullOrEmpty(issue.Location))
                    sb.AppendLine($"   Location: {issue.Location}");

                if (!string.IsNullOrEmpty(issue.Suggestion))
                    sb.AppendLine($"   💡 Suggestion: {issue.Suggestion}");

                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("✨ No issues found! This workflow is in excellent health.");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetScoreBar(int score)
    {
        int barLength = 20;
        int filled = (int)((score / 100.0) * barLength);
        return "[" + new string('█', filled) + new string('░', barLength - filled) + "]";
    }

    public static string GetSeverityIcon(HealthSeverity severity)
    {
        return severity switch
        {
            HealthSeverity.Critical => "🔴",
            HealthSeverity.Error => "🟠",
            HealthSeverity.Warning => "🟡",
            HealthSeverity.Info => "🔵",
            _ => "⚪"
        };
    }
}
