#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.ChaosEngineering;

/// <summary>
/// Chaos Engineering Patterns
/// Resilience testing through controlled failure injection
/// </summary>

/// <summary>
/// Chaos experiment
/// </summary>
public class ChaosExperiment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public ChaosTarget Target { get; set; } = new();

    [JsonPropertyName("fault")]
    public ChaosFault Fault { get; set; } = new();

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("results")]
    public ChaosResults? Results { get; set; }
}

/// <summary>
/// Chaos experiment target
/// </summary>
public class ChaosTarget
{
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("podSelector")]
    public Dictionary<string, string> PodSelector { get; set; } = new();

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "All"; // All, One, Fixed, Percentage, Random-Max-Percentage
}

/// <summary>
/// Chaos fault types
/// </summary>
public class ChaosFault
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Pod, Network, CPU, Memory, IO, Time

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty; // Kill, Delay, Loss, Bandwidth, Stress, Clock

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Pod fault - pod termination
/// </summary>
public class PodFault : ChaosFault
{
    public PodFault()
    {
        Type = "Pod";
    }

    public void SetKill()
    {
        Action = "Kill";
    }
}

/// <summary>
/// Network fault - delay, loss, bandwidth limit
/// </summary>
public class NetworkFault : ChaosFault
{
    public NetworkFault()
    {
        Type = "Network";
    }

    public void SetLatency(int delayMs, int jitterMs = 0)
    {
        Action = "Delay";
        Parameters["delay"] = $"{delayMs}ms";
        Parameters["jitter"] = $"{jitterMs}ms";
    }

    public void SetPacketLoss(int percent)
    {
        Action = "Loss";
        Parameters["loss"] = $"{percent}%";
    }

    public void SetBandwidth(string limit)
    {
        Action = "Bandwidth";
        Parameters["limit"] = limit; // 1mbps, 100kbps
    }
}

/// <summary>
/// CPU/Memory resource fault
/// </summary>
public class ResourceFault : ChaosFault
{
    public ResourceFault()
    {
        Type = "Resource";
    }

    public void SetCpuStress(int cpuPercentage, int duration)
    {
        Action = "CPUStress";
        Parameters["cpu"] = cpuPercentage;
        Parameters["duration"] = duration;
    }

    public void SetMemoryStress(string memoryUsage)
    {
        Action = "MemoryStress";
        Parameters["memory"] = memoryUsage; // 512MB, 1GB
    }
}

/// <summary>
/// File system fault
/// </summary>
public class IoFault : ChaosFault
{
    public IoFault()
    {
        Type = "IO";
    }

    public void SetFillDisk(string percentage)
    {
        Action = "FillDisk";
        Parameters["fill"] = percentage; // 50%, 90%
    }

    public void SetIoLatency(int latencyMs)
    {
        Action = "IOLatency";
        Parameters["latency"] = $"{latencyMs}ms";
    }
}

/// <summary>
/// Time fault - clock skew
/// </summary>
public class TimeFault : ChaosFault
{
    public TimeFault()
    {
        Type = "Time";
    }

    public void SetClockSkew(int offsetSeconds)
    {
        Action = "ClockSkew";
        Parameters["offset"] = $"{offsetSeconds}s";
    }
}

/// <summary>
/// Chaos experiment results
/// </summary>
public class ChaosResults
{
    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty; // Success, Failure, Partial

    [JsonPropertyName("podsAffected")]
    public int PodsAffected { get; set; }

    [JsonPropertyName("failureRate")]
    public double FailureRate { get; set; } = 0.0; // 0-1

    [JsonPropertyName("recoveryTime")]
    public TimeSpan RecoveryTime { get; set; } = TimeSpan.Zero;

    [JsonPropertyName("observations")]
    public List<string> Observations { get; set; } = new();

    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; } = new();

    [JsonPropertyName("logs")]
    public List<string> Logs { get; set; } = new();
}

/// <summary>
/// Chaos experiment schedule (recurring experiments)
/// </summary>
public class ChaosSchedule
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("experiment")]
    public ChaosExperiment Experiment { get; set; } = new();

    [JsonPropertyName("schedule")]
    public string Schedule { get; set; } = string.Empty; // Cron expression

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("lastExecution")]
    public DateTime? LastExecution { get; set; }

    [JsonPropertyName("nextExecution")]
    public DateTime? NextExecution { get; set; }
}

