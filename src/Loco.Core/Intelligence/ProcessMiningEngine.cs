// Phase 12: Process Mining & Workflow Insights Engine
// Advanced process mining with control-flow analysis and workflow pattern discovery
// Extract insights from execution history, discover process variants, and analyze control-flow

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Intelligence;

/// <summary>
/// Process event (activity log entry)
/// </summary>
public class ProcessEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string CaseId { get; set; } = string.Empty; // Workflow execution ID
    public string ActivityName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // start, complete, error
    public long DurationMs { get; set; }
    public Dictionary<string, object>? Attributes { get; set; }
}

/// <summary>
/// Control-flow edge (activity transition)
/// </summary>
public class ControlFlowEdge
{
    public string EdgeId { get; set; } = Guid.NewGuid().ToString();
    public string SourceActivity { get; set; } = string.Empty;
    public string TargetActivity { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public double Confidence { get; set; } // Percentage of times this transition occurs
    public long AverageDurationMs { get; set; }
}

/// <summary>
/// Discovered process variant (execution path)
/// </summary>
public class ProcessVariant
{
    public string VariantId { get; set; } = Guid.NewGuid().ToString();
    public List<string> ActivitySequence { get; set; } = new();
    public int Frequency { get; set; }
    public double CoveragePercent { get; set; } // % of executions following this path
    public long AverageDurationMs { get; set; }
    public long MinDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public string Classification { get; set; } = string.Empty; // common, rare, anomalous
}

/// <summary>
/// Process flow discovery result
/// </summary>
public class ProcessFlowDiscovery
{
    public string DiscoveryId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public int TotalCases { get; set; }
    public int UniquActivities { get; set; }
    public List<ControlFlowEdge> ControlFlowEdges { get; set; } = new();
    public List<ProcessVariant> DiscoveredVariants { get; set; } = new();
    public Dictionary<string, int> ActivityFrequency { get; set; } = new();
    public double ProcessComplexity { get; set; } // 0-100
}

/// <summary>
/// Activity performance metrics
/// </summary>
public class ActivityPerformance
{
    public string ActivityName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public long AverageDurationMs { get; set; }
    public long MedianDurationMs { get; set; }
    public long P95DurationMs { get; set; }
    public double SuccessRate { get; set; }
    public double ErrorRate { get; set; }
    public string PerformanceStatus { get; set; } = string.Empty; // good, acceptable, poor, critical
}

/// <summary>
/// Conformance check result
/// </summary>
public class ConformanceCheckResult
{
    public string CheckId { get; set; } = Guid.NewGuid().ToString();
    public string CaseId { get; set; } = string.Empty;
    public bool IsConforming { get; set; }
    public List<string> DeviationActivities { get; set; } = new();
    public List<string> UnexpectedTransitions { get; set; } = new();
    public double ConformanceScore { get; set; } // 0-100
    public string DeviationType { get; set; } = string.Empty; // extra_activity, missing_activity, wrong_sequence
}

/// <summary>
/// Process mining interface
/// </summary>
public interface IProcessMiningEngine
{
    // Event logging
    Task<ProcessEvent> LogActivityAsync(
        string caseId,
        string activityName,
        string status,
        long durationMs,
        CancellationToken ct = default);

    Task<List<ProcessEvent>> GetCaseEventsAsync(
        string caseId,
        CancellationToken ct = default);

    // Process discovery
    Task<ProcessFlowDiscovery> DiscoverProcessFlowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<List<ProcessVariant>> DiscoverVariantsAsync(
        string workflowId,
        CancellationToken ct = default);

    // Activity analysis
    Task<ActivityPerformance> AnalyzeActivityPerformanceAsync(
        string workflowId,
        string activityName,
        CancellationToken ct = default);

    Task<List<ActivityPerformance>> AnalyzeAllActivitiesAsync(
        string workflowId,
        CancellationToken ct = default);

    // Conformance checking
    Task<ConformanceCheckResult> CheckConformanceAsync(
        string caseId,
        List<string> expectedSequence,
        CancellationToken ct = default);

