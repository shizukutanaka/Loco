using Loco.Core.Scheduling;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loco.Api.Controllers;

/// <summary>
/// Job scheduling API endpoints
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
public class SchedulingController : ControllerBase
{
    private readonly IJobScheduler _jobScheduler;
    private readonly ILogger<SchedulingController> _logger;

    public SchedulingController(
        IJobScheduler jobScheduler,
        ILogger<SchedulingController> logger)
    {
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    /// <summary>
    /// Schedule a fire-and-forget job
    /// </summary>
    /// <param name="request">Job scheduling request</param>
    /// <returns>Job ID</returns>
    [HttpPost("fire-and-forget")]
    [ProduceResponseType(typeof(ScheduleResponse), StatusCodes.Status201Created)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScheduleFireAndForgetAsync([FromBody] ScheduleJobRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WorkflowId))
                return BadRequest(new { error = "WorkflowId is required" });

            var job = new ScheduledJob
            {
                Name = request.Name ?? $"job-{Guid.NewGuid():N}".Substring(0, 20),
                JobType = "workflow-execution",
                WorkflowId = request.WorkflowId,
                Parameters = request.Parameters ?? new(),
                Priority = request.Priority ?? 0,
                MaxRetries = request.MaxRetries ?? 3
            };

            var jobId = await _jobScheduler.ScheduleFireAndForgetAsync(job);

            _logger.LogInformation(
                "Fire-and-forget job scheduled. JobId: {JobId}, WorkflowId: {WorkflowId}",
                jobId, request.WorkflowId);

            return CreatedAtAction(
                nameof(GetJobAsync),
                new { jobId },
                new ScheduleResponse { JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule fire-and-forget job");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to schedule job" });
        }
    }

    /// <summary>
    /// Schedule a delayed job
    /// </summary>
    /// <param name="request">Delayed job scheduling request</param>
    /// <returns>Job ID</returns>
    [HttpPost("delayed")]
    [ProduceResponseType(typeof(ScheduleResponse), StatusCodes.Status201Created)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScheduleDelayedAsync([FromBody] ScheduleDelayedJobRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WorkflowId))
                return BadRequest(new { error = "WorkflowId is required" });

            if (request.DelaySeconds < 0)
                return BadRequest(new { error = "DelaySeconds must be non-negative" });

            var job = new ScheduledJob
            {
                Name = request.Name ?? $"delayed-job-{Guid.NewGuid():N}".Substring(0, 20),
                JobType = "workflow-execution",
                WorkflowId = request.WorkflowId,
                Parameters = request.Parameters ?? new(),
                Priority = request.Priority ?? 0,
                MaxRetries = request.MaxRetries ?? 3
            };

            var jobId = await _jobScheduler.ScheduleDelayedAsync(
                job,
                TimeSpan.FromSeconds(request.DelaySeconds));

            _logger.LogInformation(
                "Delayed job scheduled. JobId: {JobId}, Delay: {Delay}s",
                jobId, request.DelaySeconds);

            return CreatedAtAction(
                nameof(GetJobAsync),
                new { jobId },
                new ScheduleResponse { JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule delayed job");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to schedule job" });
        }
    }

    /// <summary>
    /// Schedule a recurring job
    /// </summary>
    /// <param name="request">Recurring job scheduling request</param>
    /// <returns>Job ID</returns>
    [HttpPost("recurring")]
    [ProduceResponseType(typeof(ScheduleResponse), StatusCodes.Status201Created)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScheduleRecurringAsync([FromBody] ScheduleRecurringJobRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.WorkflowId))
                return BadRequest(new { error = "WorkflowId is required" });

            if (string.IsNullOrWhiteSpace(request.CronExpression))
                return BadRequest(new { error = "CronExpression is required" });

            var job = new ScheduledJob
            {
                Name = request.Name ?? $"recurring-job-{Guid.NewGuid():N}".Substring(0, 20),
                JobType = "workflow-execution",
                WorkflowId = request.WorkflowId,
                Parameters = request.Parameters ?? new(),
                Priority = request.Priority ?? 0,
                MaxRetries = request.MaxRetries ?? 3
            };

            var jobId = await _jobScheduler.ScheduleRecurringAsync(
                job,
                request.CronExpression);

            _logger.LogInformation(
                "Recurring job scheduled. JobId: {JobId}, Cron: {Cron}",
                jobId, request.CronExpression);

            return CreatedAtAction(
                nameof(GetJobAsync),
                new { jobId },
                new ScheduleResponse { JobId = jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recurring job");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to schedule job" });
        }
    }

    /// <summary>
    /// Get job details
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job details</returns>
    [HttpGet("{jobId}")]
    [ProduceResponseType(typeof(JobDetails), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetJobAsync(string jobId)
    {
        try
        {
            var job = await _jobScheduler.GetJobAsync(jobId);
            if (job == null)
                return NotFound(new { error = "Job not found" });

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job details");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to retrieve job details" });
        }
    }

    /// <summary>
    /// List all jobs
    /// </summary>
    /// <returns>List of jobs</returns>
    [HttpGet]
    [ProduceResponseType(typeof(IEnumerable<JobDetails>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobsAsync()
    {
        try
        {
            var jobs = await _jobScheduler.ListJobsAsync();
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list jobs");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to list jobs" });
        }
    }

    /// <summary>
    /// Delete a job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Success result</returns>
    [HttpDelete("{jobId}")]
    [ProduceResponseType(StatusCodes.Status204NoContent)]
    [ProduceResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJobAsync(string jobId)
    {
        try
        {
            var result = await _jobScheduler.DeleteJobAsync(jobId);
            if (!result)
                return NotFound(new { error = "Job not found" });

            _logger.LogInformation("Job deleted. JobId: {JobId}", jobId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to delete job" });
        }
    }
}

/// <summary>
/// Schedule job request
/// </summary>
public class ScheduleJobRequest
{
    /// <summary>
    /// Job name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Workflow ID to execute
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Job parameters
    /// </summary>
    public Dictionary<string, object?>? Parameters { get; set; }

    /// <summary>
    /// Job priority
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int? MaxRetries { get; set; }
}

/// <summary>
/// Schedule delayed job request
/// </summary>
public class ScheduleDelayedJobRequest : ScheduleJobRequest
{
    /// <summary>
    /// Delay in seconds
    /// </summary>
    public int DelaySeconds { get; set; }
}

/// <summary>
/// Schedule recurring job request
/// </summary>
public class ScheduleRecurringJobRequest : ScheduleJobRequest
{
    /// <summary>
    /// CRON expression
    /// </summary>
    public required string CronExpression { get; set; }
}

/// <summary>
/// Schedule response
/// </summary>
public class ScheduleResponse
{
    /// <summary>
    /// Job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;
}
