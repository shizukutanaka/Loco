using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Persistence;

/// <summary>
/// Workflow execution state for persistence.
/// </summary>
public class WorkflowState
{
    public string ExecutionId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public WorkflowStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, StepState> StepStates { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Individual step execution state.
/// </summary>
public class StepState
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public StepStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public TimeSpan? Duration { get; set; }
}

/// <summary>
/// Workflow execution status.
/// </summary>
public enum WorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused,
    Retrying
}

/// <summary>
/// Step execution status.
/// </summary>
public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Retrying
}

/// <summary>
/// Persistent storage for workflow execution state.
/// Supports crash recovery, pause/resume, and execution history.
/// </summary>
public class WorkflowStateStore : IDisposable
{
    private readonly string _storagePath;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, WorkflowState> _activeStates = new();
    private readonly Timer _autoSaveTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public WorkflowStateStore(string storagePath, ILogger? logger = null, int autoSaveIntervalMs = 5000)
    {
        _storagePath = storagePath;
        _logger = logger;
        _isInitialized = false;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Ensure storage directory exists
        Directory.CreateDirectory(_storagePath);

        // Auto-save timer
        _autoSaveTimer = new Timer(
            async _ => await AutoSaveAsync(),
            null,
            TimeSpan.FromMilliseconds(autoSaveIntervalMs),
            TimeSpan.FromMilliseconds(autoSaveIntervalMs));

        _logger?.LogInformation("WorkflowStateStore initialized at {Path}", _storagePath);
    }

    /// <summary>
    /// Initializes the state store by loading existing states from disk.
    /// Must be called before using other methods to ensure states are loaded.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _logger?.LogInformation("Loading workflow states from storage...");
            await LoadAllStatesAsync();
            _isInitialized = true;
            _logger?.LogInformation("Workflow state loading completed. {Count} states loaded.", _activeStates.Count);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Checks if the store is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Creates a new workflow execution state.
    /// </summary>
    public async Task<WorkflowState> CreateStateAsync(string executionId, string workflowId, List<string> stepIds)
    {
        var state = new WorkflowState
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            Status = WorkflowStatus.Pending,
            StartedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // Initialize step states
        foreach (var stepId in stepIds)
        {
            state.StepStates[stepId] = new StepState
            {
                StepId = stepId,
                StepName = stepId,
                Status = StepStatus.Pending
            };
        }

        _activeStates[executionId] = state;
        await SaveStateAsync(state);

        _logger?.LogInformation("Created workflow state for execution {ExecutionId}", executionId);

        return state;
    }

    /// <summary>
    /// Updates workflow state.
    /// </summary>
    public async Task UpdateWorkflowStatusAsync(string executionId, WorkflowStatus status, string? errorMessage = null)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
        {
            _logger?.LogWarning("Workflow state not found: {ExecutionId}", executionId);
            return;
        }

        state.Status = status;
        state.LastUpdated = DateTime.UtcNow;
        state.ErrorMessage = errorMessage;

        if (status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
        {
            state.CompletedAt = DateTime.UtcNow;
        }

        await SaveStateAsync(state);

        _logger?.LogDebug("Updated workflow {ExecutionId} status to {Status}", executionId, status);
    }

    /// <summary>
    /// Updates step state.
    /// </summary>
    public async Task UpdateStepStateAsync(string executionId, string stepId, StepStatus status,
        object? result = null, string? errorMessage = null)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
        {
            _logger?.LogWarning("Workflow state not found: {ExecutionId}", executionId);
            return;
        }

        if (!state.StepStates.TryGetValue(stepId, out var stepState))
        {
            stepState = new StepState
            {
                StepId = stepId,
                StepName = stepId,
                Status = StepStatus.Pending
            };
            state.StepStates[stepId] = stepState;
        }

        stepState.Status = status;
        stepState.ErrorMessage = errorMessage;

        if (status == StepStatus.Running && !stepState.StartedAt.HasValue)
        {
            stepState.StartedAt = DateTime.UtcNow;
        }

