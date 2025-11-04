#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.FinOps;

/// <summary>
/// FinOps & Cost Optimization Patterns
/// Resource optimization, AI-powered scaling, cost visibility, tagging strategies
/// </summary>

/// <summary>
/// Cost allocation tag
/// </summary>
public class CostTag
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }
}

/// <summary>
/// Resource cost information
/// </summary>
public class ResourceCost
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty; // Pod, Node, Deployment, Service

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("hourlyRate")]
    public decimal HourlyRate { get; set; }

    [JsonPropertyName("dailyCost")]
    public decimal DailyCost { get; set; }

    [JsonPropertyName("monthlyCost")]
    public decimal MonthlyCost { get; set; }

    [JsonPropertyName("cpuRequest")]
    public string CpuRequest { get; set; } = string.Empty;

    [JsonPropertyName("memoryRequest")]
    public string MemoryRequest { get; set; } = string.Empty;

    [JsonPropertyName("utilizationPercent")]
    public double UtilizationPercent { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cost optimization recommendation
/// </summary>
public class CostOptimizationRecommendation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // RightSizing, DeleteUnused, SpotInstances, Consolidation

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("estimatedMonthlySavings")]
    public decimal EstimatedMonthlySavings { get; set; }

    [JsonPropertyName("estimatedAnnualSavings")]
    public decimal EstimatedAnnualSavings { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

    [JsonPropertyName("effort")]
    public string Effort { get; set; } = "Medium"; // Low, Medium, High

    [JsonPropertyName("implementationSteps")]
    public List<string> ImplementationSteps { get; set; } = new();

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "Low";

    [JsonPropertyName("roi")]
    public double ReturnOnInvestment { get; set; } // Calculated ROI
}

/// <summary>
/// Chargeback model for cost allocation
/// </summary>
public class ChargebackModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Shared, Proportional, Consumption

    [JsonPropertyName("allocatedCost")]
    public decimal AllocatedCost { get; set; }

    [JsonPropertyName("departments")]
    public Dictionary<string, decimal> DepartmentAllocations { get; set; } = new();

    [JsonPropertyName("period")]
    public string Period { get; set; } = "Monthly";
}

/// <summary>
/// Cost forecast using trend analysis
/// </summary>
public class CostForecast
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("projectedCost")]
    public decimal ProjectedCost { get; set; }

    [JsonPropertyName("confidenceLevel")]
    public double ConfidenceLevel { get; set; } = 0.95; // 0-1

    [JsonPropertyName("historicalTrend")]
    public List<(DateTime date, decimal cost)> HistoricalTrend { get; set; } = new();

    [JsonPropertyName("anomalies")]
    public List<(DateTime date, decimal cost, string reason)> Anomalies { get; set; } = new();
}

/// <summary>
/// Budget and alerts
/// </summary>
public class Budget
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("limit")]
    public decimal Limit { get; set; }

    [JsonPropertyName("spent")]
    public decimal Spent { get; set; }

    [JsonPropertyName("remaining")]
    public decimal Remaining => Limit - Spent;

    [JsonPropertyName("percentUsed")]
    public double PercentUsed => (double)Spent / (double)Limit * 100;

    [JsonPropertyName("alerts")]
    public List<BudgetAlert> Alerts { get; set; } = new();

    [JsonPropertyName("period")]
    public string Period { get; set; } = "Monthly";

    [JsonPropertyName("resetDate")]
    public DateTime ResetDate { get; set; }
}

/// <summary>
/// Budget alert threshold
/// </summary>
public class BudgetAlert
{
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.8; // 80% of budget

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Warning"; // Info, Warning, Critical

    [JsonPropertyName("channels")]
    public List<string> Channels { get; set; } = new(); // email, slack, pagerduty
}

/// <summary>
/// FinOps engine for cost management
/// </summary>
public class FinOpsEngine
{
    private readonly ConcurrentDictionary<string, ResourceCost> _resourceCosts = new();
    private readonly ConcurrentDictionary<string, CostOptimizationRecommendation> _recommendations = new();
    private readonly List<CostTag> _mandatoryTags = new();
    private readonly List<Budget> _budgets = new();
    private readonly ILogger<FinOpsEngine> _logger;

