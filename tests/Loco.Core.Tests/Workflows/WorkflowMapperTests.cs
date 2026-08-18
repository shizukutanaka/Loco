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
    public void ToVisualWorkflow_FlattensNestedParametersObject()
    {
        // The editor's PropertyPanel writes action arguments nested under
        // config.parameters (keeping them from colliding with "action").
        // Copying that object across verbatim would give the connector a single
        // argument literally named "parameters", so every real argument would
        // read back null - which is what used to happen.
        var stored = StoredWithConfig(new Dictionary<string, JsonElement>
        {
            ["action"] = Json("\"get\""),
            ["parameters"] = Json("""{"url": "https://api.test/x", "method": "GET"}"""),
        });

        var node = WorkflowMapper.ToVisualWorkflow(stored).Nodes[0];

        node.Action.Should().Be("get");
        node.Parameters.Should().ContainKey("url").WhoseValue.Should().Be("https://api.test/x");
        node.Parameters.Should().ContainKey("method").WhoseValue.Should().Be("GET");
        node.Parameters.Should().NotContainKey("parameters",
            "the nested object must be flattened, not passed through as one argument");
    }

    [Fact]
    public void ToVisualWorkflow_StillAcceptsFlatConfigEntries()
    {
        // Workflows authored with arguments at the top level of config must keep
        // working - the flattening is additive, not a replacement.
        var stored = StoredWithConfig(new Dictionary<string, JsonElement>
        {
            ["action"] = Json("\"postMessage\""),
            ["channel"] = Json("\"#general\""),
        });

        var node = WorkflowMapper.ToVisualWorkflow(stored).Nodes[0];

        node.Parameters.Should().ContainKey("channel").WhoseValue.Should().Be("#general");
    }

    [Fact]
    public void ToVisualWorkflow_MergesFlatAndNestedParameters()
    {
        var stored = StoredWithConfig(new Dictionary<string, JsonElement>
        {
            ["action"] = Json("\"sendSms\""),
            ["from"] = Json("\"+15550000\""),
            ["parameters"] = Json("""{"to": "+15551111", "body": "hi"}"""),
        });

        var node = WorkflowMapper.ToVisualWorkflow(stored).Nodes[0];

        node.Parameters.Should().ContainKey("from").WhoseValue.Should().Be("+15550000");
        node.Parameters.Should().ContainKey("to").WhoseValue.Should().Be("+15551111");
        node.Parameters.Should().ContainKey("body").WhoseValue.Should().Be("hi");
        node.Parameters.Should().NotContainKey("parameters");
    }

    [Fact]
    public void ToVisualWorkflow_NonObjectParametersValueIsLeftAlone()
    {
        // Only an object is unwrapped; anything else stays a plain argument so a
        // connector that genuinely takes a "parameters" string is unaffected.
        var stored = StoredWithConfig(new Dictionary<string, JsonElement>
        {
            ["action"] = Json("\"run\""),
            ["parameters"] = Json("\"raw-string\""),
        });

        var node = WorkflowMapper.ToVisualWorkflow(stored).Nodes[0];

        node.Parameters.Should().ContainKey("parameters").WhoseValue.Should().Be("raw-string");
    }

    /// <summary>Single-action-node StoredWorkflow with the given config.</summary>
    private static StoredWorkflow StoredWithConfig(Dictionary<string, JsonElement> config) => new()
    {
        Id = "wf-1",
        Name = "test",
        Nodes = new List<StoredWorkflowNode>
        {
            new()
            {
                Id = "n1",
                Type = "action",
                Position = new StoredPosition { X = 0, Y = 0 },
                Data = new StoredNodeData
                {
                    Label = "node",
                    Integration = "http",
                    Config = config,
                },
            },
        },
        Metadata = new StoredWorkflowMetadata(),
    };

    [Fact]
    public void ToVisualWorkflow_CarriesCredentialIdToTheEngineNode()
    {
        // Without this the field arrived from the editor, fell into
        // ExtensionData and was dropped before execution - so the connector was
        // never initialized and every action failed on a null HttpClient.
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
                    Position = new StoredPosition { X = 0, Y = 0 },
                    Data = new StoredNodeData
                    {
                        Label = "Send message",
                        Integration = "slack",
                        CredentialId = "conn-1",
                        Config = new Dictionary<string, JsonElement>
                        {
                            ["action"] = Json("\"sendMessage\""),
                        },
                    },
                },
            },
            Metadata = new StoredWorkflowMetadata(),
        };

        var node = WorkflowMapper.ToVisualWorkflow(stored).Nodes[0];

        node.CredentialId.Should().Be("conn-1");
        // A reference, never a secret: the workflow JSON stays safe to share.
        node.Parameters.Should().NotContainKey("credentialId");
    }

    [Fact]
    public void ToVisualWorkflow_NodeWithoutCredential_LeavesCredentialIdNull()
    {
        var stored = StoredWithConfig(new Dictionary<string, JsonElement>
        {
            ["action"] = Json("\"log\""),
        });

        WorkflowMapper.ToVisualWorkflow(stored).Nodes[0].CredentialId.Should().BeNull();
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
