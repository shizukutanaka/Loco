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
/// Characterization tests for VisualWorkflowEngine - the canonical execution
/// engine behind POST /api/v1/workflows/{id}/execute. The engine previously had
/// zero tests despite being the best-implemented engine in the repo.
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked); execution status is recorded in the commit message.
/// </summary>
public class VisualWorkflowEngineTests
{
    private static VisualWorkflow TwoNodeWorkflow(
        Func<WorkflowNode, WorkflowExecutionContext, Task<object?>>? handler,
        VisualWorkflowEngine engine,
        string integration = "test",
        string action = "run")
    {
        if (handler != null)
        {
            engine.RegisterNodeHandler($"{integration}:{action}", handler);
        }

        var n1 = new WorkflowNode { Name = "first", Type = "action", Integration = integration, Action = action };
        var n2 = new WorkflowNode { Name = "second", Type = "action", Integration = integration, Action = action };
        return new VisualWorkflow
        {
            Name = "two-node",
            Nodes = new List<WorkflowNode> { n1, n2 },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = n1.Id, TargetNodeId = n2.Id },
            },
        };
    }

    [Fact]
    public async Task Execute_TwoNodeChain_RunsBothAndSucceeds()
    {
        var engine = new VisualWorkflowEngine();
        var executed = new List<string>();
        var workflow = TwoNodeWorkflow(async (node, _) =>
        {
            executed.Add(node.Name);
            return node.Name;
        }, engine);

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);
        executed.Should().ContainInOrder("first", "second");
        context.NodeResults.Should().HaveCount(2);
        context.NodeResults.Values.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task Execute_NoTriggerNodes_FailsWithClearError()
    {
        var engine = new VisualWorkflowEngine();
        // Two nodes forming a cycle: neither has "no incoming connections".
        var n1 = new WorkflowNode { Name = "a", Type = "action", Integration = "t", Action = "r" };
        var n2 = new WorkflowNode { Name = "b", Type = "action", Integration = "t", Action = "r" };
        var workflow = new VisualWorkflow
        {
            Name = "cycle",
            Nodes = new List<WorkflowNode> { n1, n2 },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = n1.Id, TargetNodeId = n2.Id },
                new() { SourceNodeId = n2.Id, TargetNodeId = n1.Id },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        context.Error.Should().Contain("trigger");
    }

    [Fact]
    public async Task Execute_UnknownIntegrationAction_FailsNotSilentlySucceeds()
    {
        var engine = new VisualWorkflowEngine();
        var workflow = new VisualWorkflow
        {
            Name = "unknown-handler",
            Nodes = new List<WorkflowNode>
            {
                new() { Name = "n", Type = "custom-type", Integration = "nope", Action = "missing" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        context.Error.Should().Contain("nope:missing");
    }

    [Fact]
    public async Task Execute_FailingNode_WithErrorConnection_RoutesToErrorHandlerAndSucceeds()
    {
        var engine = new VisualWorkflowEngine();
        var errorHandlerRan = false;
        engine.RegisterNodeHandler("t:boom", (_, _) => throw new InvalidOperationException("kaboom"));
        engine.RegisterNodeHandler("t:recover", async (_, _) =>
        {
            errorHandlerRan = true;
            return "recovered";
        });

        var failing = new WorkflowNode { Name = "failing", Type = "action", Integration = "t", Action = "boom" };
        var recovery = new WorkflowNode { Name = "recovery", Type = "action", Integration = "t", Action = "recover" };
        var workflow = new VisualWorkflow
        {
            Name = "error-routing",
            Nodes = new List<WorkflowNode> { failing, recovery },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = failing.Id, TargetNodeId = recovery.Id, Condition = "error" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        errorHandlerRan.Should().BeTrue();
        context.Status.Should().Be(WorkflowExecutionStatus.Success,
            "a handled error (error-edge present) must not fail the whole workflow");
        context.NodeResults[failing.Id].Success.Should().BeFalse();
        context.NodeResults[recovery.Id].Success.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_FailingNode_WithoutErrorConnection_FailsWorkflow()
    {
        var engine = new VisualWorkflowEngine();
        engine.RegisterNodeHandler("t:boom", (_, _) => throw new InvalidOperationException("kaboom"));

        var workflow = new VisualWorkflow
        {
            Name = "unhandled-error",
            Nodes = new List<WorkflowNode>
            {
                new() { Name = "failing", Type = "action", Integration = "t", Action = "boom" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        context.Error.Should().Contain("kaboom");
    }

    [Fact]
    public async Task Execute_CancelledBetweenNodes_ReportsCancelledNotFailed()
    {
        var engine = new VisualWorkflowEngine();
        using var cts = new CancellationTokenSource();
        var workflow = TwoNodeWorkflow(async (node, _) =>
        {
            if (node.Name == "first")
            {
                // Cancel while the first node is "running"; the second must not start.
                cts.Cancel();
            }
            return node.Name;
        }, engine);

        var context = await engine.ExecuteAsync(workflow, initialVariables: null, cts.Token);

        context.Status.Should().Be(WorkflowExecutionStatus.Cancelled,
            "caller-requested cancellation is not a failure");
        context.NodeResults.Should().ContainKey(workflow.Nodes[0].Id);
        context.NodeResults.Should().NotContainKey(workflow.Nodes[1].Id);
    }

    [Fact]
    public async Task Execute_CancelDuringDelayNode_InterruptsTheWait()
    {
        var engine = new VisualWorkflowEngine();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var workflow = new VisualWorkflow
        {
            Name = "delay-cancel",
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Name = "long-delay",
                    Type = "delay",
                    // No Integration:Action pair - resolved via the type-level "delay" handler.
                    Parameters = new Dictionary<string, object> { ["seconds"] = 30 },
                },
            },
        };

        var started = DateTime.UtcNow;
        var context = await engine.ExecuteAsync(workflow, initialVariables: null, cts.Token);
        var elapsed = DateTime.UtcNow - started;

        context.Status.Should().Be(WorkflowExecutionStatus.Cancelled);
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10),
            "cancel must interrupt the delay, not sit out the full 30s");
    }

    [Fact]
    public async Task Execute_SuccessCondition_SkipsBranchWhenNodeFails()
    {
        var engine = new VisualWorkflowEngine();
        var successBranchRan = false;
        engine.RegisterNodeHandler("t:boom", (_, _) => throw new InvalidOperationException("kaboom"));
        engine.RegisterNodeHandler("t:next", async (_, _) =>
        {
            successBranchRan = true;
            return null;
        });
        engine.RegisterNodeHandler("t:recover", async (_, _) => "recovered");

        var failing = new WorkflowNode { Name = "failing", Type = "action", Integration = "t", Action = "boom" };
        var onSuccess = new WorkflowNode { Name = "onSuccess", Type = "action", Integration = "t", Action = "next" };
        var onError = new WorkflowNode { Name = "onError", Type = "action", Integration = "t", Action = "recover" };
        var workflow = new VisualWorkflow
        {
            Name = "branching",
            Nodes = new List<WorkflowNode> { failing, onSuccess, onError },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = failing.Id, TargetNodeId = onSuccess.Id, Condition = "success" },
                new() { SourceNodeId = failing.Id, TargetNodeId = onError.Id, Condition = "error" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        successBranchRan.Should().BeFalse("the success edge must not fire for a failed node");
        context.NodeResults[onError.Id].Success.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_VariableNodes_SetThenGet_SharesStateThroughContext()
    {
        var engine = new VisualWorkflowEngine();
        var setNode = new WorkflowNode
        {
            Name = "set",
            Type = "action",
            Integration = "variable",
            Action = "set",
            Parameters = new Dictionary<string, object> { ["name"] = "greeting", ["value"] = "hello" },
        };
        var getNode = new WorkflowNode
        {
            Name = "get",
            Type = "action",
            Integration = "variable",
            Action = "get",
            Parameters = new Dictionary<string, object> { ["name"] = "greeting" },
        };
        var workflow = new VisualWorkflow
        {
            Name = "variables",
            Nodes = new List<WorkflowNode> { setNode, getNode },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = setNode.Id, TargetNodeId = getNode.Id },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);
        context.Variables["greeting"].Should().Be("hello");
        context.NodeResults[getNode.Id].Data.Should().Be("hello");
    }

    [Fact]
    public void Validator_CyclicGraph_ReportsError()
    {
        var n1 = new WorkflowNode { Name = "a", Type = "action" };
        var n2 = new WorkflowNode { Name = "b", Type = "action" };
        var workflow = new VisualWorkflow
        {
            Name = "cyclic",
            Nodes = new List<WorkflowNode> { n1, n2 },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = n1.Id, TargetNodeId = n2.Id },
                new() { SourceNodeId = n2.Id, TargetNodeId = n1.Id },
            },
        };

        // VisualWorkflowValidator, not WorkflowValidator: the class was renamed
        // to stop it colliding with the identically-named one in
        // Loco.Core.Workflow (singular), and this call site was missed. It
        // could not resolve against the plural namespace this file imports, so
        // it was a third file keeping Loco.Core.Tests from compiling at all.
        var result = new VisualWorkflowValidator().Validate(workflow);

        result.IsValid.Should().BeFalse();
    }
}

/// <summary>
/// Tests that a cancelled execution reaches work already running inside a node.
///
/// POST /api/v1/executions/{id}/cancel was wired end to end - registry, token
/// source, engine - but the engine only observed the token BETWEEN nodes, and
/// the connector bridge passed its handlers the token captured when connectors
/// were registered at startup. That token belongs to the host: it is cancelled
/// when the server shuts down, never when a run is cancelled. So an HTTP call
/// already in flight inside a connector ran to completion, and a slow one kept
/// the execution alive long after the user asked it to stop.
///
/// The engine puts the execution's own token on the context for exactly this
/// reason (WorkflowExecutionContext.CancellationToken). These pin that it is
/// really the execution's token, and that a handler observing it produces a
/// Cancelled workflow rather than a Failed one.
///
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked by organization policy); the first CI run executes these.
/// </summary>
public class VisualWorkflowEngineCancellationTests
{
    private static VisualWorkflow OneNode(string integration, string action) =>
        new()
        {
            Name = "one-node",
            Nodes = new List<WorkflowNode>
            {
                new() { Name = "only", Type = "action", Integration = integration, Action = action },
            },
        };

    [Fact]
    public async Task The_context_carries_the_executions_own_token()
    {
        // The property the bridge depends on: what a handler reads off the
        // context must be the token the caller can cancel, not some default.
        var engine = new VisualWorkflowEngine();
        using var cts = new CancellationTokenSource();

        CancellationToken observed = default;
        engine.RegisterNodeHandler("t:peek", (_, context) =>
        {
            observed = context.CancellationToken;
            return Task.FromResult<object?>("ok");
        });

        await engine.ExecuteAsync(OneNode("t", "peek"), null, cts.Token);

        observed.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Cancelling_mid_node_stops_the_workflow_as_cancelled()
    {
        // The regression itself: work already running inside a node must end,
        // and the run must report Cancelled rather than Failed - a failed run
        // reads as the workflow being broken.
        var engine = new VisualWorkflowEngine();
        using var cts = new CancellationTokenSource();

        engine.RegisterNodeHandler("t:hang", async (_, context) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
            return "never reached";
        });

        var result = await engine.ExecuteAsync(OneNode("t", "hang"), null, cts.Token);

        result.Status.Should().Be(WorkflowExecutionStatus.Cancelled);
    }

    [Fact]
    public async Task A_handler_ignoring_the_token_still_finishes_its_node()
    {
        // Cancellation is cooperative. A handler that never looks at the token
        // runs to completion, and the engine stops at the next node boundary -
        // worth pinning so nobody reads the fix as a hard kill.
        var engine = new VisualWorkflowEngine();
        using var cts = new CancellationTokenSource();

        var completed = false;
        engine.RegisterNodeHandler("t:stubborn", (_, _) =>
        {
            cts.Cancel();
            completed = true;
            return Task.FromResult<object?>("done anyway");
        });

        var result = await engine.ExecuteAsync(OneNode("t", "stubborn"), null, cts.Token);

        completed.Should().BeTrue();
        result.Status.Should().Be(WorkflowExecutionStatus.Cancelled);
    }

    [Fact]
    public async Task An_uncancelled_run_leaves_the_token_uncancelled()
    {
        var engine = new VisualWorkflowEngine();

        var wasCancelled = true;
        engine.RegisterNodeHandler("t:quiet", (_, context) =>
        {
            wasCancelled = context.CancellationToken.IsCancellationRequested;
            return Task.FromResult<object?>("ok");
        });

        var result = await engine.ExecuteAsync(OneNode("t", "quiet"));

        wasCancelled.Should().BeFalse();
        result.Status.Should().Be(WorkflowExecutionStatus.Success);
    }
}

/// <summary>
/// Tests that a condition node actually branches.
///
/// It did not. The editor's condition node draws two output handles, "true" and
/// "false" (ConditionNode.tsx) - it is the only node type that names its handles
/// at all. WorkflowMapper carried the handle through as
/// WorkflowConnection.SourceOutput, the engine declared that property and never
/// read it, and the verdict the condition handler returned went into NodeResults
/// where nothing consulted it either.
///
/// So ShouldFollowConnection saw an edge with no Condition, fell through to
/// `return result.Success`, and the condition node had succeeded - meaning BOTH
/// branches ran, every time. Every if/else workflow this product executed took
/// both paths, and nothing reported it: no error, no warning, just two branches
/// of work where one was asked for.
///
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked by organization policy); the first CI run executes these.
/// </summary>
public class VisualWorkflowEngineBranchingTests
{
    /// <summary>A condition node wired to a "true" node and a "false" node.</summary>
    private static VisualWorkflow Branching(out WorkflowNode whenTrue, out WorkflowNode whenFalse)
    {
        var decide = new WorkflowNode { Name = "decide", Type = "condition" };
        whenTrue = new WorkflowNode { Name = "yes", Type = "action", Integration = "t", Action = "run" };
        whenFalse = new WorkflowNode { Name = "no", Type = "action", Integration = "t", Action = "run" };

        return new VisualWorkflow
        {
            Name = "branching",
            Nodes = new List<WorkflowNode> { decide, whenTrue, whenFalse },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = decide.Id, TargetNodeId = whenTrue.Id, SourceOutput = "true" },
                new() { SourceNodeId = decide.Id, TargetNodeId = whenFalse.Id, SourceOutput = "false" },
            },
        };
    }

    private static void SetComparison(WorkflowNode node, object left, object right, string operation)
    {
        node.Parameters["left"] = left;
        node.Parameters["right"] = right;
        node.Parameters["operation"] = operation;
    }

    [Fact]
    public async Task A_true_condition_runs_only_the_true_branch()
    {
        var engine = new VisualWorkflowEngine();
        var ran = new List<string>();
        engine.RegisterNodeHandler("t:run", (node, _) =>
        {
            ran.Add(node.Name);
            return Task.FromResult<object?>(node.Name);
        });

        var workflow = Branching(out var whenTrue, out var whenFalse);
        SetComparison(workflow.Nodes[0], "a", "a", "equals");

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);
        ran.Should().Equal("yes");
        context.NodeResults.Should().ContainKey(whenTrue.Id);
        context.NodeResults.Should().NotContainKey(whenFalse.Id);
    }

    [Fact]
    public async Task A_false_condition_runs_only_the_false_branch()
    {
        var engine = new VisualWorkflowEngine();
        var ran = new List<string>();
        engine.RegisterNodeHandler("t:run", (node, _) =>
        {
            ran.Add(node.Name);
            return Task.FromResult<object?>(node.Name);
        });

        var workflow = Branching(out var whenTrue, out var whenFalse);
        SetComparison(workflow.Nodes[0], "a", "b", "equals");

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);
        // The regression in one line: this used to be ["yes", "no"].
        ran.Should().Equal("no");
        context.NodeResults.Should().NotContainKey(whenTrue.Id);
        context.NodeResults.Should().ContainKey(whenFalse.Id);
    }

    [Theory]
    [InlineData("not_equals", "a", "b", "yes")]
    [InlineData("not_equals", "a", "a", "no")]
    [InlineData("contains", "hello world", "world", "yes")]
    [InlineData("contains", "hello world", "moon", "no")]
    public async Task Each_comparison_picks_a_side(
        string operation, string left, string right, string expected)
    {
        var engine = new VisualWorkflowEngine();
        var ran = new List<string>();
        engine.RegisterNodeHandler("t:run", (node, _) =>
        {
            ran.Add(node.Name);
            return Task.FromResult<object?>(node.Name);
        });

        var workflow = Branching(out _, out _);
        SetComparison(workflow.Nodes[0], left, right, operation);

        await engine.ExecuteAsync(workflow);

        ran.Should().Equal(expected);
    }

    [Fact]
    public async Task Numeric_comparisons_pick_a_side()
    {
        var engine = new VisualWorkflowEngine();
        var ran = new List<string>();
        engine.RegisterNodeHandler("t:run", (node, _) =>
        {
            ran.Add(node.Name);
            return Task.FromResult<object?>(node.Name);
        });

        var workflow = Branching(out _, out _);
        SetComparison(workflow.Nodes[0], 10, 3, "greater_than");

        await engine.ExecuteAsync(workflow);

        ran.Should().Equal("yes");
    }

    [Fact]
    public async Task A_branch_edge_from_a_node_that_gives_no_verdict_fails_loudly()
    {
        // Following such an edge - or its sibling - would be a guess, and a
        // guess here sends work down a path nobody chose. Better to say so.
        var engine = new VisualWorkflowEngine();
        engine.RegisterNodeHandler("t:run", (node, _) => Task.FromResult<object?>(node.Name));

        var source = new WorkflowNode { Name = "not-a-condition", Type = "action", Integration = "t", Action = "run" };
        var target = new WorkflowNode { Name = "downstream", Type = "action", Integration = "t", Action = "run" };
        var workflow = new VisualWorkflow
        {
            Name = "bogus-branch",
            Nodes = new List<WorkflowNode> { source, target },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = source.Id, TargetNodeId = target.Id, SourceOutput = "true" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        context.Error.Should().Contain("condition verdict");
    }

    [Fact]
    public async Task An_unsupported_edge_expression_fails_rather_than_always_firing()
    {
        // `return true` was the old answer, which is the one outcome that looks
        // like it works while ignoring what the user wrote.
        var engine = new VisualWorkflowEngine();
        engine.RegisterNodeHandler("t:run", (node, _) => Task.FromResult<object?>(node.Name));

        var source = new WorkflowNode { Name = "first", Type = "action", Integration = "t", Action = "run" };
        var target = new WorkflowNode { Name = "second", Type = "action", Integration = "t", Action = "run" };
        var workflow = new VisualWorkflow
        {
            Name = "custom-expression",
            Nodes = new List<WorkflowNode> { source, target },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = source.Id, TargetNodeId = target.Id, Condition = "item.amount > 100" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        context.NodeResults.Should().NotContainKey(target.Id);
    }

    [Fact]
    public async Task Default_and_error_routing_still_work()
    {
        // The branch check must not disturb the routing that already existed.
        var engine = new VisualWorkflowEngine();
        engine.RegisterNodeHandler("t:boom", (_, _) => throw new InvalidOperationException("kaboom"));
        engine.RegisterNodeHandler("t:recover", (_, _) => Task.FromResult<object?>("recovered"));

        var failing = new WorkflowNode { Name = "failing", Type = "action", Integration = "t", Action = "boom" };
        var recovery = new WorkflowNode { Name = "recovery", Type = "action", Integration = "t", Action = "recover" };
        var workflow = new VisualWorkflow
        {
            Name = "error-routing",
            Nodes = new List<WorkflowNode> { failing, recovery },
            Connections = new List<WorkflowConnection>
            {
                new() { SourceNodeId = failing.Id, TargetNodeId = recovery.Id, Condition = "error" },
            },
        };

        var context = await engine.ExecuteAsync(workflow);

        context.Status.Should().Be(WorkflowExecutionStatus.Success);
        context.NodeResults[recovery.Id].Success.Should().BeTrue();
    }
}
