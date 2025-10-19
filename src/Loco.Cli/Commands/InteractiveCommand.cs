using System;
using System.CommandLine;
using System.Threading.Tasks;
using Loco.Cli.UI;
using Loco.Core.UI;

namespace Loco.Cli.Commands;

/// <summary>
/// Interactive mode command for exploring Loco features
/// インタラクティブモードコマンド
/// </summary>
public class InteractiveCommand : Command
{
    public InteractiveCommand() : base("interactive", "Enter interactive mode")
    {
        this.SetHandler(RunInteractiveAsync);
    }

    private async Task<int> RunInteractiveAsync()
    {
        try
        {
            Console.Clear();
            ShowBanner();

            while (true)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("Interactive Mode Menu");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.ResetColor();
                Console.WriteLine();

                var options = new[]
                {
                    "System Health Check",
                    "View Resource Usage",
                    "List Workflows",
                    "Run Workflow",
                    "Infrastructure as Code (IaC)",
                    "View Configuration",
                    "Backup Configuration",
                    "Help Documentation",
                    "Exit"
                };

                var result = InteractivePrompt.Choice("What would you like to do?", options);

                if (result.Cancelled || result.SelectedIndex == options.Length - 1)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Exiting interactive mode...");
                    Console.ResetColor();
                    break;
                }

                Console.Clear();
                await HandleMenuSelection(result.SelectedIndex);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Press any key to continue...");
                Console.ResetColor();
                Console.ReadKey(true);
                Console.Clear();
                ShowBanner();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError in interactive mode: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private void ShowBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔════════════════════════════════════════════════════════════╗
║                                                            ║
║   LOCO - Interactive Mode                                 ║
║   Enterprise Automation Platform                          ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("  Navigate through Loco features interactively");
        Console.WriteLine("  Press Ctrl+C at any time to exit");
        Console.ResetColor();
    }

    private async Task HandleMenuSelection(int index)
    {
        switch (index)
        {
            case 0: // System Health Check
                await RunHealthCheckAsync();
                break;

            case 1: // View Resource Usage
                await RunResourceMonitorAsync();
                break;

            case 2: // List Workflows
                await ListWorkflowsAsync();
                break;

            case 3: // Run Workflow
                await RunWorkflowAsync();
                break;

            case 4: // Infrastructure as Code
                await RunIacMenuAsync();
                break;

            case 5: // View Configuration
                ShowConfiguration();
                break;

            case 6: // Backup Configuration
                await BackupConfigurationAsync();
                break;

            case 7: // Help Documentation
                ShowHelp();
                break;
        }
    }

    private async Task RunHealthCheckAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  System Health Check                                       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // Simulate health check
        var healthCommand = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "Loco.Cli.exe",
                Arguments = "health",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        healthCommand.Start();
        var output = await healthCommand.StandardOutput.ReadToEndAsync();
        await healthCommand.WaitForExitAsync();

