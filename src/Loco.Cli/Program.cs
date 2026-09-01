using System;
using System.CommandLine;
using System.Reflection;
using System.Threading.Tasks;
using Loco.Cli.Commands;

namespace Loco.Cli;

/// <summary>
/// Main entry point for the Loco CLI application.
/// </summary>
class Program
{
    // Internal rather than private so the test project (InternalsVisibleTo in
    // Properties/AssemblyInfo.cs) can drive the real dispatch: which strings
    // reach which command, and which exit code comes back, is the CLI's
    // outermost contract.
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            // Enable Unicode support
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Enable reflection-based JSON serialization for .NET 8
            AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();
            var rest = args[1..];

            return command switch
            {
                // System.CommandLine-based commands (same proven pattern as workflow)
                "workflow" or "run" => await new WorkflowCommand().InvokeAsync(rest),
                "diag" => await new DiagCommand().InvokeAsync(rest),
                "files" => await new FilesCommand().InvokeAsync(rest),
                "health" => await new HealthCommand().InvokeAsync(rest),
                "logs" => await new LogsCommand().InvokeAsync(rest),
                "preset" => await new PresetCommand().InvokeAsync(rest),
                "rule" => await new RuleCommand().InvokeAsync(rest),
                "start" => await new StartCommand().InvokeAsync(rest),
                "test" => await new TestsCommand().InvokeAsync(rest),
                "update" => await new UpdateCommand().InvokeAsync(rest),

                // BaseCommand-based commands (dispatch via ExecuteAsync)
                "setup" => await new SetupCommand().ExecuteAsync(rest),
                "secrets" => await new SecretsCommand().ExecuteAsync(rest),

                // Static command classes
                "backup-config" => await BackupConfigCommand.ExecuteAsync(rest),

                "version" or "-v" or "--version" => ShowVersion(),
                "help" or "-h" or "--help" => ShowHelp(),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int ShowVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        Console.WriteLine($"Loco CLI v{version.Major}.{version.Minor}.{version.Build}");
        Console.WriteLine("軽量ワークフロー自動化ツール / Lightweight workflow automation tool");
        Console.WriteLine();
        Console.WriteLine("Runtime: " + Environment.Version);
        Console.WriteLine("Platform: " + Environment.OSVersion.Platform);
        return 0;
    }

    private static int ShowHelp()
    {
        Console.WriteLine("Loco CLI - Workflow Automation Tool");
        Console.WriteLine("====================================");
        Console.WriteLine();
        Console.WriteLine("Usage: loco <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  workflow <file>   Execute a workflow from a JSON file");
        Console.WriteLine("  run <file>        Alias for 'workflow'");
        Console.WriteLine("  start             Start the workflow engine");
        Console.WriteLine("  rule              Manage automation rules");
        Console.WriteLine("  preset            Run a preset (simulation)");
        Console.WriteLine("  files             File search / clean / organize utilities");
        Console.WriteLine("  logs              View and manage logs");
        Console.WriteLine("  health            Run health checks");
        Console.WriteLine("  diag              Diagnostics");
        Console.WriteLine("  test              Run the test suite");
        Console.WriteLine("  update            Check for updates");
        Console.WriteLine("  setup             Run the setup wizard");
        Console.WriteLine("  secrets           Manage stored secrets");
        Console.WriteLine("  backup-config     Backup / restore configuration");
        Console.WriteLine("  version           Show version information");
        Console.WriteLine("  help              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Run 'loco <command> --help' (or 'loco <command>' with no args) for");
        Console.WriteLine("command-specific usage.");
        Console.WriteLine();
        Console.WriteLine("Workflow Options:");
        Console.WriteLine("  --visualize, -v   Show workflow diagram (full, compact, deps)");
        Console.WriteLine("  --dry-run, -n     Validate without executing");
        Console.WriteLine("  --health          Run health check on the workflow");
        Console.WriteLine("  --lint, -l        Run linter on the workflow");
        Console.WriteLine("  --parallel, -p    Execute steps in parallel");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  loco workflow my-workflow.json");
        Console.WriteLine("  loco workflow my-workflow.json --dry-run");
        Console.WriteLine("  loco workflow my-workflow.json --visualize full");
        Console.WriteLine();
        return 0;
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unknown command: {command}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Run 'loco help' to see available commands.");
        return 1;
    }
}