    Task<List<ConformanceCheckResult>> CheckTenantConformanceAsync(
        string tenantId,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetProcessMiningAnalyticsAsync(
        string workflowId,
        CancellationToken ct = default);
}

/// <summary>
/// Process mining engine implementation
/// </summary>
public class ProcessMiningEngine : IProcessMiningEngine
{
    private readonly ILogger<ProcessMiningEngine> _logger;
    private readonly Dictionary<string, List<ProcessEvent>> _eventLogs;
    private readonly Dictionary<string, ProcessFlowDiscovery> _discoveries;
    private readonly Dictionary<string, List<ConformanceCheckResult>> _conformanceResults;

    public ProcessMiningEngine(ILogger<ProcessMiningEngine> logger)
    {
        _logger = logger;
        _eventLogs = new Dictionary<string, List<ProcessEvent>>();
        _discoveries = new Dictionary<string, ProcessFlowDiscovery>();
        _conformanceResults = new Dictionary<string, List<ConformanceCheckResult>>();
    }

    // Event logging
    public async Task<ProcessEvent> LogActivityAsync(
        string caseId,
        string activityName,
        string status,
        long durationMs,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var processEvent = new ProcessEvent
        {
            CaseId = caseId,
            ActivityName = activityName,
            Status = status,
            DurationMs = durationMs,
            Attributes = new Dictionary<string, object>
            {
                ["case_id"] = caseId,
                ["activity"] = activityName,
                ["status"] = status
            }
        };

        if (!_eventLogs.ContainsKey(caseId))
        {
            _eventLogs[caseId] = new List<ProcessEvent>();
        }

        _eventLogs[caseId].Add(processEvent);

        _logger.LogDebug(
            "Activity logged: CaseId={CaseId}, Activity={Activity}, Status={Status}, Duration={Duration}ms",
            caseId, activityName, status, durationMs);

        return processEvent;
    }

    public async Task<List<ProcessEvent>> GetCaseEventsAsync(
        string caseId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_eventLogs.TryGetValue(caseId, out var events))
        {
            return events.OrderBy(e => e.Timestamp).ToList();
        }

        return new List<ProcessEvent>();
    }

    // Process discovery
    public async Task<ProcessFlowDiscovery> DiscoverProcessFlowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate discovery

        var allEvents = _eventLogs.Values.SelectMany(e => e).ToList();
        var workflowEvents = allEvents.Where(e => e.ActivityName.Contains(workflowId.Substring(0, Math.Min(3, workflowId.Length)))).ToList();

        if (workflowEvents.Count == 0)
        {
            workflowEvents = allEvents.TakeLast(100).ToList();
        }

        var discovery = new ProcessFlowDiscovery
        {
            WorkflowId = workflowId,
            TotalCases = _eventLogs.Count,
            UniquActivities = workflowEvents.Select(e => e.ActivityName).Distinct().Count(),
            ControlFlowEdges = DiscoverControlFlowEdges(workflowEvents),
            DiscoveredVariants = DiscoverVariantsInternal(workflowEvents),
            ActivityFrequency = workflowEvents
                .GroupBy(e => e.ActivityName)
                .ToDictionary(g => g.Key, g => g.Count()),
            ProcessComplexity = CalculateProcessComplexity(workflowEvents)
        };

        _discoveries[workflowId] = discovery;

        _logger.LogInformation(
            "Process flow discovered: WorkflowId={WorkflowId}, Activities={Activities}, Variants={Variants}, Complexity={Complexity:F1}%",
            workflowId, discovery.UniquActivities, discovery.DiscoveredVariants.Count, discovery.ProcessComplexity);

        return discovery;
    }

