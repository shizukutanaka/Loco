using System;
using System.IO;
using System.Threading.Tasks;
using Loco.Cli;
using Xunit;
using FluentAssertions;

namespace Loco.Cli.Tests;

/// <summary>
/// End-to-end tests for `loco workflow run-visual` - the first tests to run
/// the CLI's execution path at all. Each drives the real Program.Main, which
/// parses for real, loads the file for real, and executes the workflow on the
/// real VisualWorkflowEngine with all 28 connectors registered.
///
/// The success case uses engine built-ins only (trigger/transform/log), so a
/// run needs no credentials and touches no network. The credential cases stop
/// deliberately short of executing a connector - that would require a live
/// external service - and instead pin the two failure modes a user actually
/// hits: no data directory given, and a connection id the store does not hold.
/// </summary>
public class WorkflowCommandTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"loco-cli-tests-{Guid.NewGuid():N}");

    public WorkflowCommandTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteWorkflow(string name, string nodesJson, string edgesJson)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
            {
              "id": "wf-cli-test",
              "name": "{{name}}",
              "nodes": {{nodesJson}},
              "edges": {{edgesJson}},
              "metadata": {}
            }
            """);
        return path;
    }

    // trigger -> transform -> delay: three of the editor's six node types
    // (NodeType in src/Loco.VisualEditor/src/types/workflow.ts), all engine
    // built-ins. The first draft of this test used a "log" node - which this
    // very test then proved does not exist: the editor cannot produce one and
    // the engine has no handler for one. The failure was real and correct.
    private const string BuiltinsOnlyNodes = """
        [
          { "id": "n1", "type": "trigger",
            "position": { "x": 0, "y": 0 },
            "data": { "label": "Start", "config": {} } },
          { "id": "n2", "type": "transform",
            "position": { "x": 240, "y": 0 },
            "data": { "label": "Shape", "config": { "json": "{\"ok\":true}" } } },
          { "id": "n3", "type": "delay",
            "position": { "x": 480, "y": 0 },
            "data": { "label": "Breathe", "config": { "seconds": 0 } } }
        ]
        """;

    private const string ChainEdges = """
        [
          { "id": "e1", "source": "n1", "target": "n2" },
          { "id": "e2", "source": "n2", "target": "n3" }
        ]
        """;

    [Fact]
    public async Task Runs_a_builtins_only_workflow_to_completion()
    {
        var file = WriteWorkflow("CLI smoke", BuiltinsOnlyNodes, ChainEdges);

        var exit = await Program.Main(new[] { "workflow", "run-visual", file });

        exit.Should().Be(0, "a trigger->transform->log chain needs no connector and no credential");
    }

    [Fact]
    public async Task A_missing_file_fails_without_executing_anything()
    {
        var exit = await Program.Main(
            new[] { "workflow", "run-visual", Path.Combine(_dir, "absent.json") });

        exit.Should().Be(1);
    }

    [Fact]
    public async Task Unparseable_json_fails_cleanly()
    {
        var path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, "{ this is not json");

        var exit = await Program.Main(new[] { "workflow", "run-visual", path });

        exit.Should().Be(1);
    }

    private const string CredentialedNodes = """
        [
          { "id": "n1", "type": "trigger",
            "position": { "x": 0, "y": 0 },
            "data": { "label": "Start", "config": {} } },
          { "id": "n2", "type": "action",
            "position": { "x": 240, "y": 0 },
            "data": { "label": "Notify", "integration": "slack", "credentialId": "conn-1",
                      "config": { "action": "sendMessage",
                                  "parameters": { "channel": "#x", "message": "hi" } } } }
        ]
        """;

    private const string CredentialedEdges = """
        [ { "id": "e1", "source": "n1", "target": "n2" } ]
        """;

    [Fact]
    public async Task A_credentialed_workflow_with_no_data_dir_stops_with_guidance()
    {
        var file = WriteWorkflow("Needs Slack", CredentialedNodes, CredentialedEdges);

        // The environment variable is the fallback for --data-dir; it must not
        // leak into this case from the machine running the tests.
        var saved = Environment.GetEnvironmentVariable("LOCO_DATA_DIR");
        Environment.SetEnvironmentVariable("LOCO_DATA_DIR", null);
        try
        {
            var exit = await Program.Main(new[] { "workflow", "run-visual", file });

            exit.Should().Be(1,
                "running a connector uninitialized is guaranteed to fail, so the CLI " +
                "must stop and say what to pass instead of failing per node");
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCO_DATA_DIR", saved);
        }
    }

    [Fact]
    public async Task A_connection_the_store_does_not_hold_is_reported_before_execution()
    {
        var file = WriteWorkflow("Needs Slack", CredentialedNodes, CredentialedEdges);
        var dataDir = Path.Combine(_dir, "data");
        Directory.CreateDirectory(dataDir);

        // A real (empty) store at --data-dir: resolution runs, finds nothing
        // under "conn-1", and the run must stop there - same wording the API
        // uses for the same problem.
        var exit = await Program.Main(
            new[] { "workflow", "run-visual", file, "--data-dir", dataDir });

        exit.Should().Be(1);
    }
}
