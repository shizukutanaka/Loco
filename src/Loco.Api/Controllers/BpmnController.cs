using Loco.Core.Bpmn;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loco.Api.Controllers;

/// <summary>
/// BPMN workflow management API endpoints
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public class BpmnController : ControllerBase
{
    private readonly IBpmnWorkflowParser _bpmnParser;
    private readonly ILogger<BpmnController> _logger;

    public BpmnController(
        IBpmnWorkflowParser bpmnParser,
        ILogger<BpmnController> logger)
    {
        _bpmnParser = bpmnParser;
        _logger = logger;
    }

    /// <summary>
    /// Parse a BPMN workflow definition
    /// </summary>
    /// <param name="request">BPMN parse request</param>
    /// <returns>Parsed workflow definition</returns>
    [HttpPost("parse")]
    [ProduceResponseType(typeof(BpmnWorkflowDefinition), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseBpmnAsync([FromBody] ParseBpmnRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return BadRequest(new { error = "BpmnXml is required" });

            var definition = await _bpmnParser.ParseAsync(request.BpmnXml);
            if (definition == null)
                return BadRequest(new { error = "Invalid BPMN definition" });

            _logger.LogInformation(
                "BPMN workflow parsed: {WorkflowId}, Elements: {ElementCount}",
                definition.Id, definition.Elements.Count);

            return Ok(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse BPMN workflow");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to parse BPMN workflow" });
        }
    }

    /// <summary>
    /// Validate a BPMN workflow definition
    /// </summary>
    /// <param name="request">Validation request</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate")]
    [ProduceResponseType(typeof(ValidateBpmnResponse), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateBpmnAsync([FromBody] ParseBpmnRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return BadRequest(new { error = "BpmnXml is required" });

            var definition = await _bpmnParser.ParseAsync(request.BpmnXml);
            if (definition == null)
                return BadRequest(new { error = "Invalid BPMN XML" });

            var isValid = await _bpmnParser.ValidateAsync(definition);

            _logger.LogInformation(
                "BPMN workflow validated: {WorkflowId}, Valid: {IsValid}",
                definition.Id, isValid);

            return Ok(new ValidateBpmnResponse
            {
                IsValid = isValid,
                WorkflowId = definition.Id,
                ElementCount = definition.Elements.Count,
                FlowCount = definition.SequenceFlows.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate BPMN workflow");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to validate BPMN workflow" });
        }
    }

    /// <summary>
    /// Execute a BPMN workflow
    /// </summary>
    /// <param name="request">Execution request</param>
    /// <returns>Execution result</returns>
    [HttpPost("execute")]
    [ProduceResponseType(typeof(BpmnExecutionResult), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteBpmnAsync([FromBody] ExecuteBpmnRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return BadRequest(new { error = "BpmnXml is required" });

            var definition = await _bpmnParser.ParseAsync(request.BpmnXml);
            if (definition == null)
                return BadRequest(new { error = "Invalid BPMN definition" });

            var result = await _bpmnParser.ExecuteAsync(
                definition,
                request.Parameters ?? new(),
                HttpContext.RequestAborted);

            _logger.LogInformation(
                "BPMN workflow executed: {WorkflowId}, Success: {Success}, Duration: {Duration}ms",
                definition.Id, result.Success, result.Duration.TotalMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute BPMN workflow");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to execute BPMN workflow" });
        }
    }

    /// <summary>
    /// Get workflow information from BPMN definition
    /// </summary>
    /// <param name="request">Request with BPMN XML</param>
    /// <returns>Workflow information</returns>
    [HttpPost("info")]
    [ProduceResponseType(typeof(BpmnWorkflowInfo), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetWorkflowInfoAsync([FromBody] ParseBpmnRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BpmnXml))
                return BadRequest(new { error = "BpmnXml is required" });

            var definition = await _bpmnParser.ParseAsync(request.BpmnXml);
            if (definition == null)
                return BadRequest(new { error = "Invalid BPMN definition" });

            var info = new BpmnWorkflowInfo
            {
                WorkflowId = definition.Id,
                WorkflowName = definition.Name,
                ElementCount = definition.Elements.Count,
                StartEvents = definition.Elements
                    .Where(e => e.Type == BpmnElementType.StartEvent)
                    .Select(e => e.Name)
                    .ToList(),
                EndEvents = definition.Elements
                    .Where(e => e.Type == BpmnElementType.EndEvent)
                    .Select(e => e.Name)
                    .ToList(),
                Tasks = definition.Elements
                    .Where(e => e.Type == BpmnElementType.Task)
                    .Select(e => e.Name)
                    .ToList(),
                Gateways = definition.Gateways
                    .Select(g => g.Type.ToString())
                    .ToList()
            };

            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow info");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get workflow info" });
        }
    }
}

/// <summary>
/// Parse BPMN request
/// </summary>
public class ParseBpmnRequest
{
    /// <summary>
    /// BPMN XML definition
    /// </summary>
    public required string BpmnXml { get; set; }
}

/// <summary>
/// Execute BPMN request
/// </summary>
public class ExecuteBpmnRequest
{
    /// <summary>
    /// BPMN XML definition
    /// </summary>
    public required string BpmnXml { get; set; }

    /// <summary>
    /// Workflow execution parameters
    /// </summary>
    public Dictionary<string, object?>? Parameters { get; set; }
}

/// <summary>
/// BPMN validation response
/// </summary>
public class ValidateBpmnResponse
{
    /// <summary>
    /// Is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Workflow ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Element count
    /// </summary>
    public int ElementCount { get; set; }

    /// <summary>
    /// Flow count
    /// </summary>
    public int FlowCount { get; set; }
}

/// <summary>
/// BPMN workflow information
/// </summary>
public class BpmnWorkflowInfo
{
    /// <summary>
    /// Workflow ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Workflow name
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Total element count
    /// </summary>
    public int ElementCount { get; set; }

    /// <summary>
    /// Start events
    /// </summary>
    public List<string> StartEvents { get; set; } = new();

    /// <summary>
    /// End events
    /// </summary>
    public List<string> EndEvents { get; set; } = new();

    /// <summary>
    /// Tasks in workflow
    /// </summary>
    public List<string> Tasks { get; set; } = new();

    /// <summary>
    /// Gateway types used
    /// </summary>
    public List<string> Gateways { get; set; } = new();
}
