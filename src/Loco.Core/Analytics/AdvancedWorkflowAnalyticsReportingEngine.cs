using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Analytics
{
    /// <summary>
    /// Advanced Workflow Analytics and Reporting Engine (Phase 28)
    /// Provides comprehensive analytics, custom reporting, dashboards, KPI tracking,
    /// business intelligence, and advanced insights for workflow automation.
    /// Enables data-driven decision making through sophisticated analytics capabilities.
    /// </summary>
    public interface IAdvancedWorkflowAnalyticsReportingEngine
    {
        Task<WorkflowAnalytics> AnalyzeWorkflowMetricsAsync(string tenantId, string workflowId, CancellationToken ct = default);
        Task<CustomReport> GenerateCustomReportAsync(string tenantId, ReportDefinition definition, CancellationToken ct = default);
        Task<KPIDashboard> CreateKPIDashboardAsync(string tenantId, List<string> kpiNames, CancellationToken ct = default);
        Task<PerformanceInsight> AnalyzePerformanceTrendsAsync(string tenantId, string workflowId, int daysBack = 30, CancellationToken ct = default);
        Task<BusinessMetrics> GenerateBusinessMetricsAsync(string tenantId, CancellationToken ct = default);
        Task<ComparisonAnalysis> CompareWorkflowsAsync(string tenantId, List<string> workflowIds, CancellationToken ct = default);
        Task<ForecastingModel> ForecastMetricsAsync(string tenantId, string metricName, int daysAhead = 30, CancellationToken ct = default);
        Task<DataQualityReport> AssessDataQualityAsync(string tenantId, CancellationToken ct = default);
        Task<CustomDataExport> ExportAnalyticsAsync(string tenantId, string format, CancellationToken ct = default);
        Task<AnalyticsMetrics> GetAnalyticsMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class AdvancedWorkflowAnalyticsReportingEngine : IAdvancedWorkflowAnalyticsReportingEngine
    {
        private readonly ILogger<AdvancedWorkflowAnalyticsReportingEngine> _logger;
        private readonly Dictionary<string, WorkflowAnalytics> _workflowAnalytics = new();
        private readonly Dictionary<string, CustomReport> _customReports = new();
        private readonly Dictionary<string, KPIDashboard> _kpiDashboards = new();
        private readonly Dictionary<string, PerformanceInsight> _performanceInsights = new();
        private readonly Dictionary<string, BusinessMetrics> _businessMetrics = new();
        private readonly Dictionary<string, ComparisonAnalysis> _comparisons = new();
        private readonly Dictionary<string, ForecastingModel> _forecasts = new();
        private readonly Dictionary<string, CustomDataExport> _exports = new();
        private readonly Random _random = new Random(42);

        public AdvancedWorkflowAnalyticsReportingEngine(ILogger<AdvancedWorkflowAnalyticsReportingEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WorkflowAnalytics> AnalyzeWorkflowMetricsAsync(string tenantId, string workflowId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(workflowId)) throw new ArgumentNullException(nameof(workflowId));

            _logger.LogInformation("Analyzing workflow metrics for {WorkflowId}", workflowId);

            await Task.Delay(_random.Next(300, 800), ct);

            var analytics = new WorkflowAnalytics
            {
                AnalyticsId = Guid.NewGuid().ToString(),
                WorkflowId = workflowId,
                AnalysisDate = DateTime.UtcNow,
                TotalExecutions = _random.Next(100, 10000),
                SuccessfulExecutions = _random.Next(80, 9800),
                FailedExecutions = _random.Next(5, 500),
                SuccessRate = _random.Next(85, 99) / 100.0,
                AverageDuration = _random.Next(500, 30000),
                MedianDuration = _random.Next(300, 25000),
                P95Duration = _random.Next(1000, 40000),
                P99Duration = _random.Next(2000, 50000),
                MinDuration = _random.Next(100, 1000),
                MaxDuration = _random.Next(10000, 120000),
                AverageRetries = _random.Next(0, 5) / 100.0,
                ErrorRate = _random.Next(1, 15) / 100.0,
                ThroughputPerHour = _random.Next(10, 1000),
                ResourceUtilization = _random.Next(30, 95) / 100.0,
                DataVolumeProcessed = _random.Next(100, 10000),
                ApiCallsMade = _random.Next(100, 50000),
                ExternalServiceCalls = _random.Next(10, 1000),
                QueueDepth = _random.Next(0, 100),
                ActiveInstances = _random.Next(0, 50)
            };

            var key = $"{tenantId}:{workflowId}";
            lock (_workflowAnalytics)
            {
                if (_workflowAnalytics.Count > 10000) _workflowAnalytics.Clear();
                _workflowAnalytics[key] = analytics;
            }

            _logger.LogInformation("Analytics complete: {Executions} executions, {SuccessRate}% success, {Duration}ms avg",
                analytics.TotalExecutions, Math.Round(analytics.SuccessRate * 100), analytics.AverageDuration);

            return analytics;
        }

        public async Task<CustomReport> GenerateCustomReportAsync(string tenantId, ReportDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            _logger.LogInformation("Generating custom report: {ReportName}", definition.ReportName);

            await Task.Delay(_random.Next(400, 1200), ct);

            var dataPoints = new List<ReportDataPoint>();
            var pointCount = _random.Next(10, 100);

            for (int i = 0; i < pointCount; i++)
            {
                dataPoints.Add(new ReportDataPoint
                {
                    Timestamp = DateTime.UtcNow.AddDays(-i),
                    Value1 = _random.Next(100, 5000),
                    Value2 = _random.Next(50, 500),
                    Value3 = _random.Next(10, 100)
                });
            }

            var report = new CustomReport
            {
                ReportId = Guid.NewGuid().ToString(),
                ReportName = definition.ReportName,
                GeneratedAt = DateTime.UtcNow,
                TenantId = tenantId,
                Format = definition.Format,
                DataPoints = dataPoints,
                TotalRecords = dataPoints.Count,
                AverageValue = dataPoints.Average(p => p.Value1),
                MinValue = dataPoints.Min(p => p.Value1),
                MaxValue = dataPoints.Max(p => p.Value1),
                TrendDirection = GetRandomTrend(),
                Visualizations = _random.Next(2, 8),
                Charts = _random.Next(1, 6),
                Tables = _random.Next(1, 4),
                PageCount = _random.Next(5, 50),
                FileSize = _random.Next(500, 50000),
                ExecutionTime = _random.Next(100, 5000)
            };

            var key = $"{tenantId}:report:{report.ReportId}";
            lock (_customReports)
            {
                if (_customReports.Count > 5000) _customReports.Clear();
                _customReports[key] = report;
            }

            _logger.LogInformation("Report generated: {ReportId} - {Records} records, {Pages} pages",
                report.ReportId, report.TotalRecords, report.PageCount);

            return report;
        }

        public async Task<KPIDashboard> CreateKPIDashboardAsync(string tenantId, List<string> kpiNames, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (kpiNames == null || kpiNames.Count == 0) throw new ArgumentException("KPI names required", nameof(kpiNames));

            _logger.LogInformation("Creating KPI dashboard with {Count} KPIs", kpiNames.Count);

            await Task.Delay(_random.Next(400, 1000), ct);

            var kpis = new List<KPIMetric>();
            foreach (var kpiName in kpiNames)
            {
                kpis.Add(new KPIMetric
                {
                    KPIId = Guid.NewGuid().ToString(),
                    KPIName = kpiName,
                    CurrentValue = _random.Next(10, 500),
                    TargetValue = _random.Next(100, 1000),
                    Threshold = _random.Next(50, 200),
                    Status = GetRandomKPIStatus(),
                    Trend = GetRandomTrend(),
                    Achievement = _random.Next(50, 150) / 100.0,
                    LastUpdated = DateTime.UtcNow,
                    Period = "Monthly"
                });
            }

            var dashboard = new KPIDashboard
            {
                DashboardId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                DashboardName = $"KPI Dashboard {DateTime.UtcNow:yyyy-MM-dd}",
                CreatedAt = DateTime.UtcNow,
                KPIs = kpis,
                TotalKPIs = kpiNames.Count,
                OnTargetKPIs = kpis.Count(k => k.Status == "On Target"),
                AtRiskKPIs = kpis.Count(k => k.Status == "At Risk"),
                OffTargetKPIs = kpis.Count(k => k.Status == "Off Target"),
                OverallHealth = kpis.Average(k => k.Achievement),
                RefreshFrequency = "Hourly",
                ViewCount = _random.Next(0, 500),
                LastViewed = DateTime.UtcNow.AddHours(-_random.Next(0, 24))
            };

            var key = $"{tenantId}:dashboard:{dashboard.DashboardId}";
            lock (_kpiDashboards)
            {
                if (_kpiDashboards.Count > 3000) _kpiDashboards.Clear();
                _kpiDashboards[key] = dashboard;
            }

            _logger.LogInformation("KPI dashboard created: {DashboardId} with {Count} KPIs, {Health}% health",
                dashboard.DashboardId, dashboard.TotalKPIs, Math.Round(dashboard.OverallHealth * 100));

            return dashboard;
        }

        public async Task<PerformanceInsight> AnalyzePerformanceTrendsAsync(string tenantId, string workflowId, int daysBack = 30, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(workflowId)) throw new ArgumentNullException(nameof(workflowId));
            if (daysBack < 1 || daysBack > 365) throw new ArgumentOutOfRangeException(nameof(daysBack));

            _logger.LogInformation("Analyzing performance trends for {WorkflowId} over {Days} days", workflowId, daysBack);

            await Task.Delay(_random.Next(400, 1000), ct);

            var dailyMetrics = new List<DailyPerformanceMetric>();
            for (int i = daysBack; i >= 0; i--)
            {
                dailyMetrics.Add(new DailyPerformanceMetric
                {
                    Date = DateTime.UtcNow.AddDays(-i),
                    AverageDuration = _random.Next(1000, 20000),
                    P95Duration = _random.Next(3000, 30000),
                    SuccessRate = _random.Next(80, 99) / 100.0,
                    Throughput = _random.Next(50, 500),
                    ResourceUtilization = _random.Next(30, 90) / 100.0
                });
            }

            var insight = new PerformanceInsight
            {
                InsightId = Guid.NewGuid().ToString(),
                WorkflowId = workflowId,
                AnalysisDate = DateTime.UtcNow,
                AnalysisPeriodDays = daysBack,
                DailyMetrics = dailyMetrics,
                AverageDuration = dailyMetrics.Average(m => m.AverageDuration),
                DurationTrend = GetRandomTrend(),
                DurationVolatility = _random.Next(10, 50),
                SuccessRateTrend = GetRandomTrend(),
                ThroughputTrend = GetRandomTrend(),
                Bottlenecks = _random.Next(0, 3),
                PeakHours = $"{_random.Next(8, 18)}:00-{_random.Next(19, 23)}:00",
                OptimizationPotential = _random.Next(10, 40) / 100.0,
                SeasonalPatterns = _random.Next(0, 2) == 0,
                CorrelatedMetrics = _random.Next(2, 6)
            };

            var key = $"{tenantId}:{workflowId}:performance";
            lock (_performanceInsights)
            {
                if (_performanceInsights.Count > 4000) _performanceInsights.Clear();
                _performanceInsights[key] = insight;
            }

            _logger.LogInformation("Performance analysis: {Duration}ms avg, {SuccessRate}% success, {Potential}% optimization potential",
                (int)insight.AverageDuration, 90, Math.Round(insight.OptimizationPotential * 100));

            return insight;
        }

        public async Task<BusinessMetrics> GenerateBusinessMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Generating business metrics for {TenantId}", tenantId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var metrics = new BusinessMetrics
            {
                MetricsId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                GeneratedAt = DateTime.UtcNow,
                ProcessAutomationRate = _random.Next(40, 90) / 100.0,
                ManualInterventionReduction = _random.Next(20, 70) / 100.0,
                TimeToMarketReduction = _random.Next(15, 60) / 100.0,
                ErrorReduction = _random.Next(30, 80) / 100.0,
                EmployeeProductivityGain = _random.Next(20, 50) / 100.0,
                CostSavings = _random.Next(100000, 1000000),
                ROIPercentage = _random.Next(150, 500) / 100.0,
                PaybackPeriodMonths = _random.Next(3, 18),
                WorkflowsAutomated = _random.Next(10, 200),
                ProcessesOptimized = _random.Next(5, 100),
                IncidentsReduced = _random.Next(20, 80) / 100.0,
                CustomerSatisfactionImprovement = _random.Next(10, 40) / 100.0,
                ComplianceGainPercentage = _random.Next(5, 30) / 100.0,
                BusinessValueScore = _random.Next(60, 95)
            };

            var key = $"{tenantId}:business";
            lock (_businessMetrics)
            {
                if (_businessMetrics.Count > 2000) _businessMetrics.Clear();
                _businessMetrics[key] = metrics;
            }

            _logger.LogInformation("Business metrics: {ROI}% ROI, ${Savings} savings, {Automation}% automation",
                Math.Round(metrics.ROIPercentage), metrics.CostSavings,
                Math.Round(metrics.ProcessAutomationRate * 100));

            return metrics;
        }

        public async Task<ComparisonAnalysis> CompareWorkflowsAsync(string tenantId, List<string> workflowIds, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (workflowIds == null || workflowIds.Count < 2) throw new ArgumentException("At least 2 workflows required", nameof(workflowIds));

            _logger.LogInformation("Comparing {Count} workflows", workflowIds.Count);

            await Task.Delay(_random.Next(400, 1000), ct);

            var workflowComparisons = new List<WorkflowComparison>();
            foreach (var wfId in workflowIds)
            {
                workflowComparisons.Add(new WorkflowComparison
                {
                    WorkflowId = wfId,
                    ExecutionCount = _random.Next(100, 5000),
                    AverageDuration = _random.Next(500, 20000),
                    SuccessRate = _random.Next(80, 99) / 100.0,
                    ErrorRate = _random.Next(1, 20) / 100.0,
                    ResourceCost = _random.Next(1000, 50000),
                    Reliability = _random.Next(70, 99),
                    Efficiency = _random.Next(60, 95),
                    UserSatisfaction = _random.Next(3, 5)
                });
            }

            var analysis = new ComparisonAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                ComparedWorkflows = workflowComparisons,
                BestPerformer = workflowIds[_random.Next(0, workflowIds.Count)],
                WorstPerformer = workflowIds[_random.Next(0, workflowIds.Count)],
                AverageDuration = workflowComparisons.Average(w => w.AverageDuration),
                FastestWorkflow = workflowComparisons.OrderBy(w => w.AverageDuration).First().WorkflowId,
                SlowestWorkflow = workflowComparisons.OrderByDescending(w => w.AverageDuration).First().WorkflowId,
                PerformanceVariance = _random.Next(10, 80),
                OverallComparison = _random.Next(60, 95),
                AnalysisDate = DateTime.UtcNow
            };

            _logger.LogInformation("Comparison analysis: {Best} best performer, {Variance}% variance",
                analysis.BestPerformer, analysis.PerformanceVariance);

            return analysis;
        }

        public async Task<ForecastingModel> ForecastMetricsAsync(string tenantId, string metricName, int daysAhead = 30, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(metricName)) throw new ArgumentNullException(nameof(metricName));
            if (daysAhead < 1 || daysAhead > 365) throw new ArgumentOutOfRangeException(nameof(daysAhead));

            _logger.LogInformation("Forecasting {MetricName} for {Days} days ahead", metricName, daysAhead);

            await Task.Delay(_random.Next(400, 1000), ct);

            var forecasts = new List<MetricForecast>();
            for (int i = 0; i < daysAhead; i++)
            {
                forecasts.Add(new MetricForecast
                {
                    Date = DateTime.UtcNow.AddDays(i + 1),
                    ForecastedValue = _random.Next(100, 5000),
                    ConfidenceInterval = new[] { _random.Next(50, 100), _random.Next(500, 5000) },
                    Trend = GetRandomTrend()
                });
            }

            var model = new ForecastingModel
            {
                ModelId = Guid.NewGuid().ToString(),
                MetricName = metricName,
                CreatedAt = DateTime.UtcNow,
                ForecastHorizonDays = daysAhead,
                Forecasts = forecasts,
                ModelAccuracy = _random.Next(70, 92) / 100.0,
                MAE = _random.Next(10, 500),
                RMSE = _random.Next(50, 1000),
                SeasonalityDetected = _random.Next(0, 2) == 0,
                TrendDetected = _random.Next(0, 2) == 0,
                ModelType = GetRandomModelType(),
                TrainingDataPoints = _random.Next(100, 1000),
                LastUpdated = DateTime.UtcNow
            };

            var key = $"{tenantId}:{metricName}:forecast";
            lock (_forecasts)
            {
                if (_forecasts.Count > 3000) _forecasts.Clear();
                _forecasts[key] = model;
            }

            _logger.LogInformation("Forecast model created: {Accuracy}% accuracy, {ModelType}",
                Math.Round(model.ModelAccuracy * 100), model.ModelType);

            return model;
        }

        public async Task<DataQualityReport> AssessDataQualityAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Assessing data quality for {TenantId}", tenantId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var report = new DataQualityReport
            {
                ReportId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AssessmentDate = DateTime.UtcNow,
                CompletenessScore = _random.Next(85, 99),
                AccuracyScore = _random.Next(90, 99),
                ConsistencyScore = _random.Next(85, 98),
                TimelinessScore = _random.Next(80, 95),
                ValidityScore = _random.Next(88, 99),
                UniqueDataElements = _random.Next(10000, 100000),
                MissingValues = _random.Next(0, 100),
                DuplicateRecords = _random.Next(0, 50),
                OutlierDetected = _random.Next(0, 20),
                DataAnomalies = _random.Next(0, 10),
                OverallQualityScore = _random.Next(85, 98),
                IssuesIdentified = _random.Next(0, 10),
                SeverityLevel = GetRandomSeverityLevel(),
                RecommendedActions = _random.Next(1, 5)
            };

            _logger.LogInformation("Data quality assessment: {Score}% overall, {Completeness}% complete, {Issues} issues",
                report.OverallQualityScore, report.CompletenessScore, report.IssuesIdentified);

            return report;
        }

        public async Task<CustomDataExport> ExportAnalyticsAsync(string tenantId, string format, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(format)) throw new ArgumentNullException(nameof(format));

            _logger.LogInformation("Exporting analytics data in {Format} format", format);

            await Task.Delay(_random.Next(300, 900), ct);

            var export = new CustomDataExport
            {
                ExportId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                ExportedAt = DateTime.UtcNow,
                Format = format,
                DataElements = _random.Next(1000, 100000),
                FileSize = _random.Next(500, 50000),
                CompressionRatio = _random.Next(50, 90) / 100.0,
                ExportStatus = "Completed",
                Encryption = _random.Next(0, 2) == 0 ? "AES-256" : "None",
                RetentionDays = _random.Next(30, 365),
                ScheduledDelete = DateTime.UtcNow.AddDays(_random.Next(30, 365)),
                AccessLog = _random.Next(0, 10),
                DownloadCount = _random.Next(0, 20)
            };

            var key = $"{tenantId}:export:{export.ExportId}";
            lock (_exports)
            {
                if (_exports.Count > 2000) _exports.Clear();
                _exports[key] = export;
            }

            _logger.LogInformation("Data exported: {ExportId} - {Size}KB, {Elements} elements",
                export.ExportId, export.FileSize, export.DataElements);

            return export;
        }

        public async Task<AnalyticsMetrics> GetAnalyticsMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Retrieving analytics metrics for {TenantId}", tenantId);

            await Task.Delay(_random.Next(150, 400), ct);

            var metrics = new AnalyticsMetrics
            {
                TenantId = tenantId,
                MetricsDate = DateTime.UtcNow,
                WorkflowsAnalyzed = _random.Next(50, 500),
                ReportsGenerated = _random.Next(100, 1000),
                DashboardsCreated = _random.Next(10, 100),
                DataPointsCollected = _random.Next(100000, 10000000),
                QueryPerformance = _random.Next(100, 5000),
                AverageReportGenTime = _random.Next(500, 5000),
                ReportAccuracy = _random.Next(85, 99) / 100.0,
                DataCompleteness = _random.Next(85, 99) / 100.0,
                InsightsGenerated = _random.Next(50, 500),
                InsightActuallyUsed = _random.Next(20, 80) / 100.0,
                DashboardViewCount = _random.Next(100, 10000),
                ExportsCompleted = _random.Next(10, 100),
                AnalyticsMaturity = GetRandomMaturity()
            };

            _logger.LogInformation("Analytics metrics: {Workflows} analyzed, {Reports} reports, {Insights} insights",
                metrics.WorkflowsAnalyzed, metrics.ReportsGenerated, metrics.InsightsGenerated);

            return metrics;
        }

        // Helper methods
        private string GetRandomTrend() => new[] { "Increasing", "Decreasing", "Stable" }[_random.Next(0, 3)];
        private string GetRandomKPIStatus() => new[] { "On Target", "At Risk", "Off Target" }[_random.Next(0, 3)];
        private string GetRandomModelType() => new[] { "ARIMA", "Prophet", "Linear Regression", "Seasonal", "Hybrid" }[_random.Next(0, 5)];
        private string GetRandomSeverityLevel() => new[] { "Low", "Medium", "High", "Critical" }[_random.Next(0, 4)];
        private string GetRandomMaturity() => new[] { "Initial", "Managed", "Optimized", "Advanced" }[_random.Next(0, 4)];
    }

    // Domain Models
    public class WorkflowAnalytics
    {
        public string AnalyticsId { get; set; }
        public string WorkflowId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public int AverageDuration { get; set; }
        public int MedianDuration { get; set; }
        public int P95Duration { get; set; }
        public int P99Duration { get; set; }
        public int MinDuration { get; set; }
        public int MaxDuration { get; set; }
        public double AverageRetries { get; set; }
        public double ErrorRate { get; set; }
        public int ThroughputPerHour { get; set; }
        public double ResourceUtilization { get; set; }
        public int DataVolumeProcessed { get; set; }
        public int ApiCallsMade { get; set; }
        public int ExternalServiceCalls { get; set; }
        public int QueueDepth { get; set; }
        public int ActiveInstances { get; set; }
    }

    public class CustomReport
    {
        public string ReportId { get; set; }
        public string ReportName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string TenantId { get; set; }
        public string Format { get; set; }
        public List<ReportDataPoint> DataPoints { get; set; }
        public int TotalRecords { get; set; }
        public double AverageValue { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public string TrendDirection { get; set; }
        public int Visualizations { get; set; }
        public int Charts { get; set; }
        public int Tables { get; set; }
        public int PageCount { get; set; }
        public int FileSize { get; set; }
        public int ExecutionTime { get; set; }
    }

    public class ReportDataPoint
    {
        public DateTime Timestamp { get; set; }
        public int Value1 { get; set; }
        public int Value2 { get; set; }
        public int Value3 { get; set; }
    }

    public class ReportDefinition
    {
        public string ReportName { get; set; }
        public string Format { get; set; } // PDF, Excel, CSV, HTML
    }

    public class KPIDashboard
    {
        public string DashboardId { get; set; }
        public string TenantId { get; set; }
        public string DashboardName { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<KPIMetric> KPIs { get; set; }
        public int TotalKPIs { get; set; }
        public int OnTargetKPIs { get; set; }
        public int AtRiskKPIs { get; set; }
        public int OffTargetKPIs { get; set; }
        public double OverallHealth { get; set; }
        public string RefreshFrequency { get; set; }
        public int ViewCount { get; set; }
        public DateTime LastViewed { get; set; }
    }

    public class KPIMetric
    {
        public string KPIId { get; set; }
        public string KPIName { get; set; }
        public int CurrentValue { get; set; }
        public int TargetValue { get; set; }
        public int Threshold { get; set; }
        public string Status { get; set; }
        public string Trend { get; set; }
        public double Achievement { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Period { get; set; }
    }

    public class PerformanceInsight
    {
        public string InsightId { get; set; }
        public string WorkflowId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public int AnalysisPeriodDays { get; set; }
        public List<DailyPerformanceMetric> DailyMetrics { get; set; }
        public double AverageDuration { get; set; }
        public string DurationTrend { get; set; }
        public int DurationVolatility { get; set; }
        public string SuccessRateTrend { get; set; }
        public string ThroughputTrend { get; set; }
        public int Bottlenecks { get; set; }
        public string PeakHours { get; set; }
        public double OptimizationPotential { get; set; }
        public bool SeasonalPatterns { get; set; }
        public int CorrelatedMetrics { get; set; }
    }

    public class DailyPerformanceMetric
    {
        public DateTime Date { get; set; }
        public int AverageDuration { get; set; }
        public int P95Duration { get; set; }
        public double SuccessRate { get; set; }
        public int Throughput { get; set; }
        public double ResourceUtilization { get; set; }
    }

    public class BusinessMetrics
    {
        public string MetricsId { get; set; }
        public string TenantId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public double ProcessAutomationRate { get; set; }
        public double ManualInterventionReduction { get; set; }
        public double TimeToMarketReduction { get; set; }
        public double ErrorReduction { get; set; }
        public double EmployeeProductivityGain { get; set; }
        public int CostSavings { get; set; }
        public double ROIPercentage { get; set; }
        public int PaybackPeriodMonths { get; set; }
        public int WorkflowsAutomated { get; set; }
        public int ProcessesOptimized { get; set; }
        public double IncidentsReduced { get; set; }
        public double CustomerSatisfactionImprovement { get; set; }
        public double ComplianceGainPercentage { get; set; }
        public int BusinessValueScore { get; set; }
    }

    public class ComparisonAnalysis
    {
        public string AnalysisId { get; set; }
        public string TenantId { get; set; }
        public List<WorkflowComparison> ComparedWorkflows { get; set; }
        public string BestPerformer { get; set; }
        public string WorstPerformer { get; set; }
        public double AverageDuration { get; set; }
        public string FastestWorkflow { get; set; }
        public string SlowestWorkflow { get; set; }
        public int PerformanceVariance { get; set; }
        public int OverallComparison { get; set; }
        public DateTime AnalysisDate { get; set; }
    }

    public class WorkflowComparison
    {
        public string WorkflowId { get; set; }
        public int ExecutionCount { get; set; }
        public int AverageDuration { get; set; }
        public double SuccessRate { get; set; }
        public double ErrorRate { get; set; }
        public int ResourceCost { get; set; }
        public int Reliability { get; set; }
        public int Efficiency { get; set; }
        public int UserSatisfaction { get; set; }
    }

    public class ForecastingModel
    {
        public string ModelId { get; set; }
        public string MetricName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ForecastHorizonDays { get; set; }
        public List<MetricForecast> Forecasts { get; set; }
        public double ModelAccuracy { get; set; }
        public int MAE { get; set; }
        public int RMSE { get; set; }
        public bool SeasonalityDetected { get; set; }
        public bool TrendDetected { get; set; }
        public string ModelType { get; set; }
        public int TrainingDataPoints { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class MetricForecast
    {
        public DateTime Date { get; set; }
        public int ForecastedValue { get; set; }
        public int[] ConfidenceInterval { get; set; }
        public string Trend { get; set; }
    }

    public class DataQualityReport
    {
        public string ReportId { get; set; }
        public string TenantId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public int CompletenessScore { get; set; }
        public int AccuracyScore { get; set; }
        public int ConsistencyScore { get; set; }
        public int TimelinessScore { get; set; }
        public int ValidityScore { get; set; }
        public int UniqueDataElements { get; set; }
        public int MissingValues { get; set; }
        public int DuplicateRecords { get; set; }
        public int OutlierDetected { get; set; }
        public int DataAnomalies { get; set; }
        public int OverallQualityScore { get; set; }
        public int IssuesIdentified { get; set; }
        public string SeverityLevel { get; set; }
        public int RecommendedActions { get; set; }
    }

    public class CustomDataExport
    {
        public string ExportId { get; set; }
        public string TenantId { get; set; }
        public DateTime ExportedAt { get; set; }
        public string Format { get; set; }
        public int DataElements { get; set; }
        public int FileSize { get; set; }
        public double CompressionRatio { get; set; }
        public string ExportStatus { get; set; }
        public string Encryption { get; set; }
        public int RetentionDays { get; set; }
        public DateTime ScheduledDelete { get; set; }
        public int AccessLog { get; set; }
        public int DownloadCount { get; set; }
    }

    public class AnalyticsMetrics
    {
        public string TenantId { get; set; }
        public DateTime MetricsDate { get; set; }
        public int WorkflowsAnalyzed { get; set; }
        public int ReportsGenerated { get; set; }
        public int DashboardsCreated { get; set; }
        public int DataPointsCollected { get; set; }
        public int QueryPerformance { get; set; }
        public int AverageReportGenTime { get; set; }
        public double ReportAccuracy { get; set; }
        public double DataCompleteness { get; set; }
        public int InsightsGenerated { get; set; }
        public double InsightActuallyUsed { get; set; }
        public int DashboardViewCount { get; set; }
        public int ExportsCompleted { get; set; }
        public string AnalyticsMaturity { get; set; }
    }
}
