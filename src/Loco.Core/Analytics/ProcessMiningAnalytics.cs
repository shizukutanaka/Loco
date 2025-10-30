using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Loco.Core.Workflow;
using Loco.Core.Debugging;

namespace Loco.Core.Analytics;

/// <summary>
/// Process mining and analytics for workflow optimization
/// Based on multilingual research 2024/2025:
/// - RPA trends: Process mining integration to identify inefficiencies
/// - German research: 80% organizations adopt intelligent automation by 2025
/// - Chinese research: Real-time monitoring and feedback for business processes
/// - Hyperautomation: RPA + AI + ML + Analytics
/// - ROI 30-200% (first year), up to 300% long-term
/// - Cost reduction: 30-80% operational costs
/// Identifies bottlenecks, redundancies, and automation opportunities
/// </summary>
public class ProcessMiningAnalytics
{
    private readonly ILogger<ProcessMiningAnalytics> _logger;
    private readonly ConcurrentDictionary<string, ProcessModel> _processModels;
    private readonly ConcurrentDictionary<string, List<ProcessInstance>> _instances;

    public ProcessMiningAnalytics(ILogger<ProcessMiningAnalytics> logger)
    {
        _logger = logger;
        _processModels = new ConcurrentDictionary<string, ProcessModel>();
        _instances = new ConcurrentDictionary<string, List<ProcessInstance>>();
    }

    /// <summary>
    /// Discover process model from execution traces
    /// Process mining: extract actual process flow from execution data
    /// </summary>
    public ProcessModel DiscoverProcess(string workflowId, List<WorkflowExecutionTrace> traces)
    {
        _logger.LogInformation("Discovering process model for workflow {WorkflowId} from {TraceCount} traces",
            workflowId, traces.Count);

        var model = new ProcessModel
        {
            WorkflowId = workflowId,
            DiscoveryDate = DateTime.UtcNow,
            TotalInstances = traces.Count
        };

        // Extract activities and transitions
        var activities = new Dictionary<string, ActivityNode>();
        var transitions = new Dictionary<string, TransitionEdge>();

        foreach (var trace in traces)
        {
            var instance = new ProcessInstance
            {
                InstanceId = trace.SessionId,
                StartTime = trace.StartTime,
                Steps = trace.Steps.Select(s => new ProcessStep
                {
                    ActivityId = s.ActionId,
                    ActivityType = s.ActionType,
                    Timestamp = s.Timestamp,
                    Duration = s.Duration ?? TimeSpan.Zero,
                    Status = s.Status.ToString()
                }).ToList()
            };

            model.Instances.Add(instance);

            // Build activity nodes
            foreach (var step in trace.Steps)
            {
                if (!activities.ContainsKey(step.ActionId))
                {
                    activities[step.ActionId] = new ActivityNode
                    {
                        ActivityId = step.ActionId,
                        ActivityType = step.ActionType,
                        Frequency = 0,
                        AverageDuration = TimeSpan.Zero
                    };
                }

                activities[step.ActionId].Frequency++;
                if (step.Duration.HasValue)
                {
                    activities[step.ActionId].TotalDuration += step.Duration.Value;
                }
            }

            // Build transitions (edges between activities)
            for (int i = 0; i < trace.Steps.Count - 1; i++)
            {
                var from = trace.Steps[i].ActionId;
                var to = trace.Steps[i + 1].ActionId;
                var transitionKey = $"{from}->{to}";

                if (!transitions.ContainsKey(transitionKey))
                {
                    transitions[transitionKey] = new TransitionEdge
                    {
                        FromActivity = from,
                        ToActivity = to,
                        Frequency = 0
                    };
                }

                transitions[transitionKey].Frequency++;
            }
        }

        // Calculate averages
        foreach (var activity in activities.Values)
        {
            activity.AverageDuration = activity.Frequency > 0
                ? TimeSpan.FromTicks(activity.TotalDuration.Ticks / activity.Frequency)
                : TimeSpan.Zero;
        }

        model.Activities = activities.Values.ToList();
        model.Transitions = transitions.Values.ToList();

        _processModels[workflowId] = model;

        _logger.LogInformation("Discovered {ActivityCount} activities and {TransitionCount} transitions",
            model.Activities.Count, model.Transitions.Count);

        return model;
    }

