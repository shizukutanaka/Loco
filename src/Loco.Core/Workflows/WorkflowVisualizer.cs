using System.Text;

namespace Loco.Core.Workflows;

/// <summary>
/// Generates ASCII visual representations of workflows.
/// </summary>
public class WorkflowVisualizer
{
    /// <summary>
    /// Generates an ASCII diagram of the workflow structure.
    /// </summary>
    public static string GenerateDiagram(WorkflowDefinition workflow)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine($"║  {CenterText("WORKFLOW DIAGRAM", 64)}  ║");
        sb.AppendLine("╠════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Name: {workflow.Name.PadRight(58)} ║");
        sb.AppendLine($"║  ID: {workflow.Id.PadRight(60)} ║");

        if (!string.IsNullOrEmpty(workflow.Description))
        {
            sb.AppendLine($"║  Desc: {TruncateText(workflow.Description, 58).PadRight(58)} ║");
        }

        sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        if (workflow.Steps == null || workflow.Steps.Count == 0)
        {
            sb.AppendLine("  (No steps defined)");
            return sb.ToString();
        }

        // Steps
        for (int i = 0; i < workflow.Steps.Count; i++)
        {
            var step = workflow.Steps[i];
            var isLast = i == workflow.Steps.Count - 1;

            // Step box
            sb.AppendLine("  ┌─────────────────────────────────────────────────────────────────┐");
            sb.AppendLine($"  │ {i + 1}. {TruncateText(step.Name, 60).PadRight(60)} │");
            sb.AppendLine($"  │    Type: {step.Type.PadRight(56)} │");
            sb.AppendLine($"  │    ID: {step.Id.PadRight(58)} │");

            // Show key properties
            if (!string.IsNullOrEmpty(step.RunIf))
            {
                sb.AppendLine($"  │    ⚡ RunIf: {TruncateText(step.RunIf, 54).PadRight(54)} │");
            }

            if (!string.IsNullOrEmpty(step.SkipIf))
            {
                sb.AppendLine($"  │    ⊘ SkipIf: {TruncateText(step.SkipIf, 53).PadRight(53)} │");
            }

            if (step.RetryCount.HasValue && step.RetryCount.Value > 0)
            {
                sb.AppendLine($"  │    🔄 Retry: {step.RetryCount.Value} times{("".PadRight(49))} │");
            }

            if (step.TimeoutSeconds.HasValue)
            {
                sb.AppendLine($"  │    ⏱ Timeout: {step.TimeoutSeconds.Value}s{("".PadRight(52 - step.TimeoutSeconds.Value.ToString().Length))} │");
            }

            if (!string.IsNullOrEmpty(step.SaveOutput))
            {
                sb.AppendLine($"  │    💾 Save: {TruncateText(step.SaveOutput, 55).PadRight(55)} │");
            }

            sb.AppendLine("  └─────────────────────────────────────────────────────────────────┘");

            // Connector
            if (!isLast)
            {
                sb.AppendLine("                              │");
                sb.AppendLine("                              ▼");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a compact list view of the workflow.
    /// </summary>
    public static string GenerateCompactList(WorkflowDefinition workflow)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Workflow: {workflow.Name} (ID: {workflow.Id})");

        if (!string.IsNullOrEmpty(workflow.Description))
        {
            sb.AppendLine($"Description: {workflow.Description}");
        }

        sb.AppendLine($"Steps: {workflow.Steps?.Count ?? 0}");
        sb.AppendLine();

        if (workflow.Steps == null || workflow.Steps.Count == 0)
        {
            sb.AppendLine("  (No steps defined)");
            return sb.ToString();
        }

        for (int i = 0; i < workflow.Steps.Count; i++)
        {
            var step = workflow.Steps[i];
            var flags = new List<string>();

            if (!string.IsNullOrEmpty(step.RunIf))
                flags.Add($"runIf:{step.RunIf}");

            if (!string.IsNullOrEmpty(step.SkipIf))
                flags.Add($"skipIf:{step.SkipIf}");

            if (step.RetryCount.HasValue && step.RetryCount.Value > 0)
                flags.Add($"retry:{step.RetryCount.Value}");

            if (step.TimeoutSeconds.HasValue)
                flags.Add($"timeout:{step.TimeoutSeconds.Value}s");

            if (!string.IsNullOrEmpty(step.SaveOutput))
                flags.Add($"save:{step.SaveOutput}");

            var flagText = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";

            sb.AppendLine($"  {i + 1}. [{step.Type}] {step.Name}{flagText}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a dependency graph showing conditional relationships.
    /// </summary>
    public static string GenerateDependencyGraph(WorkflowDefinition workflow)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Workflow Dependencies:");
        sb.AppendLine();

        if (workflow.Steps == null || workflow.Steps.Count == 0)
        {
            sb.AppendLine("  (No steps defined)");
            return sb.ToString();
        }

        // Find steps that depend on others
        foreach (var step in workflow.Steps)
        {
            var dependencies = new List<string>();

            // Check for context variable dependencies in runIf/skipIf
            if (!string.IsNullOrEmpty(step.RunIf))
            {
                dependencies.Add($"RunIf: {step.RunIf}");
            }

            if (!string.IsNullOrEmpty(step.SkipIf))
            {
                dependencies.Add($"SkipIf: {step.SkipIf}");
            }

            // Check for variable references in message/command
            var varsUsed = ExtractVariableReferences(step);
            if (varsUsed.Count > 0)
            {
                dependencies.Add($"Uses: {string.Join(", ", varsUsed)}");
            }

            if (dependencies.Count > 0)
            {
                sb.AppendLine($"  {step.Name}:");
                foreach (var dep in dependencies)
                {
                    sb.AppendLine($"    └─ {dep}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static List<string> ExtractVariableReferences(WorkflowStep step)
    {
        var vars = new List<string>();
        var texts = new[] { step.Message, step.Command, step.Url }.Where(t => !string.IsNullOrEmpty(t));

        foreach (var text in texts)
        {
            if (text == null) continue;

            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\$\{([^}]+)\}");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var varName = match.Groups[1].Value;
                if (!vars.Contains(varName))
                {
                    vars.Add(varName);
                }
            }
        }

        return vars;
    }

    private static string CenterText(string text, int width)
    {
        if (text.Length >= width)
            return text.Substring(0, width);

        var padding = width - text.Length;
        var leftPad = padding / 2;
        var rightPad = padding - leftPad;

        return new string(' ', leftPad) + text + new string(' ', rightPad);
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Generates a schedule information display.
    /// </summary>
    public static string GenerateScheduleInfo(WorkflowDefinition workflow)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Workflow: {workflow.Name} (ID: {workflow.Id})");
        sb.AppendLine();

        if (workflow.Schedule != null)
        {
            sb.AppendLine("Schedule Configuration:");
            sb.AppendLine($"  Enabled: {workflow.Schedule.Enabled}");

            if (workflow.Schedule.RunAt.HasValue)
            {
                sb.AppendLine($"  One-time execution: {workflow.Schedule.RunAt.Value:yyyy-MM-dd HH:mm:ss}");
            }

            if (workflow.Schedule.IntervalSeconds.HasValue)
            {
                sb.AppendLine($"  Interval: Every {workflow.Schedule.IntervalSeconds.Value} seconds");
            }

            if (!string.IsNullOrEmpty(workflow.Schedule.TimeOfDay))
            {
                sb.AppendLine($"  Time of day: {workflow.Schedule.TimeOfDay}");
            }

            if (!string.IsNullOrEmpty(workflow.Schedule.DaysOfWeek))
            {
                sb.AppendLine($"  Days of week: {workflow.Schedule.DaysOfWeek}");
            }

            if (workflow.Schedule.MaxExecutions > 0)
            {
                sb.AppendLine($"  Max executions: {workflow.Schedule.MaxExecutions}");
            }

            var nextRun = ScheduleChecker.GetNextRunTime(workflow.Schedule);
            sb.AppendLine($"  Next run: {ScheduleChecker.FormatNextRunTime(nextRun)}");
        }
        else
        {
            sb.AppendLine("Schedule: Not configured (manual execution only)");
        }

        sb.AppendLine();

        if (workflow.Timing != null)
        {
            sb.AppendLine("Timing Constraints:");

            if (workflow.Timing.MaxDurationSeconds.HasValue)
            {
                sb.AppendLine($"  Max duration: {workflow.Timing.MaxDurationSeconds.Value}s");
            }

            if (workflow.Timing.StartDelaySeconds.HasValue)
            {
                sb.AppendLine($"  Start delay: {workflow.Timing.StartDelaySeconds.Value}s");
            }

            if (workflow.Timing.StepDelaySeconds.HasValue)
            {
                sb.AppendLine($"  Step delay: {workflow.Timing.StepDelaySeconds.Value}s");
            }

            if (!string.IsNullOrEmpty(workflow.Timing.EarliestStartTime))
            {
                sb.AppendLine($"  Earliest start: {workflow.Timing.EarliestStartTime}");
            }

            if (!string.IsNullOrEmpty(workflow.Timing.LatestStartTime))
            {
                sb.AppendLine($"  Latest start: {workflow.Timing.LatestStartTime}");
            }

            sb.AppendLine($"  Skip outside window: {workflow.Timing.SkipOutsideWindow}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a step dependency analysis display.
    /// </summary>
    public static string GenerateDependencyAnalysis(WorkflowDefinition workflow)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Workflow: {workflow.Name} (ID: {workflow.Id})");
        sb.AppendLine();

        if (workflow.Steps == null || workflow.Steps.Count == 0)
        {
            sb.AppendLine("No steps defined");
            return sb.ToString();
        }

        var analyzer = new DependencyAnalyzer(workflow.Steps);

        // Validate dependencies
        var (isValid, errors) = analyzer.ValidateDependencies();

        if (!isValid)
        {
            sb.AppendLine("❌ Dependency Validation FAILED:");
            sb.AppendLine();

            foreach (var error in errors)
            {
                sb.AppendLine($"  ✗ {error}");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("✅ Dependency Validation PASSED");
            sb.AppendLine();
        }

        // Show dependency graph
        sb.AppendLine(analyzer.GenerateDependencyGraph());

        return sb.ToString();
    }
}
