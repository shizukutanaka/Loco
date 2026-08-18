using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Loco.Api.Contracts;
using Loco.Api.Execution;

namespace Loco.Api.Controllers;

/// <summary>
/// Execution status + cancellation. The Visual Editor has always called
/// GET /api/v1/executions/{id} (useExecutionPolling) and
/// POST /api/v1/executions/{id}/cancel - both 404'd because no such controller
/// existed; the only status route was nested under /workflows and stubbed.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class ExecutionsController : ControllerBase
{
    private readonly ExecutionRegistry _executions;
    private readonly JsonFileExecutionStore _history;
    private readonly ILogger<ExecutionsController> _logger;

    public ExecutionsController(
        ExecutionRegistry executions,
        JsonFileExecutionStore history,
        ILogger<ExecutionsController> logger)
    {
        _executions = executions;
        _history = history;
        _logger = logger;
    }

    /// <summary>Get the current status of an execution (poll target).</summary>
    [HttpGet("{executionId}")]
    [Authorize(Policy = "CanViewWorkflows")]
    public async Task<IActionResult> GetExecution(string executionId, CancellationToken cancellationToken)
    {
        var entry = _executions.Get(executionId);
        if (entry is not null)
        {
            return Ok(Envelope.Ok(ExecutionResponseFactory.Create(entry)));
        }

        // Not in memory: either evicted, or this process did not run it. Finished
        // executions are persisted, so history answers both cases and renders
        // identically to a live one.
        var persisted = await _history.GetAsync(executionId, cancellationToken);
        if (persisted is not null)
        {
            return Ok(Envelope.Ok(ExecutionResponseFactory.Create(persisted)));
        }

        return NotFound(Envelope.Fail("NOT_FOUND", $"Execution '{executionId}' was not found"));
    }

    /// <summary>Request cancellation of a running execution (idempotent).</summary>
    [HttpPost("{executionId}/cancel")]
    [Authorize(Policy = "CanExecuteWorkflows")]
    public IActionResult CancelExecution(string executionId)
    {
        if (!_executions.Cancel(executionId))
        {
            return NotFound(Envelope.Fail("NOT_FOUND", $"Execution '{executionId}' was not found"));
        }

        _logger.LogInformation("Cancellation requested for execution {ExecutionId}", executionId);
        return Ok(Envelope.Ok(message: "Cancellation requested"));
    }
}