        if (status is StepStatus.Completed or StepStatus.Failed or StepStatus.Skipped)
        {
            stepState.CompletedAt = DateTime.UtcNow;
            stepState.Result = result;

            if (stepState.StartedAt.HasValue)
            {
                stepState.Duration = stepState.CompletedAt.Value - stepState.StartedAt.Value;
            }
        }

        state.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync(state);

        _logger?.LogDebug("Updated step {StepId} in execution {ExecutionId} to {Status}",
            stepId, executionId, status);
    }

    /// <summary>
    /// Increments retry count for a workflow.
    /// </summary>
    public async Task IncrementRetryCountAsync(string executionId)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        state.RetryCount++;
        state.Status = WorkflowStatus.Retrying;
        state.LastUpdated = DateTime.UtcNow;

        await SaveStateAsync(state);

        _logger?.LogInformation("Incremented retry count for {ExecutionId} to {Count}",
            executionId, state.RetryCount);
    }

    /// <summary>
    /// Increments retry count for a step.
    /// </summary>
    public async Task IncrementStepRetryCountAsync(string executionId, string stepId)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        if (state.StepStates.TryGetValue(stepId, out var stepState))
        {
            stepState.RetryCount++;
            stepState.Status = StepStatus.Retrying;
            state.LastUpdated = DateTime.UtcNow;

            await SaveStateAsync(state);

            _logger?.LogInformation("Incremented retry count for step {StepId} to {Count}",
                stepId, stepState.RetryCount);
        }
    }

    /// <summary>
    /// Sets workflow variables.
    /// </summary>
    public async Task SetVariablesAsync(string executionId, Dictionary<string, object> variables)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        foreach (var kvp in variables)
        {
            state.Variables[kvp.Key] = kvp.Value;
        }

        state.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync(state);
    }

    /// <summary>
    /// Gets workflow state.
    /// </summary>
    public WorkflowState? GetState(string executionId)
    {
        return _activeStates.TryGetValue(executionId, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all active workflows.
    /// </summary>
    public List<WorkflowState> GetActiveWorkflows()
    {
        return _activeStates.Values
            .Where(s => s.Status is WorkflowStatus.Running or WorkflowStatus.Paused or WorkflowStatus.Retrying)
            .OrderByDescending(s => s.LastUpdated)
            .ToList();
    }

    /// <summary>
    /// Gets workflows by status.
    /// </summary>
    public List<WorkflowState> GetWorkflowsByStatus(WorkflowStatus status)
    {
        return _activeStates.Values
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.LastUpdated)
            .ToList();
    }

    /// <summary>
    /// Recovers workflows that were running when the system crashed.
    /// </summary>
    public async Task<List<WorkflowState>> RecoverCrashedWorkflowsAsync()
    {
        var crashed = _activeStates.Values
            .Where(s => s.Status is WorkflowStatus.Running or WorkflowStatus.Retrying)
            .ToList();

        foreach (var state in crashed)
        {
            state.Status = WorkflowStatus.Failed;
            state.ErrorMessage = "Workflow interrupted by system shutdown";
            state.CompletedAt = DateTime.UtcNow;
            state.LastUpdated = DateTime.UtcNow;

            await SaveStateAsync(state);
        }

        _logger?.LogInformation("Recovered {Count} crashed workflows", crashed.Count);

        return crashed;
    }

    /// <summary>
    /// Pauses a running workflow.
    /// </summary>
    public async Task PauseWorkflowAsync(string executionId)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        if (state.Status == WorkflowStatus.Running)
        {
            state.Status = WorkflowStatus.Paused;
            state.LastUpdated = DateTime.UtcNow;
            await SaveStateAsync(state);

            _logger?.LogInformation("Paused workflow {ExecutionId}", executionId);
        }
    }

    /// <summary>
    /// Resumes a paused workflow.
    /// </summary>
    public async Task ResumeWorkflowAsync(string executionId)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        if (state.Status == WorkflowStatus.Paused)
        {
            state.Status = WorkflowStatus.Running;
            state.LastUpdated = DateTime.UtcNow;
            await SaveStateAsync(state);

            _logger?.LogInformation("Resumed workflow {ExecutionId}", executionId);
        }
    }

    /// <summary>
    /// Cancels a workflow.
    /// </summary>
    public async Task CancelWorkflowAsync(string executionId)
    {
        if (!_activeStates.TryGetValue(executionId, out var state))
            return;

        state.Status = WorkflowStatus.Cancelled;
        state.CompletedAt = DateTime.UtcNow;
        state.LastUpdated = DateTime.UtcNow;
        await SaveStateAsync(state);

        _logger?.LogInformation("Cancelled workflow {ExecutionId}", executionId);
    }

    /// <summary>
    /// Saves state to disk.
    /// </summary>
    private async Task SaveStateAsync(WorkflowState state)
    {
        await _saveLock.WaitAsync();
        try
        {
            var filePath = GetStateFilePath(state.ExecutionId);
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save state for execution {ExecutionId}", state.ExecutionId);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Auto-saves all dirty states.
    /// </summary>
    private async Task AutoSaveAsync()
    {
        try
        {
            var states = _activeStates.Values
                .Where(s => s.Status is WorkflowStatus.Running or WorkflowStatus.Paused or WorkflowStatus.Retrying)
                .ToList();

            foreach (var state in states)
            {
                await SaveStateAsync(state);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during auto-save");
        }
    }

    /// <summary>
    /// Loads all states from disk.
    /// </summary>
    private async Task LoadAllStatesAsync()
    {
        try
        {
            var files = Directory.GetFiles(_storagePath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var state = JsonSerializer.Deserialize<WorkflowState>(json, _jsonOptions);

                    if (state != null)
                    {
                        _activeStates[state.ExecutionId] = state;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load state file: {File}", file);
                }
            }

            _logger?.LogInformation("Loaded {Count} workflow states from disk", _activeStates.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load workflow states");
        }
    }

    /// <summary>
    /// Deletes old completed workflow states.
    /// </summary>
    public async Task<int> CleanupOldStatesAsync(TimeSpan olderThan)
    {
        var cutoffTime = DateTime.UtcNow - olderThan;
        var toDelete = _activeStates.Values
            .Where(s => s.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
            .Where(s => s.CompletedAt.HasValue && s.CompletedAt.Value < cutoffTime)
            .ToList();

        foreach (var state in toDelete)
        {
            _activeStates.TryRemove(state.ExecutionId, out _);

            try
            {
                var filePath = GetStateFilePath(state.ExecutionId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete state file for {ExecutionId}", state.ExecutionId);
            }
        }

        _logger?.LogInformation("Cleaned up {Count} old workflow states", toDelete.Count);

        return toDelete.Count;
    }

    /// <summary>
    /// Gets statistics.
    /// </summary>
    public WorkflowStateStoreStats GetStats()
    {
        var states = _activeStates.Values.ToList();

        return new WorkflowStateStoreStats
        {
            TotalStates = states.Count,
            RunningCount = states.Count(s => s.Status == WorkflowStatus.Running),
            CompletedCount = states.Count(s => s.Status == WorkflowStatus.Completed),
            FailedCount = states.Count(s => s.Status == WorkflowStatus.Failed),
            PausedCount = states.Count(s => s.Status == WorkflowStatus.Paused),
            RetryingCount = states.Count(s => s.Status == WorkflowStatus.Retrying),
            CancelledCount = states.Count(s => s.Status == WorkflowStatus.Cancelled),
            StoragePath = _storagePath
        };
    }

    private string GetStateFilePath(string executionId)
    {
        return Path.Combine(_storagePath, $"{executionId}.json");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _autoSaveTimer?.Dispose();
            _saveLock?.Dispose();
            _initializationLock?.Dispose();

            // Final save
            foreach (var state in _activeStates.Values)
            {
                SaveStateAsync(state).Wait();
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Workflow state store statistics.
/// </summary>
public class WorkflowStateStoreStats
{
    public int TotalStates { get; set; }
    public int RunningCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int PausedCount { get; set; }
    public int RetryingCount { get; set; }
    public int CancelledCount { get; set; }
    public string StoragePath { get; set; } = "";
}
