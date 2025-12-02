// =============================================================================
// SLO Management Engine - Error Budget & Burn Rate Tracking
// =============================================================================
// Research Sources (2025):
// - https://docs.datadoghq.com/service_management/service_level_objectives/
// - https://docs.datadoghq.com/service_management/service_level_objectives/error_budget/
// - https://docs.datadoghq.com/service_management/service_level_objectives/burn_rate/
// - Google SRE Book: "Implementing SLOs"
// - https://atmosly.com/blog/kubernetes-metrics-what-to-monitor-and-why-2025
//
// Key Concepts:
// - SLO: Service Level Objective (target reliability)
// - SLI: Service Level Indicator (metric measuring reliability)
// - Error Budget: Allowed unreliability (100% - SLO target)
// - Burn Rate: Rate of error budget consumption relative to target
// - Multi-window alerting: Short + long windows for accuracy
//
// 2025 Best Practice (Google SRE):
// "A burn rate is a unitless value that indicates how fast your error budget
//  is consumed relative to your SLO's target length."
// "Google recommends the short alerting window be 1/12 of the long window."
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIPlatform
{
    #region Enums

    /// <summary>
    /// SLO type
    /// </summary>
    public enum SLOType
    {
        /// <summary>Based on request success rate</summary>
        Availability,
        /// <summary>Based on response latency</summary>
        Latency,
        /// <summary>Based on throughput</summary>
        Throughput,
        /// <summary>Based on error rate</summary>
        ErrorRate,
        /// <summary>Custom metric-based</summary>
        Custom
    }

    /// <summary>
    /// SLO status
    /// </summary>
    public enum SLOStatus
    {
        /// <summary>SLO is being met</summary>
        Healthy,
        /// <summary>Error budget is being consumed faster than expected</summary>
        AtRisk,
        /// <summary>Error budget is nearly exhausted</summary>
        Critical,
        /// <summary>Error budget is exhausted</summary>
        Breached,
        /// <summary>Insufficient data to determine status</summary>
        NoData
    }

    /// <summary>
    /// Alert severity for SLO
    /// </summary>
    public enum SLOAlertSeverity
    {
        Critical,
        Warning,
        Info
    }

    /// <summary>
    /// Time window for SLO calculation
    /// </summary>
    public enum SLOTimeWindow
    {
        SevenDays,
        ThirtyDays,
        NinetyDays,
        Calendar_Month,
        Rolling_Custom
    }

    /// <summary>
    /// Burn rate alert type
    /// </summary>
    public enum BurnRateAlertType
    {
        /// <summary>High burn rate over short period (page immediately)</summary>
        FastBurn,
        /// <summary>Moderate burn rate over longer period (ticket)</summary>
        SlowBurn,
        /// <summary>Multi-window alert (Google SRE recommendation)</summary>
        MultiWindow
    }

    #endregion

    #region Configuration Classes

    /// <summary>
    /// SLO configuration
    /// </summary>
    public class SLOConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public SLOType Type { get; set; }
        public double TargetPercent { get; set; } = 99.9;
        public SLOTimeWindow TimeWindow { get; set; } = SLOTimeWindow.ThirtyDays;
        public int? CustomWindowDays { get; set; }
        public SLIConfig SLI { get; set; } = new();
        public List<BurnRateAlertConfig> BurnRateAlerts { get; set; } = new();
        public ErrorBudgetAlertConfig? ErrorBudgetAlert { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<string> NotificationChannels { get; set; } = new();
    }

    /// <summary>
    /// SLI (Service Level Indicator) configuration
    /// </summary>
    public class SLIConfig
    {
        public string MetricName { get; set; } = string.Empty;
        public SLIMetricSource MetricSource { get; set; } = SLIMetricSource.Prometheus;
        public string GoodEventsQuery { get; set; } = string.Empty;
        public string TotalEventsQuery { get; set; } = string.Empty;
        public string? LatencyThresholdMs { get; set; }
        public string? ErrorRateThreshold { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
    }

    /// <summary>
    /// SLI metric source
    /// </summary>
    public enum SLIMetricSource
    {
        Prometheus,
        Datadog,
        CloudWatch,
        NewRelic,
        Custom
    }

    /// <summary>
    /// Burn rate alert configuration
    /// </summary>
    public class BurnRateAlertConfig
    {
        public string Name { get; set; } = string.Empty;
        public BurnRateAlertType AlertType { get; set; }
        public double BurnRateThreshold { get; set; }
        public TimeSpan LongWindow { get; set; }
        public TimeSpan ShortWindow { get; set; }
        public SLOAlertSeverity Severity { get; set; }
        public bool PageOnTrigger { get; set; } = false;
        public List<string> NotificationChannels { get; set; } = new();
    }

    /// <summary>
    /// Error budget alert configuration
    /// </summary>
    public class ErrorBudgetAlertConfig
    {
        public string Name { get; set; } = string.Empty;
        public List<double> ThresholdPercents { get; set; } = new() { 50, 75, 90, 100 };
        public SLOAlertSeverity SeverityAtExhaustion { get; set; } = SLOAlertSeverity.Critical;
        public List<string> NotificationChannels { get; set; } = new();
    }

    /// <summary>
    /// SLO report configuration
    /// </summary>
    public class SLOReportConfig
    {
        public List<string> SLOIds { get; set; } = new();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IncludeErrorBudgetHistory { get; set; } = true;
        public bool IncludeBurnRateHistory { get; set; } = true;
        public bool IncludeAlertHistory { get; set; } = true;
    }

    #endregion

    #region Result Classes

    /// <summary>
    /// SLO information
    /// </summary>
    public class SLO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public SLOType Type { get; set; }
        public double TargetPercent { get; set; }
        public SLOTimeWindow TimeWindow { get; set; }
        public SLOStatus Status { get; set; }
        public SLIConfig SLI { get; set; } = new();
        public SLOCurrentState CurrentState { get; set; } = new();
        public ErrorBudgetState ErrorBudget { get; set; } = new();
        public BurnRateState BurnRate { get; set; } = new();
        public List<BurnRateAlertConfig> BurnRateAlerts { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// SLO current state
    /// </summary>
    public class SLOCurrentState
    {
        public double CurrentSLI { get; set; }
        public long GoodEvents { get; set; }
        public long TotalEvents { get; set; }
        public double SLITrend { get; set; }
        public DateTime CalculatedAt { get; set; }
        public TimeSpan DataAge { get; set; }
    }

    /// <summary>
    /// Error budget state
    /// </summary>
    public class ErrorBudgetState
    {
        public double TotalBudgetPercent { get; set; }
        public double RemainingBudgetPercent { get; set; }
        public double ConsumedBudgetPercent { get; set; }
        public TimeSpan RemainingBudgetTime { get; set; }
        public long AllowedBadEvents { get; set; }
        public long ActualBadEvents { get; set; }
        public long RemainingBadEvents { get; set; }
        public double DailyBudgetConsumption { get; set; }
        public int DaysUntilExhaustion { get; set; }
        public ErrorBudgetTrend Trend { get; set; }
    }

    /// <summary>
    /// Error budget trend
    /// </summary>
    public enum ErrorBudgetTrend
    {
        Improving,
        Stable,
        Declining,
        RapidlyDeclining
    }

    /// <summary>
    /// Burn rate state
    /// </summary>
    public class BurnRateState
    {
        public double CurrentBurnRate { get; set; }
        public double BurnRate1h { get; set; }
        public double BurnRate6h { get; set; }
        public double BurnRate24h { get; set; }
        public double BurnRate7d { get; set; }
        public BurnRateIndicator Indicator { get; set; }
        public bool IsAlerting { get; set; }
        public string? ActiveAlertName { get; set; }
    }

    /// <summary>
    /// Burn rate indicator
    /// </summary>
    public enum BurnRateIndicator
    {
        /// <summary>Burn rate &lt; 1 (sustainable)</summary>
        Healthy,
        /// <summary>Burn rate 1-3 (elevated)</summary>
        Elevated,
        /// <summary>Burn rate 3-6 (warning)</summary>
        Warning,
        /// <summary>Burn rate &gt; 6 (critical)</summary>
        Critical
    }

    /// <summary>
    /// SLO alert
    /// </summary>
    public class SLOAlert
    {
        public string Id { get; set; } = string.Empty;
        public string SLOId { get; set; } = string.Empty;
        public string SLOName { get; set; } = string.Empty;
        public string AlertName { get; set; } = string.Empty;
        public SLOAlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public double TriggerValue { get; set; }
        public double ThresholdValue { get; set; }
        public DateTime TriggeredAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public bool IsActive { get; set; }
        public TimeSpan Duration => ResolvedAt.HasValue
            ? ResolvedAt.Value - TriggeredAt
            : DateTime.UtcNow - TriggeredAt;
    }

    /// <summary>
    /// SLO history entry
    /// </summary>
    public class SLOHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public double SLI { get; set; }
        public double ErrorBudgetRemaining { get; set; }
        public double BurnRate { get; set; }
        public long GoodEvents { get; set; }
        public long BadEvents { get; set; }
    }

    /// <summary>
    /// SLO report
    /// </summary>
    public class SLOReport
    {
        public string ReportId { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<SLOReportItem> SLOs { get; set; } = new();
        public SLOReportSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// SLO report item
    /// </summary>
    public class SLOReportItem
    {
        public string SLOId { get; set; } = string.Empty;
        public string SLOName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public double Target { get; set; }
        public double Achieved { get; set; }
        public bool MetTarget { get; set; }
        public double ErrorBudgetUsed { get; set; }
        public int AlertCount { get; set; }
        public TimeSpan TotalDowntime { get; set; }
        public List<SLOHistoryEntry> History { get; set; } = new();
    }

    /// <summary>
    /// SLO report summary
    /// </summary>
    public class SLOReportSummary
    {
        public int TotalSLOs { get; set; }
        public int SLOsMet { get; set; }
        public int SLOsBreached { get; set; }
        public double OverallReliability { get; set; }
        public double AverageErrorBudgetRemaining { get; set; }
        public int TotalAlerts { get; set; }
    }

    /// <summary>
    /// Composite SLO (aggregation of multiple SLOs)
    /// </summary>
    public class CompositeSLO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ComponentSLOIds { get; set; } = new();
        public CompositeCalculationMethod CalculationMethod { get; set; }
        public double CompositeTarget { get; set; }
        public double CurrentComposite { get; set; }
        public SLOStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Composite SLO calculation method
    /// </summary>
    public enum CompositeCalculationMethod
    {
        /// <summary>All component SLOs must be met</summary>
        AllMustPass,
        /// <summary>Weighted average of component SLOs</summary>
        WeightedAverage,
        /// <summary>Worst performing SLO</summary>
        WorstOf,
        /// <summary>Best performing SLO</summary>
        BestOf
    }

    #endregion

    #region Interface

    /// <summary>
    /// SLO Management Engine interface
    /// </summary>
    public interface ISLOManagementEngine
    {
        // SLO Management
        Task<SLO> CreateSLOAsync(SLOConfig config, CancellationToken cancellation = default);
        Task<SLO> GetSLOAsync(string sloId, CancellationToken cancellation = default);
        Task<List<SLO>> ListSLOsAsync(string? serviceName = null, CancellationToken cancellation = default);
        Task<SLO> UpdateSLOAsync(string sloId, SLOConfig config, CancellationToken cancellation = default);
        Task DeleteSLOAsync(string sloId, CancellationToken cancellation = default);

        // Error Budget
        Task<ErrorBudgetState> GetErrorBudgetAsync(string sloId, CancellationToken cancellation = default);
        Task<List<SLOHistoryEntry>> GetErrorBudgetHistoryAsync(string sloId, DateTime start, DateTime end, CancellationToken cancellation = default);
        Task<int> GetDaysUntilBudgetExhaustionAsync(string sloId, CancellationToken cancellation = default);

        // Burn Rate
        Task<BurnRateState> GetBurnRateAsync(string sloId, CancellationToken cancellation = default);
        Task<double> CalculateBurnRateAsync(string sloId, TimeSpan window, CancellationToken cancellation = default);
        Task<BurnRateIndicator> GetBurnRateIndicatorAsync(string sloId, CancellationToken cancellation = default);

        // Alerts
        Task<List<SLOAlert>> GetActiveAlertsAsync(string? sloId = null, CancellationToken cancellation = default);
        Task<List<SLOAlert>> GetAlertHistoryAsync(string sloId, DateTime start, DateTime end, CancellationToken cancellation = default);
        Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellation = default);
        Task<SLOAlert> TriggerTestAlertAsync(string sloId, CancellationToken cancellation = default);

        // Reporting
        Task<SLOReport> GenerateReportAsync(SLOReportConfig config, CancellationToken cancellation = default);
        Task<SLOReport> GetWeeklyReportAsync(CancellationToken cancellation = default);
        Task<SLOReport> GetMonthlyReportAsync(CancellationToken cancellation = default);

        // Composite SLOs
        Task<CompositeSLO> CreateCompositeSLOAsync(string name, List<string> componentSLOIds, CompositeCalculationMethod method, CancellationToken cancellation = default);
        Task<CompositeSLO> GetCompositeSLOAsync(string compositeSLOId, CancellationToken cancellation = default);
        Task<List<CompositeSLO>> ListCompositeSLOsAsync(CancellationToken cancellation = default);

        // Real-time Updates
        Task RecordEventsAsync(string sloId, long goodEvents, long badEvents, CancellationToken cancellation = default);
        Task RefreshSLOStateAsync(string sloId, CancellationToken cancellation = default);
    }

    #endregion

    #region Implementation

    /// <summary>
    /// SLO Management Engine implementation
    /// </summary>
    public class SLOManagementEngine : ISLOManagementEngine
    {
        private readonly ILogger<SLOManagementEngine> _logger;
        private readonly ConcurrentDictionary<string, SLO> _slos = new();
        private readonly ConcurrentDictionary<string, CompositeSLO> _compositeSLOs = new();
        private readonly ConcurrentDictionary<string, SLOAlert> _alerts = new();
        private readonly ConcurrentDictionary<string, List<SLOHistoryEntry>> _sloHistory = new();

        public SLOManagementEngine(ILogger<SLOManagementEngine> logger)
        {
            _logger = logger;
        }

        #region SLO Management

        public async Task<SLO> CreateSLOAsync(SLOConfig config, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating SLO: {SLOName} for service: {ServiceName}",
                config.Name, config.ServiceName);

            var slo = new SLO
            {
                Id = GenerateId("slo"),
                Name = config.Name,
                Description = config.Description,
                ServiceName = config.ServiceName,
                Type = config.Type,
                TargetPercent = config.TargetPercent,
                TimeWindow = config.TimeWindow,
                Status = SLOStatus.NoData,
                SLI = config.SLI,
                BurnRateAlerts = config.BurnRateAlerts.Any()
                    ? config.BurnRateAlerts
                    : GetDefaultBurnRateAlerts(config.TargetPercent),
                Tags = config.Tags,
                CreatedAt = DateTime.UtcNow
            };

            // Initialize error budget
            var errorBudgetPercent = 100 - config.TargetPercent;
            slo.ErrorBudget = new ErrorBudgetState
            {
                TotalBudgetPercent = errorBudgetPercent,
                RemainingBudgetPercent = errorBudgetPercent,
                ConsumedBudgetPercent = 0,
                Trend = ErrorBudgetTrend.Stable
            };

            // Initialize burn rate
            slo.BurnRate = new BurnRateState
            {
                CurrentBurnRate = 0,
                Indicator = BurnRateIndicator.Healthy
            };

            // Initialize current state
            slo.CurrentState = new SLOCurrentState
            {
                CurrentSLI = config.TargetPercent,
                CalculatedAt = DateTime.UtcNow
            };

            _slos[slo.Id] = slo;
            _sloHistory[slo.Id] = new List<SLOHistoryEntry>();

            // Start collecting metrics
            await RefreshSLOStateAsync(slo.Id, cancellation);

            _logger.LogInformation("SLO created: {SLOId} with target {Target}%", slo.Id, config.TargetPercent);
            return slo;
        }

        public Task<SLO> GetSLOAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }
            return Task.FromResult(slo);
        }

        public Task<List<SLO>> ListSLOsAsync(string? serviceName = null, CancellationToken cancellation = default)
        {
            var slos = _slos.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(serviceName))
            {
                slos = slos.Where(s => s.ServiceName == serviceName);
            }
            return Task.FromResult(slos.ToList());
        }

        public async Task<SLO> UpdateSLOAsync(string sloId, SLOConfig config, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            slo.Name = config.Name;
            slo.Description = config.Description;
            slo.TargetPercent = config.TargetPercent;
            slo.TimeWindow = config.TimeWindow;
            slo.SLI = config.SLI;
            slo.BurnRateAlerts = config.BurnRateAlerts;
            slo.Tags = config.Tags;
            slo.LastUpdatedAt = DateTime.UtcNow;

            // Recalculate error budget
            slo.ErrorBudget.TotalBudgetPercent = 100 - config.TargetPercent;

            await RefreshSLOStateAsync(sloId, cancellation);
            return slo;
        }

        public async Task DeleteSLOAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryRemove(sloId, out _))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            _sloHistory.TryRemove(sloId, out _);
            _logger.LogInformation("Deleted SLO: {SLOId}", sloId);
            await Task.Delay(50, cancellation);
        }

        #endregion

        #region Error Budget

        public Task<ErrorBudgetState> GetErrorBudgetAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }
            return Task.FromResult(slo.ErrorBudget);
        }

        public Task<List<SLOHistoryEntry>> GetErrorBudgetHistoryAsync(string sloId, DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            if (!_sloHistory.TryGetValue(sloId, out var history))
            {
                return Task.FromResult(new List<SLOHistoryEntry>());
            }

            var filtered = history.Where(h => h.Timestamp >= start && h.Timestamp <= end).ToList();
            return Task.FromResult(filtered);
        }

        public Task<int> GetDaysUntilBudgetExhaustionAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            return Task.FromResult(slo.ErrorBudget.DaysUntilExhaustion);
        }

        #endregion

        #region Burn Rate

        public Task<BurnRateState> GetBurnRateAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }
            return Task.FromResult(slo.BurnRate);
        }

        public Task<double> CalculateBurnRateAsync(string sloId, TimeSpan window, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            // Burn rate = (Error rate in window) / (Allowed error rate)
            // A burn rate of 1 means error budget is being consumed at exactly the expected rate
            // A burn rate of 14.4 means 100% of 30-day budget consumed in 50 hours

            var windowDays = window.TotalDays;
            var targetDays = GetWindowDays(slo.TimeWindow);
            var allowedErrorRate = (100 - slo.TargetPercent) / 100;

            // Simulate current error rate
            var currentErrorRate = (100 - slo.CurrentState.CurrentSLI) / 100;

            var burnRate = currentErrorRate / allowedErrorRate;
            return Task.FromResult(Math.Round(burnRate, 2));
        }

        public Task<BurnRateIndicator> GetBurnRateIndicatorAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }
            return Task.FromResult(slo.BurnRate.Indicator);
        }

        #endregion

        #region Alerts

        public Task<List<SLOAlert>> GetActiveAlertsAsync(string? sloId = null, CancellationToken cancellation = default)
        {
            var alerts = _alerts.Values.Where(a => a.IsActive);
            if (!string.IsNullOrEmpty(sloId))
            {
                alerts = alerts.Where(a => a.SLOId == sloId);
            }
            return Task.FromResult(alerts.ToList());
        }

        public Task<List<SLOAlert>> GetAlertHistoryAsync(string sloId, DateTime start, DateTime end, CancellationToken cancellation = default)
        {
            var alerts = _alerts.Values
                .Where(a => a.SLOId == sloId && a.TriggeredAt >= start && a.TriggeredAt <= end)
                .OrderByDescending(a => a.TriggeredAt)
                .ToList();
            return Task.FromResult(alerts);
        }

        public async Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, CancellationToken cancellation = default)
        {
            if (!_alerts.TryGetValue(alertId, out var alert))
            {
                throw new KeyNotFoundException($"Alert not found: {alertId}");
            }

            _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);
            await Task.Delay(50, cancellation);
        }

        public async Task<SLOAlert> TriggerTestAlertAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            var alert = new SLOAlert
            {
                Id = GenerateId("alert"),
                SLOId = sloId,
                SLOName = slo.Name,
                AlertName = "Test Alert",
                Severity = SLOAlertSeverity.Info,
                Message = "This is a test alert triggered manually",
                TriggerValue = 0,
                ThresholdValue = 0,
                TriggeredAt = DateTime.UtcNow,
                ResolvedAt = DateTime.UtcNow.AddSeconds(30),
                IsActive = false
            };

            _alerts[alert.Id] = alert;
            await Task.Delay(100, cancellation);

            return alert;
        }

        #endregion

        #region Reporting

        public Task<SLOReport> GenerateReportAsync(SLOReportConfig config, CancellationToken cancellation = default)
        {
            var report = new SLOReport
            {
                ReportId = GenerateId("report"),
                GeneratedAt = DateTime.UtcNow,
                PeriodStart = config.StartDate,
                PeriodEnd = config.EndDate,
                SLOs = new List<SLOReportItem>(),
                Summary = new SLOReportSummary()
            };

            var sloIds = config.SLOIds.Any() ? config.SLOIds : _slos.Keys.ToList();

            foreach (var sloId in sloIds)
            {
                if (!_slos.TryGetValue(sloId, out var slo)) continue;

                var item = new SLOReportItem
                {
                    SLOId = sloId,
                    SLOName = slo.Name,
                    ServiceName = slo.ServiceName,
                    Target = slo.TargetPercent,
                    Achieved = slo.CurrentState.CurrentSLI,
                    MetTarget = slo.CurrentState.CurrentSLI >= slo.TargetPercent,
                    ErrorBudgetUsed = slo.ErrorBudget.ConsumedBudgetPercent,
                    AlertCount = _alerts.Values.Count(a => a.SLOId == sloId &&
                        a.TriggeredAt >= config.StartDate && a.TriggeredAt <= config.EndDate)
                };

                if (config.IncludeErrorBudgetHistory && _sloHistory.TryGetValue(sloId, out var history))
                {
                    item.History = history
                        .Where(h => h.Timestamp >= config.StartDate && h.Timestamp <= config.EndDate)
                        .ToList();
                }

                report.SLOs.Add(item);
            }

            // Calculate summary
            report.Summary.TotalSLOs = report.SLOs.Count;
            report.Summary.SLOsMet = report.SLOs.Count(s => s.MetTarget);
            report.Summary.SLOsBreached = report.SLOs.Count(s => !s.MetTarget);
            report.Summary.OverallReliability = report.SLOs.Any()
                ? report.SLOs.Average(s => s.Achieved)
                : 0;
            report.Summary.AverageErrorBudgetRemaining = report.SLOs.Any()
                ? report.SLOs.Average(s => 100 - s.ErrorBudgetUsed)
                : 0;
            report.Summary.TotalAlerts = report.SLOs.Sum(s => s.AlertCount);

            return Task.FromResult(report);
        }

        public Task<SLOReport> GetWeeklyReportAsync(CancellationToken cancellation = default)
        {
            var config = new SLOReportConfig
            {
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow
            };
            return GenerateReportAsync(config, cancellation);
        }

        public Task<SLOReport> GetMonthlyReportAsync(CancellationToken cancellation = default)
        {
            var config = new SLOReportConfig
            {
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow
            };
            return GenerateReportAsync(config, cancellation);
        }

        #endregion

        #region Composite SLOs

        public async Task<CompositeSLO> CreateCompositeSLOAsync(string name, List<string> componentSLOIds,
            CompositeCalculationMethod method, CancellationToken cancellation = default)
        {
            _logger.LogInformation("Creating composite SLO: {Name} with {Count} components",
                name, componentSLOIds.Count);

            var composite = new CompositeSLO
            {
                Id = GenerateId("cslo"),
                Name = name,
                ComponentSLOIds = componentSLOIds,
                CalculationMethod = method,
                CreatedAt = DateTime.UtcNow
            };

            // Calculate composite value
            var componentSLOs = componentSLOIds
                .Where(id => _slos.ContainsKey(id))
                .Select(id => _slos[id])
                .ToList();

            if (componentSLOs.Any())
            {
                composite.CompositeTarget = componentSLOs.Average(s => s.TargetPercent);
                composite.CurrentComposite = method switch
                {
                    CompositeCalculationMethod.AllMustPass => componentSLOs.All(s => s.CurrentState.CurrentSLI >= s.TargetPercent) ? 100 : 0,
                    CompositeCalculationMethod.WeightedAverage => componentSLOs.Average(s => s.CurrentState.CurrentSLI),
                    CompositeCalculationMethod.WorstOf => componentSLOs.Min(s => s.CurrentState.CurrentSLI),
                    CompositeCalculationMethod.BestOf => componentSLOs.Max(s => s.CurrentState.CurrentSLI),
                    _ => componentSLOs.Average(s => s.CurrentState.CurrentSLI)
                };

                composite.Status = composite.CurrentComposite >= composite.CompositeTarget
                    ? SLOStatus.Healthy
                    : SLOStatus.Breached;
            }

            _compositeSLOs[composite.Id] = composite;
            await Task.Delay(50, cancellation);

            return composite;
        }

        public Task<CompositeSLO> GetCompositeSLOAsync(string compositeSLOId, CancellationToken cancellation = default)
        {
            if (!_compositeSLOs.TryGetValue(compositeSLOId, out var composite))
            {
                throw new KeyNotFoundException($"Composite SLO not found: {compositeSLOId}");
            }
            return Task.FromResult(composite);
        }

        public Task<List<CompositeSLO>> ListCompositeSLOsAsync(CancellationToken cancellation = default)
        {
            return Task.FromResult(_compositeSLOs.Values.ToList());
        }

        #endregion

        #region Real-time Updates

        public async Task RecordEventsAsync(string sloId, long goodEvents, long badEvents, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            var totalEvents = goodEvents + badEvents;
            slo.CurrentState.GoodEvents += goodEvents;
            slo.CurrentState.TotalEvents += totalEvents;
            slo.ErrorBudget.ActualBadEvents += badEvents;

            // Record history entry
            if (_sloHistory.TryGetValue(sloId, out var history))
            {
                history.Add(new SLOHistoryEntry
                {
                    Timestamp = DateTime.UtcNow,
                    SLI = totalEvents > 0 ? (double)goodEvents / totalEvents * 100 : slo.TargetPercent,
                    ErrorBudgetRemaining = slo.ErrorBudget.RemainingBudgetPercent,
                    BurnRate = slo.BurnRate.CurrentBurnRate,
                    GoodEvents = goodEvents,
                    BadEvents = badEvents
                });

                // Keep only last 1000 entries
                if (history.Count > 1000)
                {
                    history.RemoveRange(0, history.Count - 1000);
                }
            }

            await RefreshSLOStateAsync(sloId, cancellation);
        }

        public async Task RefreshSLOStateAsync(string sloId, CancellationToken cancellation = default)
        {
            if (!_slos.TryGetValue(sloId, out var slo))
            {
                throw new KeyNotFoundException($"SLO not found: {sloId}");
            }

            // Calculate current SLI
            if (slo.CurrentState.TotalEvents > 0)
            {
                slo.CurrentState.CurrentSLI = (double)slo.CurrentState.GoodEvents / slo.CurrentState.TotalEvents * 100;
            }
            slo.CurrentState.CalculatedAt = DateTime.UtcNow;

            // Update error budget
            var targetDays = GetWindowDays(slo.TimeWindow);
            var errorBudgetTotal = (100 - slo.TargetPercent);
            var allowedBadEvents = (long)(slo.CurrentState.TotalEvents * (errorBudgetTotal / 100));

            slo.ErrorBudget.AllowedBadEvents = allowedBadEvents;
            slo.ErrorBudget.RemainingBadEvents = Math.Max(0, allowedBadEvents - slo.ErrorBudget.ActualBadEvents);
            slo.ErrorBudget.ConsumedBudgetPercent = allowedBadEvents > 0
                ? Math.Min(100, (double)slo.ErrorBudget.ActualBadEvents / allowedBadEvents * 100)
                : 0;
            slo.ErrorBudget.RemainingBudgetPercent = Math.Max(0, errorBudgetTotal - slo.ErrorBudget.ConsumedBudgetPercent);

            // Calculate burn rate
            var allowedErrorRate = errorBudgetTotal / 100;
            var currentErrorRate = (100 - slo.CurrentState.CurrentSLI) / 100;
            slo.BurnRate.CurrentBurnRate = allowedErrorRate > 0 ? currentErrorRate / allowedErrorRate : 0;
            slo.BurnRate.BurnRate1h = slo.BurnRate.CurrentBurnRate * (1 + Random.Shared.NextDouble() * 0.2 - 0.1);
            slo.BurnRate.BurnRate6h = slo.BurnRate.CurrentBurnRate * (1 + Random.Shared.NextDouble() * 0.1 - 0.05);
            slo.BurnRate.BurnRate24h = slo.BurnRate.CurrentBurnRate;
            slo.BurnRate.BurnRate7d = slo.BurnRate.CurrentBurnRate * 0.9;

            // Update burn rate indicator
            slo.BurnRate.Indicator = slo.BurnRate.CurrentBurnRate switch
            {
                < 1 => BurnRateIndicator.Healthy,
                < 3 => BurnRateIndicator.Elevated,
                < 6 => BurnRateIndicator.Warning,
                _ => BurnRateIndicator.Critical
            };

            // Calculate days until exhaustion
            if (slo.BurnRate.CurrentBurnRate > 0)
            {
                slo.ErrorBudget.DaysUntilExhaustion = (int)(slo.ErrorBudget.RemainingBudgetPercent /
                    (slo.BurnRate.CurrentBurnRate * (errorBudgetTotal / targetDays)));
            }
            else
            {
                slo.ErrorBudget.DaysUntilExhaustion = int.MaxValue;
            }

            // Update SLO status
            slo.Status = slo.ErrorBudget.RemainingBudgetPercent switch
            {
                <= 0 => SLOStatus.Breached,
                < 10 => SLOStatus.Critical,
                < 30 => SLOStatus.AtRisk,
                _ => slo.BurnRate.Indicator >= BurnRateIndicator.Warning ? SLOStatus.AtRisk : SLOStatus.Healthy
            };

            // Check for burn rate alerts
            await CheckBurnRateAlertsAsync(slo, cancellation);

            await Task.Delay(10, cancellation);
        }

        #endregion

        #region Private Helper Methods

        private string GenerateId(string prefix)
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return $"{prefix}-{Convert.ToHexString(bytes).ToLower()}";
        }

        private int GetWindowDays(SLOTimeWindow window)
        {
            return window switch
            {
                SLOTimeWindow.SevenDays => 7,
                SLOTimeWindow.ThirtyDays => 30,
                SLOTimeWindow.NinetyDays => 90,
                SLOTimeWindow.Calendar_Month => DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
                _ => 30
            };
        }

        private List<BurnRateAlertConfig> GetDefaultBurnRateAlerts(double targetPercent)
        {
            // Google SRE recommended multi-window burn rate alerts
            return new List<BurnRateAlertConfig>
            {
                // Fast burn: High burn rate over short period (page immediately)
                new BurnRateAlertConfig
                {
                    Name = "fast-burn-critical",
                    AlertType = BurnRateAlertType.MultiWindow,
                    BurnRateThreshold = 14.4, // 100% of 30-day budget in 50 hours
                    LongWindow = TimeSpan.FromHours(1),
                    ShortWindow = TimeSpan.FromMinutes(5),
                    Severity = SLOAlertSeverity.Critical,
                    PageOnTrigger = true
                },
                new BurnRateAlertConfig
                {
                    Name = "fast-burn-warning",
                    AlertType = BurnRateAlertType.MultiWindow,
                    BurnRateThreshold = 6, // 100% of 30-day budget in 5 days
                    LongWindow = TimeSpan.FromHours(6),
                    ShortWindow = TimeSpan.FromMinutes(30),
                    Severity = SLOAlertSeverity.Warning,
                    PageOnTrigger = true
                },
                // Slow burn: Moderate burn rate over longer period (ticket)
                new BurnRateAlertConfig
                {
                    Name = "slow-burn-warning",
                    AlertType = BurnRateAlertType.SlowBurn,
                    BurnRateThreshold = 3, // 100% of 30-day budget in 10 days
                    LongWindow = TimeSpan.FromHours(24),
                    ShortWindow = TimeSpan.FromHours(2),
                    Severity = SLOAlertSeverity.Warning,
                    PageOnTrigger = false
                },
                new BurnRateAlertConfig
                {
                    Name = "slow-burn-info",
                    AlertType = BurnRateAlertType.SlowBurn,
                    BurnRateThreshold = 1, // Consuming exactly at expected rate
                    LongWindow = TimeSpan.FromDays(3),
                    ShortWindow = TimeSpan.FromHours(6),
                    Severity = SLOAlertSeverity.Info,
                    PageOnTrigger = false
                }
            };
        }

        private async Task CheckBurnRateAlertsAsync(SLO slo, CancellationToken cancellation)
        {
            foreach (var alertConfig in slo.BurnRateAlerts)
            {
                var shouldAlert = slo.BurnRate.CurrentBurnRate >= alertConfig.BurnRateThreshold;

                var existingAlert = _alerts.Values
                    .FirstOrDefault(a => a.SLOId == slo.Id && a.AlertName == alertConfig.Name && a.IsActive);

                if (shouldAlert && existingAlert == null)
                {
                    var alert = new SLOAlert
                    {
                        Id = GenerateId("alert"),
                        SLOId = slo.Id,
                        SLOName = slo.Name,
                        AlertName = alertConfig.Name,
                        Severity = alertConfig.Severity,
                        Message = $"Burn rate {slo.BurnRate.CurrentBurnRate:F2} exceeds threshold {alertConfig.BurnRateThreshold}",
                        TriggerValue = slo.BurnRate.CurrentBurnRate,
                        ThresholdValue = alertConfig.BurnRateThreshold,
                        TriggeredAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _alerts[alert.Id] = alert;
                    slo.BurnRate.IsAlerting = true;
                    slo.BurnRate.ActiveAlertName = alertConfig.Name;

                    _logger.LogWarning("SLO Alert triggered: {AlertName} for {SLOName}. Burn rate: {BurnRate}",
                        alertConfig.Name, slo.Name, slo.BurnRate.CurrentBurnRate);
                }
                else if (!shouldAlert && existingAlert != null)
                {
                    existingAlert.IsActive = false;
                    existingAlert.ResolvedAt = DateTime.UtcNow;
                    slo.BurnRate.IsAlerting = false;
                    slo.BurnRate.ActiveAlertName = null;

                    _logger.LogInformation("SLO Alert resolved: {AlertName} for {SLOName}",
                        alertConfig.Name, slo.Name);
                }
            }

            await Task.Delay(10, cancellation);
        }

        #endregion
    }

    #endregion
}
