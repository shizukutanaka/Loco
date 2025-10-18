namespace Loco.Core.Workflows;

/// <summary>
/// Represents a hook that runs at specific workflow lifecycle points.
/// </summary>
public class WorkflowHook
{
    /// <summary>
    /// Type of hook action (log, process, http, etc.).
    /// </summary>
    public string Type { get; set; } = "log";

    /// <summary>
    /// Name/description of the hook.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Message for log hooks.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Command for process hooks.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// URL for HTTP hooks.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// HTTP method for HTTP hooks.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Request body for HTTP hooks.
    /// </summary>
    public object? Body { get; set; }

    /// <summary>
    /// Whether to continue workflow if hook fails.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Timeout for hook execution (seconds).
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Collection of workflow lifecycle hooks.
/// </summary>
public class WorkflowHooks
{
    /// <summary>
    /// Hooks to run before workflow starts.
    /// </summary>
    public List<WorkflowHook>? PreExecution { get; set; }

    /// <summary>
    /// Hooks to run after workflow completes successfully.
    /// </summary>
    public List<WorkflowHook>? PostSuccess { get; set; }

    /// <summary>
    /// Hooks to run after workflow fails.
    /// </summary>
    public List<WorkflowHook>? PostFailure { get; set; }

    /// <summary>
    /// Hooks to run after workflow completes (regardless of success/failure).
    /// </summary>
    public List<WorkflowHook>? PostExecution { get; set; }

    /// <summary>
    /// Hooks to run before each step.
    /// </summary>
    public List<WorkflowHook>? PreStep { get; set; }

    /// <summary>
    /// Hooks to run after each step.
    /// </summary>
    public List<WorkflowHook>? PostStep { get; set; }
}

/// <summary>
/// Partial class extension for WorkflowDefinition to support hooks.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Lifecycle hooks for this workflow.
    /// </summary>
    public WorkflowHooks? Hooks { get; set; }
}

/// <summary>
/// Executor for workflow hooks.
/// </summary>
public static class HookExecutor
{
    /// <summary>
    /// Executes a list of hooks.
    /// </summary>
    public static async Task<bool> ExecuteHooksAsync(
        List<WorkflowHook>? hooks,
        string phase,
        Dictionary<string, object?>? context = null)
    {
        if (hooks == null || hooks.Count == 0)
            return true;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  🪝 Executing {phase} hooks ({hooks.Count})...");
        Console.ResetColor();

        bool allSucceeded = true;

        foreach (var hook in hooks)
        {
            try
            {
                var hookName = hook.Name ?? $"{hook.Type} hook";
                Console.WriteLine($"    → {hookName}");

                bool success = await ExecuteHookAsync(hook, context);

                if (!success)
                {
                    allSucceeded = false;

                    if (!hook.ContinueOnError)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    ✗ Hook failed and continueOnError=false - stopping");
                        Console.ResetColor();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    ✗ Hook exception: {ex.Message}");
                Console.ResetColor();

                allSucceeded = false;

                if (!hook.ContinueOnError)
                {
                    return false;
                }
            }
        }

        return allSucceeded;
    }

    private static async Task<bool> ExecuteHookAsync(WorkflowHook hook, Dictionary<string, object?>? context)
    {
        switch (hook.Type.ToLowerInvariant())
        {
            case "log":
                Console.WriteLine($"      {hook.Message ?? ""}");
                return true;

            case "process":
            case "command":
                if (string.IsNullOrEmpty(hook.Command))
                {
                    Console.WriteLine("      ⚠ No command specified");
                    return false;
                }

                // For now, just log - full implementation would execute process
                Console.WriteLine($"      Command: {hook.Command}");
                return true;

            case "http":
            case "webhook":
                if (string.IsNullOrEmpty(hook.Url))
                {
                    Console.WriteLine("      ⚠ No URL specified");
                    return false;
                }

                // For now, just log - full implementation would make HTTP request
                Console.WriteLine($"      HTTP {hook.Method ?? "POST"}: {hook.Url}");
                return true;

            default:
                Console.WriteLine($"      ⚠ Unknown hook type: {hook.Type}");
                return false;
        }
    }
}
