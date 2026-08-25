using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Triggers;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// Runs scheduled workflows without anyone pressing a button.
///
/// This is the difference between a workflow RUNNER and workflow AUTOMATION.
/// CronScheduler existed in Loco.Core with nothing in Loco.Api or Loco.Cli
/// referencing it, so a saved workflow only ever ran when a human clicked
/// execute or invoked the CLI. Its unreferenced siblings - EventTrigger,
/// FileWatcherTrigger and TriggerManager - were deleted rather than wired,
/// along with two other webhook implementations that nothing could reach.
///
/// A workflow is scheduled by giving a trigger node a `cron` config value:
///
///   { "type": "trigger", "data": { "config": { "cron": "0 9 * * 1-5",
///                                              "timezone": "Asia/Tokyo" } } }
///
/// Timezone handling is CronScheduler's (e75e3dc made it DST-correct), so
/// "9am Tokyo" stays 9am across a DST boundary rather than drifting.
///
/// Schedules are RECONCILED against the store, not read once. Reading once at
/// startup made the feature look finished while failing at the only moment a
/// user meets it: set a cron in the editor, save, and nothing ever fires,
/// because the workflow did not exist when the process started. Removing a cron
/// had the mirror problem - the workflow kept running on the old schedule until
/// someone restarted the server. Neither reports anything; a schedule that does
/// not fire produces no error, no log line, and no execution to look at.
///
/// Reconciling against the store rather than being told about each save also
/// covers the paths a controller hook would miss: the CLI writing the same file,
/// or a second instance sharing the data directory.
///
/// Deliberately NOT a durable scheduler: a run missed while the process was down
/// is skipped rather than replayed. Catch-up requires persisting last-fired
/// times, and firing a backlog of missed runs at startup is worse than skipping
/// them.
/// </summary>
public sealed class WorkflowSchedulerService : IHostedService, IDisposable
{
    /// <summary>
    /// How often the registered schedules are reconciled against the store.
    ///
    /// Cron's finest granularity is a minute and CronScheduler itself ticks
    /// every 30 seconds, so a shorter interval could not make a workflow fire
    /// any sooner - it would only re-read the store more often.
    /// </summary>
    internal static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(30);

    private readonly IWorkflowStore _store;
    private readonly WorkflowExecutionService _executor;
    private readonly ILogger<WorkflowSchedulerService> _logger;

    /// <summary>
    /// What is currently registered, so a sync can tell an unchanged schedule
    /// from a changed one and log only what actually moved.
    /// </summary>
    private readonly Dictionary<string, (string Expression, string Timezone)> _registered =
        new(StringComparer.Ordinal);

    private readonly object _registeredLock = new();

    private CronScheduler? _scheduler;
    private CancellationTokenSource? _syncCts;
    private Task? _syncLoop;

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

        var scheduled = await SyncAsync(cancellationToken);
        _logger.LogInformation("Workflow scheduler started with {Count} scheduled workflow(s)", scheduled);

        _syncCts = new CancellationTokenSource();
        _syncLoop = RunSyncLoopAsync(_syncCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is not null)
        {
            _scheduler.OnScheduledExecution -= OnScheduledAsync;
        }

        _syncCts?.Cancel();

        if (_syncLoop is not null)
        {
            // Awaited so shutdown does not race a sync that is mid-read.
            try { await _syncLoop; }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SyncInterval);

        while (await SafeWaitAsync(timer, cancellationToken))
        {
            try
            {
                await SyncAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A store that is briefly unreadable must not end the loop and
                // silently stop every schedule for the rest of the process.
                _logger.LogWarning(ex, "Could not reconcile workflow schedules; will retry");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    /// <summary>
    /// Makes the registered schedules match the store: adds workflows that
    /// gained a cron, re-registers ones whose expression or timezone changed,
    /// and drops ones that lost their schedule or were deleted.
    /// </summary>
    /// <returns>How many workflows are scheduled after the sync.</returns>
    internal async Task<int> SyncAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is null) return 0;

        // Page through everything; the store is the source of truth for which
        // workflows have a schedule.
        var (workflows, _) = await _store.GetPageAsync(1, int.MaxValue, cancellationToken);

        var desired = ReadSchedules(workflows);

        // _registered is also touched from the scheduler's timer thread when a
        // fired workflow turns out to be gone, so every mutation is inside the
        // lock. The store read above deliberately stays outside it.
        lock (_registeredLock)
        {
            return Reconcile(desired);
        }
    }

