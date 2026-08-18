using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Triggers;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// Runs scheduled workflows without anyone pressing a button.
///
/// This is the difference between a workflow RUNNER and workflow AUTOMATION.
/// CronScheduler, EventTrigger, FileWatcherTrigger and TriggerManager have all
/// existed in Loco.Core, but nothing in Loco.Api or Loco.Cli referenced any of
/// them, so a saved workflow only ever ran when a human clicked execute or
/// invoked the CLI.
///
/// A workflow is scheduled by giving a trigger node a `cron` config value:
///
///   { "type": "trigger", "data": { "config": { "cron": "0 9 * * 1-5",
///                                              "timezone": "Asia/Tokyo" } } }
///
/// Timezone handling is CronScheduler's (e75e3dc made it DST-correct), so
/// "9am Tokyo" stays 9am across a DST boundary rather than drifting.
///
/// Deliberately NOT a durable scheduler: schedules are rebuilt from the store on
/// start, and a run missed while the process was down is skipped rather than
/// replayed. Catch-up requires persisting last-fired times, which belongs with
/// the execution-history persistence work (O-2) rather than here - and firing a
/// backlog of missed runs at startup is worse than skipping them.
/// </summary>
public sealed class WorkflowSchedulerService : IHostedService, IDisposable
{
    private readonly IWorkflowStore _store;
    private readonly WorkflowExecutionService _executor;
    private readonly ILogger<WorkflowSchedulerService> _logger;
    private CronScheduler? _scheduler;

    public WorkflowSchedulerService(
        IWorkflowStore store,
        WorkflowExecutionService executor,
        ILogger<WorkflowSchedulerService> logger)
    {
        _store = store;
        _executor = executor;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler = new CronScheduler(_logger);
        _scheduler.OnScheduledExecution += OnScheduledAsync;

        var scheduled = 0;

        // Page through everything once; the store is the source of truth for
        // which workflows have a schedule.
        var (workflows, _) = await _store.GetPageAsync(1, int.MaxValue, cancellationToken);

        foreach (var workflow in workflows)
        {
            var schedule = ReadSchedule(workflow);
            if (schedule is null) continue;

            try
            {
                _scheduler.AddSchedule(workflow.Id, schedule);
                scheduled++;
            }
            catch (Exception ex)
            {
                // One malformed cron expression must not stop every other
                // schedule from being registered.
                _logger.LogWarning(ex,
                    "Skipping schedule for workflow {WorkflowId}: invalid cron expression '{Expression}'",
                    workflow.Id, schedule.Expression);
            }
        }

        _logger.LogInformation("Workflow scheduler started with {Count} scheduled workflow(s)", scheduled);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is not null)
        {
            _scheduler.OnScheduledExecution -= OnScheduledAsync;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads a cron schedule from the workflow's trigger nodes, or null when it
    /// has none. The first trigger node carrying a `cron` value wins.
    /// </summary>
    private static CronSchedule? ReadSchedule(StoredWorkflow workflow)
    {
        foreach (var node in workflow.Nodes)
        {
            if (!string.Equals(node.Type, "trigger", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!node.Data.Config.TryGetValue("cron", out var cronValue))
                continue;

            var expression = cronValue.ValueKind == System.Text.Json.JsonValueKind.String
                ? cronValue.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(expression)) continue;

            var timezone = node.Data.Config.TryGetValue("timezone", out var tz)
                           && tz.ValueKind == System.Text.Json.JsonValueKind.String
                ? tz.GetString() ?? "UTC"
                : "UTC";

            return new CronSchedule
            {
                Expression = expression,
                Timezone = timezone,
                Enabled = true,
            };
        }

        return null;
    }

    private async Task OnScheduledAsync(string workflowId, DateTime firedAt)
    {
        try
        {
            var result = await _executor.StartAsync(workflowId, initialVariables: null, CancellationToken.None);

            if (result is null)
            {
                // The workflow was deleted after its schedule was registered.
                _logger.LogWarning(
                    "Scheduled workflow {WorkflowId} no longer exists; removing its schedule", workflowId);
                _scheduler?.RemoveSchedule(workflowId);
                return;
            }

            _logger.LogInformation(
                "Scheduled run of workflow {WorkflowId} started as execution {ExecutionId} (fired {FiredAt:O})",
                workflowId, result.ExecutionId, firedAt);
        }
        catch (Exception ex)
        {
            // A scheduler callback that throws would take down the timer loop and
            // silently stop every other schedule.
            _logger.LogError(ex, "Scheduled run of workflow {WorkflowId} failed to start", workflowId);
        }
    }

    public void Dispose() => _scheduler?.Dispose();
}
