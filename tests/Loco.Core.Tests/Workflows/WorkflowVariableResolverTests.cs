using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loco.Core.Workflows;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Workflows;

/// <summary>
/// Tests for {{...}} resolution, and for the fact that it now happens for every
/// node rather than only for connector-backed actions.
///
/// The resolver itself was not new - it lived inside WorkflowConnectorBridge -
/// but it ran only on the path that dispatches connector actions. The engine's
/// own built-in handlers read WorkflowNode.Parameters directly, so a condition
/// node comparing {{amount}} compared the eight characters "{{amount}}". Since
/// a condition's whole purpose is to compare something an earlier node
/// produced, that left it able to compare only two constants - while the
/// PropertyPanel's help text said "Supports {{variable}} references".
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class WorkflowVariableResolverTests
{
    private static WorkflowExecutionContext ContextWith(
        Dictionary<string, object>? variables = null,
        Dictionary<string, NodeExecutionResult>? results = null)
    {
        return new WorkflowExecutionContext
        {
            Variables = variables ?? new Dictionary<string, object>(),
            NodeResults = results ?? new Dictionary<string, NodeExecutionResult>(),
        };
    }

    [Fact]
    public void Resolve_ReplacesAWholeReferenceWithTheValueItself()
    {
        var context = ContextWith(new Dictionary<string, object> { ["amount"] = 150 });

        var resolved = WorkflowVariableResolver.Resolve("{{amount}}", context);

        // The value keeps its own type. A condition using greater_than has to
        // compare a number to a number; handing it the string "150" would make
        // the comparison depend on Convert.ToDouble rather than on the data.
        resolved.Should().Be(150);
    }

    [Fact]
    public void Resolve_SubstitutesAReferenceInsideALongerString()
    {
        var context = ContextWith(new Dictionary<string, object> { ["name"] = "Ada" });

        WorkflowVariableResolver.Resolve("Hello {{name}}, welcome", context)
            .Should().Be("Hello Ada, welcome");
    }

    [Fact]
    public void Resolve_LeavesAnUnknownReferenceEmptyRatherThanLiteral()
    {
        var context = ContextWith();

        // Inline: an unresolvable path renders as nothing. Rendering the
        // braces instead would send "{{missing}}" to a real API as if it were
        // the user's data.
        WorkflowVariableResolver.Resolve("value: {{missing}}", context)
            .Should().Be("value: ");
    }

    [Fact]
    public void Resolve_LeavesAStringWithNoReferencesAlone()
    {
        WorkflowVariableResolver.Resolve("plain text", ContextWith()).Should().Be("plain text");
    }

    [Fact]
    public void Resolve_PassesNonStringsThrough()
    {
        WorkflowVariableResolver.Resolve(42, ContextWith()).Should().Be(42);
        WorkflowVariableResolver.Resolve(null, ContextWith()).Should().BeNull();
    }

    [Fact]
    public void Resolve_ReadsAPropertyOffAnEarlierNodesResult()
    {
        var context = ContextWith(results: new Dictionary<string, NodeExecutionResult>
        {
            ["fetch"] = new NodeExecutionResult
            {
                NodeId = "fetch",
                Success = true,
                Data = new { status = "succeeded" },
            },
        });

        WorkflowVariableResolver.Resolve("{{fetch.status}}", context)
            .Should().Be("succeeded");
    }

    [Fact]
    public void WithResolvedParameters_DoesNotMutateTheOriginalNode()
    {
        // The same node object is re-executed on retry. Resolving in place
        // would bake the first attempt's values into every later one.
        var context = ContextWith(new Dictionary<string, object> { ["x"] = "first" });
        var node = new WorkflowNode
        {
            Id = "n1",
            Type = "condition",
            Parameters = new Dictionary<string, object> { ["left"] = "{{x}}" },
        };

        var resolved = WorkflowVariableResolver.WithResolvedParameters(node, context);

        resolved.Parameters["left"].Should().Be("first");
        node.Parameters["left"].Should().Be("{{x}}");
    }

    [Fact]
    public void WithResolvedParameters_CopiesEveryPropertyOfTheNode()
    {
        // Guards the copy against WorkflowNode growing a property that nobody
        // remembers to carry across - which would turn "resolve the
        // parameters" into "silently drop this node's retry policy".
        var node = new WorkflowNode
        {
            Id = "n1",
            Name = "Check",
            Type = "condition",
            Integration = "http",
            Action = "get",
            CredentialId = "cred-1",
            Parameters = new Dictionary<string, object> { ["left"] = "constant" },
            Position = new NodePosition { X = 3, Y = 4 },
            Disabled = true,
            Notes = "a note",
            RetryConfig = new RetryConfig { MaxAttempts = 5 },
        };

        var copy = WorkflowVariableResolver.WithResolvedParameters(node, ContextWith());

        var uncopied = typeof(WorkflowNode)
            .GetProperties()
            .Where(p => p.Name != nameof(WorkflowNode.Parameters))
            .Where(p => !Equals(p.GetValue(copy), p.GetValue(node)))
            .Select(p => p.Name)
            .ToList();

        uncopied.Should().BeEmpty(
            "every property of WorkflowNode must survive parameter resolution");
    }

    [Fact]
    public async Task Engine_ResolvesReferencesForBuiltInNodes()
    {
        // The regression this whole change exists for. Before, the condition
        // handler received the literal "{{amount}}" and "{{limit}}", compared
        // two unequal strings, and reported false - so the true branch of every
        // data-driven condition was unreachable.
        var engine = new VisualWorkflowEngine();
        var workflow = new VisualWorkflow
        {
            Id = "wf",
            Name = "condition over a variable",
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "check",
                    Name = "Check",
                    Type = "condition",
                    Parameters = new Dictionary<string, object>
                    {
                        ["left"] = "{{amount}}",
                        ["operation"] = "greater_than",
                        ["right"] = "100",
                    },
                },
            },
        };

        var context = await engine.ExecuteAsync(
            workflow,
            new Dictionary<string, object> { ["amount"] = 150 },
            CancellationToken.None);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);

        var verdict = context.NodeResults["check"].Data!;
        verdict.GetType().GetProperty("condition")!.GetValue(verdict)
            .Should().Be(true, "150 is greater than 100");
    }

    [Fact]
    public async Task Engine_ResolvesToFalseWhenTheDataSaysSo()
    {
        // The companion to the test above. A resolution step that always
        // produced `true` would satisfy that one on its own.
        var engine = new VisualWorkflowEngine();
        var workflow = new VisualWorkflow
        {
            Id = "wf",
            Name = "condition over a variable",
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "check",
                    Name = "Check",
                    Type = "condition",
                    Parameters = new Dictionary<string, object>
                    {
                        ["left"] = "{{amount}}",
                        ["operation"] = "greater_than",
                        ["right"] = "100",
                    },
                },
            },
        };

        var context = await engine.ExecuteAsync(
            workflow,
            new Dictionary<string, object> { ["amount"] = 20 },
            CancellationToken.None);

        var verdict = context.NodeResults["check"].Data!;
        verdict.GetType().GetProperty("condition")!.GetValue(verdict)
            .Should().Be(false, "20 is not greater than 100");
    }
}