    public async Task<List<ProcessVariant>> DiscoverVariantsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var discovery = await DiscoverProcessFlowAsync(workflowId, ct);
        return discovery.DiscoveredVariants.OrderByDescending(v => v.Frequency).ToList();
    }

    // Activity analysis
    public async Task<ActivityPerformance> AnalyzeActivityPerformanceAsync(
        string workflowId,
        string activityName,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var activityEvents = _eventLogs.Values
            .SelectMany(e => e)
            .Where(e => e.ActivityName == activityName)
            .ToList();

        if (activityEvents.Count == 0)
        {
            return new ActivityPerformance { ActivityName = activityName };
        }

        var durations = activityEvents.Select(e => e.DurationMs).OrderBy(d => d).ToList();
        var successCount = activityEvents.Count(e => e.Status == "complete");

        return new ActivityPerformance
        {
            ActivityName = activityName,
            ExecutionCount = activityEvents.Count,
            AverageDurationMs = (long)durations.Average(),
            MedianDurationMs = durations[durations.Count / 2],
            P95DurationMs = durations[(int)(durations.Count * 0.95)],
            SuccessRate = (successCount / (double)activityEvents.Count) * 100,
            ErrorRate = (activityEvents.Count - successCount) / (double)activityEvents.Count * 100,
            PerformanceStatus = ClassifyPerformance((long)durations.Average())
        };
    }

    public async Task<List<ActivityPerformance>> AnalyzeAllActivitiesAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        var allEvents = _eventLogs.Values.SelectMany(e => e).ToList();
        var activities = allEvents.Select(e => e.ActivityName).Distinct().ToList();

        var performances = new List<ActivityPerformance>();
        foreach (var activity in activities)
        {
            var perf = await AnalyzeActivityPerformanceAsync(workflowId, activity, ct);
            performances.Add(perf);
        }

        return performances.OrderByDescending(p => p.ExecutionCount).ToList();
    }

    // Conformance checking
    public async Task<ConformanceCheckResult> CheckConformanceAsync(
        string caseId,
        List<string> expectedSequence,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var caseEvents = await GetCaseEventsAsync(caseId, ct);
        var actualSequence = caseEvents.Select(e => e.ActivityName).ToList();

        var result = new ConformanceCheckResult
        {
            CaseId = caseId,
            IsConforming = SequencesMatch(actualSequence, expectedSequence),
            DeviationActivities = actualSequence.Except(expectedSequence).ToList(),
            UnexpectedTransitions = FindUnexpectedTransitions(actualSequence, expectedSequence),
            ConformanceScore = CalculateConformanceScore(actualSequence, expectedSequence),
            DeviationType = DeterminDeviationType(actualSequence, expectedSequence)
        };

        var tenantKey = caseId.Split('_')[0];
        if (!_conformanceResults.ContainsKey(tenantKey))
        {
            _conformanceResults[tenantKey] = new List<ConformanceCheckResult>();
        }

        _conformanceResults[tenantKey].Add(result);

        _logger.LogInformation(
            "Conformance checked: CaseId={CaseId}, IsConforming={IsConforming}, Score={Score:F1}%",
            caseId, result.IsConforming, result.ConformanceScore);

        return result;
    }

    public async Task<List<ConformanceCheckResult>> CheckTenantConformanceAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_conformanceResults.TryGetValue(tenantId, out var results))
        {
            return results.OrderByDescending(r => r.Timestamp).ToList();
        }

        return new List<ConformanceCheckResult>();
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetProcessMiningAnalyticsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var discovery = _discoveries.TryGetValue(workflowId, out var d) ? d : null;
        var allConformance = _conformanceResults.Values.SelectMany(c => c).ToList();

        return new Dictionary<string, object>
        {
            ["total_cases"] = _eventLogs.Count,
            ["total_events"] = _eventLogs.Values.Sum(e => e.Count),
            ["unique_activities"] = discovery?.UniquActivities ?? 0,
            ["discovered_variants"] = discovery?.DiscoveredVariants.Count ?? 0,
            ["process_complexity"] = discovery?.ProcessComplexity ?? 0,
            ["conforming_cases"] = allConformance.Count(c => c.IsConforming),
            ["non_conforming_cases"] = allConformance.Count(c => !c.IsConforming),
            ["average_conformance_score"] = allConformance.Count > 0 ? allConformance.Average(c => c.ConformanceScore) : 0,
            ["control_flow_edges"] = discovery?.ControlFlowEdges.Count ?? 0
        };
    }

    // Helper methods
    private List<ControlFlowEdge> DiscoverControlFlowEdges(List<ProcessEvent> events)
    {
        var edges = new Dictionary<string, (int frequency, long totalDuration)>();

        var cases = events.GroupBy(e => e.CaseId);
        foreach (var caseEvents in cases)
        {
            var ordered = caseEvents.OrderBy(e => e.Timestamp).ToList();
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                var key = $"{ordered[i].ActivityName}->{ordered[i + 1].ActivityName}";
                if (!edges.ContainsKey(key))
                {
                    edges[key] = (0, 0);
                }
                var (freq, dur) = edges[key];
                edges[key] = (freq + 1, dur + ordered[i + 1].DurationMs);
            }
        }

        var totalTransitions = edges.Values.Sum(e => e.frequency);

        return edges.Select(e =>
        {
            var parts = e.Key.Split("->");
            return new ControlFlowEdge
            {
                SourceActivity = parts[0],
                TargetActivity = parts[1],
                Frequency = e.Value.frequency,
                Confidence = (e.Value.frequency / (double)totalTransitions) * 100,
                AverageDurationMs = e.Value.frequency > 0 ? e.Value.totalDuration / e.Value.frequency : 0
            };
        }).ToList();
    }

    private List<ProcessVariant> DiscoverVariantsInternal(List<ProcessEvent> events)
    {
        var variants = new Dictionary<string, (int frequency, long totalDuration, long minDuration, long maxDuration)>();

        var cases = events.GroupBy(e => e.CaseId);
        foreach (var caseEvents in cases)
        {
            var sequence = string.Join("->", caseEvents.OrderBy(e => e.Timestamp).Select(e => e.ActivityName));
            var duration = caseEvents.Sum(e => e.DurationMs);

            if (!variants.ContainsKey(sequence))
            {
                variants[sequence] = (0, 0, long.MaxValue, 0);
            }

            var (freq, total, minDur, maxDur) = variants[sequence];
            variants[sequence] = (
                freq + 1,
                total + duration,
                Math.Min(minDur, duration),
                Math.Max(maxDur, duration)
            );
        }

        var totalCases = cases.Count();

        return variants.Select((v, idx) =>
        {
            var (frequency, totalDuration, minDuration, maxDuration) = v.Value;
            var activities = v.Key.Split("->").ToList();

            return new ProcessVariant
            {
                VariantId = $"var_{idx}",
                ActivitySequence = activities,
                Frequency = frequency,
                CoveragePercent = (frequency / (double)totalCases) * 100,
                AverageDurationMs = frequency > 0 ? totalDuration / frequency : 0,
                MinDurationMs = minDuration == long.MaxValue ? 0 : minDuration,
                MaxDurationMs = maxDuration,
                Classification = frequency / (double)totalCases > 0.3 ? "common" : frequency / (double)totalCases > 0.05 ? "rare" : "anomalous"
            };
        }).ToList();
    }

    private double CalculateProcessComplexity(List<ProcessEvent> events)
    {
        var uniqueActivities = events.Select(e => e.ActivityName).Distinct().Count();
        var transitions = DiscoverControlFlowEdges(events).Count;
        var cases = events.GroupBy(e => e.CaseId).Count();

        // Complexity = (activities * transitions) / cases, normalized to 0-100
        var complexity = (uniqueActivities * transitions) / Math.Max(1.0, cases) * 5;
        return Math.Min(100, complexity);
    }

    private string ClassifyPerformance(long durationMs)
    {
        return durationMs switch
        {
            < 1000 => "good",
            < 3000 => "acceptable",
            < 7000 => "poor",
            _ => "critical"
        };
    }

    private bool SequencesMatch(List<string> actual, List<string> expected)
    {
        if (actual.Count != expected.Count)
            return false;

        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] != expected[i])
                return false;
        }

        return true;
    }

    private List<string> FindUnexpectedTransitions(List<string> actual, List<string> expected)
    {
        var unexpectedTransitions = new List<string>();

        for (int i = 0; i < Math.Min(actual.Count - 1, expected.Count - 1); i++)
        {
            var actualTransition = $"{actual[i]}->{actual[i + 1]}";
            var expectedTransition = $"{expected[i]}->{expected[i + 1]}";

            if (actualTransition != expectedTransition)
            {
                unexpectedTransitions.Add(actualTransition);
            }
        }

        return unexpectedTransitions;
    }

    private double CalculateConformanceScore(List<string> actual, List<string> expected)
    {
        var matches = 0;
        var maxLen = Math.Max(actual.Count, expected.Count);

        for (int i = 0; i < Math.Min(actual.Count, expected.Count); i++)
        {
            if (actual[i] == expected[i])
                matches++;
        }

        return (matches / (double)maxLen) * 100;
    }

    private string DeterminDeviationType(List<string> actual, List<string> expected)
    {
        if (actual.Count > expected.Count)
            return "extra_activity";
        else if (actual.Count < expected.Count)
            return "missing_activity";
        else
            return "wrong_sequence";
    }
}

/// <summary>
/// Extension for conformance result timestamp
/// </summary>
public static class ConformanceCheckResultExtension
{
    public static DateTime Timestamp(this ConformanceCheckResult result)
    {
        return DateTime.UtcNow;
    }
}
