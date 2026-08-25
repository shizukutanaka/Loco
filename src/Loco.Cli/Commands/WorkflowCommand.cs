using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Integrations.Core;
using Loco.Core.Storage;
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

            // `loco workflow run-visual <file>`: executes the Visual Editor's own
            // workflow JSON shape (StoredWorkflow, saved by "Export" in the editor or
            // fetched via GET /api/v1/workflows/{id}) on VisualWorkflowEngine with all
            // 28 connectors registered - previously the CLI's only execution path
            // (above, SimpleLightEngine) supported exactly one action type ("log"), so
            // the connector catalog was unreachable from any running binary.
            var runVisualCommand = new Command(
                "run-visual",
                "Execute a Visual Editor workflow JSON file with connectors enabled");
            var runVisualFileArgument = new Argument<string>(
                name: "file",
                description: "Path to a Visual Editor workflow JSON file (StoredWorkflow shape: nodes/edges/metadata)");
            // Where the stored connections live. The API defaults this to
            // AppContext.BaseDirectory/data/workflows, which is relative to the
            // API binary and so cannot be guessed from here - point the CLI at
            // the same directory to reuse connections created in the editor.
            var runVisualDataDirOption = new Option<string?>(
                name: "--data-dir",
                description: "Directory holding connections.json and secrets/ (default: $LOCO_DATA_DIR)");
            runVisualCommand.AddArgument(runVisualFileArgument);
            runVisualCommand.AddOption(runVisualDataDirOption);
            runVisualCommand.SetHandler(
                ExecuteVisualWorkflowAsync, runVisualFileArgument, runVisualDataDirOption);
            AddCommand(runVisualCommand);
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

        private async Task<int> ExecuteVisualWorkflowAsync(string filePath, string? dataDirectory)
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

                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
                var logger = loggerFactory.CreateLogger<WorkflowCommand>();

                Console.WriteLine($"Loading Visual Editor workflow from: {filePath}");
                var json = await File.ReadAllTextAsync(filePath);
                var stored = System.Text.Json.JsonSerializer.Deserialize<StoredWorkflow>(
                    json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (stored is null || string.IsNullOrWhiteSpace(stored.Name))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: Failed to parse workflow JSON (expected the Visual Editor's Workflow shape: id/name/nodes/edges/metadata)");
                    Console.ResetColor();
                    return 1;
                }

                var visual = WorkflowMapper.ToVisualWorkflow(stored);

                // VisualWorkflowValidator, not WorkflowValidator: the class was
                // renamed to stop it colliding with the identically-named one in
                // Loco.Core.Workflow, and this call site was missed. Nothing
                // caught it because the offline type-check could not reach method
                // bodies while any declaration error existed.
                var validation = new VisualWorkflowValidator().Validate(visual);
                if (!validation.IsValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: Workflow failed validation:");
                    foreach (var error in validation.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    Console.ResetColor();
                    return 1;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Loaded workflow: {visual.Name} ({visual.Nodes.Count} nodes, {visual.Connections.Count} connections)");
                Console.ResetColor();

                // Discover and register all connectors so the workflow's node handlers
                // (integration:action) resolve, exactly as ConnectorStartupService does
                // for the API - this is the first CLI code path that can reach them.
                using var registry = new ConnectorRegistry();
                var discovered = registry.AutoDiscover(typeof(ConnectorRegistry).Assembly);
                Console.WriteLine($"Registered {discovered} connectors");

                var engine = new VisualWorkflowEngine(message => logger.LogDebug("{EngineMessage}", message));
                using var bridge = new WorkflowConnectorBridge(registry, engine);
                await bridge.RegisterAllConnectorsAsync();

                // Resolve stored credentials, exactly as the API does before it
                // starts a run. Without this every connector executed
                // uninitialized and failed on a null HttpClient - the CLI kept
                // the bug the API had already fixed, because it never looked at
                // the connection store at all.
                var required = WorkflowCredentialResolver.PlanConnections(visual);
                if (required.Count > 0)
                {
                    var resolvedDataDirectory = dataDirectory
                        ?? Environment.GetEnvironmentVariable("LOCO_DATA_DIR");

                    if (string.IsNullOrWhiteSpace(resolvedDataDirectory))
                    {
                        // Running these uninitialized is guaranteed to fail, so
                        // say what is missing instead of failing per node.
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(
                            $"Error: this workflow uses {required.Count} stored connection(s), " +
                            "but no data directory was given.");
                        Console.WriteLine(
                            "  Pass --data-dir <path> or set LOCO_DATA_DIR to the directory the " +
                            "API uses (it holds connections.json and secrets/).");
                        Console.ResetColor();
                        return 1;
                    }

                    var connections = new JsonFileConnectionStore(resolvedDataDirectory);
                    var problems = await WorkflowCredentialResolver.ConfigureAsync(
                        visual, connections, bridge);

                    if (problems.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: unresolved connections:");
                        foreach (var problem in problems)
                        {
                            Console.WriteLine($"  - {problem}");
                        }
                        Console.ResetColor();
                        return 1;
                    }

                    Console.WriteLine($"Resolved {required.Count} connection(s)");
                }

                Console.WriteLine("Executing workflow...");
                Console.WriteLine(new string('-', 60));

                var startTime = DateTime.Now;
                var context = await engine.ExecuteAsync(visual);
                var duration = DateTime.Now - startTime;

                Console.WriteLine(new string('-', 60));
                foreach (var line in context.ExecutionLog)
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine();

                if (context.Status == WorkflowExecutionStatus.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Workflow completed successfully in {duration.TotalSeconds:F2}s");
                    Console.ResetColor();
                    return 0;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Workflow {context.Status.ToString().ToLowerInvariant()} after {duration.TotalSeconds:F2}s: {context.Error}");
                Console.ResetColor();
                return 1;
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