    /// <summary>
    /// Analyze process for bottlenecks and inefficiencies
    /// RPA trend: Identify inefficiencies and optimize automation efforts
    /// </summary>
    public ProcessAnalysisReport AnalyzeProcess(string workflowId)
    {
        if (!_processModels.TryGetValue(workflowId, out var model))
        {
            throw new InvalidOperationException($"Process model not found for workflow {workflowId}");
        }

        _logger.LogInformation("Analyzing process for workflow {WorkflowId}", workflowId);

        var report = new ProcessAnalysisReport
        {
            WorkflowId = workflowId,
            AnalysisDate = DateTime.UtcNow
        };

        // Detect bottlenecks (activities with high average duration)
        var avgDuration = model.Activities.Average(a => a.AverageDuration.TotalMilliseconds);
        var bottleneckThreshold = avgDuration * 2; // 2x average is a bottleneck

        report.Bottlenecks = model.Activities
            .Where(a => a.AverageDuration.TotalMilliseconds > bottleneckThreshold)
            .Select(a => new BottleneckAnalysis
            {
                ActivityId = a.ActivityId,
                ActivityType = a.ActivityType,
                AverageDuration = a.AverageDuration,
                Frequency = a.Frequency,
                ImpactScore = CalculateImpactScore(a.AverageDuration, a.Frequency, avgDuration),
                Recommendation = GenerateBottleneckRecommendation(a)
            })
            .OrderByDescending(b => b.ImpactScore)
            .ToList();

        // Detect redundant activities (same type executed multiple times)
        var redundantGroups = model.Activities
            .GroupBy(a => a.ActivityType)
            .Where(g => g.Count() > 1)
            .Select(g => new RedundancyAnalysis
            {
                ActivityType = g.Key,
                Count = g.Count(),
                TotalDuration = TimeSpan.FromTicks(g.Sum(a => a.TotalDuration.Ticks)),
                Recommendation = $"Consider consolidating {g.Count()} instances of '{g.Key}' into a single optimized action"
            })
            .ToList();

        report.Redundancies = redundantGroups;

        // Calculate process metrics
        report.Metrics = new ProcessMetrics
        {
            AverageCycleTime = CalculateAverageCycleTime(model),
            AverageThroughput = CalculateThroughput(model),
            ProcessEfficiency = CalculateEfficiency(model),
            BottleneckCount = report.Bottlenecks.Count,
            RedundancyCount = report.Redundancies.Count
        };

        // Generate automation opportunities
        report.AutomationOpportunities = IdentifyAutomationOpportunities(model);

        // Calculate potential ROI
        report.EstimatedROI = CalculateROI(report);

        _logger.LogInformation("Analysis complete: {BottleneckCount} bottlenecks, {RedundancyCount} redundancies, {OpportunityCount} automation opportunities",
            report.Bottlenecks.Count, report.Redundancies.Count, report.AutomationOpportunities.Count);

        return report;
    }

    /// <summary>
    /// Compare two process models (conformance checking)
    /// German research: Process optimization and standardization
    /// </summary>
    public ConformanceReport CheckConformance(
        ProcessModel designedModel,
        ProcessModel actualModel)
    {
        var report = new ConformanceReport
        {
            DesignedModelId = designedModel.WorkflowId,
            ActualModelId = actualModel.WorkflowId,
            CheckDate = DateTime.UtcNow
        };

        // Find deviations
        var deviations = new List<ProcessDeviation>();

        // Check for missing activities
        var missingActivities = designedModel.Activities
            .Where(d => !actualModel.Activities.Any(a => a.ActivityId == d.ActivityId))
            .Select(d => new ProcessDeviation
            {
                Type = DeviationType.MissingActivity,
                Description = $"Activity '{d.ActivityId}' ({d.ActivityType}) is in designed model but not executed",
                Severity = DeviationSeverity.High
            });

        deviations.AddRange(missingActivities);

        // Check for extra activities
        var extraActivities = actualModel.Activities
            .Where(a => !designedModel.Activities.Any(d => d.ActivityId == a.ActivityId))
            .Select(a => new ProcessDeviation
            {
                Type = DeviationType.ExtraActivity,
                Description = $"Activity '{a.ActivityId}' ({a.ActivityType}) is executed but not in designed model",
                Severity = DeviationSeverity.Medium
            });

        deviations.AddRange(extraActivities);

        // Check for wrong transitions
        var wrongTransitions = actualModel.Transitions
            .Where(t => !designedModel.Transitions.Any(d =>
                d.FromActivity == t.FromActivity && d.ToActivity == t.ToActivity))
            .Select(t => new ProcessDeviation
            {
                Type = DeviationType.WrongTransition,
                Description = $"Unexpected transition from '{t.FromActivity}' to '{t.ToActivity}'",
                Severity = DeviationSeverity.Low
            });

        deviations.AddRange(wrongTransitions);

        report.Deviations = deviations;
        report.ConformanceScore = CalculateConformanceScore(designedModel, actualModel, deviations);

        return report;
    }

