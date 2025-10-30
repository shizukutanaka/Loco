using System.Text;
using Spectre.Console;

namespace Loco.Cli.UI;

/// <summary>
/// Interactive terminal UI with modern CLI/TUI patterns
/// Based on 2024/2025 research:
/// - CLI UX best practices (Evil Martians, clig.dev)
/// - Progress displays: spinners, X of Y pattern, progress bars
/// - Awesome TUIs (Terminal User Interfaces) patterns
/// - Fast and responsive with no superfluous visual baggage
/// - Command Line Interface Guidelines (updated UNIX principles)
/// Solves Issue #25: Desktop UI/UX improvements for CLI
/// </summary>
public class InteractiveTerminalUI
{
    /// <summary>
    /// Display interactive menu with keyboard navigation
    /// Based on awesome-tuis patterns
    /// </summary>
    public static string ShowMenu(string title, List<MenuOption> options)
    {
        var selectedOption = AnsiConsole.Prompt(
            new SelectionPrompt<MenuOption>()
                .Title($"[bold cyan]{title}[/]")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                .AddChoices(options)
                .UseConverter(opt => opt.Display)
        );

        return selectedOption.Value;
    }

    /// <summary>
    /// Display multi-select menu with checkboxes
    /// </summary>
    public static List<string> ShowMultiSelect(string title, List<MenuOption> options)
    {
        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<MenuOption>()
                .Title($"[bold cyan]{title}[/]")
                .Required()
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
                .AddChoices(options)
                .UseConverter(opt => opt.Display)
        );

