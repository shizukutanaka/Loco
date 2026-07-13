using System.Collections.Generic;
using System.Text.Json;
using Loco.Core.Workflows;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Workflows;

/// <summary>
/// Tests for WorkflowMapper - converts the Visual Editor's persisted Workflow
/// shape (StoredWorkflow) into the execution engine's VisualWorkflow. Moved
/// from Loco.Api to Loco.Core (this pass) so both the API and the CLI's
/// `run-visual` command can share one mapping.
///
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked); execution status is recorded in the commit message.
/// </summary>
public class WorkflowMapperTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ToVisualWorkflow_MapsNodeIntegrationAndAction()
    {
        var stored = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "test",
            Nodes = new List<StoredWorkflowNode>
            {
                new()
                {
                    Id = "n1",
                    Type = "action",
                    Position = new StoredPosition { X = 10, Y = 20 },
                    Data = new StoredNodeData
                    {
                        Label = "Send message",
                        Integration = "slack",
                        Config = new Dictionary<string, JsonElement>
                        {
                            ["action"] = Json("\"postMessage\""),
                            ["channel"] = Json("\"#general\""),
                        },
                    },
                },
            },
            Metadata = new StoredWorkflowMetadata(),
        };

        var visual = WorkflowMapper.ToVisualWorkflow(stored);

        visual.Nodes.Should().ContainSingle();
        var node = visual.Nodes[0];
        node.Id.Should().Be("n1");
        node.Name.Should().Be("Send message");
        node.Type.Should().Be("action");
        node.Integration.Should().Be("slack");
        node.Action.Should().Be("postMessage",
            "config[\"action\"] must become WorkflowNode.Action, not a Parameters entry");
        node.Parameters.Should().ContainKey("channel").WhoseValue.Should().Be("#general");
        node.Parameters.Should().NotContainKey("action",
            "the action key must be extracted, not duplicated into Parameters");
        node.Position.X.Should().Be(10);
        node.Position.Y.Should().Be(20);
    }

    [Fact]
    public void ToVisualWorkflow_MapsEdgeConditionAndHandles()
    {
        var stored = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "test",
            Edges = new List<StoredWorkflowEdge>
            {
                new()
                {
                    Id = "e1",
                    Source = "n1",
                    Target = "n2",
                    SourceHandle = "success",
                    TargetHandle = "in",
                    Data = new StoredEdgeData { Condition = "error" },
                },
            },
            Metadata = new StoredWorkflowMetadata(),
        };

        var visual = WorkflowMapper.ToVisualWorkflow(stored);

        var conn = visual.Connections.Should().ContainSingle().Subject;
        conn.SourceNodeId.Should().Be("n1");
        conn.TargetNodeId.Should().Be("n2");
        conn.SourceOutput.Should().Be("success");
        conn.TargetInput.Should().Be("in");
        conn.Condition.Should().Be("error");
    }

    [Fact]
    public void ToVisualWorkflow_EdgeWithoutData_DefaultsHandlesAndNullCondition()
    {
        var stored = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "test",
            Edges = new List<StoredWorkflowEdge> { new() { Id = "e1", Source = "a", Target = "b" } },
            Metadata = new StoredWorkflowMetadata(),
        };

        var conn = WorkflowMapper.ToVisualWorkflow(stored).Connections.Should().ContainSingle().Subject;

        conn.SourceOutput.Should().Be("default");
        conn.TargetInput.Should().Be("default");
        conn.Condition.Should().BeNull();
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("42", 42L)]
    [InlineData("3.5", 3.5)]
    public void ToPlainObject_ConvertsScalarJsonToClrTypes(string json, object expected)
    {
        WorkflowMapper.ToPlainObject(Json(json)).Should().Be(expected);
    }

    [Fact]
    public void ToPlainObject_ConvertsArraysAndObjects()
    {
        var array = WorkflowMapper.ToPlainObject(Json("[1, 2, 3]"));
        array.Should().BeAssignableTo<List<object>>().Which.Should().BeEquivalentTo(new object[] { 1L, 2L, 3L });

        var obj = WorkflowMapper.ToPlainObject(Json("""{"a": 1, "b": "x"}"""));
        obj.Should().BeAssignableTo<Dictionary<string, object>>().Which.Should().BeEquivalentTo(
            new Dictionary<string, object> { ["a"] = 1L, ["b"] = "x" });
    }

    [Fact]
    public void ToVisualWorkflow_CopiesTopLevelMetadata()
    {
        var stored = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "My Workflow",
            Description = "does things",
            Metadata = new StoredWorkflowMetadata
            {
                Version = "2.0.0",
                Author = "alice",
                Tags = new List<string> { "prod", "critical" },
            },
        };

        var visual = WorkflowMapper.ToVisualWorkflow(stored);

        visual.Id.Should().Be("wf-1");
        visual.Name.Should().Be("My Workflow");
        visual.Description.Should().Be("does things");
        visual.Version.Should().Be("2.0.0");
        visual.Author.Should().Be("alice");
        visual.Tags.Should().BeEquivalentTo(new[] { "prod", "critical" });
    }
}