    /// <summary>
    /// Generate visual process map
    /// Chinese research: Visual monitoring for business processes
    /// </summary>
    public string GenerateProcessMap(ProcessModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Process Map ===");
        sb.AppendLine();

        // Activity frequency heatmap
        sb.AppendLine("Activity Frequency Heatmap:");
        var maxFreq = model.Activities.Max(a => a.Frequency);
        foreach (var activity in model.Activities.OrderByDescending(a => a.Frequency))
        {
            var heatLevel = (int)((activity.Frequency / (double)maxFreq) * 10);
            var heatBar = new string('█', heatLevel) + new string('░', 10 - heatLevel);
            sb.AppendLine($"  {heatBar} {activity.ActivityType} ({activity.Frequency}x, avg {activity.AverageDuration.TotalMilliseconds:F0}ms)");
        }

        sb.AppendLine();
        sb.AppendLine("Process Flow:");

        // Build flow diagram
        var startActivities = model.Activities
            .Where(a => !model.Transitions.Any(t => t.ToActivity == a.ActivityId))
            .ToList();

        foreach (var start in startActivities)
        {
            DrawFlow(sb, model, start.ActivityId, new HashSet<string>(), 0);
        }

        return sb.ToString();
    }

    private void DrawFlow(StringBuilder sb, ProcessModel model, string activityId, HashSet<string> visited, int level)
    {
        if (visited.Contains(activityId))
        {
            sb.AppendLine($"{new string(' ', level * 2)}  (cycle detected)");
            return;
        }

        visited.Add(activityId);
        var activity = model.Activities.FirstOrDefault(a => a.ActivityId == activityId);
        if (activity == null) return;

        sb.AppendLine($"{new string(' ', level * 2)}→ {activity.ActivityType} ({activity.AverageDuration.TotalMilliseconds:F0}ms)");

        var outgoing = model.Transitions.Where(t => t.FromActivity == activityId).ToList();
        foreach (var transition in outgoing)
        {
            DrawFlow(sb, model, transition.ToActivity, new HashSet<string>(visited), level + 1);
        }
    }

    private double CalculateImpactScore(TimeSpan duration, int frequency, double avgDuration)
    {
        // Impact = (duration / avgDuration) * frequency
        return (duration.TotalMilliseconds / avgDuration) * frequency;
    }

    private string GenerateBottleneckRecommendation(ActivityNode activity)
    {
        return activity.ActivityType switch
        {
            "http_request" => "Consider caching responses, batching requests, or using a CDN",
            "file_operation" => "Consider async I/O, parallel processing, or SSD storage",
            "database_query" => "Consider indexing, query optimization, or caching",
            "notification" => "Consider batching notifications or using async delivery",
            _ => "Consider optimizing this activity or executing it asynchronously"
        };
    }

    private TimeSpan CalculateAverageCycleTime(ProcessModel model)
    {
        if (model.Instances.Count == 0) return TimeSpan.Zero;

        var cycleTimes = model.Instances
            .Where(i => i.EndTime.HasValue)
            .Select(i => i.EndTime!.Value - i.StartTime)
            .ToList();

        return cycleTimes.Count > 0
            ? TimeSpan.FromTicks((long)cycleTimes.Average(ct => ct.Ticks))
            : TimeSpan.Zero;
    }

    private double CalculateThroughput(ProcessModel model)
    {
        // Instances per hour
        if (model.Instances.Count == 0) return 0;

        var firstInstance = model.Instances.Min(i => i.StartTime);
        var lastInstance = model.Instances.Max(i => i.EndTime ?? i.StartTime);
        var duration = (lastInstance - firstInstance).TotalHours;

        return duration > 0 ? model.Instances.Count / duration : 0;
    }

    private double CalculateEfficiency(ProcessModel model)
    {
        // Efficiency = (actual time / ideal time) * 100
        // Ideal time = sum of minimum durations
        if (model.Instances.Count == 0) return 0;

        var actualTime = CalculateAverageCycleTime(model).TotalMilliseconds;
        var idealTime = model.Activities.Min(a => a.AverageDuration.TotalMilliseconds) * model.Activities.Count;

        return idealTime > 0 ? (idealTime / actualTime) * 100 : 0;
    }

