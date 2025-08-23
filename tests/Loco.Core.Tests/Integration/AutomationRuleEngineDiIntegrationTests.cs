using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Interfaces;
using Loco.Core.Services;

namespace Loco.Core.Tests.Integration
{
    public class AutomationRuleEngineDiIntegrationTests : IDisposable
    {
        private readonly string _modelsPath;

        public AutomationRuleEngineDiIntegrationTests()
        {
            _modelsPath = Path.Combine(Path.GetTempPath(), $"loco_models_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_modelsPath);
        }

        [Fact]
        public async Task AutomationRuleEngine_DI_Instantiation_And_Action_Execution_Works()
        {
            // Arrange: minimal DI container with required dependencies
            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            services.AddSingleton<HttpClient>();
            services.AddSingleton<SandboxExecutor>();
            services.AddSingleton<NaturalLanguageToDslConverter>();
            services.AddSingleton(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<LlmModelManager>>();
                var http = provider.GetRequiredService<HttpClient>();
                return new LlmModelManager(logger, http, _modelsPath);
            });
            services.AddSingleton<IAutomationRuleEngine, AutomationRuleEngine>();

            using var provider = services.BuildServiceProvider();
            var engine = provider.GetRequiredService<IAutomationRuleEngine>();

            var rule = new AutomationDsl.Rule
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "DI Log Action Test",
                Description = "Verifies DI-based action instantiation",
                Enabled = true,
                Variables = new Dictionary<string, object>(),
                Permissions = new AutomationDsl.PermissionSet(),
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    // Use a valid trigger type so validation passes; large interval to avoid background fires
                    Type = "time",
                    Parameters = new Dictionary<string, object>
                    {
                        ["intervalMs"] = 3600000
                    }
                },
                Actions = new List<AutomationDsl.ActionDefinition>
                {
                    new AutomationDsl.ActionDefinition
                    {
                        Type = "log",
                        TimeoutMs = 2000,
                        Parameters = new Dictionary<string, object>
                        {
                            ["message"] = "Hello from DI integration test"
                        }
                    }
                }
            };

            // Use independent cancellation tokens for each stage to avoid reuse/expiration interference
            using var addCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var triggerCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act: load rule
            var added = await engine.AddRuleAsync(rule, addCts.Token);
            added.Should().BeTrue("rule must be valid and loadable via DI");

            // Manually trigger and ensure it completes promptly (no hang)
            var triggerTask = engine.TriggerRuleAsync(rule.Id, new Dictionary<string, object>
            {
                ["invokedBy"] = "test"
            }, triggerCts.Token);

            var completed = await Task.WhenAny(triggerTask, Task.Delay(TimeSpan.FromSeconds(8), triggerCts.Token));
            completed.Should().Be(triggerTask, "manual trigger should complete promptly and not hang");

            var triggered = await triggerTask;
            triggered.Should().BeTrue("manual trigger should succeed for enabled rule");

            // Assert: execution completed and was recorded
            var loadedRule = engine.GetRules().Single(r => r.Id == rule.Id);
            loadedRule.ExecutionCount.Should().Be(1);
            loadedRule.LastExecutedAt.HasValue.Should().BeTrue();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_modelsPath))
                {
                    Directory.Delete(_modelsPath, true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
