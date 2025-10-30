using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Extensibility;
using Loco.Core.Extensibility.Hooks;

namespace Loco.Examples.Extensions
{
    /// <summary>
    /// A simple "Hello World" extension demonstrating basic Loco extensibility
    /// </summary>
    [Extension("hello-world", "Hello World Extension")]
    public class HelloWorldExtension : ExtensionBase
    {
        public override string Id => "hello-world";
        public override string Name => "Hello World Extension";
        public override string Version => "1.0.0";
        public override string Description => "A simple extension that greets users and demonstrates event handling";
        public override string Author => "Loco Team";
        public override string License => "MIT";
        public override IEnumerable<string> Tags => new[] { "example", "tutorial", "beginner" };

        private IDisposable? _workflowSubscription;
        private IDisposable? _commandSubscription;

        public override async Task InitializeAsync(IExtensionContext context, CancellationToken cancellationToken = default)
        {
            await base.InitializeAsync(context, cancellationToken);

            Console.WriteLine($"🎉 {Name} v{Version} initialized!");
            Console.WriteLine($"📁 Data directory: {context.DataDirectory}");
            Console.WriteLine($"📝 Log directory: {context.LogDirectory}");

            // Subscribe to workflow events
            _workflowSubscription = context.SubscribeToEvent("workflow.started", async (data) =>
            {
                Console.WriteLine("👋 Hello! A workflow just started!");
            });

            // Subscribe to command events
            _commandSubscription = context.SubscribeToEvent("command.executed", async (data) =>
            {
                Console.WriteLine("✨ A command was executed!");
            });

            // Register a simple workflow hook
            context.RegisterHook(new HelloWorldWorkflowHook());

            Console.WriteLine("✅ Hello World Extension is ready!");
        }

        public override async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"👋 {Name} shutting down...");

            // Cleanup subscriptions
            _workflowSubscription?.Dispose();
            _commandSubscription?.Dispose();

            await base.ShutdownAsync(cancellationToken);

            Console.WriteLine("✅ Goodbye from Hello World Extension!");
        }

        public override async Task<ExtensionHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            // Perform health checks
            var isHealthy = _workflowSubscription != null && _commandSubscription != null;

            return new ExtensionHealth
            {
                Status = isHealthy ? HealthStatus.Healthy : HealthStatus.Degraded,
                Message = isHealthy
                    ? "Extension is running normally"
                    : "Some subscriptions are not active",
                Data = new Dictionary<string, object>
                {
                    ["subscriptionsActive"] = isHealthy,
                    ["uptime"] = DateTime.UtcNow
                }
            };
        }

        /// <summary>
        /// Simple workflow hook that adds a greeting to workflow context
        /// </summary>
        private class HelloWorldWorkflowHook : IWorkflowHook
        {
            public async Task<bool> OnBeforeExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
            {
                // Add a greeting variable to the workflow
                context.Variables["greeting"] = "Hello from Hello World Extension!";
                context.Variables["timestamp"] = DateTime.UtcNow.ToString("o");

                Console.WriteLine($"🔧 Enhanced workflow '{context.WorkflowName}' with greeting");

                // Continue workflow execution
                return true;
            }

            public async Task OnAfterExecuteAsync(WorkflowContext context, WorkflowResult result, CancellationToken cancellationToken = default)
            {
                var emoji = result.Success ? "✅" : "❌";
                Console.WriteLine($"{emoji} Workflow '{context.WorkflowName}' completed in {result.Duration.TotalSeconds:F2}s");
            }

            public async Task<bool> OnErrorAsync(WorkflowContext context, Exception exception, CancellationToken cancellationToken = default)
            {
                Console.WriteLine($"💥 Error in workflow '{context.WorkflowName}': {exception.Message}");

                // Don't handle the error, let it propagate
                return false;
            }
        }
    }
}
