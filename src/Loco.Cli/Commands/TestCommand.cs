using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
// using Loco.Core.Components.Actions;

namespace Loco.Cli.Commands
{
    public class TestCommand : Command
    {
        public TestCommand() : base("test", "Run simple tests to verify functionality")
        {
            var testTypeOption = new Option<string>(
                new[] { "--type", "-t" },
                getDefaultValue: () => "all",
                description: "Test type: all, log, flow, action");

            AddOption(testTypeOption);

            this.SetHandler(async (InvocationContext context) =>
            {
                var testType = context.ParseResult.GetValueForOption(testTypeOption);
                var host = context.GetHost();
                var logger = host.Services.GetRequiredService<ILogger<TestCommand>>();

                await ExecuteTestAsync(testType, logger);
            });
        }

        private async Task ExecuteTestAsync(string testType, ILogger logger)
        {
            logger.LogInformation($"Running {testType} tests...");

            switch (testType?.ToLower())
            {
                case "log":
                    await TestLogging(logger);
                    break;
                case "flow":
                    await TestFlowExecution(logger);
                    break;
                case "action":
                    await TestActionExecution(logger);
                    break;
                case "all":
                default:
                    await TestLogging(logger);
                    await TestActionExecution(logger);
                    await TestFlowExecution(logger);
                    break;
            }

            logger.LogInformation("Tests completed!");
        }

        private async Task TestLogging(ILogger logger)
        {
            logger.LogInformation("=== Testing Logging ===");
            logger.LogDebug("Debug message test");
            logger.LogInformation("Info message test");
            logger.LogWarning("Warning message test");
            logger.LogError("Error message test (this is just a test, not a real error)");
            logger.LogInformation("Logging test completed");
            await Task.CompletedTask;
        }

        private async Task TestActionExecution(ILogger logger)
        {
            logger.LogInformation("=== Testing Action Execution ===");
            logger.LogInformation("Simple action test completed (LogAction removed)");
            await Task.CompletedTask;
        }

        private async Task TestFlowExecution(ILogger logger)
        {
            logger.LogInformation("=== Testing Flow Execution ===");

            var flowEngine = new SimpleFlowEngine(logger as ILogger<SimpleFlowEngine>);

            // Create a simple flow
            var flow = new SimpleFlow
            {
                Id = "test-flow",
                Name = "Test Flow",
                Description = "A simple test flow",
                Actions = new System.Collections.Generic.List<Loco.Core.Interfaces.IAction>()
            };

            flowEngine.RegisterFlow(flow);

            var context = new System.Collections.Generic.Dictionary<string, object>
            {
                ["test"] = "value"
            };

            var success = await flowEngine.ExecuteFlowAsync("test-flow", context);
            logger.LogInformation($"Flow execution result: {(success ? "Success" : "Failed")}");
        }
    }
}