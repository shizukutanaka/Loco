#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EnergyEfficiency;

/// <summary>
/// Energy-Efficient Computing Patterns
/// Green coding, ASIC accelerators, workload optimization, carbon tracking
/// </summary>

public class EnergyMetric
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("powerWatts")]
    public double PowerWatts { get; set; }

    [JsonPropertyName("energyWhConsumed")]
    public double EnergyWhConsumed { get; set; }

    [JsonPropertyName("cpuUtilizationPercent")]
    public double CpuUtilizationPercent { get; set; }

    [JsonPropertyName("temperatureCelsius")]
    public double TemperatureCelsius { get; set; }

    [JsonPropertyName("efficiency")]
    public double EfficiencyPercent { get; set; } // Operations per Watt

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GreenCodingOptimization
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("optimizationType")]
    public string OptimizationType { get; set; } = string.Empty; // Algorithm, Memory, IO, Computation

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("originalEnergyWh")]
    public double OriginalEnergyWh { get; set; }

    [JsonPropertyName("optimizedEnergyWh")]
    public double OptimizedEnergyWh { get; set; }

    [JsonPropertyName("reductionPercent")]
    public double ReductionPercent { get; set; }

    [JsonPropertyName("implementationEffort")]
    public string ImplementationEffort { get; set; } = string.Empty; // Low, Medium, High

    [JsonPropertyName("appliedAt")]
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}

public class ASICAccelerator
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // GPU, TPU, FPGA, Custom

    [JsonPropertyName("operationsPerWatt")]
    public double OperationsPerWatt { get; set; }

    [JsonPropertyName("thermalDesignPowerWatts")]
    public double TdpWatts { get; set; }

    [JsonPropertyName("computePerformanceTeraflops")]
    public double ComputePerformanceTeraflops { get; set; }

    [JsonPropertyName("memoryBandwidthGbps")]
    public double MemoryBandwidthGbps { get; set; }

    [JsonPropertyName("utilizationPercent")]
    public double UtilizationPercent { get; set; }

    [JsonPropertyName("coolingType")]
    public string CoolingType { get; set; } = string.Empty; // Air, Liquid, Immersion
}

public class WorkloadScheduling
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workloadId")]
    public string WorkloadId { get; set; } = string.Empty;

    [JsonPropertyName("schedulingStrategy")]
    public string SchedulingStrategy { get; set; } = string.Empty; // CarbonAware, TimeShift, Consolidation

    [JsonPropertyName("targetCarbonIntensity")]
    public double TargetCarbonIntensityGCO2PerKwh { get; set; }

    [JsonPropertyName("currentCarbonIntensity")]
    public double CurrentCarbonIntensityGCO2PerKwh { get; set; }

    [JsonPropertyName("waitingForIdealTime")]
    public bool WaitingForIdealTime { get; set; }

    [JsonPropertyName("estimatedCarbonSavings")]
    public double EstimatedCarbonSavingsGCO2 { get; set; }

    [JsonPropertyName("scheduledTime")]
    public DateTime ScheduledTime { get; set; }
}

public class DataCenterCooling
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("datacenterId")]
    public string DatacenterId { get; set; } = string.Empty;

    [JsonPropertyName("coolingType")]
    public string CoolingType { get; set; } = string.Empty; // Air, Liquid, Immersion, Free

    [JsonPropertyName("powerUsageEffectiveness")]
    public double Pue { get; set; } = 1.5; // Total facility power / IT equipment power

    [JsonPropertyName("totalCoolingCapacityKw")]
    public double TotalCoolingCapacityKw { get; set; }

    [JsonPropertyName("currentLoadKw")]
    public double CurrentLoadKw { get; set; }

    [JsonPropertyName("ambientTemperatureC")]
    public double AmbientTemperatureC { get; set; }

    [JsonPropertyName("chilledWaterTemperatureC")]
    public double ChilledWaterTemperatureC { get; set; }

    [JsonPropertyName("coolingSavingsPercent")]
    public double CoolingSavingsPercent { get; set; }
}

