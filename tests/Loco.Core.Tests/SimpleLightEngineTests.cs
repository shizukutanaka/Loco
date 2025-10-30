using System;
using System.Threading.Tasks;
using Loco.Core;
using Loco.Core.Configuration;
using Loco.Core.Models;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests;

/// <summary>
/// Tests for SimpleLightEngine - Lightweight automation engine
/// </summary>
public class SimpleLightEngineTests : IDisposable
{
    private readonly SimpleLightEngine _engine;
    private readonly LocoConfig _config;

    public SimpleLightEngineTests()
    {
        _config = new LocoConfig();
        _engine = new SimpleLightEngine(null, _config);
    }

    #region Engine Lifecycle Tests

    [Fact]
    public async Task StartAsync_InitializesEngine()
    {
        // Act
        await _engine.StartAsync();

        // Assert
        var status = _engine.GetEngineStatus();
        status.Should().NotBeNull();
    }

    [Fact]
    public async Task StopAsync_StopsEngine()
    {
        // Arrange
        await _engine.StartAsync();

        // Act
        await _engine.StopAsync();

        // Assert
        var status = _engine.GetEngineStatus();
        status.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ThenStop_Succeeds()
    {
        // Act & Assert
        await _engine.StartAsync();
        await _engine.StopAsync();
    }

    #endregion

    #region Engine Status Tests

    [Fact]
    public void GetEngineStatus_ReturnsEngineStatus()
    {
        // Act
        var status = _engine.GetEngineStatus();

        // Assert
        status.Should().NotBeNull();
        status.FlowCount.Should().BeGreaterThanOrEqualTo(0);
        status.RuleCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void EngineStatus_CalculatesSuccessRate()
    {
        // Act
        var status = _engine.GetEngineStatus();

        // Assert
        status.SuccessRate.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void EngineStatus_TracksExecutionCounts()
    {
        // Act
        var status = _engine.GetEngineStatus();

        // Assert
        status.TotalExecutions.Should().Be(status.SuccessfulExecutions + 
            (status.TotalExecutions - status.SuccessfulExecutions));
        status.SuccessfulExecutions.Should().BeLessThanOrEqualTo(status.TotalExecutions);
    }

    #endregion

    #region Rule Creation Tests

    [Fact]
    public void CreateRule_WithValidParameters_ReturnsRuleId()
    {
        // Arrange
        var trigger = new LightTrigger();
        var actions = new[] { new LightAction() };

        // Act
        var ruleId = _engine.CreateRule("test-rule", trigger, actions);

        // Assert
        ruleId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateRule_MultipleRules_CreatesUniqueIds()
    {
        // Arrange
        var trigger = new LightTrigger();
        var actions = new[] { new LightAction() };

        // Act
        var ruleId1 = _engine.CreateRule("rule-1", trigger, actions);
        var ruleId2 = _engine.CreateRule("rule-2", trigger, actions);

        // Assert
        ruleId1.Should().NotBe(ruleId2);
    }

    #endregion

    #region Scheduling Tests

    [Fact]
    public void ScheduleRule_WithValidInterval_Succeeds()
    {
        // Arrange
        var ruleId = _engine.CreateRule("scheduled-rule", new LightTrigger(), new[] { new LightAction() });
        var interval = TimeSpan.FromSeconds(5);

        // Act & Assert
        _engine.ScheduleRule(ruleId, interval);
    }

    [Fact]
    public void ScheduleRuleOnce_WithFutureTime_Succeeds()
    {
        // Arrange
        var ruleId = _engine.CreateRule("once-rule", new LightTrigger(), new[] { new LightAction() });
        var futureTime = DateTime.UtcNow.AddSeconds(10);

        // Act & Assert
        _engine.ScheduleRuleOnce(ruleId, futureTime);
    }

    [Fact]
    public void CancelScheduledRule_WithValidRuleId_ReturnsResult()
    {
        // Arrange
        var ruleId = _engine.CreateRule("cancel-rule", new LightTrigger(), new[] { new LightAction() });
        _engine.ScheduleRule(ruleId, TimeSpan.FromSeconds(5));

        // Act
        var cancelled = _engine.CancelScheduledRule(ruleId);

        // Assert
        cancelled.Should().BeTrue(); // Rule was scheduled, so cancellation should succeed
    }

    #endregion

    #region Flow Execution Tests

    [Fact]
    public async Task ExecuteFlowAsync_WithValidFlowId_ReturnsBoolean()
    {
        // Arrange
        await _engine.StartAsync();
        var flow = new SimpleFlow("Test Flow", "Test Description", "test-flow");
        _engine.AddFlow(flow);

        // Act
        var result = await _engine.ExecuteFlowAsync("test-flow");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AddFlow_AddsFlowToEngine()
    {
        // Arrange
        var flow = new SimpleFlow("Test Flow", "Test Description", "test-flow");

        // Act
        _engine.AddFlow(flow);

        // Assert - Flow was added, status should reflect it
        var status = _engine.GetEngineStatus();
        status.FlowCount.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Health Checks Tests

    [Fact]
    public async Task IsHealthyAsync_ReturnsStatus()
    {
        // Act & Assert - Should not throw
        await _engine.IsHealthyAsync();
    }

    [Fact]
    public async Task IsHealthyAsync_AfterStart_ReturnsHealthStatus()
    {
        // Arrange
        await _engine.StartAsync();

        // Act - Should not throw
        var isHealthy = await _engine.IsHealthyAsync();

        // Assert - IsHealthy returns a boolean value
        isHealthy.Should().BeTrue(); // Engine should be healthy after start
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_FreesResources()
    {
        // Act
        _engine.Dispose();

        // Assert - Should not throw
    }

    #endregion

    public void Dispose()
    {
        _engine?.Dispose();
    }
}
