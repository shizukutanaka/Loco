using System.Text.Json;
using FluentAssertions;
using Loco.Api.Execution;
using Loco.Core.Integrations.Core;
using Loco.Core.Triggers;
using Loco.Core.Workflows;

namespace Loco.Api.Tests;

/// <summary>
/// Tests for the schedule-reading half of WorkflowSchedulerService.
///
/// This decides whether a saved workflow ever runs on its own, and a wrong
/// answer is SILENT: the workflow simply never fires, with nothing to
/// distinguish "not scheduled" from "scheduled but misread". That is exactly
/// the failure mode worth pinning.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class WorkflowSchedulerServiceTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static StoredWorkflow WorkflowWith(
        string nodeType, Dictionary<string, JsonElement> config) => new()
    {
        Id = "wf-1",
        Name = "test",
        Nodes = new List<StoredWorkflowNode>
        {
            new()
            {
                Id = "n1",
                Type = nodeType,
                Position = new StoredPosition { X = 0, Y = 0 },
                Data = new StoredNodeData { Label = "Trigger", Config = config },
            },
        },
        Metadata = new StoredWorkflowMetadata(),
    };

    [Fact]
    public void ReadSchedule_TriggerWithCron_ReturnsIt()
    {
        var workflow = WorkflowWith("trigger", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("\"0 9 * * 1-5\""),
        });

        var schedule = WorkflowSchedulerService.ReadSchedule(workflow);

        schedule.Should().NotBeNull();
        schedule!.Expression.Should().Be("0 9 * * 1-5");
        schedule.Enabled.Should().BeTrue();
        schedule.Timezone.Should().Be("UTC", "UTC is the documented default");
    }

    [Fact]
    public void ReadSchedule_UsesTheDeclaredTimezone()
    {
        var workflow = WorkflowWith("trigger", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("\"0 9 * * *\""),
            ["timezone"] = Json("\"Asia/Tokyo\""),
        });

        WorkflowSchedulerService.ReadSchedule(workflow)!.Timezone.Should().Be("Asia/Tokyo");
    }

    [Fact]
    public void ReadSchedule_NoCron_ReturnsNull()
    {
        // A trigger without a cron value means "run on demand only", not
        // "schedule me with an empty expression".
        var workflow = WorkflowWith("trigger", new Dictionary<string, JsonElement>());

        WorkflowSchedulerService.ReadSchedule(workflow).Should().BeNull();
    }

    [Fact]
    public void ReadSchedule_BlankCron_ReturnsNull()
    {
        var workflow = WorkflowWith("trigger", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("\"   \""),
        });

        WorkflowSchedulerService.ReadSchedule(workflow).Should().BeNull();
    }

    [Fact]
    public void ReadSchedule_CronOnANonTriggerNode_IsIgnored()
    {
        // Only a trigger starts a workflow; a cron value elsewhere is not a
        // schedule and must not silently become one.
        var workflow = WorkflowWith("action", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("\"0 9 * * *\""),
        });

        WorkflowSchedulerService.ReadSchedule(workflow).Should().BeNull();
    }

    [Fact]
    public void ReadSchedule_NonStringCron_IsIgnored()
    {
        var workflow = WorkflowWith("trigger", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("42"),
        });

        WorkflowSchedulerService.ReadSchedule(workflow).Should().BeNull();
    }

    [Fact]
    public void ReadSchedule_MatchesTriggerTypeCaseInsensitively()
    {
        var workflow = WorkflowWith("Trigger", new Dictionary<string, JsonElement>
        {
            ["cron"] = Json("\"* * * * *\""),
        });

        WorkflowSchedulerService.ReadSchedule(workflow).Should().NotBeNull();
    }

    [Fact]
    public void ReadSchedule_FirstTriggerCarryingCronWins()
    {
        var workflow = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "test",
            Nodes = new List<StoredWorkflowNode>
            {
                new()
                {
                    Id = "n1",
                    Type = "trigger",
                    Position = new StoredPosition(),
                    Data = new StoredNodeData { Label = "Manual", Config = new() },
                },
                new()
                {
                    Id = "n2",
                    Type = "trigger",
                    Position = new StoredPosition(),
                    Data = new StoredNodeData
                    {
                        Label = "Nightly",
                        Config = new Dictionary<string, JsonElement>
                        {
                            ["cron"] = Json("\"0 0 * * *\""),
                        },
                    },
                },
            },
            Metadata = new StoredWorkflowMetadata(),
        };

        WorkflowSchedulerService.ReadSchedule(workflow)!.Expression.Should().Be("0 0 * * *",
            "a trigger without cron must not stop a later one from being found");
    }

    [Fact]
    public void ReadSchedule_WorkflowWithNoNodes_ReturnsNull()
    {
        var workflow = new StoredWorkflow { Id = "wf-1", Name = "empty", Metadata = new() };

        WorkflowSchedulerService.ReadSchedule(workflow).Should().BeNull();
    }
}

