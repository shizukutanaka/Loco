using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace Loco.Cli.Commands
{
    /// <summary>
    /// Command for executing workflows from JSON files.
    /// </summary>
    public class WorkflowCommand : Command
    {
        public WorkflowCommand() : base("workflow", "Execute automation workflows from JSON files")
        {
            var fileArgument = new Argument<string>(
                name: "file",
                description: "Path to the workflow JSON file");

            var visualizeOption = new Option<string?>(
                aliases: new[] { "--visualize", "-v" },
                description: "Show workflow diagram (modes: full, compact, deps)");

            var dryRunOption = new Option<bool>(
                aliases: new[] { "--dry-run", "-n" },
                description: "Validate without executing");

            var healthOption = new Option<bool>(
                aliases: new[] { "--health" },
                description: "Run health check on the workflow");

            var lintOption = new Option<bool>(
                aliases: new[] { "--lint", "-l" },
                description: "Run linter on the workflow");

            var testOption = new Option<bool>(
                aliases: new[] { "--test", "-t" },
                description: "Run tests on the workflow");

            var parallelOption = new Option<int>(
                aliases: new[] { "--parallel", "-p" },
                getDefaultValue: () => 0,
                description: "Execute steps in parallel (specify max parallelism, 0=sequential, default when used=4)");

            AddArgument(fileArgument);
            AddOption(visualizeOption);
            AddOption(dryRunOption);
            AddOption(healthOption);
            AddOption(lintOption);
            AddOption(testOption);
            AddOption(parallelOption);

            this.SetHandler(ExecuteWorkflowAsync, fileArgument, visualizeOption, dryRunOption, healthOption, lintOption, testOption, parallelOption);
        }

        private async Task<int> ExecuteWorkflowAsync(string filePath, string? visualize, bool dryRun, bool health, bool lint, bool test, int maxParallelism)
        {
            try
            {
                Console.WriteLine($"Loading workflow from: {filePath}");
                Console.WriteLine();

                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Workflow file not found: {filePath}");
                    Console.ResetColor();
                    return 1;
                }

                // Create logger
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
                var logger = loggerFactory.CreateLogger<WorkflowCommand>();

                // Load workflow definition (JSON only, no execution)
                var workflowJson = await File.ReadAllTextAsync(filePath);
                var workflowDef = System.Text.Json.JsonSerializer.Deserialize<WorkflowDefinition>(
                    workflowJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (workflowDef == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: Failed to parse workflow JSON");
                    Console.ResetColor();
                    return 1;
                }

                // Handle visualization options
                if (!string.IsNullOrEmpty(visualize))
                {
                    switch (visualize.ToLowerInvariant())
                    {
                        case "full":
                        case "diagram":
                            Console.WriteLine(WorkflowVisualizer.GenerateDiagram(workflowDef));
                            break;
                        case "compact":
                        case "list":
                            Console.WriteLine(WorkflowVisualizer.GenerateCompactList(workflowDef));
                            break;
                        case "deps":
                        case "dependencies":
                            Console.WriteLine(WorkflowVisualizer.GenerateDependencyGraph(workflowDef));
                            break;
                        default:
                            // Default to full diagram
                            Console.WriteLine(WorkflowVisualizer.GenerateDiagram(workflowDef));
                            break;
                    }
                    return 0;
                }

                // Handle health check
                if (health)
                {
                    var healthChecker = new WorkflowHealthChecker();
                    var healthReport = healthChecker.CheckWorkflow(workflowDef);
                    var reportText = WorkflowHealthChecker.GenerateHealthReport(healthReport);
                    Console.WriteLine(reportText);
                    return healthReport.IsHealthy ? 0 : 1;
                }

                // Handle linting
                if (lint)
                {
                    var linter = new WorkflowLinter();
                    var lintReport = linter.LintWorkflow(workflowDef);
                    var reportText = WorkflowLinter.GenerateLintReport(lintReport);
                    Console.WriteLine(reportText);
                    return lintReport.HasCriticalViolations ? 1 : 0;
                }

                // Handle testing (--test flag currently disabled, use --lint or --health)
                if (test)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Testing feature is currently under maintenance. Use --lint or --health instead.");
                    Console.ResetColor();
                    return 1;
                }

                if (dryRun)
                {
                    Console.WriteLine("Validating workflow...");
                    Console.WriteLine();

                    var validator = new MainWorkflowValidator();
                    var validationResult = validator.Validate(workflowDef);

                    if (validationResult.Errors.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ Validation failed with {validationResult.Errors.Count} error(s):");
                        Console.ResetColor();

                        foreach (var error in validationResult.Errors)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"  ✗ {error}");
                            Console.ResetColor();
                        }

                        return 1;
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Workflow validation passed");
                    Console.ResetColor();

                    if (validationResult.Warnings.Count > 0)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Warnings ({validationResult.Warnings.Count}):");
                        Console.ResetColor();

                        foreach (var warning in validationResult.Warnings)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  ⚠ {warning}");
                            Console.ResetColor();
                        }
                    }

                    return 0;
                }

                // Execute with parallel engine if requested
                if (maxParallelism > 0)
                {
                    var parallelism = maxParallelism > 0 ? maxParallelism : 4; // Default to 4 if not specified
                    Console.WriteLine($"Executing workflow in PARALLEL mode (max parallelism: {parallelism})...");
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine();

                    var parallelEngine = new ParallelExecutionEngine(logger, parallelism);
                    var result = await parallelEngine.ExecuteAsync(workflowDef);

                    Console.WriteLine();
                    Console.WriteLine(ParallelExecutionEngine.GenerateExecutionReport(result));

                    return result.Success ? 0 : 1;
                }

                // Load workflow for sequential execution
                var workflowLoader = new WorkflowLoader(logger);
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
                Console.ResetColor();
                Console.WriteLine();

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

        public async Task<int> InvokeAsync(string[] args)
        {
            return await ((Command)this).InvokeAsync(args);
        }
    }
}