public class CarbonFootprint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("energyWh")]
    public double EnergyWh { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("gridCarbonIntensity")]
    public double GridCarbonIntensityGCO2PerKwh { get; set; }

    [JsonPropertyName("renewableEnergyPercent")]
    public double RenewableEnergyPercent { get; set; }

    [JsonPropertyName("estimatedCarbonGrams")]
    public double EstimatedCarbonGrams { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class EnergyEfficiencyStatistics
{
    [JsonPropertyName("totalEnergyWh")]
    public double TotalEnergyWh { get; set; }

    [JsonPropertyName("averagePowerWatts")]
    public double AveragePowerWatts { get; set; }

    [JsonPropertyName("peakPowerWatts")]
    public double PeakPowerWatts { get; set; }

    [JsonPropertyName("carbonEmissionsGrams")]
    public double CarbonEmissionsGrams { get; set; }

    [JsonPropertyName("energySavingsPercent")]
    public double EnergySavingsPercent { get; set; }

    [JsonPropertyName("pueDatacenter")]
    public double PueDatacenter { get; set; } = 1.5;

    [JsonPropertyName("optimizationsApplied")]
    public int OptimizationsApplied { get; set; }

    [JsonPropertyName("costSavings")]
    public decimal CostSavingsDollars { get; set; }
}

/// <summary>
/// Energy Efficiency Engine
/// </summary>
public class EnergyEfficiencyEngine
{
    private readonly ConcurrentDictionary<string, EnergyMetric> _metrics = new();
    private readonly ConcurrentDictionary<string, ASICAccelerator> _accelerators = new();
    private readonly List<GreenCodingOptimization> _optimizations = new();
    private readonly List<WorkloadScheduling> _schedulings = new();
    private readonly List<CarbonFootprint> _carbonFootprints = new();
    private readonly EnergyEfficiencyStatistics _stats = new();
    private readonly ILogger<EnergyEfficiencyEngine> _logger;

    public EnergyEfficiencyEngine(ILogger<EnergyEfficiencyEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Record energy metric
    /// </summary>
    public async Task<EnergyMetric> RecordEnergyMetricAsync(
        string resourceId,
        double powerWatts,
        double cpuUtilizationPercent,
        double temperatureCelsius)
    {
        var metric = new EnergyMetric
        {
            ResourceId = resourceId,
            PowerWatts = powerWatts,
            CpuUtilizationPercent = cpuUtilizationPercent,
            TemperatureCelsius = temperatureCelsius,
            EnergyWhConsumed = powerWatts / 3600, // Convert to Wh
            EfficiencyPercent = cpuUtilizationPercent / (powerWatts / 10) * 100 // Simple efficiency calculation
        };

        _metrics[metric.Id] = metric;
        _stats.TotalEnergyWh += metric.EnergyWhConsumed;
        _stats.PeakPowerWatts = Math.Max(_stats.PeakPowerWatts, powerWatts);

        if (_stats.AveragePowerWatts == 0)
            _stats.AveragePowerWatts = powerWatts;
        else
            _stats.AveragePowerWatts = (_stats.AveragePowerWatts + powerWatts) / 2;

        _logger.LogInformation(
            "Recorded energy metric: {Resource} ({Power}W, {Cpu}% util, {Temp}°C)",
            resourceId,
            powerWatts,
            cpuUtilizationPercent,
            temperatureCelsius);

        return metric;
    }

    /// <summary>
    /// Register ASIC accelerator
    /// </summary>
    public async Task<ASICAccelerator> RegisterASICAcceleratorAsync(
        string name,
        string type,
        double operationsPerWatt,
        double tdpWatts,
        double teraflops,
        string coolingType = "Liquid")
    {
        var accelerator = new ASICAccelerator
        {
            Name = name,
            Type = type,
            OperationsPerWatt = operationsPerWatt,
            TdpWatts = tdpWatts,
            ComputePerformanceTeraflops = teraflops,
            CoolingType = coolingType
        };

        _accelerators[accelerator.Id] = accelerator;

        _logger.LogInformation(
            "Registered ASIC accelerator: {Name} ({Type}, {Ops}/W, {Tdp}W TDP)",
            name,
            type,
            operationsPerWatt,
            tdpWatts);

        return accelerator;
    }

    /// <summary>
    /// Apply green coding optimization
    /// </summary>
    public async Task<GreenCodingOptimization> ApplyOptimizationAsync(
        string optimizationType,
        string description,
        double originalEnergyWh,
        double optimizedEnergyWh,
        string effort = "Low")
    {
        var optimization = new GreenCodingOptimization
        {
            OptimizationType = optimizationType,
            Description = description,
            OriginalEnergyWh = originalEnergyWh,
            OptimizedEnergyWh = optimizedEnergyWh,
            ReductionPercent = ((originalEnergyWh - optimizedEnergyWh) / originalEnergyWh) * 100,
            ImplementationEffort = effort
        };

        _optimizations.Add(optimization);
        _stats.OptimizationsApplied++;
        _stats.EnergySavingsPercent += optimization.ReductionPercent;

        _logger.LogInformation(
            "Applied green coding optimization: {Type} ({Reduction:F1}% reduction)",
            optimizationType,
            optimization.ReductionPercent);

        return optimization;
    }

    /// <summary>
    /// Schedule workload on low-carbon time
    /// </summary>
    public async Task<WorkloadScheduling> ScheduleWorkloadAsync(
        string workloadId,
        double currentCarbonIntensity,
        double targetCarbonIntensity)
    {
        var scheduling = new WorkloadScheduling
        {
            WorkloadId = workloadId,
            SchedulingStrategy = "CarbonAware",
            CurrentCarbonIntensityGCO2PerKwh = currentCarbonIntensity,
            TargetCarbonIntensityGCO2PerKwh = targetCarbonIntensity,
            WaitingForIdealTime = currentCarbonIntensity > targetCarbonIntensity,
            ScheduledTime = DateTime.UtcNow.AddHours(1)
        };

        if (!scheduling.WaitingForIdealTime)
        {
            var savings = (currentCarbonIntensity - targetCarbonIntensity) * 0.5; // Assume 0.5 kWh workload
            scheduling.EstimatedCarbonSavingsGCO2 = savings;
        }

        _schedulings.Add(scheduling);

        _logger.LogInformation(
            "Scheduled workload: {Workload} (current: {Current}g CO2/kWh, target: {Target}g CO2/kWh)",
            workloadId,
            currentCarbonIntensity,
            targetCarbonIntensity);

        return scheduling;
    }

    /// <summary>
    /// Record carbon footprint
    /// </summary>
    public async Task<CarbonFootprint> RecordCarbonFootprintAsync(
        string resourceId,
        double energyWh,
        string region,
        double gridCarbonIntensity,
        double renewablePercent = 0)
    {
        var footprint = new CarbonFootprint
        {
            ResourceId = resourceId,
            EnergyWh = energyWh,
            Region = region,
            GridCarbonIntensityGCO2PerKwh = gridCarbonIntensity,
            RenewableEnergyPercent = renewablePercent,
            EstimatedCarbonGrams = (energyWh / 1000) * gridCarbonIntensity * (1 - renewablePercent / 100)
        };

        _carbonFootprints.Add(footprint);
        _stats.CarbonEmissionsGrams += footprint.EstimatedCarbonGrams;

        _logger.LogInformation(
            "Recorded carbon footprint: {Resource} ({Carbon:F1}g CO2, {Renewable}% renewable)",
            resourceId,
            footprint.EstimatedCarbonGrams,
            renewablePercent);

        return footprint;
    }

    /// <summary>
    /// Get energy efficiency statistics
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var avgCarbonSavings = _schedulings.Count > 0
            ? _schedulings.Average(s => s.EstimatedCarbonSavingsGCO2)
            : 0;

        return new()
        {
            ["totalEnergyWh"] = Math.Round(_stats.TotalEnergyWh, 2),
            ["averagePowerWatts"] = Math.Round(_stats.AveragePowerWatts, 2),
            ["peakPowerWatts"] = Math.Round(_stats.PeakPowerWatts, 2),
            ["carbonEmissionsGrams"] = Math.Round(_stats.CarbonEmissionsGrams, 2),
            ["energySavingsPercent"] = Math.Round(_stats.EnergySavingsPercent, 2),
            ["pueDatacenter"] = Math.Round(_stats.PueDatacenter, 2),
            ["optimizationsApplied"] = _stats.OptimizationsApplied,
            ["registeredAccelerators"] = _accelerators.Count,
            ["costSavingsDollars"] = Math.Round(_stats.CostSavingsDollars, 2),
            ["averageCarbonSavingsPerSchedule"] = Math.Round(avgCarbonSavings, 2)
        };
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class EnergyEfficiencyExtensions
{
    public static IServiceCollection AddEnergyEfficiency(this IServiceCollection services)
    {
        services.AddSingleton<EnergyEfficiencyEngine>();
        return services;
    }
}
