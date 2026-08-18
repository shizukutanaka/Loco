using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Loco.Api.Contracts;
using Loco.Api.Execution;
using Loco.Core.Integrations.Core;
using Loco.Core.Interfaces;
using Loco.Core.Storage;
using Loco.Core.Workflows;
using VisualValidator = Loco.Core.Workflows.VisualWorkflowValidator;

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
    private readonly WorkflowExecutionService _executor;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        IWorkflowStore store,
        WorkflowExecutionService executor,
        ILogger<WorkflowsController> logger)
    {
        _store = store;
        _executor = executor;
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

        var initialVariables = request?.Input?
            .ToDictionary(kv => kv.Key, kv => WorkflowMapper.ToPlainObject(kv.Value));

        // Credential resolution, connector initialization and registry
        // bookkeeping all live in WorkflowExecutionService so that a scheduled
        // run takes exactly the same path as this one.
        var result = await _executor.StartAsync(id, initialVariables, cancellationToken);

        if (result is null)
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Workflow '{id}' was not found"));
        }

        if (!result.Started)
        {
            return result.Failure switch
            {
                WorkflowExecutionService.StartFailure.MissingCredentials => BadRequest(Envelope.Fail(
                    "MISSING_CREDENTIALS",
                    "One or more nodes reference a connection that is not available",
                    new Dictionary<string, object> { ["errors"] = result.Errors })),
                _ => BadRequest(Envelope.Fail(
                    "INVALID_WORKFLOW",
                    "Workflow failed validation",
                    new Dictionary<string, object> { ["errors"] = result.Errors })),
            };
        }

        var entry = result.Entry!;

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
