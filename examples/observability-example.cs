using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Telemetry;
using Loco.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Loco.Examples
{
    /// <summary>
    /// Example demonstrating OpenTelemetry integration and performance profiling.
    /// Shows how to add observability to your automation workflows.
    /// </summary>
    public class ObservabilityExample
    {
        public static async Task Main()
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<ObservabilityExample>();

            Console.WriteLine("=== Loco Observability Example ===\n");

            // Example 1: OpenTelemetry Distributed Tracing
            await DemonstrateTelemetry(logger);

            Console.WriteLine();

            // Example 2: Performance Profiling
            await DemonstratePerformanceProfiling(logger);

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Demonstrates OpenTelemetry integration for distributed tracing.
        /// </summary>
        private static async Task DemonstrateTelemetry(ILogger logger)
        {
            Console.WriteLine("1. OpenTelemetry Distributed Tracing");
            Console.WriteLine("=====================================\n");

            using var telemetry = new OpenTelemetryProvider(logger);

            // Track a complex workflow with nested operations
            using (var workflow = telemetry.CreateOperationScope("workflow.execution"))
            {
                Console.WriteLine("Executing workflow with distributed tracing...");

                // Operation 1: File Processing
                using (var fileOp = telemetry.CreateOperationScope("file.process", new Dictionary<string, object?>
                {
                    { "file.name", "data.json" },
                    { "file.size", 1024 }
                }))
                {
                    await Task.Delay(100); // Simulate work
                    Console.WriteLine("  ✓ File processed");
                }

                // Operation 2: API Call
                using (var apiOp = telemetry.CreateOperationScope("api.call", new Dictionary<string, object?>
                {
                    { "api.endpoint", "/workflows" },
                    { "api.method", "POST" }
                }))
                {
                    await Task.Delay(200); // Simulate work
                    Console.WriteLine("  ✓ API call completed");
                }

                // Operation 3: Database Update
                using (var dbOp = telemetry.CreateOperationScope("database.update", new Dictionary<string, object?>
                {
                    { "db.table", "workflows" },
                    { "db.operation", "insert" }
                }))
                {
                    await Task.Delay(50); // Simulate work
                    Console.WriteLine("  ✓ Database updated");
                }
            }

            Console.WriteLine($"\nActive operations: {telemetry.ActiveOperations}");
            Console.WriteLine("All traces have been recorded and can be exported to your observability backend.");
        }

        /// <summary>
        /// Demonstrates performance profiling capabilities.
        /// </summary>
        private static async Task DemonstratePerformanceProfiling(ILogger logger)
        {
            Console.WriteLine("2. Performance Profiling");
            Console.WriteLine("=========================\n");

            using var profiler = new PerformanceProfiler(logger, TimeSpan.Zero); // Disable auto-reporting for demo

            // Profile multiple operations
            for (int i = 0; i < 10; i++)
            {
                using (profiler.Profile("data.processing"))
                {
                    await Task.Delay(Random.Shared.Next(10, 50));
                    // Simulate data processing with varying memory allocation
                    var data = new byte[Random.Shared.Next(1000, 5000)];
                }

                using (profiler.Profile("calculation.heavy"))
                {
                    await Task.Delay(Random.Shared.Next(50, 150));
                    // Simulate heavy calculation
                    var result = 0.0;
                    for (int j = 0; j < 10000; j++)
                    {
                        result += Math.Sqrt(j);
                    }
                }

                using (profiler.Profile("io.operation"))
                {
                    await Task.Delay(Random.Shared.Next(5, 20));
                }
            }

            // Generate performance report
            var report = profiler.GenerateReport();
            Console.WriteLine(report.FormatReport());

            // Show top operations
            Console.WriteLine("\nTop Operations by Total Time:");
            Console.WriteLine("-------------------------------");
            foreach (var metrics in profiler.GetTopOperations(3))
            {
                Console.WriteLine($"  {metrics.OperationName}:");
                Console.WriteLine($"    Total Time: {metrics.TotalTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"    Avg Time: {metrics.AverageTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"    Calls: {metrics.CallCount}");
                Console.WriteLine($"    Memory: {metrics.AverageMemoryBytes:N0} bytes avg\n");
            }
        }
    }

    /// <summary>
    /// Example: Integrating observability into automation workflows.
    /// </summary>
    public class WorkflowWithObservability
    {
        private readonly OpenTelemetryProvider _telemetry;
        private readonly PerformanceProfiler _profiler;
        private readonly ILogger _logger;

        public WorkflowWithObservability(ILogger logger)
        {
            _logger = logger;
            _telemetry = new OpenTelemetryProvider(logger);
            _profiler = new PerformanceProfiler(logger, TimeSpan.FromMinutes(5));
        }

        public async Task<bool> ExecuteWorkflowAsync(string workflowName)
        {
            using var activity = _telemetry.CreateOperationScope($"workflow.{workflowName}");

            try
            {
                using (_profiler.Profile($"workflow.{workflowName}"))
                {
                    _logger.LogInformation("Starting workflow {WorkflowName}", workflowName);

                    // Step 1: Validate inputs
                    using (_profiler.Profile("workflow.validate"))
                    {
                        await ValidateInputsAsync();
                    }

                    // Step 2: Execute main logic
                    using (_profiler.Profile("workflow.execute"))
                    {
                        await ExecuteMainLogicAsync();
                    }

                    // Step 3: Save results
                    using (_profiler.Profile("workflow.save"))
                    {
                        await SaveResultsAsync();
                    }

                    _logger.LogInformation("Workflow {WorkflowName} completed successfully", workflowName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _telemetry.RecordError($"workflow.{workflowName}", ex);
                _logger.LogError(ex, "Workflow {WorkflowName} failed", workflowName);
                return false;
            }
        }

        private async Task ValidateInputsAsync()
        {
            await Task.Delay(50); // Simulate validation
        }

        private async Task ExecuteMainLogicAsync()
        {
            await Task.Delay(200); // Simulate main logic
        }

        private async Task SaveResultsAsync()
        {
            await Task.Delay(100); // Simulate saving
        }

        public void Dispose()
        {
            _telemetry.Dispose();
            _profiler.Dispose();
        }
    }
}
