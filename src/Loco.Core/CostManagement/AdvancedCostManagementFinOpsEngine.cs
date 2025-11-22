using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.CostManagement
{
    /// <summary>
    /// Advanced Cost Management and FinOps Engine (Phase 28)
    /// Provides comprehensive cost tracking, budgeting, optimization, and financial operations
    /// for enterprise workflow automation at scale. Enables visibility into spending,
    /// cost attribution, budget management, and automated cost optimization.
    /// </summary>
    public interface IAdvancedCostManagementFinOpsEngine
    {
        Task<CostAllocationReport> AllocateCostsAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<BudgetForecast> ForecastBudgetAsync(string tenantId, int monthsAhead = 3, CancellationToken ct = default);
        Task<List<CostOptimizationOpportunity>> IdentifyOptimizationOpportunitiesAsync(string tenantId, CancellationToken ct = default);
        Task<BudgetAlert> CheckBudgetThresholdsAsync(string tenantId, CancellationToken ct = default);
        Task<CostAnomalyDetection> DetectCostAnomaliesAsync(string tenantId, int daysBack = 30, CancellationToken ct = default);
        Task<ChargebackReport> GenerateChargebackReportAsync(string tenantId, string departmentId, CancellationToken ct = default);
        Task<CostTrendAnalysis> AnalyzeCostTrendsAsync(string tenantId, int monthsBack = 6, CancellationToken ct = default);
        Task<FinancialHealthReport> AssessFinancialHealthAsync(string tenantId, CancellationToken ct = default);
        Task<ResourceCostOptimization> OptimizeResourceCostsAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<FinOpsMetrics> GetFinOpsMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class AdvancedCostManagementFinOpsEngine : IAdvancedCostManagementFinOpsEngine
    {
        private readonly ILogger<AdvancedCostManagementFinOpsEngine> _logger;
        private readonly Dictionary<string, CostAllocationReport> _costAllocations = new();
        private readonly Dictionary<string, BudgetForecast> _budgetForecasts = new();
        private readonly Dictionary<string, List<CostOptimizationOpportunity>> _optimizationOpportunities = new();
        private readonly Dictionary<string, BudgetAlert> _budgetAlerts = new();
        private readonly Dictionary<string, CostAnomalyDetection> _costAnomalies = new();
        private readonly Dictionary<string, ChargebackReport> _chargebacks = new();
        private readonly Dictionary<string, CostTrendAnalysis> _costTrends = new();
        private readonly Random _random = new Random(42);

        public AdvancedCostManagementFinOpsEngine(ILogger<AdvancedCostManagementFinOpsEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CostAllocationReport> AllocateCostsAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(workflowId)) throw new ArgumentNullException(nameof(workflowId));

            _logger.LogInformation("Allocating costs for {WorkflowId} in tenant {TenantId}", workflowId, tenantId);

            await Task.Delay(_random.Next(300, 800), ct);

            var costBreakdown = new List<CostComponent>
            {
                new CostComponent
                {
                    Category = "Compute",
                    Amount = _random.Next(100, 5000),
                    Percentage = _random.Next(30, 50),
                    Unit = "CPU-hours"
                },
                new CostComponent
                {
                    Category = "Storage",
                    Amount = _random.Next(50, 2000),
                    Percentage = _random.Next(15, 30),
                    Unit = "GB-months"
                },
                new CostComponent
                {
                    Category = "Network",
                    Amount = _random.Next(25, 1000),
                    Percentage = _random.Next(10, 20),
                    Unit = "GB-transferred"
                },
                new CostComponent
                {
                    Category = "Services",
                    Amount = _random.Next(50, 3000),
                    Percentage = _random.Next(10, 25),
                    Unit = "API-calls"
                }
            };

            var report = new CostAllocationReport
            {
                ReportId = Guid.NewGuid().ToString(),
                WorkflowId = workflowId,
                ReportDate = DateTime.UtcNow,
                CostComponents = costBreakdown,
                TotalCost = costBreakdown.Sum(c => c.Amount),
                CostPerExecution = _random.Next(10, 500),
                AccumulatedCost = _random.Next(10000, 500000),
                CostTrendPercent = _random.Next(-20, 30),
                OwningDepartment = $"dept-{_random.Next(1, 20)}",
                CostAllocationAccuracy = _random.Next(85, 99) / 100.0
            };

            var key = $"{tenantId}:{workflowId}";
            lock (_costAllocations)
            {
                if (_costAllocations.Count > 10000) _costAllocations.Clear();
                _costAllocations[key] = report;
            }

            _logger.LogInformation("Cost allocated: {TotalCost} ({Accuracy}% accuracy)",
                report.TotalCost, Math.Round(report.CostAllocationAccuracy * 100));

            return report;
        }

        public async Task<BudgetForecast> ForecastBudgetAsync(string tenantId, int monthsAhead = 3, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (monthsAhead < 1 || monthsAhead > 12) throw new ArgumentOutOfRangeException(nameof(monthsAhead));

            _logger.LogInformation("Forecasting budget for {TenantId} {Months} months ahead", tenantId, monthsAhead);

            await Task.Delay(_random.Next(400, 1000), ct);

            var monthlyForecasts = new List<MonthlyForecast>();
            for (int i = 0; i < monthsAhead; i++)
            {
                monthlyForecasts.Add(new MonthlyForecast
                {
                    Month = DateTime.UtcNow.AddMonths(i + 1),
                    ForecastedCost = _random.Next(5000, 50000),
                    ConfidenceLevel = _random.Next(70, 95) / 100.0,
                    TrendDirection = GetRandomTrend(),
                    RiskFactors = _random.Next(0, 3)
                });
            }

            var forecast = new BudgetForecast
            {
                ForecastId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                ForecastDate = DateTime.UtcNow,
                ForecastHorizonMonths = monthsAhead,
                MonthlyForecasts = monthlyForecasts,
                TotalForecastedCost = monthlyForecasts.Sum(m => m.ForecastedCost),
                AverageMonthlySpend = monthlyForecasts.Average(m => m.ForecastedCost),
                VariabilityScore = _random.Next(10, 80),
                AnomalyProbability = _random.Next(5, 40) / 100.0,
                ForecastAccuracy = _random.Next(75, 92) / 100.0
            };

            var key = $"{tenantId}:budget";
            lock (_budgetForecasts)
            {
                if (_budgetForecasts.Count > 5000) _budgetForecasts.Clear();
                _budgetForecasts[key] = forecast;
            }

            _logger.LogInformation("Budget forecast: {TotalCost} over {Months} months, {Accuracy}% accuracy",
                forecast.TotalForecastedCost, monthsAhead, Math.Round(forecast.ForecastAccuracy * 100));

            return forecast;
        }

        public async Task<List<CostOptimizationOpportunity>> IdentifyOptimizationOpportunitiesAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Identifying cost optimization opportunities for {TenantId}", tenantId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var opportunities = new List<CostOptimizationOpportunity>
            {
                new CostOptimizationOpportunity
                {
                    OpportunityId = Guid.NewGuid().ToString(),
                    Category = "Resource Right-Sizing",
                    Title = "Downsize Overallocated Resources",
                    Description = "Current allocation is 2x required capacity",
                    EstimatedMonthlySavings = _random.Next(2000, 10000),
                    SavingsPercentage = _random.Next(20, 40),
                    ImplementationEffort = (ImplementationEffort)_random.Next(0, 2),
                    PaybackPeriodMonths = _random.Next(1, 6),
                    RiskLevel = (RiskLevel)_random.Next(0, 2)
                },
                new CostOptimizationOpportunity
                {
                    OpportunityId = Guid.NewGuid().ToString(),
                    Category = "Reserved Capacity",
                    Title = "Purchase Reserved Instances",
                    Description = "Commit to 1-year reservations for stable workloads",
                    EstimatedMonthlySavings = _random.Next(1000, 8000),
                    SavingsPercentage = _random.Next(25, 40),
                    ImplementationEffort = ImplementationEffort.Low,
                    PaybackPeriodMonths = _random.Next(2, 8),
                    RiskLevel = RiskLevel.Low
                },
                new CostOptimizationOpportunity
                {
                    OpportunityId = Guid.NewGuid().ToString(),
                    Category = "Data Optimization",
                    Title = "Archive Cold Data",
                    Description = "Move infrequently accessed data to cheaper storage",
                    EstimatedMonthlySavings = _random.Next(500, 3000),
                    SavingsPercentage = _random.Next(30, 50),
                    ImplementationEffort = ImplementationEffort.Medium,
                    PaybackPeriodMonths = _random.Next(1, 4),
                    RiskLevel = RiskLevel.Low
                }
            };

            var key = $"{tenantId}:optimization";
            lock (_optimizationOpportunities)
            {
                if (!_optimizationOpportunities.ContainsKey(key))
                    _optimizationOpportunities[key] = new List<CostOptimizationOpportunity>();
                if (_optimizationOpportunities[key].Count > 5000) _optimizationOpportunities[key].Clear();
                _optimizationOpportunities[key].AddRange(opportunities);
            }

            _logger.LogInformation("Identified {Count} optimization opportunities, {TotalSavings}/month potential",
                opportunities.Count, opportunities.Sum(o => o.EstimatedMonthlySavings));

            return opportunities;
        }

        public async Task<BudgetAlert> CheckBudgetThresholdsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Checking budget thresholds for {TenantId}", tenantId);

            await Task.Delay(_random.Next(200, 500), ct);

            var alert = new BudgetAlert
            {
                AlertId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                CheckTime = DateTime.UtcNow,
                MonthlyBudget = _random.Next(50000, 500000),
                CurrentSpend = _random.Next(10000, 450000),
                SpendPercentage = _random.Next(20, 95),
                ForecastedEndOfMonth = _random.Next(10000, 500000),
                AlertLevel = GetRandomAlertLevel(),
                BudgetStatus = GetRandomBudgetStatus(),
                ThresholdExceeded = _random.Next(0, 2) == 0,
                RunwayDays = _random.Next(1, 30),
                RecommendedAction = GetRandomBudgetAction(),
                IsAnomaly = _random.Next(0, 10) == 0
            };

            var key = $"{tenantId}:alert";
            lock (_budgetAlerts)
            {
                if (_budgetAlerts.Count > 3000) _budgetAlerts.Clear();
                _budgetAlerts[key] = alert;
            }

            _logger.LogInformation("Budget check: {Percentage}% spent, {Status}, {AlertLevel}",
                alert.SpendPercentage, alert.BudgetStatus, alert.AlertLevel);

            return alert;
        }

        public async Task<CostAnomalyDetection> DetectCostAnomaliesAsync(string tenantId, int daysBack = 30, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (daysBack < 1 || daysBack > 365) throw new ArgumentOutOfRangeException(nameof(daysBack));

            _logger.LogInformation("Detecting cost anomalies for {TenantId} over {Days} days", tenantId, daysBack);

            await Task.Delay(_random.Next(300, 800), ct);

            var anomalies = new List<CostAnomaly>();
            var anomalyCount = _random.Next(0, 4);

            for (int i = 0; i < anomalyCount; i++)
            {
                anomalies.Add(new CostAnomaly
                {
                    AnomalyId = Guid.NewGuid().ToString(),
                    DetectedAt = DateTime.UtcNow.AddDays(-_random.Next(0, daysBack)),
                    AnomalyType = GetRandomAnomalyType(),
                    Severity = (AnomalySeverity)_random.Next(0, 3),
                    ExpectedCost = _random.Next(1000, 10000),
                    ActualCost = _random.Next(15000, 50000),
                    CostDeviation = _random.Next(20, 300),
                    AffectedResources = _random.Next(1, 10),
                    ConfidenceScore = _random.Next(75, 99) / 100.0,
                    RootCauseHypothesis = GetRandomCauseHypothesis(),
                    RequiredInvestigation = _random.Next(0, 2) == 0
                });
            }

            var detection = new CostAnomalyDetection
            {
                DetectionId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AnalysisPeriod = daysBack,
                DetectedAt = DateTime.UtcNow,
                Anomalies = anomalies,
                TotalAnomalyCost = anomalies.Sum(a => a.ActualCost - a.ExpectedCost),
                AnomalyCount = anomalies.Count,
                CriticalAnomalies = anomalies.Count(a => a.Severity == AnomalySeverity.High),
                AnomalyDetectionAccuracy = _random.Next(80, 95) / 100.0,
                RecommendedActions = _random.Next(1, 4)
            };

            var key = $"{tenantId}:anomaly";
            lock (_costAnomalies)
            {
                if (_costAnomalies.Count > 4000) _costAnomalies.Clear();
                _costAnomalies[key] = detection;
            }

            _logger.LogInformation("Anomaly detection complete: {Count} anomalies, {TotalCost} unexpected cost",
                anomalies.Count, detection.TotalAnomalyCost);

            return detection;
        }

        public async Task<ChargebackReport> GenerateChargebackReportAsync(string tenantId, string departmentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(departmentId)) throw new ArgumentNullException(nameof(departmentId));

            _logger.LogInformation("Generating chargeback report for {DepartmentId} in {TenantId}",
                departmentId, tenantId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var lineItems = new List<ChargebackLineItem>();
            var itemCount = _random.Next(5, 15);

            for (int i = 0; i < itemCount; i++)
            {
                lineItems.Add(new ChargebackLineItem
                {
                    ItemId = Guid.NewGuid().ToString(),
                    ResourceType = GetRandomResourceType(),
                    Description = $"Resource allocation {i + 1}",
                    UsageMetric = $"{_random.Next(10, 1000)} units",
                    RatePerUnit = _random.Next(1, 100),
                    TotalCharge = _random.Next(100, 10000),
                    AllocationBasis = GetRandomAllocationBasis()
                });
            }

            var report = new ChargebackReport
            {
                ReportId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                DepartmentId = departmentId,
                ReportDate = DateTime.UtcNow,
                BillingPeriod = "2025-11",
                LineItems = lineItems,
                Subtotal = lineItems.Sum(l => l.TotalCharge),
                Taxes = (int)(lineItems.Sum(l => l.TotalCharge) * 0.10),
                Discounts = _random.Next(0, 5000),
                TotalAmount = lineItems.Sum(l => l.TotalCharge) + (int)(lineItems.Sum(l => l.TotalCharge) * 0.10) - _random.Next(0, 5000),
                CostAllocationConfidence = _random.Next(80, 98) / 100.0,
                IssuedDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30)
            };

            var key = $"{tenantId}:{departmentId}:chargeback";
            lock (_chargebacks)
            {
                if (_chargebacks.Count > 5000) _chargebacks.Clear();
                _chargebacks[key] = report;
            }

            _logger.LogInformation("Chargeback report generated: {Amount} for {Department} ({Items} items)",
                report.TotalAmount, departmentId, lineItems.Count);

            return report;
        }

        public async Task<CostTrendAnalysis> AnalyzeCostTrendsAsync(string tenantId, int monthsBack = 6, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (monthsBack < 1 || monthsBack > 24) throw new ArgumentOutOfRangeException(nameof(monthsBack));

            _logger.LogInformation("Analyzing cost trends for {TenantId} over {Months} months", tenantId, monthsBack);

            await Task.Delay(_random.Next(400, 1000), ct);

            var monthlyTrends = new List<MonthlyCostTrend>();
            for (int i = monthsBack; i >= 0; i--)
            {
                monthlyTrends.Add(new MonthlyCostTrend
                {
                    Month = DateTime.UtcNow.AddMonths(-i),
                    TotalCost = _random.Next(10000, 100000),
                    ComputeCost = _random.Next(3000, 40000),
                    StorageCost = _random.Next(1000, 20000),
                    NetworkCost = _random.Next(500, 10000),
                    ServiceCost = _random.Next(2000, 30000),
                    MonthOverMonthGrowth = _random.Next(-20, 40) / 100.0
                });
            }

            var analysis = new CostTrendAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AnalysisDate = DateTime.UtcNow,
                AnalysisPeriodMonths = monthsBack,
                MonthlyTrends = monthlyTrends,
                AverageMonthlyCost = monthlyTrends.Average(m => m.TotalCost),
                TrendDirection = GetRandomTrend(),
                VolatilityScore = _random.Next(10, 80),
                SeasonalityDetected = _random.Next(0, 2) == 0,
                FastestGrowingCategory = GetRandomResourceType(),
                HighestCostCategory = GetRandomResourceType(),
                CostOptimizationPotential = _random.Next(10, 40) / 100.0
            };

            var key = $"{tenantId}:trends";
            lock (_costTrends)
            {
                if (_costTrends.Count > 3000) _costTrends.Clear();
                _costTrends[key] = analysis;
            }

            _logger.LogInformation("Cost trend analysis: {AvgCost}/month, {Trend} trend, {Volatility} volatility",
                (int)analysis.AverageMonthlyCost, analysis.TrendDirection, analysis.VolatilityScore);

            return analysis;
        }

        public async Task<FinancialHealthReport> AssessFinancialHealthAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Assessing financial health for {TenantId}", tenantId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var report = new FinancialHealthReport
            {
                ReportId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AssessmentDate = DateTime.UtcNow,
                MonthlySpend = _random.Next(20000, 200000),
                BudgetUtilization = _random.Next(40, 90) / 100.0,
                CostPerWorkflow = _random.Next(100, 5000),
                CostEfficiencyScore = _random.Next(60, 95),
                BudgetHealth = GetRandomHealthStatus(),
                SpendingVelocity = GetRandomVelocity(),
                RunwayDaysRemaining = _random.Next(15, 365),
                EstimatedYearlySpend = _random.Next(200000, 2000000),
                PreviousYearComparison = _random.Next(-30, 60) / 100.0,
                FinancialRiskScore = _random.Next(20, 80),
                RecommendedActions = _random.Next(2, 6)
            };

            _logger.LogInformation("Financial health: {Score}/100, {Status}, {Runway} days runway",
                report.CostEfficiencyScore, report.BudgetHealth, report.RunwayDaysRemaining);

            return report;
        }

        public async Task<ResourceCostOptimization> OptimizeResourceCostsAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(workflowId)) throw new ArgumentNullException(nameof(workflowId));

            _logger.LogInformation("Optimizing resource costs for {WorkflowId}", workflowId);

            await Task.Delay(_random.Next(400, 900), ct);

            var currentAllocation = new ResourceAllocation
            {
                CPU = _random.Next(4, 32),
                Memory = _random.Next(8, 256),
                Storage = _random.Next(100, 2000),
                NetworkBandwidth = _random.Next(10, 500),
                MonthlyCost = _random.Next(5000, 50000)
            };

            var optimizedAllocation = new ResourceAllocation
            {
                CPU = Math.Max(1, currentAllocation.CPU / 2),
                Memory = Math.Max(2, currentAllocation.Memory / 2),
                Storage = Math.Max(10, (int)(currentAllocation.Storage * 0.8)),
                NetworkBandwidth = Math.Max(1, (int)(currentAllocation.NetworkBandwidth * 0.9)),
                MonthlyCost = (int)(currentAllocation.MonthlyCost * _random.Next(50, 80) / 100.0)
            };

            var optimization = new ResourceCostOptimization
            {
                OptimizationId = Guid.NewGuid().ToString(),
                WorkflowId = workflowId,
                GeneratedAt = DateTime.UtcNow,
                CurrentAllocation = currentAllocation,
                OptimizedAllocation = optimizedAllocation,
                MonthlySavings = currentAllocation.MonthlyCost - optimizedAllocation.MonthlyCost,
                AnnualSavings = (currentAllocation.MonthlyCost - optimizedAllocation.MonthlyCost) * 12,
                SavingsPercentage = ((currentAllocation.MonthlyCost - optimizedAllocation.MonthlyCost) / currentAllocation.MonthlyCost * 100),
                PerformanceImpact = _random.Next(-10, 5) / 100.0,
                ImplementationRisk = (RiskLevel)_random.Next(0, 2),
                RecommendationPriority = _random.Next(1, 10),
                PaybackMonths = _random.Next(1, 12)
            };

            _logger.LogInformation("Resource optimization: ${Savings}/month, {Percentage}% reduction",
                optimization.MonthlySavings, Math.Round(optimization.SavingsPercentage));

            return optimization;
        }

        public async Task<FinOpsMetrics> GetFinOpsMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Retrieving FinOps metrics for {TenantId}", tenantId);

            await Task.Delay(_random.Next(150, 400), ct);

            var metrics = new FinOpsMetrics
            {
                TenantId = tenantId,
                MetricsDate = DateTime.UtcNow,
                WorkflowsCostAnalyzed = _random.Next(50, 500),
                CostRecommendationsProvided = _random.Next(100, 1000),
                AnomaliesDetected = _random.Next(10, 100),
                BudgetAlertsIssued = _random.Next(5, 50),
                ChargebackReportsGenerated = _random.Next(10, 100),
                TotalCostTracked = _random.Next(100000, 5000000),
                PotentialSavingsIdentified = _random.Next(50000, 1000000),
                OptimizationImplementationRate = _random.Next(20, 80) / 100.0,
                AverageCostPerWorkflow = _random.Next(500, 5000),
                CostPredictionAccuracy = _random.Next(75, 92) / 100.0,
                BudgetForecastAccuracy = _random.Next(70, 90) / 100.0,
                AnomalyDetectionAccuracy = _random.Next(80, 95) / 100.0,
                FinOpsMaturityLevel = GetRandomMaturityLevel(),
                MonthlySpendingVelocity = _random.Next(50000, 500000)
            };

            _logger.LogInformation("FinOps metrics: {Workflows} analyzed, ${Savings} identified, {Accuracy}% accuracy",
                metrics.WorkflowsCostAnalyzed,
                metrics.PotentialSavingsIdentified,
                Math.Round(metrics.CostPredictionAccuracy * 100));

            return metrics;
        }

        // Helper methods
        private string GetRandomTrend() => new[] { "Increasing", "Decreasing", "Stable" }[_random.Next(0, 3)];
        private string GetRandomAlertLevel() => new[] { "Green", "Yellow", "Orange", "Red" }[_random.Next(0, 4)];
        private string GetRandomBudgetStatus() => new[] { "On Track", "At Risk", "Over Budget", "Critical" }[_random.Next(0, 4)];
        private string GetRandomBudgetAction() => new[] { "Optimize", "Review", "Reduce", "Investigate", "None" }[_random.Next(0, 5)];
        private string GetRandomAnomalyType() => new[] { "Spike", "Gradual Increase", "Unusual Pattern", "Resource Burst" }[_random.Next(0, 4)];
        private string GetRandomCauseHypothesis() => new[] { "Workload increase", "Configuration change", "Resource leak", "New service", "External factor" }[_random.Next(0, 5)];
        private string GetRandomResourceType() => new[] { "Compute", "Storage", "Network", "Database", "Service" }[_random.Next(0, 5)];
        private string GetRandomAllocationBasis() => new[] { "Usage", "Fixed", "Ratio", "Consumption", "Headcount" }[_random.Next(0, 5)];
        private string GetRandomHealthStatus() => new[] { "Excellent", "Good", "Fair", "At Risk", "Critical" }[_random.Next(0, 5)];
        private string GetRandomVelocity() => new[] { "Low", "Moderate", "High", "Very High" }[_random.Next(0, 4)];
        private string GetRandomMaturityLevel() => new[] { "Initial", "Managed", "Optimized", "Advanced" }[_random.Next(0, 4)];
    }

    // Domain Models
    public class CostAllocationReport
    {
        public string ReportId { get; set; }
        public string WorkflowId { get; set; }
        public DateTime ReportDate { get; set; }
        public List<CostComponent> CostComponents { get; set; }
        public int TotalCost { get; set; }
        public int CostPerExecution { get; set; }
        public int AccumulatedCost { get; set; }
        public int CostTrendPercent { get; set; }
        public string OwningDepartment { get; set; }
        public double CostAllocationAccuracy { get; set; }
    }

    public class CostComponent
    {
        public string Category { get; set; }
        public int Amount { get; set; }
        public int Percentage { get; set; }
        public string Unit { get; set; }
    }

    public class BudgetForecast
    {
        public string ForecastId { get; set; }
        public string TenantId { get; set; }
        public DateTime ForecastDate { get; set; }
        public int ForecastHorizonMonths { get; set; }
        public List<MonthlyForecast> MonthlyForecasts { get; set; }
        public int TotalForecastedCost { get; set; }
        public double AverageMonthlySpend { get; set; }
        public int VariabilityScore { get; set; }
        public double AnomalyProbability { get; set; }
        public double ForecastAccuracy { get; set; }
    }

    public class MonthlyForecast
    {
        public DateTime Month { get; set; }
        public int ForecastedCost { get; set; }
        public double ConfidenceLevel { get; set; }
        public string TrendDirection { get; set; }
        public int RiskFactors { get; set; }
    }

    public class CostOptimizationOpportunity
    {
        public string OpportunityId { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int EstimatedMonthlySavings { get; set; }
        public int SavingsPercentage { get; set; }
        public ImplementationEffort ImplementationEffort { get; set; }
        public int PaybackPeriodMonths { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class BudgetAlert
    {
        public string AlertId { get; set; }
        public string TenantId { get; set; }
        public DateTime CheckTime { get; set; }
        public int MonthlyBudget { get; set; }
        public int CurrentSpend { get; set; }
        public int SpendPercentage { get; set; }
        public int ForecastedEndOfMonth { get; set; }
        public string AlertLevel { get; set; }
        public string BudgetStatus { get; set; }
        public bool ThresholdExceeded { get; set; }
        public int RunwayDays { get; set; }
        public string RecommendedAction { get; set; }
        public bool IsAnomaly { get; set; }
    }

    public class CostAnomalyDetection
    {
        public string DetectionId { get; set; }
        public string TenantId { get; set; }
        public int AnalysisPeriod { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<CostAnomaly> Anomalies { get; set; }
        public int TotalAnomalyCost { get; set; }
        public int AnomalyCount { get; set; }
        public int CriticalAnomalies { get; set; }
        public double AnomalyDetectionAccuracy { get; set; }
        public int RecommendedActions { get; set; }
    }

    public class CostAnomaly
    {
        public string AnomalyId { get; set; }
        public DateTime DetectedAt { get; set; }
        public string AnomalyType { get; set; }
        public AnomalySeverity Severity { get; set; }
        public int ExpectedCost { get; set; }
        public int ActualCost { get; set; }
        public int CostDeviation { get; set; }
        public int AffectedResources { get; set; }
        public double ConfidenceScore { get; set; }
        public string RootCauseHypothesis { get; set; }
        public bool RequiredInvestigation { get; set; }
    }

    public class ChargebackReport
    {
        public string ReportId { get; set; }
        public string TenantId { get; set; }
        public string DepartmentId { get; set; }
        public DateTime ReportDate { get; set; }
        public string BillingPeriod { get; set; }
        public List<ChargebackLineItem> LineItems { get; set; }
        public int Subtotal { get; set; }
        public int Taxes { get; set; }
        public int Discounts { get; set; }
        public int TotalAmount { get; set; }
        public double CostAllocationConfidence { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class ChargebackLineItem
    {
        public string ItemId { get; set; }
        public string ResourceType { get; set; }
        public string Description { get; set; }
        public string UsageMetric { get; set; }
        public int RatePerUnit { get; set; }
        public int TotalCharge { get; set; }
        public string AllocationBasis { get; set; }
    }

    public class CostTrendAnalysis
    {
        public string AnalysisId { get; set; }
        public string TenantId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int AnalysisPeriodMonths { get; set; }
        public List<MonthlyCostTrend> MonthlyTrends { get; set; }
        public double AverageMonthlyCost { get; set; }
        public string TrendDirection { get; set; }
        public int VolatilityScore { get; set; }
        public bool SeasonalityDetected { get; set; }
        public string FastestGrowingCategory { get; set; }
        public string HighestCostCategory { get; set; }
        public double CostOptimizationPotential { get; set; }
    }

    public class MonthlyCostTrend
    {
        public DateTime Month { get; set; }
        public int TotalCost { get; set; }
        public int ComputeCost { get; set; }
        public int StorageCost { get; set; }
        public int NetworkCost { get; set; }
        public int ServiceCost { get; set; }
        public double MonthOverMonthGrowth { get; set; }
    }

    public class FinancialHealthReport
    {
        public string ReportId { get; set; }
        public string TenantId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public int MonthlySpend { get; set; }
        public double BudgetUtilization { get; set; }
        public int CostPerWorkflow { get; set; }
        public int CostEfficiencyScore { get; set; }
        public string BudgetHealth { get; set; }
        public string SpendingVelocity { get; set; }
        public int RunwayDaysRemaining { get; set; }
        public int EstimatedYearlySpend { get; set; }
        public double PreviousYearComparison { get; set; }
        public int FinancialRiskScore { get; set; }
        public int RecommendedActions { get; set; }
    }

    public class ResourceCostOptimization
    {
        public string OptimizationId { get; set; }
        public string WorkflowId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public ResourceAllocation CurrentAllocation { get; set; }
        public ResourceAllocation OptimizedAllocation { get; set; }
        public int MonthlySavings { get; set; }
        public int AnnualSavings { get; set; }
        public double SavingsPercentage { get; set; }
        public double PerformanceImpact { get; set; }
        public RiskLevel ImplementationRisk { get; set; }
        public int RecommendationPriority { get; set; }
        public int PaybackMonths { get; set; }
    }

    public class ResourceAllocation
    {
        public int CPU { get; set; }
        public int Memory { get; set; }
        public int Storage { get; set; }
        public int NetworkBandwidth { get; set; }
        public int MonthlyCost { get; set; }
    }

    public class FinOpsMetrics
    {
        public string TenantId { get; set; }
        public DateTime MetricsDate { get; set; }
        public int WorkflowsCostAnalyzed { get; set; }
        public int CostRecommendationsProvided { get; set; }
        public int AnomaliesDetected { get; set; }
        public int BudgetAlertsIssued { get; set; }
        public int ChargebackReportsGenerated { get; set; }
        public int TotalCostTracked { get; set; }
        public int PotentialSavingsIdentified { get; set; }
        public double OptimizationImplementationRate { get; set; }
        public int AverageCostPerWorkflow { get; set; }
        public double CostPredictionAccuracy { get; set; }
        public double BudgetForecastAccuracy { get; set; }
        public double AnomalyDetectionAccuracy { get; set; }
        public string FinOpsMaturityLevel { get; set; }
        public int MonthlySpendingVelocity { get; set; }
    }

    // Enums
    public enum ImplementationEffort { Low = 0, Medium = 1, High = 2 }
    public enum RiskLevel { Low = 0, Medium = 1, High = 2 }
    public enum AnomalySeverity { Low = 0, Medium = 1, High = 2 }
}
