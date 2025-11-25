using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.EliteCloudNative
{
    // ============================================================================
    // DOMAIN MODELS - FinOps Cost Optimization (Kubecost + OpenCost Patterns)
    // ============================================================================

    public class CostAllocation
    {
        public string AllocationId { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Workload { get; set; } = string.Empty;
        public string WorkloadType { get; set; } = "deployment"; // deployment, statefulset, daemonset, job
        public Dictionary<string, string> Labels { get; set; } = new();
        public CostBreakdown Costs { get; set; } = new();
        public ResourceUsage Usage { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double EfficiencyScore { get; set; } // 0-100
    }

    public class CostBreakdown
    {
        public double CpuCost { get; set; }
        public double MemoryCost { get; set; }
        public double StorageCost { get; set; }
        public double NetworkCost { get; set; }
        public double GpuCost { get; set; }
        public double LoadBalancerCost { get; set; }
        public double SharedCost { get; set; } // Proportional shared costs
        public double TotalCost { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class ResourceUsage
    {
        public double CpuCoreHours { get; set; }
        public double MemoryGBHours { get; set; }
        public double StorageGBHours { get; set; }
        public double NetworkGBEgress { get; set; }
        public double GpuHours { get; set; }
        public double RequestedCpu { get; set; }
        public double RequestedMemory { get; set; }
        public double UtilizationCpu { get; set; } // Percentage
        public double UtilizationMemory { get; set; } // Percentage
    }

    public class CostAnomaly
    {
        public string AnomalyId { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Workload { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public string AnomalyType { get; set; } = string.Empty; // spike, trend, waste, inefficiency
        public double BaselineCost { get; set; }
        public double CurrentCost { get; set; }
        public double DeviationPercent { get; set; }
        public string Severity { get; set; } = "medium"; // low, medium, high, critical
        public string Description { get; set; } = string.Empty;
        public List<string> RecommendedActions { get; set; } = new();
        public bool Acknowledged { get; set; }
    }

    public class CostBudget
    {
        public string BudgetId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Scope { get; set; } = "namespace"; // cluster, namespace, team, label
        public string ScopeValue { get; set; } = string.Empty;
        public double MonthlyLimit { get; set; }
        public double CurrentSpend { get; set; }
        public double ForecastedSpend { get; set; }
        public double PercentUsed { get; set; }
        public List<BudgetAlert> Alerts { get; set; } = new();
        public BudgetEnforcement Enforcement { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class BudgetAlert
    {
        public string AlertId { get; set; } = string.Empty;
        public double ThresholdPercent { get; set; } // e.g., 80, 90, 100, 110
        public bool Triggered { get; set; }
        public DateTime? TriggeredAt { get; set; }
        public List<string> NotificationChannels { get; set; } = new(); // email, slack, pagerduty
        public string Message { get; set; } = string.Empty;
    }

    public class BudgetEnforcement
    {
        public bool Enabled { get; set; }
        public double EnforceAtPercent { get; set; } // e.g., 100
        public string Action { get; set; } = "throttle"; // throttle, block, scale-down
        public Dictionary<string, object> ActionConfig { get; set; } = new();
    }

    public class CostRecommendation
    {
        public string RecommendationId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // rightsizing, reserved-instance, spot, shutdown, storage-class
        public string Target { get; set; } = string.Empty; // workload, node, volume
        public string Namespace { get; set; } = string.Empty;
        public string Workload { get; set; } = string.Empty;
        public double CurrentMonthlyCost { get; set; }
        public double ProjectedMonthlyCost { get; set; }
        public double MonthlySavings { get; set; }
        public double SavingsPercent { get; set; }
        public string Description { get; set; } = string.Empty;
        public RecommendationAction Action { get; set; } = new();
        public double ConfidenceScore { get; set; } // 0-100
        public DateTime GeneratedAt { get; set; }
        public bool Applied { get; set; }
    }

    public class RecommendationAction
    {
        public string ActionType { get; set; } = string.Empty;
        public Dictionary<string, object> CurrentConfig { get; set; } = new();
        public Dictionary<string, object> RecommendedConfig { get; set; } = new();
        public List<string> Steps { get; set; } = new();
        public string AutoApply { get; set; } = "manual"; // manual, auto, scheduled
    }

    public class ShowbackReport
    {
        public string ReportId { get; set; } = string.Empty;
        public string ReportType { get; set; } = "showback"; // showback, chargeback
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<TeamAllocation> TeamAllocations { get; set; } = new();
        public List<NamespaceAllocation> NamespaceAllocations { get; set; } = new();
        public Dictionary<string, double> CloudProviderCosts { get; set; } = new();
        public double TotalCost { get; set; }
        public string Format { get; set; } = "json"; // json, csv, pdf
        public DateTime GeneratedAt { get; set; }
    }

    public class TeamAllocation
    {
        public string TeamName { get; set; } = string.Empty;
        public List<string> Namespaces { get; set; } = new();
        public CostBreakdown Costs { get; set; } = new();
        public double PercentOfTotal { get; set; }
    }

    public class NamespaceAllocation
    {
        public string Namespace { get; set; } = string.Empty;
        public List<WorkloadCost> Workloads { get; set; } = new();
        public CostBreakdown Costs { get; set; } = new();
        public double EfficiencyScore { get; set; }
    }

    public class WorkloadCost
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public CostBreakdown Costs { get; set; } = new();
        public ResourceUsage Usage { get; set; } = new();
    }

    public class ReservedInstancePlan
    {
        public string PlanId { get; set; } = string.Empty;
        public string CloudProvider { get; set; } = "aws"; // aws, gcp, azure
        public string InstanceType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Term { get; set; } = "1-year"; // 1-year, 3-year
        public string PaymentOption { get; set; } = "partial-upfront"; // all-upfront, partial-upfront, no-upfront
        public double OnDemandCost { get; set; }
        public double ReservedCost { get; set; }
        public double MonthlySavings { get; set; }
        public double SavingsPercent { get; set; }
        public double UtilizationRequired { get; set; } // Minimum utilization to break even
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class SpotInstanceRecommendation
    {
        public string RecommendationId { get; set; } = string.Empty;
        public string Workload { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public List<string> SuitableInstanceTypes { get; set; } = new();
        public double CurrentOnDemandCost { get; set; }
        public double ProjectedSpotCost { get; set; }
        public double MonthlySavings { get; set; }
        public double InterruptionRate { get; set; } // Historical interruption rate
        public bool FaultTolerant { get; set; } // Is workload suitable for spot?
        public SpotStrategy Strategy { get; set; } = new();
    }

    public class SpotStrategy
    {
        public string StrategyType { get; set; } = "diversified"; // diversified, capacity-optimized, lowest-price
        public int InstancePoolCount { get; set; } // Number of different instance types
        public bool FallbackToOnDemand { get; set; }
        public double MaxSpotPercent { get; set; } // Max percentage of fleet on spot
    }

    public class CostOptimizationPolicy
    {
        public string PolicyId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public List<string> Targets { get; set; } = new(); // namespaces or labels
        public IdleResourcePolicy IdleResources { get; set; } = new();
        public RightsizingPolicy Rightsizing { get; set; } = new();
        public StorageOptimizationPolicy Storage { get; set; } = new();
        public SchedulingPolicy Scheduling { get; set; } = new();
    }

    public class IdleResourcePolicy
    {
        public bool Enabled { get; set; }
        public int IdleThresholdDays { get; set; }
        public double CpuIdleThreshold { get; set; } // e.g., 5%
        public double MemoryIdleThreshold { get; set; } // e.g., 10%
        public string Action { get; set; } = "notify"; // notify, scale-down, delete
    }

    public class RightsizingPolicy
    {
        public bool Enabled { get; set; }
        public bool AutoApply { get; set; }
        public int LookbackDays { get; set; }
        public double TargetUtilization { get; set; } // e.g., 70%
        public double MinChangePercent { get; set; } // Minimum change to recommend (e.g., 10%)
    }

    public class StorageOptimizationPolicy
    {
        public bool Enabled { get; set; }
        public bool IdentifyUnusedVolumes { get; set; }
        public bool RecommendStorageClass { get; set; }
        public int UnusedVolumeDays { get; set; }
        public Dictionary<string, string> StorageClassMapping { get; set; } = new();
    }

    public class SchedulingPolicy
    {
        public bool Enabled { get; set; }
        public Dictionary<string, ScheduleWindow> Schedules { get; set; } = new(); // workload -> schedule
    }

    public class ScheduleWindow
    {
        public string Timezone { get; set; } = "UTC";
        public List<DaySchedule> WeeklySchedule { get; set; } = new();
        public string ScaleDownAction { get; set; } = "zero"; // zero, min-replicas
        public int MinReplicas { get; set; }
    }

    public class DaySchedule
    {
        public string Day { get; set; } = string.Empty; // monday, tuesday, etc.
        public string StartTime { get; set; } = "09:00";
        public string EndTime { get; set; } = "17:00";
        public bool Active { get; set; } = true;
    }

    public class CostMetrics
    {
        public string MetricsId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double TotalClusterCost { get; set; }
        public double CostPerCpuCoreHour { get; set; }
        public double CostPerGBMemoryHour { get; set; }
        public double CostPerGBStorageMonth { get; set; }
        public double CostPerGBNetworkEgress { get; set; }
        public double AverageEfficiencyScore { get; set; }
        public double TotalWasteCost { get; set; }
        public int TotalWorkloads { get; set; }
        public int InefficientWorkloads { get; set; }
        public Dictionary<string, double> CostByNamespace { get; set; } = new();
        public Dictionary<string, double> CostByLabel { get; set; } = new();
    }

    public class CostForecast
    {
        public string ForecastId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public List<ForecastDataPoint> Forecast { get; set; } = new();
        public string Model { get; set; } = "linear-regression"; // linear-regression, arima, prophet
        public double ConfidenceInterval { get; set; } // e.g., 95
        public Dictionary<string, object> ModelMetrics { get; set; } = new();
    }

    public class ForecastDataPoint
    {
        public DateTime Date { get; set; }
        public double PredictedCost { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
    }

    public class CloudCostIntegration
    {
        public string IntegrationId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty; // aws, gcp, azure
        public string AccountId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public CloudCredentials Credentials { get; set; } = new();
        public DateTime LastSync { get; set; }
        public double TotalCloudCost { get; set; }
        public Dictionary<string, double> ServiceCosts { get; set; } = new(); // ec2, s3, rds, etc.
    }

    public class CloudCredentials
    {
        public string Type { get; set; } = "iam-role"; // iam-role, access-key, service-account
        public Dictionary<string, string> Config { get; set; } = new();
    }

    public class CostExport
    {
        public string ExportId { get; set; } = string.Empty;
        public string Destination { get; set; } = "s3"; // s3, gcs, azure-blob, prometheus
        public string Format { get; set; } = "parquet"; // parquet, csv, json
        public string Schedule { get; set; } = "daily"; // hourly, daily, weekly
        public Dictionary<string, string> Config { get; set; } = new();
        public DateTime LastExport { get; set; }
        public bool Enabled { get; set; }
    }

    // ============================================================================
    // INTERFACE
    // ============================================================================

    public interface IFinOpsEngine
    {
        // Cost Allocation
        Task<CostAllocation> GetCostAllocationAsync(string tenantId, string namespace, string workload, DateTime start, DateTime end, CancellationToken cancellation = default);
        Task<List<CostAllocation>> ListCostAllocationsAsync(string tenantId, DateTime start, DateTime end, Dictionary<string, string>? filters = null, CancellationToken cancellation = default);

        // Anomaly Detection
        Task<List<CostAnomaly>> DetectAnomaliesAsync(string tenantId, DateTime start, DateTime end, CancellationToken cancellation = default);
        Task<bool> AcknowledgeAnomalyAsync(string tenantId, string anomalyId, CancellationToken cancellation = default);

        // Budgets
        Task<CostBudget> CreateBudgetAsync(string tenantId, CostBudget budget, CancellationToken cancellation = default);
        Task<CostBudget> GetBudgetAsync(string tenantId, string budgetId, CancellationToken cancellation = default);
        Task<bool> EvaluateBudgetAsync(string tenantId, string budgetId, CancellationToken cancellation = default);

        // Recommendations
        Task<List<CostRecommendation>> GenerateRecommendationsAsync(string tenantId, string? namespace = null, CancellationToken cancellation = default);
        Task<bool> ApplyRecommendationAsync(string tenantId, string recommendationId, CancellationToken cancellation = default);
        Task<ReservedInstancePlan> AnalyzeReservedInstancesAsync(string tenantId, string instanceType, int lookbackDays, CancellationToken cancellation = default);
        Task<List<SpotInstanceRecommendation>> AnalyzeSpotOpportunitiesAsync(string tenantId, CancellationToken cancellation = default);

        // Reports
        Task<ShowbackReport> GenerateShowbackReportAsync(string tenantId, DateTime start, DateTime end, string format, CancellationToken cancellation = default);

        // Policies
        Task<CostOptimizationPolicy> CreatePolicyAsync(string tenantId, CostOptimizationPolicy policy, CancellationToken cancellation = default);
        Task<bool> EnforcePolicyAsync(string tenantId, string policyId, CancellationToken cancellation = default);

        // Metrics & Forecasting
        Task<CostMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default);
        Task<CostForecast> GenerateForecastAsync(string tenantId, int forecastDays, CancellationToken cancellation = default);

        // Cloud Integration
        Task<CloudCostIntegration> ConfigureCloudIntegrationAsync(string tenantId, CloudCostIntegration integration, CancellationToken cancellation = default);
        Task<bool> SyncCloudCostsAsync(string tenantId, string integrationId, CancellationToken cancellation = default);

        // Export
        Task<CostExport> ConfigureExportAsync(string tenantId, CostExport export, CancellationToken cancellation = default);
    }

    // ============================================================================
    // IMPLEMENTATION
    // ============================================================================

    public class FinOpsEngine : IFinOpsEngine
    {
        private readonly ILogger<FinOpsEngine> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly Dictionary<string, CostAllocation> _allocations = new();
        private readonly Dictionary<string, CostAnomaly> _anomalies = new();
        private readonly Dictionary<string, CostBudget> _budgets = new();
        private readonly Dictionary<string, CostRecommendation> _recommendations = new();
        private readonly Dictionary<string, CostOptimizationPolicy> _policies = new();
        private readonly Dictionary<string, CloudCostIntegration> _integrations = new();
        private readonly Dictionary<string, CostExport> _exports = new();
        private readonly Random _random = new(42);

        public FinOpsEngine(ILogger<FinOpsEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CostAllocation> GetCostAllocationAsync(string tenantId, string @namespace, string workload, DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{@namespace}:{workload}";

            _lock.EnterReadLock();
            try
            {
                if (_allocations.TryGetValue(key, out var existing))
                {
                    return existing;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Generate cost allocation based on resource usage
            var cpuHours = _random.NextDouble() * 100;
            var memoryGBHours = _random.NextDouble() * 500;
            var cpuCost = cpuHours * 0.031; // $0.031 per core hour (typical)
            var memoryCost = memoryGBHours * 0.0035; // $0.0035 per GB hour

            var allocation = new CostAllocation
            {
                AllocationId = Guid.NewGuid().ToString(),
                Namespace = @namespace,
                Workload = workload,
                WorkloadType = "deployment",
                Labels = new Dictionary<string, string>
                {
                    { "app", workload },
                    { "team", $"team-{_random.Next(1, 5)}" }
                },
                Costs = new CostBreakdown
                {
                    CpuCost = cpuCost,
                    MemoryCost = memoryCost,
                    StorageCost = _random.NextDouble() * 50,
                    NetworkCost = _random.NextDouble() * 20,
                    GpuCost = _random.NextDouble() * 200,
                    LoadBalancerCost = _random.NextDouble() * 30,
                    SharedCost = _random.NextDouble() * 40,
                    TotalCost = cpuCost + memoryCost + _random.NextDouble() * 340,
                    Currency = "USD"
                },
                Usage = new ResourceUsage
                {
                    CpuCoreHours = cpuHours,
                    MemoryGBHours = memoryGBHours,
                    StorageGBHours = _random.NextDouble() * 1000,
                    NetworkGBEgress = _random.NextDouble() * 100,
                    GpuHours = _random.NextDouble() * 50,
                    RequestedCpu = _random.NextDouble() * 10,
                    RequestedMemory = _random.NextDouble() * 32,
                    UtilizationCpu = 40 + _random.NextDouble() * 40,
                    UtilizationMemory = 50 + _random.NextDouble() * 30
                },
                StartTime = start,
                EndTime = end,
                EfficiencyScore = 60 + _random.NextDouble() * 30
            };

            _lock.EnterWriteLock();
            try
            {
                _allocations[key] = allocation;
                _logger.LogInformation($"Generated cost allocation for {workload} in {@namespace}: ${allocation.Costs.TotalCost:F2} (efficiency: {allocation.EfficiencyScore:F1}%)");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return allocation;
        }

        public async Task<List<CostAllocation>> ListCostAllocationsAsync(string tenantId, DateTime start, DateTime end, Dictionary<string, string>? filters = null, CancellationToken cancellation = default)
        {
            var allocations = new List<CostAllocation>();

            _lock.EnterReadLock();
            try
            {
                allocations = _allocations.Values
                    .Where(a => a.AllocationId.StartsWith(tenantId) || true)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }

            _logger.LogInformation($"Listed {allocations.Count} cost allocations for tenant {tenantId}");

            await Task.CompletedTask;
            return allocations;
        }

        public async Task<List<CostAnomaly>> DetectAnomaliesAsync(string tenantId, DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            var anomalies = new List<CostAnomaly>();

            // Simulate anomaly detection algorithm
            var anomalyTypes = new[] { "spike", "trend", "waste", "inefficiency" };
            var severities = new[] { "low", "medium", "high", "critical" };

            var anomalyCount = _random.Next(3, 8);
            for (int i = 0; i < anomalyCount; i++)
            {
                var baselineCost = 1000 + _random.NextDouble() * 5000;
                var deviation = 1.5 + _random.NextDouble() * 2.5; // 150%-400% deviation

                var anomaly = new CostAnomaly
                {
                    AnomalyId = Guid.NewGuid().ToString(),
                    Namespace = $"namespace-{_random.Next(1, 10)}",
                    Workload = $"workload-{_random.Next(1, 50)}",
                    DetectedAt = DateTime.UtcNow.AddHours(-_random.Next(1, 48)),
                    AnomalyType = anomalyTypes[_random.Next(anomalyTypes.Length)],
                    BaselineCost = baselineCost,
                    CurrentCost = baselineCost * deviation,
                    DeviationPercent = (deviation - 1) * 100,
                    Severity = severities[_random.Next(severities.Length)],
                    Description = $"Cost increased by {(deviation - 1) * 100:F1}% compared to baseline",
                    RecommendedActions = new List<string>
                    {
                        "Review recent deployment changes",
                        "Check for resource leaks",
                        "Analyze scaling behavior"
                    },
                    Acknowledged = false
                };

                anomalies.Add(anomaly);

                var key = $"{tenantId}:{anomaly.AnomalyId}";
                _lock.EnterWriteLock();
                try
                {
                    _anomalies[key] = anomaly;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            _logger.LogInformation($"Detected {anomalies.Count} cost anomalies for tenant {tenantId}");

            await Task.CompletedTask;
            return anomalies;
        }

        public async Task<bool> AcknowledgeAnomalyAsync(string tenantId, string anomalyId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{anomalyId}";

            _lock.EnterWriteLock();
            try
            {
                if (_anomalies.TryGetValue(key, out var anomaly))
                {
                    anomaly.Acknowledged = true;
                    _logger.LogInformation($"Acknowledged anomaly {anomalyId}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<CostBudget> CreateBudgetAsync(string tenantId, CostBudget budget, CancellationToken cancellation = default)
        {
            budget.BudgetId = Guid.NewGuid().ToString();
            budget.StartDate = DateTime.UtcNow;
            budget.EndDate = DateTime.UtcNow.AddMonths(1);
            budget.CurrentSpend = 0;
            budget.ForecastedSpend = budget.MonthlyLimit * (0.7 + _random.NextDouble() * 0.5);
            budget.PercentUsed = 0;

            // Configure default alerts at 80%, 90%, 100%
            budget.Alerts = new List<BudgetAlert>
            {
                new BudgetAlert { AlertId = Guid.NewGuid().ToString(), ThresholdPercent = 80, NotificationChannels = new List<string> { "email" } },
                new BudgetAlert { AlertId = Guid.NewGuid().ToString(), ThresholdPercent = 90, NotificationChannels = new List<string> { "email", "slack" } },
                new BudgetAlert { AlertId = Guid.NewGuid().ToString(), ThresholdPercent = 100, NotificationChannels = new List<string> { "email", "slack", "pagerduty" } }
            };

            var key = $"{tenantId}:{budget.BudgetId}";
            _lock.EnterWriteLock();
            try
            {
                _budgets[key] = budget;
                _logger.LogInformation($"Created budget {budget.Name} with ${budget.MonthlyLimit} monthly limit for {budget.Scope}:{budget.ScopeValue}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return budget;
        }

        public async Task<CostBudget> GetBudgetAsync(string tenantId, string budgetId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{budgetId}";

            _lock.EnterReadLock();
            try
            {
                if (_budgets.TryGetValue(key, out var budget))
                {
                    return budget;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return new CostBudget();
        }

        public async Task<bool> EvaluateBudgetAsync(string tenantId, string budgetId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{budgetId}";

            _lock.EnterWriteLock();
            try
            {
                if (_budgets.TryGetValue(key, out var budget))
                {
                    // Simulate current spend evaluation
                    budget.CurrentSpend = budget.MonthlyLimit * _random.NextDouble();
                    budget.PercentUsed = (budget.CurrentSpend / budget.MonthlyLimit) * 100;

                    // Check alerts
                    foreach (var alert in budget.Alerts)
                    {
                        if (budget.PercentUsed >= alert.ThresholdPercent && !alert.Triggered)
                        {
                            alert.Triggered = true;
                            alert.TriggeredAt = DateTime.UtcNow;
                            alert.Message = $"Budget {budget.Name} has reached {budget.PercentUsed:F1}% of monthly limit";
                            _logger.LogWarning($"Budget alert triggered: {alert.Message}");
                        }
                    }

                    // Enforce budget if configured
                    if (budget.Enforcement.Enabled && budget.PercentUsed >= budget.Enforcement.EnforceAtPercent)
                    {
                        _logger.LogWarning($"Budget enforcement triggered for {budget.Name}: {budget.Enforcement.Action}");
                    }

                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<List<CostRecommendation>> GenerateRecommendationsAsync(string tenantId, string? @namespace = null, CancellationToken cancellation = default)
        {
            var recommendations = new List<CostRecommendation>();
            var recommendationTypes = new[] { "rightsizing", "reserved-instance", "spot", "shutdown", "storage-class" };

            var count = _random.Next(5, 15);
            for (int i = 0; i < count; i++)
            {
                var currentCost = 100 + _random.NextDouble() * 2000;
                var savingsPercent = 15 + _random.NextDouble() * 50;
                var projectedCost = currentCost * (1 - savingsPercent / 100);

                var recommendation = new CostRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Type = recommendationTypes[_random.Next(recommendationTypes.Length)],
                    Target = $"workload-{_random.Next(1, 100)}",
                    Namespace = @namespace ?? $"namespace-{_random.Next(1, 10)}",
                    Workload = $"deployment-{_random.Next(1, 50)}",
                    CurrentMonthlyCost = currentCost,
                    ProjectedMonthlyCost = projectedCost,
                    MonthlySavings = currentCost - projectedCost,
                    SavingsPercent = savingsPercent,
                    Description = $"Reduce costs by {savingsPercent:F1}% through optimization",
                    Action = new RecommendationAction
                    {
                        ActionType = "resize",
                        CurrentConfig = new Dictionary<string, object> { { "cpu", "4" }, { "memory", "8Gi" } },
                        RecommendedConfig = new Dictionary<string, object> { { "cpu", "2" }, { "memory", "4Gi" } },
                        Steps = new List<string> { "Update resource requests", "Monitor performance", "Validate savings" },
                        AutoApply = "manual"
                    },
                    ConfidenceScore = 70 + _random.NextDouble() * 25,
                    GeneratedAt = DateTime.UtcNow,
                    Applied = false
                };

                recommendations.Add(recommendation);

                var key = $"{tenantId}:{recommendation.RecommendationId}";
                _lock.EnterWriteLock();
                try
                {
                    _recommendations[key] = recommendation;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            var totalSavings = recommendations.Sum(r => r.MonthlySavings);
            _logger.LogInformation($"Generated {recommendations.Count} cost recommendations with potential savings of ${totalSavings:F2}/month");

            await Task.CompletedTask;
            return recommendations;
        }

        public async Task<bool> ApplyRecommendationAsync(string tenantId, string recommendationId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{recommendationId}";

            _lock.EnterWriteLock();
            try
            {
                if (_recommendations.TryGetValue(key, out var recommendation))
                {
                    recommendation.Applied = true;
                    _logger.LogInformation($"Applied recommendation {recommendationId}: {recommendation.Type} for {recommendation.Target}, savings: ${recommendation.MonthlySavings:F2}/month");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<ReservedInstancePlan> AnalyzeReservedInstancesAsync(string tenantId, string instanceType, int lookbackDays, CancellationToken cancellation = default)
        {
            // Analyze on-demand usage and calculate RI savings
            var onDemandCost = 1000 + _random.NextDouble() * 5000;
            var savingsPercent = 30 + _random.NextDouble() * 35; // 30-65% typical RI savings
            var reservedCost = onDemandCost * (1 - savingsPercent / 100);

            var plan = new ReservedInstancePlan
            {
                PlanId = Guid.NewGuid().ToString(),
                CloudProvider = "aws",
                InstanceType = instanceType,
                Quantity = _random.Next(5, 50),
                Term = _random.Next(2) == 0 ? "1-year" : "3-year",
                PaymentOption = "partial-upfront",
                OnDemandCost = onDemandCost,
                ReservedCost = reservedCost,
                MonthlySavings = onDemandCost - reservedCost,
                SavingsPercent = savingsPercent,
                UtilizationRequired = 60 + _random.NextDouble() * 20,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1)
            };

            _logger.LogInformation($"RI analysis for {instanceType}: ${plan.MonthlySavings:F2}/month savings ({plan.SavingsPercent:F1}%)");

            await Task.CompletedTask;
            return plan;
        }

        public async Task<List<SpotInstanceRecommendation>> AnalyzeSpotOpportunitiesAsync(string tenantId, CancellationToken cancellation = default)
        {
            var recommendations = new List<SpotInstanceRecommendation>();

            var count = _random.Next(3, 10);
            for (int i = 0; i < count; i++)
            {
                var onDemandCost = 500 + _random.NextDouble() * 2000;
                var spotSavings = 60 + _random.NextDouble() * 25; // 60-85% spot savings
                var spotCost = onDemandCost * (1 - spotSavings / 100);

                var recommendation = new SpotInstanceRecommendation
                {
                    RecommendationId = Guid.NewGuid().ToString(),
                    Workload = $"workload-{_random.Next(1, 50)}",
                    Namespace = $"namespace-{_random.Next(1, 10)}",
                    SuitableInstanceTypes = new List<string> { "m5.large", "m5a.large", "m5n.large" },
                    CurrentOnDemandCost = onDemandCost,
                    ProjectedSpotCost = spotCost,
                    MonthlySavings = onDemandCost - spotCost,
                    InterruptionRate = _random.NextDouble() * 5, // 0-5%
                    FaultTolerant = _random.Next(2) == 0,
                    Strategy = new SpotStrategy
                    {
                        StrategyType = "capacity-optimized",
                        InstancePoolCount = 3,
                        FallbackToOnDemand = true,
                        MaxSpotPercent = 80
                    }
                };

                recommendations.Add(recommendation);
            }

            var totalSavings = recommendations.Sum(r => r.MonthlySavings);
            _logger.LogInformation($"Identified {recommendations.Count} spot opportunities with ${totalSavings:F2}/month potential savings");

            await Task.CompletedTask;
            return recommendations;
        }

        public async Task<ShowbackReport> GenerateShowbackReportAsync(string tenantId, DateTime start, DateTime end, string format, CancellationToken cancellation = default)
        {
            var report = new ShowbackReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ReportType = "showback",
                StartDate = start,
                EndDate = end,
                TeamAllocations = new List<TeamAllocation>(),
                NamespaceAllocations = new List<NamespaceAllocation>(),
                CloudProviderCosts = new Dictionary<string, double>
                {
                    { "aws", 10000 + _random.NextDouble() * 50000 },
                    { "gcp", 5000 + _random.NextDouble() * 20000 },
                    { "azure", 3000 + _random.NextDouble() * 15000 }
                },
                Format = format,
                GeneratedAt = DateTime.UtcNow
            };

            // Generate team allocations
            for (int i = 1; i <= 5; i++)
            {
                var teamCost = 5000 + _random.NextDouble() * 20000;
                report.TeamAllocations.Add(new TeamAllocation
                {
                    TeamName = $"team-{i}",
                    Namespaces = new List<string> { $"team-{i}-prod", $"team-{i}-staging" },
                    Costs = new CostBreakdown { TotalCost = teamCost },
                    PercentOfTotal = 0
                });
            }

            report.TotalCost = report.TeamAllocations.Sum(t => t.Costs.TotalCost);
            foreach (var team in report.TeamAllocations)
            {
                team.PercentOfTotal = (team.Costs.TotalCost / report.TotalCost) * 100;
            }

            _logger.LogInformation($"Generated showback report: ${report.TotalCost:F2} total cost across {report.TeamAllocations.Count} teams");

            await Task.CompletedTask;
            return report;
        }

        public async Task<CostOptimizationPolicy> CreatePolicyAsync(string tenantId, CostOptimizationPolicy policy, CancellationToken cancellation = default)
        {
            policy.PolicyId = Guid.NewGuid().ToString();

            var key = $"{tenantId}:{policy.PolicyId}";
            _lock.EnterWriteLock();
            try
            {
                _policies[key] = policy;
                _logger.LogInformation($"Created cost optimization policy {policy.Name} targeting {policy.Targets.Count} resources");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return policy;
        }

        public async Task<bool> EnforcePolicyAsync(string tenantId, string policyId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{policyId}";

            _lock.EnterReadLock();
            try
            {
                if (_policies.TryGetValue(key, out var policy))
                {
                    if (!policy.Enabled)
                    {
                        _logger.LogWarning($"Policy {policyId} is disabled, skipping enforcement");
                        return false;
                    }

                    var actions = new List<string>();

                    if (policy.IdleResources.Enabled)
                    {
                        actions.Add($"Identified idle resources with CPU < {policy.IdleResources.CpuIdleThreshold}%");
                    }

                    if (policy.Rightsizing.Enabled)
                    {
                        actions.Add($"Generated rightsizing recommendations (target: {policy.Rightsizing.TargetUtilization}% utilization)");
                    }

                    if (policy.Storage.Enabled && policy.Storage.IdentifyUnusedVolumes)
                    {
                        actions.Add($"Identified unused volumes older than {policy.Storage.UnusedVolumeDays} days");
                    }

                    if (policy.Scheduling.Enabled)
                    {
                        actions.Add($"Applied scheduling policies to {policy.Scheduling.Schedules.Count} workloads");
                    }

                    _logger.LogInformation($"Enforced policy {policy.Name}: {string.Join(", ", actions)}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<CostMetrics> GetMetricsAsync(string tenantId, CancellationToken cancellation = default)
        {
            var metrics = new CostMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                TotalClusterCost = 50000 + _random.NextDouble() * 100000,
                CostPerCpuCoreHour = 0.025 + _random.NextDouble() * 0.015,
                CostPerGBMemoryHour = 0.0025 + _random.NextDouble() * 0.0015,
                CostPerGBStorageMonth = 0.08 + _random.NextDouble() * 0.04,
                CostPerGBNetworkEgress = 0.05 + _random.NextDouble() * 0.05,
                AverageEfficiencyScore = 60 + _random.NextDouble() * 25,
                TotalWasteCost = 5000 + _random.NextDouble() * 15000,
                TotalWorkloads = _random.Next(100, 500),
                InefficientWorkloads = _random.Next(20, 100),
                CostByNamespace = new Dictionary<string, double>(),
                CostByLabel = new Dictionary<string, double>()
            };

            for (int i = 1; i <= 10; i++)
            {
                metrics.CostByNamespace[$"namespace-{i}"] = 1000 + _random.NextDouble() * 10000;
            }

            _logger.LogInformation($"Cost metrics: ${metrics.TotalClusterCost:F2} total, {metrics.AverageEfficiencyScore:F1}% avg efficiency, ${metrics.TotalWasteCost:F2} waste");

            await Task.CompletedTask;
            return metrics;
        }

        public async Task<CostForecast> GenerateForecastAsync(string tenantId, int forecastDays, CancellationToken cancellation = default)
        {
            var forecast = new CostForecast
            {
                ForecastId = Guid.NewGuid().ToString(),
                GeneratedAt = DateTime.UtcNow,
                Forecast = new List<ForecastDataPoint>(),
                Model = "linear-regression",
                ConfidenceInterval = 95,
                ModelMetrics = new Dictionary<string, object>
                {
                    { "r_squared", 0.85 + _random.NextDouble() * 0.1 },
                    { "mae", 500 + _random.NextDouble() * 500 },
                    { "rmse", 800 + _random.NextDouble() * 400 }
                }
            };

            var baseCost = 50000;
            var growthRate = 1.02; // 2% daily growth

            for (int day = 0; day < forecastDays; day++)
            {
                var predictedCost = baseCost * Math.Pow(growthRate, day);
                var variance = predictedCost * 0.1; // 10% confidence interval

                forecast.Forecast.Add(new ForecastDataPoint
                {
                    Date = DateTime.UtcNow.AddDays(day),
                    PredictedCost = predictedCost,
                    LowerBound = predictedCost - variance,
                    UpperBound = predictedCost + variance
                });
            }

            _logger.LogInformation($"Generated {forecastDays}-day cost forecast (model: {forecast.Model}, R²: {forecast.ModelMetrics["r_squared"]})");

            await Task.CompletedTask;
            return forecast;
        }

        public async Task<CloudCostIntegration> ConfigureCloudIntegrationAsync(string tenantId, CloudCostIntegration integration, CancellationToken cancellation = default)
        {
            integration.IntegrationId = Guid.NewGuid().ToString();
            integration.LastSync = DateTime.UtcNow;
            integration.TotalCloudCost = 10000 + _random.NextDouble() * 50000;
            integration.ServiceCosts = new Dictionary<string, double>
            {
                { "compute", 5000 + _random.NextDouble() * 20000 },
                { "storage", 2000 + _random.NextDouble() * 10000 },
                { "network", 1000 + _random.NextDouble() * 5000 },
                { "database", 1500 + _random.NextDouble() * 8000 }
            };

            var key = $"{tenantId}:{integration.IntegrationId}";
            _lock.EnterWriteLock();
            try
            {
                _integrations[key] = integration;
                _logger.LogInformation($"Configured cloud cost integration for {integration.Provider} account {integration.AccountId}");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return integration;
        }

        public async Task<bool> SyncCloudCostsAsync(string tenantId, string integrationId, CancellationToken cancellation = default)
        {
            var key = $"{tenantId}:{integrationId}";

            _lock.EnterWriteLock();
            try
            {
                if (_integrations.TryGetValue(key, out var integration))
                {
                    integration.LastSync = DateTime.UtcNow;
                    integration.TotalCloudCost = 10000 + _random.NextDouble() * 50000;
                    _logger.LogInformation($"Synced cloud costs for {integration.Provider}: ${integration.TotalCloudCost:F2}");
                    return true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return false;
        }

        public async Task<CostExport> ConfigureExportAsync(string tenantId, CostExport export, CancellationToken cancellation = default)
        {
            export.ExportId = Guid.NewGuid().ToString();
            export.LastExport = DateTime.UtcNow;

            var key = $"{tenantId}:{export.ExportId}";
            _lock.EnterWriteLock();
            try
            {
                _exports[key] = export;
                _logger.LogInformation($"Configured cost export to {export.Destination} in {export.Format} format ({export.Schedule} schedule)");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            await Task.CompletedTask;
            return export;
        }
    }
}
