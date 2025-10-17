using System;
using System.IO;
using System.Threading.Tasks;
using Loco.Cli.UI;
using Loco.Core.Configuration;

namespace Loco.Cli.Commands;

/// <summary>
/// Setup command implementation
/// </summary>
public class SetupCommand : BaseCommand
{
    public override CommandHelp GetHelp() => new CommandHelp
    {
        Name = "setup",
        Description = "セットアップウィザードを実行 / Run setup wizard",
        Usage = "loco setup",
        Examples = new[] { "loco setup" }
    };

    public override async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.WriteLine($"Unknown setup options: {string.Join(' ', args)}");
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║           Loco Setup Wizard                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("Welcome to Loco - Enterprise Automation Platform");
        Console.WriteLine("このセットアップウィザードでLocoをインストールします。\n");

        // Step 1: Check prerequisites
        ConsoleUI.SectionHeader("Step 1: Checking Prerequisites", '=');
        if (!CheckPrerequisites())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Prerequisites check failed. Please resolve the issues above and try again.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ All prerequisites met!");
        Console.ResetColor();
        Console.WriteLine();

        // Step 2: Configuration
        ConsoleUI.SectionHeader("Step 2: Configuration", '=');
        var config = await ConfigureSettings();

        // Step 3: Directory setup
        ConsoleUI.SectionHeader("Step 3: Directory Setup", '=');
        if (!SetupDirectories())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Directory setup failed.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Directories created successfully!");
        Console.ResetColor();
        Console.WriteLine();

        // Step 4: Save configuration
        ConsoleUI.SectionHeader("Step 4: Saving Configuration", '=');
        if (!SaveConfiguration(config))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Configuration save failed.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Configuration saved!");
        Console.ResetColor();
        Console.WriteLine();

        // Completion
        ConsoleUI.SectionHeader("Setup Complete!", '=');
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("🎉 Loco has been successfully installed!");
        Console.WriteLine("🎉 Locoのインストールが完了しました！");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("Next steps / 次のステップ:");
        Console.WriteLine("1. Run 'loco health' to verify installation");
        Console.WriteLine("   'loco health' を実行してインストールを確認してください");
        Console.WriteLine("2. Run 'loco help' to see available commands");
        Console.WriteLine("   'loco help' を実行して利用可能なコマンドを確認してください");
        Console.WriteLine("3. Try 'loco preset system' for your first automation");
        Console.WriteLine("   'loco preset system' で最初の自動化をお試しください");
        Console.WriteLine();

        ConsoleUI.Tip("You can always re-run setup with 'loco setup' if needed.",
                      "必要に応じて 'loco setup' でセットアップを再実行できます。");

        return 0;
    }

    private static bool CheckPrerequisites()
    {
        var success = true;

        // Check .NET runtime
        Console.Write("Checking .NET runtime... ");
        try
        {
            var version = Environment.Version;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ .NET {version.Major}.{version.Minor} detected");
            Console.ResetColor();
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ .NET runtime not found");
            Console.ResetColor();
            Console.WriteLine("  Please install .NET 8.0 or later from https://dotnet.microsoft.com/download");
            success = false;
        }

        // Check write permissions
        Console.Write("Checking write permissions... ");
        try
        {
            var testFile = Path.Combine(Path.GetTempPath(), "loco_setup_test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Write permissions OK");
            Console.ResetColor();
        }
        catch
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Insufficient write permissions");
            Console.ResetColor();
            Console.WriteLine("  Please run as administrator or check file permissions");
            success = false;
        }

        return success;
    }

    private static async Task<LocoConfig> ConfigureSettings()
    {
        var config = new LocoConfig();

        // Ask for log level
        var logLevels = new[] { "Information", "Warning", "Error", "Debug" };
        var selectedLogLevel = ConsoleUI.ShowMenu(
            "Select log level / ログレベルを選択",
            logLevels,
            0,
            "ログレベルを選択してください");

        if (selectedLogLevel >= 0)
        {
            // Set log level based on selection
            config.LogLevel = logLevels[selectedLogLevel];
        }

        // Ask for max concurrent flows
        Console.Write("Maximum concurrent flows (1-10, default: 5): ");
        var maxFlowsInput = Console.ReadLine();
        if (int.TryParse(maxFlowsInput, out var maxFlows) && maxFlows >= 1 && maxFlows <= 10)
        {
            config.MaxConcurrentFlows = maxFlows;
        }
        else
        {
            config.MaxConcurrentFlows = 5;
            Console.WriteLine("Using default: 5");
        }

        return config;
    }

    private static bool SetupDirectories()
    {
        try
        {
            var directories = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "rules"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "plugins"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "data")
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Console.WriteLine($"Created directory: {dir}");
                }
                else
                {
                    Console.WriteLine($"Directory exists: {dir}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error creating directories: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool SaveConfiguration(LocoConfig config)
    {
        try
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco",
                "config.json");

            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(configPath, json);
            Console.WriteLine($"Configuration saved to: {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }
}