    private List<AutomationOpportunity> IdentifyAutomationOpportunities(ProcessModel model)
    {
        var opportunities = new List<AutomationOpportunity>();

        // High-frequency activities are good automation candidates
        var highFreqActivities = model.Activities
            .Where(a => a.Frequency > model.TotalInstances * 0.8) // 80%+ occurrence
            .Select(a => new AutomationOpportunity
            {
                ActivityType = a.ActivityType,
                Frequency = a.Frequency,
                EstimatedTimeSavings = a.TotalDuration,
                Priority = AutomationPriority.High,
                Recommendation = $"High-frequency activity ({a.Frequency} occurrences) - automate to save {a.TotalDuration.TotalHours:F1} hours"
            });

        opportunities.AddRange(highFreqActivities);

        return opportunities;
    }

    private ROIEstimate CalculateROI(ProcessAnalysisReport report)
    {
        // Based on RPA research: 30-200% ROI first year, up to 300% long-term
        // Cost reduction: 30-80%

        var totalTimeSavings = report.AutomationOpportunities.Sum(o => o.EstimatedTimeSavings.TotalHours);
        var assumedHourlyRate = 50; // USD per hour
        var estimatedSavings = totalTimeSavings * assumedHourlyRate;

        return new ROIEstimate
        {
            EstimatedAnnualSavings = estimatedSavings * 12, // Monthly to annual
            EstimatedCostReduction = 0.5, // 50% (conservative estimate between 30-80%)
            ProjectedROI = 1.5, // 150% (conservative estimate between 30-200%)
            PaybackPeriodMonths = 6, // Typical RPA payback period
            TimeSavingsHoursPerMonth = totalTimeSavings
        };
    }

    private double CalculateConformanceScore(
        ProcessModel designed,
        ProcessModel actual,
        List<ProcessDeviation> deviations)
    {
        // Conformance score = 1 - (deviations / total elements)
        var totalElements = designed.Activities.Count + designed.Transitions.Count;
        var deviationCount = deviations.Count;

        return totalElements > 0 ? Math.Max(0, 1 - (deviationCount / (double)totalElements)) * 100 : 0;
    }
}

public class ProcessModel
{
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime DiscoveryDate { get; set; }
    public int TotalInstances { get; set; }
    public List<ActivityNode> Activities { get; set; } = new();
    public List<TransitionEdge> Transitions { get; set; } = new();
    public List<ProcessInstance> Instances { get; set; } = new();
}

public class ActivityNode
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

public class TransitionEdge
{
    public string FromActivity { get; set; } = string.Empty;
    public string ToActivity { get; set; } = string.Empty;
    public int Frequency { get; set; }
}

public class ProcessInstance
{
    public string InstanceId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ProcessStep> Steps { get; set; } = new();
}

public class ProcessStep
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ProcessAnalysisReport
{
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public List<BottleneckAnalysis> Bottlenecks { get; set; } = new();
    public List<RedundancyAnalysis> Redundancies { get; set; } = new();
    public List<AutomationOpportunity> AutomationOpportunities { get; set; } = new();
    public ProcessMetrics Metrics { get; set; } = new();
    public ROIEstimate EstimatedROI { get; set; } = new();
}

public class BottleneckAnalysis
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public TimeSpan AverageDuration { get; set; }
    public int Frequency { get; set; }
    public double ImpactScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class RedundancyAnalysis
{
    public string ActivityType { get; set; } = string.Empty;
    public int Count { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class AutomationOpportunity
{
    public string ActivityType { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public TimeSpan EstimatedTimeSavings { get; set; }
    public AutomationPriority Priority { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class ProcessMetrics
{
    public TimeSpan AverageCycleTime { get; set; }
    public double AverageThroughput { get; set; }
    public double ProcessEfficiency { get; set; }
    public int BottleneckCount { get; set; }
    public int RedundancyCount { get; set; }
}

public class ROIEstimate
{
    public double EstimatedAnnualSavings { get; set; }
    public double EstimatedCostReduction { get; set; }
    public double ProjectedROI { get; set; }
    public int PaybackPeriodMonths { get; set; }
    public double TimeSavingsHoursPerMonth { get; set; }
}

public class ConformanceReport
{
    public string DesignedModelId { get; set; } = string.Empty;
    public string ActualModelId { get; set; } = string.Empty;
    public DateTime CheckDate { get; set; }
    public List<ProcessDeviation> Deviations { get; set; } = new();
    public double ConformanceScore { get; set; }
}

public class ProcessDeviation
{
    public DeviationType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DeviationSeverity Severity { get; set; }
}

public enum AutomationPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum DeviationType
{
    MissingActivity,
    ExtraActivity,
    WrongTransition,
    IncorrectTiming
}

public enum DeviationSeverity
{
    Low,
    Medium,
    High,
    Critical
}
