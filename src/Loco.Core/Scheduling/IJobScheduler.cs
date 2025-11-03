namespace Loco.Core.Scheduling;

/// <summary>
/// Interface for job scheduling and management
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Schedules a fire-and-forget job
    /// </summary>
    /// <param name="job">The job to schedule</param>
    /// <returns>The job ID</returns>
    Task<string> ScheduleFireAndForgetAsync(ScheduledJob job);

    /// <summary>
    /// Schedules a delayed job
    /// </summary>
    /// <param name="job">The job to schedule</param>
    /// <param name="delay">Delay before execution</param>
    /// <returns>The job ID</returns>
    Task<string> ScheduleDelayedAsync(ScheduledJob job, TimeSpan delay);

    /// <summary>
    /// Schedules a recurring job
    /// </summary>
    /// <param name="job">The job to schedule</param>
    /// <param name="cronExpression">CRON expression for schedule</param>
    /// <returns>The job ID</returns>
    Task<string> ScheduleRecurringAsync(ScheduledJob job, string cronExpression);

    /// <summary>
    /// Gets job details
    /// </summary>
    /// <param name="jobId">The job ID</param>
    /// <returns>Job details</returns>
    Task<JobDetails?> GetJobAsync(string jobId);

    /// <summary>
    /// Deletes a job
    /// </summary>
    /// <param name="jobId">The job ID</param>
    /// <returns>True if deleted, false otherwise</returns>
    Task<bool> DeleteJobAsync(string jobId);

    /// <summary>
    /// Lists all jobs
    /// </summary>
    /// <returns>List of job details</returns>
    Task<IEnumerable<JobDetails>> ListJobsAsync();
}

/// <summary>
/// Represents a scheduled job
/// </summary>
public class ScheduledJob
{
    /// <summary>
    /// Job name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Job type
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Workflow ID to execute
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Job parameters
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Job priority
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for job execution
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Job execution details
/// </summary>
public class JobDetails
{
    /// <summary>
    /// Job ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Job name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Job state
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// Creation time
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last execution time
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Next execution time
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Number of attempts
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Execution result or error message
    /// </summary>
    public string? Result { get; set; }
}

/// <summary>
/// Job execution state
/// </summary>
public enum JobState
{
    Enqueued,
    Processing,
    Succeeded,
    Failed,
    Scheduled,
    Deleted
}