/// <summary>
/// Tests for reconciling registered schedules against the store.
///
/// Schedules used to be read once, at startup. That made the feature look
/// finished while failing at the only moment a user meets it: set a cron in the
/// editor, save, and nothing ever fires, because the workflow did not exist when
/// the process started. Removing a cron had the mirror problem - the workflow
/// kept running on the old schedule until someone restarted the server.
///
/// Both failures are silent. A schedule that does not fire produces no error, no
/// log line and no execution to look at, so the diff below is the only place the
/// mistake can be caught.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class WorkflowScheduleReconciliationTests
{
    private static Dictionary<string, (string Expression, string Timezone)> Registered(
        params (string Id, string Expression, string Timezone)[] entries) =>
        entries.ToDictionary(e => e.Id, e => (e.Expression, e.Timezone), StringComparer.Ordinal);

    private static Dictionary<string, CronSchedule> Desired(
        params (string Id, string Expression, string Timezone)[] entries) =>
        entries.ToDictionary(
            e => e.Id,
            e => new CronSchedule { Expression = e.Expression, Timezone = e.Timezone, Enabled = true },
            StringComparer.Ordinal);

    [Fact]
    public void Registers_a_workflow_saved_after_the_server_started()
    {
        // The core regression: nothing was registered, the store now has a
        // scheduled workflow, and it must be picked up without a restart.
        var plan = WorkflowSchedulerService.Plan(
            Registered(),
            Desired(("wf-1", "0 9 * * 1-5", "UTC")));

        plan.Add.Should().Equal("wf-1");
        plan.Remove.Should().BeEmpty();
    }

    [Fact]
    public void Drops_a_schedule_the_user_removed()
    {
        var plan = WorkflowSchedulerService.Plan(
            Registered(("wf-1", "0 9 * * 1-5", "UTC")),
            Desired());

        plan.Remove.Should().Equal("wf-1");
        plan.Add.Should().BeEmpty();
    }

    [Fact]
    public void Leaves_an_unchanged_schedule_alone()
    {
        // Re-adding on every sync would work, but it would also log a change
        // every 30 seconds and make a real change impossible to spot.
        var plan = WorkflowSchedulerService.Plan(
            Registered(("wf-1", "0 9 * * 1-5", "UTC")),
            Desired(("wf-1", "0 9 * * 1-5", "UTC")));

        plan.Add.Should().BeEmpty();
        plan.Remove.Should().BeEmpty();
    }

    [Fact]
    public void Re_registers_a_workflow_whose_expression_changed()
    {
        var plan = WorkflowSchedulerService.Plan(
            Registered(("wf-1", "0 9 * * 1-5", "UTC")),
            Desired(("wf-1", "0 17 * * 1-5", "UTC")));

        plan.Add.Should().Equal("wf-1");
        plan.Remove.Should().BeEmpty();
    }

    [Fact]
    public void Re_registers_a_workflow_whose_timezone_changed()
    {
        // "9am UTC" and "9am Tokyo" are different schedules; comparing only the
        // expression would leave the workflow firing nine hours late forever.
        var plan = WorkflowSchedulerService.Plan(
            Registered(("wf-1", "0 9 * * *", "UTC")),
            Desired(("wf-1", "0 9 * * *", "Asia/Tokyo")));

        plan.Add.Should().Equal("wf-1");
    }

    [Fact]
    public void Handles_several_workflows_moving_in_different_directions_at_once()
    {
        var plan = WorkflowSchedulerService.Plan(
            Registered(
                ("unchanged", "0 9 * * *", "UTC"),
                ("changed", "0 9 * * *", "UTC"),
                ("deleted", "0 9 * * *", "UTC")),
            Desired(
                ("unchanged", "0 9 * * *", "UTC"),
                ("changed", "*/5 * * * *", "UTC"),
                ("added", "0 0 * * *", "UTC")));

        plan.Add.Should().BeEquivalentTo(new[] { "changed", "added" });
        plan.Remove.Should().Equal("deleted");
    }

    [Fact]
    public void Reads_only_the_workflows_that_carry_a_cron()
    {
        var scheduled = new StoredWorkflow
        {
            Id = "wf-1",
            Name = "daily",
            Nodes = new List<StoredWorkflowNode>
            {
                new()
                {
                    Id = "n1",
                    Type = "trigger",
                    Position = new StoredPosition { X = 0, Y = 0 },
                    Data = new StoredNodeData
                    {
                        Label = "Trigger",
                        Config = new Dictionary<string, JsonElement>
                        {
                            ["cron"] = JsonDocument.Parse("\"0 9 * * *\"").RootElement,
                        },
                    },
                },
            },
            Metadata = new StoredWorkflowMetadata(),
        };

        var manual = new StoredWorkflow { Id = "wf-2", Name = "manual", Metadata = new() };

        var schedules = WorkflowSchedulerService.ReadSchedules(new[] { scheduled, manual });

        schedules.Keys.Should().Equal("wf-1");
        schedules["wf-1"].Expression.Should().Be("0 9 * * *");
    }
}

