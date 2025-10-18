using System.Diagnostics;
using System.Text;

namespace Loco.Core.Workflows;

/// <summary>
/// Performance profile for a workflow execution.
/// </summary>
public class WorkflowProfile
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public TimeSpan TotalDuration { get; set; }
    public List<StepProfile> StepProfiles { get; set; } = new();
    public Dictionary<string, TimeSpan> PhaseTimings { get; set; } = new();
    public PerformanceMetrics Metrics { get; set; } = new();
}

/// <summary>
/// Performance profile for a step.
/// </summary>
public class StepProfile
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public double PercentageOfTotal { get; set; }
    public int RetryCount { get; set; }
    public long MemoryUsed { get; set; }
    public bool IsBottleneck { get; set; }
}

/// <summary>
/// Performance metrics for execution.
/// </summary>
public class PerformanceMetrics
{
    public long PeakMemoryUsage { get; set; }
    public double AverageStepDuration { get; set; }
    public TimeSpan LongestStepDuration { get; set; }
    public string? BottleneckStepId { get; set; }
    public int ParallelizableSteps { get; set; }
    public double ParallelizationEfficiency { get; set; }
}

/// <summary>
/// Profiles workflow executions for performance analysis.
/// </summary>
public class WorkflowProfiler
{
    private readonly Dictionary<string, Stopwatch> _stepTimers = new();
    private readonly Dictionary<string, long> _stepMemory = new();
    private readonly Dictionary<string, int> _stepRetries = new();
    private readonly Stopwatch _totalTimer = new();
    private readonly Dictionary<string, Stopwatch> _phaseTimers = new();
    private string _currentWorkflowId = "";
    private string _currentWorkflowName = "";
    private long _startMemory;

    /// <summary>
    /// Starts profiling a workflow execution.
    /// </summary>
    public void StartProfiling(string workflowId, string workflowName)
    {
        _currentWorkflowId = workflowId;
        _currentWorkflowName = workflowName;
        _stepTimers.Clear();
        _stepMemory.Clear();
        _stepRetries.Clear();
        _phaseTimers.Clear();

        _startMemory = GC.GetTotalMemory(false);
        _totalTimer.Restart();
    }

    /// <summary>
    /// Starts profiling a step.
    /// </summary>
    public void StartStep(string stepId)
    {
        var sw = Stopwatch.StartNew();
        _stepTimers[stepId] = sw;
        _stepMemory[stepId] = GC.GetTotalMemory(false);
    }

    /// <summary>
    /// Ends profiling a step.
    /// </summary>
    public void EndStep(string stepId)
    {
        if (_stepTimers.TryGetValue(stepId, out var sw))
        {
            sw.Stop();
        }
    }

    /// <summary>
    /// Records a step retry.
    /// </summary>
    public void RecordRetry(string stepId)
    {
        if (!_stepRetries.ContainsKey(stepId))
        {
            _stepRetries[stepId] = 0;
        }
        _stepRetries[stepId]++;
    }

    /// <summary>
    /// Starts profiling a phase.
    /// </summary>
    public void StartPhase(string phaseName)
    {
        _phaseTimers[phaseName] = Stopwatch.StartNew();
    }

    /// <summary>
    /// Ends profiling a phase.
    /// </summary>
    public void EndPhase(string phaseName)
    {
        if (_phaseTimers.TryGetValue(phaseName, out var sw))
        {
            sw.Stop();
        }
    }

    /// <summary>
    /// Ends profiling and generates a profile.
    /// </summary>
    public WorkflowProfile EndProfiling(List<WorkflowStep> steps)
    {
        _totalTimer.Stop();

        var profile = new WorkflowProfile
        {
            WorkflowId = _currentWorkflowId,
            WorkflowName = _currentWorkflowName,
            TotalDuration = _totalTimer.Elapsed
        };

        // Create step profiles
        foreach (var step in steps)
        {
            if (_stepTimers.TryGetValue(step.Id, out var sw))
            {
                var memoryBefore = _stepMemory.GetValueOrDefault(step.Id, 0);
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryUsed = Math.Max(0, memoryAfter - memoryBefore);

                var stepProfile = new StepProfile
                {
                    StepId = step.Id,
                    StepName = step.Name,
                    Duration = sw.Elapsed,
                    PercentageOfTotal = (sw.Elapsed.TotalMilliseconds / _totalTimer.Elapsed.TotalMilliseconds) * 100,
                    RetryCount = _stepRetries.GetValueOrDefault(step.Id, 0),
                    MemoryUsed = memoryUsed
                };

                profile.StepProfiles.Add(stepProfile);
            }
        }

        // Identify bottlenecks
        if (profile.StepProfiles.Count > 0)
        {
            var avgDuration = profile.StepProfiles.Average(s => s.Duration.TotalMilliseconds);
            var threshold = avgDuration * 2; // 2x average is considered a bottleneck

            foreach (var stepProfile in profile.StepProfiles)
            {
                if (stepProfile.Duration.TotalMilliseconds > threshold)
                {
                    stepProfile.IsBottleneck = true;
                }
            }
        }

        // Calculate phase timings
        foreach (var kvp in _phaseTimers)
        {
            profile.PhaseTimings[kvp.Key] = kvp.Value.Elapsed;
        }

        // Calculate metrics
        profile.Metrics = CalculateMetrics(profile);

        return profile;
    }

