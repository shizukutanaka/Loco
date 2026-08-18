using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Loco.Api.Contracts;
using Loco.Api.Execution;
using Loco.Core.Integrations.Core;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Workflows;
using VisualValidator = Loco.Core.Workflows.WorkflowValidator;

namespace Loco.Api.Controllers;

/// <summary>
/// Workflow CRUD + execution + validation, implementing exactly the contract the
/// Visual Editor's api client already speaks (src/Loco.VisualEditor/src/api/):
/// envelope responses, camelCase, page/pageSize pagination, and the editor's own
/// Workflow JSON shape stored losslessly.
///
/// Every action here previously returned "In a real implementation..." stub data
/// with no persistence at all; workflows now live in IWorkflowStore and execute
/// on the connector-enabled VisualWorkflowEngine.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowStore _store;
    private readonly VisualWorkflowEngine _engine;
    private readonly ExecutionRegistry _executions;
    private readonly JsonFileConnectionStore _connections;
    private readonly WorkflowConnectorBridge _bridge;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        IWorkflowStore store,
        VisualWorkflowEngine engine,
        ExecutionRegistry executions,
        JsonFileConnectionStore connections,
        WorkflowConnectorBridge bridge,
        ILogger<WorkflowsController> logger)
    {
        _store = store;
        _engine = engine;
        _executions = executions;
        _connections = connections;
        _bridge = bridge;
        _logger = logger;
    }

    /// <summary>List workflows (page/pageSize, newest-updated first).</summary>
    [HttpGet]
    [Authorize(Policy = "CanViewWorkflows")]
    public async Task<IActionResult> GetWorkflows(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _store.GetPageAsync(page, pageSize, cancellationToken);

        return Ok(Envelope.Ok(new
        {
            workflows = items,
            total,
            page,
            pageSize,
        }));
    }

    /// <summary>Get one workflow by id.</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanViewWorkflows")]
    public async Task<IActionResult> GetWorkflow(string id, CancellationToken cancellationToken)
    {
        var workflow = await _store.GetAsync(id, cancellationToken);
        if (workflow is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Workflow '{id}' was not found"));
        }

        return Ok(Envelope.Ok(workflow));
    }

    /// <summary>Create a workflow. The server assigns the id and timestamps.</summary>
    [HttpPost]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> CreateWorkflow(
        [FromBody] WorkflowCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(Envelope.Fail("INVALID_ARGUMENT", "Workflow name is required"));
        }

        var now = DateTime.UtcNow.ToString("O");
        var workflow = new StoredWorkflow
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            Description = request.Description,
            Nodes = request.Nodes ?? new List<StoredWorkflowNode>(),
            Edges = request.Edges ?? new List<StoredWorkflowEdge>(),
            Metadata = request.Metadata ?? new StoredWorkflowMetadata(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _store.UpsertAsync(workflow, cancellationToken);
        _logger.LogInformation("Created workflow {WorkflowId} ({Name})", workflow.Id, workflow.Name);

        return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, Envelope.Ok(workflow));
    }

    /// <summary>Update an existing workflow (partial: only supplied fields change).</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> UpdateWorkflow(
        string id, [FromBody] WorkflowUpdateRequest request, CancellationToken cancellationToken)
    {
        var existing = await _store.GetAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Workflow '{id}' was not found"));
        }

        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.Nodes is not null) existing.Nodes = request.Nodes;
        if (request.Edges is not null) existing.Edges = request.Edges;
        if (request.Metadata is not null) existing.Metadata = request.Metadata;
        existing.UpdatedAt = DateTime.UtcNow.ToString("O");

        await _store.UpsertAsync(existing, cancellationToken);
        _logger.LogInformation("Updated workflow {WorkflowId}", id);

        return Ok(Envelope.Ok(existing));
    }

    /// <summary>Delete a workflow.</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    public async Task<IActionResult> DeleteWorkflow(string id, CancellationToken cancellationToken)
    {
        var removed = await _store.DeleteAsync(id, cancellationToken);
        if (!removed)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Workflow '{id}' was not found"));
        }

        _logger.LogInformation("Deleted workflow {WorkflowId}", id);
        return Ok(Envelope.Ok(message: "Workflow deleted"));
    }

    /// <summary>
    /// Start executing a workflow. Returns immediately with a pending/running
    /// execution the client polls via GET /api/v1/executions/{executionId}.
    /// </summary>
    [HttpPost("{id}/execute")]
    [Authorize(Policy = "CanExecuteWorkflows")]
    public async Task<IActionResult> ExecuteWorkflow(
        string id, [FromBody] ExecuteRequest? request, CancellationToken cancellationToken)
    {
        var stored = await _store.GetAsync(id, cancellationToken);
        if (stored is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Workflow '{id}' was not found"));
        }

        var visual = WorkflowMapper.ToVisualWorkflow(stored);

        var validation = new VisualValidator().Validate(visual);
        if (!validation.IsValid)
        {
            return BadRequest(Envelope.Fail(
                "INVALID_WORKFLOW",
                "Workflow failed validation",
                new Dictionary<string, object> { ["errors"] = validation.Errors }));
        }

        if (request?.DryRun == true)
        {
            // The ExecuteRequest.DryRun field has existed since this controller was
            // written, but nothing ever checked it - a caller passing dryRun:true
            // expecting a safe preview would silently get a REAL execution (every
            // connector action actually invoked) instead. Short-circuit before ever
            // touching the engine/ExecutionRegistry/connectors: report the validated
            // node plan and stop, exactly what "dry run" promises.
            var dryRunId = Guid.NewGuid().ToString("N");
            var dryRunAt = DateTime.UtcNow.ToString("O");
            var plannedNodes = visual.Nodes.Select(n => new
            {
                nodeId = n.Id,
                name = n.Name,
                integration = n.Integration,
                action = n.Action,
            }).ToList();

            _logger.LogInformation(
                "Dry run for workflow {WorkflowId}: {NodeCount} node(s) planned, no connectors invoked",
                id, plannedNodes.Count);

            return Ok(Envelope.Ok(new
            {
                executionId = dryRunId,
                status = "completed",
                startedAt = dryRunAt,
                completedAt = dryRunAt,
                output = new { dryRun = true, plannedNodes },
                logs = new[]
                {
                    new
                    {
                        timestamp = dryRunAt,
                        level = "info",
                        message = $"Dry run: {plannedNodes.Count} node(s) validated; no connector actions were invoked.",
                    },
                },
            }));
        }

        // Initialize every connector this workflow uses with its stored
        // credentials BEFORE execution starts.
        //
        // This is the step that was missing entirely: WorkflowConnectorBridge.
        // ConfigureConnector had no caller anywhere in the codebase, so connectors
        // were registered as node handlers but never initialized - all 28 of them
        // failed at execution with a null HttpClient. A node referencing a
        // connection that no longer exists is reported here rather than failing
        // opaquely mid-run.
        var missingCredentials = new List<string>();

        foreach (var group in visual.Nodes
                     .Where(n => !string.IsNullOrEmpty(n.CredentialId) && !string.IsNullOrEmpty(n.Integration))
                     .GroupBy(n => (n.Integration, n.CredentialId!)))
        {
            var (integration, credentialId) = group.Key;
            var config = await _connections.BuildConfigurationAsync(credentialId, cancellationToken);

            if (config is null)
            {
                missingCredentials.Add(
                    $"node '{group.First().Name}' references connection '{credentialId}', which does not exist");
                continue;
            }

            await _bridge.ConfigureConnectorAsync(integration, config, cancellationToken);
        }

        if (missingCredentials.Count > 0)
        {
            return BadRequest(Envelope.Fail(
                "MISSING_CREDENTIALS",
                "One or more nodes reference a connection that is not available",
                new Dictionary<string, object> { ["errors"] = missingCredentials }));
        }

        var initialVariables = request?.Input?
            .ToDictionary(kv => kv.Key, kv => WorkflowMapper.ToPlainObject(kv.Value));

        var executionId = Guid.NewGuid().ToString("N");
        // The execution outlives this HTTP request - tie its lifetime to the app,
        // not to the request's cancellation token.
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
            "Started execution {ExecutionId} of workflow {WorkflowId}", executionId, id);

        return Accepted(Envelope.Ok(ExecutionResponseFactory.Create(entry)));
    }

    /// <summary>
    /// Validate a workflow definition without executing it (the editor's
    /// ValidationPanel calls this; the endpoint previously did not exist).
    /// </summary>
    [HttpPost("validate")]
    [Authorize(Policy = "CanViewWorkflows")]
    public IActionResult ValidateWorkflow([FromBody] StoredWorkflow workflow)
    {
        var visual = WorkflowMapper.ToVisualWorkflow(workflow);
        var result = new VisualValidator().Validate(visual);

        return Ok(Envelope.Ok(new
        {
            valid = result.IsValid,
            errors = result.Errors,
        }));
    }
}

/// <summary>Mirrors the frontend's WorkflowCreateRequest.</summary>
public class WorkflowCreateRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<StoredWorkflowNode>? Nodes { get; set; }
    public List<StoredWorkflowEdge>? Edges { get; set; }
    public StoredWorkflowMetadata? Metadata { get; set; }
}

/// <summary>Mirrors the frontend's WorkflowUpdateRequest (all fields optional).</summary>
public class WorkflowUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<StoredWorkflowNode>? Nodes { get; set; }
    public List<StoredWorkflowEdge>? Edges { get; set; }
    public StoredWorkflowMetadata? Metadata { get; set; }
}

/// <summary>Body of POST {id}/execute: { input?, dryRun? }.</summary>
public class ExecuteRequest
{
    public Dictionary<string, JsonElement>? Input { get; set; }
    public bool DryRun { get; set; }
}
