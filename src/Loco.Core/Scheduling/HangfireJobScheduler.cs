using Hangfire;
using Microsoft.Extensions.Logging;
using Loco.Core.Workflows;

namespace Loco.Core.Scheduling;

/// <summary>
/// Hangfire-based job scheduler implementation
/// </summary>
public class HangfireJobScheduler : IJobScheduler
{
    private readonly ILogger<HangfireJobScheduler> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireJobScheduler(
        ILogger<HangfireJobScheduler> logger,
        IRecurringJobManager recurringJobManager,
        IBackgroundJobClient backgroundJobClient)
    {
        _logger = logger;
        _recurringJobManager = recurringJobManager;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc />
    public async Task<string> ScheduleFireAndForgetAsync(ScheduledJob job)
    {
        try
        {
            var jobId = _backgroundJobClient.Enqueue<WorkflowJobExecutor>(x =>
                x.ExecuteWorkflowAsync(job.WorkflowId, job.Parameters, default));

            _logger.LogInformation(
                "Fire-and-forget job scheduled. JobId: {JobId}, WorkflowId: {WorkflowId}",
                jobId, job.WorkflowId);

            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule fire-and-forget job: {JobName}", job.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> ScheduleDelayedAsync(ScheduledJob job, TimeSpan delay)
    {
        try
        {
            var jobId = _backgroundJobClient.Schedule<WorkflowJobExecutor>(x =>
                x.ExecuteWorkflowAsync(job.WorkflowId, job.Parameters, default),
                delay);

            _logger.LogInformation(
                "Delayed job scheduled. JobId: {JobId}, WorkflowId: {WorkflowId}, Delay: {Delay}",
                jobId, job.WorkflowId, delay);

            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule delayed job: {JobName}", job.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> ScheduleRecurringAsync(ScheduledJob job, string cronExpression)
    {
        try
        {
            var jobId = $"{job.WorkflowId}-{Guid.NewGuid():N}".Substring(0, 50);

            _recurringJobManager.AddOrUpdate(
                jobId,
                () => new WorkflowJobExecutor(null!, null!).ExecuteWorkflowAsync(
                    job.WorkflowId, job.Parameters, CancellationToken.None),
                cronExpression);

            _logger.LogInformation(
                "Recurring job scheduled. JobId: {JobId}, WorkflowId: {WorkflowId}, Cron: {Cron}",
                jobId, job.WorkflowId, cronExpression);

            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recurring job: {JobName}", job.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<JobDetails?> GetJobAsync(string jobId)
    {
        try
        {
            // Note: Hangfire API limitations - full job details require more complex querying
            // This is a simplified implementation
            _logger.LogDebug("Retrieved job details for JobId: {JobId}", jobId);
            return new JobDetails
            {
                Id = jobId,
                State = JobState.Processing,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job details for JobId: {JobId}", jobId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteJobAsync(string jobId)
    {
        try
        {
            var result = _backgroundJobClient.Delete(jobId);
            if (result)
            {
                _logger.LogInformation("Job deleted. JobId: {JobId}", jobId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job: {JobId}", jobId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<JobDetails>> ListJobsAsync()
    {
        try
        {
            // Simplified implementation - would need storage access for full details
            _logger.LogDebug("Listing all scheduled jobs");
            return new List<JobDetails>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list jobs");
            return new List<JobDetails>();
        }
    }
}

/// <summary>
/// Workflow job executor for Hangfire
/// </summary>
public class WorkflowJobExecutor
{
    private readonly VisualWorkflowEngine _engine;
    private readonly ILogger<WorkflowJobExecutor> _logger;

    public WorkflowJobExecutor(VisualWorkflowEngine engine, ILogger<WorkflowJobExecutor> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Executes a workflow as a background job
    /// </summary>
    public async Task ExecuteWorkflowAsync(
        string workflowId,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting workflow execution. WorkflowId: {WorkflowId}", workflowId);

            // Execute workflow through automation engine
            var workflow = new VisualWorkflow { Id = workflowId, Name = workflowId };
            var result = await _engine.ExecuteAsync(workflow, parameters, cancellationToken);

            _logger.LogInformation(
                "Workflow execution completed. WorkflowId: {WorkflowId}, Success: {Success}",
                workflowId, result.Status == WorkflowExecutionStatus.Success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed. WorkflowId: {WorkflowId}", workflowId);
            throw;
        }
    }
}
