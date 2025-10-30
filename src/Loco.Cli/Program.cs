using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Loco.Cli.Commands;
using Loco.Cli.UI;

namespace Loco.Cli;

/// <summary>
/// Main entry point for the Loco CLI application.
/// Responsible for initialization and command routing.
/// </summary>
class Program
{
    private static readonly HelpSystem HelpSystem = new();
    private static readonly LocalizationManager Localization = new();

    /// <summary>
    /// Main entry point
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        try
        {
            InitializeEnvironment();
            InitializeLocalization();

            if (args.Length == 0)
            {
                HelpSystem.ShowHelp();
                return 0;
            }

            return await RouteCommand(args);
        }
        catch (Exception ex)
        {
            HandleFatalError(ex);
            return 1;
        }
    }

    /// <summary>
    /// Initialize console and system environment
    /// </summary>
    private static void InitializeEnvironment()
    {
        // Enable Unicode support
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Enable reflection-based JSON serialization for .NET 8
        AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);
    }

    /// <summary>
    /// Initialize localization based on system culture
    /// </summary>
    private static void InitializeLocalization()
    {
        var bestCulture = Localization.DetectBestCulture();
        Localization.CurrentCulture = bestCulture;
    }

    /// <summary>
    /// Route command to appropriate handler
    /// </summary>
    private static async Task<int> RouteCommand(string[] args)
    {
        var commandName = args[0].ToLowerInvariant();

        // Handle help command
        if (commandName is "help" or "-h" or "--help")
        {
            if (args.Length > 1)
            {
                HelpSystem.ShowHelp(args[1]);
            }
            else
            {
                HelpSystem.ShowHelp();
            }
            return 0;
        }

        // Route to commands
        return commandName switch
        {
            "start" => await new StartCommand().InvokeAsync(args.Skip(1).ToArray()),
            "health" => await new HealthCommand().InvokeAsync(args.Skip(1).ToArray()),
            "diag" or "diagnostics" => await new DiagCommand().InvokeAsync(args.Skip(1).ToArray()),
            "rule" => await new RuleCommand().InvokeAsync(args.Skip(1).ToArray()),
            "preset" => await new PresetCommand().InvokeAsync(args.Skip(1).ToArray()),
            "files" => await new FilesCommand().InvokeAsync(args.Skip(1).ToArray()),
            "logs" => await new LogsCommand().InvokeAsync(args.Skip(1).ToArray()),
            "update" or "check-update" => await new UpdateCommand().InvokeAsync(args.Skip(1).ToArray()),
            "resource" or "resources" => await new ResourceCommand().InvokeAsync(args.Skip(1).ToArray()),
            "backup-config" or "config-backup" => await BackupConfigCommand.ExecuteAsync(args.Skip(1).ToArray()),
            "setup" => await new SetupCommand().ExecuteAsync(args.Skip(1).ToArray()),
            "version" => await new VersionCommand().ExecuteAsync(args.Skip(1).ToArray()),
            "test" or "tests" => await new TestsCommand().InvokeAsync(args.Skip(1).ToArray()),
            "iac" or "infrastructure" => await new IacCommand().InvokeAsync(args.Skip(1).ToArray()),
            "workflow" or "wf" => await new WorkflowCommand().InvokeAsync(args.Skip(1).ToArray()),
            "interactive" or "i" => await new InteractiveCommand().InvokeAsync(args.Skip(1).ToArray()),
            _ => HandleUnknownCommand(commandName)
        };
    }

    /// <summary>
    /// Handle unknown command
    /// </summary>
    private static int HandleUnknownCommand(string commandName)
    {
        ConsoleUI.Error(
            $"Unknown command: {commandName}",
            Localization.GetString("errors.invalidCommand", $"不明なコマンド: {commandName}")
        );

        Console.WriteLine("\nSuggestions / 提案:");
        var suggestions = HelpSystem.SearchCommands(commandName);
        if (suggestions.Length > 0)
        {
            Console.ForegroundColor = ConsoleUI.Colors.Info;
            foreach (var suggestion in suggestions.Take(3))
            {
                Console.WriteLine($"  {ConsoleUI.Icons.Arrow} {suggestion}");
            }
            Console.ResetColor();
        }

        Console.WriteLine("\nRun 'Loco.Cli.exe help' to see all available commands.");
        Console.WriteLine("'Loco.Cli.exe help' を実行して、利用可能なすべてのコマンドを表示します。");
        return 1;
    }

    /// <summary>
    /// Handle fatal errors
    /// </summary>
    private static void HandleFatalError(Exception ex)
    {
        ConsoleUI.Error($"Fatal error: {ex.Message}", $"致命的なエラー: {ex.Message}");
        Console.ForegroundColor = ConsoleUI.Colors.Muted;
        Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        Console.ResetColor();
    }
}
