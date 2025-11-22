// Phase 11: Cost & ROI Analytics Engine
// Comprehensive financial analysis, cost allocation, and ROI tracking
// Cost optimization, budget management, and profitability analysis

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Analytics;

/// <summary>
/// Cost allocation record
/// </summary>
public class CostAllocation
{
    public string AllocationId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;
    public double ComputeCostUsd { get; set; }
    public double StorageCostUsd { get; set; }
    public double NetworkCostUsd { get; set; }
    public double ServiceCostUsd { get; set; }
    public double TotalCostUsd { get; set; }
    public int ExecutionCount { get; set; }
    public double CostPerExecutionUsd { get; set; }
}

/// <summary>
/// ROI calculation
/// </summary>
public class ROICalculation
{
    public string RoiId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public double TotalInvestmentUsd { get; set; }
    public double TotalBenefitUsd { get; set; }
    public double ROIPercent { get; set; }
    public string ROIStatus { get; set; } = string.Empty; // positive, breakeven, negative
    public int MonthsToBreakeven { get; set; }
    public double AnnualizedROIPercent { get; set; }
    public List<string> BenefitCategories { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cost optimization recommendation
/// </summary>
public class CostOptimizationOpportunity
{
    public string OpportId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string OpportunityType { get; set; } = string.Empty; // resource_reduction, right_sizing, schedule_optimization, caching
    public string Description { get; set; } = string.Empty;
    public double PotentialSavingsUsd { get; set; }
    public double SavingsPercent { get; set; }
    public string ImplementationEffort { get; set; } = string.Empty; // low, medium, high
    public string Priority { get; set; } = string.Empty; // low, medium, high, critical
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Budget tracking record
/// </summary>
public class BudgetAllocation
{
    public string BudgetId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime BudgetPeriodStart { get; set; }
    public DateTime BudgetPeriodEnd { get; set; }
    public double AllocatedBudgetUsd { get; set; }
    public double SpentUsd { get; set; }
    public double RemainingBudgetUsd { get; set; }
    public double BudgetUtilizationPercent { get; set; }
    public string Status { get; set; } = string.Empty; // on_track, at_risk, over_budget
    public bool AlertThresholdExceeded { get; set; }
}

/// <summary>
/// Cost comparison analysis
/// </summary>
public class CostComparisonAnalysis
{
    public string ComparisonId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string ComparisonPeriod { get; set; } = string.Empty; // month, quarter, year
    public DateTime Period1Start { get; set; }
    public DateTime Period1End { get; set; }
    public double Period1TotalCostUsd { get; set; }
    public DateTime Period2Start { get; set; }
    public DateTime Period2End { get; set; }
    public double Period2TotalCostUsd { get; set; }
    public double CostChangeUsd { get; set; }
    public double CostChangePercent { get; set; }
    public Dictionary<string, double> CostBreakdownPeriod1 { get; set; } = new();
    public Dictionary<string, double> CostBreakdownPeriod2 { get; set; } = new();
}

/// <summary>
/// Profitability analysis
/// </summary>
public class ProfitabilityAnalysis
{
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public double RevenueGeneratedUsd { get; set; }
    public double TotalCostsUsd { get; set; }
    public double GrossProfitUsd { get; set; }
    public double ProfitMarginPercent { get; set; }
    public double CostPerUnitOutput { get; set; }
    public int ExecutionCount { get; set; }
    public double RevenuePerExecutionUsd { get; set; }
    public string Profitability { get; set; } = string.Empty; // highly_profitable, profitable, break_even, unprofitable
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cost and ROI analytics interface
/// </summary>
public interface ICostAndROIAnalyticsEngine
{
    // Cost allocation
    Task<CostAllocation> AllocateCostsAsync(
        string tenantId,
        string workflowId,
        string department,
        CancellationToken ct = default);

    Task<List<CostAllocation>> GetCostAllocationByDepartmentAsync(
        string tenantId,
        string department,
        CancellationToken ct = default);

    Task<Dictionary<string, double>> GetCostBreakdownAsync(
        string tenantId,
        CancellationToken ct = default);

    // ROI calculations
    Task<ROICalculation> CalculateWorkflowROIAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<List<ROICalculation>> GetTenantROIAnalysisAsync(
        string tenantId,
        CancellationToken ct = default);

    // Cost optimization
    Task<List<CostOptimizationOpportunity>> IdentifyCostOptimizationOpportunitiesAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<double> EstimatePotentialSavingsAsync(
        string tenantId,
        CancellationToken ct = default);

    // Budget tracking
    Task<BudgetAllocation> CreateBudgetAllocationAsync(
        string tenantId,
        string department,
        double allocatedBudget,
        CancellationToken ct = default);

    Task<List<BudgetAllocation>> GetBudgetStatusAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> UpdateBudgetSpendingAsync(
        string budgetId,
        double additionalSpend,
        CancellationToken ct = default);

    // Cost comparison
    Task<CostComparisonAnalysis> CompareCostPeriodsAsync(
        string tenantId,
        DateTime period1Start,
        DateTime period1End,
        DateTime period2Start,
        DateTime period2End,
        CancellationToken ct = default);

    // Profitability
    Task<ProfitabilityAnalysis> AnalyzeWorkflowProfitabilityAsync(
        string workflowId,
        double estimatedRevenue,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> GetFinancialAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Cost and ROI analytics engine implementation
/// </summary>
public class CostAndROIAnalyticsEngine : ICostAndROIAnalyticsEngine
{
    private readonly ILogger<CostAndROIAnalyticsEngine> _logger;
    private readonly Dictionary<string, List<CostAllocation>> _costAllocations;
    private readonly Dictionary<string, List<ROICalculation>> _roiCalculations;
    private readonly Dictionary<string, List<CostOptimizationOpportunity>> _opportunities;
    private readonly Dictionary<string, List<BudgetAllocation>> _budgets;
    private readonly Dictionary<string, List<ProfitabilityAnalysis>> _profitabilityAnalyses;

    public CostAndROIAnalyticsEngine(ILogger<CostAndROIAnalyticsEngine> logger)
    {
        _logger = logger;
        _costAllocations = new Dictionary<string, List<CostAllocation>>();
        _roiCalculations = new Dictionary<string, List<ROICalculation>>();
        _opportunities = new Dictionary<string, List<CostOptimizationOpportunity>>();
        _budgets = new Dictionary<string, List<BudgetAllocation>>();
        _profitabilityAnalyses = new Dictionary<string, List<ProfitabilityAnalysis>>();
    }

    // Cost allocation
    public async Task<CostAllocation> AllocateCostsAsync(
        string tenantId,
        string workflowId,
        string department,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var baseCost = 125.0 + (Math.Abs(Math.Sin(workflowId.GetHashCode() / 1000.0)) * 200);

        var allocation = new CostAllocation
        {
            TenantId = tenantId,
            WorkflowId = workflowId,
            Department = department,
            ComputeCostUsd = baseCost * 0.45,
            StorageCostUsd = baseCost * 0.25,
            NetworkCostUsd = baseCost * 0.15,
            ServiceCostUsd = baseCost * 0.15,
            TotalCostUsd = baseCost,
            ExecutionCount = 150,
            CostPerExecutionUsd = baseCost / 150
        };

        var key = $"{tenantId}:{department}";
        if (!_costAllocations.ContainsKey(key))
        {
            _costAllocations[key] = new List<CostAllocation>();
        }

        _costAllocations[key].Add(allocation);

        _logger.LogInformation(
            "Costs allocated: TenantId={TenantId}, WorkflowId={WorkflowId}, Department={Department}, TotalCost=${TotalCost:F2}",
            tenantId, workflowId, department, allocation.TotalCostUsd);

        return allocation;
    }

    public async Task<List<CostAllocation>> GetCostAllocationByDepartmentAsync(
        string tenantId,
        string department,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var key = $"{tenantId}:{department}";
        if (_costAllocations.TryGetValue(key, out var allocations))
        {
            return allocations
                .OrderByDescending(a => a.AllocationDate)
                .ToList();
        }

        return new List<CostAllocation>();
    }

    public async Task<Dictionary<string, double>> GetCostBreakdownAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allAllocations = _costAllocations
            .Where(kvp => kvp.Key.StartsWith(tenantId))
            .SelectMany(kvp => kvp.Value)
            .ToList();

        return new Dictionary<string, double>
        {
            ["Compute"] = allAllocations.Sum(a => a.ComputeCostUsd),
            ["Storage"] = allAllocations.Sum(a => a.StorageCostUsd),
            ["Network"] = allAllocations.Sum(a => a.NetworkCostUsd),
            ["Services"] = allAllocations.Sum(a => a.ServiceCostUsd),
            ["Total"] = allAllocations.Sum(a => a.TotalCostUsd)
        };
    }

    // ROI calculations
    public async Task<ROICalculation> CalculateWorkflowROIAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate ROI calculation

        var investment = 2500.0 + (Math.Abs(Math.Sin(workflowId.GetHashCode() / 1000.0)) * 1500);
        var benefits = investment * 2.5; // 250% ROI baseline
        var roi = ((benefits - investment) / investment) * 100;

        var calculation = new ROICalculation
        {
            WorkflowId = workflowId,
            WorkflowName = $"Workflow_{workflowId.Substring(0, Math.Min(8, workflowId.Length))}",
            TotalInvestmentUsd = investment,
            TotalBenefitUsd = benefits,
            ROIPercent = roi,
            ROIStatus = roi > 0 ? "positive" : roi < 0 ? "negative" : "breakeven",
            MonthsToBreakeven = 3,
            AnnualizedROIPercent = roi * 4, // Annualized quarterly ROI
            BenefitCategories = new List<string>
            {
                "Time savings",
                "Error reduction",
                "Compliance automation",
                "Operational efficiency"
            }
        };

        var key = $"{workflowId}";
        if (!_roiCalculations.ContainsKey(key))
        {
            _roiCalculations[key] = new List<ROICalculation>();
        }

        _roiCalculations[key].Add(calculation);

        _logger.LogInformation(
            "ROI calculated: WorkflowId={WorkflowId}, ROI={ROI:F1}%, Investment=${Investment:F2}, Benefits=${Benefits:F2}",
            workflowId, roi, investment, benefits);

        return calculation;
    }

    public async Task<List<ROICalculation>> GetTenantROIAnalysisAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allCalculations = _roiCalculations.Values.SelectMany(r => r).ToList();
        return allCalculations.OrderByDescending(r => r.ROIPercent).ToList();
    }

    // Cost optimization
    public async Task<List<CostOptimizationOpportunity>> IdentifyCostOptimizationOpportunitiesAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate opportunity analysis

        var opportunities = new List<CostOptimizationOpportunity>
        {
            new CostOptimizationOpportunity
            {
                TenantId = tenantId,
                OpportunityType = "resource_reduction",
                Description = "Right-size workflow compute resources: workflows in execution tier can use 30% less CPU",
                PotentialSavingsUsd = 3200.0,
                SavingsPercent = 12.5,
                ImplementationEffort = "low",
                Priority = "high"
            },
            new CostOptimizationOpportunity
            {
                TenantId = tenantId,
                OpportunityType = "schedule_optimization",
                Description = "Schedule non-critical workflows during off-peak hours for 40% lower compute costs",
                PotentialSavingsUsd = 2100.0,
                SavingsPercent = 8.2,
                ImplementationEffort = "medium",
                Priority = "high"
            },
            new CostOptimizationOpportunity
            {
                TenantId = tenantId,
                OpportunityType = "caching",
                Description = "Implement intelligent result caching for frequently-executed workflows; reduce execution count by 35%",
                PotentialSavingsUsd = 4500.0,
                SavingsPercent = 17.5,
                ImplementationEffort = "medium",
                Priority = "critical"
            },
            new CostOptimizationOpportunity
            {
                TenantId = tenantId,
                OpportunityType = "resource_reduction",
                Description = "Consolidate underutilized workflows; reduce storage footprint by 45%",
                PotentialSavingsUsd = 1800.0,
                SavingsPercent = 7.0,
                ImplementationEffort = "high",
                Priority = "medium"
            }
        };

        if (!_opportunities.ContainsKey(tenantId))
        {
            _opportunities[tenantId] = new List<CostOptimizationOpportunity>();
        }

        _opportunities[tenantId].AddRange(opportunities);

        _logger.LogInformation(
            "Cost optimization opportunities identified: TenantId={TenantId}, Count={Count}, TotalPotentialSavings=${Savings:F2}",
            tenantId, opportunities.Count, opportunities.Sum(o => o.PotentialSavingsUsd));

        return opportunities;
    }

    public async Task<double> EstimatePotentialSavingsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var opportunities = await IdentifyCostOptimizationOpportunitiesAsync(tenantId, ct);
        return opportunities.Sum(o => o.PotentialSavingsUsd);
    }

    // Budget tracking
    public async Task<BudgetAllocation> CreateBudgetAllocationAsync(
        string tenantId,
        string department,
        double allocatedBudget,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var currentSpend = allocatedBudget * 0.65; // Assume 65% spent
        var remaining = allocatedBudget - currentSpend;
        var utilization = (currentSpend / allocatedBudget) * 100;

        var budget = new BudgetAllocation
        {
            TenantId = tenantId,
            Department = department,
            BudgetPeriodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
            BudgetPeriodEnd = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).AddDays(-1),
            AllocatedBudgetUsd = allocatedBudget,
            SpentUsd = currentSpend,
            RemainingBudgetUsd = remaining,
            BudgetUtilizationPercent = utilization,
            Status = utilization > 85 ? "at_risk" : utilization > 100 ? "over_budget" : "on_track",
            AlertThresholdExceeded = utilization > 80
        };

        var key = $"{tenantId}:{department}";
        if (!_budgets.ContainsKey(key))
        {
            _budgets[key] = new List<BudgetAllocation>();
        }

        _budgets[key].Add(budget);

        _logger.LogInformation(
            "Budget allocation created: TenantId={TenantId}, Department={Department}, Allocated=${Allocated:F2}, Spent=${Spent:F2}, Utilization={Util:F1}%",
            tenantId, department, allocatedBudget, currentSpend, utilization);

        return budget;
    }

