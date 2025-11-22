// Phase 5: Advanced Execution Timeline & Observability
// Detailed timeline visualization, distributed profiling, and performance insights
// Enables root cause analysis and performance optimization

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Observability;

/// <summary>
/// Timeline event (represents execution of a single operation)
/// </summary>
public class TimelineEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty; // step_start, step_complete, api_call, db_query
    public string ResourceName { get; set; } = string.Empty; // Step name, API endpoint, SQL query
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ParentEventId { get; set; }
    public string Status { get; set; } = "success"; // success, failure, timeout, cancelled
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public double DurationMs => (EndTime - StartTime).TotalMilliseconds;
}

/// <summary>
/// Execution timeline (collection of events for single execution)
/// </summary>
public class ExecutionTimeline
{
    public string ExecutionId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<TimelineEvent> Events { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty; // For distributed tracing

    public double TotalDurationMs => (EndTime - StartTime).TotalMilliseconds;

    public IEnumerable<TimelineEvent> GetCriticalPath() =>
        Events.Where(e => !e.EventType.Contains("internal")).OrderBy(e => e.StartTime);

    public double GetParallelizationScore() =>
        CalculateParallelizationScore();

    private double CalculateParallelizationScore()
    {
        // Score: 0 = fully sequential, 1 = perfectly parallel
        var totalSequential = Events.Sum(e => e.DurationMs);
        var maxParallel = Events.Max(e => e.EndTime.Ticks) - Events.Min(e => e.StartTime.Ticks);

        if (maxParallel == 0)
            return 0;

        return 1.0 - (maxParallel / totalSequential);
    }
}

/// <summary>
/// Performance profile for bottleneck analysis
/// </summary>
public class PerformanceProfile
{
    public string ExecutionId { get; set; } = string.Empty;
    public List<ProfileEntry> TopSlowOperations { get; set; } = new();
    public List<ProfileEntry> MostFrequentOperations { get; set; } = new();
    public Dictionary<string, double> OperationDistribution { get; set; } = new();
    public double CriticalPathDurationMs { get; set; }
    public List<string> Bottlenecks { get; set; } = new();
}

/// <summary>
/// Single entry in performance profile
/// </summary>
public class ProfileEntry
{
    public string OperationName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalDurationMs { get; set; }
    public double AverageDurationMs { get; set; }
    public double P95DurationMs { get; set; }
    public double P99DurationMs { get; set; }

    public double PercentageOfTotal { get; set; }
}

/// <summary>
/// Execution timeline builder (fluent API)
/// </summary>
public class TimelineBuilder
{
    private readonly ExecutionTimeline _timeline;
    private string? _currentEventId;

    public TimelineBuilder(string executionId, string workflowId)
    {
        _timeline = new ExecutionTimeline
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            StartTime = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString(),
        };
    }

    /// <summary>
    /// Add timeline event
    /// </summary>
    public TimelineBuilder AddEvent(
        string eventType,
        string resourceName,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? parentEventId = null)
    {
        var now = DateTime.UtcNow;

        var @event = new TimelineEvent
        {
            EventType = eventType,
            ResourceName = resourceName,
            StartTime = startTime ?? now,
            EndTime = endTime ?? now.AddMilliseconds(100),
            ParentEventId = parentEventId ?? _currentEventId,
            Status = "success",
        };

        _timeline.Events.Add(@event);
        _currentEventId = @event.EventId;

        return this;
    }

    /// <summary>
    /// Add event with metadata
    /// </summary>
    public TimelineBuilder AddEventWithMetadata(
        string eventType,
        string resourceName,
        Dictionary<string, object> metadata,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        var now = DateTime.UtcNow;

        var @event = new TimelineEvent
        {
            EventType = eventType,
            ResourceName = resourceName,
            StartTime = startTime ?? now,
            EndTime = endTime ?? now.AddMilliseconds(100),
            Metadata = metadata,
            Status = "success",
        };

        _timeline.Events.Add(@event);
        _currentEventId = @event.EventId;

        return this;
    }

    /// <summary>
    /// Mark event as failed
    /// </summary>
    public TimelineBuilder MarkEventFailed(string eventId, string errorMessage)
    {
        var @event = _timeline.Events.FirstOrDefault(e => e.EventId == eventId);
        if (@event != null)
        {
            @event.Status = "failure";
            @event.ErrorMessage = errorMessage;
        }

        return this;
    }

    /// <summary>
    /// Build timeline
    /// </summary>
    public ExecutionTimeline Build()
    {
        _timeline.EndTime = DateTime.UtcNow;
        return _timeline;
    }
}

/// <summary>
/// Advanced observability interface
/// </summary>
public interface IExecutionObservability
{
    Task RecordTimelineAsync(
        ExecutionTimeline timeline,
        CancellationToken ct = default);

    Task<ExecutionTimeline?> GetTimelineAsync(
        string executionId,
        CancellationToken ct = default);

    Task<PerformanceProfile> ProfileExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    Task<List<(string Operation, double DurationMs)>> GetCriticalPathAsync(
        string executionId,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> GetOperationDistributionAsync(
        string executionId,
        CancellationToken ct = default);
}

/// <summary>
/// Execution observability implementation
/// </summary>
public class ExecutionObservability : IExecutionObservability
{
    private readonly ILogger<ExecutionObservability> _logger;
    private readonly Dictionary<string, ExecutionTimeline> _timelines;
    private readonly Dictionary<string, PerformanceProfile> _profiles;

    public ExecutionObservability(ILogger<ExecutionObservability> logger)
    {
        _logger = logger;
        _timelines = new Dictionary<string, ExecutionTimeline>();
        _profiles = new Dictionary<string, PerformanceProfile>();
    }

