using Xunit;
using Loco.Core.FlowComposer;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace Loco.Core.Tests;

public class FlowComposerBuilderTests
{
    private readonly FlowComposerBuilder _builder;

    public FlowComposerBuilderTests()
    {
        _builder = new FlowComposerBuilder(NullLogger<FlowComposerBuilder>.Instance);
    }

    [Fact]
    public void AddTrigger_AddsTriggerToFlow()
    {
        // Arrange
        var componentId = "Core.Time.Interval";
        var parameters = new System.Collections.Generic.Dictionary<string, object> { { "interval", "5m" } };

        // Act
        _builder.AddTrigger(componentId, parameters);
        var flow = _builder.Build();

        // Assert
        Assert.Single(flow.Triggers);
        Assert.Equal(componentId, flow.Triggers.First().Uses);
        Assert.Equal("5m", flow.Triggers.First().With["interval"]);
    }

    [Fact]
    public void AddAction_AddsActionToFlow()
    {
        // Arrange
        var componentId = "Core.Log.Write";
        var parameters = new System.Collections.Generic.Dictionary<string, object> { { "message", "Hello" } };

        // Act
        _builder.AddAction(componentId, parameters);
        var flow = _builder.Build();

        // Assert
        Assert.Single(flow.Actions);
        Assert.Equal(componentId, flow.Actions.First().Uses);
        Assert.Equal("Hello", flow.Actions.First().With["message"]);
    }

    [Fact]
    public void AddCondition_AddsConditionToFlow()
    {
        // Arrange
        var componentId = "Core.Condition.Equals";
        var parameters = new System.Collections.Generic.Dictionary<string, object> { { "left", "a" }, { "right", "b" } };

        // Act
        _builder.AddCondition(componentId, parameters);
        var flow = _builder.Build();

        // Assert
        Assert.Single(flow.Conditions);
        Assert.Equal(componentId, flow.Conditions.First().Uses);
        Assert.Equal("a", flow.Conditions.First().With["left"]);
    }

    [Fact]
    public void Clear_RemovesAllComponentsFromFlow()
    {
        // Arrange
        _builder.AddTrigger("Core.Time.Interval", null);
        _builder.AddAction("Core.Log.Write", null);

        // Act
        _builder.Clear();
        var flow = _builder.Build();

        // Assert
        Assert.Empty(flow.Triggers);
        Assert.Empty(flow.Actions);
        Assert.Empty(flow.Conditions);
    }

    [Fact]
    public void Build_WithMultipleComponents_BuildsCorrectFlow()
    {
        // Arrange
        _builder.AddTrigger("T1", new System.Collections.Generic.Dictionary<string, object> { { "p1", "v1" } });
        _builder.AddCondition("C1", new System.Collections.Generic.Dictionary<string, object> { { "p2", "v2" } });
        _builder.AddAction("A1", new System.Collections.Generic.Dictionary<string, object> { { "p3", "v3" } });
        _builder.AddAction("A2", new System.Collections.Generic.Dictionary<string, object> { { "p4", "v4" } });

        // Act
        var flow = _builder.Build();

        // Assert
        Assert.Single(flow.Triggers);
        Assert.Equal("T1", flow.Triggers.First().Uses);
        Assert.Single(flow.Conditions);
        Assert.Equal("C1", flow.Conditions.First().Uses);
        Assert.Equal(2, flow.Actions.Count);
        Assert.Equal("A1", flow.Actions[0].Uses);
        Assert.Equal("A2", flow.Actions[1].Uses);
    }
}
