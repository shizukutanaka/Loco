using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Loco.Core;
using Loco.Core.Models;
using Loco.Core.Storage;
using Loco.Core.Configuration;

namespace Loco.Core.Tests
{
    public class SimpleLightEnginePersistenceTests
    {
        private readonly string _testStorePath;
        private readonly Mock<ILogger> _mockLogger;

        public SimpleLightEnginePersistenceTests()
        {
            _testStorePath = Path.Combine(Path.GetTempPath(), $"loco_test_rules_{Guid.NewGuid()}.json");
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public async Task SimpleLightEngine_Should_Load_Rules_From_Persistent_Store()
        {
            // Arrange
            var config = new LocoConfig { MaxConcurrentFlows = 5 };
            var store = new JsonFileRuleStore(_testStorePath, _mockLogger.Object);

            var trigger = new LightTrigger { Type = "manual" };
            var action = new LightAction
            {
                Type = "log",
                Parameters = new Dictionary<string, object?>
                {
                    ["message"] = "Test message"
                }
            };

            var testRule = new SimpleRule("test-rule-1", "Test Rule", trigger, new[] { action });
            await store.UpsertRuleAsync(testRule);

            // Act - Create engine with the store
            var engine = new SimpleLightEngine(_mockLogger.Object, config, store);
            await engine.StartAsync();

            // Assert
            var status = engine.GetEngineStatus();
            Assert.Equal(1, status.RuleCount);
        }

        [Fact]
        public async Task SimpleLightEngine_Should_Persist_New_Rules()
        {
            // Arrange
            var config = new LocoConfig { MaxConcurrentFlows = 5 };
            var store = new JsonFileRuleStore(_testStorePath, _mockLogger.Object);
            var engine = new SimpleLightEngine(_mockLogger.Object, config, store);
            await engine.StartAsync();

            var trigger = new LightTrigger { Type = "manual" };
            var action = new LightAction
            {
                Type = "log",
                Parameters = new Dictionary<string, object?>
                {
                    ["message"] = "Persisted message"
                }
            };

            // Act
            var ruleId = engine.CreateRule("Persisted Test Rule", trigger, new[] { action });

            // Assert - New rule should be in the store
            await Task.Delay(100); // Give async persistence time to complete
            var persistedRule = await store.GetRuleAsync(ruleId);
            Assert.NotNull(persistedRule);
            Assert.Equal("Persisted Test Rule", persistedRule.Name);
        }

        [Fact]
        public async Task JsonFileRuleStore_Should_Maintain_Data_Integrity()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testStorePath, _mockLogger.Object);
            var trigger = new LightTrigger { Type = "schedule" };

            // Act - Create multiple rules
            for (int i = 0; i < 5; i++)
            {
                var action = new LightAction
                {
                    Type = "log",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["message"] = $"Message {i}"
                    }
                };
                var rule = new SimpleRule($"rule-{i}", $"Rule {i}", trigger, new[] { action });
                await store.UpsertRuleAsync(rule);
            }

            // Assert - All rules should be retrievable
            var allRules = await store.GetRulesAsync();
            Assert.Equal(5, allRules.Count);

            foreach (var i in Enumerable.Range(0, 5))
            {
                var rule = await store.GetRuleAsync($"rule-{i}");
                Assert.NotNull(rule);
                Assert.Equal($"Rule {i}", rule.Name);
            }
        }

        [Fact]
        public async Task JsonFileRuleStore_Should_Handle_Deletion()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testStorePath, _mockLogger.Object);
            var trigger = new LightTrigger { Type = "manual" };
            var action = new LightAction
            {
                Type = "log",
                Parameters = new Dictionary<string, object?>
                {
                    ["message"] = "Delete test"
                }
            };
            var rule = new SimpleRule("delete-test", "Delete Test Rule", trigger, new[] { action });
            await store.UpsertRuleAsync(rule);

            // Act
            await store.DeleteRuleAsync("delete-test");

            // Assert
            var deletedRule = await store.GetRuleAsync("delete-test");
            Assert.Null(deletedRule);
        }

        [Fact]
        public async Task JsonFileRuleStore_Should_Filter_Enabled_Rules()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testStorePath, _mockLogger.Object);
            var trigger = new LightTrigger { Type = "manual" };
            var action = new LightAction
            {
                Type = "log",
                Parameters = new Dictionary<string, object?>
                {
                    ["message"] = "Filter test"
                }
            };

            var enabledRule = new SimpleRule("enabled-1", "Enabled Rule", trigger, new[] { action })
            {
                IsEnabled = true
            };
            var disabledRule = new SimpleRule("disabled-1", "Disabled Rule", trigger, new[] { action })
            {
                IsEnabled = false
            };

            await store.UpsertRuleAsync(enabledRule);
            await store.UpsertRuleAsync(disabledRule);

            // Act
            var enabledRules = await store.GetEnabledRulesAsync();

            // Assert
            Assert.Single(enabledRules);
            Assert.Equal("enabled-1", enabledRules[0].Id);
        }

        // Cleanup
        public void Dispose()
        {
            if (File.Exists(_testStorePath))
            {
                File.Delete(_testStorePath);
            }
        }
    }
}
