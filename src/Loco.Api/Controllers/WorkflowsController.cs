using Loco.Core.Interfaces;
using Loco.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Loco.Api.Controllers;

/// <summary>
/// Workflow Management API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class WorkflowsController : ControllerBase
{
    private readonly IAutomationEngine _engine;
    private readonly IRuleStore _ruleStore;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        IAutomationEngine engine,
        IRuleStore ruleStore,
        ILogger<WorkflowsController> logger)
    {
        _engine = engine;
        _ruleStore = ruleStore;
        _logger = logger;
    }

    /// <summary>
    /// Get all workflows
    /// </summary>
    /// <param name="skip">Number of records to skip (pagination)</param>
    /// <param name="take">Number of records to take (max 100)</param>
    /// <returns>List of workflows</returns>
    [HttpGet]
    [Authorize(Policy = "CanViewWorkflows")]
    [ProducesResponseType(typeof(PaginatedResponse<WorkflowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<PaginatedResponse<WorkflowDto>>> GetWorkflows(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        _logger.LogInformation("Getting workflows: skip={Skip}, take={Take}", skip, take);

        if (take > 100)
            return BadRequest(new { message = "Maximum take value is 100" });

        try
        {
            // In a real implementation, fetch from database with pagination
            var workflows = new List<WorkflowDto>();

            return Ok(new PaginatedResponse<WorkflowDto>
            {
                Items = workflows,
                Total = workflows.Count,
                Skip = skip,
                Take = take,
                HasMore = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflows");
            throw;
        }
    }

    /// <summary>
    /// Get a specific workflow by ID
    /// </summary>
    /// <param name="id">Workflow identifier</param>
    /// <returns>Workflow details</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "CanViewWorkflows")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<WorkflowDto>> GetWorkflow(string id)
    {
        _logger.LogInformation("Getting workflow: {WorkflowId}", id);

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Workflow ID is required" });

        try
        {
            // In a real implementation, fetch from database
            return NotFound(new { code = "NOT_FOUND", message = $"Workflow '{id}' not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow {WorkflowId}", id);
            throw;
        }
    }

    /// <summary>
    /// Create a new workflow
    /// </summary>
    /// <param name="request">Workflow creation request</param>
    /// <returns>Created workflow with ID</returns>
    [HttpPost]
    [Authorize(Policy = "CanManageWorkflows")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<WorkflowDto>> CreateWorkflow(
        [FromBody] CreateWorkflowRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey = null)
    {
        _logger.LogInformation("Creating workflow: {WorkflowName}", request.Name);

        // Validation
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new
            {
                code = "VALIDATION_FAILED",
                message = "Validation failed",
                errors = new { Name = new[] { "Workflow name is required" } }
            });

        try
        {
            // In a real implementation, create in database
            var workflow = new WorkflowDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Workflow created: {WorkflowId}", workflow.Id);

            return CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            throw;
        }
    }

    /// <summary>
    /// Update an existing workflow
    /// </summary>
    /// <param name="id">Workflow identifier</param>
    /// <param name="request">Update request</param>
    /// <returns>Updated workflow</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    [ProducesResponseType(typeof(WorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<WorkflowDto>> UpdateWorkflow(
        string id,
        [FromBody] UpdateWorkflowRequest request)
    {
        _logger.LogInformation("Updating workflow: {WorkflowId}", id);

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Workflow ID is required" });

        try
        {
            // In a real implementation, update in database
            return NotFound(new { code = "NOT_FOUND", message = $"Workflow '{id}' not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow {WorkflowId}", id);
            throw;
        }
    }

    /// <summary>
    /// Delete a workflow
    /// </summary>
    /// <param name="id">Workflow identifier</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "CanManageWorkflows")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("moderate")]
    public async Task<IActionResult> DeleteWorkflow(string id)
    {
        _logger.LogInformation("Deleting workflow: {WorkflowId}", id);

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Workflow ID is required" });

        try
        {
            // In a real implementation, delete from database
            return NotFound(new { code = "NOT_FOUND", message = $"Workflow '{id}' not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow {WorkflowId}", id);
            throw;
        }
    }

    /// <summary>
    /// Execute a workflow
    /// </summary>
    /// <param name="id">Workflow identifier</param>
    /// <param name="request">Execution request with parameters</param>
    /// <returns>Execution result</returns>
    [HttpPost("{id}/execute")]
    [Authorize(Policy = "CanExecuteWorkflows")]
    [ProducesResponseType(typeof(ExecutionResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<ExecutionResultDto>> ExecuteWorkflow(
        string id,
        [FromBody] ExecuteWorkflowRequest request)
    {
        _logger.LogInformation("Executing workflow: {WorkflowId}", id);

        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Workflow ID is required" });

        try
        {
            var executionId = Guid.NewGuid().ToString();

            // In a real implementation, execute workflow asynchronously
            var result = new ExecutionResultDto
            {
                ExecutionId = executionId,
                WorkflowId = id,
                Status = "Queued",
                StartedAt = DateTime.UtcNow,
                Progress = 0
            };

            _logger.LogInformation("Workflow queued for execution: {ExecutionId}", executionId);

            return Accepted(new { Location = $"/api/v1/executions/{executionId}" }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId}", id);
            throw;
        }
    }

    /// <summary>
    /// Get workflow execution status
    /// </summary>
    /// <param name="id">Workflow identifier</param>
    /// <param name="executionId">Execution identifier</param>
    /// <returns>Execution status</returns>
    [HttpGet("{id}/executions/{executionId}")]
    [Authorize(Policy = "CanViewWorkflows")]
    [ProducesResponseType(typeof(ExecutionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("moderate")]
    public async Task<ActionResult<ExecutionStatusDto>> GetExecutionStatus(
        string id,
        string executionId)
    {
        _logger.LogInformation("Getting execution status: {WorkflowId}/{ExecutionId}", id, executionId);

        try
        {
            // In a real implementation, fetch from database
            return NotFound(new { code = "NOT_FOUND", message = $"Execution '{executionId}' not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution status");
            throw;
        }
    }
}

// Request/Response DTOs
public class CreateWorkflowRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<WorkflowStepRequest>? Steps { get; set; }
}

public class UpdateWorkflowRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<WorkflowStepRequest>? Steps { get; set; }
}

public class ExecuteWorkflowRequest
{
    public Dictionary<string, object>? Parameters { get; set; }
    public bool AsyncExecution { get; set; } = true;
}

public class WorkflowStepRequest
{
    public int Order { get; set; }
    public string Type { get; set; } = "";
    public string ActionName { get; set; } = "";
    public Dictionary<string, object>? Configuration { get; set; }
}

public class WorkflowDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<WorkflowStepDto>? Steps { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkflowStepDto
{
    public string Id { get; set; } = "";
    public int Order { get; set; }
    public string Type { get; set; } = "";
    public string ActionName { get; set; } = "";
}

public class ExecutionResultDto
{
    public string ExecutionId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Progress { get; set; }
    public object? Result { get; set; }
}

public class ExecutionStatusDto
{
    public string ExecutionId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Progress { get; set; }
    public List<StepExecutionDto>? StepExecutions { get; set; }
}

public class StepExecutionDto
{
    public string StepId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public object? Result { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore { get; set; }
}
