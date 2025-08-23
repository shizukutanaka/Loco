using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loco.Core.Models;
using Loco.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Loco.Core.Tests
{
    /// <summary>
    /// Performance-focused unit tests
    /// Following Rob Pike's testing philosophy
    /// </summary>
    public class RuleEngineTests
    {
        private readonly Mock<ILogger<RuleEngine>> _loggerMock;
        private readonly RuleEngine _engine;

        public RuleEngineTests()
        {
            _loggerMock = new Mock<ILogger<RuleEngine>>();
            _engine = new RuleEngine(_loggerMock.Object);
        }

        [Fact]
        public async Task RegisterRule_ValidRule_ReturnsTrue()
        {
            // Arrange
            var rule = CreateTestRule();

            // Act
            var result = await _engine.RegisterRuleAsync(rule);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RegisterRule_NullRule_ReturnsFalse()
        {
            // Act
            var result = await _engine.RegisterRuleAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteRule_RegisteredRule_ExecutesSuccessfully()
        {
            // Arrange
            var rule = CreateTestRule();
            await _engine.RegisterRuleAsync(rule);
            var context = new ExecutionContext();

            // Act
            var result = await _engine.ExecuteRuleAsync(rule.Id, context);

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.Error);
        }

        [Fact]
        public async Task ExecuteRule_UnregisteredRule_ReturnsError()
        {
            // Arrange
            var context = new ExecutionContext();

            // Act
            var result = await _engine.ExecuteRuleAsync("unknown-rule", context);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public void ValidateRule_ValidRule_ReturnsValid()
        {
            // Arrange
            var rule = CreateTestRule();

            // Act
            var result = _engine.ValidateRule(rule);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ValidateRule_MissingTrigger_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Trigger = null,
                Actions = new() { new AutomationDsl.ActionDefinition { Type = "test" } }
            };

            // Act
            var result = _engine.ValidateRule(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("trigger", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void ValidateRule_MissingActions_ReturnsInvalid()
        {
            // Arrange
            var rule = new AutomationDsl.Rule
            {
                Id = "test-rule",
                Trigger = new AutomationDsl.TriggerDefinition { Type = "manual" },
                Actions = new List<AutomationDsl.ActionDefinition>() // Empty list
            };

            // Act
            var result = _engine.ValidateRule(rule);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("action", result.Errors[0], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteRules_MultipleRules_ExecutesInParallel()
        {
            // Arrange
            var rule1 = CreateTestRule("rule1");
            var rule2 = CreateTestRule("rule2");
            var rule3 = CreateTestRule("rule3");
            
            await _engine.RegisterRuleAsync(rule1);
            await _engine.RegisterRuleAsync(rule2);
            await _engine.RegisterRuleAsync(rule3);

            var context = new ExecutionContext();
            var ruleIds = new[] { "rule1", "rule2", "rule3" };

            // Act
            var results = await _engine.ExecuteRulesAsync(ruleIds, context);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Success));
        }

        [Fact]
        public async Task RuleExecution_Performance_CompletesWithinTimeout()
        {
            // Arrange
            var rule = CreateTestRule();
            await _engine.RegisterRuleAsync(rule);
            var context = new ExecutionContext();

            // Act
            var startTime = DateTime.UtcNow;
            var result = await _engine.ExecuteRuleAsync(rule.Id, context);
            var executionTime = DateTime.UtcNow - startTime;

            // Assert
            Assert.True(result.Success);
            Assert.True(executionTime < TimeSpan.FromSeconds(1), 
                $"Execution took {executionTime.TotalMilliseconds}ms, expected < 1000ms");
        }

        private AutomationDsl.Rule CreateTestRule(string id = "test-rule")
        {
            return new AutomationDsl.Rule
            {
                Id = id,
                Name = "Test Rule",
                Trigger = new AutomationDsl.TriggerDefinition
                {
                    Type = "time",
                    Parameters = new() { ["interval"] = "5m" }
                },
                Actions = new()
                {
                    new AutomationDsl.ActionDefinition
                    {
                        Type = "log",
                        Parameters = new() { ["message"] = "Test action executed" }
                    }
                }
            };
        }
    }
}
