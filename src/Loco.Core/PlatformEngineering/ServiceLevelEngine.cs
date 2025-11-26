// =============================================================================
// Service Level Engine
// SLO/SLI/Error Budget Management Engine
// Based on: OpenSLO Spec, Google SRE Book, Sloth, Pyrra
// Research: https://openslo.com, https://sre.google/books/
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
    /// SLI type classification
    /// </summary>
    public enum SLIType
    {
        Availability,      // Service uptime
        Latency,           // Response time
        Throughput,        // Requests per second
        ErrorRate,         // Error percentage
        Saturation,        // Resource utilization
        Correctness,       // Data accuracy
        Freshness,         // Data recency
        Coverage,          // Feature coverage
        Quality,           // Custom quality metric
        Durability         // Data durability
    }

    /// <summary>
    /// SLI measurement method
    /// </summary>
    public enum SLIMeasurementMethod
    {
        RatioBased,        // Good events / total events
        ThresholdBased,    // Events meeting threshold
        WindowBased,       // Metrics within time window
        SyntheticProbe,    // Active monitoring probes
        RealUserMetrics,   // RUM data
        LogBased           // Log analysis
    }

    /// <summary>
    /// SLO window type
    /// </summary>
    public enum SLOWindowType
    {
        Rolling,           // Rolling window (e.g., last 30 days)
        Calendar           // Calendar-based (e.g., monthly)
    }

    /// <summary>
    /// Error budget policy action
    /// </summary>
    public enum BudgetPolicyAction
    {
        Alert,             // Send notification
        SlowDown,          // Reduce deployment velocity
        Freeze,            // Stop non-critical changes
        Escalate,          // Escalate to management
        AutoRemediate,     // Trigger automation
        PageOnCall         // Page on-call engineer
    }

    /// <summary>
    /// Alert severity
    /// </summary>
    public enum AlertSeverity
    {
        Page,              // Immediate action required
        Ticket,            // Create ticket for resolution
        Warning,           // Informational warning
        Info               // Informational only
    }

    /// <summary>
    /// SLO compliance status
    /// </summary>
    public enum SLOComplianceStatus
    {
        Met,               // Within SLO target
        AtRisk,            // Approaching budget exhaustion
        Breached,          // Budget exhausted
        Unknown            // Insufficient data
    }

    #endregion

    #region Core Types

    /// <summary>
    /// Service Level Indicator (SLI) specification
    /// </summary>
    public class ServiceLevelIndicator
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// SLI name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// SLI type
        /// </summary>
        public SLIType Type { get; set; }

        /// <summary>
        /// Measurement method
        /// </summary>
        public SLIMeasurementMethod Method { get; set; }

        /// <summary>
        /// Metric query specification
        /// </summary>
        public SLIMetricSpec MetricSpec { get; set; } = new();

        /// <summary>
        /// Threshold for threshold-based SLIs
        /// </summary>
        public SLIThreshold? Threshold { get; set; }

        /// <summary>
        /// Metadata labels
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// SLI metric specification
    /// </summary>
    public class SLIMetricSpec
    {
        /// <summary>
        /// Query for good events/measurements
        /// </summary>
        public string GoodQuery { get; set; } = string.Empty;

        /// <summary>
        /// Query for total events/measurements
        /// </summary>
        public string TotalQuery { get; set; } = string.Empty;

        /// <summary>
        /// Alternative: ratio query (good/total in single query)
        /// </summary>
        public string? RatioQuery { get; set; }

        /// <summary>
        /// Metric source (prometheus, datadog, newrelic, etc.)
        /// </summary>
        public string MetricSource { get; set; } = "prometheus";

        /// <summary>
        /// Additional query parameters
        /// </summary>
        public Dictionary<string, string> QueryParams { get; set; } = new();
    }

    /// <summary>
    /// SLI threshold configuration
    /// </summary>
    public class SLIThreshold
    {
        /// <summary>
        /// Threshold value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Comparison operator (lt, lte, gt, gte)
        /// </summary>
        public string Operator { get; set; } = "lt";

        /// <summary>
        /// Unit of measurement
        /// </summary>
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service Level Objective (SLO) specification
    /// </summary>
    public class ServiceLevelObjective
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// SLO name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Service this SLO applies to
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Associated SLI
        /// </summary>
        public string SLIId { get; set; } = string.Empty;

        /// <summary>
        /// Target objective (e.g., 99.9 for 99.9%)
        /// </summary>
        public double Target { get; set; }

        /// <summary>
        /// Time window configuration
        /// </summary>
        public SLOWindow Window { get; set; } = new();

        /// <summary>
        /// Error budget policies
        /// </summary>
        public List<ErrorBudgetPolicy> BudgetPolicies { get; set; } = new();

        /// <summary>
        /// Alert rules
        /// </summary>
        public List<SLOAlertRule> AlertRules { get; set; } = new();

        /// <summary>
        /// Metadata labels
        /// </summary>
        public Dictionary<string, string> Labels { get; set; } = new();

        /// <summary>
        /// Is this SLO active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Owner team
        /// </summary>
        public string OwnerTeam { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// SLO time window configuration
    /// </summary>
    public class SLOWindow
    {
        /// <summary>
        /// Window type (rolling or calendar)
        /// </summary>
        public SLOWindowType Type { get; set; } = SLOWindowType.Rolling;

        /// <summary>
        /// Window duration (e.g., 30 for 30-day rolling)
        /// </summary>
        public int Duration { get; set; } = 30;

        /// <summary>
        /// Duration unit (day, hour, month)
        /// </summary>
        public string Unit { get; set; } = "day";

        /// <summary>
        /// Calendar alignment (for calendar windows)
        /// </summary>
        public string? CalendarAlignment { get; set; }
    }

    /// <summary>
    /// Error budget policy
    /// </summary>
    public class ErrorBudgetPolicy
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Budget remaining threshold to trigger (0-100)
        /// </summary>
        public double TriggerThreshold { get; set; }

        /// <summary>
        /// Actions to take when triggered
        /// </summary>
        public List<BudgetPolicyAction> Actions { get; set; } = new();

        /// <summary>
        /// Notification channels
        /// </summary>
        public List<string> NotificationChannels { get; set; } = new();

        /// <summary>
        /// Auto-remediation script (if applicable)
        /// </summary>
        public string? RemediationScript { get; set; }
    }

    /// <summary>
    /// SLO alert rule (multi-window, multi-burn rate)
    /// </summary>
    public class SLOAlertRule
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Burn rate threshold
        /// </summary>
        public double BurnRate { get; set; }

        /// <summary>
        /// Short window for fast detection
        /// </summary>
        public TimeSpan ShortWindow { get; set; }

        /// <summary>
        /// Long window for sustained issues
        /// </summary>
        public TimeSpan LongWindow { get; set; }

        /// <summary>
        /// Percentage of error budget consumed to alert
        /// </summary>
        public double BudgetConsumedPercent { get; set; }

        /// <summary>
        /// Alert annotations
        /// </summary>
        public Dictionary<string, string> Annotations { get; set; } = new();
    }

    #endregion

    #region Error Budget Types

    /// <summary>
    /// Error budget calculation
    /// </summary>
    public class ErrorBudget
    {
        public string SLOId { get; set; } = string.Empty;
        public string SLOName { get; set; } = string.Empty;

        /// <summary>
        /// Time window for this budget
        /// </summary>
        public SLOWindow Window { get; set; } = new();

        /// <summary>
        /// Window start time
        /// </summary>
        public DateTime WindowStart { get; set; }

        /// <summary>
        /// Window end time
        /// </summary>
        public DateTime WindowEnd { get; set; }

        /// <summary>
        /// Total budget in minutes/events (depending on SLI type)
        /// </summary>
        public double TotalBudget { get; set; }

        /// <summary>
        /// Budget consumed
        /// </summary>
        public double ConsumedBudget { get; set; }

        /// <summary>
        /// Budget remaining
        /// </summary>
        public double RemainingBudget => TotalBudget - ConsumedBudget;

        /// <summary>
        /// Percentage remaining (0-100)
        /// </summary>
        public double RemainingPercent => TotalBudget > 0 ? (RemainingBudget / TotalBudget) * 100 : 0;

        /// <summary>
        /// Current burn rate (budget consumed per hour)
        /// </summary>
        public double CurrentBurnRate { get; set; }

        /// <summary>
        /// Average burn rate over the window
        /// </summary>
        public double AverageBurnRate { get; set; }

        /// <summary>
        /// Time until budget exhaustion at current burn rate
        /// </summary>
        public TimeSpan? TimeToExhaustion { get; set; }

        /// <summary>
        /// Budget status
        /// </summary>
        public SLOComplianceStatus Status { get; set; }

        /// <summary>
        /// Budget trend (positive = improving, negative = degrading)
        /// </summary>
        public double Trend { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Error budget burn down tracking
    /// </summary>
    public class BudgetBurnDown
    {
        public string SLOId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double RemainingPercent { get; set; }
        public double BurnRate { get; set; }
        public double CumulativeConsumed { get; set; }
    }

    /// <summary>
    /// Error budget consumption event
    /// </summary>
    public class BudgetConsumptionEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SLOId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

        /// <summary>
        /// Budget consumed by this event
        /// </summary>
        public double BudgetConsumed { get; set; }

        /// <summary>
        /// Cause/description
        /// </summary>
        public string Cause { get; set; } = string.Empty;

        /// <summary>
        /// Related incident ID
        /// </summary>
        public string? IncidentId { get; set; }

        /// <summary>
        /// Attribution (team/service/change)
        /// </summary>
        public Dictionary<string, string> Attribution { get; set; } = new();
    }

    #endregion

    #region Compliance Types

    /// <summary>
    /// SLO compliance report
    /// </summary>
    public class SLOComplianceReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Report period
        /// </summary>
        public ReportPeriod Period { get; set; } = new();

        /// <summary>
        /// Overall compliance summary
        /// </summary>
        public ComplianceSummary Summary { get; set; } = new();

        /// <summary>
        /// Individual SLO results
        /// </summary>
        public List<SLOComplianceResult> SLOResults { get; set; } = new();

        /// <summary>
        /// Top error budget consumers
        /// </summary>
        public List<BudgetConsumer> TopBudgetConsumers { get; set; } = new();

        /// <summary>
        /// Recommendations
        /// </summary>
        public List<SLORecommendation> Recommendations { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Report period
    /// </summary>
    public class ReportPeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; } = "monthly"; // weekly, monthly, quarterly
    }

    /// <summary>
    /// Compliance summary
    /// </summary>
    public class ComplianceSummary
    {
        public int TotalSLOs { get; set; }
        public int MetCount { get; set; }
        public int AtRiskCount { get; set; }
        public int BreachedCount { get; set; }
        public double OverallCompliancePercent { get; set; }
        public double AverageErrorBudgetRemaining { get; set; }
    }

    /// <summary>
    /// Individual SLO compliance result
    /// </summary>
    public class SLOComplianceResult
    {
        public string SLOId { get; set; } = string.Empty;
        public string SLOName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Target SLO percentage
        /// </summary>
        public double Target { get; set; }

        /// <summary>
        /// Actual achieved percentage
        /// </summary>
        public double Actual { get; set; }

        /// <summary>
        /// Compliance status
        /// </summary>
        public SLOComplianceStatus Status { get; set; }

        /// <summary>
        /// Error budget status
        /// </summary>
        public ErrorBudget Budget { get; set; } = new();

        /// <summary>
        /// Number of incidents affecting this SLO
        /// </summary>
        public int IncidentCount { get; set; }

        /// <summary>
        /// Total downtime
        /// </summary>
        public TimeSpan TotalDowntime { get; set; }
    }

    /// <summary>
    /// Budget consumer (team/service/change)
    /// </summary>
    public class BudgetConsumer
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // team, service, deployment
        public double BudgetConsumed { get; set; }
        public double PercentOfTotal { get; set; }
        public int EventCount { get; set; }
    }

    /// <summary>
    /// SLO recommendation
    /// </summary>
    public class SLORecommendation
    {
        public string SLOId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public List<string> Actions { get; set; } = new();
    }

    #endregion

    #region Alert Types

    /// <summary>
    /// SLO alert instance
    /// </summary>
    public class SLOAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SLOId { get; set; } = string.Empty;
        public string SLOName { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert status
        /// </summary>
        public string Status { get; set; } = "firing"; // firing, resolved

        /// <summary>
        /// Current burn rate
        /// </summary>
        public double BurnRate { get; set; }

        /// <summary>
        /// Error budget remaining when alert fired
        /// </summary>
        public double BudgetRemainingPercent { get; set; }

        /// <summary>
        /// Alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Alert annotations
        /// </summary>
        public Dictionary<string, string> Annotations { get; set; } = new();

        public DateTime FiredAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>
    /// Multi-window, multi-burn rate alert configuration
    /// Based on Google SRE workbook
    /// </summary>
    public class MultiWindowBurnRateConfig
    {
        /// <summary>
        /// SLO window in hours
        /// </summary>
        public int SLOWindowHours { get; set; } = 720; // 30 days

        /// <summary>
        /// Alert configurations
        /// </summary>
        public List<BurnRateAlert> Alerts { get; set; } = new()
        {
            // Page-level: 2% budget in 1 hour (burn rate 14.4x)
            new BurnRateAlert
            {
                Severity = AlertSeverity.Page,
                BurnRateMultiple = 14.4,
                ShortWindowMinutes = 5,
                LongWindowMinutes = 60,
                BudgetConsumedPercent = 2
            },
            // Page-level: 5% budget in 6 hours (burn rate 6x)
            new BurnRateAlert
            {
                Severity = AlertSeverity.Page,
                BurnRateMultiple = 6,
                ShortWindowMinutes = 30,
                LongWindowMinutes = 360,
                BudgetConsumedPercent = 5
            },
            // Ticket-level: 10% budget in 3 days (burn rate 1x)
            new BurnRateAlert
            {
                Severity = AlertSeverity.Ticket,
                BurnRateMultiple = 1,
                ShortWindowMinutes = 360,
                LongWindowMinutes = 4320,
                BudgetConsumedPercent = 10
            }
        };
    }

    /// <summary>
    /// Individual burn rate alert
    /// </summary>
    public class BurnRateAlert
    {
        public AlertSeverity Severity { get; set; }
        public double BurnRateMultiple { get; set; }
        public int ShortWindowMinutes { get; set; }
        public int LongWindowMinutes { get; set; }
        public double BudgetConsumedPercent { get; set; }
    }

    #endregion

    #region OpenSLO Types

    /// <summary>
    /// OpenSLO specification format (for export/import)
    /// </summary>
    public class OpenSLOSpec
    {
        public string ApiVersion { get; set; } = "openslo/v1";
        public string Kind { get; set; } = "SLO";
        public OpenSLOMetadata Metadata { get; set; } = new();
        public OpenSLOSpecification Spec { get; set; } = new();
    }

    public class OpenSLOMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Annotations { get; set; } = new();
    }

    public class OpenSLOSpecification
    {
        public string Description { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Indicator { get; set; } = string.Empty;
        public List<OpenSLOTarget> Objectives { get; set; } = new();
        public OpenSLOTimeWindow TimeWindow { get; set; } = new();
        public List<OpenSLOAlertPolicy> AlertPolicies { get; set; } = new();
    }

    public class OpenSLOTarget
    {
        public string DisplayName { get; set; } = string.Empty;
        public double Target { get; set; }
        public string? TargetPercent { get; set; }
        public string? Op { get; set; }
        public double? Value { get; set; }
    }

    public class OpenSLOTimeWindow
    {
        public string Duration { get; set; } = "30d";
        public bool IsRolling { get; set; } = true;
    }

    public class OpenSLOAlertPolicy
    {
        public string Name { get; set; } = string.Empty;
        public string? Condition { get; set; }
        public List<OpenSLOAlertCondition> Conditions { get; set; } = new();
        public List<OpenSLONotificationTarget> NotificationTargets { get; set; } = new();
    }

    public class OpenSLOAlertCondition
    {
        public string Kind { get; set; } = "burnrate";
        public double Threshold { get; set; }
        public string LookbackWindow { get; set; } = "1h";
    }

    public class OpenSLONotificationTarget
    {
        public string TargetRef { get; set; } = string.Empty;
    }

    #endregion

    #region Interface

    /// <summary>
    /// Service Level Engine interface
    /// Provides SLO/SLI/Error Budget management
    /// </summary>
    public interface IServiceLevelEngine
    {
        #region SLI Management

        /// <summary>
        /// Create SLI
        /// </summary>
        Task<ServiceLevelIndicator> CreateSLIAsync(
            string tenantId,
            ServiceLevelIndicator sli,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get SLI by ID
        /// </summary>
        Task<ServiceLevelIndicator?> GetSLIAsync(
            string tenantId,
            string sliId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List all SLIs
        /// </summary>
        Task<List<ServiceLevelIndicator>> ListSLIsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Calculate current SLI value
        /// </summary>
        Task<double> CalculateSLIValueAsync(
            string tenantId,
            string sliId,
            TimeSpan window,
            CancellationToken cancellation = default);

        #endregion

        #region SLO Management

        /// <summary>
        /// Create SLO
        /// </summary>
        Task<ServiceLevelObjective> CreateSLOAsync(
            string tenantId,
            ServiceLevelObjective slo,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get SLO by ID
        /// </summary>
        Task<ServiceLevelObjective?> GetSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Update SLO
        /// </summary>
        Task<ServiceLevelObjective> UpdateSLOAsync(
            string tenantId,
            ServiceLevelObjective slo,
            CancellationToken cancellation = default);

        /// <summary>
        /// Delete SLO
        /// </summary>
        Task<bool> DeleteSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List all SLOs
        /// </summary>
        Task<List<ServiceLevelObjective>> ListSLOsAsync(
            string tenantId,
            string? serviceId = null,
            CancellationToken cancellation = default);

        #endregion

        #region Error Budget

        /// <summary>
        /// Calculate error budget for an SLO
        /// </summary>
        Task<ErrorBudget> CalculateErrorBudgetAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get error budget burn down history
        /// </summary>
        Task<List<BudgetBurnDown>> GetBudgetBurnDownAsync(
            string tenantId,
            string sloId,
            TimeSpan window,
            CancellationToken cancellation = default);

        /// <summary>
        /// Record budget consumption event
        /// </summary>
        Task<BudgetConsumptionEvent> RecordBudgetConsumptionAsync(
            string tenantId,
            BudgetConsumptionEvent consumptionEvent,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get budget consumption events
        /// </summary>
        Task<List<BudgetConsumptionEvent>> GetBudgetConsumptionEventsAsync(
            string tenantId,
            string sloId,
            TimeSpan window,
            CancellationToken cancellation = default);

        #endregion

        #region Alerting

        /// <summary>
        /// Check SLO alerts
        /// </summary>
        Task<List<SLOAlert>> CheckAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get active alerts
        /// </summary>
        Task<List<SLOAlert>> GetActiveAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Generate Prometheus alerting rules
        /// </summary>
        Task<string> GeneratePrometheusRulesAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default);

        #endregion

        #region Reporting

        /// <summary>
        /// Generate compliance report
        /// </summary>
        Task<SLOComplianceReport> GenerateComplianceReportAsync(
            string tenantId,
            ReportPeriod period,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get SLO dashboard data
        /// </summary>
        Task<SLODashboardData> GetDashboardDataAsync(
            string tenantId,
            CancellationToken cancellation = default);

        #endregion

        #region OpenSLO

        /// <summary>
        /// Export SLO to OpenSLO format
        /// </summary>
        Task<OpenSLOSpec> ExportToOpenSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Import SLO from OpenSLO format
        /// </summary>
        Task<ServiceLevelObjective> ImportFromOpenSLOAsync(
            string tenantId,
            OpenSLOSpec spec,
            CancellationToken cancellation = default);

        #endregion
    }

    #endregion

    #region Dashboard Types

    /// <summary>
    /// SLO dashboard data
    /// </summary>
    public class SLODashboardData
    {
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Overall summary
        /// </summary>
        public DashboardSummary Summary { get; set; } = new();

        /// <summary>
        /// SLO status cards
        /// </summary>
        public List<SLOStatusCard> SLOCards { get; set; } = new();

        /// <summary>
        /// Recent alerts
        /// </summary>
        public List<SLOAlert> RecentAlerts { get; set; } = new();

        /// <summary>
        /// Recent budget consumption events
        /// </summary>
        public List<BudgetConsumptionEvent> RecentEvents { get; set; } = new();

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Dashboard summary
    /// </summary>
    public class DashboardSummary
    {
        public int TotalSLOs { get; set; }
        public int HealthySLOs { get; set; }
        public int AtRiskSLOs { get; set; }
        public int BreachedSLOs { get; set; }
        public double AverageBudgetRemaining { get; set; }
        public int ActiveAlerts { get; set; }
    }

    /// <summary>
    /// SLO status card for dashboard
    /// </summary>
    public class SLOStatusCard
    {
        public string SLOId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public SLOComplianceStatus Status { get; set; }
        public double Target { get; set; }
        public double Current { get; set; }
        public double BudgetRemainingPercent { get; set; }
        public double BurnRate { get; set; }
        public TimeSpan? TimeToExhaustion { get; set; }
        public List<SparklinePoint> Sparkline { get; set; } = new();
    }

    /// <summary>
    /// Sparkline data point
    /// </summary>
    public class SparklinePoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    #endregion

    #region Implementation

    /// <summary>
    /// Service Level Engine implementation
    /// </summary>
    public class ServiceLevelEngine : IServiceLevelEngine
    {
        private readonly ILogger<ServiceLevelEngine> _logger;
        private readonly Dictionary<string, Dictionary<string, ServiceLevelIndicator>> _slis = new();
        private readonly Dictionary<string, Dictionary<string, ServiceLevelObjective>> _slos = new();
        private readonly Dictionary<string, List<BudgetConsumptionEvent>> _consumptionEvents = new();
        private readonly Dictionary<string, List<SLOAlert>> _alerts = new();
        private readonly Dictionary<string, List<BudgetBurnDown>> _burnDownHistory = new();

        // Simulated metrics data (in real implementation, would query Prometheus/Datadog/etc.)
        private readonly Random _random = new();

        public ServiceLevelEngine(ILogger<ServiceLevelEngine> logger)
        {
            _logger = logger;
            InitializeDefaultSLIs();
        }

        #region SLI Management

        public Task<ServiceLevelIndicator> CreateSLIAsync(
            string tenantId,
            ServiceLevelIndicator sli,
            CancellationToken cancellation = default)
        {
            if (!_slis.ContainsKey(tenantId))
                _slis[tenantId] = new();

            sli.TenantId = tenantId;
            sli.CreatedAt = DateTime.UtcNow;

            _slis[tenantId][sli.Id] = sli;

            _logger.LogInformation(
                "Created SLI {Id} '{Name}' for tenant {TenantId}",
                sli.Id, sli.Name, tenantId);

            return Task.FromResult(sli);
        }

        public Task<ServiceLevelIndicator?> GetSLIAsync(
            string tenantId,
            string sliId,
            CancellationToken cancellation = default)
        {
            if (_slis.TryGetValue(tenantId, out var slis) &&
                slis.TryGetValue(sliId, out var sli))
            {
                return Task.FromResult<ServiceLevelIndicator?>(sli);
            }

            return Task.FromResult<ServiceLevelIndicator?>(null);
        }

        public Task<List<ServiceLevelIndicator>> ListSLIsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_slis.TryGetValue(tenantId, out var slis))
                return Task.FromResult(new List<ServiceLevelIndicator>());

            return Task.FromResult(slis.Values.ToList());
        }

        public Task<double> CalculateSLIValueAsync(
            string tenantId,
            string sliId,
            TimeSpan window,
            CancellationToken cancellation = default)
        {
            // Simulated SLI calculation
            // In real implementation, would execute queries against metrics backend
            var baseValue = 0.995 + (_random.NextDouble() * 0.004); // 99.5% - 99.9%
            return Task.FromResult(baseValue * 100); // Return as percentage
        }

        #endregion

        #region SLO Management

        public async Task<ServiceLevelObjective> CreateSLOAsync(
            string tenantId,
            ServiceLevelObjective slo,
            CancellationToken cancellation = default)
        {
            if (!_slos.ContainsKey(tenantId))
                _slos[tenantId] = new();

            slo.TenantId = tenantId;
            slo.CreatedAt = DateTime.UtcNow;

            // Generate default alert rules if not provided
            if (!slo.AlertRules.Any())
            {
                slo.AlertRules = GenerateDefaultAlertRules(slo.Target);
            }

            // Generate default budget policies if not provided
            if (!slo.BudgetPolicies.Any())
            {
                slo.BudgetPolicies = GenerateDefaultBudgetPolicies();
            }

            _slos[tenantId][slo.Id] = slo;

            _logger.LogInformation(
                "Created SLO {Id} '{Name}' with target {Target}% for tenant {TenantId}",
                slo.Id, slo.Name, slo.Target, tenantId);

            return slo;
        }

        private List<SLOAlertRule> GenerateDefaultAlertRules(double target)
        {
            var errorBudget = 100 - target; // e.g., 0.1% for 99.9% SLO
            var windowHours = 720; // 30-day SLO

            return new List<SLOAlertRule>
            {
                // Page: 2% budget consumed in 1 hour (14.4x burn rate)
                new SLOAlertRule
                {
                    Name = "HighBurnRate",
                    Severity = AlertSeverity.Page,
                    BurnRate = 14.4,
                    ShortWindow = TimeSpan.FromMinutes(5),
                    LongWindow = TimeSpan.FromHours(1),
                    BudgetConsumedPercent = 2,
                    Annotations = new Dictionary<string, string>
                    {
                        ["summary"] = "High error rate consuming error budget rapidly",
                        ["description"] = "Error budget is being consumed at 14.4x the sustainable rate"
                    }
                },
                // Page: 5% budget consumed in 6 hours (6x burn rate)
                new SLOAlertRule
                {
                    Name = "MediumBurnRate",
                    Severity = AlertSeverity.Page,
                    BurnRate = 6,
                    ShortWindow = TimeSpan.FromMinutes(30),
                    LongWindow = TimeSpan.FromHours(6),
                    BudgetConsumedPercent = 5,
                    Annotations = new Dictionary<string, string>
                    {
                        ["summary"] = "Elevated error rate affecting error budget",
                        ["description"] = "Error budget is being consumed at 6x the sustainable rate"
                    }
                },
                // Ticket: 10% budget consumed in 3 days (1x burn rate)
                new SLOAlertRule
                {
                    Name = "LowBurnRate",
                    Severity = AlertSeverity.Ticket,
                    BurnRate = 1,
                    ShortWindow = TimeSpan.FromHours(6),
                    LongWindow = TimeSpan.FromDays(3),
                    BudgetConsumedPercent = 10,
                    Annotations = new Dictionary<string, string>
                    {
                        ["summary"] = "Sustained error rate at budget consumption limit",
                        ["description"] = "Error budget is being consumed at the sustainable rate"
                    }
                }
            };
        }

        private List<ErrorBudgetPolicy> GenerateDefaultBudgetPolicies()
        {
            return new List<ErrorBudgetPolicy>
            {
                new ErrorBudgetPolicy
                {
                    Name = "BudgetLow",
                    TriggerThreshold = 25,
                    Actions = new List<BudgetPolicyAction>
                    {
                        BudgetPolicyAction.Alert,
                        BudgetPolicyAction.SlowDown
                    },
                    NotificationChannels = new List<string> { "slack-sre", "email-oncall" }
                },
                new ErrorBudgetPolicy
                {
                    Name = "BudgetCritical",
                    TriggerThreshold = 10,
                    Actions = new List<BudgetPolicyAction>
                    {
                        BudgetPolicyAction.Alert,
                        BudgetPolicyAction.Freeze,
                        BudgetPolicyAction.Escalate
                    },
                    NotificationChannels = new List<string> { "pagerduty", "slack-sre", "email-management" }
                },
                new ErrorBudgetPolicy
                {
                    Name = "BudgetExhausted",
                    TriggerThreshold = 0,
                    Actions = new List<BudgetPolicyAction>
                    {
                        BudgetPolicyAction.PageOnCall,
                        BudgetPolicyAction.Freeze,
                        BudgetPolicyAction.Escalate
                    },
                    NotificationChannels = new List<string> { "pagerduty-critical", "slack-incident" }
                }
            };
        }

        public Task<ServiceLevelObjective?> GetSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default)
        {
            if (_slos.TryGetValue(tenantId, out var slos) &&
                slos.TryGetValue(sloId, out var slo))
            {
                return Task.FromResult<ServiceLevelObjective?>(slo);
            }

            return Task.FromResult<ServiceLevelObjective?>(null);
        }

        public Task<ServiceLevelObjective> UpdateSLOAsync(
            string tenantId,
            ServiceLevelObjective slo,
            CancellationToken cancellation = default)
        {
            if (!_slos.ContainsKey(tenantId) ||
                !_slos[tenantId].ContainsKey(slo.Id))
            {
                throw new InvalidOperationException($"SLO {slo.Id} not found");
            }

            slo.UpdatedAt = DateTime.UtcNow;
            _slos[tenantId][slo.Id] = slo;

            return Task.FromResult(slo);
        }

        public Task<bool> DeleteSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default)
        {
            if (_slos.TryGetValue(tenantId, out var slos))
            {
                return Task.FromResult(slos.Remove(sloId));
            }

            return Task.FromResult(false);
        }

        public Task<List<ServiceLevelObjective>> ListSLOsAsync(
            string tenantId,
            string? serviceId = null,
            CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(tenantId, out var slos))
                return Task.FromResult(new List<ServiceLevelObjective>());

            var result = slos.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(serviceId))
            {
                result = result.Where(s => s.ServiceId == serviceId);
            }

            return Task.FromResult(result.ToList());
        }

        #endregion

        #region Error Budget

        public async Task<ErrorBudget> CalculateErrorBudgetAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default)
        {
            var slo = await GetSLOAsync(tenantId, sloId, cancellation);
            if (slo == null)
                throw new InvalidOperationException($"SLO {sloId} not found");

            var windowDays = slo.Window.Duration;
            var windowMinutes = windowDays * 24 * 60.0;

            // Calculate total error budget (in minutes)
            var errorBudgetPercent = 100 - slo.Target;
            var totalBudgetMinutes = windowMinutes * (errorBudgetPercent / 100);

            // Simulate current SLI value
            var currentSLI = await CalculateSLIValueAsync(tenantId, slo.SLIId, TimeSpan.FromDays(windowDays), cancellation);

            // Calculate consumed budget
            var actualErrorPercent = Math.Max(0, 100 - currentSLI);
            var consumedMinutes = windowMinutes * (actualErrorPercent / 100);

            // Calculate burn rate
            var expectedConsumedMinutes = totalBudgetMinutes * (DateTime.UtcNow - slo.CreatedAt).TotalDays / windowDays;
            var burnRate = expectedConsumedMinutes > 0 ? consumedMinutes / expectedConsumedMinutes : 1;

            // Calculate time to exhaustion
            TimeSpan? timeToExhaustion = null;
            if (burnRate > 1 && consumedMinutes < totalBudgetMinutes)
            {
                var remainingMinutes = totalBudgetMinutes - consumedMinutes;
                var burnRatePerMinute = consumedMinutes / (DateTime.UtcNow - slo.CreatedAt).TotalMinutes;
                if (burnRatePerMinute > 0)
                {
                    timeToExhaustion = TimeSpan.FromMinutes(remainingMinutes / burnRatePerMinute);
                }
            }

            // Determine status
            var remainingPercent = totalBudgetMinutes > 0 ? ((totalBudgetMinutes - consumedMinutes) / totalBudgetMinutes) * 100 : 0;
            var status = remainingPercent switch
            {
                <= 0 => SLOComplianceStatus.Breached,
                <= 25 => SLOComplianceStatus.AtRisk,
                _ => SLOComplianceStatus.Met
            };

            var budget = new ErrorBudget
            {
                SLOId = sloId,
                SLOName = slo.Name,
                Window = slo.Window,
                WindowStart = DateTime.UtcNow.AddDays(-windowDays),
                WindowEnd = DateTime.UtcNow,
                TotalBudget = totalBudgetMinutes,
                ConsumedBudget = consumedMinutes,
                CurrentBurnRate = burnRate,
                AverageBurnRate = burnRate,
                TimeToExhaustion = timeToExhaustion,
                Status = status,
                Trend = burnRate < 1 ? 1 : -1 // Positive if improving
            };

            return budget;
        }

        public async Task<List<BudgetBurnDown>> GetBudgetBurnDownAsync(
            string tenantId,
            string sloId,
            TimeSpan window,
            CancellationToken cancellation = default)
        {
            // Generate historical burn down data
            var burnDown = new List<BudgetBurnDown>();
            var now = DateTime.UtcNow;
            var startingBudget = 100.0;

            for (int i = (int)window.TotalHours; i >= 0; i--)
            {
                var timestamp = now.AddHours(-i);
                var consumed = (window.TotalHours - i) / window.TotalHours * (100 - startingBudget * 0.7);

                burnDown.Add(new BudgetBurnDown
                {
                    SLOId = sloId,
                    Timestamp = timestamp,
                    RemainingPercent = Math.Max(0, startingBudget - consumed),
                    BurnRate = 1 + (_random.NextDouble() - 0.5) * 0.5,
                    CumulativeConsumed = consumed
                });
            }

            return burnDown;
        }

        public Task<BudgetConsumptionEvent> RecordBudgetConsumptionAsync(
            string tenantId,
            BudgetConsumptionEvent consumptionEvent,
            CancellationToken cancellation = default)
        {
            if (!_consumptionEvents.ContainsKey(tenantId))
                _consumptionEvents[tenantId] = new();

            _consumptionEvents[tenantId].Add(consumptionEvent);

            _logger.LogInformation(
                "Recorded budget consumption event {Id} for SLO {SLOId}: {BudgetConsumed:F2}%",
                consumptionEvent.Id, consumptionEvent.SLOId, consumptionEvent.BudgetConsumed);

            return Task.FromResult(consumptionEvent);
        }

        public Task<List<BudgetConsumptionEvent>> GetBudgetConsumptionEventsAsync(
            string tenantId,
            string sloId,
            TimeSpan window,
            CancellationToken cancellation = default)
        {
            if (!_consumptionEvents.TryGetValue(tenantId, out var events))
                return Task.FromResult(new List<BudgetConsumptionEvent>());

            var cutoff = DateTime.UtcNow - window;
            var result = events
                .Where(e => e.SLOId == sloId && e.StartTime >= cutoff)
                .OrderByDescending(e => e.StartTime)
                .ToList();

            return Task.FromResult(result);
        }

        #endregion

        #region Alerting

        public async Task<List<SLOAlert>> CheckAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var alerts = new List<SLOAlert>();
            var slos = await ListSLOsAsync(tenantId, null, cancellation);

            foreach (var slo in slos.Where(s => s.IsActive))
            {
                var budget = await CalculateErrorBudgetAsync(tenantId, slo.Id, cancellation);

                foreach (var rule in slo.AlertRules)
                {
                    // Check if burn rate exceeds threshold
                    if (budget.CurrentBurnRate >= rule.BurnRate)
                    {
                        var alert = new SLOAlert
                        {
                            SLOId = slo.Id,
                            SLOName = slo.Name,
                            RuleName = rule.Name,
                            Severity = rule.Severity,
                            BurnRate = budget.CurrentBurnRate,
                            BudgetRemainingPercent = budget.RemainingPercent,
                            Message = $"SLO '{slo.Name}' burn rate ({budget.CurrentBurnRate:F1}x) exceeds threshold ({rule.BurnRate}x)",
                            Annotations = rule.Annotations
                        };

                        alerts.Add(alert);

                        // Store alert
                        if (!_alerts.ContainsKey(tenantId))
                            _alerts[tenantId] = new();
                        _alerts[tenantId].Add(alert);
                    }
                }

                // Check budget policies
                foreach (var policy in slo.BudgetPolicies)
                {
                    if (budget.RemainingPercent <= policy.TriggerThreshold)
                    {
                        _logger.LogWarning(
                            "Budget policy '{PolicyName}' triggered for SLO '{SLOName}': {RemainingPercent:F1}% remaining",
                            policy.Name, slo.Name, budget.RemainingPercent);

                        // Execute policy actions
                        foreach (var action in policy.Actions)
                        {
                            ExecutePolicyAction(tenantId, slo, policy, action, budget);
                        }
                    }
                }
            }

            return alerts;
        }

        private void ExecutePolicyAction(
            string tenantId,
            ServiceLevelObjective slo,
            ErrorBudgetPolicy policy,
            BudgetPolicyAction action,
            ErrorBudget budget)
        {
            switch (action)
            {
                case BudgetPolicyAction.Alert:
                    _logger.LogInformation("ALERT: SLO {SLOName} budget at {Remaining:F1}%", slo.Name, budget.RemainingPercent);
                    break;
                case BudgetPolicyAction.SlowDown:
                    _logger.LogWarning("SLOW DOWN: Reducing deployment velocity for {ServiceName}", slo.ServiceName);
                    break;
                case BudgetPolicyAction.Freeze:
                    _logger.LogError("FREEZE: Non-critical changes frozen for {ServiceName}", slo.ServiceName);
                    break;
                case BudgetPolicyAction.Escalate:
                    _logger.LogError("ESCALATE: Escalating to management for {ServiceName}", slo.ServiceName);
                    break;
                case BudgetPolicyAction.PageOnCall:
                    _logger.LogCritical("PAGE: Paging on-call engineer for {ServiceName}", slo.ServiceName);
                    break;
            }
        }

        public Task<List<SLOAlert>> GetActiveAlertsAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            if (!_alerts.TryGetValue(tenantId, out var alerts))
                return Task.FromResult(new List<SLOAlert>());

            var active = alerts.Where(a => a.Status == "firing").ToList();
            return Task.FromResult(active);
        }

        public async Task<string> GeneratePrometheusRulesAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default)
        {
            var slo = await GetSLOAsync(tenantId, sloId, cancellation);
            if (slo == null)
                throw new InvalidOperationException($"SLO {sloId} not found");

            var sli = await GetSLIAsync(tenantId, slo.SLIId, cancellation);
            var errorBudget = 100 - slo.Target;
            var windowSeconds = slo.Window.Duration * 24 * 60 * 60;

            var rules = $@"# SLO: {slo.Name}
# Target: {slo.Target}%
# Error Budget: {errorBudget}%

groups:
  - name: {slo.Name.ToLower().Replace(" ", "-")}-slo-rules
    rules:
      # SLI Recording Rule
      - record: sli:{slo.Name.ToLower().Replace(" ", "_")}:ratio
        expr: |
          sum(rate({sli?.MetricSpec.GoodQuery}[5m])) /
          sum(rate({sli?.MetricSpec.TotalQuery}[5m]))

      # Error Budget Remaining
      - record: error_budget:{slo.Name.ToLower().Replace(" ", "_")}:remaining
        expr: |
          1 - (
            (1 - sli:{slo.Name.ToLower().Replace(" ", "_")}:ratio) /
            {errorBudget / 100}
          )

      # Burn Rate (1h)
      - record: burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:1h
        expr: |
          sum(rate({sli?.MetricSpec.GoodQuery}[1h])) /
          sum(rate({sli?.MetricSpec.TotalQuery}[1h]))

      # Page Alert: High Burn Rate (14.4x)
      - alert: {slo.Name}HighBurnRate
        expr: |
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:1h > 14.4 * {errorBudget / 100}
          and
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:5m > 14.4 * {errorBudget / 100}
        for: 2m
        labels:
          severity: page
          slo: {slo.Name}
        annotations:
          summary: ""High burn rate on {slo.Name}""
          description: ""Error budget being consumed at 14.4x sustainable rate""

      # Page Alert: Medium Burn Rate (6x)
      - alert: {slo.Name}MediumBurnRate
        expr: |
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:6h > 6 * {errorBudget / 100}
          and
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:30m > 6 * {errorBudget / 100}
        for: 5m
        labels:
          severity: page
          slo: {slo.Name}
        annotations:
          summary: ""Medium burn rate on {slo.Name}""
          description: ""Error budget being consumed at 6x sustainable rate""

      # Ticket Alert: Low Burn Rate (1x)
      - alert: {slo.Name}LowBurnRate
        expr: |
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:3d > {errorBudget / 100}
          and
          burn_rate:{slo.Name.ToLower().Replace(" ", "_")}:6h > {errorBudget / 100}
        for: 15m
        labels:
          severity: ticket
          slo: {slo.Name}
        annotations:
          summary: ""Sustained burn rate on {slo.Name}""
          description: ""Error budget consumption at sustainable rate limit""
";

            return rules;
        }

        #endregion

        #region Reporting

        public async Task<SLOComplianceReport> GenerateComplianceReportAsync(
            string tenantId,
            ReportPeriod period,
            CancellationToken cancellation = default)
        {
            var report = new SLOComplianceReport
            {
                TenantId = tenantId,
                Period = period
            };

            var slos = await ListSLOsAsync(tenantId, null, cancellation);

            foreach (var slo in slos)
            {
                var budget = await CalculateErrorBudgetAsync(tenantId, slo.Id, cancellation);
                var currentSLI = await CalculateSLIValueAsync(tenantId, slo.SLIId, TimeSpan.FromDays(30), cancellation);

                var result = new SLOComplianceResult
                {
                    SLOId = slo.Id,
                    SLOName = slo.Name,
                    ServiceName = slo.ServiceName,
                    Target = slo.Target,
                    Actual = currentSLI,
                    Status = budget.Status,
                    Budget = budget
                };

                report.SLOResults.Add(result);
            }

            // Calculate summary
            report.Summary = new ComplianceSummary
            {
                TotalSLOs = report.SLOResults.Count,
                MetCount = report.SLOResults.Count(r => r.Status == SLOComplianceStatus.Met),
                AtRiskCount = report.SLOResults.Count(r => r.Status == SLOComplianceStatus.AtRisk),
                BreachedCount = report.SLOResults.Count(r => r.Status == SLOComplianceStatus.Breached),
                OverallCompliancePercent = report.SLOResults.Count > 0
                    ? report.SLOResults.Count(r => r.Status == SLOComplianceStatus.Met) * 100.0 / report.SLOResults.Count
                    : 0,
                AverageErrorBudgetRemaining = report.SLOResults.Count > 0
                    ? report.SLOResults.Average(r => r.Budget.RemainingPercent)
                    : 0
            };

            // Generate recommendations
            foreach (var result in report.SLOResults.Where(r => r.Status != SLOComplianceStatus.Met))
            {
                report.Recommendations.Add(new SLORecommendation
                {
                    SLOId = result.SLOId,
                    Type = result.Status == SLOComplianceStatus.Breached ? "critical" : "improvement",
                    Title = $"Improve reliability for {result.SLOName}",
                    Description = $"Current: {result.Actual:F2}%, Target: {result.Target}%",
                    Priority = result.Status == SLOComplianceStatus.Breached ? "high" : "medium",
                    Actions = new List<string>
                    {
                        "Review recent incidents and changes",
                        "Analyze error patterns",
                        "Consider infrastructure improvements",
                        "Review deployment practices"
                    }
                });
            }

            return report;
        }

        public async Task<SLODashboardData> GetDashboardDataAsync(
            string tenantId,
            CancellationToken cancellation = default)
        {
            var dashboard = new SLODashboardData { TenantId = tenantId };

            var slos = await ListSLOsAsync(tenantId, null, cancellation);

            foreach (var slo in slos)
            {
                var budget = await CalculateErrorBudgetAsync(tenantId, slo.Id, cancellation);
                var currentSLI = await CalculateSLIValueAsync(tenantId, slo.SLIId, TimeSpan.FromDays(30), cancellation);

                var card = new SLOStatusCard
                {
                    SLOId = slo.Id,
                    Name = slo.Name,
                    ServiceName = slo.ServiceName,
                    Status = budget.Status,
                    Target = slo.Target,
                    Current = currentSLI,
                    BudgetRemainingPercent = budget.RemainingPercent,
                    BurnRate = budget.CurrentBurnRate,
                    TimeToExhaustion = budget.TimeToExhaustion
                };

                // Generate sparkline data
                var burnDown = await GetBudgetBurnDownAsync(tenantId, slo.Id, TimeSpan.FromDays(7), cancellation);
                card.Sparkline = burnDown.Select(b => new SparklinePoint
                {
                    Timestamp = b.Timestamp,
                    Value = b.RemainingPercent
                }).ToList();

                dashboard.SLOCards.Add(card);
            }

            // Summary
            dashboard.Summary = new DashboardSummary
            {
                TotalSLOs = dashboard.SLOCards.Count,
                HealthySLOs = dashboard.SLOCards.Count(c => c.Status == SLOComplianceStatus.Met),
                AtRiskSLOs = dashboard.SLOCards.Count(c => c.Status == SLOComplianceStatus.AtRisk),
                BreachedSLOs = dashboard.SLOCards.Count(c => c.Status == SLOComplianceStatus.Breached),
                AverageBudgetRemaining = dashboard.SLOCards.Count > 0
                    ? dashboard.SLOCards.Average(c => c.BudgetRemainingPercent)
                    : 0,
                ActiveAlerts = (await GetActiveAlertsAsync(tenantId, cancellation)).Count
            };

            // Recent alerts
            dashboard.RecentAlerts = (await GetActiveAlertsAsync(tenantId, cancellation))
                .OrderByDescending(a => a.FiredAt)
                .Take(10)
                .ToList();

            return dashboard;
        }

        #endregion

        #region OpenSLO

        public async Task<OpenSLOSpec> ExportToOpenSLOAsync(
            string tenantId,
            string sloId,
            CancellationToken cancellation = default)
        {
            var slo = await GetSLOAsync(tenantId, sloId, cancellation);
            if (slo == null)
                throw new InvalidOperationException($"SLO {sloId} not found");

            var spec = new OpenSLOSpec
            {
                ApiVersion = "openslo/v1",
                Kind = "SLO",
                Metadata = new OpenSLOMetadata
                {
                    Name = slo.Id,
                    DisplayName = slo.Name,
                    Labels = slo.Labels
                },
                Spec = new OpenSLOSpecification
                {
                    Description = slo.Description,
                    Service = slo.ServiceId,
                    Indicator = slo.SLIId,
                    Objectives = new List<OpenSLOTarget>
                    {
                        new OpenSLOTarget
                        {
                            DisplayName = "Primary",
                            Target = slo.Target / 100
                        }
                    },
                    TimeWindow = new OpenSLOTimeWindow
                    {
                        Duration = $"{slo.Window.Duration}d",
                        IsRolling = slo.Window.Type == SLOWindowType.Rolling
                    },
                    AlertPolicies = slo.AlertRules.Select(r => new OpenSLOAlertPolicy
                    {
                        Name = r.Name,
                        Conditions = new List<OpenSLOAlertCondition>
                        {
                            new OpenSLOAlertCondition
                            {
                                Kind = "burnrate",
                                Threshold = r.BurnRate,
                                LookbackWindow = $"{r.LongWindow.TotalMinutes}m"
                            }
                        }
                    }).ToList()
                }
            };

            return spec;
        }

        public async Task<ServiceLevelObjective> ImportFromOpenSLOAsync(
            string tenantId,
            OpenSLOSpec spec,
            CancellationToken cancellation = default)
        {
            var target = spec.Spec.Objectives.FirstOrDefault()?.Target ?? 0.999;

            var slo = new ServiceLevelObjective
            {
                Name = spec.Metadata.DisplayName,
                Description = spec.Spec.Description,
                ServiceId = spec.Spec.Service,
                SLIId = spec.Spec.Indicator,
                Target = target * 100, // Convert to percentage
                Labels = spec.Metadata.Labels,
                Window = new SLOWindow
                {
                    Type = spec.Spec.TimeWindow.IsRolling ? SLOWindowType.Rolling : SLOWindowType.Calendar,
                    Duration = ParseDuration(spec.Spec.TimeWindow.Duration)
                }
            };

            return await CreateSLOAsync(tenantId, slo, cancellation);
        }

        private int ParseDuration(string duration)
        {
            if (duration.EndsWith("d"))
            {
                return int.Parse(duration.TrimEnd('d'));
            }
            if (duration.EndsWith("h"))
            {
                return int.Parse(duration.TrimEnd('h')) / 24;
            }
            return 30; // Default
        }

        #endregion

        #region Initialization

        private void InitializeDefaultSLIs()
        {
            var defaultSLIs = new[]
            {
                new ServiceLevelIndicator
                {
                    Name = "Availability",
                    Description = "Service availability (successful requests / total requests)",
                    Type = SLIType.Availability,
                    Method = SLIMeasurementMethod.RatioBased,
                    MetricSpec = new SLIMetricSpec
                    {
                        GoodQuery = "sum(http_requests_total{status!~\"5..\"})",
                        TotalQuery = "sum(http_requests_total)",
                        MetricSource = "prometheus"
                    }
                },
                new ServiceLevelIndicator
                {
                    Name = "Latency P99",
                    Description = "99th percentile latency under threshold",
                    Type = SLIType.Latency,
                    Method = SLIMeasurementMethod.ThresholdBased,
                    MetricSpec = new SLIMetricSpec
                    {
                        GoodQuery = "sum(http_request_duration_seconds_bucket{le=\"0.3\"})",
                        TotalQuery = "sum(http_request_duration_seconds_count)",
                        MetricSource = "prometheus"
                    },
                    Threshold = new SLIThreshold
                    {
                        Value = 300,
                        Operator = "lt",
                        Unit = "ms"
                    }
                },
                new ServiceLevelIndicator
                {
                    Name = "Error Rate",
                    Description = "Error rate (errors / total requests)",
                    Type = SLIType.ErrorRate,
                    Method = SLIMeasurementMethod.RatioBased,
                    MetricSpec = new SLIMetricSpec
                    {
                        GoodQuery = "sum(http_requests_total{status!~\"5..\"})",
                        TotalQuery = "sum(http_requests_total)",
                        MetricSource = "prometheus"
                    }
                }
            };

            foreach (var sli in defaultSLIs)
            {
                if (!_slis.ContainsKey("default"))
                    _slis["default"] = new();
                _slis["default"][sli.Id] = sli;
            }
        }

        #endregion
    }

    #endregion
}