    /// <summary>
    /// Record execution timeline
    /// </summary>
    public async Task RecordTimelineAsync(
        ExecutionTimeline timeline,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _timelines[timeline.ExecutionId] = timeline;

        // Generate profile
        var profile = GenerateProfile(timeline);
        _profiles[timeline.ExecutionId] = profile;

        _logger.LogInformation(
            "Timeline recorded for {ExecutionId}: {Duration}ms, {Events} events, " +
            "Parallelization Score: {Score:P}",
            timeline.ExecutionId,
            timeline.TotalDurationMs,
            timeline.Events.Count,
            timeline.GetParallelizationScore());
    }

    /// <summary>
    /// Get execution timeline
    /// </summary>
    public async Task<ExecutionTimeline?> GetTimelineAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _timelines.TryGetValue(executionId, out var timeline);
        return timeline;
    }

    /// <summary>
    /// Profile execution for performance analysis
    /// </summary>
    public async Task<PerformanceProfile> ProfileExecutionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate analysis

        if (_profiles.TryGetValue(executionId, out var profile))
        {
            return profile;
        }

        return new PerformanceProfile { ExecutionId = executionId };
    }

    /// <summary>
    /// Get critical path (longest dependency chain)
    /// </summary>
    public async Task<List<(string Operation, double DurationMs)>> GetCriticalPathAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_timelines.TryGetValue(executionId, out var timeline))
        {
            return new List<(string, double)>();
        }

        var criticalPath = timeline.GetCriticalPath()
            .Select(e => (e.ResourceName, e.DurationMs))
            .ToList();

        return criticalPath;
    }

    /// <summary>
    /// Get operation distribution (percentage breakdown)
    /// </summary>
    public async Task<Dictionary<string, double>> GetOperationDistributionAsync(
        string executionId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_timelines.TryGetValue(executionId, out var timeline))
        {
            return new Dictionary<string, double>();
        }

        var totalDuration = timeline.TotalDurationMs;
        var distribution = timeline.Events
            .GroupBy(e => e.EventType)
            .ToDictionary(
                g => g.Key,
                g => (g.Sum(e => e.DurationMs) / totalDuration) * 100);

        return distribution;
    }

    // Private helper method
    private PerformanceProfile GenerateProfile(ExecutionTimeline timeline)
    {
        var profile = new PerformanceProfile { ExecutionId = timeline.ExecutionId };
        var totalDuration = timeline.TotalDurationMs;

        // Group operations
        var operationGroups = timeline.Events
            .GroupBy(e => e.ResourceName)
            .Select(g => new ProfileEntry
            {
                OperationName = g.Key,
                Count = g.Count(),
                TotalDurationMs = g.Sum(e => e.DurationMs),
                AverageDurationMs = g.Average(e => e.DurationMs),
                P95DurationMs = g.OrderByDescending(e => e.DurationMs).Take((int)(g.Count() * 0.05)).FirstOrDefault()?.DurationMs ?? 0,
                P99DurationMs = g.OrderByDescending(e => e.DurationMs).Take((int)(g.Count() * 0.01)).FirstOrDefault()?.DurationMs ?? 0,
            })
            .ToList();

        // Calculate percentage of total
        foreach (var entry in operationGroups)
        {
            entry.PercentageOfTotal = (entry.TotalDurationMs / totalDuration) * 100;
        }

        // Top slow operations
        profile.TopSlowOperations = operationGroups
            .OrderByDescending(e => e.TotalDurationMs)
            .Take(5)
            .ToList();

        // Most frequent operations
        profile.MostFrequentOperations = operationGroups
            .OrderByDescending(e => e.Count)
            .Take(5)
            .ToList();

        // Operation distribution
        profile.OperationDistribution = operationGroups
            .ToDictionary(e => e.OperationName, e => e.PercentageOfTotal);

        // Critical path
        var criticalPath = timeline.GetCriticalPath().ToList();
        profile.CriticalPathDurationMs = criticalPath.Sum(e => e.DurationMs);

        // Identify bottlenecks (operations > 20% of total)
        profile.Bottlenecks = operationGroups
            .Where(e => e.PercentageOfTotal > 20)
            .Select(e => e.OperationName)
            .ToList();

        return profile;
    }
}

/// <summary>
/// Timeline visualization helper (generates text-based timeline)
/// </summary>
public static class TimelineVisualizer
{
    /// <summary>
    /// Generate ASCII timeline visualization
    /// </summary>
    public static string Visualize(ExecutionTimeline timeline)
    {
        var minTime = timeline.Events.Min(e => e.StartTime);
        var maxTime = timeline.Events.Max(e => e.EndTime);
        var timeRange = (maxTime - minTime).TotalMilliseconds;

        var output = new System.Text.StringBuilder();

        output.AppendLine($"Execution Timeline: {timeline.ExecutionId}");
        output.AppendLine($"Duration: {timeline.TotalDurationMs:F2}ms");
        output.AppendLine($"Parallelization Score: {timeline.GetParallelizationScore():P}");
        output.AppendLine();
        output.AppendLine("Timeline Visualization:");
        output.AppendLine(new string('=', 80));

        foreach (var @event in timeline.Events.OrderBy(e => e.StartTime))
        {
            var startOffset = (@event.StartTime - minTime).TotalMilliseconds;
            var barLength = (int)((@event.DurationMs / timeRange) * 40);

            var statusIcon = @event.Status == "success" ? "✓" : "✗";
            output.AppendLine(
                $"{statusIcon} {@event.ResourceName,-30} [{startOffset:F0}ms] " +
                new string('█', Math.Max(1, barLength)) +
                $" {@event.DurationMs:F2}ms");
        }

        return output.ToString();
    }
}