/// <summary>
/// Tests for how a workflow's connections are grouped before they are applied
/// (WorkflowCredentialResolver.PlanConnections, shared by the API and the CLI).
///
/// This decides which connector instance each node runs against. It used to be
/// a refusal: ConnectorRegistry caches one instance per connector id and
/// InitializeAsync replaces its configuration, so two Slack nodes on different
/// workspaces both ran against whichever credential was applied last - posting
/// to the wrong workspace with nothing anywhere reporting it, which is why the
/// API declined such workflows rather than guess.
///
/// WorkflowConnectorBridge now keeps one instance per (connector, connection),
/// and the node handler resolves which to use from the node's own CredentialId.
/// So the grouping below is no longer a veto; it is the set of connections to
/// resolve and initialize, and two connections for one connector is an ordinary
/// case that must produce two groups.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class ConnectorConnectionGroupingTests
{
    private static VisualWorkflow WorkflowWith(params (string Integration, string? CredentialId)[] nodes)
    {
        var workflow = new VisualWorkflow { Id = "wf-1", Name = "test" };
        var i = 0;
        foreach (var (integration, credentialId) in nodes)
        {
            workflow.Nodes.Add(new WorkflowNode
            {
                Id = $"n{++i}",
                Name = $"Node {i}",
                Type = "action",
                Integration = integration,
                CredentialId = credentialId,
            });
        }
        return workflow;
    }

    /// <summary>
    /// Calls the real grouping. This used to be a copy of it, which is worth
    /// naming: a test that mirrors the logic it checks passes whatever the
    /// logic does, and would have gone on passing had the two diverged. The
    /// decision now lives in Loco.Core so the API and the CLI share one
    /// implementation, and this asserts against that one.
    /// </summary>
    private static List<(string Integration, string CredentialId)> Groups(VisualWorkflow workflow) =>
        WorkflowCredentialResolver.PlanConnections(workflow)
            .Select(r => (r.Integration, r.CredentialId))
            .OrderBy(k => k.Integration)
            .ThenBy(k => k.CredentialId)
            .ToList();

    [Fact]
    public void Two_connections_for_one_connector_produce_two_groups()
    {
        // The case that was refused outright. Each connection gets its own
        // instance, so both nodes reach the workspace they name.
        var workflow = WorkflowWith(("slack", "conn-a"), ("slack", "conn-b"));

        Groups(workflow).Should().Equal(("slack", "conn-a"), ("slack", "conn-b"));
    }

    [Fact]
    public void The_same_connection_used_twice_is_resolved_once()
    {
        // Two Slack nodes on one workspace is the common case, and resolving it
        // twice would decrypt and re-initialize for no reason.
        var workflow = WorkflowWith(("slack", "conn-a"), ("slack", "conn-a"));

        Groups(workflow).Should().Equal(("slack", "conn-a"));
    }

    [Fact]
    public void Different_connectors_are_grouped_separately()
    {
        var workflow = WorkflowWith(("slack", "conn-a"), ("github", "conn-b"));

        Groups(workflow).Should().Equal(("github", "conn-b"), ("slack", "conn-a"));
    }

    [Fact]
    public void Nodes_without_a_connection_are_ignored()
    {
        // Engine built-ins carry no credential and need nothing resolved.
        var workflow = WorkflowWith(("slack", "conn-a"), ("slack", null), ("transform", null));

        Groups(workflow).Should().Equal(("slack", "conn-a"));
    }

    [Fact]
    public void One_connection_shared_by_two_connectors_stays_two_groups()
    {
        // Nothing stops a connection id being named by nodes of different
        // connectors; each connector still needs its own instance initialized.
        var workflow = WorkflowWith(("slack", "conn-a"), ("github", "conn-a"));

        Groups(workflow).Should().Equal(("github", "conn-a"), ("slack", "conn-a"));
    }
}