    /// <summary>Every workflow in the store that carries a cron, by id.</summary>
    internal static Dictionary<string, CronSchedule> ReadSchedules(
        IEnumerable<StoredWorkflow> workflows)
    {
        var desired = new Dictionary<string, CronSchedule>(StringComparer.Ordinal);

        foreach (var workflow in workflows)
        {
            var schedule = ReadSchedule(workflow);
            if (schedule is not null) desired[workflow.Id] = schedule;
        }

        return desired;
    }

    /// <summary>
    /// What has to change for the registered schedules to match the store.
    ///
    /// Separated from applying it because this is the part that can be wrong in
    /// a way nothing reports: a workflow left out of <see cref="Add"/> silently
    /// never runs, and one left out of <see cref="Remove"/> silently keeps
    /// running on a schedule the user deleted.
    /// </summary>
    internal readonly record struct SchedulePlan(
        IReadOnlyList<string> Remove, IReadOnlyList<string> Add);

    /// <summary>
    /// Diffs what is registered against what the store asks for. A schedule
    /// counts as changed - and so needs re-adding - when either its expression
    /// or its timezone moved; "9am UTC" and "9am Tokyo" are different schedules.
    /// </summary>
    internal static SchedulePlan Plan(
        IReadOnlyDictionary<string, (string Expression, string Timezone)> registered,
        IReadOnlyDictionary<string, CronSchedule> desired) => new(
            registered.Keys.Where(id => !desired.ContainsKey(id)).ToList(),
            desired
                .Where(kv => !registered.TryGetValue(kv.Key, out var current)
                             || current != (kv.Value.Expression, kv.Value.Timezone))
                .Select(kv => kv.Key)
                .ToList());

    private int Reconcile(Dictionary<string, CronSchedule> desired)
    {
        if (_scheduler is null) return 0;

        var plan = Plan(_registered, desired);

        foreach (var workflowId in plan.Remove)
        {
            _scheduler.RemoveSchedule(workflowId);
            _registered.Remove(workflowId);
            _logger.LogInformation(
                "Workflow {WorkflowId} is no longer scheduled; removed its schedule", workflowId);
        }

        foreach (var workflowId in plan.Add)
        {
            var schedule = desired[workflowId];

            try
            {
                _scheduler.AddSchedule(workflowId, schedule);
                _registered[workflowId] = (schedule.Expression, schedule.Timezone);
            }
            catch (Exception ex)
            {
                // One malformed cron expression must not stop every other
                // schedule from being registered. Any previous schedule for
                // this workflow is dropped rather than left running: an edit
                // that breaks the expression must not keep firing the old one.
                // It is also not recorded as registered, so a later correction
                // is picked up rather than treated as unchanged.
                _scheduler.RemoveSchedule(workflowId);
                _registered.Remove(workflowId);
                _logger.LogWarning(ex,
                    "Skipping schedule for workflow {WorkflowId}: invalid cron expression '{Expression}'",
                    workflowId, schedule.Expression);
            }
        }

        return _registered.Count;
    }

    /// <summary>
    /// Reads a cron schedule from the workflow's trigger nodes, or null when it
    /// has none. The first trigger node carrying a `cron` value wins.
    /// </summary>
    /// <remarks>
    /// Internal rather than private so it can be tested directly. This decides
    /// whether a workflow runs on its own at all, and getting it wrong is silent:
    /// the workflow simply never fires.
    /// </remarks>
    internal static CronSchedule? ReadSchedule(StoredWorkflow workflow)
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
                // Forgotten here too, or a workflow recreated under the same id
                // before the next sync would look unchanged and never be
                // registered again.
                lock (_registeredLock)
                {
                    _registered.Remove(workflowId);
                }
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

    public void Dispose()
    {
        _syncCts?.Cancel();
        _syncCts?.Dispose();
        _scheduler?.Dispose();
    }
}