        Console.WriteLine(output);
    }

    private async Task RunResourceMonitorAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Resource Monitor                                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var resourceCommand = new ResourceCommand();
        await resourceCommand.InvokeAsync(new[] { "stats", "--detailed" });
    }

    private async Task ListWorkflowsAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Available Workflows                                       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var workflowsDir = System.IO.Path.Combine("workflows");
        if (System.IO.Directory.Exists(workflowsDir))
        {
            var files = System.IO.Directory.GetFiles(workflowsDir, "*.json");
            if (files.Length > 0)
            {
                Console.WriteLine($"Found {files.Length} workflow(s):");
                Console.WriteLine();
                foreach (var file in files)
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(file);
                    Console.WriteLine($"  • {name}");
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No workflows found in workflows/ directory");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Workflows directory not found");
            Console.ResetColor();
        }

        await Task.CompletedTask;
    }

    private async Task RunWorkflowAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Run Workflow                                              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var workflowsDir = System.IO.Path.Combine("workflows");
        if (!System.IO.Directory.Exists(workflowsDir))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Workflows directory not found");
            Console.ResetColor();
            return;
        }

        var files = System.IO.Directory.GetFiles(workflowsDir, "*.json");
        if (files.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No workflows found");
            Console.ResetColor();
            return;
        }

        var options = new string[files.Length + 1];
        for (int i = 0; i < files.Length; i++)
        {
            options[i] = System.IO.Path.GetFileNameWithoutExtension(files[i]);
        }
        options[files.Length] = "Cancel";

        var result = InteractivePrompt.Choice("Select a workflow to run:", options);

        if (!result.Cancelled && result.SelectedIndex < files.Length)
        {
            var selectedFile = files[result.SelectedIndex];
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Running workflow: {System.IO.Path.GetFileName(selectedFile)}");
            Console.ResetColor();
            Console.WriteLine();

            var workflowCommand = new WorkflowCommand();
            await workflowCommand.InvokeAsync(selectedFile);
        }
    }

    private async Task RunIacMenuAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Infrastructure as Code (IaC)                              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var options = new[]
        {
            "Validate IaC file",
            "Deploy infrastructure",
            "Generate IaC from workflows",
            "Convert YAML/JSON",
            "Back to main menu"
        };

        var result = InteractivePrompt.Choice("IaC Operations:", options);

        if (result.Cancelled || result.SelectedIndex == options.Length - 1)
            return;

        Console.WriteLine();

        switch (result.SelectedIndex)
        {
            case 0: // Validate
                var validatePath = InteractivePrompt.Input("Enter IaC file path:", "examples/iac/infrastructure.yaml");
                if (!validatePath.Cancelled && !string.IsNullOrEmpty(validatePath.Value))
                {
                    var iacCommand = new IacCommand();
                    await iacCommand.InvokeAsync(new[] { "validate", validatePath.Value, "--detailed" });
                }
                break;

            case 1: // Deploy
                var deployPath = InteractivePrompt.Input("Enter IaC file path:", "examples/iac/infrastructure.yaml");
                if (!deployPath.Cancelled && !string.IsNullOrEmpty(deployPath.Value))
                {
                    var dryRun = InteractivePrompt.Confirm("Perform dry-run first?", true);
                    var iacCommand = new IacCommand();
                    if (dryRun)
                    {
                        await iacCommand.InvokeAsync(new[] { "deploy", deployPath.Value, "--dry-run" });
                    }
                    else
                    {
                        var confirm = InteractivePrompt.Confirm("Proceed with deployment?", false);
                        if (confirm)
                        {
                            await iacCommand.InvokeAsync(new[] { "deploy", deployPath.Value, "--verbose" });
                        }
                    }
                }
                break;

            case 2: // Generate
                var outputPath = InteractivePrompt.Input("Enter output file path:", "infrastructure.yaml");
                if (!outputPath.Cancelled && !string.IsNullOrEmpty(outputPath.Value))
                {
                    var iacCommand = new IacCommand();
                    await iacCommand.InvokeAsync(new[] { "generate", "workflows", "--output", outputPath.Value });
                }
                break;

            case 3: // Convert
                var inputPath = InteractivePrompt.Input("Enter input file path:", "examples/iac/infrastructure.yaml");
                if (!inputPath.Cancelled && !string.IsNullOrEmpty(inputPath.Value))
                {
                    var outputExt = inputPath.Value.EndsWith(".yaml") || inputPath.Value.EndsWith(".yml") ? ".json" : ".yaml";
                    var defaultOutput = System.IO.Path.ChangeExtension(inputPath.Value, outputExt);
                    var convOutputPath = InteractivePrompt.Input("Enter output file path:", defaultOutput);
                    if (!convOutputPath.Cancelled && !string.IsNullOrEmpty(convOutputPath.Value))
                    {
                        var iacCommand = new IacCommand();
                        await iacCommand.InvokeAsync(new[] { "convert", inputPath.Value, convOutputPath.Value });
                    }
                }
                break;
        }
    }

    private void ShowConfiguration()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Current Configuration                                     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("Application Information:");
        Console.WriteLine($"  Working Directory:  {Environment.CurrentDirectory}");
        Console.WriteLine($"  OS Version:         {Environment.OSVersion}");
        Console.WriteLine($"  .NET Version:       {Environment.Version}");
        Console.WriteLine($"  Processor Count:    {Environment.ProcessorCount}");
        Console.WriteLine($"  User:               {Environment.UserName}");
        Console.WriteLine($"  Machine:            {Environment.MachineName}");
    }

    private async Task BackupConfigurationAsync()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Backup Configuration                                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var confirm = InteractivePrompt.Confirm("Create a configuration backup?", true);
        if (confirm)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Creating backup...");
            Console.ResetColor();

            // Run backup-config command
            var backupCommand = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "Loco.Cli.exe",
                    Arguments = "backup-config create",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            backupCommand.Start();
            var output = await backupCommand.StandardOutput.ReadToEndAsync();
            await backupCommand.WaitForExitAsync();

            Console.WriteLine(output);
        }
    }

    private void ShowHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Help Documentation                                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        var helpTopics = new[]
        {
            "Main help (all commands)",
            "Workflow commands",
            "IaC commands",
            "Resource monitoring",
            "Health check",
            "Back to menu"
        };

        var result = InteractivePrompt.Choice("Select a help topic:", helpTopics);

        if (!result.Cancelled && result.SelectedIndex < helpTopics.Length - 1)
        {
            Console.WriteLine();
            string commandHelp = result.SelectedIndex switch
            {
                0 => "",
                1 => "workflow",
                2 => "iac",
                3 => "resource",
                4 => "health",
                _ => ""
            };

            new HelpSystem().ShowHelp(commandHelp);
        }
    }

    public async Task<int> InvokeAsync(string[] args)
    {
        return await RunInteractiveAsync();
    }
}