    public FinOpsEngine(ILogger<FinOpsEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register resource cost
    /// </summary>
    public async Task RegisterResourceCostAsync(ResourceCost cost)
    {
        _resourceCosts.AddOrUpdate(cost.ResourceId, cost, (_, _) => cost);

        _logger.LogInformation(
            "Registered resource cost: {Resource} ({Type}) - Daily: ${Daily}, Monthly: ${Monthly}",
            cost.ResourceId,
            cost.ResourceType,
            cost.DailyCost,
            cost.MonthlyCost);
    }

    /// <summary>
    /// Add cost optimization recommendation
    /// </summary>
    public async Task AddRecommendationAsync(CostOptimizationRecommendation recommendation)
    {
        _recommendations.AddOrUpdate(recommendation.Id, recommendation, (_, _) => recommendation);

        _logger.LogInformation(
            "Generated recommendation: {Title} - Est. Monthly Savings: ${Savings}",
            recommendation.Title,
            recommendation.EstimatedMonthlySavings);
    }

    /// <summary>
    /// Analyze underutilized resources (right-sizing)
    /// </summary>
    public async Task<List<CostOptimizationRecommendation>> AnalyzeUnderutilizedResourcesAsync(
        double utilizationThreshold = 30)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var resource in _resourceCosts.Values)
        {
            if (resource.UtilizationPercent < utilizationThreshold)
            {
                var savingsPercent = (100 - resource.UtilizationPercent) / 100;
                var monthlySavings = resource.MonthlyCost * (decimal)savingsPercent;

                var rec = new CostOptimizationRecommendation
                {
                    ResourceId = resource.ResourceId,
                    Type = "RightSizing",
                    Title = $"Right-size {resource.ResourceType}: {resource.ResourceId}",
                    Description = $"Resource utilization is only {resource.UtilizationPercent:F1}%",
                    EstimatedMonthlySavings = monthlySavings,
                    EstimatedAnnualSavings = monthlySavings * 12,
                    Priority = resource.UtilizationPercent < 10 ? "Critical" : "High",
                    Effort = "Low"
                };

                recommendations.Add(rec);
                await AddRecommendationAsync(rec);
            }
        }

