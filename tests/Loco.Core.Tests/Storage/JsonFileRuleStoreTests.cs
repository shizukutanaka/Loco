using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Loco.Core.Storage;
using Loco.Core.Models;

namespace Loco.Core.Tests.Storage
{
    public class JsonFileRuleStoreTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _testFilePath;

        public JsonFileRuleStoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "loco-test-" + Guid.NewGuid());
            Directory.CreateDirectory(_testDirectory);
            _testFilePath = Path.Combine(_testDirectory, "test-rules.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public async Task GetRulesAsync_EmptyStore_ReturnsEmptyList()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            // Act
            var rules = await store.GetRulesAsync();

            // Assert
            Assert.NotNull(rules);
            Assert.Empty(rules);
        }

        [Fact]
        public async Task UpsertRuleAsync_NewRule_SavesSuccessfully()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "test-rule-1",
                Name = "Test Rule",
                Description = "Test Description",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = new[] { new LightAction { Type = "log" } }
            };

            // Act
            await store.UpsertRuleAsync(rule);

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Single(rules);
            Assert.Equal("test-rule-1", rules[0].Id);
            Assert.Equal("Test Rule", rules[0].Name);
        }

        [Fact]
        public async Task UpsertRuleAsync_ExistingRule_UpdatesSuccessfully()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "test-rule-1",
                Name = "Original Name",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            };

            await store.UpsertRuleAsync(rule);

            // Act
            rule.Name = "Updated Name";
            await store.UpsertRuleAsync(rule);

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Single(rules);
            Assert.Equal("Updated Name", rules[0].Name);
        }

        [Fact]
        public async Task GetRuleAsync_ExistingRule_ReturnsRule()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "test-rule-1",
                Name = "Test Rule",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            };

            await store.UpsertRuleAsync(rule);

            // Act
            var retrieved = await store.GetRuleAsync("test-rule-1");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("test-rule-1", retrieved.Id);
            Assert.Equal("Test Rule", retrieved.Name);
        }

        [Fact]
        public async Task GetRuleAsync_NonExistentRule_ReturnsNull()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            // Act
            var result = await store.GetRuleAsync("non-existent-id");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteRuleAsync_ExistingRule_RemovesRule()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "test-rule-1",
                Name = "Test Rule",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            };

            await store.UpsertRuleAsync(rule);

            // Act
            await store.DeleteRuleAsync("test-rule-1");

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Empty(rules);
        }

        [Fact]
        public async Task DeleteRuleAsync_NonExistentRule_DoesNotThrow()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            // Act & Assert
            await store.DeleteRuleAsync("non-existent-id"); // Should not throw
        }

        [Fact]
        public async Task ClearRulesAsync_WithMultipleRules_RemovesAllRules()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            for (int i = 0; i < 5; i++)
            {
                await store.UpsertRuleAsync(new SimpleRule
                {
                    Id = $"rule-{i}",
                    Name = $"Rule {i}",
                    Trigger = new LightTrigger { Type = "manual" },
                    Actions = Array.Empty<LightAction>()
                });
            }

            // Act
            await store.ClearRulesAsync();

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Empty(rules);
        }

        [Fact]
        public async Task RuleExistsAsync_ExistingRule_ReturnsTrue()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "test-rule-1",
                Name = "Test Rule",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            };

            await store.UpsertRuleAsync(rule);

            // Act
            var exists = await store.RuleExistsAsync("test-rule-1");

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task RuleExistsAsync_NonExistentRule_ReturnsFalse()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            // Act
            var exists = await store.RuleExistsAsync("non-existent-id");

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task GetEnabledRulesAsync_FiltersDisabledRules()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            await store.UpsertRuleAsync(new SimpleRule
            {
                Id = "enabled-rule",
                Name = "Enabled Rule",
                IsEnabled = true,
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            });

            await store.UpsertRuleAsync(new SimpleRule
            {
                Id = "disabled-rule",
                Name = "Disabled Rule",
                IsEnabled = false,
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            });

            // Act
            var enabledRules = await store.GetEnabledRulesAsync();

            // Assert
            Assert.Single(enabledRules);
            Assert.Equal("enabled-rule", enabledRules[0].Id);
        }

        [Fact]
        public async Task UpsertRuleAsync_MultipleRules_MaintainsAllRules()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);

            // Act
            for (int i = 0; i < 10; i++)
            {
                await store.UpsertRuleAsync(new SimpleRule
                {
                    Id = $"rule-{i}",
                    Name = $"Rule {i}",
                    Trigger = new LightTrigger { Type = "manual" },
                    Actions = Array.Empty<LightAction>()
                });
            }

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Equal(10, rules.Count);
        }

        [Fact]
        public async Task JsonFileRuleStore_PersistsAcrossInstances()
        {
            // Arrange
            var rule = new SimpleRule
            {
                Id = "persistent-rule",
                Name = "Persistent Rule",
                Trigger = new LightTrigger { Type = "manual" },
                Actions = Array.Empty<LightAction>()
            };

            // Act - First instance
            var store1 = new JsonFileRuleStore(_testFilePath);
            await store1.UpsertRuleAsync(rule);

            // Act - Second instance
            var store2 = new JsonFileRuleStore(_testFilePath);
            var retrieved = await store2.GetRuleAsync("persistent-rule");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Persistent Rule", retrieved.Name);
        }

        [Fact]
        public async Task UpsertRuleAsync_WithComplexActions_PreservesData()
        {
            // Arrange
            var store = new JsonFileRuleStore(_testFilePath);
            var rule = new SimpleRule
            {
                Id = "complex-rule",
                Name = "Complex Rule",
                Trigger = new LightTrigger
                {
                    Type = "scheduled",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["interval"] = "5m",
                        ["enabled"] = true
                    }
                },
                Actions = new[]
                {
                    new LightAction
                    {
                        Type = "file",
                        Parameters = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["operation"] = "list",
                            ["path"] = "/tmp"
                        }
                    },
                    new LightAction
                    {
                        Type = "log",
                        Parameters = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["message"] = "Test message",
                            ["level"] = "info"
                        }
                    }
                }
            };

            // Act
            await store.UpsertRuleAsync(rule);
            var retrieved = await store.GetRuleAsync("complex-rule");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(2, retrieved.Actions.Length);
            Assert.Equal("file", retrieved.Actions[0].Type);
            Assert.Equal("log", retrieved.Actions[1].Type);
            Assert.NotNull(retrieved.Trigger.Parameters);
            Assert.Equal(2, retrieved.Trigger.Parameters.Count);
        }
    }
}
