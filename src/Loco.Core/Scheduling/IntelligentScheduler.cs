using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Scheduling;

/// <summary>
/// Intelligent task scheduler with priority-based scheduling, dependency management, and resource optimization
/// Based on 2025 best practices: AI-powered scheduling patterns, multi-factor optimization
/// インテリジェントタスクスケジューラー - 優先度、依存関係、リソースを考慮
/// </summary>
public class IntelligentScheduler
{
    private readonly ILogger<IntelligentScheduler>? _logger;
    private readonly SortedSet<ScheduledTask> _taskQueue;
    private readonly Dictionary<string, ScheduledTask> _taskRegistry;
    private readonly Dictionary<string, List<string>> _dependencies;
    private readonly SemaphoreSlim _schedulerLock;
    private readonly ResourceMonitor _resourceMonitor;

    public IntelligentScheduler(ILogger<IntelligentScheduler>? logger = null)
    {
        _logger = logger;
        _taskQueue = new SortedSet<ScheduledTask>(new TaskPriorityComparer());
        _taskRegistry = new Dictionary<string, ScheduledTask>();
        _dependencies = new Dictionary<string, List<string>>();
        _schedulerLock = new SemaphoreSlim(1, 1);
        _resourceMonitor = new ResourceMonitor();
    }

    /// <summary>
    /// Schedule a task with intelligent prioritization
    /// タスクをインテリジェントに優先順位付けしてスケジュール
    /// </summary>
    public async Task<ScheduleResult> ScheduleTaskAsync(
        TaskDefinition task,
        SchedulingContext context,
        CancellationToken cancellationToken = default)
    {
        if (task == null)
            throw new ArgumentNullException(nameof(task));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        await _schedulerLock.WaitAsync(cancellationToken);
        try
        {
            // Calculate task priority based on multiple factors
            var priority = CalculatePriority(task, context);

            // Check dependencies
            var dependencies = task.Dependencies ?? Array.Empty<string>();
            var canSchedule = CanScheduleTask(task.Id, dependencies);

            if (!canSchedule)
            {
                _logger?.LogWarning("Task {TaskId} cannot be scheduled due to unmet dependencies", task.Id);
                return new ScheduleResult
                {
                    Success = false,
                    TaskId = task.Id,
                    Message = "Dependencies not met",
                    EstimatedStartTime = null
                };
            }

            // Check resource availability
            var resourceCheck = await _resourceMonitor.CheckResourcesAsync(task.ResourceRequirements);
            if (!resourceCheck.Available)
            {
                _logger?.LogInformation("Task {TaskId} delayed due to insufficient resources. Will retry when resources become available.", task.Id);

                // Schedule for later when resources might be available
                var delayedStartTime = DateTime.UtcNow.AddMinutes(5);
                var scheduledTask = new ScheduledTask
                {
                    Id = task.Id,
                    Definition = task,
                    Priority = priority,
                    EstimatedStartTime = delayedStartTime,
                    Status = TaskStatus.Pending,
                    ResourceRequirements = task.ResourceRequirements
                };

                _taskQueue.Add(scheduledTask);
                _taskRegistry[task.Id] = scheduledTask;

                return new ScheduleResult
                {
                    Success = true,
                    TaskId = task.Id,
                    Message = "Scheduled (delayed due to resources)",
                    EstimatedStartTime = delayedStartTime
                };
            }

            // Calculate estimated start time based on queue and priorities
            var estimatedStart = CalculateEstimatedStartTime(priority, task.ResourceRequirements);

            // Create scheduled task
            var newTask = new ScheduledTask
            {
                Id = task.Id,
                Definition = task,
                Priority = priority,
                EstimatedStartTime = estimatedStart,
                Status = TaskStatus.Pending,
                ResourceRequirements = task.ResourceRequirements,
                ScheduledAt = DateTime.UtcNow
            };

            // Add to queue and registry
            _taskQueue.Add(newTask);
            _taskRegistry[task.Id] = newTask;

            // Register dependencies
            if (dependencies.Length > 0)
            {
                _dependencies[task.Id] = dependencies.ToList();
            }

            _logger?.LogInformation(
                "Scheduled task {TaskId} with priority {Priority}. Estimated start: {EstimatedStart}",
                task.Id, priority, estimatedStart);

            return new ScheduleResult
            {
                Success = true,
                TaskId = task.Id,
                Priority = priority,
                EstimatedStartTime = estimatedStart,
                Message = "Successfully scheduled"
            };
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Get the next task to execute based on priority and dependencies
    /// 優先度と依存関係に基づいて次に実行するタスクを取得
    /// </summary>
    public async Task<ScheduledTask?> GetNextTaskAsync(CancellationToken cancellationToken = default)
    {
        await _schedulerLock.WaitAsync(cancellationToken);
        try
        {
            // Find the highest priority task with met dependencies
            foreach (var task in _taskQueue)
            {
                if (task.Status != TaskStatus.Pending)
                    continue;

                // Check if dependencies are met
                if (_dependencies.TryGetValue(task.Id, out var deps))
                {
                    var allDepsMet = deps.All(depId =>
                        _taskRegistry.TryGetValue(depId, out var depTask) &&
                        depTask.Status == TaskStatus.Completed);

                    if (!allDepsMet)
                        continue;
                }

                // Check if resource requirements can be met
                var resourceCheck = await _resourceMonitor.CheckResourcesAsync(task.ResourceRequirements);
                if (!resourceCheck.Available)
                    continue;

                // Mark as running
                task.Status = TaskStatus.Running;
                task.ActualStartTime = DateTime.UtcNow;

                _logger?.LogInformation("Dequeued task {TaskId} for execution", task.Id);
                return task;
            }

            return null;
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Mark a task as completed
    /// タスクを完了としてマーク
    /// </summary>
    public async Task CompleteTaskAsync(string taskId, bool success, CancellationToken cancellationToken = default)
    {
        await _schedulerLock.WaitAsync(cancellationToken);
        try
        {
            if (_taskRegistry.TryGetValue(taskId, out var task))
            {
                task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;
                task.CompletedAt = DateTime.UtcNow;

                // Remove from queue if present
                _taskQueue.Remove(task);

                _logger?.LogInformation("Task {TaskId} marked as {Status}", taskId, task.Status);

                // Check for dependent tasks that can now be scheduled
                var dependents = _dependencies.Where(kvp => kvp.Value.Contains(taskId)).Select(kvp => kvp.Key);
                foreach (var dependentId in dependents)
                {
                    _logger?.LogDebug("Dependency {TaskId} completed for task {DependentId}", taskId, dependentId);
                }
            }
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Cancel a scheduled task
    /// スケジュールされたタスクをキャンセル
    /// </summary>
    public async Task<bool> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await _schedulerLock.WaitAsync(cancellationToken);
        try
        {
            if (_taskRegistry.TryGetValue(taskId, out var task))
            {
                if (task.Status == TaskStatus.Running)
                {
                    _logger?.LogWarning("Cannot cancel task {TaskId} - already running", taskId);
                    return false;
                }

                task.Status = TaskStatus.Cancelled;
                _taskQueue.Remove(task);
                _taskRegistry.Remove(taskId);
                _dependencies.Remove(taskId);

                _logger?.LogInformation("Cancelled task {TaskId}", taskId);
                return true;
            }

            return false;
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Get current queue statistics
    /// 現在のキュー統計を取得
    /// </summary>
    public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await _schedulerLock.WaitAsync(cancellationToken);
        try
        {
            var stats = new QueueStatistics
            {
                TotalTasks = _taskRegistry.Count,
                PendingTasks = _taskQueue.Count(t => t.Status == TaskStatus.Pending),
                RunningTasks = _taskQueue.Count(t => t.Status == TaskStatus.Running),
                CompletedTasks = _taskRegistry.Values.Count(t => t.Status == TaskStatus.Completed),
                FailedTasks = _taskRegistry.Values.Count(t => t.Status == TaskStatus.Failed),
                CancelledTasks = _taskRegistry.Values.Count(t => t.Status == TaskStatus.Cancelled),
                AverageWaitTime = CalculateAverageWaitTime(),
                ResourceUtilization = await _resourceMonitor.GetUtilizationAsync()
            };

            return stats;
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Calculate task priority based on multiple factors
    /// 複数の要素に基づいてタスクの優先度を計算
    /// </summary>
    private int CalculatePriority(TaskDefinition task, SchedulingContext context)
    {
        var basePriority = task.Priority;

        // Factor 1: Deadline urgency (高いほど優先度アップ)
        if (task.Deadline.HasValue)
        {
            var timeUntilDeadline = task.Deadline.Value - DateTime.UtcNow;
            if (timeUntilDeadline.TotalHours < 1)
                basePriority += 50; // Critical - deadline within 1 hour
            else if (timeUntilDeadline.TotalHours < 24)
                basePriority += 20; // High - deadline within 24 hours
            else if (timeUntilDeadline.TotalDays < 7)
                basePriority += 10; // Medium - deadline within a week
        }

        // Factor 2: Estimated duration (短いタスクを優先)
        if (task.EstimatedDuration.HasValue && task.EstimatedDuration.Value.TotalMinutes < 5)
        {
            basePriority += 15; // Quick tasks get priority boost
        }

        // Factor 3: Number of dependencies (依存が少ないタスクを優先)
        var depCount = task.Dependencies?.Length ?? 0;
        if (depCount == 0)
        {
            basePriority += 10; // No dependencies - can run immediately
        }

        // Factor 4: Business importance (context-based)
        if (context.HighPriorityTags != null && task.Tags != null)
        {
            var matchingTags = task.Tags.Intersect(context.HighPriorityTags).Count();
            basePriority += matchingTags * 5;
        }

        // Factor 5: Resource efficiency (リソース効率が良いタスクを優先)
        if (task.ResourceRequirements != null)
        {
            var efficiency = CalculateResourceEfficiency(task.ResourceRequirements);
            basePriority += (int)(efficiency * 10);
        }

        return Math.Max(0, basePriority);
    }

    private double CalculateResourceEfficiency(ResourceRequirements requirements)
    {
        // Simple heuristic: lower resource requirements = higher efficiency
        var memoryScore = 1.0 - (requirements.MemoryMB / 1024.0); // Normalize to 0-1
        var cpuScore = 1.0 - (requirements.CpuPercent / 100.0);
        return (memoryScore + cpuScore) / 2.0;
    }

    private bool CanScheduleTask(string taskId, string[] dependencies)
    {
        if (dependencies == null || dependencies.Length == 0)
            return true;

        // Check for circular dependencies
        var visited = new HashSet<string>();
        if (HasCircularDependency(taskId, dependencies, visited))
        {
            _logger?.LogError("Circular dependency detected for task {TaskId}", taskId);
            return false;
        }

        return true;
    }

    private bool HasCircularDependency(string taskId, string[] dependencies, HashSet<string> visited)
    {
        if (visited.Contains(taskId))
            return true;

        visited.Add(taskId);

        foreach (var depId in dependencies)
        {
            if (_dependencies.TryGetValue(depId, out var subDeps))
            {
                if (HasCircularDependency(depId, subDeps.ToArray(), visited))
                    return true;
            }
        }

        visited.Remove(taskId);
        return false;
    }

    private DateTime CalculateEstimatedStartTime(int priority, ResourceRequirements? requirements)
    {
        // Simple estimation: higher priority = sooner start time
        var baseDelay = TimeSpan.FromSeconds(Math.Max(0, 100 - priority));

        // Adjust for resource availability
        if (requirements != null)
        {
            var resourceDelay = _resourceMonitor.EstimateResourceAvailabilityDelay(requirements);
            baseDelay += resourceDelay;
        }

        return DateTime.UtcNow.Add(baseDelay);
    }

    private TimeSpan CalculateAverageWaitTime()
    {
        var completedTasks = _taskRegistry.Values
            .Where(t => t.Status == TaskStatus.Completed && t.ActualStartTime.HasValue)
            .ToList();

        if (completedTasks.Count == 0)
            return TimeSpan.Zero;

        var totalWaitTime = completedTasks
            .Select(t => t.ActualStartTime!.Value - t.ScheduledAt)
            .Aggregate(TimeSpan.Zero, (sum, wait) => sum + wait);

        return TimeSpan.FromTicks(totalWaitTime.Ticks / completedTasks.Count);
    }
}

#region Supporting Classes

/// <summary>
/// Task definition with priority, dependencies, and resource requirements
/// タスク定義 - 優先度、依存関係、リソース要件
/// </summary>
public class TaskDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; } = 50; // 0-100, default 50
    public string[] Dependencies { get; set; } = Array.Empty<string>();
    public DateTime? Deadline { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public ResourceRequirements? ResourceRequirements { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Scheduling context with environment-specific parameters
/// スケジューリングコンテキスト - 環境固有のパラメータ
/// </summary>
public class SchedulingContext
{
    public string[] HighPriorityTags { get; set; } = Array.Empty<string>();
    public int MaxConcurrentTasks { get; set; } = 10;
    public bool AllowResourceOversubscription { get; set; } = false;
    public TimeSpan DefaultTaskTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Scheduled task with execution metadata
/// スケジュールされたタスク - 実行メタデータ付き
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; } = string.Empty;
    public TaskDefinition Definition { get; set; } = new();
    public int Priority { get; set; }
    public DateTime EstimatedStartTime { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public ResourceRequirements? ResourceRequirements { get; set; }
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Resource requirements for a task
/// タスクのリソース要件
/// </summary>
public class ResourceRequirements
{
    public int MemoryMB { get; set; } = 100;
    public int CpuPercent { get; set; } = 10;
    public int DiskMB { get; set; } = 0;
    public int NetworkBandwidthKbps { get; set; } = 0;
}

/// <summary>
/// Result of scheduling operation
/// スケジューリング操作の結果
/// </summary>
public class ScheduleResult
{
    public bool Success { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? EstimatedStartTime { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Queue statistics
/// キュー統計
/// </summary>
public class QueueStatistics
{
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int RunningTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int CancelledTasks { get; set; }
    public TimeSpan AverageWaitTime { get; set; }
    public ResourceUtilization ResourceUtilization { get; set; } = new();
}

/// <summary>
/// Resource monitor for tracking system resources
/// リソース監視 - システムリソースの追跡
/// </summary>
internal class ResourceMonitor
{
    public async Task<ResourceCheckResult> CheckResourcesAsync(ResourceRequirements? requirements)
    {
        if (requirements == null)
            return new ResourceCheckResult { Available = true };

        // Simple check: always available for now
        // TODO: Implement actual resource monitoring
        var availableMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
        var hasMemory = availableMemory > requirements.MemoryMB;

        return await Task.FromResult(new ResourceCheckResult
        {
            Available = hasMemory,
            AvailableMemoryMB = (int)availableMemory
        });
    }

    public Task<ResourceUtilization> GetUtilizationAsync()
    {
        var utilization = new ResourceUtilization
        {
            MemoryUsedMB = (int)(GC.GetTotalMemory(false) / 1024 / 1024),
            CpuPercent = 0, // TODO: Implement CPU monitoring
            DiskUsedMB = 0 // TODO: Implement disk monitoring
        };

        return Task.FromResult(utilization);
    }

    public TimeSpan EstimateResourceAvailabilityDelay(ResourceRequirements requirements)
    {
        // Simple heuristic: if resources are tight, add a delay
        // TODO: Implement sophisticated resource prediction
        return TimeSpan.FromSeconds(5);
    }
}

public class ResourceCheckResult
{
    public bool Available { get; set; }
    public int AvailableMemoryMB { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ResourceUtilization
{
    public int MemoryUsedMB { get; set; }
    public int CpuPercent { get; set; }
    public int DiskUsedMB { get; set; }
}

/// <summary>
/// Comparer for task priority (higher priority first)
/// タスク優先度の比較 - 高優先度が先
/// </summary>
internal class TaskPriorityComparer : IComparer<ScheduledTask>
{
    public int Compare(ScheduledTask? x, ScheduledTask? y)
    {
        if (x == null || y == null)
            return 0;

        // Primary: Priority (descending)
        var priorityCompare = y.Priority.CompareTo(x.Priority);
        if (priorityCompare != 0)
            return priorityCompare;

        // Secondary: Estimated start time (ascending)
        var timeCompare = x.EstimatedStartTime.CompareTo(y.EstimatedStartTime);
        if (timeCompare != 0)
            return timeCompare;

        // Tertiary: Task ID (for stability)
        return string.Compare(x.Id, y.Id, StringComparison.Ordinal);
    }
}

#endregion
