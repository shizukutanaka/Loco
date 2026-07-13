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

        var result = new WorkflowValidator().Validate(workflow);

        result.IsValid.Should().BeFalse();
    }
}
