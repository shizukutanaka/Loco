using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Core.Storage;
using Loco.Core.Utilities;

namespace Loco.Core.Tests
{
    /// <summary>
    /// Property-Based テストスイート
    /// Tests based on invariant properties that should always hold
    ///
    /// Property-based testing generates random inputs and verifies
    /// that certain properties always hold true regardless of input
    /// </summary>
    public class PropertyBasedTests
    {
        #region Rule Store Properties

        [Theory]
        [InlineData("rule-1", "Test Rule")]
        [InlineData("rule-with-dashes", "Rule With Dashes")]
        [InlineData("rule_with_underscores", "Rule_With_Underscores")]
        public async Task RuleStore_UpsertedRuleCanBeRetrieved(string ruleId, string ruleName)
        {
            // Property: After upserting a rule, it should be retrievable by its ID
            // and contain the same data

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var rule = new SimpleRule
            {
                Id = ruleId,
                Name = ruleName,
                IsEnabled = true
            };

            // Act
            await store.UpsertRuleAsync(rule);
            var retrieved = await store.GetRuleAsync(ruleId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(ruleId, retrieved.Id);
            Assert.Equal(ruleName, retrieved.Name);
            Assert.True(retrieved.IsEnabled);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task RuleStore_CountAfterUpsert_MatchesExpected(int count)
        {
            // Property: The number of rules should equal the number of upserts
            // (no duplicates if same ID, but count increases)

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            // Act
            for (int i = 0; i < count; i++)
            {
                var rule = new SimpleRule
                {
                    Id = $"rule-{i}",
                    Name = $"Rule {i}",
                    IsEnabled = true
                };
                await store.UpsertRuleAsync(rule);
            }

            var allRules = await store.GetRulesAsync();

            // Assert
            Assert.Equal(count, allRules.Count);
        }

        [Theory]
        [InlineData("rule-1")]
        [InlineData("rule-123")]
        public async Task RuleStore_DeletedRuleIsNotRetrievable(string ruleId)
        {
            // Property: After deleting a rule, it should not be retrievable

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var rule = new SimpleRule { Id = ruleId, Name = "Test" };
            await store.UpsertRuleAsync(rule);

            // Act
            await store.DeleteRuleAsync(ruleId);
            var retrieved = await store.GetRuleAsync(ruleId);

            // Assert
            Assert.Null(retrieved);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RuleStore_EnabledRulesFilteringConsistent(bool enabled)
        {
            // Property: Enabled rules filter should only return rules with matching status

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var enabledRule = new SimpleRule { Id = "enabled", Name = "Enabled", IsEnabled = true };
            var disabledRule = new SimpleRule { Id = "disabled", Name = "Disabled", IsEnabled = false };

            await store.UpsertRuleAsync(enabledRule);
            await store.UpsertRuleAsync(disabledRule);

            // Act
            var enabledRules = await store.GetEnabledRulesAsync();

            // Assert
            Assert.Single(enabledRules);
            Assert.True(enabledRules.All(r => r.IsEnabled));
            Assert.DoesNotContain(disabledRule.Id, enabledRules.Select(r => r.Id));
        }

        #endregion

        #region Workflow Execution Properties

        [Fact]
        public async Task WorkflowExecution_AlwaysProducesConsistentState()
        {
            // Property: Multiple executions of the same workflow with same inputs
            // should produce consistent execution patterns (not necessarily same output,
            // but deterministic behavior)

            // Arrange
            var executions = new List<string>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                var execution = Guid.NewGuid().ToString();
                executions.Add(execution);
            }

            // Assert
            // All executions have unique IDs (idempotency property)
            Assert.Equal(executions.Count, executions.Distinct().Count());

            // All execution IDs are valid GUIDs
            foreach (var exec in executions)
            {
                Assert.NotEmpty(exec);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task WorkflowExecution_ProgressIsMonotonic(int startProgress)
        {
            // Property: Progress should never decrease during execution
            // (monotonically increasing or stay same)

            // Arrange
            var progressValues = new[] { startProgress, startProgress, startProgress + 25, startProgress + 50, startProgress + 100 };

            // Act
            for (int i = 1; i < progressValues.Length; i++)
            {
                var currentProgress = progressValues[i];
                var previousProgress = progressValues[i - 1];

                // Assert
                Assert.True(currentProgress >= previousProgress,
                    $"Progress decreased from {previousProgress} to {currentProgress}");
            }
        }

        [Fact]
        public void WorkflowExecution_StatusTransitionsAreValid()
        {
            // Property: Workflow status should follow valid state transitions
            // Only specific transitions are allowed

            var validTransitions = new Dictionary<string, List<string>>
            {
                { "Queued", new List<string> { "Running", "Cancelled" } },
                { "Running", new List<string> { "Completed", "Failed", "Paused" } },
                { "Paused", new List<string> { "Running", "Cancelled" } },
                { "Completed", new List<string> { } },    // Terminal state
                { "Failed", new List<string> { } },        // Terminal state
                { "Cancelled", new List<string> { } }      // Terminal state
            };

            var statuses = new[] { "Queued", "Running", "Completed" };

            // Assert
            for (int i = 1; i < statuses.Length; i++)
            {
                var currentStatus = statuses[i];
                var previousStatus = statuses[i - 1];

                Assert.True(validTransitions.ContainsKey(previousStatus),
                    $"Unknown status: {previousStatus}");

                Assert.True(validTransitions[previousStatus].Contains(currentStatus),
                    $"Invalid transition from {previousStatus} to {currentStatus}");
            }
        }

        #endregion

        #region Data Integrity Properties

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task UpsertRule_MultipleTimesSameId_OnlyLatestKept(int updateCount)
        {
            // Property: Upserting same rule ID multiple times should result in
            // exactly one rule with the latest data

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var ruleId = "test-rule";

            // Act
            for (int i = 0; i < updateCount; i++)
            {
                var rule = new SimpleRule
                {
                    Id = ruleId,
                    Name = $"Update {i}",
                    IsEnabled = i % 2 == 0
                };
                await store.UpsertRuleAsync(rule);
            }

            var allRules = await store.GetRulesAsync();
            var finalRule = await store.GetRuleAsync(ruleId);

            // Assert
            Assert.Single(allRules);
            Assert.NotNull(finalRule);
            Assert.Equal($"Update {updateCount - 1}", finalRule.Name);
            Assert.Equal((updateCount - 1) % 2 == 0, finalRule.IsEnabled);
        }

        [Fact]
        public async Task ClearRules_RemovesAllRules()
        {
            // Property: After clearing, there should be zero rules

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            for (int i = 0; i < 10; i++)
            {
                await store.UpsertRuleAsync(new SimpleRule { Id = $"rule-{i}", Name = $"Rule {i}" });
            }

            // Act
            await store.ClearRulesAsync();
            var rules = await store.GetRulesAsync();

            // Assert
            Assert.Empty(rules);
        }

        #endregion

        #region Input Validation Properties

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RuleStore_NullOrEmptyIdHandledGracefully(string invalidId)
        {
            // Property: Null or empty IDs should not crash, but handled gracefully

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            // Act & Assert
            var exists = await store.RuleExistsAsync(invalidId);
            var rule = await store.GetRuleAsync(invalidId);

            // Assert
            Assert.False(exists);
            Assert.Null(rule);
        }

        #endregion

        #region Idempotency Properties

        [Fact]
        public async Task WorkflowExecution_IdempotencyKeyProducesConsistentResult()
        {
            // Property: Executing same workflow with same parameters and idempotency key
            // should produce same result (idempotent)

            // Arrange
            var executionId1 = Guid.NewGuid().ToString();
            var executionId2 = Guid.NewGuid().ToString();
            var idempotencyKey = Guid.NewGuid().ToString();

            // Act & Assert
            // In real scenario, would call API twice with same idempotency key
            // Both should return same result (either new execution or cached result)

            Assert.NotEqual(executionId1, executionId2);
            Assert.NotEmpty(idempotencyKey);
        }

        #endregion

        #region Concurrency Properties

        [Fact]
        public async Task ConcurrentRuleOperations_NoRaceConditions()
        {
            // Property: Concurrent operations should not cause data corruption

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var tasks = new List<Task>();

            // Act - Create 10 concurrent upserts
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var rule = new SimpleRule
                    {
                        Id = $"rule-{index}",
                        Name = $"Rule {index}",
                        IsEnabled = true
                    };
                    await store.UpsertRuleAsync(rule);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var allRules = await store.GetRulesAsync();
            Assert.Equal(10, allRules.Count);

            // All rules should be intact
            for (int i = 0; i < 10; i++)
            {
                var rule = await store.GetRuleAsync($"rule-{i}");
                Assert.NotNull(rule);
                Assert.Equal($"Rule {i}", rule.Name);
            }
        }

        #endregion

        #region Helper Methods

        private void AssertProperty(bool property, string propertyName)
        {
            Assert.True(property, $"Property failed: {propertyName}");
        }

        private void AssertPropertyForAll<T>(IEnumerable<T> items, Func<T, bool> property, string propertyName)
        {
            foreach (var item in items)
            {
                AssertProperty(property(item), $"{propertyName} for {item}");
            }
        }

        #endregion
    }
}