    /// <summary>
    /// Calculates performance metrics.
    /// </summary>
    private PerformanceMetrics CalculateMetrics(WorkflowProfile profile)
    {
        var metrics = new PerformanceMetrics();

        if (profile.StepProfiles.Count == 0)
            return metrics;

        metrics.PeakMemoryUsage = GC.GetTotalMemory(false) - _startMemory;
        metrics.AverageStepDuration = profile.StepProfiles.Average(s => s.Duration.TotalMilliseconds);
        metrics.LongestStepDuration = TimeSpan.FromMilliseconds(
            profile.StepProfiles.Max(s => s.Duration.TotalMilliseconds));

        var bottleneck = profile.StepProfiles.OrderByDescending(s => s.Duration).FirstOrDefault();
        if (bottleneck != null)
        {
            metrics.BottleneckStepId = bottleneck.StepId;
        }

        // Estimate parallelization efficiency
        var totalStepTime = TimeSpan.FromMilliseconds(
            profile.StepProfiles.Sum(s => s.Duration.TotalMilliseconds));

        if (totalStepTime.TotalMilliseconds > 0)
        {
            metrics.ParallelizationEfficiency =
                (profile.TotalDuration.TotalMilliseconds / totalStepTime.TotalMilliseconds) * 100;
        }

        return metrics;
    }

