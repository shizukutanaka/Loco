// Phase 13: Auto-Tuning and Parameter Optimization Engine
// Automatic discovery and optimization of workflow parameters
// Dynamic parameter adjustment, adaptive algorithms, and learning feedback loops

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Autonomous;

/// <summary>
/// Tunable workflow parameter
/// </summary>
public class WorkflowParameter
{
    public string ParameterId { get; set; } = Guid.NewGuid().ToString();
    public string ParameterName { get; set; } = string.Empty;
    public string ParameterType { get; set; } = string.Empty; // timeout, retry_count, batch_size, parallelism, threshold, cache_ttl
    public double CurrentValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double DefaultValue { get; set; }
    public bool IsOptimizable { get; set; } = true;
    public DateTime LastTunedAt { get; set; } = DateTime.UtcNow;
    public double EffectivenessScore { get; set; } // 0-100, how well current value performs
}

/// <summary>
/// Parameter tuning configuration
/// </summary>
public class ParameterTuningConfiguration
{
    public string ConfigurationId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public List<WorkflowParameter> ParametersToTune { get; set; } = new();
    public string TuningStrategy { get; set; } = string.Empty; // genetic_algorithm, simulated_annealing, bayesian_optimization, grid_search
    public int MaxIterations { get; set; } = 100;
    public int PopulationSize { get; set; } = 20;
    public double MutationRate { get; set; } = 0.15;
    public double CrossoverRate { get; set; } = 0.8;
    public bool EnableAdaptiveAdjustment { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Parameter variation (alternative parameter set)
/// </summary>
public class ParameterVariation
{
    public string VariationId { get; set; } = Guid.NewGuid().ToString();
    public string ConfigurationId { get; set; } = string.Empty;
    public Dictionary<string, double> ParameterValues { get; set; } = new();
    public double FitnessScore { get; set; } // 0-100, quality measure
    public int TestExecutions { get; set; }
    public double AveragePerformanceImprovement { get; set; } // Percentage
    public double AverageReliabilityChange { get; set; } // Percentage
    public double AverageCostChange { get; set; } // Percentage
    public string Status { get; set; } = string.Empty; // candidate, testing, validated, promoted, archived
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Parameter optimization result
/// </summary>
public class ParameterOptimizationResult
{
    public string ResultId { get; set; } = Guid.NewGuid().ToString();
    public string ConfigurationId { get; set; } = string.Empty;
    public List<ParameterVariation> PopulationHistory { get; set; } = new();
    public ParameterVariation BestVariation { get; set; } = new();
    public int IterationsCompleted { get; set; }
    public double FitnessImprovement { get; set; } // Best fitness - baseline fitness
    public double ConvergenceSpeed { get; set; } // Iterations to convergence (0-100)
    public string ConvergenceStatus { get; set; } = string.Empty; // converged, improving, diverged, early_exit
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tuning metrics and progress tracking
/// </summary>
public class TuningMetrics
{
    public string MetricsId { get; set; } = Guid.NewGuid().ToString();
    public string ConfigurationId { get; set; } = string.Empty;
    public List<double> FitnessHistory { get; set; } = new(); // Fitness per iteration
    public List<double> DiversityHistory { get; set; } = new(); // Population diversity per iteration
    public double InitialBestFitness { get; set; }
    public double CurrentBestFitness { get; set; }
    public double AverageFitness { get; set; }
    public int StagnationCounter { get; set; } // Iterations without improvement
    public double ConvergenceRate { get; set; } // (Current - Initial) / Initial
    public double ExecutionTimeMs { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Adaptive parameter adjustment for runtime
/// </summary>
public class AdaptiveParameterAdjustment
{
    public string AdjustmentId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public double PreviousValue { get; set; }
    public double NewValue { get; set; }
    public string AdjustmentReason { get; set; } = string.Empty; // timeout, resource_pressure, degraded_performance, learning_feedback
    public double ExpectedImprovementPercent { get; set; }
    public bool IsSuccessful { get; set; }
    public DateTime AdjustedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Auto-tuning interface
/// </summary>
public interface IAutoTuningAndParameterOptimizationEngine
{
    // Configuration
    Task<ParameterTuningConfiguration> CreateTuningConfigurationAsync(
        string workflowId,
        List<string> parameterNames,
        string tuningStrategy,
        CancellationToken ct = default);

    Task<ParameterTuningConfiguration> GetTuningConfigurationAsync(
        string configurationId,
        CancellationToken ct = default);

    // Optimization
    Task<ParameterOptimizationResult> RunParameterOptimizationAsync(
        string configurationId,
        CancellationToken ct = default);

    Task<List<ParameterVariation>> GetOptimizationPopulationAsync(
        string configurationId,
        CancellationToken ct = default);

    // Parameter management
    Task<WorkflowParameter> GetParameterAsync(
        string workflowId,
        string parameterName,
        CancellationToken ct = default);

    Task<bool> ApplyOptimalParametersAsync(
        string workflowId,
        string resultId,
        CancellationToken ct = default);

    // Adaptive adjustment
    Task<AdaptiveParameterAdjustment> AdjustParameterAsync(
        string workflowId,
        string executionId,
        string parameterName,
        string adjustmentReason,
        CancellationToken ct = default);

    Task<List<AdaptiveParameterAdjustment>> GetAdjustmentHistoryAsync(
        string workflowId,
        CancellationToken ct = default);

    // Analytics
    Task<TuningMetrics> GetTuningMetricsAsync(
        string configurationId,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> GetAutoTuningAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Auto-tuning and parameter optimization engine implementation
/// </summary>
public class AutoTuningAndParameterOptimizationEngine : IAutoTuningAndParameterOptimizationEngine
{
    private readonly ILogger<AutoTuningAndParameterOptimizationEngine> _logger;
    private readonly Dictionary<string, List<WorkflowParameter>> _parameters;
    private readonly Dictionary<string, ParameterTuningConfiguration> _configurations;
    private readonly Dictionary<string, List<ParameterVariation>> _populations;
    private readonly Dictionary<string, ParameterOptimizationResult> _results;
    private readonly Dictionary<string, List<AdaptiveParameterAdjustment>> _adjustments;
    private readonly Dictionary<string, TuningMetrics> _metrics;

    public AutoTuningAndParameterOptimizationEngine(ILogger<AutoTuningAndParameterOptimizationEngine> logger)
    {
        _logger = logger;
        _parameters = new Dictionary<string, List<WorkflowParameter>>();
        _configurations = new Dictionary<string, ParameterTuningConfiguration>();
        _populations = new Dictionary<string, List<ParameterVariation>>();
        _results = new Dictionary<string, ParameterOptimizationResult>();
        _adjustments = new Dictionary<string, List<AdaptiveParameterAdjustment>>();
        _metrics = new Dictionary<string, TuningMetrics>();
    }

    // Configuration
    public async Task<ParameterTuningConfiguration> CreateTuningConfigurationAsync(
        string workflowId,
        List<string> parameterNames,
        string tuningStrategy,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var config = new ParameterTuningConfiguration
        {
            WorkflowId = workflowId,
            TuningStrategy = tuningStrategy,
            ParametersToTune = GenerateDefaultParameters(parameterNames)
        };

        _configurations[config.ConfigurationId] = config;

        _logger.LogInformation(
            \"Tuning configuration created: WorkflowId={WorkflowId}, Strategy={Strategy}, Parameters={Count}\",
            workflowId, tuningStrategy, parameterNames.Count);

        return config;
    }

    public async Task<ParameterTuningConfiguration> GetTuningConfigurationAsync(
        string configurationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_configurations.TryGetValue(configurationId, out var config))
        {
            return config;
        }

        return null;
    }

    // Optimization
    public async Task<ParameterOptimizationResult> RunParameterOptimizationAsync(
        string configurationId,
        CancellationToken ct = default)
    {
        await Task.Delay(300, ct); // Simulate optimization

        var config = _configurations.TryGetValue(configurationId, out var c) ? c : null;
        if (config == null)
            return null;

        var result = new ParameterOptimizationResult
        {
            ConfigurationId = configurationId,
            PopulationHistory = new List<ParameterVariation>(),
            IterationsCompleted = 0
        };

        var population = InitializePopulation(config);
        var bestFitness = 0.0;
        var stagnationCount = 0;

        for (int iter = 0; iter < config.MaxIterations; iter++)
        {
            await Task.Delay(50, ct); // Simulate iteration work

            // Evaluate population
            foreach (var individual in population)
            {
                individual.FitnessScore = EvaluateFitness(individual);
                individual.TestExecutions++;
            }

            // Track best
            var currentBest = population.OrderByDescending(p => p.FitnessScore).First();
            if (currentBest.FitnessScore > bestFitness)
            {
                bestFitness = currentBest.FitnessScore;
                stagnationCount = 0;
            }
            else
            {
                stagnationCount++;
            }

            result.PopulationHistory.AddRange(population);
            result.IterationsCompleted = iter + 1;

            // Early exit if converged
            if (stagnationCount > 15)
            {
                result.ConvergenceStatus = \"converged\";
                break;
            }

            // Genetic algorithm operators
            if (config.TuningStrategy == \"genetic_algorithm\")
            {
                population = ApplyGeneticOperators(population, config);
            }
            else if (config.TuningStrategy == \"simulated_annealing\")
            {
                population = ApplySimulatedAnnealing(population, iter, config);
            }
        }

        result.BestVariation = result.PopulationHistory.OrderByDescending(p => p.FitnessScore).First();
        result.FitnessImprovement = result.BestVariation.FitnessScore - 50; // Baseline is 50
        result.ConvergenceSpeed = (100.0 - (stagnationCount / config.MaxIterations * 100));
        if (result.ConvergenceStatus != \"converged\")
            result.ConvergenceStatus = result.FitnessImprovement > 20 ? \"improving\" : \"diverged\";

        _results[result.ResultId] = result;
        _populations[configurationId] = result.PopulationHistory.TakeLast(config.PopulationSize).ToList();

        var metrics = new TuningMetrics
        {
            ConfigurationId = configurationId,
            InitialBestFitness = 50,
            CurrentBestFitness = result.BestVariation.FitnessScore,
            AverageFitness = result.PopulationHistory.Average(p => p.FitnessScore),
            ConvergenceRate = (result.BestVariation.FitnessScore - 50) / 50
        };

        _metrics[result.ResultId] = metrics;

        _logger.LogInformation(
            \"Parameter optimization completed: ConfigurationId={ConfigId}, BestFitness={Fitness:F1}, Iterations={Iterations}, Status={Status}\",
            configurationId, result.BestVariation.FitnessScore, result.IterationsCompleted, result.ConvergenceStatus);

        return result;
    }

    public async Task<List<ParameterVariation>> GetOptimizationPopulationAsync(
        string configurationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_populations.TryGetValue(configurationId, out var population))
        {
            return population.OrderByDescending(p => p.FitnessScore).ToList();
        }

        return new List<ParameterVariation>();
    }

    // Parameter management
    public async Task<WorkflowParameter> GetParameterAsync(
        string workflowId,
        string parameterName,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_parameters.TryGetValue(workflowId, out var parameters))
        {
            return parameters.FirstOrDefault(p => p.ParameterName == parameterName);
        }

        return null;
    }

    public async Task<bool> ApplyOptimalParametersAsync(
        string workflowId,
        string resultId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_results.TryGetValue(resultId, out var result))
            return false;

        if (!_parameters.ContainsKey(workflowId))
        {
            _parameters[workflowId] = new List<WorkflowParameter>();
        }

        // Apply best variation parameters
        foreach (var kvp in result.BestVariation.ParameterValues)
        {
            var param = _parameters[workflowId].FirstOrDefault(p => p.ParameterName == kvp.Key);
            if (param != null)
            {
                param.CurrentValue = kvp.Value;
                param.LastTunedAt = DateTime.UtcNow;
                param.EffectivenessScore = result.BestVariation.FitnessScore;
            }
        }

        _logger.LogInformation(
            \"Optimal parameters applied: WorkflowId={WorkflowId}, ResultId={ResultId}, BestFitness={Fitness:F1}\",
            workflowId, resultId, result.BestVariation.FitnessScore);

        return true;
    }

    // Adaptive adjustment
    public async Task<AdaptiveParameterAdjustment> AdjustParameterAsync(
        string workflowId,
        string executionId,
        string parameterName,
        string adjustmentReason,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var parameter = await GetParameterAsync(workflowId, parameterName, ct);
        if (parameter == null)
            return null;

        var previousValue = parameter.CurrentValue;
        var newValue = CalculateAdaptiveAdjustment(parameter, adjustmentReason);

        var adjustment = new AdaptiveParameterAdjustment
        {
            WorkflowId = workflowId,
            ExecutionId = executionId,
            ParameterName = parameterName,
            PreviousValue = previousValue,
            NewValue = newValue,
            AdjustmentReason = adjustmentReason,
            ExpectedImprovementPercent = CalculateExpectedImprovement(adjustmentReason),
            IsSuccessful = Math.Abs(newValue - previousValue) > 0.001
        };

        if (!_adjustments.ContainsKey(workflowId))
        {
            _adjustments[workflowId] = new List<AdaptiveParameterAdjustment>();
        }

        _adjustments[workflowId].Add(adjustment);
        parameter.CurrentValue = newValue;

        _logger.LogInformation(
            \"Parameter adjusted: WorkflowId={WorkflowId}, Parameter={Param}, From={From:F2}, To={To:F2}, Reason={Reason}\",
            workflowId, parameterName, previousValue, newValue, adjustmentReason);

        return adjustment;
    }

    public async Task<List<AdaptiveParameterAdjustment>> GetAdjustmentHistoryAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_adjustments.TryGetValue(workflowId, out var adjustments))
        {
            return adjustments.OrderByDescending(a => a.AdjustedAt).ToList();
        }

        return new List<AdaptiveParameterAdjustment>();
    }

    // Analytics
    public async Task<TuningMetrics> GetTuningMetricsAsync(
        string configurationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var population = _populations.TryGetValue(configurationId, out var p) ? p : new List<ParameterVariation>();

        var metrics = new TuningMetrics
        {
            ConfigurationId = configurationId,
            FitnessHistory = population.Select(v => v.FitnessScore).ToList(),
            CurrentBestFitness = population.Count > 0 ? population.Max(v => v.FitnessScore) : 0,
            AverageFitness = population.Count > 0 ? population.Average(v => v.FitnessScore) : 0,
            InitialBestFitness = 50
        };

        metrics.ConvergenceRate = (metrics.CurrentBestFitness - metrics.InitialBestFitness) / metrics.InitialBestFitness;
        metrics.DiversityHistory = population.Select(v => CalculatePopulationDiversity(population)).Distinct().ToList();

        return metrics;
    }

    public async Task<Dictionary<string, object>> GetAutoTuningAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allResults = _results.Values.ToList();
        var allAdjustments = _adjustments.Values.SelectMany(a => a).ToList();

        var successfulOptimizations = allResults.Count(r => r.ConvergenceStatus == \"converged\");
        var successfulAdjustments = allAdjustments.Count(a => a.IsSuccessful);

        return new Dictionary<string, object>
        {
            [\"total_optimizations_run\"] = allResults.Count,
            [\"successful_optimizations\"] = successfulOptimizations,
            [\"average_fitness_improvement\"] = allResults.Count > 0 ? allResults.Average(r => r.FitnessImprovement) : 0,
            [\"convergence_success_rate\"] = allResults.Count > 0 ? (successfulOptimizations / (double)allResults.Count) * 100 : 0,
            [\"total_adaptive_adjustments\"] = allAdjustments.Count,
            [\"successful_adjustments\"] = successfulAdjustments,
            [\"adjustment_success_rate\"] = allAdjustments.Count > 0 ? (successfulAdjustments / (double)allAdjustments.Count) * 100 : 0,
            [\"average_improvement_from_adaptation\"] = allAdjustments.Count > 0 ? allAdjustments.Average(a => a.ExpectedImprovementPercent) : 0,
            [\"most_common_adjustment_reason\"] = allAdjustments.GroupBy(a => a.AdjustmentReason).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? \"none\",
            [\"average_convergence_speed\"] = allResults.Count > 0 ? allResults.Average(r => r.ConvergenceSpeed) : 0
        };
    }

    // Helpers
    private List<WorkflowParameter> GenerateDefaultParameters(List<string> parameterNames)
    {
        var parameters = new List<WorkflowParameter>();

        var parameterSpecs = new Dictionary<string, (double min, double max, double def)>
        {
            [\"timeout\"] = (100, 30000, 5000),
            [\"retry_count\"] = (1, 10, 3),
            [\"batch_size\"] = (1, 1000, 100),
            [\"parallelism\"] = (1, 128, 4),
            [\"cache_ttl\"] = (60, 3600, 300),
            [\"threshold\"] = (0.1, 1.0, 0.5),
            [\"max_queue_size\"] = (100, 100000, 10000),
            [\"backoff_multiplier\"] = (1.0, 10.0, 2.0)
        };

        foreach (var name in parameterNames)
        {
            var (min, max, def) = parameterSpecs.TryGetValue(name, out var spec)
                ? spec
                : (0, 1000, 500);

            parameters.Add(new WorkflowParameter
            {
                ParameterName = name,
                ParameterType = name,
                CurrentValue = def,
                MinValue = min,
                MaxValue = max,
                DefaultValue = def,
                EffectivenessScore = 50
            });
        }

        return parameters;
    }

    private List<ParameterVariation> InitializePopulation(ParameterTuningConfiguration config)
    {
        var population = new List<ParameterVariation>();

        for (int i = 0; i < config.PopulationSize; i++)
        {
            var variation = new ParameterVariation
            {
                ConfigurationId = config.ConfigurationId,
                ParameterValues = new Dictionary<string, double>(),
                Status = \"candidate\"
            };

            foreach (var param in config.ParametersToTune)
            {
                variation.ParameterValues[param.ParameterName] =
                    param.MinValue + (Random.Shared.NextDouble() * (param.MaxValue - param.MinValue));
            }

            population.Add(variation);
        }

        return population;
    }

    private double EvaluateFitness(ParameterVariation variation)
    {
        // Simulate fitness evaluation based on parameter values
        var baseScore = 50.0;
        var variance = 0.0;

        foreach (var kvp in variation.ParameterValues)
        {
            variance += Math.Sin(kvp.Value / 100.0) * 10;
        }

        var fitness = Math.Min(100, baseScore + variance);
        variation.AveragePerformanceImprovement = (fitness - 50) * 0.8;
        variation.AverageReliabilityChange = (fitness - 50) * 0.3;
        variation.AverageCostChange = -(fitness - 50) * 0.2;

        return Math.Max(0, Math.Min(100, fitness));
    }

    private List<ParameterVariation> ApplyGeneticOperators(
        List<ParameterVariation> population,
        ParameterTuningConfiguration config)
    {
        var newPopulation = new List<ParameterVariation>();

        // Elitism: keep best individuals
        var elite = population.OrderByDescending(p => p.FitnessScore).Take((int)(config.PopulationSize * 0.1)).ToList();
        newPopulation.AddRange(elite);

        // Crossover and mutation
        while (newPopulation.Count < config.PopulationSize)
        {
            var parent1 = population[Random.Shared.Next(population.Count)];
            var parent2 = population[Random.Shared.Next(population.Count)];

            var offspring = new ParameterVariation
            {
                ConfigurationId = config.ConfigurationId,
                ParameterValues = new Dictionary<string, double>(),
                Status = \"candidate\"
            };

            foreach (var kvp in parent1.ParameterValues)
            {
                if (Random.Shared.NextDouble() < config.CrossoverRate)
                {
                    offspring.ParameterValues[kvp.Key] = kvp.Value;
                }
                else if (parent2.ParameterValues.TryGetValue(kvp.Key, out var value))
                {
                    offspring.ParameterValues[kvp.Key] = value;
                }
            }

            // Mutation
            foreach (var kvp in offspring.ParameterValues.ToList())
            {
                if (Random.Shared.NextDouble() < config.MutationRate)
                {
                    var param = config.ParametersToTune.FirstOrDefault(p => p.ParameterName == kvp.Key);
                    if (param != null)
                    {
                        offspring.ParameterValues[kvp.Key] =
                            param.MinValue + (Random.Shared.NextDouble() * (param.MaxValue - param.MinValue));
                    }
                }
            }

            newPopulation.Add(offspring);
        }

        return newPopulation.Take(config.PopulationSize).ToList();
    }

    private List<ParameterVariation> ApplySimulatedAnnealing(
        List<ParameterVariation> population,
        int iteration,
        ParameterTuningConfiguration config)
    {
        var newPopulation = new List<ParameterVariation>(population);
        var temperature = Math.Exp(-iteration / (double)config.MaxIterations);

        foreach (var individual in newPopulation)
        {
            if (Random.Shared.NextDouble() < temperature)
            {
                var param = config.ParametersToTune[Random.Shared.Next(config.ParametersToTune.Count)];
                var paramName = param.ParameterName;

                if (individual.ParameterValues.TryGetValue(paramName, out var value))
                {
                    var delta = (Random.Shared.NextDouble() - 0.5) * (param.MaxValue - param.MinValue);
                    individual.ParameterValues[paramName] = Math.Clamp(
                        value + delta,
                        param.MinValue,
                        param.MaxValue);
                }
            }
        }

        return newPopulation;
    }

    private double CalculateAdaptiveAdjustment(WorkflowParameter parameter, string reason)
    {
        return reason switch
        {
            \"timeout\" => Math.Min(parameter.MaxValue, parameter.CurrentValue * 1.5),
            \"resource_pressure\" => Math.Max(parameter.MinValue, parameter.CurrentValue * 0.8),
            \"degraded_performance\" => Math.Min(parameter.MaxValue, parameter.CurrentValue * 1.3),
            \"learning_feedback\" => Math.Clamp(
                parameter.CurrentValue + (Random.Shared.NextDouble() - 0.5) * parameter.CurrentValue * 0.2,
                parameter.MinValue,
                parameter.MaxValue),
            _ => parameter.CurrentValue
        };
    }

    private double CalculateExpectedImprovement(string reason)
    {
        return reason switch
        {
            \"timeout\" => 15.0,
            \"resource_pressure\" => 12.0,
            \"degraded_performance\" => 18.0,
            \"learning_feedback\" => 8.0,
            _ => 5.0
        };
    }

    private double CalculatePopulationDiversity(List<ParameterVariation> population)
    {
        if (population.Count < 2)
            return 100.0;

        var distances = 0.0;
        var count = 0;

        for (int i = 0; i < Math.Min(population.Count, 10); i++)
        {
            for (int j = i + 1; j < Math.Min(population.Count, 10); j++)
            {
                var ind1 = population[i];
                var ind2 = population[j];

                var distance = ind1.ParameterValues.Sum(kvp =>
                    Math.Abs(kvp.Value - (ind2.ParameterValues.TryGetValue(kvp.Key, out var val) ? val : 0)));

                distances += distance;
                count++;
            }
        }

        return Math.Min(100, count > 0 ? distances / count : 0);
    }
}
