// =============================================================================
// Cost Management Engine
// FinOps and Cloud Cost Optimization Engine
// Based on: OpenCost, Kubecost, FOCUS Spec, FinOps Foundation
// Research: https://www.opencost.io, https://www.finops.org
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.PlatformEngineering
{
    #region Enums

    /// <summary>
    /// Cloud provider types
    /// </summary>
    public enum CloudProvider
    {
        AWS,
        Azure,
        GCP,
        OnPremise,
        Hybrid,
        Multi
    }

    /// <summary>
    /// Cost allocation method
    /// </summary>
    public enum CostAllocationMethod
    {
        Direct,          // Direct cost assignment
        Proportional,    // Based on resource usage
        Even,            // Split evenly across consumers
        Custom           // Custom allocation rules
    }

    /// <summary>
    /// Resource cost category (FOCUS spec aligned)
    /// </summary>
    public enum CostCategory
    {
        Compute,
        Storage,
        Network,
        Database,
        Analytics,
        MachineLearning,
        Serverless,
        Containers,
        Security,
        Management,
        Support,
        Other
    }

    /// <summary>
    /// Pricing model type
    /// </summary>
    public enum PricingModel
    {
        OnDemand,
        Reserved,
        Spot,
        Savings,
        Committed,
        Enterprise
    }

    /// <summary>
    /// Budget alert severity
    /// </summary>
    public enum BudgetAlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// Anomaly detection status
    /// </summary>
    public enum AnomalyStatus
    {
        Detected,
        Investigating,
        Resolved,
        FalsePositive,
        Acknowledged
    }

    /// <summary>
    /// Optimization recommendation type
    /// </summary>
    public enum OptimizationType
    {
        Rightsizing,
        ReservedInstance,
        SavingsPlan,
        SpotInstance,
        IdleResource,
        UnusedResource,
        StorageOptimization,
        NetworkOptimization,
        LicenseOptimization,
        ArchitectureChange
    }

    /// <summary>
    /// Recommendation priority
    /// </summary>
    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Cost allocation record
    /// </summary>
    public class CostAllocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Time window for this allocation
        /// </summary>
        public CostTimeWindow TimeWindow { get; set; } = new();

        /// <summary>
        /// Resource identifier
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>
        /// Cloud provider
        /// </summary>
        public CloudProvider Provider { get; set; }

        /// <summary>
        /// Cost category (FOCUS aligned)
        /// </summary>
        public CostCategory Category { get; set; }

        /// <summary>
        /// Allocation dimensions (team, project, environment, etc.)
        /// </summary>
        public Dictionary<string, string> AllocationDimensions { get; set; } = new();

        /// <summary>
        /// Cost breakdown
        /// </summary>
        public CostBreakdown Costs { get; set; } = new();

        /// <summary>
        /// Usage metrics
        /// </summary>
        public Dictionary<string, double> UsageMetrics { get; set; } = new();

        /// <summary>
        /// Pricing model used
        /// </summary>
        public PricingModel PricingModel { get; set; }

        /// <summary>
        /// Kubernetes allocation details (if applicable)
        /// </summary>
        public KubernetesAllocation? KubernetesDetails { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cost time window
    /// </summary>
    public class CostTimeWindow
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Granularity { get; set; } = "hourly"; // hourly, daily, weekly, monthly
    }

    /// <summary>
    /// Cost breakdown structure
    /// </summary>
    public class CostBreakdown
    {
        /// <summary>
        /// Total cost in USD
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// CPU cost component
        /// </summary>
        public decimal CpuCost { get; set; }

        /// <summary>
        /// Memory cost component
        /// </summary>
        public decimal MemoryCost { get; set; }

        /// <summary>
        /// Storage cost component
        /// </summary>
        public decimal StorageCost { get; set; }

        /// <summary>
        /// Network cost component
        /// </summary>
        public decimal NetworkCost { get; set; }

        /// <summary>
        /// GPU cost component
        /// </summary>
        public decimal GpuCost { get; set; }

        /// <summary>
        /// Shared/overhead costs
        /// </summary>
        public decimal SharedCost { get; set; }

        /// <summary>
        /// External costs (licenses, support, etc.)
        /// </summary>
        public decimal ExternalCost { get; set; }

        /// <summary>
        /// Discount applied
        /// </summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>
        /// Effective cost after discounts
        /// </summary>
        public decimal EffectiveCost => TotalCost - DiscountAmount;

        /// <summary>
        /// Currency code
        /// </summary>
        public string Currency { get; set; } = "USD";
    }

    /// <summary>
    /// Kubernetes-specific allocation details (OpenCost model)
    /// </summary>
    public class KubernetesAllocation
    {
        public string ClusterName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string ControllerKind { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string Pod { get; set; } = string.Empty;
        public string Container { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Labels for allocation grouping
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Resource requests
        /// </summary>
        public ResourceMetrics Requests { get; set; } = new();

        /// <summary>
        /// Resource usage (actual)
        /// </summary>
        public ResourceMetrics Usage { get; set; } = new();

        /// <summary>
        /// Efficiency metrics
        /// </summary>
        public EfficiencyMetrics Efficiency { get; set; } = new();
    }

    /// <summary>
    /// Resource metrics
    /// </summary>
    public class ResourceMetrics
    {
        public double CpuCores { get; set; }
        public double MemoryGiB { get; set; }
        public double StorageGiB { get; set; }
        public int GpuCount { get; set; }
    }

    /// <summary>
    /// Efficiency metrics
    /// </summary>
    public class EfficiencyMetrics
    {
        /// <summary>
        /// CPU utilization (0-1)
        /// </summary>
        public double CpuEfficiency { get; set; }

        /// <summary>
        /// Memory utilization (0-1)
        /// </summary>
        public double MemoryEfficiency { get; set; }

        /// <summary>
        /// Overall efficiency score (0-1)
        /// </summary>
        public double TotalEfficiency { get; set; }

        /// <summary>
        /// Idle cost (wasted resources)
        /// </summary>
        public decimal IdleCost { get; set; }
    }

    #endregion

    #region Budget Types

    /// <summary>
    /// Budget definition
    /// </summary>
    public class Budget
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Budget amount in USD
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Budget period
        /// </summary>
        public BudgetPeriod Period { get; set; } = new();

        /// <summary>
        /// Scope filters (team, project, environment, etc.)
        /// </summary>
        public Dictionary<string, string> Scope { get; set; } = new();

        /// <summary>
        /// Alert thresholds
        /// </summary>
        public List<BudgetThreshold> Thresholds { get; set; } = new();

        /// <summary>
        /// Notification channels
        /// </summary>
        public List<NotificationChannel> NotificationChannels { get; set; } = new();

        /// <summary>
        /// Current spend tracking
        /// </summary>
        public BudgetSpend CurrentSpend { get; set; } = new();

        /// <summary>
        /// Forecast to end of period
        /// </summary>
        public BudgetForecast? Forecast { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Budget period configuration
    /// </summary>
    public class BudgetPeriod
    {
        public string Type { get; set; } = "monthly"; // monthly, quarterly, annual, custom
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Recurring { get; set; } = true;
    }

    /// <summary>
    /// Budget threshold configuration
    /// </summary>
    public class BudgetThreshold
    {
        /// <summary>
        /// Percentage of budget (0-100)
        /// </summary>
        public int Percentage { get; set; }

        /// <summary>
        /// Alert severity at this threshold
        /// </summary>
        public BudgetAlertSeverity Severity { get; set; }

        /// <summary>
        /// Whether this threshold has been breached
        /// </summary>
        public bool Breached { get; set; }

        /// <summary>
        /// Time when threshold was breached
        /// </summary>
        public DateTime? BreachedAt { get; set; }
    }

    /// <summary>
    /// Notification channel configuration
    /// </summary>
    public class NotificationChannel
    {
        public string Type { get; set; } = string.Empty; // email, slack, pagerduty, webhook
        public string Target { get; set; } = string.Empty;
        public Dictionary<string, string> Config { get; set; } = new();
    }

    /// <summary>
    /// Current budget spend tracking
    /// </summary>
    public class BudgetSpend
    {
        public decimal ActualSpend { get; set; }
        public decimal PercentUsed { get; set; }
        public decimal Remaining { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Budget forecast
    /// </summary>
    public class BudgetForecast
    {
        public decimal PredictedTotal { get; set; }
        public decimal PredictedOverage { get; set; }
        public int DaysUntilBreach { get; set; }
        public double ConfidenceLevel { get; set; }
        public string ForecastMethod { get; set; } = "linear";
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Budget alert
    /// </summary>
    public class BudgetAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BudgetId { get; set; } = string.Empty;
        public string BudgetName { get; set; } = string.Empty;
        public BudgetAlertSeverity Severity { get; set; }
        public int ThresholdPercent { get; set; }
        public decimal ActualPercent { get; set; }
        public decimal ActualSpend { get; set; }
        public decimal BudgetAmount { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool Acknowledged { get; set; }
    }

    #endregion

    #region Anomaly Types

    /// <summary>
    /// Cost anomaly detection result
    /// </summary>
    public class CostAnomaly
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Affected resource or allocation dimension
        /// </summary>
        public string AffectedEntity { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Anomaly detection details
        /// </summary>
        public AnomalyDetection Detection { get; set; } = new();

        /// <summary>
        /// Status
        /// </summary>
        public AnomalyStatus Status { get; set; } = AnomalyStatus.Detected;

        /// <summary>
        /// Impact assessment
        /// </summary>
        public AnomalyImpact Impact { get; set; } = new();

        /// <summary>
        /// Root cause analysis
        /// </summary>
        public List<string> PossibleCauses { get; set; } = new();

        /// <summary>
        /// Recommended actions
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public string? Resolution { get; set; }
    }

    /// <summary>
    /// Anomaly detection details
    /// </summary>
    public class AnomalyDetection
    {
        /// <summary>
        /// Detection method used
        /// </summary>
        public string Method { get; set; } = "statistical"; // statistical, ml, threshold

        /// <summary>
        /// Expected value based on historical data
        /// </summary>
        public decimal ExpectedValue { get; set; }

        /// <summary>
        /// Actual observed value
        /// </summary>
        public decimal ActualValue { get; set; }

        /// <summary>
        /// Deviation percentage
        /// </summary>
        public double DeviationPercent { get; set; }

        /// <summary>
        /// Confidence score (0-1)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Standard deviation from mean
        /// </summary>
        public double StandardDeviations { get; set; }
    }

    /// <summary>
    /// Anomaly impact assessment
    /// </summary>
    public class AnomalyImpact
    {
        /// <summary>
        /// Estimated additional cost
        /// </summary>
        public decimal EstimatedImpact { get; set; }

        /// <summary>
        /// Projected monthly impact
        /// </summary>
        public decimal MonthlyImpact { get; set; }

        /// <summary>
        /// Severity classification
        /// </summary>
        public BudgetAlertSeverity Severity { get; set; }
    }

    #endregion

    #region Optimization Types

    /// <summary>
    /// Cost optimization recommendation
    /// </summary>
    public class CostRecommendation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Recommendation type
        /// </summary>
        public OptimizationType Type { get; set; }

        /// <summary>
        /// Priority level
        /// </summary>
        public RecommendationPriority Priority { get; set; }

        /// <summary>
        /// Affected resource(s)
        /// </summary>
        public List<string> AffectedResources { get; set; } = new();

        /// <summary>
        /// Current state
        /// </summary>
        public string CurrentState { get; set; } = string.Empty;

        /// <summary>
        /// Recommended state
        /// </summary>
        public string RecommendedState { get; set; } = string.Empty;

        /// <summary>
        /// Savings estimate
        /// </summary>
        public SavingsEstimate Savings { get; set; } = new();

        /// <summary>
        /// Implementation details
        /// </summary>
        public ImplementationGuide Implementation { get; set; } = new();

        /// <summary>
        /// Risk assessment
        /// </summary>
        public RiskAssessment Risk { get; set; } = new();

        /// <summary>
        /// Status tracking
        /// </summary>
        public RecommendationStatus Status { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Savings estimate
    /// </summary>
    public class SavingsEstimate
    {
        public decimal MonthlySavings { get; set; }
        public decimal AnnualSavings { get; set; }
        public decimal PercentageSavings { get; set; }
        public double ConfidenceLevel { get; set; }
    }

    /// <summary>
    /// Implementation guide
    /// </summary>
    public class ImplementationGuide
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new();
        public string Complexity { get; set; } = "low"; // low, medium, high
        public int EstimatedMinutes { get; set; }
        public bool AutomationAvailable { get; set; }
        public string? AutomationScript { get; set; }
    }

    /// <summary>
    /// Risk assessment
    /// </summary>
    public class RiskAssessment
    {
        public string Level { get; set; } = "low"; // low, medium, high
        public List<string> Considerations { get; set; } = new();
        public List<string> Mitigations { get; set; } = new();
        public bool RequiresDowntime { get; set; }
    }

    /// <summary>
    /// Recommendation status
    /// </summary>
    public class RecommendationStatus
    {
        public string State { get; set; } = "new"; // new, acknowledged, in_progress, implemented, dismissed
        public string? DismissReason { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? ImplementedAt { get; set; }
        public decimal? ActualSavings { get; set; }
    }

    #endregion

    #region Report Types

    /// <summary>
    /// Cost report
    /// </summary>
    public class CostReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Report time range
        /// </summary>
        public CostTimeWindow TimeRange { get; set; } = new();

        /// <summary>
        /// Total costs
        /// </summary>
        public CostBreakdown TotalCosts { get; set; } = new();

        /// <summary>
        /// Cost by dimension
        /// </summary>
        public Dictionary<string, Dictionary<string, decimal>> CostsByDimension { get; set; } = new();

        /// <summary>
        /// Cost trend data
        /// </summary>
        public List<CostTrendPoint> Trend { get; set; } = new();

        /// <summary>
        /// Top cost drivers
        /// </summary>
        public List<CostDriver> TopCostDrivers { get; set; } = new();

        /// <summary>
        /// Efficiency summary
        /// </summary>
        public EfficiencySummary Efficiency { get; set; } = new();

        /// <summary>
        /// Active recommendations summary
        /// </summary>
        public RecommendationsSummary Recommendations { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cost trend data point
    /// </summary>
    public class CostTrendPoint
    {
        public DateTime Date { get; set; }
        public decimal Cost { get; set; }
        public decimal? Forecast { get; set; }
    }

    /// <summary>
    /// Cost driver analysis
    /// </summary>
    public class CostDriver
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal PercentOfTotal { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
    }

    /// <summary>
    /// Efficiency summary
    /// </summary>
    public class EfficiencySummary
    {
        public double OverallEfficiency { get; set; }
        public decimal TotalIdleCost { get; set; }
        public decimal PotentialSavings { get; set; }
        public int OptimizableResources { get; set; }
    }

    /// <summary>
    /// Recommendations summary
    /// </summary>
    public class RecommendationsSummary
    {
        public int TotalCount { get; set; }
        public decimal TotalPotentialSavings { get; set; }
        public Dictionary<OptimizationType, int> ByType { get; set; } = new();
        public Dictionary<RecommendationPriority, int> ByPriority { get; set; } = new();
    }

    #endregion

    #region Showback/Chargeback Types

    /// <summary>
    /// Chargeback configuration
    /// </summary>
    public class ChargebackConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Allocation rules
        /// </summary>
        public List<AllocationRule> Rules { get; set; } = new();

        /// <summary>
        /// Shared cost distribution method
        /// </summary>
        public SharedCostConfig SharedCosts { get; set; } = new();

        /// <summary>
        /// Markup/discount configuration
        /// </summary>
        public PricingAdjustments Adjustments { get; set; } = new();

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cost allocation rule
    /// </summary>
    public class AllocationRule
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }

        /// <summary>
        /// Matching criteria
        /// </summary>
        public Dictionary<string, string> Criteria { get; set; } = new();

        /// <summary>
        /// Target allocation dimension
        /// </summary>
        public string TargetDimension { get; set; } = string.Empty;
        public string TargetValue { get; set; } = string.Empty;

        /// <summary>
        /// Allocation method
        /// </summary>
        public CostAllocationMethod Method { get; set; }

        /// <summary>
        /// Custom allocation weights (if Method is Custom)
        /// </summary>
        public Dictionary<string, double>? CustomWeights { get; set; }
    }

    /// <summary>
    /// Shared cost configuration
    /// </summary>
    public class SharedCostConfig
    {
        /// <summary>
        /// Distribution method for shared costs
        /// </summary>
        public CostAllocationMethod Method { get; set; } = CostAllocationMethod.Proportional;

        /// <summary>
        /// Shared cost pools
        /// </summary>
        public List<SharedCostPool> Pools { get; set; } = new();
    }

    /// <summary>
    /// Shared cost pool
    /// </summary>
    public class SharedCostPool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Resources included in this pool
        /// </summary>
        public List<string> IncludedResources { get; set; } = new();

        /// <summary>
        /// Distribution targets
        /// </summary>
        public Dictionary<string, double> DistributionWeights { get; set; } = new();
    }

    /// <summary>
    /// Pricing adjustments
    /// </summary>
    public class PricingAdjustments
    {
        /// <summary>
        /// Global markup percentage
        /// </summary>
        public double MarkupPercent { get; set; }

        /// <summary>
        /// Category-specific adjustments
        /// </summary>
        public Dictionary<CostCategory, double> CategoryAdjustments { get; set; } = new();

        /// <summary>
        /// Team/department-specific adjustments
        /// </summary>
        public Dictionary<string, double> EntityAdjustments { get; set; } = new();
    }

    /// <summary>
    /// Chargeback statement
    /// </summary>
    public class ChargebackStatement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Statement period
        /// </summary>
        public CostTimeWindow Period { get; set; } = new();

        /// <summary>
        /// Line items
        /// </summary>
        public List<ChargebackLineItem> LineItems { get; set; } = new();

        /// <summary>
        /// Totals
        /// </summary>
        public ChargebackTotals Totals { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Chargeback line item
    /// </summary>
    public class ChargebackLineItem
    {
        public string Description { get; set; } = string.Empty;
        public CostCategory Category { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public decimal BaseCost { get; set; }
        public decimal SharedCost { get; set; }
        public decimal Adjustments { get; set; }
        public decimal FinalCost { get; set; }
    }

    /// <summary>
    /// Chargeback totals
    /// </summary>
    public class ChargebackTotals
    {
        public decimal DirectCosts { get; set; }
        public decimal SharedCosts { get; set; }
        public decimal Adjustments { get; set; }
        public decimal GrandTotal { get; set; }
    }

    #endregion

    #region Interface

    /// <summary>
    /// Cost Management Engine interface
    /// Provides FinOps capabilities for cloud cost optimization
    /// </summary>
    public interface ICostManagementEngine
    {
        #region Cost Allocation

        /// <summary>
        /// Record cost allocation
        /// </summary>
        Task<CostAllocation> RecordAllocationAsync(
            string tenantId,
            CostAllocation allocation,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get allocations for a time range
        /// </summary>
        Task<List<CostAllocation>> GetAllocationsAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            Dictionary<string, string>? filters = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Aggregate costs by dimension
        /// </summary>
        Task<Dictionary<string, decimal>> AggregateCostsAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            string dimension,
            CancellationToken cancellation = default);

        #endregion

        #region Budget Management

        /// <summary>
        /// Create budget
        /// </summary>
        Task<Budget> CreateBudgetAsync(
            string tenantId,
            Budget budget,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get budget by ID
        /// </summary>
        Task<Budget?> GetBudgetAsync(
            string tenantId,
            string budgetId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update budget
        /// </summary>
        Task<Budget> UpdateBudgetAsync(
            string tenantId,
            Budget budget,
            CancellationToken cancellation = default);

        /// <summary>
        /// List all budgets
        /// </summary>
        Task<List<Budget>> ListBudgetsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Check budget thresholds and generate alerts
        /// </summary>
        Task<List<BudgetAlert>> CheckBudgetsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Forecast budget usage
        /// </summary>
        Task<BudgetForecast> ForecastBudgetAsync(
            string tenantId,
            string budgetId,
            CancellationToken cancellation = default);

        #endregion

        #region Anomaly Detection

        /// <summary>
        /// Detect cost anomalies
        /// </summary>
        Task<List<CostAnomaly>> DetectAnomaliesAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get anomaly by ID
        /// </summary>
        Task<CostAnomaly?> GetAnomalyAsync(
            string tenantId,
            string anomalyId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update anomaly status
        /// </summary>
        Task<CostAnomaly> UpdateAnomalyStatusAsync(
            string tenantId,
            string anomalyId,
            AnomalyStatus status,
            string? resolution = null,
            CancellationToken cancellation = default);

        #endregion

        #region Optimization Recommendations

        /// <summary>
        /// Generate optimization recommendations
        /// </summary>
        Task<List<CostRecommendation>> GenerateRecommendationsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get recommendation by ID
        /// </summary>
        Task<CostRecommendation?> GetRecommendationAsync(
            string tenantId,
            string recommendationId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update recommendation status
        /// </summary>
        Task<CostRecommendation> UpdateRecommendationStatusAsync(
            string tenantId,
            string recommendationId,
            string state,
            string? reason = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Apply automated recommendation
        /// </summary>
        Task<bool> ApplyRecommendationAsync(
            string tenantId,
            string recommendationId,
            CancellationToken cancellation = default);

        #endregion

        #region Reporting

        /// <summary>
        /// Generate cost report
        /// </summary>
        Task<CostReport> GenerateReportAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get cost trend
        /// </summary>
        Task<List<CostTrendPoint>> GetCostTrendAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            string granularity = "daily",
            CancellationToken cancellation = default);

        #endregion

        #region Showback/Chargeback

        /// <summary>
        /// Configure chargeback rules
        /// </summary>
        Task<ChargebackConfig> ConfigureChargebackAsync(
            string tenantId,
            ChargebackConfig config,
            CancellationToken cancellation = default);

        /// <summary>
        /// Generate chargeback statements
        /// </summary>
        Task<List<ChargebackStatement>> GenerateChargebackStatementsAsync(
            string tenantId,
            CostTimeWindow period,
            string entityDimension,
            CancellationToken cancellation = default);

        #endregion
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Cost Management Engine implementation
    /// </summary>
    public class CostManagementEngine : ICostManagementEngine
    {
        private readonly ILogger<CostManagementEngine> _logger;
        private readonly Dictionary<string, List<CostAllocation>> _allocations = new();
        private readonly Dictionary<string, Dictionary<string, Budget>> _budgets = new();
        private readonly Dictionary<string, Dictionary<string, CostAnomaly>> _anomalies = new();
        private readonly Dictionary<string, Dictionary<string, CostRecommendation>> _recommendations = new();
        private readonly Dictionary<string, ChargebackConfig> _chargebackConfigs = new();

        // Cost rates (simplified - real implementation would pull from cloud APIs)
        private static readonly Dictionary<CloudProvider, Dictionary<string, decimal>> _costRates = new()
        {
            [CloudProvider.AWS] = new()
            {
                ["cpu_per_hour"] = 0.048m,
                ["memory_gb_per_hour"] = 0.006m,
                ["storage_gb_per_month"] = 0.10m,
                ["network_gb"] = 0.09m,
                ["gpu_per_hour"] = 1.00m
            },
            [CloudProvider.Azure] = new()
            {
                ["cpu_per_hour"] = 0.05m,
                ["memory_gb_per_hour"] = 0.0065m,
                ["storage_gb_per_month"] = 0.12m,
                ["network_gb"] = 0.087m,
                ["gpu_per_hour"] = 1.10m
            },
            [CloudProvider.GCP] = new()
            {
                ["cpu_per_hour"] = 0.045m,
                ["memory_gb_per_hour"] = 0.0055m,
                ["storage_gb_per_month"] = 0.08m,
                ["network_gb"] = 0.085m,
                ["gpu_per_hour"] = 0.95m
            }
        };

        public CostManagementEngine(ILogger<CostManagementEngine> logger)
        {
            _logger = logger;
        }

        #region Cost Allocation

        public Task<CostAllocation> RecordAllocationAsync(
            string tenantId,
            CostAllocation allocation,
            CancellationToken cancellation = default)
        {
            if (!_allocations.ContainsKey(tenantId))
                _allocations[tenantId] = new();

            allocation.TenantId = tenantId;
            allocation.CreatedAt = DateTime.UtcNow;

            // Calculate costs if not provided
            if (allocation.Costs.TotalCost == 0 && allocation.KubernetesDetails != null)
            {
                allocation.Costs = CalculateKubernetesCosts(allocation);
            }

            _allocations[tenantId].Add(allocation);

            _logger.LogInformation(
                "Recorded cost allocation {Id} for resource {Resource}: ${Cost:F2}",
                allocation.Id, allocation.ResourceName, allocation.Costs.TotalCost);

            return Task.FromResult(allocation);
        }

        private CostBreakdown CalculateKubernetesCosts(CostAllocation allocation)
        {
            var k8s = allocation.KubernetesDetails!;
            var rates = _costRates[allocation.Provider];
            var hours = (allocation.TimeWindow.End - allocation.TimeWindow.Start).TotalHours;

            var cpuCost = (decimal)(k8s.Usage.CpuCores * hours) * rates["cpu_per_hour"];
            var memoryCost = (decimal)(k8s.Usage.MemoryGiB * hours) * rates["memory_gb_per_hour"];
            var storageCost = (decimal)k8s.Usage.StorageGiB * rates["storage_gb_per_month"] / 720m * (decimal)hours;
            var gpuCost = (decimal)(k8s.Usage.GpuCount * hours) * rates["gpu_per_hour"];

            // Calculate idle costs
            var requestedCpuCost = (decimal)(k8s.Requests.CpuCores * hours) * rates["cpu_per_hour"];
            var requestedMemoryCost = (decimal)(k8s.Requests.MemoryGiB * hours) * rates["memory_gb_per_hour"];
            var idleCost = (requestedCpuCost - cpuCost) + (requestedMemoryCost - memoryCost);

            k8s.Efficiency = new EfficiencyMetrics
            {
                CpuEfficiency = k8s.Requests.CpuCores > 0 ? k8s.Usage.CpuCores / k8s.Requests.CpuCores : 0,
                MemoryEfficiency = k8s.Requests.MemoryGiB > 0 ? k8s.Usage.MemoryGiB / k8s.Requests.MemoryGiB : 0,
                IdleCost = idleCost
            };
            k8s.Efficiency.TotalEfficiency = (k8s.Efficiency.CpuEfficiency + k8s.Efficiency.MemoryEfficiency) / 2;

            return new CostBreakdown
            {
                CpuCost = cpuCost,
                MemoryCost = memoryCost,
                StorageCost = storageCost,
                GpuCost = gpuCost,
                TotalCost = cpuCost + memoryCost + storageCost + gpuCost
            };
        }

        public Task<List<CostAllocation>> GetAllocationsAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            Dictionary<string, string>? filters = null,
            CancellationToken cancellation = default)
        {
            if (!_allocations.TryGetValue(tenantId, out var allocations))
                return Task.FromResult(new List<CostAllocation>());

            var query = allocations.Where(a =>
                a.TimeWindow.Start >= timeWindow.Start &&
                a.TimeWindow.End <= timeWindow.End);

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    query = query.Where(a =>
                        a.AllocationDimensions.TryGetValue(filter.Key, out var value) &&
                        value == filter.Value);
                }
            }

            return Task.FromResult(query.ToList());
        }

        public async Task<Dictionary<string, decimal>> AggregateCostsAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            string dimension,
            CancellationToken cancellation = default)
        {
            var allocations = await GetAllocationsAsync(tenantId, timeWindow, null, cancellation);

            return allocations
                .Where(a => a.AllocationDimensions.ContainsKey(dimension))
                .GroupBy(a => a.AllocationDimensions[dimension])
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a => a.Costs.TotalCost));
        }

        #endregion

        #region Budget Management

        public Task<Budget> CreateBudgetAsync(
            string tenantId,
            Budget budget,
            CancellationToken cancellation = default)
        {
            if (!_budgets.ContainsKey(tenantId))
                _budgets[tenantId] = new();

            budget.TenantId = tenantId;
            budget.CreatedAt = DateTime.UtcNow;
            budget.CurrentSpend = new BudgetSpend();

            // Set default thresholds if not provided
            if (!budget.Thresholds.Any())
            {
                budget.Thresholds = new List<BudgetThreshold>
                {
                    new() { Percentage = 50, Severity = BudgetAlertSeverity.Info },
                    new() { Percentage = 80, Severity = BudgetAlertSeverity.Warning },
                    new() { Percentage = 100, Severity = BudgetAlertSeverity.Critical }
                };
            }

            _budgets[tenantId][budget.Id] = budget;

            _logger.LogInformation(
                "Created budget {Id} '{Name}' for ${Amount:F2}",
                budget.Id, budget.Name, budget.Amount);

            return Task.FromResult(budget);
        }

        public Task<Budget?> GetBudgetAsync(
            string tenantId,
            string budgetId,
            CancellationToken cancellation = default)
        {
            if (_budgets.TryGetValue(tenantId, out var budgets) &&
                budgets.TryGetValue(budgetId, out var budget))
            {
                return Task.FromResult<Budget?>(budget);
            }

            return Task.FromResult<Budget?>(null);
        }

        public Task<Budget> UpdateBudgetAsync(
            string tenantId,
            Budget budget,
            CancellationToken cancellation = default)
        {
            if (!_budgets.ContainsKey(tenantId) ||
                !_budgets[tenantId].ContainsKey(budget.Id))
            {
                throw new InvalidOperationException($"Budget {budget.Id} not found");
            }

            budget.UpdatedAt = DateTime.UtcNow;
            _budgets[tenantId][budget.Id] = budget;

            return Task.FromResult(budget);
        }

        public Task<List<Budget>> ListBudgetsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_budgets.TryGetValue(tenantId, out var budgets))
                return Task.FromResult(new List<Budget>());

            return Task.FromResult(budgets.Values.ToList());
        }

        public async Task<List<BudgetAlert>> CheckBudgetsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var alerts = new List<BudgetAlert>();
            var budgets = await ListBudgetsAsync(tenantId, cancellation);

            foreach (var budget in budgets.Where(b => b.IsActive))
            {
                // Get current spend for budget period
                var allocations = await GetAllocationsAsync(
                    tenantId,
                    new CostTimeWindow
                    {
                        Start = budget.Period.StartDate,
                        End = DateTime.UtcNow
                    },
                    budget.Scope,
                    cancellation);

                var currentSpend = allocations.Sum(a => a.Costs.TotalCost);
                var percentUsed = budget.Amount > 0 ? (currentSpend / budget.Amount) * 100 : 0;

                budget.CurrentSpend = new BudgetSpend
                {
                    ActualSpend = currentSpend,
                    PercentUsed = percentUsed,
                    Remaining = budget.Amount - currentSpend,
                    LastUpdated = DateTime.UtcNow
                };

                // Check thresholds
                foreach (var threshold in budget.Thresholds.OrderByDescending(t => t.Percentage))
                {
                    if (percentUsed >= threshold.Percentage && !threshold.Breached)
                    {
                        threshold.Breached = true;
                        threshold.BreachedAt = DateTime.UtcNow;

                        alerts.Add(new BudgetAlert
                        {
                            BudgetId = budget.Id,
                            BudgetName = budget.Name,
                            Severity = threshold.Severity,
                            ThresholdPercent = threshold.Percentage,
                            ActualPercent = percentUsed,
                            ActualSpend = currentSpend,
                            BudgetAmount = budget.Amount,
                            Message = $"Budget '{budget.Name}' has reached {percentUsed:F1}% ({currentSpend:C2} of {budget.Amount:C2})"
                        });
                    }
                }

                // Generate forecast
                budget.Forecast = await ForecastBudgetAsync(tenantId, budget.Id, cancellation);
            }

            _logger.LogInformation(
                "Checked {Count} budgets for tenant {TenantId}, generated {AlertCount} alerts",
                budgets.Count, tenantId, alerts.Count);

            return alerts;
        }

        public Task<BudgetForecast> ForecastBudgetAsync(
            string tenantId,
            string budgetId,
            CancellationToken cancellation = default)
        {
            if (!_budgets.TryGetValue(tenantId, out var budgets) ||
                !budgets.TryGetValue(budgetId, out var budget))
            {
                throw new InvalidOperationException($"Budget {budgetId} not found");
            }

            var daysElapsed = (DateTime.UtcNow - budget.Period.StartDate).TotalDays;
            var totalDays = (budget.Period.EndDate - budget.Period.StartDate).TotalDays;
            var daysRemaining = totalDays - daysElapsed;

            if (daysElapsed <= 0)
            {
                return Task.FromResult(new BudgetForecast
                {
                    PredictedTotal = 0,
                    GeneratedAt = DateTime.UtcNow
                });
            }

            var dailyRate = budget.CurrentSpend.ActualSpend / (decimal)daysElapsed;
            var predictedTotal = dailyRate * (decimal)totalDays;
            var predictedOverage = Math.Max(0, predictedTotal - budget.Amount);

            var daysUntilBreach = dailyRate > 0
                ? (int)((budget.Amount - budget.CurrentSpend.ActualSpend) / dailyRate)
                : int.MaxValue;

            return Task.FromResult(new BudgetForecast
            {
                PredictedTotal = predictedTotal,
                PredictedOverage = predictedOverage,
                DaysUntilBreach = Math.Max(0, Math.Min(daysUntilBreach, (int)daysRemaining)),
                ConfidenceLevel = Math.Min(0.95, 0.5 + (daysElapsed / totalDays) * 0.45),
                ForecastMethod = "linear",
                GeneratedAt = DateTime.UtcNow
            });
        }

        #endregion

        #region Anomaly Detection

        public async Task<List<CostAnomaly>> DetectAnomaliesAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            CancellationToken cancellation = default)
        {
            var anomalies = new List<CostAnomaly>();

            // Get current period allocations
            var currentAllocations = await GetAllocationsAsync(tenantId, timeWindow, null, cancellation);

            // Get historical allocations (previous 30 days for baseline)
            var historicalWindow = new CostTimeWindow
            {
                Start = timeWindow.Start.AddDays(-30),
                End = timeWindow.Start
            };
            var historicalAllocations = await GetAllocationsAsync(tenantId, historicalWindow, null, cancellation);

            // Group by resource and compare
            var currentByResource = currentAllocations
                .GroupBy(a => a.ResourceId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Costs.TotalCost));

            var historicalByResource = historicalAllocations
                .GroupBy(a => a.ResourceId)
                .ToDictionary(g => g.Key, g => new
                {
                    Mean = g.Average(a => (double)a.Costs.TotalCost),
                    StdDev = CalculateStdDev(g.Select(a => (double)a.Costs.TotalCost))
                });

            foreach (var (resourceId, currentCost) in currentByResource)
            {
                if (historicalByResource.TryGetValue(resourceId, out var historical))
                {
                    var deviation = ((double)currentCost - historical.Mean) / (historical.StdDev > 0 ? historical.StdDev : 1);
                    var deviationPercent = historical.Mean > 0 ? (((double)currentCost - historical.Mean) / historical.Mean) * 100 : 0;

                    // Flag if more than 2 standard deviations or > 50% increase
                    if (Math.Abs(deviation) > 2 || Math.Abs(deviationPercent) > 50)
                    {
                        var anomaly = new CostAnomaly
                        {
                            TenantId = tenantId,
                            AffectedEntity = resourceId,
                            EntityType = "resource",
                            Detection = new AnomalyDetection
                            {
                                Method = "statistical",
                                ExpectedValue = (decimal)historical.Mean,
                                ActualValue = currentCost,
                                DeviationPercent = deviationPercent,
                                ConfidenceScore = Math.Min(0.99, Math.Abs(deviation) / 5),
                                StandardDeviations = deviation
                            },
                            Impact = new AnomalyImpact
                            {
                                EstimatedImpact = currentCost - (decimal)historical.Mean,
                                MonthlyImpact = (currentCost - (decimal)historical.Mean) * 30,
                                Severity = Math.Abs(deviation) > 3 ? BudgetAlertSeverity.Critical :
                                          Math.Abs(deviation) > 2.5 ? BudgetAlertSeverity.Warning : BudgetAlertSeverity.Info
                            },
                            PossibleCauses = GeneratePossibleCauses(deviation > 0, deviationPercent),
                            RecommendedActions = GenerateRecommendedActions(deviation > 0)
                        };

                        anomalies.Add(anomaly);

                        // Store anomaly
                        if (!_anomalies.ContainsKey(tenantId))
                            _anomalies[tenantId] = new();
                        _anomalies[tenantId][anomaly.Id] = anomaly;
                    }
                }
            }

            _logger.LogInformation(
                "Detected {Count} cost anomalies for tenant {TenantId}",
                anomalies.Count, tenantId);

            return anomalies;
        }

        private double CalculateStdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0;

            var mean = list.Average();
            var sumSquares = list.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / (list.Count - 1));
        }

        private List<string> GeneratePossibleCauses(bool isIncrease, double deviationPercent)
        {
            if (isIncrease)
            {
                return new List<string>
                {
                    "Traffic spike or increased workload",
                    "Resource scaling event",
                    "New deployment or service launch",
                    "Inefficient code or query",
                    "Pricing tier change",
                    Math.Abs(deviationPercent) > 100 ? "Possible misconfiguration or resource leak" : "Seasonal variation"
                };
            }
            return new List<string>
            {
                "Service downtime or outage",
                "Successful optimization",
                "Reduced traffic or workload",
                "Resource decommissioning"
            };
        }

        private List<string> GenerateRecommendedActions(bool isIncrease)
        {
            if (isIncrease)
            {
                return new List<string>
                {
                    "Investigate recent changes and deployments",
                    "Check for resource leaks or orphaned resources",
                    "Review auto-scaling configurations",
                    "Analyze traffic patterns and usage",
                    "Consider cost allocation tags for better visibility"
                };
            }
            return new List<string>
            {
                "Verify service health and availability",
                "Confirm if reduction is expected",
                "Document optimization success if applicable"
            };
        }

        public Task<CostAnomaly?> GetAnomalyAsync(
            string tenantId,
            string anomalyId,
            CancellationToken cancellation = default)
        {
            if (_anomalies.TryGetValue(tenantId, out var anomalies) &&
                anomalies.TryGetValue(anomalyId, out var anomaly))
            {
                return Task.FromResult<CostAnomaly?>(anomaly);
            }

            return Task.FromResult<CostAnomaly?>(null);
        }

        public Task<CostAnomaly> UpdateAnomalyStatusAsync(
            string tenantId,
            string anomalyId,
            AnomalyStatus status,
            string? resolution = null,
            CancellationToken cancellation = default)
        {
            if (!_anomalies.TryGetValue(tenantId, out var anomalies) ||
                !anomalies.TryGetValue(anomalyId, out var anomaly))
            {
                throw new InvalidOperationException($"Anomaly {anomalyId} not found");
            }

            anomaly.Status = status;
            if (status == AnomalyStatus.Resolved)
            {
                anomaly.ResolvedAt = DateTime.UtcNow;
                anomaly.Resolution = resolution;
            }

            return Task.FromResult(anomaly);
        }

        #endregion

        #region Optimization Recommendations

        public async Task<List<CostRecommendation>> GenerateRecommendationsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var recommendations = new List<CostRecommendation>();

            // Get recent allocations
            var timeWindow = new CostTimeWindow
            {
                Start = DateTime.UtcNow.AddDays(-7),
                End = DateTime.UtcNow
            };
            var allocations = await GetAllocationsAsync(tenantId, timeWindow, null, cancellation);

            // Analyze for rightsizing opportunities
            foreach (var allocation in allocations.Where(a => a.KubernetesDetails != null))
            {
                var k8s = allocation.KubernetesDetails!;

                // Low CPU efficiency
                if (k8s.Efficiency.CpuEfficiency < 0.3 && k8s.Requests.CpuCores > 0.5)
                {
                    var recommendation = CreateRightsizingRecommendation(
                        tenantId, allocation, "CPU",
                        k8s.Requests.CpuCores, k8s.Usage.CpuCores,
                        allocation.Costs.CpuCost);
                    recommendations.Add(recommendation);
                }

                // Low memory efficiency
                if (k8s.Efficiency.MemoryEfficiency < 0.3 && k8s.Requests.MemoryGiB > 0.5)
                {
                    var recommendation = CreateRightsizingRecommendation(
                        tenantId, allocation, "Memory",
                        k8s.Requests.MemoryGiB, k8s.Usage.MemoryGiB,
                        allocation.Costs.MemoryCost);
                    recommendations.Add(recommendation);
                }

                // Idle resources (very low utilization)
                if (k8s.Efficiency.TotalEfficiency < 0.1)
                {
                    recommendations.Add(new CostRecommendation
                    {
                        TenantId = tenantId,
                        Type = OptimizationType.IdleResource,
                        Priority = RecommendationPriority.High,
                        AffectedResources = new List<string> { allocation.ResourceId },
                        CurrentState = $"Resource {allocation.ResourceName} has <10% utilization",
                        RecommendedState = "Consider scaling down or terminating if unused",
                        Savings = new SavingsEstimate
                        {
                            MonthlySavings = allocation.Costs.TotalCost * 30 * 0.9m,
                            AnnualSavings = allocation.Costs.TotalCost * 365 * 0.9m,
                            PercentageSavings = 90,
                            ConfidenceLevel = 0.85
                        },
                        Implementation = new ImplementationGuide
                        {
                            Summary = "Review and potentially terminate idle resource",
                            Steps = new List<string>
                            {
                                "Verify resource is not needed",
                                "Check for dependent services",
                                "Take backup if necessary",
                                "Scale down or terminate"
                            },
                            Complexity = "low",
                            EstimatedMinutes = 15
                        },
                        Risk = new RiskAssessment
                        {
                            Level = "medium",
                            Considerations = new List<string>
                            {
                                "Ensure no critical dependencies",
                                "Consider seasonal usage patterns"
                            }
                        }
                    });
                }
            }

            // Reserved instance recommendations for stable workloads
            var stableAllocations = allocations
                .GroupBy(a => a.ResourceId)
                .Where(g => g.Count() >= 7 && g.All(a => a.Costs.TotalCost > 0))
                .Select(g => g.First())
                .Where(a => a.PricingModel == PricingModel.OnDemand);

            foreach (var allocation in stableAllocations)
            {
                recommendations.Add(new CostRecommendation
                {
                    TenantId = tenantId,
                    Type = OptimizationType.ReservedInstance,
                    Priority = RecommendationPriority.Medium,
                    AffectedResources = new List<string> { allocation.ResourceId },
                    CurrentState = "On-demand pricing",
                    RecommendedState = "1-year reserved instance (no upfront)",
                    Savings = new SavingsEstimate
                    {
                        MonthlySavings = allocation.Costs.TotalCost * 30 * 0.3m,
                        AnnualSavings = allocation.Costs.TotalCost * 365 * 0.3m,
                        PercentageSavings = 30,
                        ConfidenceLevel = 0.9
                    },
                    Implementation = new ImplementationGuide
                    {
                        Summary = "Purchase reserved instance commitment",
                        Steps = new List<string>
                        {
                            "Verify workload stability",
                            "Select reservation term (1 or 3 year)",
                            "Choose payment option (no upfront, partial, all upfront)",
                            "Purchase reservation through cloud console"
                        },
                        Complexity = "low",
                        EstimatedMinutes = 30
                    },
                    Risk = new RiskAssessment
                    {
                        Level = "low",
                        Considerations = new List<string>
                        {
                            "Commitment period inflexibility",
                            "Instance type must remain suitable"
                        }
                    }
                });
            }

            // Store recommendations
            if (!_recommendations.ContainsKey(tenantId))
                _recommendations[tenantId] = new();

            foreach (var rec in recommendations)
            {
                _recommendations[tenantId][rec.Id] = rec;
            }

            _logger.LogInformation(
                "Generated {Count} cost optimization recommendations for tenant {TenantId}",
                recommendations.Count, tenantId);

            return recommendations;
        }

        private CostRecommendation CreateRightsizingRecommendation(
            string tenantId,
            CostAllocation allocation,
            string resourceType,
            double requested,
            double used,
            decimal currentCost)
        {
            var recommendedSize = Math.Max(used * 1.2, requested * 0.5); // 20% headroom or 50% reduction
            var savingsPercent = 1 - (recommendedSize / requested);

            return new CostRecommendation
            {
                TenantId = tenantId,
                Type = OptimizationType.Rightsizing,
                Priority = savingsPercent > 0.5 ? RecommendationPriority.High : RecommendationPriority.Medium,
                AffectedResources = new List<string> { allocation.ResourceId },
                CurrentState = $"{resourceType}: {requested:F2} requested, {used:F2} used ({(used / requested * 100):F0}% utilization)",
                RecommendedState = $"Reduce {resourceType} to {recommendedSize:F2}",
                Savings = new SavingsEstimate
                {
                    MonthlySavings = currentCost * 30 * (decimal)savingsPercent,
                    AnnualSavings = currentCost * 365 * (decimal)savingsPercent,
                    PercentageSavings = (decimal)savingsPercent * 100,
                    ConfidenceLevel = 0.8
                },
                Implementation = new ImplementationGuide
                {
                    Summary = $"Reduce {resourceType} resource requests",
                    Steps = new List<string>
                    {
                        $"Update deployment manifest with new {resourceType} requests",
                        "Apply changes during low-traffic window",
                        "Monitor performance metrics after change"
                    },
                    Complexity = "low",
                    EstimatedMinutes = 10,
                    AutomationAvailable = true
                },
                Risk = new RiskAssessment
                {
                    Level = "low",
                    Considerations = new List<string>
                    {
                        "Monitor for performance degradation",
                        "Consider traffic spikes"
                    },
                    Mitigations = new List<string>
                    {
                        "Enable HPA for automatic scaling",
                        "Set up alerts for resource pressure"
                    }
                }
            };
        }

        public Task<CostRecommendation?> GetRecommendationAsync(
            string tenantId,
            string recommendationId,
            CancellationToken cancellation = default)
        {
            if (_recommendations.TryGetValue(tenantId, out var recommendations) &&
                recommendations.TryGetValue(recommendationId, out var recommendation))
            {
                return Task.FromResult<CostRecommendation?>(recommendation);
            }

            return Task.FromResult<CostRecommendation?>(null);
        }

        public Task<CostRecommendation> UpdateRecommendationStatusAsync(
            string tenantId,
            string recommendationId,
            string state,
            string? reason = null,
            CancellationToken cancellation = default)
        {
            if (!_recommendations.TryGetValue(tenantId, out var recommendations) ||
                !recommendations.TryGetValue(recommendationId, out var recommendation))
            {
                throw new InvalidOperationException($"Recommendation {recommendationId} not found");
            }

            recommendation.Status.State = state;

            switch (state)
            {
                case "acknowledged":
                    recommendation.Status.AcknowledgedAt = DateTime.UtcNow;
                    break;
                case "implemented":
                    recommendation.Status.ImplementedAt = DateTime.UtcNow;
                    break;
                case "dismissed":
                    recommendation.Status.DismissReason = reason;
                    break;
            }

            return Task.FromResult(recommendation);
        }

        public Task<bool> ApplyRecommendationAsync(
            string tenantId,
            string recommendationId,
            CancellationToken cancellation = default)
        {
            // In real implementation, this would execute automation scripts
            _logger.LogInformation(
                "Applied recommendation {Id} for tenant {TenantId}",
                recommendationId, tenantId);

            return Task.FromResult(true);
        }

        #endregion

        #region Reporting

        public async Task<CostReport> GenerateReportAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            CancellationToken cancellation = default)
        {
            var allocations = await GetAllocationsAsync(tenantId, timeWindow, null, cancellation);

            var report = new CostReport
            {
                TenantId = tenantId,
                Name = $"Cost Report {timeWindow.Start:yyyy-MM-dd} to {timeWindow.End:yyyy-MM-dd}",
                TimeRange = timeWindow
            };

            // Calculate totals
            report.TotalCosts = new CostBreakdown
            {
                TotalCost = allocations.Sum(a => a.Costs.TotalCost),
                CpuCost = allocations.Sum(a => a.Costs.CpuCost),
                MemoryCost = allocations.Sum(a => a.Costs.MemoryCost),
                StorageCost = allocations.Sum(a => a.Costs.StorageCost),
                NetworkCost = allocations.Sum(a => a.Costs.NetworkCost),
                GpuCost = allocations.Sum(a => a.Costs.GpuCost)
            };

            // Cost by dimension
            report.CostsByDimension["team"] = await AggregateCostsAsync(tenantId, timeWindow, "team", cancellation);
            report.CostsByDimension["environment"] = await AggregateCostsAsync(tenantId, timeWindow, "environment", cancellation);
            report.CostsByDimension["project"] = await AggregateCostsAsync(tenantId, timeWindow, "project", cancellation);

            // Cost trend
            report.Trend = await GetCostTrendAsync(tenantId, timeWindow, "daily", cancellation);

            // Top cost drivers
            report.TopCostDrivers = allocations
                .GroupBy(a => a.ResourceName)
                .Select(g => new CostDriver
                {
                    Name = g.Key,
                    Type = g.First().ResourceType,
                    Cost = g.Sum(a => a.Costs.TotalCost),
                    PercentOfTotal = report.TotalCosts.TotalCost > 0
                        ? g.Sum(a => a.Costs.TotalCost) / report.TotalCosts.TotalCost * 100
                        : 0
                })
                .OrderByDescending(d => d.Cost)
                .Take(10)
                .ToList();

            // Efficiency summary
            var k8sAllocations = allocations.Where(a => a.KubernetesDetails != null).ToList();
            if (k8sAllocations.Any())
            {
                report.Efficiency = new EfficiencySummary
                {
                    OverallEfficiency = k8sAllocations.Average(a => a.KubernetesDetails!.Efficiency.TotalEfficiency),
                    TotalIdleCost = k8sAllocations.Sum(a => a.KubernetesDetails!.Efficiency.IdleCost),
                    PotentialSavings = k8sAllocations.Sum(a => a.KubernetesDetails!.Efficiency.IdleCost) * 0.7m,
                    OptimizableResources = k8sAllocations.Count(a => a.KubernetesDetails!.Efficiency.TotalEfficiency < 0.5)
                };
            }

            // Recommendations summary
            var recommendations = await GenerateRecommendationsAsync(tenantId, cancellation);
            report.Recommendations = new RecommendationsSummary
            {
                TotalCount = recommendations.Count,
                TotalPotentialSavings = recommendations.Sum(r => r.Savings.MonthlySavings),
                ByType = recommendations.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                ByPriority = recommendations.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count())
            };

            return report;
        }

        public async Task<List<CostTrendPoint>> GetCostTrendAsync(
            string tenantId,
            CostTimeWindow timeWindow,
            string granularity = "daily",
            CancellationToken cancellation = default)
        {
            var allocations = await GetAllocationsAsync(tenantId, timeWindow, null, cancellation);
            var trend = new List<CostTrendPoint>();

            var current = timeWindow.Start;
            var interval = granularity switch
            {
                "hourly" => TimeSpan.FromHours(1),
                "daily" => TimeSpan.FromDays(1),
                "weekly" => TimeSpan.FromDays(7),
                "monthly" => TimeSpan.FromDays(30),
                _ => TimeSpan.FromDays(1)
            };

            while (current < timeWindow.End)
            {
                var periodEnd = current + interval;
                var periodCost = allocations
                    .Where(a => a.TimeWindow.Start >= current && a.TimeWindow.End <= periodEnd)
                    .Sum(a => a.Costs.TotalCost);

                trend.Add(new CostTrendPoint
                {
                    Date = current,
                    Cost = periodCost
                });

                current = periodEnd;
            }

            // Add simple forecast for future dates
            if (trend.Count >= 3)
            {
                var avgGrowth = trend.Count > 1
                    ? (trend.Last().Cost - trend.First().Cost) / (trend.Count - 1)
                    : 0;

                var lastCost = trend.Last().Cost;
                for (int i = 1; i <= 7; i++)
                {
                    trend.Add(new CostTrendPoint
                    {
                        Date = timeWindow.End.AddDays(i * (interval.TotalDays)),
                        Forecast = lastCost + avgGrowth * i
                    });
                }
            }

            return trend;
        }

        #endregion

        #region Showback/Chargeback

        public Task<ChargebackConfig> ConfigureChargebackAsync(
            string tenantId,
            ChargebackConfig config,
            CancellationToken cancellation = default)
        {
            config.TenantId = tenantId;
            config.CreatedAt = DateTime.UtcNow;

            _chargebackConfigs[tenantId] = config;

            _logger.LogInformation(
                "Configured chargeback {Id} for tenant {TenantId}",
                config.Id, tenantId);

            return Task.FromResult(config);
        }

        public async Task<List<ChargebackStatement>> GenerateChargebackStatementsAsync(
            string tenantId,
            CostTimeWindow period,
            string entityDimension,
            CancellationToken cancellation = default)
        {
            var statements = new List<ChargebackStatement>();
            var allocations = await GetAllocationsAsync(tenantId, period, null, cancellation);

            // Group allocations by the specified dimension
            var groupedAllocations = allocations
                .Where(a => a.AllocationDimensions.ContainsKey(entityDimension))
                .GroupBy(a => a.AllocationDimensions[entityDimension]);

            // Get chargeback config
            _chargebackConfigs.TryGetValue(tenantId, out var config);

            foreach (var group in groupedAllocations)
            {
                var statement = new ChargebackStatement
                {
                    TenantId = tenantId,
                    Entity = group.Key,
                    EntityType = entityDimension,
                    Period = period
                };

                // Group by category for line items
                var byCategory = group.GroupBy(a => a.Category);
                foreach (var categoryGroup in byCategory)
                {
                    var baseCost = categoryGroup.Sum(a => a.Costs.TotalCost);
                    var adjustment = config != null && config.Adjustments.CategoryAdjustments.TryGetValue(categoryGroup.Key, out var adj)
                        ? baseCost * (decimal)adj / 100m
                        : 0;

                    statement.LineItems.Add(new ChargebackLineItem
                    {
                        Description = categoryGroup.Key.ToString(),
                        Category = categoryGroup.Key,
                        ResourceType = categoryGroup.First().ResourceType,
                        BaseCost = baseCost,
                        SharedCost = 0, // Would be calculated from shared cost pools
                        Adjustments = adjustment,
                        FinalCost = baseCost + adjustment
                    });
                }

                // Calculate totals
                statement.Totals = new ChargebackTotals
                {
                    DirectCosts = statement.LineItems.Sum(l => l.BaseCost),
                    SharedCosts = statement.LineItems.Sum(l => l.SharedCost),
                    Adjustments = statement.LineItems.Sum(l => l.Adjustments),
                    GrandTotal = statement.LineItems.Sum(l => l.FinalCost)
                };

                statements.Add(statement);
            }

            _logger.LogInformation(
                "Generated {Count} chargeback statements for tenant {TenantId}",
                statements.Count, tenantId);

            return statements;
        }

        #endregion
    }

    #endregion
}
