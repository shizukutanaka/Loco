using System.Text.Json;
using FluentAssertions;
using Loco.Api.Execution;
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
/// NOTE: authored in an environment where dotnet test could not run (NuGet
/// egress blocked by organization policy); the first CI run executes these.
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
