#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AutonomousSystems;

/// <summary>
/// Autonomous Systems Patterns
/// Self-healing infrastructure, AIOps, predictive analytics, autonomous remediation
/// </summary>

public class AnomalyDetection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("metricName")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }

    [JsonPropertyName("baselineValue")]
    public double BaselineValue { get; set; }

    [JsonPropertyName("deviation")]
    public double DeviationPercent { get; set; }

    [JsonPropertyName("anomalyScore")]
    public double AnomalyScore { get; set; } // 0-1

    [JsonPropertyName("isAnomaly")]
    public bool IsAnomaly { get; set; }

    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

public class PredictiveAnalytics
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("predictedMetric")]
    public string PredictedMetric { get; set; } = string.Empty;

    [JsonPropertyName("predictionModel")]
    public string PredictionModel { get; set; } = string.Empty; // ARIMA, LSTMs, Prophet

    [JsonPropertyName("predictedValue")]
    public double PredictedValue { get; set; }

    [JsonPropertyName("confidenceLevel")]
    public double ConfidenceLevel { get; set; } // 0-1

    [JsonPropertyName("forecastHours")]
    public int ForecastHours { get; set; }

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High, Critical

    [JsonPropertyName("predictionTime")]
    public DateTime PredictionTime { get; set; } = DateTime.UtcNow;
}

public class AutomatedRemedy
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("anomalyId")]
    public string AnomalyId { get; set; } = string.Empty;

    [JsonPropertyName("remediationType")]
    public string RemediationType { get; set; } = string.Empty; // Restart, Rescale, Rollback, Isolate

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("affectedResources")]
    public List<string> AffectedResources { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, executing, succeeded, failed

    [JsonPropertyName("executionStartTime")]
    public DateTime ExecutionStartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("executionEndTime")]
    public DateTime? ExecutionEndTime { get; set; }

    [JsonPropertyName("recoveryTimeSeconds")]
    public double? RecoveryTimeSeconds { get; set; }
}

public class SelfDrivingStorage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("totalCapacityGb")]
    public long TotalCapacityGb { get; set; }

    [JsonPropertyName("usedCapacityGb")]
    public long UsedCapacityGb { get; set; }

    [JsonPropertyName("utilizationPercent")]
    public double UtilizationPercent { get; set; }

    [JsonPropertyName("autonomousActionsPerformed")]
    public int AutonomousActionsPerformed { get; set; }

    [JsonPropertyName("lastMigrationTime")]
    public DateTime? LastMigrationTime { get; set; }

    [JsonPropertyName("predictedFullnessTime")]
    public DateTime PredictedFullnessTime { get; set; }

    [JsonPropertyName("automatedCompactionEnabled")]
    public bool AutomatedCompactionEnabled { get; set; }
}

public class ITOTSecurityContext
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("itInfrastructure")]
    public Dictionary<string, object> ItInfrastructure { get; set; } = new();

    [JsonPropertyName("operationalTechnology")]
    public Dictionary<string, object> OperationalTechnology { get; set; } = new();

    [JsonPropertyName("convergenceLevel")]
    public string ConvergenceLevel { get; set; } = string.Empty; // Isolated, Connected, Integrated

    [JsonPropertyName("threatLevel")]
    public string ThreatLevel { get; set; } = string.Empty; // Low, Medium, High, Critical

    [JsonPropertyName("automatedSecurityActions")]
    public List<string> AutomatedSecurityActions { get; set; } = new();
}

public class AgenticAI
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("agentType")]
    public string AgentType { get; set; } = string.Empty; // Monitoring, Remediation, Optimization, Planning

    [JsonPropertyName("autonomyLevel")]
    public int AutonomyLevel { get; set; } = 1; // 1-5 (1=supervised, 5=fully autonomous)

    [JsonPropertyName("decisions")]
    public List<(DateTime timestamp, string decision, double confidence)> Decisions { get; set; } = new();

    [JsonPropertyName("successRate")]
    public double SuccessRate { get; set; } = 0;

    [JsonPropertyName("lastAction")]
    public DateTime LastActionTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // active, paused, failed
}

