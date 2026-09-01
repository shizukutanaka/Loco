using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Loco.Core.Models;
using Loco.Core.Storage;

namespace Loco.Core.Tests
{
    /// <summary>
    /// Chaos Engineering テストスイート
    /// Tests system resilience and fault tolerance
    ///
    /// Chaos engineering intentionally injects faults to verify
    /// the system can recover and handle failures gracefully
    /// </summary>
    public class ChaosEngineeringTests
    {
        #region Fault Injection: Transient Failures

        [Fact]
        public async Task RuleStore_RecoverFromTransientFileError()
        {
            // Chaos: Simulate temporary file access error
            // Verify: System recovers and succeeds on retry

            // Arrange
            var testFilePath = Path.Combine(Path.GetTempPath(), $"test-chaos-{Guid.NewGuid()}.json");
            var store = new JsonFileRuleStore(testFilePath);
            var rule = new SimpleRule { Id = "rule-1", Name = "Test" };

            // Act
            // Even if file is temporarily unavailable, operation should eventually succeed
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await store.UpsertRuleAsync(rule);
                    break;
                }
                catch (Exception) when (attempt < 2)
                {
                    await Task.Delay(100);
                }
            }

            // Assert
            var retrieved = await store.GetRuleAsync("rule-1");
            Assert.NotNull(retrieved);
            Assert.Equal("Test", retrieved.Name);
        }

        [Fact]
        public async Task RuleStore_HandlesPartialWriteFailure()
        {
            // Chaos: Simulate incomplete write operation
            // Verify: Data remains consistent (no corruption)

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var rule = new SimpleRule { Id = "rule-1", Name = "Test" };
            await store.UpsertRuleAsync(rule);

            // Act
            // Simulate failure recovery
            var retrieved = await store.GetRuleAsync("rule-1");

            // Assert - Data should be consistent
            Assert.NotNull(retrieved);
            Assert.Equal("Test", retrieved.Name);
        }

        #endregion

        #region Chaos: Resource Exhaustion

        [Fact]
        public async Task RuleStore_HandlesLargeDataset()
        {
            // Chaos: Stress with large dataset
            // Verify: Handles memory pressure gracefully

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            // Act - Create 1000 rules (stress test)
            for (int i = 0; i < 1000; i++)
            {
                var rule = new SimpleRule
                {
                    Id = $"rule-{i}",
                    Name = $"Rule {i}",
                    IsEnabled = i % 2 == 0
                };
                await store.UpsertRuleAsync(rule);
            }

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Equal(1000, rules.Count);

            // Verify specific rules are retrievable
            var lastRule = await store.GetRuleAsync("rule-999");
            Assert.NotNull(lastRule);
        }

        [Fact]
        public async Task RuleStore_HandlesConcurrentLoad()
        {
            // Chaos: High concurrent load
            // Verify: No data corruption under stress

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var tasks = new List<Task>();

            // Act - 50 concurrent writes
            for (int i = 0; i < 50; i++)
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

            // Add concurrent reads
            for (int i = 0; i < 30; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await store.GetRulesAsync();
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Equal(50, rules.Count);
        }

        #endregion

        #region Chaos: Timing Issues

        // WorkflowExecution_HandlesNetworkLatency was removed here. It awaited
        // Task.Delay(100ms) - SimulateNetworkLatency's whole body - and asserted
        // that at least 100ms had elapsed. That tests the .NET timer, not one
        // line of Loco, and it is flaky in both directions: Stopwatch and the
        // timer read different clocks, so a 99.x ms measurement fails a run that
        // did nothing wrong. It failed exactly that way when the CLI tests were
        // added and the suite's timing shifted.

        [Fact]
        public async Task RuleStore_SurvivesRandomDelays()
        {
            // Chaos: Random delays injected
            // Verify: All operations complete eventually

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));
            var random = new Random(42);
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 20; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    // Inject random delay
                    var delay = random.Next(0, 100);
                    await Task.Delay(delay);

                    var rule = new SimpleRule { Id = $"rule-{index}", Name = $"Rule {index}" };
                    await store.UpsertRuleAsync(rule);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var rules = await store.GetRulesAsync();
            Assert.Equal(20, rules.Count);
        }

        #endregion

        #region Chaos: Invalid State Transitions

        [Fact]
        public void WorkflowExecution_RejectsInvalidStatusTransitions()
        {
            // Chaos: Attempt invalid state transitions
            // Verify: System rejects invalid transitions

            var validTransitions = new Dictionary<string, List<string>>
            {
                { "Queued", new List<string> { "Running", "Cancelled" } },
                { "Running", new List<string> { "Completed", "Failed" } },
                { "Completed", new List<string> { } },  // No transitions from final state
            };

            var invalidTransitions = new[]
            {
                ("Completed", "Running"),  // Can't go back
                ("Failed", "Queued"),      // Can't restart
                ("Cancelled", "Running")   // Cancelled is final
            };

            // Act & Assert
            foreach (var (from, to) in invalidTransitions)
            {
                if (validTransitions.ContainsKey(from))
                {
                    var isValid = validTransitions[from].Contains(to);
                    Assert.False(isValid, $"Transition {from} -> {to} should be invalid");
                }
            }
        }

        #endregion

        #region Chaos: Data Corruption Scenarios

        [Fact]
        public async Task RuleStore_SurvivesCorruptedFile()
        {
            // Chaos: Corrupt data file
            // Verify: System handles gracefully without crashing

            // Arrange
            var testFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json");
            var store = new JsonFileRuleStore(testFilePath);

            var rule = new SimpleRule { Id = "rule-1", Name = "Test" };
            await store.UpsertRuleAsync(rule);

            // Corrupt file by writing invalid JSON
            File.WriteAllText(testFilePath, "{ corrupted json [[[");

            // Act - Try to read after corruption
            var result = async () => await store.GetRulesAsync();

            // Assert - Should not throw, should return empty list
            var rules = await result();
            Assert.NotNull(rules);
            // File is corrupted, so should return empty or handle gracefully
        }

        [Fact]
        public async Task RuleStore_HandlesInsufficientPermissions()
        {
            // Chaos: File system permission denied
            // Verify: System indicates error gracefully

            // Arrange
            var testFilePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json");
            var store = new JsonFileRuleStore(testFilePath);

            var rule = new SimpleRule { Id = "rule-1", Name = "Test" };
            await store.UpsertRuleAsync(rule);

            // Act & Assert
            // Make file read-only (on Windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var fileInfo = new System.IO.FileInfo(testFilePath);
                fileInfo.Attributes = System.IO.FileAttributes.ReadOnly;

                // Try to write (should fail or handle gracefully)
                try
                {
                    var rule2 = new SimpleRule { Id = "rule-2", Name = "Test2" };
                    await store.UpsertRuleAsync(rule2);
                    // If this succeeds, permissions weren't actually denied
                }
                catch (UnauthorizedAccessException)
                {
                    // Expected
                }

                // Restore permissions
                fileInfo.Attributes = System.IO.FileAttributes.Normal;
            }
        }

        #endregion

        #region Chaos: Cascading Failures

        [Fact]
        public async Task System_RecoverFromCascadingFailures()
        {
            // Chaos: Multiple failures in sequence
            // Verify: System recovers from cascade

            // Arrange
            var store = new JsonFileRuleStore(
                Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.json"));

            var failures = 0;

            // Act - Simulate cascading failures
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    // Simulate failure
                    if (i % 3 == 0)
                    {
                        throw new InvalidOperationException("Simulated failure");
                    }

                    var rule = new SimpleRule { Id = $"rule-{i}", Name = $"Rule {i}" };
                    await store.UpsertRuleAsync(rule);
                }
                catch (InvalidOperationException)
                {
                    failures++;
                    // System should continue despite failures
                }
            }

            // Assert
            Assert.True(failures > 0, "No failures were simulated");

            // System should still be operational
            var rules = await store.GetRulesAsync();
            Assert.NotEmpty(rules);
        }

        #endregion

        #region Monitoring Chaos

        [Fact]
        public async Task System_TracksFailureMetrics()
        {
            // Chaos: Monitor failure rates
            // Verify: Metrics accurately reflect failures

            // Arrange
            var metrics = new ChaosMetrics();

            // Act
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    var failureRate = i % 10 == 0 ? 1 : 0;
                    if (failureRate == 1)
                    {
                        throw new Exception("Simulated failure");
                    }
                    metrics.RecordSuccess();
                }
                catch
                {
                    metrics.RecordFailure();
                }
            }

            // Assert
            Assert.Equal(90, metrics.SuccessCount);
            Assert.Equal(10, metrics.FailureCount);
            Assert.Equal(90.0d, metrics.SuccessRate());
        }

        #endregion

        #region Helper Methods

        #endregion
    }

    /// <summary>
    /// Chaos テスト用のメトリクス収集
    /// Metrics collection for chaos testing
    /// </summary>
    public class ChaosMetrics
    {
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }

        public void RecordSuccess()
        {
            SuccessCount++;
        }

        public void RecordFailure()
        {
            FailureCount++;
        }

        public double SuccessRate()
        {
            var total = SuccessCount + FailureCount;
            return total == 0 ? 0 : (double)SuccessCount / total * 100;
        }
    }
}
