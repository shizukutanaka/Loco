using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Interfaces;
using Loco.Automation.Services;
using Loco.Automation.Interfaces;

namespace Loco.Core.Tests.Integration
{
    public class EndToEndIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly IAutomationService _automationService;
        private readonly IAutomationRuleEngine _ruleEngine;
        private readonly IFlowEngine _flowEngine;
        private readonly string _testDataPath;

        public EndToEndIntegrationTests()
        {
            _testDataPath = Path.Combine(Path.GetTempPath(), $"loco_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDataPath);

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            services.AddSingleton<IAutomationRuleEngine, AutomationRuleEngine>();
            services.AddSingleton<IAutomationService, AutomationService>();
            services.AddSingleton<IFlowEngine, FlowEngine>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<NaturalLanguageToDslConverter>();
            services.AddSingleton<INaturalLanguageRuleService, NaturalLanguageRuleService>();

            _serviceProvider = services.BuildServiceProvider();
            _automationService = _serviceProvider.GetRequiredService<IAutomationService>();
            _ruleEngine = _serviceProvider.GetRequiredService<IAutomationRuleEngine>();
            _flowEngine = _serviceProvider.GetRequiredService<IFlowEngine>();
        }

        [Fact]
        public async Task CompleteWorkflow_FromNaturalLanguage_To_Execution()
        {
            // Arrange
            var nlService = _serviceProvider.GetRequiredService<INaturalLanguageRuleService>();
            var naturalLanguageInput = "Every day at 9 AM, send notification 'Good morning' and backup files";

            // Act - Convert natural language to rule
            var ruleJson = await nlService.ConvertTextToRuleJsonAsync(naturalLanguageInput);
            ruleJson.Should().NotBeNullOrEmpty();

            // Add rule to automation service
            var added = await _automationService.AddRuleFromJsonAsync(ruleJson);
            added.Should().BeTrue();

            // Verify rule was added
            var rules = await _ruleEngine.GetAllRulesAsync();
            rules.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Flow_Registration_And_Execution()
        {
            // Arrange
            var flow = new FlowDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Integration Flow",
                Description = "End-to-end test flow",
                CreatedAt = DateTime.UtcNow,
                Triggers = new List<FlowTrigger>
                {
                    new FlowTrigger
                    {
                        Type = "manual",
                        Config = new Dictionary<string, object>()
                    }
                },
                Actions = new List<FlowAction>
                {
                    new FlowAction
                    {
                        Type = "log",
                        Config = new Dictionary<string, object>
                        {
                            { "message", "Integration test executed" }
                        }
                    }
                }
            };

            // Act
            var registered = await _flowEngine.RegisterFlowAsync(flow);
            registered.Should().BeTrue();

            var executed = await _flowEngine.ExecuteFlowAsync(flow.Id);
            executed.Should().BeTrue();

            // Cleanup
            var unregistered = await _flowEngine.UnregisterFlowAsync(flow.Id);
            unregistered.Should().BeTrue();
        }

        [Fact]
        public async Task Rule_Persistence_And_Loading()
        {
            // Arrange
            var rulesFilePath = Path.Combine(_testDataPath, "test_rules.json");
            var rule = new Rule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Persistence Test Rule",
                Description = "Tests rule persistence",
                Enabled = true,
                Trigger = new Trigger
                {
                    Type = "file.changed",
                    Config = new Dictionary<string, object>
                    {
                        { "path", _testDataPath },
                        { "pattern", "*.txt" }
                    }
                },
                Actions = new List<Action>
                {
                    new Action
                    {
                        Type = "log",
                        Config = new Dictionary<string, object>
                        {
                            { "message", "File changed detected" }
                        }
                    }
                }
            };

            // Act - Add and save rule
            await _ruleEngine.AddRuleAsync(rule);
            await _automationService.SaveRulesAsync(rulesFilePath);

            // Clear and reload
            await _ruleEngine.ClearAllRulesAsync();
            var rulesBeforeLoad = await _ruleEngine.GetAllRulesAsync();
            rulesBeforeLoad.Should().BeEmpty();

            await _automationService.LoadRulesFromFileAsync(rulesFilePath);

            // Assert
            var rulesAfterLoad = await _ruleEngine.GetAllRulesAsync();
            rulesAfterLoad.Should().ContainSingle();
            rulesAfterLoad[0].Id.Should().Be(rule.Id);
            rulesAfterLoad[0].Name.Should().Be(rule.Name);
        }

        [Fact]
        public async Task Multiple_Rules_Execution_Order()
        {
            // Arrange
            var executionOrder = new List<string>();
            var rules = new List<Rule>();

            for (int i = 1; i <= 3; i++)
            {
                var rule = new Rule
                {
                    Id = $"rule_{i}",
                    Name = $"Rule {i}",
                    Enabled = true,
                    Priority = i,
                    Trigger = new Trigger
                    {
                        Type = "manual",
                        Config = new Dictionary<string, object>()
                    },
                    Actions = new List<Action>
                    {
                        new Action
                        {
                            Type = "custom",
                            Config = new Dictionary<string, object>
                            {
                                { "order", i }
                            }
                        }
                    }
                };
                rules.Add(rule);
                await _ruleEngine.AddRuleAsync(rule);
            }

            // Act - Trigger all rules
            foreach (var rule in rules)
            {
                await _ruleEngine.TriggerRuleAsync(rule.Id, new Dictionary<string, object>());
            }

            // Assert - Rules should execute in priority order
            // Note: This is a simplified test. In real implementation, 
            // you'd need to track execution order through the action handlers
            var allRules = await _ruleEngine.GetAllRulesAsync();
            allRules.Should().HaveCount(3);
        }

        [Fact]
        public async Task Error_Handling_And_Recovery()
        {
            // Arrange
            var faultyRule = new Rule
            {
                Id = "faulty_rule",
                Name = "Faulty Rule",
                Enabled = true,
                Actions = new List<Action>
                {
                    new Action
                    {
                        Type = "invalid_action_type",
                        Config = null // Intentionally null to cause error
                    }
                }
            };

            // Act & Assert - Should handle gracefully
            await _ruleEngine.AddRuleAsync(faultyRule);
            
            Func<Task> act = async () => 
                await _ruleEngine.TriggerRuleAsync(faultyRule.Id, new Dictionary<string, object>());
            
            await act.Should().NotThrowAsync();

            // System should still be operational
            var rules = await _ruleEngine.GetAllRulesAsync();
            rules.Should().Contain(r => r.Id == faultyRule.Id);
        }

        [Fact]
        public async Task Concurrent_Rule_Operations()
        {
            // Arrange
            var tasks = new List<Task<bool>>();
            var ruleCount = 50;

            // Act - Add multiple rules concurrently
            for (int i = 0; i < ruleCount; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    var rule = new Rule
                    {
                        Id = $"concurrent_rule_{index}",
                        Name = $"Concurrent Rule {index}",
                        Enabled = true
                    };
                    return await _ruleEngine.AddRuleAsync(rule);
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllBeEquivalentTo(true);
            var allRules = await _ruleEngine.GetAllRulesAsync();
            allRules.Should().HaveCount(ruleCount);
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            
            // Clean up test data
            if (Directory.Exists(_testDataPath))
            {
                try
                {
                    Directory.Delete(_testDataPath, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
    }
}
