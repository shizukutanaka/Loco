// Phase 13: Predictive Maintenance Engine
// Proactive failure prediction and preventive maintenance scheduling
// Health monitoring, degradation detection, and predictive alerts

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Autonomous;

/// <summary>
/// Health metric for component
/// </summary>
public class ComponentHealthMetric
{
    public string MetricId { get; set; } = Guid.NewGuid().ToString();
    public string ComponentName { get; set; } = string.Empty;
    public double HealthScore { get; set; } // 0-100
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    public List<double> HealthHistory { get; set; } = new(); // Last 30 measurements
    public double DegradationRate { get; set; } // Points per hour
    public string HealthStatus { get; set; } = string.Empty; // healthy, degraded, critical, failing
}

/// <summary>
/// Failure prediction
/// </summary>
public class FailurePrediction
{
    public string PredictionId { get; set; } = Guid.NewGuid().ToString();
    public string ComponentName { get; set; } = string.Empty;
    public double FailureProbabilityPercent { get; set; }
    public DateTime PredictedFailureTime { get; set; }
    public long HoursUntilFailure { get; set; }
    public string FailureMode { get; set; } = string.Empty; // gradual_degradation, sudden_failure
    public double Confidence { get; set; } // 0-100
    public List<string> WarningIndicators { get; set; } = new();
    public List<string> PreventiveActions { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maintenance schedule
/// </summary>
public class MaintenanceSchedule
{
    public string ScheduleId { get; set; } = Guid.NewGuid().ToString();
    public string ComponentName { get; set; } = string.Empty;
    public string MaintenanceType { get; set; } = string.Empty; // preventive, corrective, emergency
    public DateTime ScheduledDate { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public string MaintenanceActions { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // scheduled, in_progress, completed, cancelled
    public DateTime? CompletedDate { get; set; }
    public bool WasSuccessful { get; set; }
}

/// <summary>
/// Degradation trend
/// </summary>
public class DegradationTrend
{
    public string TrendId { get; set; } = Guid.NewGuid().ToString();
    public string ComponentName { get; set; } = string.Empty;
    public List<double> HealthValues { get; set; } = new();
    public List<DateTime> Timestamps { get; set; } = new();
    public double LinearDegradationRate { get; set; } // Points per hour
    public double AverageDegradationRate { get; set; }
    public DateTime ProjectedFailureDate { get; set; }
    public string TrendType { get; set; } = string.Empty; // linear, exponential, cyclic
    public double Confidence { get; set; }
}

/// <summary>
/// Predictive maintenance interface
/// </summary>
public interface IPredictiveMaintenanceEngine
{
    // Health monitoring
    Task<ComponentHealthMetric> RecordHealthMetricAsync(
        string componentName,
        double healthScore,
        CancellationToken ct = default);

    Task<ComponentHealthMetric> GetComponentHealthAsync(
        string componentName,
        CancellationToken ct = default);

    Task<List<ComponentHealthMetric>> GetAllComponentHealthAsync(
        CancellationToken ct = default);

    // Failure prediction
    Task<FailurePrediction> PredictFailureAsync(
        string componentName,
        CancellationToken ct = default);

    Task<List<FailurePrediction>> PredictMultipleFailuresAsync(
        string tenantId,
        CancellationToken ct = default);

    // Maintenance scheduling
    Task<MaintenanceSchedule> ScheduleMaintenanceAsync(
        string componentName,
        string maintenanceType,
        DateTime scheduledDate,
        CancellationToken ct = default);

    Task<List<MaintenanceSchedule>> GetUpcomingMaintenanceAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> CompleteMaintenanceAsync(
        string scheduleId,
        bool wasSuccessful,
        CancellationToken ct = default);

    // Degradation analysis
    Task<DegradationTrend> AnalyzeDegradationAsync(
        string componentName,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> GetPredictiveMaintenanceAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Predictive maintenance engine implementation
/// </summary>
public class PredictiveMaintenanceEngine : IPredictiveMaintenanceEngine
{
    private readonly ILogger<PredictiveMaintenanceEngine> _logger;
    private readonly Dictionary<string, List<ComponentHealthMetric>> _healthMetrics;
    private readonly Dictionary<string, List<FailurePrediction>> _predictions;
    private readonly Dictionary<string, List<MaintenanceSchedule>> _maintenanceSchedules;
    private readonly Dictionary<string, List<DegradationTrend>> _degradationTrends;

    public PredictiveMaintenanceEngine(ILogger<PredictiveMaintenanceEngine> logger)
    {
        _logger = logger;
        _healthMetrics = new Dictionary<string, List<ComponentHealthMetric>>();
        _predictions = new Dictionary<string, List<FailurePrediction>>();
        _maintenanceSchedules = new Dictionary<string, List<MaintenanceSchedule>>();
        _degradationTrends = new Dictionary<string, List<DegradationTrend>>();
    }

    // Health monitoring
    public async Task<ComponentHealthMetric> RecordHealthMetricAsync(
        string componentName,
        double healthScore,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var metric = new ComponentHealthMetric
        {
            ComponentName = componentName,
            HealthScore = healthScore,
            HealthStatus = ClassifyHealth(healthScore),
            DegradationRate = (100 - healthScore) / 24.0 // Estimated degradation per hour
        };

        if (!_healthMetrics.ContainsKey(componentName))
        {
            _healthMetrics[componentName] = new List<ComponentHealthMetric>();
        }

        _healthMetrics[componentName].Add(metric);

        // Keep only last 30 metrics
        if (_healthMetrics[componentName].Count > 30)
        {
            _healthMetrics[componentName] = _healthMetrics[componentName].TakeLast(30).ToList();
        }

        _logger.LogInformation(
            "Health metric recorded: Component={Component}, Score={Score:F1}%, Status={Status}",
            componentName, healthScore, metric.HealthStatus);

        return metric;
    }

    public async Task<ComponentHealthMetric> GetComponentHealthAsync(
        string componentName,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_healthMetrics.TryGetValue(componentName, out var metrics))
        {
            return metrics.OrderByDescending(m => m.MeasuredAt).FirstOrDefault() ?? new ComponentHealthMetric();
        }

        return new ComponentHealthMetric { ComponentName = componentName };
    }

    public async Task<List<ComponentHealthMetric>> GetAllComponentHealthAsync(
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allMetrics = _healthMetrics.Values
            .SelectMany(m => m)
            .GroupBy(m => m.ComponentName)
            .Select(g => g.OrderByDescending(m => m.MeasuredAt).First())
            .ToList();

        return allMetrics;
    }

    // Failure prediction
    public async Task<FailurePrediction> PredictFailureAsync(
        string componentName,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate prediction calculation

        var metrics = _healthMetrics.TryGetValue(componentName, out var m) ? m : new List<ComponentHealthMetric>();
        var latestHealth = metrics.OrderByDescending(x => x.MeasuredAt).FirstOrDefault();

        if (latestHealth == null)
            return null;

        var healthScore = latestHealth.HealthScore;
        var hoursUntilFailure = (long)((100 - healthScore) / latestHealth.DegradationRate);

        var prediction = new FailurePrediction
        {
            ComponentName = componentName,
            FailureProbabilityPercent = 100 - healthScore,
            PredictedFailureTime = DateTime.UtcNow.AddHours(hoursUntilFailure),
            HoursUntilFailure = Math.Max(0, hoursUntilFailure),
            FailureMode = hoursUntilFailure < 24 ? "gradual_degradation" : "sudden_failure",
            Confidence = Math.Min(95, 60 + (metrics.Count * 5)), // Increases with more data
            WarningIndicators = new List<string>
            {
                $"Health score {healthScore:F1}% (threshold: 30%)",
                $"Degradation rate {latestHealth.DegradationRate:F2} points/hour",
                $"Trend: {(metrics.Count > 5 && metrics[metrics.Count - 1].HealthScore < metrics[metrics.Count - 5].HealthScore ? "worsening" : "stable")}"
            },
            PreventiveActions = new List<string>
            {
                "Schedule preventive maintenance within 48 hours",
                "Allocate additional resources as buffer",
                "Monitor closely for acceleration of degradation",
                "Prepare fallback mechanisms"
            }
        };

        if (!_predictions.ContainsKey(componentName))
        {
            _predictions[componentName] = new List<FailurePrediction>();
        }

        _predictions[componentName].Add(prediction);

        _logger.LogWarning(
            "Failure predicted: Component={Component}, Probability={Probability:F1}%, HoursUntilFailure={Hours}, Confidence={Confidence:F1}%",
            componentName, prediction.FailureProbabilityPercent, hoursUntilFailure, prediction.Confidence);

        return prediction;
    }

    public async Task<List<FailurePrediction>> PredictMultipleFailuresAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var predictions = new List<FailurePrediction>();
        var components = _healthMetrics.Keys.ToList();

        foreach (var component in components)
        {
            var prediction = await PredictFailureAsync(component, ct);
            if (prediction != null && prediction.FailureProbabilityPercent > 30)
            {
                predictions.Add(prediction);
            }
        }

        return predictions.OrderByDescending(p => p.FailureProbabilityPercent).ToList();
    }

    // Maintenance scheduling
    public async Task<MaintenanceSchedule> ScheduleMaintenanceAsync(
        string componentName,
        string maintenanceType,
        DateTime scheduledDate,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var schedule = new MaintenanceSchedule
        {
            ComponentName = componentName,
            MaintenanceType = maintenanceType,
            ScheduledDate = scheduledDate,
            EstimatedDurationMinutes = 30,
            MaintenanceActions = GetMaintenanceActions(maintenanceType),
            Status = "scheduled"
        };

        if (!_maintenanceSchedules.ContainsKey(componentName))
        {
            _maintenanceSchedules[componentName] = new List<MaintenanceSchedule>();
        }

        _maintenanceSchedules[componentName].Add(schedule);

        _logger.LogInformation(
            "Maintenance scheduled: Component={Component}, Type={Type}, Date={Date:g}",
            componentName, maintenanceType, scheduledDate);

        return schedule;
    }

    public async Task<List<MaintenanceSchedule>> GetUpcomingMaintenanceAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var upcoming = _maintenanceSchedules.Values
            .SelectMany(s => s)
            .Where(s => s.Status == "scheduled" && s.ScheduledDate > DateTime.UtcNow)
            .OrderBy(s => s.ScheduledDate)
            .ToList();

        return upcoming;
    }

    public async Task<bool> CompleteMaintenanceAsync(
        string scheduleId,
        bool wasSuccessful,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var schedules in _maintenanceSchedules.Values)
        {
            var schedule = schedules.FirstOrDefault(s => s.ScheduleId == scheduleId);
            if (schedule != null)
            {
                schedule.Status = "completed";
                schedule.CompletedDate = DateTime.UtcNow;
                schedule.WasSuccessful = wasSuccessful;

                _logger.LogInformation(
                    "Maintenance completed: Component={Component}, Successful={Success}",
                    schedule.ComponentName, wasSuccessful);

                return true;
            }
        }

        return false;
    }

    // Degradation analysis
    public async Task<DegradationTrend> AnalyzeDegradationAsync(
        string componentName,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate trend calculation

        var metrics = _healthMetrics.TryGetValue(componentName, out var m) ? m.OrderBy(x => x.MeasuredAt).ToList() : new List<ComponentHealthMetric>();

        if (metrics.Count < 2)
            return null;

        var healthValues = metrics.Select(m => m.HealthScore).ToList();
        var timestamps = metrics.Select(m => m.MeasuredAt).ToList();

        // Calculate linear degradation
        var timeSpan = (timestamps.Last() - timestamps.First()).TotalHours;
        var healthChange = healthValues.Last() - healthValues.First();
        var linearRate = timeSpan > 0 ? healthChange / timeSpan : 0;

        var trend = new DegradationTrend
        {
            ComponentName = componentName,
            HealthValues = healthValues,
            Timestamps = timestamps,
            LinearDegradationRate = linearRate,
            AverageDegradationRate = healthValues.Skip(1).Select((h, i) => h - healthValues[i]).Average(),
            ProjectedFailureDate = linearRate < 0
                ? DateTime.UtcNow.AddHours((100 - healthValues.Last()) / Math.Abs(linearRate))
                : DateTime.UtcNow.AddDays(365),
            TrendType = Math.Abs(linearRate) < 1 ? "linear" : linearRate < -2 ? "exponential" : "cyclic",
            Confidence = Math.Min(95, 50 + (metrics.Count * 2))
        };

        if (!_degradationTrends.ContainsKey(componentName))
        {
            _degradationTrends[componentName] = new List<DegradationTrend>();
        }

        _degradationTrends[componentName].Add(trend);

        return trend;
    }

    public async Task<Dictionary<string, object>> GetPredictiveMaintenanceAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allMetrics = _healthMetrics.Values.SelectMany(m => m).ToList();
        var allPredictions = _predictions.Values.SelectMany(p => p).ToList();
        var allSchedules = _maintenanceSchedules.Values.SelectMany(s => s).ToList();

        var componentsAtRisk = allPredictions.Count(p => p.FailureProbabilityPercent > 70);
        var upcomingMaintenance = allSchedules.Count(s => s.Status == "scheduled" && s.ScheduledDate <= DateTime.UtcNow.AddDays(7));

        return new Dictionary<string, object>
        {
            ["total_monitored_components"] = _healthMetrics.Keys.Count,
            ["average_health_score"] = allMetrics.Count > 0 ? allMetrics.Average(m => m.HealthScore) : 0,
            ["components_at_risk"] = componentsAtRisk,
            ["critical_components"] = allPredictions.Count(p => p.FailureProbabilityPercent > 90),
            ["total_predictions_made"] = allPredictions.Count,
            ["average_prediction_confidence"] = allPredictions.Count > 0 ? allPredictions.Average(p => p.Confidence) : 0,
            ["scheduled_maintenance_count"] = allSchedules.Count(s => s.Status == "scheduled"),
            ["upcoming_maintenance_7days"] = upcomingMaintenance,
            ["maintenance_success_rate"] = allSchedules.Count(s => s.Status == "completed") > 0
                ? (allSchedules.Count(s => s.Status == "completed" && s.WasSuccessful) / (double)allSchedules.Count(s => s.Status == "completed")) * 100
                : 0
        };
    }

    // Helpers
    private string ClassifyHealth(double healthScore)
    {
        return healthScore switch
        {
            >= 80 => "healthy",
            >= 60 => "degraded",
            >= 40 => "critical",
            _ => "failing"
        };
    }

    private string GetMaintenanceActions(string maintenanceType)
    {
        return maintenanceType switch
        {
            "preventive" => "Perform routine checks, clean filters, update software, validate configurations",
            "corrective" => "Repair or replace failed components, verify functionality",
            "emergency" => "Critical repair to restore immediate functionality",
            _ => "Perform standard maintenance actions"
        };
    }
}