/// <summary>
/// Chaos engine - executes and manages experiments
/// </summary>
public class ChaosEngine
{
    private readonly Dictionary<string, ChaosExperiment> _experiments = new();
    private readonly Dictionary<string, ChaosSchedule> _schedules = new();
    private readonly ConcurrentQueue<ChaosExperiment> _executionQueue = new();
    private readonly ILogger<ChaosEngine> _logger;

    public ChaosEngine(ILogger<ChaosEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create experiment
    /// </summary>
    public async Task<ChaosExperiment> CreateExperimentAsync(ChaosExperiment experiment)
    {
        _experiments[experiment.Id] = experiment;

        _logger.LogInformation(
            "Created chaos experiment: {Name} ({Type}/{Action})",
            experiment.Name,
            experiment.Fault.Type,
            experiment.Fault.Action);

        return experiment;
    }

    /// <summary>
    /// Run experiment
    /// </summary>
    public async Task<ChaosExperiment> RunExperimentAsync(string experimentId)
    {
        if (!_experiments.TryGetValue(experimentId, out var experiment))
        {
            throw new InvalidOperationException("Experiment not found");
        }

        experiment.Status = "Running";
        experiment.StartTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting chaos experiment: {Name} for {Duration}",
            experiment.Name,
            experiment.Duration);

        // Simulate experiment execution
        await Task.Delay(1000); // Simulate injection delay

        // Simulate outcome (simplified)
        var outcome = SimulateExperiment(experiment);

        experiment.Status = "Completed";
        experiment.EndTime = DateTime.UtcNow;
        experiment.Results = outcome;

        _logger.LogInformation(
            "Completed chaos experiment: {Name} ({Outcome})",
            experiment.Name,
            outcome.Outcome);

        return experiment;
    }

    /// <summary>
    /// Schedule recurring experiment
    /// </summary>
    public async Task<ChaosSchedule> ScheduleExperimentAsync(ChaosSchedule schedule)
    {
        _schedules[schedule.Name] = schedule;

        // Calculate next execution
        schedule.NextExecution = CalculateNextExecution(schedule.Schedule);

        _logger.LogInformation(
            "Scheduled chaos experiment: {Name} (next: {NextExecution})",
            schedule.Name,
            schedule.NextExecution);

        return schedule;
    }

    /// <summary>
    /// Pause/cancel running experiment
    /// </summary>
    public async Task PauseExperimentAsync(string experimentId)
    {
        if (_experiments.TryGetValue(experimentId, out var experiment))
        {
            if (experiment.Status == "Running")
            {
                experiment.Status = "Paused";
                experiment.EndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Paused chaos experiment: {Name}",
                    experiment.Name);
            }
        }
    }

    /// <summary>
    /// Get experiment results
    /// </summary>
    public ChaosExperiment? GetExperiment(string experimentId)
    {
        _experiments.TryGetValue(experimentId, out var experiment);
        return experiment;
    }

    /// <summary>
    /// List all experiments
    /// </summary>
    public List<ChaosExperiment> ListExperiments(string? status = null)
    {
        return _experiments.Values
            .Where(e => status == null || e.Status == status)
            .ToList();
    }

    private ChaosResults SimulateExperiment(ChaosExperiment experiment)
    {
        var random = new Random();

        var results = new ChaosResults
        {
            PodsAffected = random.Next(1, 5),
            FailureRate = random.NextDouble() * 0.5, // 0-50% failure rate
            RecoveryTime = TimeSpan.FromSeconds(random.Next(5, 60)),
            Observations = new()
            {
                "System recovered gracefully",
                "No data loss detected",
                "Service remained available"
            }
        };

        // Determine outcome
        results.Outcome = results.FailureRate > 0.3 ? "Partial" : "Success";

        // Add metrics
        results.Metrics["cpuUsageIncrease"] = random.NextDouble() * 100;
        results.Metrics["memoryUsageIncrease"] = random.NextDouble() * 50;
        results.Metrics["requestErrorRate"] = random.NextDouble() * 10;

        return results;
    }

    private DateTime CalculateNextExecution(string cronExpression)
    {
        // Simplified: next execution in 24 hours
        return DateTime.UtcNow.AddHours(24);
    }

    /// <summary>
    /// Get chaos engineering stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["totalExperiments"] = _experiments.Count,
            ["runningExperiments"] = _experiments.Values.Count(e => e.Status == "Running"),
            ["completedExperiments"] = _experiments.Values.Count(e => e.Status == "Completed"),
            ["scheduledExperiments"] = _schedules.Count
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class ChaosEngineeringExtensions
{
    public static IServiceCollection AddChaosEngineering(this IServiceCollection services)
    {
        services.AddSingleton<ChaosEngine>();
        return services;
    }
}