        return selected.Select(opt => opt.Value).ToList();
    }

    /// <summary>
    /// Display progress bar for long-running operations
    /// Based on CLI UX best practices: 3 patterns (spinner, X of Y, progress bar)
    /// </summary>
    public static async Task ShowProgressAsync(
        string description,
        Func<ProgressContext, Task> operation)
    {
        await AnsiConsole.Progress()
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask(description);
                task.MaxValue = 100;

                await operation(ctx);

                task.Value = 100;
            });
    }

    /// <summary>
    /// Display spinner for indeterminate operations
    /// CLI UX pattern: spinner for keeping users informed
    /// </summary>
    public static async Task<T> ShowSpinnerAsync<T>(
        string description,
        Func<Task<T>> operation)
    {
        return await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(description, async ctx =>
            {
                return await operation();
            });
    }

    /// <summary>
    /// Display table with workflow information
    /// Fast and responsive with no animations
    /// </summary>
    public static void ShowTable(string title, List<string> headers, List<List<string>> rows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Title($"[bold yellow]{title}[/]");

        foreach (var header in headers)
        {
            table.AddColumn(new TableColumn($"[bold cyan]{header}[/]").Centered());
        }

        foreach (var row in rows)
        {
            table.AddRow(row.Select(cell => new Markup(cell)).ToArray());
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Display tree structure for workflow hierarchy
    /// Based on TUI best practices for hierarchical data
    /// </summary>
    public static void ShowTree(string title, LocoTreeNode root)
    {
        var tree = new Tree($"[bold cyan]{title}[/]");
        AddTreeNodes(tree.AddNode($"[yellow]{root.Label}[/]"), root.Children);
        AnsiConsole.Write(tree);
    }

    private static void AddTreeNodes(Spectre.Console.TreeNode parent, List<LocoTreeNode> children)
    {
        foreach (var child in children)
        {
            var node = parent.AddNode($"[green]{child.Label}[/]");
            if (child.Children.Count > 0)
            {
                AddTreeNodes(node, child.Children);
            }
        }
    }

    /// <summary>
    /// Display panel with formatted content
    /// </summary>
    public static void ShowPanel(string title, string content, Color? borderColor = null)
    {
        var color = borderColor ?? Color.Cyan1;
        var panel = new Panel(content)
            .Header($"[bold]{title}[/]")
            .BorderColor(color)
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Display success message with visual indicator
    /// </summary>
    public static void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display error message with visual indicator
    /// </summary>
    public static void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display warning message with visual indicator
    /// </summary>
    public static void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Display info message with visual indicator
    /// </summary>
    public static void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[cyan]ℹ[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Prompt for text input with validation
    /// </summary>
    public static string PromptText(string question, string? defaultValue = null, bool allowEmpty = false)
    {
        var prompt = new TextPrompt<string>($"[cyan]{question}[/]");

        if (defaultValue != null)
        {
            prompt.DefaultValue(defaultValue);
        }

        if (!allowEmpty)
        {
            prompt.Validate(input => !string.IsNullOrWhiteSpace(input)
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Input cannot be empty[/]"));
        }

        return AnsiConsole.Prompt(prompt);
    }

    /// <summary>
    /// Prompt for confirmation (Yes/No)
    /// </summary>
    public static bool PromptConfirmation(string question, bool defaultValue = false)
    {
        return AnsiConsole.Confirm($"[cyan]{question}[/]", defaultValue);
    }

    /// <summary>
    /// Display live updating status
    /// Pattern from CLI UX best practices for long-running operations
    /// </summary>
    public static async Task ShowLiveStatusAsync(
        string description,
        Func<LiveDisplayContext, Task> operation)
    {
        await AnsiConsole.Live(new Markup($"[cyan]{description}[/]"))
            .StartAsync(async ctx =>
            {
                await operation(ctx);
            });
    }

    /// <summary>
    /// Display rule (horizontal line) for visual separation
    /// </summary>
    public static void ShowRule(string? title = null)
    {
        if (title != null)
        {
            AnsiConsole.Write(new Rule($"[yellow]{title}[/]")
                .RuleStyle(Style.Parse("grey")));
        }
        else
        {
            AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("grey")));
        }
    }

    /// <summary>
    /// Display figlet text (ASCII art banner)
    /// </summary>
    public static void ShowBanner(string text, Color? color = null)
    {
        var figletColor = color ?? Color.Cyan1;
        AnsiConsole.Write(
            new FigletText(text)
                .LeftJustified()
                .Color(figletColor));
    }

    /// <summary>
    /// Display calendar for date selection
    /// </summary>
    public static void ShowCalendar(DateTime date)
    {
        var calendar = new Calendar(date)
            .AddCalendarEvent(date)
            .HeaderStyle(Style.Parse("cyan bold"))
            .HighlightStyle(Style.Parse("yellow bold"));

        AnsiConsole.Write(calendar);
    }

    /// <summary>
    /// Display bar chart for metrics visualization
    /// </summary>
    public static void ShowBarChart(string title, Dictionary<string, double> data)
    {
        var chart = new BarChart()
            .Width(60)
            .Label($"[bold underline cyan]{title}[/]")
            .CenterLabel();

        foreach (var item in data)
        {
            chart.AddItem(item.Key, item.Value, Color.Cyan1);
        }

        AnsiConsole.Write(chart);
    }

    /// <summary>
    /// Display breakdown chart (like pie chart but horizontal)
    /// </summary>
    public static void ShowBreakdownChart(string title, Dictionary<string, double> data)
    {
        var chart = new BreakdownChart()
            .Width(60)
            .UseValueFormatter(value => $"{value:F1}%");

        foreach (var item in data)
        {
            chart.AddItem(item.Key, item.Value, Color.Cyan1);
        }

        AnsiConsole.Write(new Panel(chart)
            .Header($"[bold]{title}[/]")
            .BorderColor(Color.Cyan1));
    }

    /// <summary>
    /// Clear the console
    /// </summary>
    public static void Clear()
    {
        AnsiConsole.Clear();
    }

    /// <summary>
    /// Display JSON in a formatted panel
    /// </summary>
    public static void ShowJson(string title, string json)
    {
        var jsonText = new Markup(Markup.Escape(json));
        var panel = new Panel(jsonText)
            .Header($"[bold cyan]{title}[/]")
            .BorderColor(Color.Cyan1)
            .Padding(1, 1);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Display exception details in a formatted panel
    /// Based on Sentry-style error display
    /// </summary>
    public static void ShowException(Exception exception)
    {
        AnsiConsole.WriteException(exception,
            ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes |
            ExceptionFormats.ShortenMethods | ExceptionFormats.ShowLinks);
    }

    /// <summary>
    /// Display markup text with color support
    /// </summary>
    public static void Write(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }

    /// <summary>
    /// Display plain text (escaped)
    /// </summary>
    public static void WriteLine(string text)
    {
        AnsiConsole.WriteLine(Markup.Escape(text));
    }
}

public class MenuOption
{
    public string Value { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string? Description { get; set; }

    public MenuOption(string value, string display, string? description = null)
    {
        Value = value;
        Display = display;
        Description = description;
    }
}

public class LocoTreeNode
{
    public string Label { get; set; } = string.Empty;
    public List<LocoTreeNode> Children { get; set; } = new();

    public LocoTreeNode(string label)
    {
        Label = label;
    }

    public LocoTreeNode AddChild(string label)
    {
        var child = new LocoTreeNode(label);
        Children.Add(child);
        return child;
    }
}