public class AutonomousStatistics
{
    [JsonPropertyName("totalAnomaliesDetected")]
    public long TotalAnomaliesDetected { get; set; }

    [JsonPropertyName("automatedRemediationsPerformed")]
    public long AutomatedRemediationsPerformed { get; set; }

    [JsonPropertyName("remediationSuccessRate")]
    public double RemediationSuccessRate { get; set; }

    [JsonPropertyName("mttr")]
    public double MeanTimeToRepairSeconds { get; set; }

    [JsonPropertyName("detectionLatencyMs")]
    public double DetectionLatencyMs { get; set; }

    [JsonPropertyName("predictiveAccuracy")]
    public double PredictiveAccuracy { get; set; }

    [JsonPropertyName("costSavedByAutomation")]
    public decimal CostSavedByAutomation { get; set; }

    [JsonPropertyName("humanInterventionsRequired")]
    public long HumanInterventionsRequired { get; set; }
}

/// <summary>
/// Autonomous Systems Engine
/// </summary>
public class AutonomousSystemsEngine
{
    private readonly ConcurrentDictionary<string, AnomalyDetection> _anomalies = new();
    private readonly ConcurrentDictionary<string, PredictiveAnalytics> _predictions = new();
    private readonly ConcurrentDictionary<string, AutomatedRemedy> _remedies = new();
    private readonly ConcurrentDictionary<string, AgenticAI> _agents = new();
    private readonly List<SelfDrivingStorage> _storages = new();
    private readonly AutonomousStatistics _stats = new();
    private readonly ILogger<AutonomousSystemsEngine> _logger;

