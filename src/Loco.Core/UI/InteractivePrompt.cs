using System.Text;

namespace Loco.Core.UI;

/// <summary>
/// Interactive prompt types.
/// </summary>
public enum PromptType
{
    Confirmation,
    Choice,
    Input,
    Password
}

/// <summary>
/// Prompt result with user input.
/// </summary>
public class PromptResult
{
    public bool Confirmed { get; set; }
    public string Value { get; set; } = "";
    public int SelectedIndex { get; set; } = -1;
    public bool Cancelled { get; set; }
}

/// <summary>
/// Interactive user prompts for terminal applications.
/// </summary>
public static class InteractivePrompt
{
    /// <summary>
    /// Prompts user for yes/no confirmation.
    /// </summary>
    /// <param name="message">The question to ask</param>
    /// <param name="defaultYes">Default answer if user just presses Enter</param>
    /// <returns>True if user confirmed, false otherwise</returns>
    public static bool Confirm(string message, bool defaultYes = false)
    {
        var defaultOption = defaultYes ? "Y/n" : "y/N";
        Console.Write($"{message} [{defaultOption}]: ");

        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(response))
            return defaultYes;

        return response == "y" || response == "yes";
    }

    /// <summary>
    /// Prompts user for yes/no confirmation with custom styling.
    /// </summary>
    public static bool ConfirmStyled(string message, string? details = null, bool defaultYes = false)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ CONFIRMATION REQUIRED                                       │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {message}");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(details))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"  {details}");
            Console.ResetColor();
        }

        Console.WriteLine();
        var result = Confirm("  Proceed?", defaultYes);
        Console.WriteLine();

        return result;
    }

    /// <summary>
    /// Prompts user to select from a list of choices.
    /// </summary>
    public static PromptResult Choice(string message, string[] options, int defaultIndex = 0)
    {
        Console.WriteLine(message);
        Console.WriteLine();

        for (int i = 0; i < options.Length; i++)
        {
            var prefix = i == defaultIndex ? ">" : " ";
            Console.WriteLine($"  {prefix} {i + 1}. {options[i]}");
        }

        Console.WriteLine();
        Console.Write($"Select (1-{options.Length}) [default: {defaultIndex + 1}]: ");

        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
        {
            return new PromptResult
            {
                Confirmed = true,
                SelectedIndex = defaultIndex,
                Value = options[defaultIndex]
            };
        }

        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Length)
        {
            return new PromptResult
            {
                Confirmed = true,
                SelectedIndex = choice - 1,
                Value = options[choice - 1]
            };
        }

        return new PromptResult { Cancelled = true };
    }

    /// <summary>
    /// Prompts user for text input.
    /// </summary>
    public static PromptResult Input(string message, string? defaultValue = null, bool required = false)
    {
        var prompt = string.IsNullOrEmpty(defaultValue)
            ? $"{message}: "
            : $"{message} [{defaultValue}]: ";

        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
            {
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    return new PromptResult
                    {
                        Confirmed = true,
                        Value = defaultValue
                    };
                }

                if (required)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("  Error: Input is required");
                    Console.ResetColor();
                    continue;
                }

                return new PromptResult { Cancelled = true };
            }

            return new PromptResult
            {
                Confirmed = true,
                Value = input
            };
        }
    }

    /// <summary>
    /// Prompts user for password input (masked).
    /// </summary>
    public static PromptResult Password(string message = "Password", bool showMask = true)
    {
        Console.Write($"{message}: ");

        var password = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    if (showMask)
                    {
                        Console.Write("\b \b");
                    }
                }
                continue;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return new PromptResult { Cancelled = true };
            }

            password.Append(key.KeyChar);
            if (showMask)
            {
                Console.Write("*");
            }
        }

        return new PromptResult
        {
            Confirmed = true,
            Value = password.ToString()
        };
    }

    /// <summary>
    /// Prompts user to press any key to continue.
    /// </summary>
    public static void PressAnyKey(string message = "Press any key to continue...")
    {
        Console.WriteLine(message);
        Console.ReadKey(intercept: true);
    }

    /// <summary>
    /// Shows a menu with arrow key navigation.
    /// </summary>
    public static PromptResult Menu(string title, string[] options, string? description = null)
    {
        var selectedIndex = 0;
        ConsoleKey key;

        do
        {
            Console.Clear();

            // Title
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"╔══ {title} ══╗");
            Console.ResetColor();
            Console.WriteLine();

            // Description
            if (!string.IsNullOrEmpty(description))
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  {description}");
                Console.ResetColor();
                Console.WriteLine();
            }

            // Options
            for (int i = 0; i < options.Length; i++)
            {
                if (i == selectedIndex)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ► {options[i]}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"    {options[i]}");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("  Use ↑↓ arrows to navigate, Enter to select, Esc to cancel");
            Console.ResetColor();

            // Handle input
            key = Console.ReadKey(intercept: true).Key;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : options.Length - 1;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex < options.Length - 1 ? selectedIndex + 1 : 0;
                    break;
            }

        } while (key != ConsoleKey.Enter && key != ConsoleKey.Escape);

        Console.Clear();

        if (key == ConsoleKey.Escape)
        {
            return new PromptResult { Cancelled = true };
        }

        return new PromptResult
        {
            Confirmed = true,
            SelectedIndex = selectedIndex,
            Value = options[selectedIndex]
        };
    }

    /// <summary>
    /// Shows a multi-select menu with space bar to toggle.
    /// </summary>
    public static PromptResult MultiSelect(string title, string[] options, bool[] initialSelection = null)
    {
        var selected = initialSelection ?? new bool[options.Length];
        var currentIndex = 0;
        ConsoleKey key;

        do
        {
            Console.Clear();

            // Title
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"╔══ {title} ══╗");
            Console.ResetColor();
            Console.WriteLine();

            // Options
            for (int i = 0; i < options.Length; i++)
            {
                var checkbox = selected[i] ? "[✓]" : "[ ]";
                var cursor = i == currentIndex ? "►" : " ";

                if (i == currentIndex)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }

                Console.WriteLine($"  {cursor} {checkbox} {options[i]}");

                if (i == currentIndex)
                {
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("  ↑↓: Navigate | Space: Toggle | Enter: Confirm | Esc: Cancel");
            Console.ResetColor();

            // Handle input
            key = Console.ReadKey(intercept: true).Key;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    currentIndex = currentIndex > 0 ? currentIndex - 1 : options.Length - 1;
                    break;
                case ConsoleKey.DownArrow:
                    currentIndex = currentIndex < options.Length - 1 ? currentIndex + 1 : 0;
                    break;
                case ConsoleKey.Spacebar:
                    selected[currentIndex] = !selected[currentIndex];
                    break;
            }

        } while (key != ConsoleKey.Enter && key != ConsoleKey.Escape);

        Console.Clear();

        if (key == ConsoleKey.Escape)
        {
            return new PromptResult { Cancelled = true };
        }

        var selectedOptions = options.Where((opt, idx) => selected[idx]).ToList();

        return new PromptResult
        {
            Confirmed = true,
            Value = string.Join(", ", selectedOptions)
        };
    }
}

/// <summary>
/// Extended workflow step with interactive prompt support.
/// </summary>
public partial class WorkflowStep
{
    /// <summary>
    /// Interactive prompt configuration for this step.
    /// </summary>
    public InteractivePromptConfig? Prompt { get; set; }
}

/// <summary>
/// Configuration for interactive prompts in workflows.
/// </summary>
public class InteractivePromptConfig
{
    /// <summary>
    /// Type of prompt (confirmation, choice, input, password).
    /// </summary>
    public string Type { get; set; } = "confirmation";

    /// <summary>
    /// Message to display to the user.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Optional details or description.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// For choice prompts: list of options.
    /// </summary>
    public List<string>? Options { get; set; }

    /// <summary>
    /// Default value or index.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Whether the prompt is required (cannot be skipped).
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Variable name to store the user's response.
    /// </summary>
    public string? SaveTo { get; set; }

    /// <summary>
    /// Whether to skip this step if prompt is cancelled.
    /// </summary>
    public bool SkipOnCancel { get; set; } = true;

    /// <summary>
    /// Timeout in seconds (0 = no timeout).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;
}
