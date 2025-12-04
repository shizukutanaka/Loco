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
    static async Task<int> Main(string[] args)
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

            return command switch
            {
                "workflow" or "run" => await new WorkflowCommand().InvokeAsync(args[1..]),
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
        Console.WriteLine("  version           Show version information");
        Console.WriteLine("  help              Show this help message");
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
