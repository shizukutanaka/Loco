namespace Loco.Core.Sagas;

/// <summary>
/// Saga step interface
/// </summary>
public interface ISagaStep
{
    /// <summary>
    /// Step name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Step description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the step
    /// </summary>
    Task<SagaStepResult> ExecuteAsync(SagaContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Compensates (rolls back) the step
    /// </summary>
    Task<bool> CompensateAsync(SagaContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Gets step timeout
    /// </summary>
    TimeSpan GetTimeout();
}

/// <summary>
/// Saga step result
/// </summary>
public class SagaStepResult
{
    /// <summary>
    /// Success flag
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Output data
    /// </summary>
    public Dictionary<string, object?> Output { get; set; } = new();

    /// <summary>
    /// Should retry
    /// </summary>
    public bool ShouldRetry { get; set; }

    /// <summary>
    /// Retry count
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Next step name (for routing)
    /// </summary>
    public string? NextStepName { get; set; }
}

/// <summary>
/// Saga context for sharing state between steps
/// </summary>
public class SagaContext
{
    /// <summary>
    /// Saga ID
    /// </summary>
    public string SagaId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Current step name
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Saga data/variables
    /// </summary>
    public Dictionary<string, object?> Data { get; set; } = new();

    /// <summary>
    /// Executed steps for compensation
    /// </summary>
    public List<string> ExecutedSteps { get; set; } = new();

    /// <summary>
    /// Step results
    /// </summary>
    public Dictionary<string, SagaStepResult> StepResults { get; set; } = new();

    /// <summary>
    /// Start time
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Is compensating
    /// </summary>
    public bool IsCompensating { get; set; }
}

/// <summary>
/// Saga definition interface
/// </summary>
public interface ISagaDefinition
{
    /// <summary>
    /// Saga name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Saga description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets saga steps
    /// </summary>
    IEnumerable<ISagaStep> GetSteps();

    /// <summary>
    /// Gets start step
    /// </summary>
    ISagaStep? GetStartStep();

    /// <summary>
    /// Gets next step
    /// </summary>
    ISagaStep? GetNextStep(string currentStepName, SagaStepResult result);

    /// <summary>
    /// Saga timeout
    /// </summary>
    TimeSpan GetTimeout();
}

/// <summary>
/// Saga orchestrator interface
/// </summary>
public interface ISagaOrchestrator
{
    /// <summary>
    /// Executes a saga
    /// </summary>
    Task<SagaExecutionResult> ExecuteAsync(
        ISagaDefinition definition,
        Dictionary<string, object?> initialData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets saga status
    /// </summary>
    Task<SagaExecutionResult?> GetStatusAsync(string sagaId);

    /// <summary>
    /// Cancels a saga
    /// </summary>
    Task<bool> CancelAsync(string sagaId);
}

/// <summary>
/// Saga execution result
/// </summary>
public class SagaExecutionResult
{
    /// <summary>
    /// Saga ID
    /// </summary>
    public string SagaId { get; set; } = string.Empty;

    /// <summary>
    /// Saga name
    /// </summary>
    public string SagaName { get; set; } = string.Empty;

    /// <summary>
    /// Success flag
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status
    /// </summary>
    public SagaStatus Status { get; set; }

    /// <summary>
    /// Current step
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Executed steps
    /// </summary>
    public List<string> ExecutedSteps { get; set; } = new();

    /// <summary>
    /// Step results
    /// </summary>
    public Dictionary<string, SagaStepResult> StepResults { get; set; } = new();

    /// <summary>
    /// Final output
    /// </summary>
    public Dictionary<string, object?> Output { get; set; } = new();

    /// <summary>
    /// Error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Execution duration
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    /// Compensation performed
    /// </summary>
    public bool CompensationPerformed { get; set; }
}

/// <summary>
/// Saga status enumeration
/// </summary>
public enum SagaStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Compensating,
    Compensated,
    Cancelled
}
