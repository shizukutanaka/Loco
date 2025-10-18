namespace Loco.Core.Workflows;

/// <summary>
/// Represents an interactive confirmation prompt.
/// </summary>
public class InteractivePrompt
{
    /// <summary>
    /// Message to display to the user.
    /// </summary>
    public string Message { get; set; } = "Continue?";

    /// <summary>
    /// Default choice if user just presses Enter (true = yes, false = no).
    /// </summary>
    public bool DefaultYes { get; set; } = true;

    /// <summary>
    /// Timeout in seconds before auto-accepting default choice (0 = no timeout).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// What to do if user declines: "skip" (skip step), "stop" (stop workflow), "continue" (continue anyway).
    /// </summary>
    public string OnDecline { get; set; } = "skip";
}

/// <summary>
/// Partial class extension for WorkflowStep to support interactive prompts.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Interactive prompt to show before executing this step.
    /// </summary>
    public InteractivePrompt? Prompt { get; set; }
}

/// <summary>
/// Utility for handling interactive prompts in the console.
/// </summary>
public static class InteractivePrompter
{
    /// <summary>
    /// Shows an interactive yes/no prompt and returns the user's choice.
    /// </summary>
    public static async Task<bool> PromptAsync(InteractivePrompt prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"  ⚠ {prompt.Message} ");

        if (prompt.DefaultYes)
        {
            Console.Write("[Y/n] ");
        }
        else
        {
            Console.Write("[y/N] ");
        }
        Console.ResetColor();

        if (prompt.TimeoutSeconds > 0)
        {
            Console.Write($"(timeout in {prompt.TimeoutSeconds}s) ");
        }

        string? response;

        if (prompt.TimeoutSeconds > 0)
        {
            // Timeout logic
            var cts = new CancellationTokenSource();
            var readTask = Task.Run(() => Console.ReadLine(), cts.Token);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(prompt.TimeoutSeconds));

            var completedTask = await Task.WhenAny(readTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                cts.Cancel();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⏱ Timeout - using default ({(prompt.DefaultYes ? "Yes" : "No")})");
                Console.ResetColor();
                return prompt.DefaultYes;
            }

            response = readTask.Result;
        }
        else
        {
            response = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            return prompt.DefaultYes;
        }

        var normalized = response.Trim().ToLowerInvariant();

        if (normalized == "y" || normalized == "yes")
        {
            return true;
        }
        else if (normalized == "n" || normalized == "no")
        {
            return false;
        }

        // Invalid input, use default
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  Invalid input - using default ({(prompt.DefaultYes ? "Yes" : "No")})");
        Console.ResetColor();
        return prompt.DefaultYes;
    }
}