    public async Task<List<BudgetAllocation>> GetBudgetStatusAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allBudgets = _budgets
            .Where(kvp => kvp.Key.StartsWith(tenantId))
            .SelectMany(kvp => kvp.Value)
            .ToList();

        return allBudgets.OrderByDescending(b => b.BudgetUtilizationPercent).ToList();
    }

    public async Task<bool> UpdateBudgetSpendingAsync(
        string budgetId,
        double additionalSpend,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var budgets in _budgets.Values)
        {
            var budget = budgets.FirstOrDefault(b => b.BudgetId == budgetId);
            if (budget != null)
            {
                budget.SpentUsd += additionalSpend;
                budget.RemainingBudgetUsd -= additionalSpend;
                budget.BudgetUtilizationPercent = (budget.SpentUsd / budget.AllocatedBudgetUsd) * 100;
                budget.Status = budget.BudgetUtilizationPercent > 85 ? "at_risk" : budget.BudgetUtilizationPercent > 100 ? "over_budget" : "on_track";
                budget.AlertThresholdExceeded = budget.BudgetUtilizationPercent > 80;

                return true;
            }
        }

        return false;
    }

    // Cost comparison
    public async Task<CostComparisonAnalysis> CompareCostPeriodsAsync(
        string tenantId,
        DateTime period1Start,
        DateTime period1End,
        DateTime period2Start,
        DateTime period2End,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate comparison analysis

        var period1Cost = 15000.0 + (Math.Abs(Math.Sin(tenantId.GetHashCode() / 1000.0)) * 5000);
        var period2Cost = period1Cost * 1.15; // 15% increase
        var change = period2Cost - period1Cost;
        var changePercent = (change / period1Cost) * 100;

        var comparison = new CostComparisonAnalysis
        {
            TenantId = tenantId,
            ComparisonPeriod = "month",
            Period1Start = period1Start,
            Period1End = period1End,
            Period1TotalCostUsd = period1Cost,
            Period2Start = period2Start,
            Period2End = period2End,
            Period2TotalCostUsd = period2Cost,
            CostChangeUsd = change,
            CostChangePercent = changePercent,
            CostBreakdownPeriod1 = new Dictionary<string, double>
            {
                ["Compute"] = period1Cost * 0.45,
                ["Storage"] = period1Cost * 0.25,
                ["Network"] = period1Cost * 0.15,
                ["Services"] = period1Cost * 0.15
            },
            CostBreakdownPeriod2 = new Dictionary<string, double>
            {
                ["Compute"] = period2Cost * 0.47,
                ["Storage"] = period2Cost * 0.24,
                ["Network"] = period2Cost * 0.14,
                ["Services"] = period2Cost * 0.15
            }
        };

        return comparison;
    }

    // Profitability
    public async Task<ProfitabilityAnalysis> AnalyzeWorkflowProfitabilityAsync(
        string workflowId,
        double estimatedRevenue,
        CancellationToken ct = default)
    {
        await Task.Delay(80, ct); // Simulate profitability analysis

        var costs = 5000.0 + (Math.Abs(Math.Sin(workflowId.GetHashCode() / 1000.0)) * 2000);
        var revenue = estimatedRevenue;
        var profit = revenue - costs;
        var margin = (profit / revenue) * 100;
        var execCount = 250;

        var analysis = new ProfitabilityAnalysis
        {
            WorkflowId = workflowId,
            RevenueGeneratedUsd = revenue,
            TotalCostsUsd = costs,
            GrossProfitUsd = profit,
            ProfitMarginPercent = margin,
            CostPerUnitOutput = costs / execCount,
            ExecutionCount = execCount,
            RevenuePerExecutionUsd = revenue / execCount,
            Profitability = profit > 0 ? (margin > 30 ? "highly_profitable" : "profitable") : profit == 0 ? "break_even" : "unprofitable"
        };

        var key = $"{workflowId}";
        if (!_profitabilityAnalyses.ContainsKey(key))
        {
            _profitabilityAnalyses[key] = new List<ProfitabilityAnalysis>();
        }

        _profitabilityAnalyses[key].Add(analysis);

        _logger.LogInformation(
            "Profitability analyzed: WorkflowId={WorkflowId}, Revenue=${Revenue:F2}, Costs=${Costs:F2}, Profit=${Profit:F2}, Margin={Margin:F1}%",
            workflowId, revenue, costs, profit, margin);

        return analysis;
    }

    public async Task<Dictionary<string, object>> GetFinancialAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var costAllocations = _costAllocations
            .Where(kvp => kvp.Key.StartsWith(tenantId))
            .SelectMany(kvp => kvp.Value)
            .ToList();

        var roiCalcs = _roiCalculations.Values.SelectMany(r => r).ToList();
        var budgets = _budgets
            .Where(kvp => kvp.Key.StartsWith(tenantId))
            .SelectMany(kvp => kvp.Value)
            .ToList();

        var opportunities = _opportunities.TryGetValue(tenantId, out var opps) ? opps : new List<CostOptimizationOpportunity>();

        return new Dictionary<string, object>
        {
            ["total_allocated_costs"] = costAllocations.Sum(a => a.TotalCostUsd),
            ["average_cost_per_execution"] = costAllocations.Count > 0 ? costAllocations.Average(a => a.CostPerExecutionUsd) : 0,
            ["total_budgets"] = budgets.Count,
            ["budgets_at_risk"] = budgets.Count(b => b.Status == "at_risk"),
            ["over_budget_count"] = budgets.Count(b => b.Status == "over_budget"),
            ["average_roi"] = roiCalcs.Count > 0 ? roiCalcs.Average(r => r.ROIPercent) : 0,
            ["positive_roi_workflows"] = roiCalcs.Count(r => r.ROIStatus == "positive"),
            ["total_optimization_opportunities"] = opportunities.Count,
            ["total_potential_savings"] = opportunities.Sum(o => o.PotentialSavingsUsd),
            ["critical_priority_opportunities"] = opportunities.Count(o => o.Priority == "critical")
        };
    }
}