        return recommendations;
    }

    /// <summary>
    /// Identify unused resources
    /// </summary>
    public async Task<List<CostOptimizationRecommendation>> IdentifyUnusedResourcesAsync(
        TimeSpan inactivityThreshold)
    {
        var recommendations = new List<CostOptimizationRecommendation>();

        foreach (var resource in _resourceCosts.Values)
        {
            if (DateTime.UtcNow - resource.LastUpdated > inactivityThreshold &&
                resource.UtilizationPercent < 5)
            {
                var rec = new CostOptimizationRecommendation
                {
                    ResourceId = resource.ResourceId,
                    Type = "DeleteUnused",
                    Title = $"Delete unused resource: {resource.ResourceId}",
                    Description = $"No activity for {inactivityThreshold.TotalDays} days",
                    EstimatedMonthlySavings = resource.MonthlyCost,
                    EstimatedAnnualSavings = resource.MonthlyCost * 12,
                    Priority = "High",
                    Effort = "Low"
                };

                recommendations.Add(rec);
                await AddRecommendationAsync(rec);
            }
        }

        return recommendations;
    }

    /// <summary>
    /// Calculate total infrastructure cost
    /// </summary>
    public Dictionary<string, object> GetTotalCosts()
    {
        var dailyTotal = _resourceCosts.Values.Sum(r => r.DailyCost);
        var monthlyTotal = _resourceCosts.Values.Sum(r => r.MonthlyCost);
        var annualTotal = monthlyTotal * 12;

        var byCostCenter = _resourceCosts.Values
            .GroupBy(r => r.Tags.TryGetValue("CostCenter", out var cc) ? cc : "Untagged")
            .ToDictionary(g => g.Key, g => g.Sum(r => r.MonthlyCost));

        var byNamespace = _resourceCosts.Values
            .GroupBy(r => r.Namespace)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.MonthlyCost));

        return new()
        {
            ["dailyCost"] = Math.Round(dailyTotal, 2),
            ["monthlyCost"] = Math.Round(monthlyTotal, 2),
            ["annualCost"] = Math.Round(annualTotal, 2),
            ["resourceCount"] = _resourceCosts.Count,
            ["byCostCenter"] = byCostCenter.ToDictionary(k => k.Key, v => (object)Math.Round(v.Value, 2)),
            ["byNamespace"] = byNamespace.ToDictionary(k => k.Key, v => (object)Math.Round(v.Value, 2))
        };
    }

    /// <summary>
    /// Forecast cost trend
    /// </summary>
    public CostForecast ForecastCosts(int forecastDaysAhead = 30)
    {
        var historicalData = _resourceCosts.Values
            .OrderBy(r => r.LastUpdated)
            .GroupBy(r => r.LastUpdated.Date)
            .Select(g => (date: g.Key, cost: g.Sum(r => r.DailyCost)))
            .ToList();

        if (historicalData.Count < 2)
        {
            return new()
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(forecastDaysAhead),
                ProjectedCost = _resourceCosts.Values.Sum(r => r.MonthlyCost),
                ConfidenceLevel = 0.5
            };
        }

        var trend = CalculateTrend(historicalData);
        var lastCost = historicalData.Last().cost;
        var projectedCost = lastCost + (trend * forecastDaysAhead);

        var forecast = new CostForecast
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(forecastDaysAhead),
            ProjectedCost = projectedCost,
            HistoricalTrend = historicalData
        };

        return forecast;
    }

    /// <summary>
    /// Create budget with alerts
    /// </summary>
    public async Task<Budget> CreateBudgetAsync(
        string name,
        decimal limit,
        List<BudgetAlert>? alerts = null)
    {
        var budget = new Budget
        {
            Name = name,
            Limit = limit,
            Alerts = alerts ?? new List<BudgetAlert>
            {
                new() { Threshold = 0.5, Severity = "Info" },
                new() { Threshold = 0.8, Severity = "Warning" },
                new() { Threshold = 1.0, Severity = "Critical" }
            },
            ResetDate = DateTime.UtcNow.AddMonths(1)
        };

        _budgets.Add(budget);

        _logger.LogInformation(
            "Created budget: {Name} - Limit: ${Limit}",
            name,
            limit);

        return budget;
    }

    /// <summary>
    /// Get budget status
    /// </summary>
    public Budget? GetBudgetStatus(string budgetName)
    {
        var budget = _budgets.FirstOrDefault(b => b.Name == budgetName);
        if (budget != null)
        {
            budget.Spent = _resourceCosts.Values.Sum(r => r.MonthlyCost);
        }
        return budget;
    }

    /// <summary>
    /// Get optimization opportunities
    /// </summary>
    public async Task<List<CostOptimizationRecommendation>> GetOptimizationOpportunitiesAsync()
    {
        var opportunities = new List<CostOptimizationRecommendation>();

        opportunities.AddRange(await AnalyzeUnderutilizedResourcesAsync());
        opportunities.AddRange(await IdentifyUnusedResourcesAsync(TimeSpan.FromDays(30)));

        return opportunities.OrderByDescending(o => o.EstimatedMonthlySavings).ToList();
    }

    /// <summary>
    /// Get FinOps stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        var costs = GetTotalCosts();
        var opportunities = _recommendations.Values.ToList();

        return new()
        {
            ["totalResources"] = _resourceCosts.Count,
            ["monthlyCost"] = costs["monthlyCost"],
            ["totalRecommendations"] = _recommendations.Count,
            ["potentialMonthlySavings"] = Math.Round(
                opportunities.Sum(r => r.EstimatedMonthlySavings), 2),
            ["averageUtilization"] = Math.Round(
                _resourceCosts.Values.Average(r => r.UtilizationPercent), 2),
            ["budgets"] = _budgets.Count
        };
    }

    private decimal CalculateTrend(List<(DateTime date, decimal cost)> data)
    {
        if (data.Count < 2)
            return 0;

        var x = Enumerable.Range(0, data.Count).Select(i => (double)i).ToList();
        var y = data.Select(d => (double)d.cost).ToList();

        var avgX = x.Average();
        var avgY = y.Average();

        var numerator = x.Zip(y, (xi, yi) => (xi - avgX) * (yi - avgY)).Sum();
        var denominator = x.Select(xi => (xi - avgX) * (xi - avgX)).Sum();

        return denominator == 0 ? 0 : (decimal)(numerator / denominator);
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class FinOpsExtensions
{
    public static IServiceCollection AddFinOps(this IServiceCollection services)
    {
        services.AddSingleton<FinOpsEngine>();
        return services;
    }
}