    public AutonomousSystemsEngine(ILogger<AutonomousSystemsEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Detect anomaly
    /// </summary>
    public async Task<AnomalyDetection> DetectAnomalyAsync(
        string resourceId,
        string metricName,
        double currentValue,
        double baselineValue)
    {
        var deviation = Math.Abs(currentValue - baselineValue) / baselineValue * 100;
        var anomalyScore = Math.Min(deviation / 50, 1.0); // Normalize to 0-1

        var anomaly = new AnomalyDetection
        {
            ResourceId = resourceId,
            MetricName = metricName,
            CurrentValue = currentValue,
            BaselineValue = baselineValue,
            DeviationPercent = deviation,
            AnomalyScore = anomalyScore,
            IsAnomaly = anomalyScore > 0.7
        };

        _anomalies[anomaly.Id] = anomaly;
        _stats.TotalAnomaliesDetected++;

        if (anomaly.IsAnomaly)
        {
            _logger.LogWarning(
                "Detected anomaly: {Resource}.{Metric} ({Value} vs baseline {Baseline}, score: {Score:F2})",
                resourceId,
                metricName,
                currentValue,
                baselineValue,
                anomalyScore);
        }

        return anomaly;
    }

    /// <summary>
    /// Predict future metric value
    /// </summary>
    public async Task<PredictiveAnalytics> PredictMetricAsync(
        string resourceId,
        string metricName,
        int forecastHours = 24)
    {
        var prediction = new PredictiveAnalytics
        {
            ResourceId = resourceId,
            PredictedMetric = metricName,
            PredictionModel = "Prophet",
            PredictedValue = new Random().NextDouble() * 100,
            ConfidenceLevel = 0.85,
            ForecastHours = forecastHours,
            RiskLevel = "Medium"
        };

        _predictions[prediction.Id] = prediction;

        _logger.LogInformation(
            "Predicted {Metric} for {Resource} ({Hours}h forecast)",
            metricName,
            resourceId,
            forecastHours);

        return prediction;
    }

    /// <summary>
    /// Execute automated remedy
    /// </summary>
    public async Task<AutomatedRemedy> ExecuteAutomatedRemedyAsync(
        string anomalyId,
        string remediationType,
        List<string> affectedResources)
    {
        var remedy = new AutomatedRemedy
        {
            AnomalyId = anomalyId,
            RemediationType = remediationType,
            AffectedResources = affectedResources,
            Status = "executing"
        };

        _remedies[remedy.Id] = remedy;

        // Simulate execution
        await Task.Delay(new Random().Next(100, 1000));

        remedy.Status = "succeeded";
        remedy.ExecutionEndTime = DateTime.UtcNow;
        remedy.RecoveryTimeSeconds = (remedy.ExecutionEndTime.Value - remedy.ExecutionStartTime).TotalSeconds;

        _stats.AutomatedRemediationsPerformed++;

        _logger.LogInformation(
            "Executed automated remedy: {Type} on {Count} resources ({Time:F1}s)",
            remediationType,
            affectedResources.Count,
            remedy.RecoveryTimeSeconds);

        return remedy;
    }

    /// <summary>
    /// Create autonomous agent
    /// </summary>
    public async Task<AgenticAI> CreateAutonomousAgentAsync(
        string agentName,
        string agentType,
        int autonomyLevel = 3)
    {
        var agent = new AgenticAI
        {
            AgentName = agentName,
            AgentType = agentType,
            AutonomyLevel = autonomyLevel
        };

        _agents[agent.Id] = agent;

        _logger.LogInformation(
            "Created autonomous agent: {Name} ({Type}, autonomy level: {Level})",
            agentName,
            agentType,
            autonomyLevel);

        return agent;
    }

    /// <summary>
    /// Record agent decision
    /// </summary>
    public async Task RecordAgentDecisionAsync(
        string agentId,
        string decision,
        double confidence)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Decisions.Add((DateTime.UtcNow, decision, confidence));

            _logger.LogInformation(
                "Agent {Name} made decision: {Decision} (confidence: {Conf:F2})",
                agent.AgentName,
                decision,
                confidence);
        }
    }

    /// <summary>
    /// Register self-driving storage
    /// </summary>
    public async Task RegisterSelfDrivingStorageAsync(SelfDrivingStorage storage)
    {
        _storages.Add(storage);

        _logger.LogInformation(
            "Registered self-driving storage: {Name} ({Capacity}GB, {Util}% utilized)",
            storage.Name,
            storage.TotalCapacityGb,
            storage.UtilizationPercent);
    }

    /// <summary>
    /// Get autonomous systems statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var completedRemedies = _remedies.Values
            .Where(r => r.Status == "succeeded")
            .ToList();

        var avgMttr = completedRemedies.Count > 0
            ? completedRemedies.Average(r => r.RecoveryTimeSeconds ?? 0)
            : 0;

        var succeedingRemedies = _remedies.Values
            .Count(r => r.Status == "succeeded");

        var remediationSuccessRate = _remedies.Count > 0
            ? ((double)succeedingRemedies / _remedies.Count * 100)
            : 0;

        return new()
        {
            ["totalAnomaliesDetected"] = _stats.TotalAnomaliesDetected,
            ["automatedRemediationsPerformed"] = _stats.AutomatedRemediationsPerformed,
            ["remediationSuccessRate"] = Math.Round(remediationSuccessRate, 2) + "%",
            ["meanTimeToRepairSeconds"] = Math.Round(avgMttr, 2),
            ["autonomousAgents"] = _agents.Count,
            ["selfDrivingStorages"] = _storages.Count,
            ["predictionsGenerated"] = _predictions.Count,
            ["humanInterventionsRequired"] = _stats.HumanInterventionsRequired
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class AutonomousSystemsExtensions
{
    public static IServiceCollection AddAutonomousSystems(this IServiceCollection services)
    {
        services.AddSingleton<AutonomousSystemsEngine>();
        return services;
    }
}
