using Loco.Core.Integrations.Core;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Workflows;

namespace Loco.Api.Execution;

/// <summary>
/// The single path from "a stored workflow" to "a running execution".
///
/// Extracted from WorkflowsController so the scheduler can start a workflow the
/// same way an HTTP caller does. Without this the two would drift: a scheduled
/// run that skipped credential resolution, for instance, would fail on a null
/// HttpClient exactly the way manual runs used to.
/// </summary>
public sealed class WorkflowExecutionService
{
    private readonly IWorkflowStore _store;
    private readonly VisualWorkflowEngine _engine;
    private readonly ExecutionRegistry _executions;
    private readonly JsonFileConnectionStore _connections;
    private readonly WorkflowConnectorBridge _bridge;
    private readonly ILogger<WorkflowExecutionService> _logger;

    public WorkflowExecutionService(
        IWorkflowStore store,
        VisualWorkflowEngine engine,
        ExecutionRegistry executions,
        JsonFileConnectionStore connections,
        WorkflowConnectorBridge bridge,
        ILogger<WorkflowExecutionService> logger)
    {
        _store = store;
        _engine = engine;
        _executions = executions;
        _connections = connections;
        _bridge = bridge;
        _logger = logger;
    }

    /// <summary>Why a start attempt did not produce a running execution.</summary>
    public enum StartFailure
    {
        NotFound,
        ValidationFailed,
        MissingCredentials,
    }

    /// <summary>
    /// Outcome of a start attempt. Exactly one of <see cref="Entry"/> or
    /// <see cref="Failure"/> is set.
    /// </summary>
    public sealed record StartResult(
        ExecutionRegistry.Entry? Entry,
        StartFailure? Failure,
        IReadOnlyList<string> Errors)
    {
        public string ExecutionId => Entry?.ExecutionId ?? "";
        public bool Started => Entry is not null;

        public static StartResult Ok(ExecutionRegistry.Entry entry) =>
            new(entry, null, Array.Empty<string>());

        public static StartResult Fail(StartFailure failure, IReadOnlyList<string> errors) =>
            new(null, failure, errors);
    }

    /// <summary>
    /// Resolves credentials, initializes the connectors the workflow uses, and
    /// starts it. Returns null when the workflow does not exist, so a caller
    /// whose schedule outlived the workflow can react.
    /// </summary>
    public async Task<StartResult?> StartAsync(
        string workflowId,
        Dictionary<string, object>? initialVariables,
        CancellationToken cancellationToken)
    {
        var stored = await _store.GetAsync(workflowId, cancellationToken);
        if (stored is null) return null;

        var visual = WorkflowMapper.ToVisualWorkflow(stored);

        var validation = new VisualWorkflowValidator().Validate(visual);
        if (!validation.IsValid)
        {
            return StartResult.Fail(StartFailure.ValidationFailed, validation.Errors.ToList());
        }

        var missing = await ConfigureConnectorsAsync(visual, cancellationToken);
        if (missing.Count > 0)
        {
            return StartResult.Fail(StartFailure.MissingCredentials, missing);
        }

        return StartResult.Ok(StartExecution(stored, visual, initialVariables));
    }

    /// <summary>
    /// Initializes every connector this workflow references with its stored
    /// credentials, returning a description of each unresolvable reference.
    ///
    /// This is the step that was missing entirely: ConfigureConnector had no
    /// caller, so connectors executed uninitialized and every action failed on a
    /// null HttpClient.
    /// </summary>
    public async Task<List<string>> ConfigureConnectorsAsync(
        VisualWorkflow visual, CancellationToken cancellationToken)
    {
        var problems = new List<string>();

        var credentialed = visual.Nodes
            .Where(n => !string.IsNullOrEmpty(n.CredentialId) && !string.IsNullOrEmpty(n.Integration))
            .ToList();

        // One connector instance per (connector, connection), so two Slack nodes
        // on different workspaces stay independent.
        //
        // This used to be refused outright. ConnectorRegistry caches a single
        // instance per connector id and InitializeAsync replaces its
        // configuration, so both nodes ran against whichever credential was
        // applied last - posting to the wrong workspace with nothing reporting
        // it. Refusing was the honest stopgap; WorkflowConnectorBridge now keys
        // instances by connection, and the node handler resolves which one to
        // use from the node's own CredentialId at execution time.
        foreach (var group in credentialed
                     .GroupBy(n => (n.Integration, CredentialId: n.CredentialId!)))
        {
            var config = await _connections.BuildConfigurationAsync(group.Key.CredentialId, cancellationToken);

            if (config is null)
            {
                problems.Add(
                    $"node '{group.First().Name}' references connection " +
                    $"'{group.Key.CredentialId}', which does not exist");
                continue;
            }

            await _bridge.ConfigureConnectionAsync(
                group.Key.Integration, group.Key.CredentialId, config, cancellationToken);
        }

        return problems;
    }

    /// <summary>
    /// Starts the run and registers it. The execution outlives the request that
    /// began it, so its lifetime is tied to a dedicated CancellationTokenSource
    /// rather than to any caller's token.
    /// </summary>
    public ExecutionRegistry.Entry StartExecution(
        StoredWorkflow stored,
        VisualWorkflow visual,
        Dictionary<string, object>? initialVariables)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var cts = new CancellationTokenSource();

        var context = new WorkflowExecutionContext
        {
            ExecutionId = executionId,
            WorkflowId = stored.Id,
            Status = WorkflowExecutionStatus.Running,
        };

        var completion = Task.Run(async () =>
        {
            var resultContext = await _engine.ExecuteAsync(visual, initialVariables, cts.Token);
            // The engine builds its own context; copy the outcome onto the one the
            // registry exposes so pollers observe the terminal state.
            context.Status = resultContext.Status;
            context.Error = resultContext.Error;
            context.EndTime = resultContext.EndTime;
            context.NodeResults = resultContext.NodeResults;
            context.Variables = resultContext.Variables;
            context.ExecutionLog = resultContext.ExecutionLog;
        }, CancellationToken.None);

        var entry = new ExecutionRegistry.Entry(
            executionId, stored.Id, DateTime.UtcNow, context, cts, completion);
        _executions.Register(entry);

        _logger.LogInformation(
            "Started execution {ExecutionId} of workflow {WorkflowId}", executionId, stored.Id);

        return entry;
    }
}
