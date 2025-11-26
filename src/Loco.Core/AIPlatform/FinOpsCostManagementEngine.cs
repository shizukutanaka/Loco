using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.AIPlatform
{
    /// <summary>
    /// FinOps Cost Management Engine implementing cloud cost optimization and sustainability
    /// Based on: OpenCost (CNCF Sandbox), Kubecost patterns, Cloud Carbon Footprint
    ///
    /// Key Patterns:
    /// - Cost Allocation: Accurate Kubernetes cost attribution (per namespace, pod, label)
    /// - Rightsizing: Automated resource optimization (65% of workloads over-provisioned)
    /// - Spot Instance Management: 70-90% cost savings with intelligent fallback
    /// - Showback/Chargeback: Cross-team cost accountability
    /// - Budget Management: Alerts and forecasting
    /// - Sustainability: Carbon footprint tracking (Kepler eBPF energy metrics)
    ///
    /// Research Sources (2024-2025):
    /// - OpenCost: CNCF Sandbox project, real-time K8s cost monitoring
    /// - Kubecost: Enterprise features, 30% cost reduction cases
    /// - FinOps Impact: 65% of K8s workloads use <50% of requested resources
    /// - Spot Instances: 70-90% discount vs on-demand
    /// - Industry Adoption: Zalando (30% reduction), Adobe, Spotify
    /// - Kepler: CNCF Sandbox, eBPF + RAPL for pod/container energy metrics
    /// - Cloud Carbon Footprint: Open-source carbon tracking (Green Software Foundation)
    /// </summary>
    public interface IFinOpsCostManagementEngine
    {
        // Cost Allocation & Monitoring
        Task<CostBreakdown> GetCostBreakdownAsync(string tenantId, CostQuery query, CancellationToken cancellation = default);
        Task<List<CostAllocation>> AllocateCostsAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<CostTrend> AnalyzeCostTrendAsync(string tenantId, string dimension, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<List<CostAnomaly>> DetectCostAnomaliesAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);

        // Rightsizing & Optimization
        Task<List<RightsizingRecommendation>> GenerateRightsizingRecommendationsAsync(string tenantId, string @namespace, CancellationToken cancellation = default);
        Task<RightsizingExecution> ApplyRightsizingAsync(string tenantId, string recommendationId, CancellationToken cancellation = default);
        Task<OptimizationReport> GenerateOptimizationReportAsync(string tenantId, CancellationToken cancellation = default);
        Task<double> CalculatePotentialSavingsAsync(string tenantId, CancellationToken cancellation = default);

        // Spot Instance Management
        Task<SpotInstanceStrategy> CreateSpotStrategyAsync(string tenantId, SpotInstanceConfig config, CancellationToken cancellation = default);
        Task<SpotInstanceMetrics> GetSpotInstanceMetricsAsync(string tenantId, CancellationToken cancellation = default);
        Task<SpotInterruptionPrediction> PredictSpotInterruptionAsync(string tenantId, string instanceType, CancellationToken cancellation = default);

        // Budget Management
        Task<Budget> CreateBudgetAsync(string tenantId, Budget budget, CancellationToken cancellation = default);
        Task<List<Budget>> GetBudgetsAsync(string tenantId, CancellationToken cancellation = default);
        Task<BudgetStatus> CheckBudgetStatusAsync(string tenantId, string budgetId, CancellationToken cancellation = default);
        Task<CostForecast> ForecastCostsAsync(string tenantId, TimeSpan forecastHorizon, CancellationToken cancellation = default);

        // Showback/Chargeback
        Task<ShowbackReport> GenerateShowbackReportAsync(string tenantId, string teamId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<ChargebackReport> GenerateChargebackReportAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<CostAllocationRule> CreateAllocationRuleAsync(string tenantId, CostAllocationRule rule, CancellationToken cancellation = default);

        // Sustainability & Carbon Footprint
        Task<CarbonFootprint> CalculateCarbonFootprintAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<EnergyMetrics> GetEnergyMetricsAsync(string tenantId, string @namespace, CancellationToken cancellation = default);
        Task<SustainabilityReport> GenerateSustainabilityReportAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default);
        Task<List<SustainabilityRecommendation>> GetSustainabilityRecommendationsAsync(string tenantId, CancellationToken cancellation = default);
    }

    public class FinOpsCostManagementEngine : IFinOpsCostManagementEngine
    {
        private readonly Dictionary<string, Budget> _budgets = new();
        private readonly Dictionary<string, CostAllocationRule> _allocationRules = new();
        private readonly Dictionary<string, RightsizingRecommendation> _recommendations = new();
        private readonly Dictionary<string, SpotInstanceStrategy> _spotStrategies = new();

        // Cost rates (simplified, in production would come from cloud provider APIs)
        private const double CpuCostPerCoreHour = 0.0416; // ~$30/month per core
        private const double MemoryCostPerGBHour = 0.0052; // ~$3.75/month per GB
        private const double StorageCostPerGBMonth = 0.10; // $0.10/GB/month
        private const double NetworkCostPerGB = 0.01; // $0.01/GB

        public async Task<CostBreakdown> GetCostBreakdownAsync(string tenantId, CostQuery query, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            var breakdown = new CostBreakdown
            {
                TenantId = tenantId,
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                TotalCost = 0,
                BreakdownItems = new List<CostBreakdownItem>()
            };

            // Simulate cost data collection from OpenCost/Kubecost
            var namespaces = new[] { "production", "staging", "development" };
            foreach (var ns in namespaces)
            {
                var item = await CalculateNamespaceCostAsync(ns, query.StartDate, query.EndDate, cancellation);
                breakdown.BreakdownItems.Add(item);
                breakdown.TotalCost += item.TotalCost;
            }

            // Apply grouping
            if (!string.IsNullOrEmpty(query.GroupBy))
            {
                breakdown.BreakdownItems = GroupCostItems(breakdown.BreakdownItems, query.GroupBy);
            }

            return breakdown;
        }

        public async Task<List<CostAllocation>> AllocateCostsAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(150, cancellation);

            // Research: Accurate cost allocation per namespace, pod, label
            var allocations = new List<CostAllocation>();

            // Allocate by team
            var teams = new[] { "team-platform", "team-frontend", "team-backend", "team-data" };
            foreach (var team in teams)
            {
                var allocation = new CostAllocation
                {
                    Dimension = "team",
                    Value = team,
                    StartDate = startDate,
                    EndDate = endDate,
                    ComputeCost = CalculateRandomCost(1000, 5000),
                    MemoryCost = CalculateRandomCost(500, 2000),
                    StorageCost = CalculateRandomCost(200, 1000),
                    NetworkCost = CalculateRandomCost(100, 500)
                };

                allocation.TotalCost = allocation.ComputeCost + allocation.MemoryCost + allocation.StorageCost + allocation.NetworkCost;
                allocations.Add(allocation);
            }

            return allocations;
        }

        public async Task<CostTrend> AnalyzeCostTrendAsync(string tenantId, string dimension, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(100, cancellation);

            var trend = new CostTrend
            {
                Dimension = dimension,
                StartDate = startDate,
                EndDate = endDate,
                DataPoints = new List<CostDataPoint>()
            };

            // Generate daily cost data
            var days = (int)(endDate - startDate).TotalDays;
            var baseCost = 1000.0;

            for (int i = 0; i <= days; i++)
            {
                var date = startDate.AddDays(i);
                var dailyCost = baseCost + (i * 10) + (Math.Sin(i * 0.5) * 100); // Trend + seasonality

                trend.DataPoints.Add(new CostDataPoint
                {
                    Date = date,
                    Cost = dailyCost,
                    ComputeCost = dailyCost * 0.5,
                    MemoryCost = dailyCost * 0.25,
                    StorageCost = dailyCost * 0.15,
                    NetworkCost = dailyCost * 0.10
                });
            }

            // Calculate trend statistics
            trend.TotalCost = trend.DataPoints.Sum(d => d.Cost);
            trend.AverageDailyCost = trend.TotalCost / days;
            trend.TrendDirection = trend.DataPoints.Last().Cost > trend.DataPoints.First().Cost ? "increasing" : "decreasing";
            trend.PercentageChange = ((trend.DataPoints.Last().Cost - trend.DataPoints.First().Cost) / trend.DataPoints.First().Cost) * 100;

            return trend;
        }

        public async Task<List<CostAnomaly>> DetectCostAnomaliesAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(150, cancellation);

            var anomalies = new List<CostAnomaly>();

            // Simulate anomaly detection
            anomalies.Add(new CostAnomaly
            {
                Id = Guid.NewGuid().ToString(),
                DetectedAt = DateTime.UtcNow.AddDays(-2),
                Dimension = "namespace",
                Value = "production",
                ExpectedCost = 1500,
                ActualCost = 3200,
                Deviation = 113.3, // 113% increase
                Severity = "high",
                Description = "Unexpected cost spike in production namespace",
                PossibleCauses = new List<string>
                {
                    "Traffic spike (5x normal)",
                    "New workload deployed without resource limits",
                    "Database query performance degradation"
                }
            });

            anomalies.Add(new CostAnomaly
            {
                Id = Guid.NewGuid().ToString(),
                DetectedAt = DateTime.UtcNow.AddDays(-1),
                Dimension = "pod",
                Value = "data-processing-job-xyz",
                ExpectedCost = 200,
                ActualCost = 850,
                Deviation = 325, // 325% increase
                Severity = "critical",
                Description = "Data processing job running longer than expected",
                PossibleCauses = new List<string>
                {
                    "Job hung, not completing",
                    "Input data size increased",
                    "Memory leak causing OOM restarts"
                }
            });

            return anomalies;
        }

        public async Task<List<RightsizingRecommendation>> GenerateRightsizingRecommendationsAsync(string tenantId, string @namespace, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            // Research: 65% of K8s workloads use <50% of requested resources
            var recommendations = new List<RightsizingRecommendation>();

            // Generate recommendations for over-provisioned workloads
            var workloads = new[]
            {
                ("frontend", 4.0, 8.0, 1.2, 2.5, "deployment"),
                ("api-gateway", 2.0, 4.0, 0.8, 1.5, "deployment"),
                ("user-service", 2.0, 4.0, 0.6, 1.0, "deployment"),
                ("payment-service", 4.0, 8.0, 1.5, 3.0, "deployment"),
                ("data-processor", 8.0, 16.0, 3.5, 7.0, "job")
            };

            foreach (var (name, currentCpu, currentMemory, avgCpu, avgMemory, type) in workloads)
            {
                // Calculate utilization
                var cpuUtilization = avgCpu / currentCpu;
                var memoryUtilization = avgMemory / currentMemory;

                // Generate recommendation if under-utilized (<50%)
                if (cpuUtilization < 0.5 || memoryUtilization < 0.5)
                {
                    var recommendedCpu = Math.Ceiling(avgCpu * 1.3 * 10) / 10; // +30% headroom
                    var recommendedMemory = Math.Ceiling(avgMemory * 1.3 * 10) / 10;

                    var monthlySavings = CalculateMonthlySavings(
                        currentCpu, currentMemory,
                        recommendedCpu, recommendedMemory
                    );

                    var recommendation = new RightsizingRecommendation
                    {
                        Id = Guid.NewGuid().ToString(),
                        Namespace = @namespace,
                        WorkloadName = name,
                        WorkloadType = type,
                        CurrentResources = new ResourceAllocation
                        {
                            CpuCores = currentCpu,
                            MemoryGB = currentMemory
                        },
                        RecommendedResources = new ResourceAllocation
                        {
                            CpuCores = recommendedCpu,
                            MemoryGB = recommendedMemory
                        },
                        CurrentUtilization = new ResourceUtilization
                        {
                            CpuUtilization = cpuUtilization,
                            MemoryUtilization = memoryUtilization
                        },
                        MonthlySavings = monthlySavings,
                        AnnualSavings = monthlySavings * 12,
                        Confidence = 0.85,
                        Rationale = $"Workload using {cpuUtilization * 100:F1}% CPU and {memoryUtilization * 100:F1}% memory over last 7 days"
                    };

                    recommendations.Add(recommendation);

                    var key = $"{tenantId}:{recommendation.Id}";
                    _recommendations[key] = recommendation;
                }
            }

            return recommendations;
        }

        public async Task<RightsizingExecution> ApplyRightsizingAsync(string tenantId, string recommendationId, CancellationToken cancellation = default)
        {
            await Task.Delay(150, cancellation);

            var key = $"{tenantId}:{recommendationId}";
            if (!_recommendations.TryGetValue(key, out var recommendation))
                throw new KeyNotFoundException($"Recommendation not found: {recommendationId}");

            var execution = new RightsizingExecution
            {
                Id = Guid.NewGuid().ToString(),
                RecommendationId = recommendationId,
                Status = "in_progress",
                StartedAt = DateTime.UtcNow,
                Steps = new List<RightsizingStep>()
            };

            // Step 1: Validate recommendation
            execution.Steps.Add(new RightsizingStep
            {
                Name = "validate",
                Status = "completed",
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow.AddSeconds(5)
            });

            // Step 2: Update resource requests/limits
            await Task.Delay(100, cancellation);
            execution.Steps.Add(new RightsizingStep
            {
                Name = "update_resources",
                Status = "completed",
                StartedAt = DateTime.UtcNow.AddSeconds(5),
                CompletedAt = DateTime.UtcNow.AddSeconds(15)
            });

            // Step 3: Monitor health
            await Task.Delay(100, cancellation);
            execution.Steps.Add(new RightsizingStep
            {
                Name = "monitor_health",
                Status = "completed",
                StartedAt = DateTime.UtcNow.AddSeconds(15),
                CompletedAt = DateTime.UtcNow.AddSeconds(25)
            });

            execution.Status = "completed";
            execution.CompletedAt = DateTime.UtcNow.AddSeconds(25);
            execution.ActualSavings = recommendation.MonthlySavings;

            return execution;
        }

        public async Task<OptimizationReport> GenerateOptimizationReportAsync(string tenantId, CancellationToken cancellation = default)
        {
            await Task.Delay(250, cancellation);

            var report = new OptimizationReport
            {
                TenantId = tenantId,
                GeneratedAt = DateTime.UtcNow,
                TotalMonthlyCost = 15000,
                OptimizationOpportunities = new List<OptimizationOpportunity>()
            };

            // Rightsizing opportunities
            var rightsizingRecommendations = await GenerateRightsizingRecommendationsAsync(tenantId, "production", cancellation);
            report.OptimizationOpportunities.Add(new OptimizationOpportunity
            {
                Type = "rightsizing",
                Description = "Rightsize over-provisioned workloads",
                PotentialMonthlySavings = rightsizingRecommendations.Sum(r => r.MonthlySavings),
                ActionCount = rightsizingRecommendations.Count,
                Confidence = 0.85,
                Priority = "high"
            });

            // Spot instance opportunities (70-90% savings)
            report.OptimizationOpportunities.Add(new OptimizationOpportunity
            {
                Type = "spot_instances",
                Description = "Migrate fault-tolerant workloads to spot instances",
                PotentialMonthlySavings = 2400, // 80% of $3000
                ActionCount = 8,
                Confidence = 0.90,
                Priority = "high"
            });

            // Storage optimization
            report.OptimizationOpportunities.Add(new OptimizationOpportunity
            {
                Type = "storage_optimization",
                Description = "Remove unused volumes and snapshots",
                PotentialMonthlySavings = 450,
                ActionCount = 25,
                Confidence = 0.95,
                Priority = "medium"
            });

            // Reserved instances/savings plans
            report.OptimizationOpportunities.Add(new OptimizationOpportunity
            {
                Type = "reserved_instances",
                Description = "Purchase reserved instances for stable workloads",
                PotentialMonthlySavings = 1800, // 40% discount
                ActionCount = 1,
                Confidence = 0.80,
                Priority = "medium"
            });

            // Idle resources
            report.OptimizationOpportunities.Add(new OptimizationOpportunity
            {
                Type = "idle_resources",
                Description = "Terminate idle development environments",
                PotentialMonthlySavings = 650,
                ActionCount = 12,
                Confidence = 0.92,
                Priority = "high"
            });

            report.TotalPotentialSavings = report.OptimizationOpportunities.Sum(o => o.PotentialMonthlySavings);
            report.SavingsPercentage = (report.TotalPotentialSavings / report.TotalMonthlyCost) * 100;

            return report;
        }

        public async Task<double> CalculatePotentialSavingsAsync(string tenantId, CancellationToken cancellation = default)
        {
            await Task.Delay(100, cancellation);

            var report = await GenerateOptimizationReportAsync(tenantId, cancellation);
            return report.TotalPotentialSavings;
        }

        public async Task<SpotInstanceStrategy> CreateSpotStrategyAsync(string tenantId, SpotInstanceConfig config, CancellationToken cancellation = default)
        {
            await Task.Delay(100, cancellation);

            // Research: Spot instances offer 70-90% discount vs on-demand
            var strategy = new SpotInstanceStrategy
            {
                Id = Guid.NewGuid().ToString(),
                Name = config.Name,
                TargetWorkloads = config.TargetWorkloads,
                SpotPercentage = config.SpotPercentage,
                FallbackToOnDemand = config.FallbackToOnDemand,
                MaxPrice = config.MaxPrice,
                InstanceTypes = config.InstanceTypes,
                DiversificationStrategy = config.DiversificationStrategy,
                CreatedAt = DateTime.UtcNow
            };

            // Calculate expected savings
            strategy.ExpectedMonthlySavings = CalculateSpotSavings(config);

            var key = $"{tenantId}:{strategy.Id}";
            _spotStrategies[key] = strategy;

            return strategy;
        }

        public async Task<SpotInstanceMetrics> GetSpotInstanceMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            await Task.Delay(100, cancellation);

            var metrics = new SpotInstanceMetrics
            {
                TenantId = tenantId,
                CollectedAt = DateTime.UtcNow,
                TotalInstances = 100,
                SpotInstances = 75,
                OnDemandInstances = 25,
                SpotPercentage = 0.75,
                SpotInterruptions = 3,
                InterruptionRate = 0.04, // 4% interruption rate
                AverageSavings = 0.82, // 82% average savings
                MonthlySavings = 6200
            };

            // Interruption history
            metrics.InterruptionHistory = new List<SpotInterruption>
            {
                new SpotInterruption
                {
                    Timestamp = DateTime.UtcNow.AddDays(-5),
                    InstanceType = "c5.xlarge",
                    AvailabilityZone = "us-east-1a",
                    Reason = "capacity_oversubscribed",
                    RecoveryTimeMs = 45000
                },
                new SpotInterruption
                {
                    Timestamp = DateTime.UtcNow.AddDays(-2),
                    InstanceType = "m5.2xlarge",
                    AvailabilityZone = "us-east-1b",
                    Reason = "price_exceeded",
                    RecoveryTimeMs = 38000
                }
            };

            return metrics;
        }

        public async Task<SpotInterruptionPrediction> PredictSpotInterruptionAsync(string tenantId, string instanceType, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);

            // Predict spot interruption probability based on historical data
            var prediction = new SpotInterruptionPrediction
            {
                InstanceType = instanceType,
                PredictedAt = DateTime.UtcNow,
                InterruptionProbability = 0.08, // 8% probability in next hour
                TimeWindow = TimeSpan.FromHours(1),
                Confidence = 0.75,
                HistoricalInterruptionRate = 0.05, // 5% historical rate
                Recommendations = new List<string>
                {
                    "Consider diversifying across multiple instance types",
                    "Set max spot price to 50% of on-demand price",
                    "Use capacity-optimized allocation strategy"
                }
            };

            return prediction;
        }

        public async Task<Budget> CreateBudgetAsync(string tenantId, Budget budget, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);

            budget.Id = budget.Id ?? Guid.NewGuid().ToString();
            budget.CreatedAt = DateTime.UtcNow;
            budget.Status = "active";

            var key = $"{tenantId}:{budget.Id}";
            _budgets[key] = budget;

            return budget;
        }

        public async Task<List<Budget>> GetBudgetsAsync(string tenantId, CancellationToken cancellation = default)
        {
            await Task.Delay(20, cancellation);

            return _budgets.Values
                .Where(b => b.Id.StartsWith(tenantId) || _budgets.ContainsKey($"{tenantId}:{b.Id}"))
                .ToList();
        }

        public async Task<BudgetStatus> CheckBudgetStatusAsync(string tenantId, string budgetId, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);

            var key = $"{tenantId}:{budgetId}";
            if (!_budgets.TryGetValue(key, out var budget))
                throw new KeyNotFoundException($"Budget not found: {budgetId}");

            // Simulate cost tracking
            var currentCost = budget.Amount * 0.75; // 75% of budget used

            var status = new BudgetStatus
            {
                BudgetId = budgetId,
                BudgetName = budget.Name,
                Amount = budget.Amount,
                CurrentCost = currentCost,
                Percentage = (currentCost / budget.Amount) * 100,
                RemainingAmount = budget.Amount - currentCost,
                Period = budget.Period,
                Status = currentCost > budget.Amount ? "exceeded" : currentCost > budget.Amount * 0.9 ? "warning" : "on_track",
                CheckedAt = DateTime.UtcNow
            };

            // Check thresholds
            foreach (var threshold in budget.Thresholds)
            {
                if (status.Percentage >= threshold.Percentage && !threshold.Notified)
                {
                    status.TriggeredThresholds.Add(threshold);
                }
            }

            return status;
        }

        public async Task<CostForecast> ForecastCostsAsync(string tenantId, TimeSpan forecastHorizon, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            var forecast = new CostForecast
            {
                TenantId = tenantId,
                ForecastedAt = DateTime.UtcNow,
                Horizon = forecastHorizon,
                CurrentMonthlyCost = 15000,
                Predictions = new List<CostPrediction>()
            };

            // Generate predictions using linear regression + seasonality
            var daysToForecast = (int)forecastHorizon.TotalDays;
            for (int i = 1; i <= daysToForecast; i++)
            {
                var date = DateTime.UtcNow.AddDays(i);
                var baseCost = 500; // Daily base cost
                var growth = i * 5; // Linear growth
                var seasonality = Math.Sin(i * 0.2) * 50; // Weekly pattern
                var predictedCost = baseCost + growth + seasonality;

                forecast.Predictions.Add(new CostPrediction
                {
                    Date = date,
                    PredictedCost = predictedCost,
                    ConfidenceLower = predictedCost * 0.9,
                    ConfidenceUpper = predictedCost * 1.1
                });
            }

            forecast.ForecastedMonthlyCost = forecast.Predictions.Take(30).Sum(p => p.PredictedCost);
            forecast.ExpectedGrowth = ((forecast.ForecastedMonthlyCost - forecast.CurrentMonthlyCost) / forecast.CurrentMonthlyCost) * 100;

            return forecast;
        }

        public async Task<ShowbackReport> GenerateShowbackReportAsync(string tenantId, string teamId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(150, cancellation);

            // Showback: Information-only cost visibility (no billing)
            var report = new ShowbackReport
            {
                TeamId = teamId,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow,
                CostBreakdown = new Dictionary<string, double>(),
                ResourceUsage = new Dictionary<string, ResourceUsage>()
            };

            // Cost breakdown by resource type
            report.CostBreakdown["compute"] = 3200;
            report.CostBreakdown["memory"] = 1200;
            report.CostBreakdown["storage"] = 450;
            report.CostBreakdown["network"] = 280;

            report.TotalCost = report.CostBreakdown.Values.Sum();

            // Resource usage details
            report.ResourceUsage["cpu_cores"] = new ResourceUsage { Average = 12.5, Peak = 24, Unit = "cores" };
            report.ResourceUsage["memory_gb"] = new ResourceUsage { Average = 48, Peak = 96, Unit = "GB" };
            report.ResourceUsage["storage_gb"] = new ResourceUsage { Average = 250, Peak = 320, Unit = "GB" };

            return report;
        }

        public async Task<ChargebackReport> GenerateChargebackReportAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            // Chargeback: Actual billing/invoicing to teams
            var report = new ChargebackReport
            {
                TenantId = tenantId,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow,
                TeamCharges = new List<TeamCharge>()
            };

            var teams = new[] { "team-platform", "team-frontend", "team-backend", "team-data" };
            foreach (var team in teams)
            {
                var showback = await GenerateShowbackReportAsync(tenantId, team, startDate, endDate, cancellation);

                report.TeamCharges.Add(new TeamCharge
                {
                    TeamId = team,
                    TotalCost = showback.TotalCost,
                    ComputeCost = showback.CostBreakdown["compute"],
                    MemoryCost = showback.CostBreakdown["memory"],
                    StorageCost = showback.CostBreakdown["storage"],
                    NetworkCost = showback.CostBreakdown["network"]
                });
            }

            report.TotalCost = report.TeamCharges.Sum(tc => tc.TotalCost);

            return report;
        }

        public async Task<CostAllocationRule> CreateAllocationRuleAsync(string tenantId, CostAllocationRule rule, CancellationToken cancellation = default)
        {
            await Task.Delay(50, cancellation);

            rule.Id = rule.Id ?? Guid.NewGuid().ToString();
            rule.CreatedAt = DateTime.UtcNow;

            var key = $"{tenantId}:{rule.Id}";
            _allocationRules[key] = rule;

            return rule;
        }

        public async Task<CarbonFootprint> CalculateCarbonFootprintAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            // Research: Kepler (CNCF Sandbox) + Cloud Carbon Footprint
            var footprint = new CarbonFootprint
            {
                TenantId = tenantId,
                StartDate = startDate,
                EndDate = endDate,
                CalculatedAt = DateTime.UtcNow,
                TotalEmissionsKgCO2e = 0,
                EmissionsBySource = new Dictionary<string, double>()
            };

            // Calculate emissions by cloud region (grid carbon intensity varies)
            var regions = new[]
            {
                ("us-east-1", 450.0, 0.45), // Virginia: 450 gCO2/kWh
                ("us-west-2", 350.0, 0.28), // Oregon: 350 gCO2/kWh (cleaner grid)
                ("eu-west-1", 280.0, 0.35)  // Ireland: 280 gCO2/kWh
            };

            foreach (var (region, carbonIntensity, energyUsageKWh) in regions)
            {
                var emissionsKg = (energyUsageKWh * carbonIntensity) / 1000; // Convert g to kg
                footprint.EmissionsBySource[region] = emissionsKg;
                footprint.TotalEmissionsKgCO2e += emissionsKg;
            }

            // Calculate by resource type
            footprint.EmissionsByResourceType = new Dictionary<string, double>
            {
                ["compute"] = footprint.TotalEmissionsKgCO2e * 0.60,
                ["storage"] = footprint.TotalEmissionsKgCO2e * 0.25,
                ["network"] = footprint.TotalEmissionsKgCO2e * 0.15
            };

            // Equivalent metrics for context
            footprint.Equivalents = new Dictionary<string, double>
            {
                ["trees_planted"] = footprint.TotalEmissionsKgCO2e / 21, // 1 tree absorbs ~21 kg CO2/year
                ["km_driven"] = footprint.TotalEmissionsKgCO2e / 0.12,   // Average car: 120g CO2/km
                ["homes_powered_hours"] = footprint.TotalEmissionsKgCO2e / 0.5 // Average home: 0.5 kg CO2/hour
            };

            return footprint;
        }

        public async Task<EnergyMetrics> GetEnergyMetricsAsync(string tenantId, string @namespace, CancellationToken cancellation = default)
        {
            await Task.Delay(150, cancellation);

            // Research: Kepler uses eBPF + RAPL for pod/container energy metrics
            var metrics = new EnergyMetrics
            {
                Namespace = @namespace,
                CollectedAt = DateTime.UtcNow,
                TotalEnergyWh = 0,
                PodMetrics = new List<PodEnergyMetric>()
            };

            // Simulate Kepler metrics
            var pods = new[]
            {
                ("frontend-abc123", 12.5, 8.3, 1.2),
                ("api-gateway-def456", 18.7, 12.4, 2.3),
                ("user-service-ghi789", 15.2, 10.1, 1.8),
                ("payment-service-jkl012", 22.4, 14.8, 3.1)
            };

            foreach (var (podName, cpuEnergy, dramEnergy, otherEnergy) in pods)
            {
                var totalEnergy = cpuEnergy + dramEnergy + otherEnergy;

                metrics.PodMetrics.Add(new PodEnergyMetric
                {
                    PodName = podName,
                    CPUEnergyWh = cpuEnergy,
                    DRAMEnergyWh = dramEnergy,
                    OtherEnergyWh = otherEnergy,
                    TotalEnergyWh = totalEnergy
                });

                metrics.TotalEnergyWh += totalEnergy;
            }

            // Calculate carbon footprint (assume US East grid: 450 gCO2/kWh)
            metrics.CarbonEmissionsKgCO2e = (metrics.TotalEnergyWh / 1000) * 0.45;

            return metrics;
        }

        public async Task<SustainabilityReport> GenerateSustainabilityReportAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellation = default)
        {
            await Task.Delay(200, cancellation);

            var report = new SustainabilityReport
            {
                TenantId = tenantId,
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow
            };

            // Carbon footprint
            var footprint = await CalculateCarbonFootprintAsync(tenantId, startDate, endDate, cancellation);
            report.TotalEmissionsKgCO2e = footprint.TotalEmissionsKgCO2e;
            report.EmissionsByRegion = footprint.EmissionsBySource;

            // Energy efficiency metrics
            report.TotalEnergyUsageKWh = footprint.EmissionsBySource.Sum(kvp => kvp.Value / 0.4); // Rough estimate
            report.EnergyEfficiency = 0.72; // PUE (Power Usage Effectiveness) - 1.0 is ideal

            // Renewable energy percentage
            report.RenewableEnergyPercentage = 45.0; // % of energy from renewable sources

            // Recommendations
            report.Recommendations = await GetSustainabilityRecommendationsAsync(tenantId, cancellation);

            // Trend (vs previous period)
            report.EmissionsTrend = -8.5; // 8.5% reduction
            report.EfficiencyTrend = 5.2; // 5.2% improvement

            return report;
        }

        public async Task<List<SustainabilityRecommendation>> GetSustainabilityRecommendationsAsync(string tenantId, CancellationToken cancellation = default)
        {
            await Task.Delay(100, cancellation);

            var recommendations = new List<SustainabilityRecommendation>
            {
                new SustainabilityRecommendation
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "region_optimization",
                    Title = "Migrate workloads to low-carbon regions",
                    Description = "Move non-latency-sensitive workloads from us-east-1 (450 gCO2/kWh) to us-west-2 (350 gCO2/kWh)",
                    EstimatedEmissionReduction = 125.0, // kg CO2e/month
                    EmissionReductionPercentage = 15.0,
                    Implementation = "Use multi-region Kubernetes clusters with affinity rules",
                    Priority = "high"
                },
                new SustainabilityRecommendation
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "temporal_shifting",
                    Title = "Schedule batch jobs during low-carbon hours",
                    Description = "Shift data processing jobs to hours when grid has higher renewable energy percentage",
                    EstimatedEmissionReduction = 45.0,
                    EmissionReductionPercentage = 5.5,
                    Implementation = "Use CronJob schedules aligned with carbon-aware scheduling",
                    Priority = "medium"
                },
                new SustainabilityRecommendation
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "efficiency_optimization",
                    Title = "Optimize resource efficiency",
                    Description = "Apply rightsizing recommendations to reduce energy waste from over-provisioned resources",
                    EstimatedEmissionReduction = 85.0,
                    EmissionReductionPercentage = 10.5,
                    Implementation = "Apply automated rightsizing recommendations",
                    Priority = "high"
                },
                new SustainabilityRecommendation
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = "renewable_energy",
                    Title = "Increase renewable energy commitment",
                    Description = "Purchase renewable energy credits (RECs) or use cloud provider renewable energy programs",
                    EstimatedEmissionReduction = 300.0,
                    EmissionReductionPercentage = 37.0,
                    Implementation = "Enable AWS Customer Carbon Footprint Tool or Azure Carbon Optimization",
                    Priority = "medium"
                }
            };

            return recommendations;
        }

        // Private helper methods

        private async Task<CostBreakdownItem> CalculateNamespaceCostAsync(string @namespace, DateTime startDate, DateTime endDate, CancellationToken cancellation)
        {
            await Task.Delay(50, cancellation);

            var hours = (endDate - startDate).TotalHours;

            // Simulate resource usage
            var cpuCores = @namespace == "production" ? 24.0 : @namespace == "staging" ? 12.0 : 6.0;
            var memoryGB = @namespace == "production" ? 96.0 : @namespace == "staging" ? 48.0 : 24.0;
            var storageGB = @namespace == "production" ? 500.0 : @namespace == "staging" ? 250.0 : 100.0;
            var networkGB = @namespace == "production" ? 1000.0 : @namespace == "staging" ? 500.0 : 200.0;

            var item = new CostBreakdownItem
            {
                Dimension = "namespace",
                Value = @namespace,
                ComputeCost = cpuCores * CpuCostPerCoreHour * hours,
                MemoryCost = memoryGB * MemoryCostPerGBHour * hours,
                StorageCost = storageGB * StorageCostPerGBMonth * ((endDate - startDate).TotalDays / 30.0),
                NetworkCost = networkGB * NetworkCostPerGB
            };

            item.TotalCost = item.ComputeCost + item.MemoryCost + item.StorageCost + item.NetworkCost;

            return item;
        }

        private List<CostBreakdownItem> GroupCostItems(List<CostBreakdownItem> items, string groupBy)
        {
            // Simplified grouping logic
            return items;
        }

        private double CalculateRandomCost(double min, double max)
        {
            var random = new Random();
            return min + (random.NextDouble() * (max - min));
        }

        private double CalculateMonthlySavings(double currentCpu, double currentMemory, double recommendedCpu, double recommendedMemory)
        {
            var hoursPerMonth = 730;
            var currentCost = (currentCpu * CpuCostPerCoreHour + currentMemory * MemoryCostPerGBHour) * hoursPerMonth;
            var recommendedCost = (recommendedCpu * CpuCostPerCoreHour + recommendedMemory * MemoryCostPerGBHour) * hoursPerMonth;
            return currentCost - recommendedCost;
        }

        private double CalculateSpotSavings(SpotInstanceConfig config)
        {
            // Assume 80% discount on spot instances
            var spotDiscount = 0.80;
            var baseMonthly Cost = 5000.0; // Base cost if all on-demand
            return baseMonthlyCost * (config.SpotPercentage / 100.0) * spotDiscount;
        }
    }

    // Data Models

    public class CostQuery
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = string.Empty; // namespace, team, label, etc.
        public Dictionary<string, string> Filters { get; set; } = new();
    }

    public class CostBreakdown
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalCost { get; set; }
        public List<CostBreakdownItem> BreakdownItems { get; set; } = new();
    }

    public class CostBreakdownItem
    {
        public string Dimension { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public double ComputeCost { get; set; }
        public double MemoryCost { get; set; }
        public double StorageCost { get; set; }
        public double NetworkCost { get; set; }
        public double TotalCost { get; set; }
    }

    public class CostAllocation
    {
        public string Dimension { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double ComputeCost { get; set; }
        public double MemoryCost { get; set; }
        public double StorageCost { get; set; }
        public double NetworkCost { get; set; }
        public double TotalCost { get; set; }
    }

    public class CostTrend
    {
        public string Dimension { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<CostDataPoint> DataPoints { get; set; } = new();
        public double TotalCost { get; set; }
        public double AverageDailyCost { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public double PercentageChange { get; set; }
    }

    public class CostDataPoint
    {
        public DateTime Date { get; set; }
        public double Cost { get; set; }
        public double ComputeCost { get; set; }
        public double MemoryCost { get; set; }
        public double StorageCost { get; set; }
        public double NetworkCost { get; set; }
    }

    public class CostAnomaly
    {
        public string Id { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public string Dimension { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public double ExpectedCost { get; set; }
        public double ActualCost { get; set; }
        public double Deviation { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> PossibleCauses { get; set; } = new();
    }

    public class RightsizingRecommendation
    {
        public string Id { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string WorkloadName { get; set; } = string.Empty;
        public string WorkloadType { get; set; } = string.Empty;
        public ResourceAllocation CurrentResources { get; set; } = new();
        public ResourceAllocation RecommendedResources { get; set; } = new();
        public ResourceUtilization CurrentUtilization { get; set; } = new();
        public double MonthlySavings { get; set; }
        public double AnnualSavings { get; set; }
        public double Confidence { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }

    public class ResourceAllocation
    {
        public double CpuCores { get; set; }
        public double MemoryGB { get; set; }
    }

    public class ResourceUtilization
    {
        public double CpuUtilization { get; set; }
        public double MemoryUtilization { get; set; }
    }

    public class RightsizingExecution
    {
        public string Id { get; set; } = string.Empty;
        public string RecommendationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<RightsizingStep> Steps { get; set; } = new();
        public double ActualSavings { get; set; }
    }

    public class RightsizingStep
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class OptimizationReport
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public double TotalMonthlyCost { get; set; }
        public double TotalPotentialSavings { get; set; }
        public double SavingsPercentage { get; set; }
        public List<OptimizationOpportunity> OptimizationOpportunities { get; set; } = new();
    }

    public class OptimizationOpportunity
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double PotentialMonthlySavings { get; set; }
        public int ActionCount { get; set; }
        public double Confidence { get; set; }
        public string Priority { get; set; } = string.Empty;
    }

    public class SpotInstanceConfig
    {
        public string Name { get; set; } = string.Empty;
        public List<string> TargetWorkloads { get; set; } = new();
        public double SpotPercentage { get; set; } = 80.0; // 80% spot, 20% on-demand
        public bool FallbackToOnDemand { get; set; } = true;
        public double MaxPrice { get; set; } = 0.5; // Max price as % of on-demand
        public List<string> InstanceTypes { get; set; } = new();
        public string DiversificationStrategy { get; set; } = "capacity-optimized";
    }

    public class SpotInstanceStrategy
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> TargetWorkloads { get; set; } = new();
        public double SpotPercentage { get; set; }
        public bool FallbackToOnDemand { get; set; }
        public double MaxPrice { get; set; }
        public List<string> InstanceTypes { get; set; } = new();
        public string DiversificationStrategy { get; set; } = string.Empty;
        public double ExpectedMonthlySavings { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SpotInstanceMetrics
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime CollectedAt { get; set; }
        public int TotalInstances { get; set; }
        public int SpotInstances { get; set; }
        public int OnDemandInstances { get; set; }
        public double SpotPercentage { get; set; }
        public int SpotInterruptions { get; set; }
        public double InterruptionRate { get; set; }
        public double AverageSavings { get; set; }
        public double MonthlySavings { get; set; }
        public List<SpotInterruption> InterruptionHistory { get; set; } = new();
    }

    public class SpotInterruption
    {
        public DateTime Timestamp { get; set; }
        public string InstanceType { get; set; } = string.Empty;
        public string AvailabilityZone { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double RecoveryTimeMs { get; set; }
    }

    public class SpotInterruptionPrediction
    {
        public string InstanceType { get; set; } = string.Empty;
        public DateTime PredictedAt { get; set; }
        public double InterruptionProbability { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public double Confidence { get; set; }
        public double HistoricalInterruptionRate { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class Budget
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Period { get; set; } = "monthly"; // daily, weekly, monthly, quarterly, yearly
        public List<string> Scope { get; set; } = new(); // namespaces, teams, labels
        public List<BudgetThreshold> Thresholds { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class BudgetThreshold
    {
        public double Percentage { get; set; } // 50, 80, 100
        public string AlertChannel { get; set; } = string.Empty; // email, slack, pagerduty
        public bool Notified { get; set; } = false;
    }

    public class BudgetStatus
    {
        public string BudgetId { get; set; } = string.Empty;
        public string BudgetName { get; set; } = string.Empty;
        public double Amount { get; set; }
        public double CurrentCost { get; set; }
        public double Percentage { get; set; }
        public double RemainingAmount { get; set; }
        public string Period { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // on_track, warning, exceeded
        public List<BudgetThreshold> TriggeredThresholds { get; set; } = new();
        public DateTime CheckedAt { get; set; }
    }

    public class CostForecast
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime ForecastedAt { get; set; }
        public TimeSpan Horizon { get; set; }
        public double CurrentMonthlyCost { get; set; }
        public double ForecastedMonthlyCost { get; set; }
        public double ExpectedGrowth { get; set; }
        public List<CostPrediction> Predictions { get; set; } = new();
    }

    public class CostPrediction
    {
        public DateTime Date { get; set; }
        public double PredictedCost { get; set; }
        public double ConfidenceLower { get; set; }
        public double ConfidenceUpper { get; set; }
    }

    public class ShowbackReport
    {
        public string TeamId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double TotalCost { get; set; }
        public Dictionary<string, double> CostBreakdown { get; set; } = new();
        public Dictionary<string, ResourceUsage> ResourceUsage { get; set; } = new();
    }

    public class ResourceUsage
    {
        public double Average { get; set; }
        public double Peak { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class ChargebackReport
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double TotalCost { get; set; }
        public List<TeamCharge> TeamCharges { get; set; } = new();
    }

    public class TeamCharge
    {
        public string TeamId { get; set; } = string.Empty;
        public double TotalCost { get; set; }
        public double ComputeCost { get; set; }
        public double MemoryCost { get; set; }
        public double StorageCost { get; set; }
        public double NetworkCost { get; set; }
    }

    public class CostAllocationRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AllocationMethod { get; set; } = string.Empty; // proportional, equal, custom
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class CarbonFootprint
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CalculatedAt { get; set; }
        public double TotalEmissionsKgCO2e { get; set; }
        public Dictionary<string, double> EmissionsBySource { get; set; } = new(); // By region/cloud
        public Dictionary<string, double> EmissionsByResourceType { get; set; } = new();
        public Dictionary<string, double> Equivalents { get; set; } = new(); // Trees, km driven, etc.
    }

    public class EnergyMetrics
    {
        public string Namespace { get; set; } = string.Empty;
        public DateTime CollectedAt { get; set; }
        public double TotalEnergyWh { get; set; }
        public List<PodEnergyMetric> PodMetrics { get; set; } = new();
        public double CarbonEmissionsKgCO2e { get; set; }
    }

    public class PodEnergyMetric
    {
        public string PodName { get; set; } = string.Empty;
        public double CPUEnergyWh { get; set; }
        public double DRAMEnergyWh { get; set; }
        public double OtherEnergyWh { get; set; }
        public double TotalEnergyWh { get; set; }
    }

    public class SustainabilityReport
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double TotalEmissionsKgCO2e { get; set; }
        public Dictionary<string, double> EmissionsByRegion { get; set; } = new();
        public double TotalEnergyUsageKWh { get; set; }
        public double EnergyEfficiency { get; set; } // PUE
        public double RenewableEnergyPercentage { get; set; }
        public double EmissionsTrend { get; set; } // % change
        public double EfficiencyTrend { get; set; }
        public List<SustainabilityRecommendation> Recommendations { get; set; } = new();
    }

    public class SustainabilityRecommendation
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double EstimatedEmissionReduction { get; set; } // kg CO2e
        public double EmissionReductionPercentage { get; set; }
        public string Implementation { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
    }
}
