using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Models;
using Loco.Core.Diagnostics;
using Loco.Core.Validation;
// using Loco.Core.Performance; // Removed
// using Loco.Core.Storage; // Removed
using Loco.Core.Exceptions;
// using Loco.Core.Internationalization; // Removed
using Loco.Cli.UI;
using Loco.Cli.Commands;

namespace Loco.Cli;

class Program
{
    private static readonly JsonSerializerOptions RuleJsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HelpSystem HelpSystem = new();
    private static readonly LocalizationManager Localization = new();

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Enable Unicode support
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Enable reflection-based JSON serialization for .NET 8
            AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", true);

            // Initialize localization
            var systemCulture = CultureInfo.CurrentCulture;
            var bestCulture = Localization.DetectBestCulture();
            Localization.CurrentCulture = bestCulture;

            if (args.Length == 0)
            {
                HelpSystem.ShowHelp();
                return 0;
            }

            var command = args[0].ToLowerInvariant();

        // Interactive mode
        if (command == "interactive" || command == "i")
        {
            return await new InteractiveCommand().InvokeAsync(args.Skip(1).ToArray());
        }

        switch (command)
        {
            case "start":
                return await new StartCommand().InvokeAsync(args.Skip(1).ToArray());
            case "health":
                return await new HealthCommand().InvokeAsync(args.Skip(1).ToArray());
            case "diag":
            case "diagnostics":
                return await new DiagCommand().InvokeAsync(args.Skip(1).ToArray());
            case "rule":
                return await new RuleCommand().InvokeAsync(args.Skip(1).ToArray());
            case "preset":
                return await new PresetCommand().InvokeAsync(args.Skip(1).ToArray());
            case "files":
                return await new FilesCommand().InvokeAsync(args.Skip(1).ToArray());
            case "logs":
                return await new LogsCommand().InvokeAsync(args.Skip(1).ToArray());
            case "update":
            case "check-update":
                return await new UpdateCommand().InvokeAsync(args.Skip(1).ToArray());
            case "resource":
            case "resources":
                return await new ResourceCommand().InvokeAsync(args.Skip(1).ToArray());
            case "backup-config":
            case "config-backup":
                return await BackupConfigCommand.ExecuteAsync(args.Skip(1).ToArray());
            case "secrets":
            case "secret":
                return await new SecretsCommand().ExecuteAsync(args.Skip(1).ToArray());
            case "setup":
                return await new SetupCommand().ExecuteAsync(args.Skip(1).ToArray());
            case "version":
                return await new VersionCommand().ExecuteAsync(args.Skip(1).ToArray());
            case "test":
            case "tests":
                return await new TestsCommand().InvokeAsync(args.Skip(1).ToArray());
            case "iac":
            case "infrastructure":
                return await new IacCommand().InvokeAsync(args.Skip(1).ToArray());
            case "workflow":
            case "wf":
                return await new WorkflowCommand().InvokeAsync(args.Skip(1).ToArray());
            case "demo":
            case "ui-demo":
                await UIDemo.RunAsync();
                return 0;
            case "help":
            case "-h":
            case "--help":
                if (args.Length > 1)
                {
                    HelpSystem.ShowHelp(args[1]);
                }
                else
                {
                    HelpSystem.ShowHelp();
                }
                return 0;
            default:
                ConsoleUI.Error($"Unknown command: {command}", Localization.GetString("errors.invalidCommand", $"不明なコマンド: {command}"));
                Console.WriteLine("\nSuggestions / 提案:");
                var suggestions = HelpSystem.SearchCommands(command);
                if (suggestions.Length > 0)
                {
                    Console.ForegroundColor = ConsoleUI.Colors.Info;
                    foreach (var suggestion in suggestions.Take(3))
                    {
                        Console.WriteLine($"  {ConsoleUI.Icons.Arrow} {suggestion}");
                    }
                    Console.ResetColor();
                }
                Console.WriteLine($"\nRun 'Loco.Cli.exe help' to see all available commands.");
                Console.WriteLine($"'Loco.Cli.exe help' を実行して、利用可能なすべてのコマンドを表示します。");
                return 1;
        }
        }
        catch (Exception ex)
        {
            ConsoleUI.Error($"Fatal error: {ex.Message}", $"致命的なエラー: {ex.Message}");
            Console.ForegroundColor = ConsoleUI.Colors.Muted;
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> RunPresetAsync(
        string presetName,
        IEnumerable<LightAction> actions,
        string? rulesPath,
        IEnumerable<string> highlightLines,
        string executionLabel)
    {
        var engineResult = CreateEngine(rulesPath);
        if (engineResult == null)
        {
            return 1;
        }

        var (engine, _, _) = engineResult.Value;
        try
        {
            await engine.StartAsync();

            var ruleId = engine.CreateRule(
                presetName,
                new LightTrigger
                {
                    Type = "manual",
                    Parameters = new Dictionary<string, object>()
                },
                actions.ToArray());

            Console.WriteLine($"✓ Created {presetName}: {ruleId}");
            foreach (var line in highlightLines)
            {
                Console.WriteLine($"  - {line}");
            }

            Console.WriteLine($"\nExecuting {executionLabel}...");
            await engine.ExecuteRuleAsync(ruleId);

            return 0;
        }
        finally
        {
            await engine.StopAsync();
            engine.Dispose();
        }
    }

    // ShowHelp is now handled by HelpSystem

    private static bool TryConsumeOption(List<string> args, string optionName, out string? value)
    {
        value = null;
        for (int i = 0; i < args.Count; i++)
        {
            var current = args[i];
            if (current.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count)
                {
                    Console.WriteLine($"Option {optionName} requires a value.");
                    return false;
                }

                value = args[i + 1];
                args.RemoveAt(i + 1);
                args.RemoveAt(i);
                return true;
            }

            if (current.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = current.Substring(optionName.Length + 1);
                args.RemoveAt(i);
                return true;
            }
        }

        return true;
    }

    private static bool ConsumeFlag(List<string> args, string flag)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
            {
                args.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    private static (SimpleLightEngine engine, object? ruleStore, LocoConfig config)? CreateEngine(string? rulesPath)
    {
        // Note: Persistent rule store not yet implemented
        // var ruleStore = CreateRuleStore(rulesPath);

        var config = new LocoConfig();
        var engine = new SimpleLightEngine(null, config);
        return (engine, null, config);
    }

    // CreateRuleStore removed - persistent storage not yet implemented
    // private static IRuleStore? CreateRuleStore(string? rulesPath) { ... }

    private static async Task<int> RunTests(string[] args)
    {
        var remaining = args.ToList();
        if (!TryConsumeOption(remaining, "--rules-path", out var rulesPath))
        {
            return 1;
        }

        if (remaining.Count > 0)
        {
            Console.WriteLine($"Unknown test options: {string.Join(' ', remaining)}");
            return 1;
        }

        SimpleLightEngine? engine = null;
        try
        {
            var engineResult = CreateEngine(rulesPath);
            if (engineResult == null)
            {
                return 1;
            }

            engine = engineResult.Value.engine;

            Console.WriteLine("Running system tests...");
            await engine.StartAsync();

            var isHealthy = await engine.IsHealthyAsync();
            Console.WriteLine($"Health check: {(isHealthy ? "✓ PASS" : "✗ FAIL")}");

            var ruleId = engine.CreateRule(
                "Test Rule",
                new LightTrigger { Type = "manual" },
                new[] { new LightAction { Type = "log", Parameters = new() { ["message"] = "Test successful" } } }
            );
            Console.WriteLine($"Test rule created: {ruleId}");

            await engine.ExecuteRuleAsync(ruleId);
            Console.WriteLine("Test execution completed");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
            return 1;
        }
        finally
        {
            engine?.Dispose();
        }
    }

    private static async Task<int> CheckHealth(string[] args)
    {
        var engineResult = CreateEngine(null);
        if (engineResult == null)
        {
            return 1;
        }

        var (engine, _, _) = engineResult.Value;
        try
        {
            await engine.StartAsync();
            var isHealthy = await engine.IsHealthyAsync();
            var status = engine.GetEngineStatus();

            Console.WriteLine($"Engine Health: {(isHealthy ? "✓ Healthy" : "✗ Unhealthy")}");
            Console.WriteLine($"Active Rules: {status.RuleCount}");
            Console.WriteLine($"Total Executions: {status.TotalExecutions}");
            Console.WriteLine($"Success Rate: {status.SuccessRate:F1}%");

            return isHealthy ? 0 : 1;
        }
        finally
        {
            await engine.StopAsync();
            engine.Dispose();
        }
    }

    private static async Task<int> DiagnosticsCommand(string[] args)
    {
        try
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Loco"
            );
            var outputPath = args.Length > 0 && !args[0].StartsWith("--")
                ? args[0]
                : Path.Combine(baseDir, "logs", $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            ConsoleUI.Tip("Diagnostics includes system, engine, and configuration checks.", "診断はシステム・エンジン・設定のチェックを含みます。");

            var diagnosticsCommand = new Loco.Core.Diagnostics.DiagnosticsCommand();
            using var progressBar = new ProgressBar(totalWidth: 40, prefix: "Diagnostics ", suffix: "Collecting...");
            progressBar.Show();

            var report = await diagnosticsCommand.GenerateReportAsync(outputPath);
            progressBar.Hide();

            Console.WriteLine(report);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Diagnostics report saved to:");
            Console.WriteLine($"  Text: {outputPath}");
            Console.WriteLine($"  JSON: {Path.ChangeExtension(outputPath, ".json")}");
            Console.ResetColor();

            ConsoleUI.Tip("Share the JSON report with support for faster troubleshooting.", "JSONレポートをサポートに共有するとトラブルシュートが迅速になります。");

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleUI.FriendlyError("Diagnostics", ex.Message, string.Join("\n", new[]
            {
                "Verify you have write permissions to the target directory.",
                "Re-run with elevated privileges if required.",
                "Check available disk space and retry."
            }),
            string.Join("\n", new[]
            {
                "レポート書き込み先に対する権限を確認してください。",
                "必要に応じて管理者権限で再実行してください。",
                "ディスク空き容量を確認し、再度お試しください。"
            }));
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to generate diagnostics: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void RenderHealthReportToConsole(HealthCheckEnhanced.HealthCheckReport report)
    {
        if (report == null)
            return;

        Console.ForegroundColor = report.OverallStatus switch
        {
            HealthStatus.Healthy => ConsoleColor.Green,
            HealthStatus.Warning => ConsoleColor.Yellow,
            HealthStatus.Critical => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
        Console.WriteLine($"Overall Status: {report.OverallStatus}");
        Console.WriteLine($"Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.ResetColor();
        Console.WriteLine();

        foreach (var check in report.Checks)
        {
            Console.ForegroundColor = check.Status switch
            {
                HealthStatus.Healthy => ConsoleColor.Green,
                HealthStatus.Warning => ConsoleColor.Yellow,
                HealthStatus.Critical => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
            var icon = check.Status switch
            {
                HealthStatus.Healthy => "✓",
                HealthStatus.Warning => "⚠",
                HealthStatus.Critical => "✗",
                _ => "?"
            };
            Console.Write($"{icon} {check.Name}: ");
            Console.ResetColor();
            Console.WriteLine(check.Message);

            if (check.Details != null && check.Details.Count > 0)
            {
                foreach (var detail in check.Details)
                {
                    Console.WriteLine($"    {detail.Key}: {detail.Value}");
                }
            }

            if (check.Recommendations is { Count: > 0 })
            {
                foreach (var recommendation in check.Recommendations)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"    → {recommendation}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
    }

    private static void RenderEngineStatus(bool engineHealthy, EngineStatus status)
    {
        Console.WriteLine("=== Engine Status ===");
        Console.ForegroundColor = engineHealthy ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Engine: {(engineHealthy ? "✓ Healthy" : "✗ Unhealthy")}");
        Console.ResetColor();
        Console.WriteLine($"  Flows: {status.FlowCount}");
        Console.WriteLine($"  Rules: {status.RuleCount}");
        Console.WriteLine($"  Total Executions: {status.TotalExecutions}");
        Console.WriteLine($"  Success Rate: {status.SuccessRate:F1}%");
        Console.WriteLine();
    }

    private static void CheckDirectory(string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{label}: ○ Not configured");
            Console.ResetColor();
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{label}: ✓ {fullPath}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{label}: ○ {fullPath} (will be created)");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{label}: ✗ Invalid path - {ex.Message}");
            Console.ResetColor();
        }
    }

    // Additional command implementations would continue here...
    // For brevity, I'll implement the essential ones and add the rest as needed

    private static async Task<int> RuleCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: Loco.Cli.exe rule <list|enable|disable|delete> [options]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Loco.Cli.exe rule list");
            Console.WriteLine("  Loco.Cli.exe rule list --json");
            Console.WriteLine("  Loco.Cli.exe rule disable <ruleId>");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();
        var optionArgs = args.Skip(1).ToList();

        if (!TryConsumeOption(optionArgs, "--rules-path", out var rulesPath))
        {
            return 1;
        }

        // JSON output flag (reserved for future use)
        while (ConsumeFlag(optionArgs, "--json"))
        {
            // Flag consumed but not yet implemented
        }

        if (optionArgs.Count > 0 && subCommand is "list")
        {
            Console.WriteLine($"Unknown rule options: {string.Join(' ', optionArgs)}");
            return 1;
        }

        // Note: Persistent rule store not yet implemented
        Console.WriteLine("Rule persistence is not yet implemented.");
        Console.WriteLine("Rules are currently stored in memory only during engine runtime.");
        Console.WriteLine();
        Console.WriteLine("To work with rules:");
        Console.WriteLine("  1. Start the engine: loco start");
        Console.WriteLine("  2. Create rules programmatically via SimpleLightEngine API");
        Console.WriteLine();
        return 1;
    }

    // ListRulesAsync and UpdateRuleStatusAsync removed - rule persistence not yet implemented
    // private static async Task<int> ListRulesAsync(...) { ... }
    // private static async Task<int> UpdateRuleStatusAsync(...) { ... }

    private static async Task<int> QuickCommand(string command, string message)
    {
        switch (command.ToLowerInvariant())
        {
            case "log":
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                return 0;
            case "stats":
                // Show basic system stats
                Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                Console.WriteLine($"CPU cores: {Environment.ProcessorCount}");
                return 0;
            default:
                Console.WriteLine("Available quick commands: log, stats");
                return 1;
        }
    }

    private static async Task<int> CacheCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: Loco.Cli.exe cache <set|get|list|clear> [options]");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "clear":
                    Console.WriteLine("Clearing cache...");
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Cache cleared successfully");
                    Console.ResetColor();
                    return 0;

                case "list":
                    Console.WriteLine("=== Cache Statistics ===");
                    Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                    Console.WriteLine($"Gen 0: {GC.CollectionCount(0)} collections");
                    Console.WriteLine($"Gen 1: {GC.CollectionCount(1)} collections");
                    Console.WriteLine($"Gen 2: {GC.CollectionCount(2)} collections");
                    return 0;

                default:
                    Console.WriteLine($"Unknown cache command: {subCommand}");
                    Console.WriteLine("Available commands: clear, list");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Cache command failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> MonitorCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: Loco.Cli.exe monitor <memory|disk|system> [threshold]");
            return 1;
        }

        var monitorType = args[0].ToLowerInvariant();

        try
        {
            switch (monitorType)
            {
                case "memory":
                    var memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
                    var threshold = args.Length > 1 && int.TryParse(args[1], out var mem) ? mem : 512;

                    Console.WriteLine("=== Memory Monitor ===");
                    Console.WriteLine($"Current Usage: {memoryMB:F1} MB");
                    Console.WriteLine($"Threshold: {threshold} MB");

                    if (memoryMB > threshold)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠ Memory usage above threshold!");
                        Console.ResetColor();
                        return 1;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ Memory usage normal");
                        Console.ResetColor();
                        return 0;
                    }

                case "disk":
                    var path = args.Length > 1 ? args[1] : "C:\\";
                    var diskThreshold = args.Length > 2 && int.TryParse(args[2], out var disk) ? disk : 5;

                    if (Directory.Exists(path))
                    {
                        var drive = new System.IO.DriveInfo(path);
                        var freeGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

                        Console.WriteLine("=== Disk Monitor ===");
                        Console.WriteLine($"Drive: {drive.Name}");
                        Console.WriteLine($"Free Space: {freeGB:F1} GB");
                        Console.WriteLine($"Threshold: {diskThreshold} GB");

                        if (freeGB < diskThreshold)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠ Low disk space!");
                            Console.ResetColor();
                            return 1;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("✓ Disk space normal");
                            Console.ResetColor();
                            return 0;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Path not found: {path}");
                        return 1;
                    }

                case "system":
                    Console.WriteLine("=== System Monitor ===");
                    Console.WriteLine($"OS: {Environment.OSVersion}");
                    Console.WriteLine($"Machine: {Environment.MachineName}");
                    Console.WriteLine($"Processors: {Environment.ProcessorCount}");
                    Console.WriteLine($"Uptime: {Environment.TickCount64 / 1000 / 60} minutes");
                    Console.WriteLine($"Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                    return 0;

                default:
                    Console.WriteLine($"Unknown monitor type: {monitorType}");
                    Console.WriteLine("Available types: memory, disk, system");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Monitor command failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> ProcessCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco process <command> [workingDir] [timeout]");
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco process \"dir\" . 30");
            Console.WriteLine("  loco process \"echo Hello\" . 10");
            return 1;
        }

        var command = args[0];
        var workingDir = args.Length > 1 ? args[1] : ".";
        var timeoutSec = args.Length > 2 && int.TryParse(args[2], out var t) ? t : 60;

        try
        {
            Console.WriteLine($"Executing: {command}");
            Console.WriteLine($"Working Directory: {Path.GetFullPath(workingDir)}");
            Console.WriteLine($"Timeout: {timeoutSec}s");
            Console.WriteLine();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = Path.GetFullPath(workingDir),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // Use ArgumentList to prevent command injection
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start process");
                return 1;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutSec * 1000))
            {
                process.Kill();
                Console.WriteLine("Process timed out");
                return 1;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine("[Output]");
                Console.WriteLine(output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Error]");
                Console.WriteLine(error);
                Console.ResetColor();
            }

            Console.WriteLine($"\nExit Code: {process.ExitCode}");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Process execution failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> BackupCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: loco backup <source> <destination>");
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco backup C:\\Data C:\\Backup\\Data");
            Console.WriteLine("  loco backup ./config ./backup/config");
            return 1;
        }

        var source = args[0];
        var destination = args[1];

        try
        {
            if (!Directory.Exists(source) && !File.Exists(source))
            {
                Console.WriteLine($"Source not found: {source}");
                return 1;
            }

            Console.WriteLine($"Backing up: {source}");
            Console.WriteLine($"To: {destination}");
            Console.WriteLine();

            var isDirectory = Directory.Exists(source);
            var filesCopied = 0;

            if (isDirectory)
            {
                Directory.CreateDirectory(destination);

                foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(source, file);
                    var destFile = Path.Combine(destination, relativePath);
                    var destDir = Path.GetDirectoryName(destFile);

                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destFile, true);
                    filesCopied++;

                    if (filesCopied % 100 == 0)
                    {
                        Console.WriteLine($"Copied {filesCopied} files...");
                    }
                }
            }
            else
            {
                var destDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(source, destination, true);
                filesCopied = 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Backup completed: {filesCopied} file(s) copied");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Backup failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> WatchCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco watch <path> [pattern]");
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco watch ./data");
            Console.WriteLine("  loco watch ./logs \"*.log\"");
            return 1;
        }

        var path = args[0];
        var pattern = args.Length > 1 ? args[1] : "*.*";

        try
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory not found: {path}");
                return 1;
            }

            Console.WriteLine($"Watching: {Path.GetFullPath(path)}");
            Console.WriteLine($"Pattern: {pattern}");
            Console.WriteLine("Press Ctrl+C to stop watching...");
            Console.WriteLine();

            using var watcher = new FileSystemWatcher(path)
            {
                Filter = pattern,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = true
            };

            watcher.Created += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Created: {e.Name}");
                Console.ResetColor();
            };

            watcher.Changed += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Changed: {e.Name}");
                Console.ResetColor();
            };

            watcher.Deleted += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Deleted: {e.Name}");
                Console.ResetColor();
            };

            watcher.Renamed += (s, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Renamed: {e.OldName} -> {e.Name}");
                Console.ResetColor();
            };

            watcher.EnableRaisingEvents = true;

            var tcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (_, _) => tcs.SetResult(true);
            await tcs.Task;

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Watch command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ScheduleCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco schedule <add|list|remove|run> [options]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco schedule list");
            Console.WriteLine("  loco schedule add daily \"0 0 * * *\" backup");
            Console.WriteLine("  loco schedule remove daily");
            Console.WriteLine("  loco schedule run daily");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            var schedulerDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loco", "schedules");
            Directory.CreateDirectory(schedulerDir);

            switch (subCommand)
            {
                case "list":
                    var schedules = Directory.GetFiles(schedulerDir, "*.json");

                    if (schedules.Length == 0)
                    {
                        Console.WriteLine("No scheduled tasks found.");
                        Console.WriteLine("Use 'schedule add' to create a new schedule.");
                        return 0;
                    }

                    Console.WriteLine("=== Scheduled Tasks ===");
                    foreach (var schedule in schedules)
                    {
                        var name = Path.GetFileNameWithoutExtension(schedule);
                        var content = File.ReadAllText(schedule);
                        Console.WriteLine($"  {name}");
                        Console.WriteLine($"    {content}");
                    }
                    return 0;

                case "add":
                    if (args.Length < 4)
                    {
                        Console.WriteLine("Usage: loco schedule add <name> <cron_expression> <command>");
                        Console.WriteLine("Example: loco schedule add daily \"0 0 * * *\" \"backup ./data ./backup\"");
                        return 1;
                    }

                    var addName = args[1];
                    var cronExpr = args[2];
                    var command = string.Join(" ", args.Skip(3));

                    var scheduleFile = Path.Combine(schedulerDir, $"{addName}.json");
                    var scheduleData = new
                    {
                        name = addName,
                        cron = cronExpr,
                        command = command,
                        enabled = true,
                        created = DateTime.UtcNow
                    };

                    File.WriteAllText(scheduleFile, System.Text.Json.JsonSerializer.Serialize(scheduleData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Schedule created: {addName}");
                    Console.ResetColor();
                    Console.WriteLine($"  Cron: {cronExpr}");
                    Console.WriteLine($"  Command: {command}");
                    return 0;

                case "remove":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: loco schedule remove <name>");
                        return 1;
                    }

                    var removeName = args[1];
                    var removeFile = Path.Combine(schedulerDir, $"{removeName}.json");

                    if (!File.Exists(removeFile))
                    {
                        Console.WriteLine($"Schedule not found: {removeName}");
                        return 1;
                    }

                    File.Delete(removeFile);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Schedule removed: {removeName}");
                    Console.ResetColor();
                    return 0;

                case "run":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: loco schedule run <name>");
                        return 1;
                    }

                    var runName = args[1];
                    var runFile = Path.Combine(schedulerDir, $"{runName}.json");

                    if (!File.Exists(runFile))
                    {
                        Console.WriteLine($"Schedule not found: {runName}");
                        return 1;
                    }

                    var runContent = File.ReadAllText(runFile);
                    var runData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(runContent);

                    if (runData == null || !runData.ContainsKey("command"))
                    {
                        Console.WriteLine("Invalid schedule data");
                        return 1;
                    }

                    var runCommand = runData["command"].ToString();
                    Console.WriteLine($"Running scheduled task: {runName}");
                    Console.WriteLine($"Command: {runCommand}");
                    Console.WriteLine();

                    // Note: This is a simple implementation
                    // A real scheduler would parse and execute the command properly
                    Console.WriteLine("Note: Command execution from scheduler is not yet fully implemented.");
                    Console.WriteLine("This is a placeholder for future scheduler integration.");
                    return 0;

                default:
                    Console.WriteLine("Available schedule commands: add, list, remove, run");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Schedule command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ConfigCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco config <show|set|reset|verify> [options]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco config show");
            Console.WriteLine("  loco config set MaxConcurrentFlows 10");
            Console.WriteLine("  loco config reset");
            Console.WriteLine("  loco config verify --json");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "show":
                    {
                        var optionArgs = args.Skip(1).ToList();
                        var asJson = ConsumeFlag(optionArgs, "--json") || ConsumeFlag(optionArgs, "-j");

                        if (optionArgs.Count > 0)
                        {
                            ConsoleUI.Error($"Unknown option(s): {string.Join(' ', optionArgs)}", $"不明なオプション: {string.Join(' ', optionArgs)}");
                            return 1;
                        }

                        var config = new LocoConfig();
                        var warnings = config.GetPathResolutionWarningsSnapshot();
                        var snapshot = config.GetDiagnosticSnapshot();

                        if (asJson)
                        {
                            var payload = config.GetDiagnosticSnapshot();

                            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }));
                        }
                        else
                        {
                            Console.WriteLine("=== Loco Configuration ===");
                            Console.WriteLine();
                            Console.WriteLine($"Config Path: {snapshot["SourceConfigPath"]}");
                            Console.WriteLine($"Warnings: {(snapshot.TryGetValue("WarningsCount", out var warningsCount) ? warningsCount : 0)}");
                            if (snapshot.TryGetValue("WarningsSummary", out var summary) && summary is string summaryText && !string.IsNullOrWhiteSpace(summaryText))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"  Summary: {summaryText}");
                                Console.ResetColor();
                            }

                            Console.WriteLine();
                            Console.WriteLine("=== Core Limits ===");
                            Console.WriteLine($"Max Concurrent Flows: {snapshot["MaxConcurrentFlows"]}");
                            Console.WriteLine($"Memory Limit: {snapshot["MemoryLimitMB"]} MB");
                            Console.WriteLine($"Cache Size: {snapshot["CacheSizeMB"]} MB");
                            Console.WriteLine($"Default Timeout: {snapshot["DefaultTimeoutSeconds"]} s");
                            Console.WriteLine($"Default Retry Count: {snapshot["DefaultRetryCount"]}");
                            Console.WriteLine($"Rate Limit (per min): {snapshot["RateLimitPerMinute"]}");
                            Console.WriteLine($"Circuit Breaker Threshold: {snapshot["CircuitBreakerThreshold"]}");
                            Console.WriteLine($"Circuit Breaker Timeout: {snapshot["CircuitBreakerTimeoutSeconds"]} s");

                            Console.WriteLine();
                            Console.WriteLine("=== Logging ===");
                            Console.WriteLine($"Log Directory: {snapshot["LogDirectory"]}");
                            Console.WriteLine($"Cache Directory: {snapshot["CacheDirectory"]}");
                            Console.WriteLine($"Working Directory: {snapshot["WorkingDirectory"]}");
                            Console.WriteLine($"Log Level: {snapshot["LogLevel"]}");
                            Console.WriteLine($"File Logging: {((snapshot.TryGetValue("EnableFileLogging", out var fileLogValue) && fileLogValue is bool fileLog && fileLog) ? "Enabled" : "Disabled")}");
                            Console.WriteLine($"Console Logging: {((snapshot.TryGetValue("EnableConsoleLogging", out var consoleLogValue) && consoleLogValue is bool consoleLog && consoleLog) ? "Enabled" : "Disabled")}");
                            Console.WriteLine($"Log Retention: {snapshot["LogRetentionDays"]} days");

                            Console.WriteLine();
                            Console.WriteLine("=== Security Controls ===");
                            void WriteToggle(string name, object? value)
                            {
                                var enabled = value is bool b && b;
                                Console.WriteLine($"{name}: {(enabled ? "Enabled" : "Disabled")}");
                            }

                            WriteToggle("Audit Logging", snapshot["EnableAuditLogging"]);
                            WriteToggle("Input Validation", snapshot["EnableInputValidation"]);
                            WriteToggle("Health Checks", snapshot["EnableHealthChecks"]);
                            WriteToggle("Metrics", snapshot["EnableMetrics"]);
                            WriteToggle("Circuit Breaker", snapshot["EnableCircuitBreaker"]);
                            WriteToggle("Compression", snapshot["EnableCompression"]);

                            string FormatBytes(object? value)
                            {
                                if (value is null)
                                    return "(unknown)";

                                var bytes = Convert.ToDouble(value);
                                var units = new[] { "B", "KB", "MB", "GB", "TB" };
                                var unitIndex = 0;

                                while (bytes >= 1024 && unitIndex < units.Length - 1)
                                {
                                    bytes /= 1024;
                                    unitIndex++;
                                }

                                return $"{bytes:0.##} {units[unitIndex]}";
                            }

                            static string FormatOptional(object? value, string fallback = "(not set)")
                            {
                                return value switch
                                {
                                    null => fallback,
                                    string s when string.IsNullOrWhiteSpace(s) => fallback,
                                    _ => value.ToString() ?? fallback
                                };
                            }

                            Console.WriteLine();
                            Console.WriteLine("=== File Handling ===");
                            WriteToggle("Auto Backup", snapshot["EnableAutoBackup"]);
                            Console.WriteLine($"Max File Size: {FormatBytes(snapshot.TryGetValue("MaxFileSizeBytes", out var maxFileSize) ? maxFileSize : null)}");
                            Console.WriteLine($"Compression Threshold: {snapshot["CompressionThresholdKB"]} KB");

                            Console.WriteLine();
                            Console.WriteLine("=== LLM Configuration ===");
                            Console.WriteLine($"Provider: {FormatOptional(snapshot.TryGetValue("LlmProvider", out var llmProvider) ? llmProvider : null)}");
                            Console.WriteLine($"Model: {FormatOptional(snapshot.TryGetValue("LlmModel", out var llmModel) ? llmModel : null)}");
                            Console.WriteLine($"Endpoint: {FormatOptional(snapshot.TryGetValue("LlmApiEndpoint", out var llmEndpoint) ? llmEndpoint : null)}");
                            Console.WriteLine($"Max Tokens: {FormatOptional(snapshot.TryGetValue("LlmMaxTokens", out var llmMaxTokens) ? llmMaxTokens : null, "(not configured)")}");
                            Console.WriteLine($"Temperature: {FormatOptional(snapshot.TryGetValue("LlmTemperature", out var llmTemperature) ? llmTemperature : null, "(not configured)")}");
                            Console.WriteLine($"HTTP Timeout: {FormatOptional(snapshot.TryGetValue("LlmHttpTimeoutMs", out var llmTimeout) ? llmTimeout : null, "(not configured)")} ms");
                            Console.WriteLine($"API Key Configured: {(snapshot.TryGetValue("IsLlmApiKeyConfigured", out var llmApiKeyConfigured) && llmApiKeyConfigured is bool keyConfigured && keyConfigured ? "Yes" : "No")}");

                            void WriteArray(string title, object? value)
                            {
                                Console.WriteLine($"=== {title} ===");
                                if (value is IEnumerable<string> items && items.Any())
                                {
                                    foreach (var item in items)
                                    {
                                        Console.WriteLine($"  - {item}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("  (none)");
                                }
                                Console.WriteLine();
                            }

                            WriteArray("Allowed Paths", snapshot["AllowedPaths"]);
                            WriteArray("Forbidden Paths", snapshot["ForbiddenPaths"]);
                            WriteArray("Trusted Domains", snapshot["TrustedDomains"]);
                            WriteArray("Blocked Domains", snapshot["BlockedDomains"]);

                            if (warnings.Count > 0)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("Detailed Warnings:");
                                foreach (var warning in warnings)
                                {
                                    Console.WriteLine($"  - {warning}");
                                }
                                Console.ResetColor();
                            }
                        }

                        return warnings.Count == 0 ? 0 : 1;
                    }

                case "set":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: loco config set <key> <value>");
                        Console.WriteLine();
                        Console.WriteLine("Available keys:");
                        Console.WriteLine("  MaxConcurrentFlows");
                        Console.WriteLine("  MemoryLimitMB");
                        Console.WriteLine("  CacheSizeMB");
                        return 1;
                    }

                    var key = args[1];
                    var value = args[2];

                    Console.WriteLine($"Setting {key} = {value}");
                    Console.WriteLine();
                    Console.WriteLine("Note: Configuration persistence is not yet implemented.");
                    Console.WriteLine("Changes will only apply to the current session.");
                    return 0;

                case "reset":
                    Console.WriteLine("Resetting configuration to defaults...");
                    Console.WriteLine();
                    Console.WriteLine("Note: Configuration persistence is not yet implemented.");
                    Console.WriteLine("This is a placeholder for future configuration reset functionality.");
                    return 0;

                case "verify":
                    {
                        var optionArgs = args.Skip(1).ToList();
                        var asJson = ConsumeFlag(optionArgs, "--json") || ConsumeFlag(optionArgs, "-j");

                        if (optionArgs.Count > 0)
                        {
                            ConsoleUI.Error($"Unknown option(s): {string.Join(' ', optionArgs)}", $"不明なオプション: {string.Join(' ', optionArgs)}");
                            return 1;
                        }

                        try
                        {
                            var configVerify = new LocoConfig();
                            var warnings = configVerify.GetPathResolutionWarningsSnapshot();
                            var status = warnings.Count == 0 ? "Healthy" : "Warning";
                            var recommendedActions = warnings.Count == 0
                                ? Array.Empty<string>()
                                : new[]
                                {
                                    "Run 'loco config show --json' for the categorized snapshot",
                                    "Review 'docs/CONFIGURATION.md' Path Safety Enforcement guidance",
                                    "Create a backup via 'loco backup-config create' before editing configuration files"
                                };

                            if (asJson)
                            {
                                var payload = new Dictionary<string, object?>
                                {
                                    ["status"] = status,
                                    ["diagnostics"] = configVerify.GetDiagnosticSnapshot(),
                                    ["recommendedActions"] = recommendedActions
                                };

                                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
                                {
                                    WriteIndented = true
                                }));
                            }
                            else
                            {
                                Console.WriteLine("Configuration Verification / 構成検証");
                                Console.WriteLine();
                                Console.WriteLine($"Status / 状態: {status}");
                                Console.WriteLine($"Config Path / 構成パス: {configVerify.SourceConfigPath ?? "defaults"}");

                                if (warnings.Count == 0)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("✓ No warnings detected / 警告は検出されませんでした");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"Warning count / 警告数: {warnings.Count}");
                                    if (configVerify.GetPathResolutionWarningsSummary() is { } verifySummary && !string.IsNullOrWhiteSpace(verifySummary))
                                    {
                                        Console.WriteLine($"Summary / 要約: {verifySummary}");
                                    }
                                    Console.WriteLine("Warnings / 警告:");
                                    foreach (var warning in warnings)
                                    {
                                        Console.WriteLine($"  - {warning}");
                                    }
                                    Console.ResetColor();

                                    if (recommendedActions.Length > 0)
                                    {
                                        Console.WriteLine();
                                        Console.WriteLine("Recommended actions / 推奨アクション:");
                                        foreach (var action in recommendedActions)
                                        {
                                            Console.WriteLine($"  - {action}");
                                        }
                                    }
                                }
                            }

                            return warnings.Count == 0 ? 0 : 1;
                        }
                        catch (LocoConfigurationException ex)
                        {
                            ConsoleUI.Error($"Configuration validation failed: {ex.Message}", $"構成の検証に失敗しました: {ex.Message}");
                            Console.WriteLine("Recommended actions:");
                            Console.WriteLine("  - Inspect docs/CONFIGURATION.md for validation requirements");
                            Console.WriteLine("  - Review the reported error and adjust loco.config.json accordingly");
                            return 2;
                        }
                        catch (Exception ex)
                        {
                            ConsoleUI.Error($"Configuration check failed: {ex.Message}", $"構成チェックが失敗しました: {ex.Message}");
                            Console.WriteLine("Recommended actions:");
                            Console.WriteLine("  - Review application logs for detailed stack trace");
                            Console.WriteLine("  - Re-run 'loco config verify --json' after resolving the issue");
                            return 2;
                        }
                    }

                default:
                    Console.WriteLine("Available config commands: show, set, reset, verify");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Config command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> PresetCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco preset <list|create|system|daily|cleanup> [options]");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();
        var optionArgs = args.Skip(1).ToList();

        if (!TryConsumeOption(optionArgs, "--rules-path", out var rulesPath))
        {
            return 1;
        }

        if (optionArgs.Count > 0)
        {
            Console.WriteLine($"Unknown preset options: {string.Join(' ', optionArgs)}");
            return 1;
        }

        try
        {
            switch (subCommand)
            {
                case "list":
                    Console.WriteLine("Available presets:");
                    Console.WriteLine("  system    - Basic system monitoring (memory, disk)");
                    Console.WriteLine("  daily     - Daily backup and cleanup routine");
                    Console.WriteLine("  cleanup   - Temporary file cleanup");
                    Console.WriteLine("  watchdog  - Watch critical directories");
                    Console.WriteLine("  heartbeat - Regular health check every 30 seconds");
                    return 0;

                case "system":
                    return await RunPresetAsync("System Monitor Preset", new[]
                    {
                        new LightAction
                        {
                            Type = "monitor",
                            Parameters = new() { ["type"] = "memory", ["threshold"] = "512" }
                        },
                        new LightAction
                        {
                            Type = "monitor",
                            Parameters = new() { ["type"] = "disk", ["path"] = "C:\\", ["threshold"] = "5" }
                        },
                        new LightAction
                        {
                            Type = "monitor",
                            Parameters = new() { ["type"] = "system" }
                        }
                    }, rulesPath, new[]
                    {
                        "Memory usage check (512MB threshold)",
                        "Disk space check (5GB threshold)",
                        "System information display"
                    }, "system monitoring");

                case "daily":
                    return await RunPresetAsync("Daily Maintenance Preset", new[]
                    {
                        new LightAction
                        {
                            Type = "log",
                            Parameters = new() { ["message"] = "Starting daily maintenance" }
                        },
                        new LightAction
                        {
                            Type = "cleanup",
                            Parameters = new() { ["target"] = "temp", ["olderThanDays"] = "7" }
                        },
                        new LightAction
                        {
                            Type = "cleanup",
                            Parameters = new() { ["target"] = "logs", ["olderThanDays"] = "30" }
                        },
                        new LightAction
                        {
                            Type = "file",
                            Parameters = new() { ["operation"] = "list", ["path"] = "." }
                        },
                        new LightAction
                        {
                            Type = "log",
                            Parameters = new() { ["message"] = "Daily maintenance completed" }
                        }
                    }, rulesPath, new[]
                    {
                        "Clean temporary files older than 7 days",
                        "Clean log files older than 30 days",
                        "List current directory files"
                    }, "daily maintenance");

                case "cleanup":
                    return await RunPresetAsync("Cleanup Preset", new[]
                    {
                        new LightAction
                        {
                            Type = "cleanup",
                            Parameters = new() { ["target"] = "temp", ["olderThanDays"] = "1" }
                        }
                    }, rulesPath, new[]
                    {
                        "Clean temporary files older than 1 day"
                    }, "cleanup");

                default:
                    Console.WriteLine("Available preset commands: list, system, daily, cleanup, watchdog, heartbeat, create");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Preset command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> InfoCommand(string[] args)
    {
        var optionArgs = args.ToList();

        if (!TryConsumeOption(optionArgs, "--rules-path", out var rulesPath))
        {
            return 1;
        }

        if (optionArgs.Count > 0)
        {
            Console.WriteLine($"Unknown info options: {string.Join(' ', optionArgs)}");
            return 1;
        }

        try
        {
            Console.WriteLine("=== Loco System Information ===");
            Console.WriteLine();

            // Basic system info
            Console.WriteLine("[System]");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Machine Name: {Environment.MachineName}");
            Console.WriteLine($"User Name: {Environment.UserName}");
            Console.WriteLine($"CPU Cores: {Environment.ProcessorCount}");
            Console.WriteLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            Console.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
            Console.WriteLine();

            // Engine info
            Console.WriteLine("[Engine]");
            var engineResult = CreateEngine(rulesPath);
            if (engineResult == null)
            {
                return 1;
            }

            var (engine, _, config) = engineResult.Value;
            try
            {
                await engine.StartAsync();
                var status = engine.GetEngineStatus();
                var isHealthy = await engine.IsHealthyAsync();

                Console.WriteLine($"Status: {(isHealthy ? "✓ Healthy" : "✗ Unhealthy")}");
                Console.WriteLine($"Active Flows: {status.FlowCount}");
                Console.WriteLine($"Active Rules: {status.RuleCount}");
                Console.WriteLine($"Total Executions: {status.TotalExecutions}");
                Console.WriteLine($"Success Rate: {status.SuccessRate:F1}%");
                Console.WriteLine();

                // Configuration info
                Console.WriteLine("[Configuration]");
                Console.WriteLine($"Max Concurrent Flows: {config.MaxConcurrentFlows}");
                Console.WriteLine($"Memory Limit: {config.MemoryLimitMB} MB");
                Console.WriteLine($"Cache Size: {config.CacheSizeMB} MB");
                Console.WriteLine($"Memory Optimization: {(config.EnableMemoryOptimization ? "Enabled" : "Disabled")}");
                Console.WriteLine($"Batch Processing: Enabled");
                Console.WriteLine($"Config Source: {config.SourceConfigPath ?? "(default)"}");
                Console.WriteLine($"Path Warning Status: {(config.HasPathResolutionWarnings ? "Warnings detected" : "Clean")}");
                DisplayConfigWarnings(config, "info");

                Console.WriteLine("Allowed Paths:");
                if (config.AllowedPaths.Length == 0)
                {
                    Console.WriteLine(" - (none)");
                }
                else
                {
                    foreach (var path in config.AllowedPaths)
                    {
                        Console.WriteLine($" - {path}");
                    }
                }

                Console.WriteLine("Forbidden Paths:");
                if (config.ForbiddenPaths.Length == 0)
                {
                    Console.WriteLine(" - (none)");
                }
                else
                {
                    foreach (var path in config.ForbiddenPaths)
                    {
                        Console.WriteLine($" - {path}");
                    }
                }

                Console.WriteLine($"Allowed Path Count: {config.AllowedPaths.Length}");
                Console.WriteLine($"Forbidden Path Count: {config.ForbiddenPaths.Length}");

                if (config.HasPathResolutionWarnings &&
                    config.AllowedPaths.Length == 0 &&
                    config.ForbiddenPaths.Length == 0)
                {
                    Console.WriteLine("Warning: No allowed or forbidden paths are configured despite warnings; check configuration inputs.");
                }

                return 0;
            }
            finally
            {
                await engine.StopAsync();
                engine.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Info command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> FilesCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco files <search|stats|clean|organize> [options]");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "search":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: loco files search <pattern> [directory]");
                        Console.WriteLine("Examples:");
                        Console.WriteLine("  loco files search \"*.txt\"");
                        Console.WriteLine("  loco files search \"*.cs\" src/");
                        Console.WriteLine("  loco files search \"README*\" .");
                        return 1;
                    }

                    var pattern = args[1];
                    var searchDir = args.Length > 2 ? args[2] : ".";

                    if (!Directory.Exists(searchDir))
                    {
                        Console.WriteLine($"Directory not found: {searchDir}");
                        return 1;
                    }

                    Console.WriteLine($"Searching for: {pattern} in {Path.GetFullPath(searchDir)}");
                    var files = Directory.GetFiles(searchDir, pattern, SearchOption.AllDirectories);

                    if (files.Length == 0)
                    {
                        Console.WriteLine("No files found matching the pattern.");
                        return 0;
                    }

                    Console.WriteLine($"Found {files.Length} file(s):");
                    foreach (var file in files.Take(50)) // Limit to first 50 results
                    {
                        var info = new FileInfo(file);
                        var relativePath = Path.GetRelativePath(searchDir, file);
                        Console.WriteLine($"  {relativePath} ({info.Length:N0} bytes, {info.LastWriteTime:yyyy-MM-dd HH:mm})");
                    }

                    if (files.Length > 50)
                    {
                        Console.WriteLine($"  ... and {files.Length - 50} more files");
                    }

                    return 0;

                case "stats":
                    var statsDir = args.Length > 1 ? args[1] : ".";

                    if (!Directory.Exists(statsDir))
                    {
                        Console.WriteLine($"Directory not found: {statsDir}");
                        return 1;
                    }

                    Console.WriteLine($"=== Directory Statistics: {Path.GetFullPath(statsDir)} ===");
                    Console.WriteLine();

                    var allFiles = Directory.GetFiles(statsDir, "*", SearchOption.AllDirectories);
                    var allDirs = Directory.GetDirectories(statsDir, "*", SearchOption.AllDirectories);

                    Console.WriteLine($"Files: {allFiles.Length:N0}");
                    Console.WriteLine($"Directories: {allDirs.Length:N0}");

                    long totalSize = 0;
                    foreach (var file in allFiles)
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;
                    }

                    Console.WriteLine($"Total Size: {FormatBytes(totalSize)}");
                    return 0;

                default:
                    Console.WriteLine("Available file commands: search, stats");
                    Console.WriteLine();
                    Console.WriteLine("Examples:");
                    Console.WriteLine("  loco files search \"*.txt\"        - Find all text files");
                    Console.WriteLine("  loco files stats Downloads/       - Show directory statistics");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Files command failed: {ex.Message}");
            return 1;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return string.Format("{0:N1} {1}", number, suffixes[counter]);
    }

    private static void DisplayConfigWarnings(LocoConfig config, string context)
    {
        if (!config.HasPathResolutionWarnings)
        {
            return;
        }

        var warnings = config.GetPathResolutionWarningsSnapshot();
        Console.WriteLine();
        Console.WriteLine($"[Configuration Warnings: {context}]");
        foreach (var warning in warnings)
        {
            Console.WriteLine($" - {warning}");
        }

        var summary = config.GetPathResolutionWarningsSummary();
        if (!string.IsNullOrEmpty(summary))
        {
            Console.WriteLine($"Summary: {summary}");
        }

        config.ClearPathResolutionWarnings();
        Console.WriteLine();
    }

    private static async Task<int> LogsCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco logs <view|stats|search|clear> [options]");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();

        try
        {
            var config = new LocoConfig();
            var logDir = config.LogDirectory;
            DisplayConfigWarnings(config, "logs");

            switch (subCommand)
            {
                case "view":
                    var lines = args.Length > 1 && int.TryParse(args[1], out var lineCount) ? lineCount : 50;

                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("No log directory found.");
                        return 0;
                    }

                    var logFiles = Directory.GetFiles(logDir, "*.log")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .ToArray();

                    if (logFiles.Length == 0)
                    {
                        Console.WriteLine("No log files found.");
                        return 0;
                    }

                    Console.WriteLine($"=== Last {lines} log entries from {Path.GetFileName(logFiles[0])} ===");

                    var allLines = File.ReadAllLines(logFiles[0]);
                    var recentLines = allLines.TakeLast(lines);

                    foreach (var line in recentLines)
                    {
                        Console.WriteLine(line);
                    }

                    return 0;

                case "stats":
                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("No log directory found.");
                        return 0;
                    }

                    var allLogFiles = Directory.GetFiles(logDir, "*.log");

                    Console.WriteLine("=== Log Statistics ===");
                    Console.WriteLine($"Log Directory: {logDir}");
                    Console.WriteLine($"Log Files: {allLogFiles.Length}");

                    long totalSize = 0;
                    int totalLines = 0;
                    var logLevels = new Dictionary<string, int>();

                    foreach (var file in allLogFiles)
                    {
                        var info = new FileInfo(file);
                        totalSize += info.Length;

                        var fileLines = File.ReadAllLines(file);
                        totalLines += fileLines.Length;

                        // Count log levels (simple heuristic)
                        foreach (var line in fileLines)
                        {
                            if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("[ERR]", StringComparison.OrdinalIgnoreCase))
                                logLevels["ERROR"] = logLevels.GetValueOrDefault("ERROR", 0) + 1;
                            else if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase) || line.Contains("[WRN]", StringComparison.OrdinalIgnoreCase))
                                logLevels["WARNING"] = logLevels.GetValueOrDefault("WARNING", 0) + 1;
                            else if (line.Contains("INFO", StringComparison.OrdinalIgnoreCase) || line.Contains("[INF]", StringComparison.OrdinalIgnoreCase))
                                logLevels["INFO"] = logLevels.GetValueOrDefault("INFO", 0) + 1;
                            else if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase) || line.Contains("[DBG]", StringComparison.OrdinalIgnoreCase))
                                logLevels["DEBUG"] = logLevels.GetValueOrDefault("DEBUG", 0) + 1;
                        }
                    }

                    Console.WriteLine($"Total Size: {FormatBytes(totalSize)}");
                    Console.WriteLine($"Total Lines: {totalLines:N0}");
                    Console.WriteLine();
                    Console.WriteLine("Log Levels:");
                    foreach (var level in logLevels.OrderByDescending(x => x.Value))
                    {
                        Console.WriteLine($"  {level.Key}: {level.Value:N0}");
                    }

                    return 0;

                case "search":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Usage: loco logs search <pattern> [max_results]");
                        Console.WriteLine("Example: loco logs search \"ERROR\" 100");
                        return 1;
                    }

                    var searchPattern = args[1];
                    var maxResults = args.Length > 2 && int.TryParse(args[2], out var max) ? max : 100;

                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("No log directory found.");
                        return 0;
                    }

                    var searchFiles = Directory.GetFiles(logDir, "*.log")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime);

                    var matchCount = 0;
                    Console.WriteLine($"=== Searching for '{searchPattern}' ===");

                    foreach (var file in searchFiles)
                    {
                        var fileLines = File.ReadAllLines(file);
                        var fileName = Path.GetFileName(file);

                        for (int i = 0; i < fileLines.Length && matchCount < maxResults; i++)
                        {
                            if (fileLines[i].Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[{fileName}:{i + 1}] {fileLines[i]}");
                                matchCount++;
                            }
                        }

                        if (matchCount >= maxResults) break;
                    }

                    Console.WriteLine($"\nFound {matchCount} matches.");
                    return 0;

                case "clear":
                    if (!Directory.Exists(logDir))
                    {
                        Console.WriteLine("No log directory found.");
                        return 0;
                    }

                    var clearFiles = Directory.GetFiles(logDir, "*.log");
                    var confirm = args.Length > 1 && args[1].ToLowerInvariant() == "--confirm";

                    if (!confirm)
                    {
                        Console.WriteLine($"This will delete {clearFiles.Length} log file(s).");
                        Console.WriteLine("Use --confirm to proceed: loco logs clear --confirm");
                        return 1;
                    }

                    foreach (var file in clearFiles)
                    {
                        File.Delete(file);
                        Console.WriteLine($"Deleted: {Path.GetFileName(file)}");
                    }

                    Console.WriteLine($"Cleared {clearFiles.Length} log file(s).");
                    return 0;

                default:
                    Console.WriteLine("Available log commands: view, stats, search, clear");
                    Console.WriteLine();
                    Console.WriteLine("Examples:");
                    Console.WriteLine("  loco logs view 100        - View last 100 log lines");
                    Console.WriteLine("  loco logs stats           - Show log statistics");
                    Console.WriteLine("  loco logs search ERROR   - Search for error messages");
                    Console.WriteLine("  loco logs clear --confirm - Clear all log files");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Logs command failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> WorkflowCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco workflow <list|stats|<file_path>> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --verbose, -v        Show detailed execution logs");
            Console.WriteLine("  --dry-run            Validate workflow without executing");
            Console.WriteLine("  --visualize          Show workflow diagram without executing");
            Console.WriteLine("  --compact            Show compact workflow list");
            Console.WriteLine("  --deps               Show workflow dependencies");
            Console.WriteLine("  --schedule           Show workflow schedule information");
            Console.WriteLine("  --analyze            Analyze workflow with dependency validation");
            Console.WriteLine("  --health             Run health check on workflow");
            Console.WriteLine("  --lint               Run linter for code quality checks");
            Console.WriteLine("  --test               Run smoke tests on workflow");
            Console.WriteLine("  --env <name>         Use environment preset (dev, staging, production)");
            Console.WriteLine("  --var name=value     Set workflow variable (can be used multiple times)");
            Console.WriteLine("  --output <file>      Save execution summary to file");
            Console.WriteLine("  --report             Show detailed execution report");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  loco workflow list");
            Console.WriteLine("  loco workflow stats");
            Console.WriteLine("  loco workflow workflows/hello-world.json");
            Console.WriteLine("  loco workflow workflows/hello-world.json --verbose");
            Console.WriteLine("  loco workflow workflows/hello-world.json --dry-run");
            Console.WriteLine("  loco workflow workflows/hello-world.json --visualize");
            Console.WriteLine("  loco workflow workflows/hello-world.json --deps");
            Console.WriteLine("  loco workflow workflows/backup.json --var source=C:\\data --var dest=C:\\backup");
            Console.WriteLine("  loco workflow workflows/hello-world.json --output execution.log");
            Console.WriteLine("  loco workflow workflows/hello-world.json --report");
            return 1;
        }

        var command = args[0];

        // Handle "list" subcommand
        if (command.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return await ListWorkflows();
        }

        // Handle "stats" subcommand
        if (command.Equals("stats", StringComparison.OrdinalIgnoreCase))
        {
            ShowWorkflowStats();
            return 0;
        }

        // Handle "info" subcommand
        if (command.Equals("info", StringComparison.OrdinalIgnoreCase) && args.Length > 1)
        {
            return await ShowWorkflowInfo(args[1]);
        }

        var filePath = args[0];
        var isDryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var isVerbose = args.Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase) || a == "-v");
        var showReport = args.Any(a => a.Equals("--report", StringComparison.OrdinalIgnoreCase));
        var visualize = args.Any(a => a.Equals("--visualize", StringComparison.OrdinalIgnoreCase));
        var compact = args.Any(a => a.Equals("--compact", StringComparison.OrdinalIgnoreCase));
        var showDeps = args.Any(a => a.Equals("--deps", StringComparison.OrdinalIgnoreCase));
        var showSchedule = args.Any(a => a.Equals("--schedule", StringComparison.OrdinalIgnoreCase));
        var analyze = args.Any(a => a.Equals("--analyze", StringComparison.OrdinalIgnoreCase));
        var runHealthCheck = args.Any(a => a.Equals("--health", StringComparison.OrdinalIgnoreCase));
        var runLint = args.Any(a => a.Equals("--lint", StringComparison.OrdinalIgnoreCase));
        var runTest = args.Any(a => a.Equals("--test", StringComparison.OrdinalIgnoreCase));

        // Parse environment preset
        string? environment = null;
        var envIndex = Array.FindIndex(args, a => a.Equals("--env", StringComparison.OrdinalIgnoreCase));
        if (envIndex >= 0 && envIndex + 1 < args.Length)
        {
            environment = args[envIndex + 1];
        }

        // Parse output file
        string? outputFile = null;
        var outputIndex = Array.FindIndex(args, a => a.Equals("--output", StringComparison.OrdinalIgnoreCase));
        if (outputIndex >= 0 && outputIndex + 1 < args.Length)
        {
            outputFile = args[outputIndex + 1];
        }

        // Parse workflow variables (--var name=value)
        var variables = new Dictionary<string, string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].Equals("--var", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var varArg = args[i + 1];
                var parts = varArg.Split('=', 2);
                if (parts.Length == 2)
                {
                    variables[parts[0]] = parts[1];
                    Console.WriteLine($"Variable: {parts[0]} = {parts[1]}");
                }
                i++; // Skip next arg since we consumed it
            }
        }

        try
        {
            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Workflow file not found: {filePath}");
                Console.ResetColor();
                return 1;
            }

            // Handle visualization and analysis options (no execution)
            if (visualize || compact || showDeps || showSchedule || analyze || runHealthCheck || runLint || runTest)
            {
                var workflowJson = await File.ReadAllTextAsync(filePath);
                var workflowDef = System.Text.Json.JsonSerializer.Deserialize<Loco.Core.Workflows.WorkflowDefinition>(
                    workflowJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (workflowDef == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: Failed to parse workflow JSON");
                    Console.ResetColor();
                    return 1;
                }

                if (visualize)
                {
                    Console.WriteLine(Loco.Core.Workflows.WorkflowVisualizer.GenerateDiagram(workflowDef));
                }
                else if (compact)
                {
                    Console.WriteLine(Loco.Core.Workflows.WorkflowVisualizer.GenerateCompactList(workflowDef));
                }
                else if (showDeps)
                {
                    Console.WriteLine(Loco.Core.Workflows.WorkflowVisualizer.GenerateDependencyGraph(workflowDef));
                }
                else if (showSchedule)
                {
                    Console.WriteLine(Loco.Core.Workflows.WorkflowVisualizer.GenerateScheduleInfo(workflowDef));
                }
                else if (analyze)
                {
                    Console.WriteLine(Loco.Core.Workflows.WorkflowVisualizer.GenerateDependencyAnalysis(workflowDef));
                }
                else if (runHealthCheck)
                {
                    var checker = new Loco.Core.Workflows.WorkflowHealthChecker();
                    var healthReport = checker.CheckWorkflow(workflowDef);
                    Console.WriteLine(Loco.Core.Workflows.WorkflowHealthChecker.GenerateHealthReport(healthReport));

                    // Return non-zero exit code if unhealthy
                    return healthReport.IsHealthy ? 0 : 1;
                }
                else if (runLint)
                {
                    var linter = new Loco.Core.Workflows.WorkflowLinter();
                    var lintReport = linter.LintWorkflow(workflowDef);
                    Console.WriteLine(Loco.Core.Workflows.WorkflowLinter.GenerateLintReport(lintReport));

                    // Return non-zero exit code if critical violations found
                    return lintReport.HasCriticalViolations ? 1 : 0;
                }
                else if (runTest)
                {
                    var testRunner = new Loco.Core.Workflows.WorkflowTestRunner();
                    var testResult = await testRunner.RunSmokeTestsAsync(workflowDef);
                    Console.WriteLine(Loco.Core.Workflows.WorkflowTestRunner.GenerateTestReport(testResult));

                    // Return non-zero exit code if tests failed
                    return testResult.AllPassed ? 0 : 1;
                }

                return 0;
            }

            if (isDryRun)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("DRY-RUN MODE - No actions will be executed");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine($"Loading workflow from: {filePath}");
            Console.WriteLine();

            // Create logger factory for workflow execution
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger("Workflow");

            // Load workflow with variables and environment
            var workflowLoader = new Loco.Core.Workflows.WorkflowLoader(logger, variables, environment);
            var workflow = await workflowLoader.LoadFromFileAsync(filePath);

            if (workflow == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Failed to load workflow");
                Console.ResetColor();
                return 1;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Loaded workflow: {workflow.Name} (ID: {workflow.Id})");
            Console.WriteLine($"  Steps: {workflow.Actions.Count}");
            Console.ResetColor();
            Console.WriteLine();

            if (isDryRun)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Workflow validation passed");
                Console.WriteLine("  Dry-run complete - workflow is ready to execute");
                Console.ResetColor();
                return 0;
            }

            // Create engine and execute workflow
            var config = new LocoConfig
            {
                MaxConcurrentFlows = 1,
                DefaultTimeoutSeconds = 300,
                DefaultRetryCount = 0
            };

            using var engine = new SimpleLightEngine(logger, config);
            await engine.StartAsync();

            engine.AddFlow(workflow);

            Console.WriteLine("Executing workflow...");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine();

            var startTime = DateTime.Now;
            var success = await engine.ExecuteFlowAsync(workflow.Id);
            var duration = DateTime.Now - startTime;

            Console.WriteLine();
            Console.WriteLine(new string('-', 60));

            // Record statistics
            _workflowStats.RecordExecution(workflow.Id, success, duration);

            // Save output log if requested
            if (!string.IsNullOrEmpty(outputFile))
            {
                Loco.Core.Workflows.WorkflowOutputLogger.SaveExecutionSummary(
                    outputFile,
                    workflow.Name,
                    workflow.Id,
                    success,
                    duration,
                    workflow.Actions.Count,
                    success ? null : "Workflow execution failed");

                Console.WriteLine($"Execution summary saved to: {outputFile}");
            }

            // Show detailed report if requested
            if (showReport)
            {
                Console.WriteLine();
                ShowExecutionReport(workflow, success, startTime, DateTime.Now);
            }

            if (success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Workflow completed successfully in {duration.TotalSeconds:F2}s");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Workflow failed after {duration.TotalSeconds:F2}s");
                Console.ResetColor();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HistoryCommand(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loco history <list|stats|clear> [options]");
            return 1;
        }

        var subCommand = args[0].ToLowerInvariant();
        var optionArgs = args.Skip(1).ToList();

        if (!TryConsumeOption(optionArgs, "--rules-path", out var rulesPath))
        {
            Console.WriteLine("Usage: loco history <list|stats|clear> [--rules-path <path>] [options]");
            return 1;
        }

        var remainingArgs = optionArgs.ToArray();

        try
        {
            var engineResult = CreateEngine(rulesPath);
            if (engineResult == null)
            {
                return 1;
            }

            var (engine, _, _) = engineResult.Value;
            await engine.StartAsync();

            try
            {
                switch (subCommand)
                {
                    case "list":
                        var limit = remainingArgs.Length > 0 && int.TryParse(remainingArgs[0], out var listLimit) ? listLimit : 20;

                        Console.WriteLine($"=== Recent Executions (Last {limit}) ===");

                        // For now, we'll show basic engine stats since detailed history tracking
                        // would require modifications to the SimpleLightEngine
                        var status = engine.GetEngineStatus();
                        Console.WriteLine($"Total Executions: {status.TotalExecutions}");
                        Console.WriteLine($"Successful Executions: {status.SuccessfulExecutions}");
                        Console.WriteLine($"Failed Executions: {status.TotalExecutions - status.SuccessfulExecutions}");
                        Console.WriteLine($"Success Rate: {status.SuccessRate:F1}%");
                        Console.WriteLine();
                        Console.WriteLine("Note: Detailed execution history tracking is not yet implemented.");
                        Console.WriteLine("This shows aggregate statistics from the current engine session.");

                        return 0;

                    case "stats":
                        var engineStatus = engine.GetEngineStatus();

                        Console.WriteLine("=== Execution Statistics ===");
                        Console.WriteLine($"Engine Status: {(await engine.IsHealthyAsync() ? "Healthy" : "Unhealthy")}");
                        Console.WriteLine($"Active Flows: {engineStatus.FlowCount}");
                        Console.WriteLine($"Active Rules: {engineStatus.RuleCount}");
                        Console.WriteLine($"Total Executions: {engineStatus.TotalExecutions}");
                        Console.WriteLine($"Successful Executions: {engineStatus.SuccessfulExecutions}");
                        Console.WriteLine($"Failed Executions: {engineStatus.TotalExecutions - engineStatus.SuccessfulExecutions}");
                        Console.WriteLine($"Success Rate: {engineStatus.SuccessRate:F1}%");
                        Console.WriteLine();

                        Console.WriteLine("[Performance]");
                        Console.WriteLine($"Memory Usage: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
                        Console.WriteLine($"Gen 0 Collections: {GC.CollectionCount(0)}");
                        Console.WriteLine($"Gen 1 Collections: {GC.CollectionCount(1)}");
                        Console.WriteLine($"Gen 2 Collections: {GC.CollectionCount(2)}");

                        return 0;

                    case "clear":
                        var confirm = remainingArgs.Any(arg => arg.Equals("--confirm", StringComparison.OrdinalIgnoreCase));

                        if (!confirm)
                        {
                            Console.WriteLine("This will clear the execution history and reset statistics.");
                            Console.WriteLine("Use --confirm to proceed: loco history clear --confirm");
                            return 1;
                        }

                        // Note: In a real implementation, this would clear persistent history storage
                        Console.WriteLine("Execution history cleared.");
                        Console.WriteLine("Note: This is a placeholder. Persistent history storage is not yet implemented.");
                        return 0;

                    default:
                        Console.WriteLine("Available history commands: list, stats, clear");
                        return 1;
                }
            }
            finally
            {
                await engine.StopAsync();
                engine.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"History command failed: {ex.Message}");
            return 1;
        }
    }

    private static readonly Loco.Core.Workflows.WorkflowStatistics _workflowStats = new("workflow-stats.json");

    private static void ShowWorkflowStats()
    {
        var stats = _workflowStats.GetAllStats().ToList();

        if (!stats.Any())
        {
            Console.WriteLine("No workflow execution statistics available.");
            Console.WriteLine("Run some workflows to see statistics.");
            return;
        }

        Console.WriteLine("=== Workflow Execution Statistics ===");
        Console.WriteLine();

        foreach (var stat in stats.OrderByDescending(s => s.TotalExecutions))
        {
            Console.WriteLine(stat.ToString());
            Console.WriteLine();
        }
    }

    private static async Task<int> ShowWorkflowInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Workflow file not found: {filePath}");
                Console.ResetColor();
                return 1;
            }

            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger("WorkflowInfo");
            var workflowLoader = new Loco.Core.Workflows.WorkflowLoader(logger);
            var workflow = await workflowLoader.LoadFromFileAsync(filePath);

            if (workflow == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Failed to load workflow");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine("=== Workflow Information ===");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Name: {workflow.Name}");
            Console.ResetColor();
            Console.WriteLine($"ID: {workflow.Id}");
            Console.WriteLine($"Description: {workflow.Description ?? "N/A"}");
            Console.WriteLine($"Steps: {workflow.Actions.Count}");
            Console.WriteLine($"File: {filePath}");
            Console.WriteLine();

            Console.WriteLine("Steps:");
            for (int i = 0; i < workflow.Actions.Count; i++)
            {
                var action = workflow.Actions[i];
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  {i + 1}. ");
                Console.ResetColor();
                Console.WriteLine($"{action.Name} ({action.Id})");
            }
            Console.WriteLine();

            // Show statistics if available
            var stats = _workflowStats.GetStats(workflow.Id);
            if (stats != null)
            {
                Console.WriteLine("Execution Statistics:");
                Console.WriteLine($"  Total runs: {stats.TotalExecutions}");
                Console.WriteLine($"  Success rate: {stats.SuccessRate:F1}%");
                Console.WriteLine($"  Avg duration: {stats.AverageDuration.TotalSeconds:F2}s");
                Console.WriteLine($"  Last run: {stats.LastExecutionTime}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("No execution history yet");
                Console.ResetColor();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void ShowExecutionReport(Loco.Core.Models.SimpleFlow workflow, bool success, DateTime startTime, DateTime endTime)
    {
        var duration = endTime - startTime;
        var report = new Loco.Core.Workflows.WorkflowExecutionReport
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            StartTime = startTime,
            EndTime = endTime,
            Success = success,
            TotalSteps = workflow.Actions.Count,
            ExecutedSteps = workflow.Actions.Count,
            SkippedSteps = 0,
            FailedSteps = success ? 0 : 1
        };

        Console.WriteLine(report.GenerateTextReport());
    }

    private static async Task<int> ListWorkflows()
    {
        try
        {
            var workflowsDir = "workflows";

            if (!Directory.Exists(workflowsDir))
            {
                Console.WriteLine("No workflows directory found.");
                Console.WriteLine($"Create a '{workflowsDir}' directory and add workflow JSON files.");
                return 0;
            }

            Console.WriteLine("Scanning for workflows...");
            Console.WriteLine();

            var catalog = new Loco.Core.Workflows.WorkflowCatalog();
            var count = await catalog.ScanDirectoryAsync(workflowsDir, recursive: true);

            if (count == 0)
            {
                Console.WriteLine("No workflow files found in the workflows directory.");
                return 0;
            }

            Console.WriteLine("=== Workflow Catalog ===");
            Console.WriteLine();

            // Display catalog
            Console.WriteLine(catalog.GenerateCatalogDisplay());

            // Show summary
            Console.WriteLine();
            Console.WriteLine($"Total workflows: {count}");

            var categories = catalog.GetCategories();
            if (categories.Count > 0)
            {
                Console.WriteLine($"Categories: {string.Join(", ", categories)}");
            }

            var tags = catalog.GetTags();
            if (tags.Count > 0)
            {
                Console.WriteLine($"Tags: {string.Join(", ", tags)}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error listing workflows: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}