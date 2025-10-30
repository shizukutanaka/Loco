using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Loco.Cli.Commands;
using Loco.Cli.UI;
using Loco.Cli.Services;

namespace Loco.Cli;

/// <summary>
/// Main entry point for the Loco CLI application.
/// Responsible for initialization and command routing.
/// </summary>
class Program
{
    private static ServiceContainer? _serviceContainer;
    private static HelpSystem? _helpSystem;
    private static LocalizationManager? _localization;

    /// <summary>
    /// Main entry point
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        try
        {
            InitializeEnvironment();
            InitializeServices();
            InitializeLocalization();

            if (args.Length == 0)
            {
                _helpSystem?.ShowHelp();
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
    /// Initialize dependency injection services
    /// </summary>
    private static void InitializeServices()
    {
        _serviceContainer = new ServiceContainer();
        _helpSystem = _serviceContainer.GetService<HelpSystem>();
        _localization = _serviceContainer.GetService<LocalizationManager>();
    }

    /// <summary>
    /// Initialize localization based on system culture
    /// </summary>
    private static void InitializeLocalization()
    {
        if (_localization == null)
            throw new InvalidOperationException("Localization service not initialized");

        var bestCulture = _localization.DetectBestCulture();
        _localization.CurrentCulture = bestCulture;
    }

    /// <summary>
    /// Route command to appropriate handler using dependency injection
    /// </summary>
    private static async Task<int> RouteCommand(string[] args)
    {
        if (_serviceContainer == null)
            throw new InvalidOperationException("Service container not initialized");

        var commandName = args[0].ToLowerInvariant();

        // Handle help command
        if (commandName is "help" or "-h" or "--help")
        {
            if (args.Length > 1)
            {
                _helpSystem?.ShowHelp(args[1]);
            }
            else
            {
                _helpSystem?.ShowHelp();
            }
            return 0;
        }

        // Create factory and execute command
        var factory = new CommandFactory(_serviceContainer);
        try
        {
            return await factory.ExecuteAsync(commandName, args.Skip(1).ToArray());
        }
        catch (CommandNotFoundException)
        {
            return HandleUnknownCommand(commandName);
        }
    }

    /// <summary>
    /// Handle unknown command
    /// </summary>
    private static int HandleUnknownCommand(string commandName)
    {
        if (_localization == null)
            throw new InvalidOperationException("Localization service not initialized");

        ConsoleUI.Error(
            $"Unknown command: {commandName}",
            _localization.GetString("errors.invalidCommand", $"不明なコマンド: {commandName}")
        );

        Console.WriteLine("\nSuggestions / 提案:");
        var suggestions = _helpSystem?.SearchCommands(commandName) ?? Array.Empty<string>();
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
        ConsoleUI.Error(
            $"Fatal error: {ex.Message}",
            $"致命的なエラー: {ex.Message}"
        );
        Console.ForegroundColor = ConsoleUI.Colors.Muted;
        Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        Console.ResetColor();
    }
}

