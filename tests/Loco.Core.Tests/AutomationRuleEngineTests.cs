using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Loco.Core.Models.AutomationDsl;
using Loco.Core.Interfaces;
using Loco.Core.Services;
using Loco.Core.Triggers;

namespace Loco.Core.Tests
{
    public class AutomationRuleEngineTests
    {
        private readonly Mock<ILogger<AutomationRuleEngine>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<SandboxExecutor> _sandboxExecutorMock;
        private readonly Mock<LlmModelManager> _llmModelManagerMock;
        private readonly Mock<NaturalLanguageToDslConverter> _nlConverterMock;
        private readonly Mock<ITriggerFactory> _triggerFactoryMock;
        private readonly AutomationRuleEngine _ruleEngine;

        public AutomationRuleEngineTests()
        {
            _loggerMock = new Mock<ILogger<AutomationRuleEngine>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _sandboxExecutorMock = new Mock<SandboxExecutor>(Mock.Of<ILogger<SandboxExecutor>>());
            _llmModelManagerMock = new Mock<LlmModelManager>(Mock.Of<ILogger<LlmModelManager>>(), Mock.Of<IServiceProvider>());
            _nlConverterMock = new Mock<NaturalLanguageToDslConverter>(Mock.Of<ILogger<NaturalLanguageToDslConverter>>(), _llmModelManagerMock.Object);
            _triggerFactoryMock = new Mock<ITriggerFactory>();

            _ruleEngine = new AutomationRuleEngine(
                _loggerMock.Object,
                _serviceProviderMock.Object,
                _sandboxExecutorMock.Object,
                _llmModelManagerMock.Object,
                _nlConverterMock.Object,
                _triggerFactoryMock.Object);
        }

        private Rule CreateTestRule(string id = null, bool enabled = true)
        {
            return new Rule
            {
                Id = id ?? Guid.NewGuid().ToString(),
                Name = "Test Rule",
                Enabled = enabled,
                Trigger = new TriggerDefinition { Type = "manual" },
                Actions = new List<ActionDefinition> 
                {
                    new ActionDefinition { Type = "log" }
                },
                Permissions = new PermissionSet { Network = true } 
            };
        }

        [Fact]
        public async Task AddRuleAsync_WithValidRule_ShouldLoadAndStartTrigger()
        {
            // Arrange
            var rule = CreateTestRule();
            var triggerMock = new Mock<IRuntimeTrigger>();
            _triggerFactoryMock.Setup(f => f.CreateTrigger(rule.Trigger)).Returns(triggerMock.Object);

            // Act
            var result = await _ruleEngine.AddRuleAsync(rule);

            // Assert
            result.Should().BeTrue();
            _ruleEngine.GetRules().Should().ContainSingle(r => r.Id == rule.Id);
            _triggerFactoryMock.Verify(f => f.CreateTrigger(rule.Trigger), Times.Once);
            triggerMock.Verify(t => t.StartAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddRuleAsync_WithDisabledRule_ShouldNotStartTrigger()
        {
            // Arrange
            var rule = CreateTestRule(enabled: false);
            var triggerMock = new Mock<IRuntimeTrigger>();
            _triggerFactoryMock.Setup(f => f.CreateTrigger(rule.Trigger)).Returns(triggerMock.Object);

            // Act
            var result = await _ruleEngine.AddRuleAsync(rule);

            // Assert
            result.Should().BeTrue();
            triggerMock.Verify(t => t.StartAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteRuleAsync_WithExistingRule_ShouldStopAndRemoveRuleAndTrigger()
        {
            // Arrange
            var rule = CreateTestRule();
            var triggerMock = new Mock<IRuntimeTrigger>();
            _triggerFactoryMock.Setup(f => f.CreateTrigger(rule.Trigger)).Returns(triggerMock.Object);
            await _ruleEngine.AddRuleAsync(rule);

            // Act
            var result = await _ruleEngine.DeleteRuleAsync(rule.Id);

            // Assert
            result.Should().BeTrue();
            _ruleEngine.GetRules().Should().BeEmpty();
            triggerMock.Verify(t => t.StopAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteRuleAsync_WithNonExistentRule_ShouldReturnFalse()
        {
            // Act
            var result = await _ruleEngine.DeleteRuleAsync("non-existent-id");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SetRuleEnabledAsync_ToTrue_ShouldStartTrigger()
        {
            // Arrange
            var rule = CreateTestRule(enabled: false);
            var triggerMock = new Mock<IRuntimeTrigger>();
            _triggerFactoryMock.Setup(f => f.CreateTrigger(rule.Trigger)).Returns(triggerMock.Object);
            await _ruleEngine.AddRuleAsync(rule);

            // Act
            var result = await _ruleEngine.SetRuleEnabledAsync(rule.Id, true);

            // Assert
            result.Should().BeTrue();
            _ruleEngine.GetRules().First().Enabled.Should().BeTrue();
            triggerMock.Verify(t => t.StartAsync(It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetRuleEnabledAsync_ToFalse_ShouldStopTrigger()
        {
            // Arrange
            var rule = CreateTestRule(enabled: true);
            var triggerMock = new Mock<IRuntimeTrigger>();
            _triggerFactoryMock.Setup(f => f.CreateTrigger(rule.Trigger)).Returns(triggerMock.Object);
            await _ruleEngine.AddRuleAsync(rule);

            // Act
            var result = await _ruleEngine.SetRuleEnabledAsync(rule.Id, false);

            // Assert
            result.Should().BeTrue();
            _ruleEngine.GetRules().First().Enabled.Should().BeFalse();
            triggerMock.Verify(t => t.StopAsync(), Times.Once);
        }
    }
}