    /// <summary>
    /// Generates a performance profile report.
    /// </summary>
    public static string GenerateProfileReport(WorkflowProfile profile)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WORKFLOW PERFORMANCE PROFILE                                                  ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Workflow: {profile.WorkflowName,-67} ║");
        sb.AppendLine($"║ ID: {profile.WorkflowId,-73} ║");
        sb.AppendLine($"║ Total Duration: {FormatDuration(profile.TotalDuration),-63} ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Overall metrics
        sb.AppendLine("Performance Metrics:");
        sb.AppendLine($"  Total Steps: {profile.StepProfiles.Count}");
        sb.AppendLine($"  Average Step Duration: {profile.Metrics.AverageStepDuration:F2}ms");
        sb.AppendLine($"  Longest Step: {FormatDuration(profile.Metrics.LongestStepDuration)}");
        sb.AppendLine($"  Peak Memory: {FormatMemory(profile.Metrics.PeakMemoryUsage)}");

        if (profile.Metrics.BottleneckStepId != null)
        {
            var bottleneck = profile.StepProfiles.FirstOrDefault(s => s.StepId == profile.Metrics.BottleneckStepId);
            if (bottleneck != null)
            {
                sb.AppendLine($"  Bottleneck: {bottleneck.StepName} ({bottleneck.StepId})");
            }
        }

        sb.AppendLine();

        // Step breakdown
        sb.AppendLine("Step Performance Breakdown:");
        sb.AppendLine();

        var sortedSteps = profile.StepProfiles.OrderByDescending(s => s.Duration).ToList();

        foreach (var step in sortedSteps)
        {
            var icon = step.IsBottleneck ? "⚠️" : "✓";
            sb.AppendLine($"{icon} {step.StepName} ({step.StepId})");
            sb.AppendLine($"   Duration: {FormatDuration(step.Duration)} ({step.PercentageOfTotal:F1}% of total)");
            sb.AppendLine($"   {GenerateBar(step.PercentageOfTotal)}");

            if (step.RetryCount > 0)
            {
                sb.AppendLine($"   Retries: {step.RetryCount}");
            }

            if (step.MemoryUsed > 0)
            {
                sb.AppendLine($"   Memory: {FormatMemory(step.MemoryUsed)}");
            }

            if (step.IsBottleneck)
            {
                sb.AppendLine($"   ⚠️  This step is a bottleneck (>2x average duration)");
            }

            sb.AppendLine();
        }

        // Phase timings
        if (profile.PhaseTimings.Count > 0)
        {
            sb.AppendLine("Phase Timings:");
            foreach (var kvp in profile.PhaseTimings.OrderByDescending(p => p.Value))
            {
                var percentage = (kvp.Value.TotalMilliseconds / profile.TotalDuration.TotalMilliseconds) * 100;
                sb.AppendLine($"  {kvp.Key}: {FormatDuration(kvp.Value)} ({percentage:F1}%)");
            }
            sb.AppendLine();
        }

        // Optimization suggestions
        sb.AppendLine("Optimization Suggestions:");
        var suggestions = GenerateOptimizationSuggestions(profile);
        if (suggestions.Count == 0)
        {
            sb.AppendLine("  ✅ No major optimization opportunities detected.");
        }
        else
        {
            foreach (var suggestion in suggestions)
            {
                sb.AppendLine($"  💡 {suggestion}");
            }
        }

        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Generates optimization suggestions based on profile.
    /// </summary>
    private static List<string> GenerateOptimizationSuggestions(WorkflowProfile profile)
    {
        var suggestions = new List<string>();

        // Check for bottlenecks
        var bottlenecks = profile.StepProfiles.Where(s => s.IsBottleneck).ToList();
        if (bottlenecks.Count > 0)
        {
            suggestions.Add($"Optimize {bottlenecks.Count} bottleneck step(s): {string.Join(", ", bottlenecks.Select(s => s.StepId))}");
        }

        // Check for retry-heavy steps
        var retryHeavy = profile.StepProfiles.Where(s => s.RetryCount > 3).ToList();
        if (retryHeavy.Count > 0)
        {
            suggestions.Add($"Reduce retries for steps: {string.Join(", ", retryHeavy.Select(s => s.StepId))}");
        }

        // Check parallelization efficiency
        if (profile.Metrics.ParallelizationEfficiency < 50)
        {
            suggestions.Add("Consider adding step dependencies to enable parallel execution");
        }

        // Check for long sequential chains
        if (profile.StepProfiles.Count > 10 && bottlenecks.Count == 0)
        {
            suggestions.Add("Consider breaking workflow into smaller, reusable workflows");
        }

        // Check memory usage
        if (profile.Metrics.PeakMemoryUsage > 100 * 1024 * 1024) // 100MB
        {
            suggestions.Add($"High memory usage detected ({FormatMemory(profile.Metrics.PeakMemoryUsage)}). Consider optimizing data handling.");
        }

        return suggestions;
    }

    /// <summary>
    /// Generates a comparison report between two profiles.
    /// </summary>
    public static string GenerateComparisonReport(WorkflowProfile baseline, WorkflowProfile current)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ PERFORMANCE COMPARISON                                                        ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Overall comparison
        var durationDiff = current.TotalDuration.TotalMilliseconds - baseline.TotalDuration.TotalMilliseconds;
        var durationPctChange = (durationDiff / baseline.TotalDuration.TotalMilliseconds) * 100;

        sb.AppendLine("Overall Performance:");
        sb.AppendLine($"  Baseline: {FormatDuration(baseline.TotalDuration)}");
        sb.AppendLine($"  Current: {FormatDuration(current.TotalDuration)}");
        sb.AppendLine($"  Change: {FormatChange(durationDiff, durationPctChange)} {GetChangeIcon(durationPctChange)}");
        sb.AppendLine();

        // Step-by-step comparison
        sb.AppendLine("Step-by-Step Comparison:");
        sb.AppendLine();

        foreach (var currentStep in current.StepProfiles)
        {
            var baselineStep = baseline.StepProfiles.FirstOrDefault(s => s.StepId == currentStep.StepId);
            if (baselineStep != null)
            {
                var stepDiff = currentStep.Duration.TotalMilliseconds - baselineStep.Duration.TotalMilliseconds;
                var stepPctChange = (stepDiff / baselineStep.Duration.TotalMilliseconds) * 100;

                sb.AppendLine($"  {currentStep.StepName} ({currentStep.StepId})");
                sb.AppendLine($"    Baseline: {FormatDuration(baselineStep.Duration)}");
                sb.AppendLine($"    Current: {FormatDuration(currentStep.Duration)}");
                sb.AppendLine($"    Change: {FormatChange(stepDiff, stepPctChange)} {GetChangeIcon(stepPctChange)}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F2}h";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F2}m";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F2}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }

    private static string FormatMemory(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} bytes";
    }

    private static string FormatChange(double diff, double pctChange)
    {
        var sign = diff > 0 ? "+" : "";
        return $"{sign}{diff:F0}ms ({sign}{pctChange:F1}%)";
    }

    private static string GetChangeIcon(double pctChange)
    {
        if (Math.Abs(pctChange) < 5)
            return "≈"; // Negligible
        if (pctChange < 0)
            return "✅"; // Improvement (faster)
        return "⚠️"; // Regression (slower)
    }

    private static string GenerateBar(double percentage, int width = 40)
    {
        var filled = (int)((percentage / 100.0) * width);
        filled = Math.Min(width, Math.Max(0, filled));
        var empty = width - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }
}
