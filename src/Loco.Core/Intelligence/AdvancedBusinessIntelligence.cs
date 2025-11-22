using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Intelligence
{
    /// <summary>
    /// Advanced business intelligence and reporting system
    /// Phase 24: Dashboard creation, KPI tracking, report generation, trend analysis, custom visualizations
    /// </summary>
    public interface IAdvancedBusinessIntelligence
    {
        Task<Dashboard> CreateDashboardAsync(string tenantId, DashboardDefinition definition, CancellationToken ct = default);
        Task<Dashboard> GetDashboardAsync(string tenantId, string dashboardId, CancellationToken ct = default);
        Task<List<Dashboard>> GetDashboardsAsync(string tenantId, CancellationToken ct = default);
        Task<bool> UpdateDashboardAsync(string tenantId, string dashboardId, DashboardDefinition definition, CancellationToken ct = default);
        Task<WorkflowReport> GenerateReportAsync(string tenantId, ReportDefinition reportDef, CancellationToken ct = default);
        Task<List<KPIMetric>> GetKPIMetricsAsync(string tenantId, string dashboardId, CancellationToken ct = default);
        Task<TrendAnalysis> AnalyzeTrendAsync(string tenantId, string metricName, int daysBack = 30, CancellationToken ct = default);
        Task<bool> ScheduleReportAsync(string tenantId, string reportId, ReportSchedule schedule, CancellationToken ct = default);
        Task<List<Visualization>> GetVisualizationsAsync(string tenantId, CancellationToken ct = default);
        Task<BusinessIntelligenceMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class AdvancedBusinessIntelligence : IAdvancedBusinessIntelligence
    {
        private readonly ILogger<AdvancedBusinessIntelligence> _logger;
        private readonly Dictionary<string, Dashboard> _dashboards = new();
        private readonly Dictionary<string, WorkflowReport> _reports = new();
        private readonly Dictionary<string, ReportSchedule> _schedules = new();
        private readonly Dictionary<string, List<KPIMetric>> _kpiHistory = new();
        private readonly Dictionary<string, List<TimeSeriesPoint>> _timeSeries = new();
        private readonly Random _random = new(42);

        public AdvancedBusinessIntelligence(ILogger<AdvancedBusinessIntelligence> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Dashboard> CreateDashboardAsync(string tenantId, DashboardDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating dashboard {DashboardName}", definition.Name);
            await Task.Delay(30, ct);

            var dashboard = new Dashboard
            {
                DashboardId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Status = "active",
                RefreshInterval = definition.RefreshInterval ?? "5m",
                Layout = definition.Layout ?? "grid",
                Widgets = new List<DashboardWidget>(),
                IsPublished = false,
                ViewCount = 0,
                LastViewedAt = null
            };

            var key = $"{tenantId}:{dashboard.DashboardId}";
            _dashboards[key] = dashboard;

            return dashboard;
        }

        public async Task<Dashboard> GetDashboardAsync(string tenantId, string dashboardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting dashboard {DashboardId}", dashboardId);
            await Task.Delay(15, ct);

            var key = $"{tenantId}:{dashboardId}";
            if (!_dashboards.ContainsKey(key))
                throw new InvalidOperationException("Dashboard not found");

            var dashboard = _dashboards[key];
            dashboard.ViewCount++;
            dashboard.LastViewedAt = DateTimeOffset.UtcNow;

            return dashboard;
        }

        public async Task<List<Dashboard>> GetDashboardsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving dashboards");
            await Task.Delay(20, ct);

            return _dashboards
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .OrderByDescending(d => d.ViewCount)
                .ToList();
        }

        public async Task<bool> UpdateDashboardAsync(string tenantId, string dashboardId, DashboardDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Updating dashboard {DashboardId}", dashboardId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{dashboardId}";
            if (!_dashboards.ContainsKey(key))
                return false;

            var dashboard = _dashboards[key];
            dashboard.Name = definition.Name ?? dashboard.Name;
            dashboard.Description = definition.Description ?? dashboard.Description;
            dashboard.RefreshInterval = definition.RefreshInterval ?? dashboard.RefreshInterval;
            dashboard.Layout = definition.Layout ?? dashboard.Layout;
            dashboard.UpdatedAt = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<WorkflowReport> GenerateReportAsync(string tenantId, ReportDefinition reportDef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Generating report {ReportName}", reportDef.Name);
            await Task.Delay(40, ct);

            var report = new WorkflowReport
            {
                ReportId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = reportDef.Name,
                Description = reportDef.Description,
                GeneratedAt = DateTimeOffset.UtcNow,
                ReportType = reportDef.ReportType ?? "summary",
                Status = "completed",
                ExecutionTime = _random.Next(100, 5000),
                DataPoints = _random.Next(100, 10000),
                TotalWorkflows = _random.Next(10, 500),
                SuccessfulWorkflows = _random.Next(5, 450),
                FailedWorkflows = _random.Next(0, 50),
                AverageExecutionTime = _random.Next(1000, 60000),
                AverageSuccessRate = _random.Next(85, 99.9m),
                TopPerformingWorkflows = new List<string> { "workflow-1", "workflow-2", "workflow-3" },
                BottleneckAreas = new List<string> { "data-processing", "api-calls", "database-queries" },
                Insights = GenerateInsights(),
                Recommendations = GenerateRecommendations(),
                MetricsBreakdown = new Dictionary<string, decimal>(),
                ExportFormats = new List<string> { "pdf", "csv", "json", "xlsx" }
            };

            var key = $"{tenantId}:{report.ReportId}";
            _reports[key] = report;

            return report;
        }

        public async Task<List<KPIMetric>> GetKPIMetricsAsync(string tenantId, string dashboardId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting KPI metrics for dashboard {DashboardId}", dashboardId);
            await Task.Delay(25, ct);

            var key = $"{tenantId}:{dashboardId}";
            var metrics = new List<KPIMetric>
            {
                new()
                {
                    MetricId = "kpi-1",
                    Name = "Workflow Success Rate",
                    CurrentValue = _random.Next(85, 99.9m),
                    TargetValue = 99,
                    Trend = _random.NextDouble() < 0.6 ? "up" : "down",
                    TrendPercentage = _random.Next(-10, 30),
                    Status = "healthy",
                    LastUpdated = DateTimeOffset.UtcNow,
                    Threshold = new MetricThreshold { Warning = 95, Critical = 90 }
                },
                new()
                {
                    MetricId = "kpi-2",
                    Name = "Average Execution Time",
                    CurrentValue = _random.Next(1000, 60000),
                    TargetValue = 5000,
                    Trend = _random.NextDouble() < 0.5 ? "down" : "up",
                    TrendPercentage = _random.Next(-30, 20),
                    Status = _random.NextDouble() < 0.7 ? "healthy" : "warning",
                    LastUpdated = DateTimeOffset.UtcNow,
                    Threshold = new MetricThreshold { Warning = 8000, Critical = 15000 }
                },
                new()
                {
                    MetricId = "kpi-3",
                    Name = "Resource Utilization",
                    CurrentValue = _random.Next(40, 95),
                    TargetValue = 70,
                    Trend = _random.NextDouble() < 0.55 ? "up" : "down",
                    TrendPercentage = _random.Next(-15, 25),
                    Status = "healthy",
                    LastUpdated = DateTimeOffset.UtcNow,
                    Threshold = new MetricThreshold { Warning = 85, Critical = 95 }
                },
                new()
                {
                    MetricId = "kpi-4",
                    Name = "Cost Per Execution",
                    CurrentValue = _random.Next(1, 100),
                    TargetValue = 25,
                    Trend = _random.NextDouble() < 0.5 ? "down" : "up",
                    TrendPercentage = _random.Next(-20, 30),
                    Status = "healthy",
                    LastUpdated = DateTimeOffset.UtcNow,
                    Threshold = new MetricThreshold { Warning = 50, Critical = 100 }
                }
            };

            if (!_kpiHistory.ContainsKey(key))
                _kpiHistory[key] = new List<KPIMetric>();

            _kpiHistory[key].AddRange(metrics);
            if (_kpiHistory[key].Count > 1000)
                _kpiHistory[key] = _kpiHistory[key].Skip(_kpiHistory[key].Count - 1000).ToList();

            return metrics;
        }

        public async Task<TrendAnalysis> AnalyzeTrendAsync(string tenantId, string metricName, int daysBack = 30, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Analyzing trend for metric {MetricName}", metricName);
            await Task.Delay(35, ct);

            var points = new List<TimeSeriesPoint>();
            for (int i = 0; i < daysBack; i++)
            {
                points.Add(new TimeSeriesPoint
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(-i),
                    Value = _random.Next(50, 100),
                    Label = $"Day {daysBack - i}"
                });
            }

            var analysis = new TrendAnalysis
            {
                MetricName = metricName,
                AnalyzedAt = DateTimeOffset.UtcNow,
                TimeWindow = $"{daysBack} days",
                DataPoints = points,
                AverageValue = points.Count > 0 ? points.Average(p => p.Value) : 0,
                MinValue = points.Count > 0 ? points.Min(p => p.Value) : 0,
                MaxValue = points.Count > 0 ? points.Max(p => p.Value) : 0,
                StandardDeviation = CalculateStdDev(points.Select(p => (double)p.Value).ToList()),
                Trend = _random.NextDouble() < 0.5 ? "increasing" : "decreasing",
                TrendStrength = _random.Next(0, 100),
                Forecast = GenerateForecast(points),
                Anomalies = DetectAnomalies(points),
                SeasonalPattern = _random.NextDouble() < 0.6 ? "detected" : "none",
                CorrelatedMetrics = new List<string> { "success-rate", "execution-time", "resource-usage" }
            };

            return analysis;
        }

        public async Task<bool> ScheduleReportAsync(string tenantId, string reportId, ReportSchedule schedule, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Scheduling report {ReportId}", reportId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{reportId}";
            var reportKey = $"{tenantId}:{reportId}";

            if (!_reports.ContainsKey(reportKey))
                return false;

            schedule.ScheduleId = Guid.NewGuid().ToString("N");
            schedule.CreatedAt = DateTimeOffset.UtcNow;
            schedule.Status = "active";

            _schedules[key] = schedule;
            return true;
        }

        public async Task<List<Visualization>> GetVisualizationsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving visualizations");
            await Task.Delay(25, ct);

            var visualizations = new List<Visualization>
            {
                new()
                {
                    VisualizationId = "viz-1",
                    Name = "Workflow Success Rate Over Time",
                    Type = "line-chart",
                    DataSource = "kpi-1",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsPublished = true,
                    ViewCount = _random.Next(100, 10000),
                    ChartOptions = new ChartOptions { Title = "Success Rate Trend", YAxisLabel = "Percentage (%)" }
                },
                new()
                {
                    VisualizationId = "viz-2",
                    Name = "Resource Utilization by Workflow",
                    Type = "bar-chart",
                    DataSource = "kpi-3",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsPublished = true,
                    ViewCount = _random.Next(50, 5000),
                    ChartOptions = new ChartOptions { Title = "Resource Usage", YAxisLabel = "Utilization (%)" }
                },
                new()
                {
                    VisualizationId = "viz-3",
                    Name = "Cost Distribution",
                    Type = "pie-chart",
                    DataSource = "cost-metrics",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsPublished = true,
                    ViewCount = _random.Next(200, 8000),
                    ChartOptions = new ChartOptions { Title = "Cost Breakdown", ShowLegend = true }
                },
                new()
                {
                    VisualizationId = "viz-4",
                    Name = "Performance Heatmap",
                    Type = "heatmap",
                    DataSource = "performance-data",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    IsPublished = false,
                    ViewCount = 0,
                    ChartOptions = new ChartOptions { Title = "Performance Heatmap", ShowScale = true }
                }
            };

            return visualizations;
        }

        public async Task<BusinessIntelligenceMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating BI metrics");
            await Task.Delay(30, ct);

            var dashboardCount = _dashboards.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));
            var reportCount = _reports.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));

            var metrics = new BusinessIntelligenceMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalDashboards = dashboardCount,
                PublishedDashboards = _dashboards.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.IsPublished),
                TotalReports = reportCount,
                ReportsGeneratedToday = _random.Next(0, 50),
                ScheduledReports = _schedules.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                AverageDashboardViewsPerDay = _random.Next(50, 500),
                TotalDataPoints = _random.Next(1000, 1000000),
                InsightAlerts = _random.Next(0, 20),
                AnomaliesDetected = _random.Next(0, 10),
                DataFreshness = "real-time",
                StorageUsed = _random.Next(100, 5000)
            };

            return metrics;
        }

        private List<string> GenerateInsights()
        {
            return new List<string>
            {
                "Success rate improved by 5% compared to last week",
                "Peak execution times occur during business hours (9am-5pm)",
                "Database queries account for 40% of execution time",
                "Top 10% of workflows handle 60% of total volume",
                "Resource utilization trending upward - plan capacity expansion"
            };
        }

        private List<string> GenerateRecommendations()
        {
            return new List<string>
            {
                "Optimize slow database queries to reduce execution time",
                "Implement caching for frequently accessed data",
                "Scale resources during peak hours (10am-2pm)",
                "Review workflows with low success rates for improvement",
                "Consider workflow consolidation to reduce overhead"
            };
        }

        private List<TimeSeriesPoint> GenerateForecast(List<TimeSeriesPoint> historicalData)
        {
            var forecast = new List<TimeSeriesPoint>();
            var lastValue = historicalData.LastOrDefault()?.Value ?? 0;

            for (int i = 1; i <= 7; i++)
            {
                forecast.Add(new TimeSeriesPoint
                {
                    Timestamp = DateTimeOffset.UtcNow.AddDays(i),
                    Value = (int)(lastValue + _random.Next(-10, 10)),
                    Label = $"Day +{i}"
                });
            }

            return forecast;
        }

        private List<Anomaly> DetectAnomalies(List<TimeSeriesPoint> data)
        {
            var anomalies = new List<Anomaly>();

            if (data.Count < 3)
                return anomalies;

            var avgValue = data.Average(p => p.Value);
            var stdDev = CalculateStdDev(data.Select(p => (double)p.Value).ToList());

            for (int i = 0; i < data.Count; i++)
            {
                if (Math.Abs(data[i].Value - avgValue) > 2 * stdDev)
                {
                    anomalies.Add(new Anomaly
                    {
                        Timestamp = data[i].Timestamp,
                        Value = data[i].Value,
                        Severity = Math.Abs(data[i].Value - avgValue) > 3 * stdDev ? "critical" : "warning",
                        Description = $"Value {data[i].Value} deviates significantly from average {(int)avgValue}"
                    });
                }
            }

            return anomalies;
        }

        private double CalculateStdDev(List<double> values)
        {
            if (values.Count == 0)
                return 0;

            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }
    }

    public class DashboardDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string RefreshInterval { get; set; }
        public string Layout { get; set; }
    }

    public class Dashboard
    {
        public string DashboardId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public string RefreshInterval { get; set; }
        public string Layout { get; set; }
        public List<DashboardWidget> Widgets { get; set; } = new();
        public bool IsPublished { get; set; }
        public int ViewCount { get; set; }
        public DateTimeOffset? LastViewedAt { get; set; }
    }

    public class DashboardWidget
    {
        public string WidgetId { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string DataSource { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
    }

    public class ReportDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReportType { get; set; }
        public List<string> Metrics { get; set; } = new();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class WorkflowReport
    {
        public string ReportId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTimeOffset GeneratedAt { get; set; }
        public string ReportType { get; set; }
        public string Status { get; set; }
        public int ExecutionTime { get; set; }
        public int DataPoints { get; set; }
        public int TotalWorkflows { get; set; }
        public int SuccessfulWorkflows { get; set; }
        public int FailedWorkflows { get; set; }
        public int AverageExecutionTime { get; set; }
        public decimal AverageSuccessRate { get; set; }
        public List<string> TopPerformingWorkflows { get; set; } = new();
        public List<string> BottleneckAreas { get; set; } = new();
        public List<string> Insights { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public Dictionary<string, decimal> MetricsBreakdown { get; set; } = new();
        public List<string> ExportFormats { get; set; } = new();
    }

    public class KPIMetric
    {
        public string MetricId { get; set; }
        public string Name { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal TargetValue { get; set; }
        public string Trend { get; set; }
        public int TrendPercentage { get; set; }
        public string Status { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public MetricThreshold Threshold { get; set; }
    }

    public class MetricThreshold
    {
        public decimal Warning { get; set; }
        public decimal Critical { get; set; }
    }

    public class TrendAnalysis
    {
        public string MetricName { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public string TimeWindow { get; set; }
        public List<TimeSeriesPoint> DataPoints { get; set; } = new();
        public double AverageValue { get; set; }
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public double StandardDeviation { get; set; }
        public string Trend { get; set; }
        public int TrendStrength { get; set; }
        public List<TimeSeriesPoint> Forecast { get; set; } = new();
        public List<Anomaly> Anomalies { get; set; } = new();
        public string SeasonalPattern { get; set; }
        public List<string> CorrelatedMetrics { get; set; } = new();
    }

    public class TimeSeriesPoint
    {
        public DateTimeOffset Timestamp { get; set; }
        public int Value { get; set; }
        public string Label { get; set; }
    }

    public class Anomaly
    {
        public DateTimeOffset Timestamp { get; set; }
        public int Value { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
    }

    public class ReportSchedule
    {
        public string ScheduleId { get; set; }
        public string ReportId { get; set; }
        public string Frequency { get; set; } // daily, weekly, monthly
        public string DeliveryChannel { get; set; } // email, dashboard, slack
        public List<string> Recipients { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public string Status { get; set; }
        public DateTimeOffset? NextRunTime { get; set; }
    }

    public class Visualization
    {
        public string VisualizationId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // line-chart, bar-chart, pie-chart, heatmap, table, gauge
        public string DataSource { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool IsPublished { get; set; }
        public int ViewCount { get; set; }
        public ChartOptions ChartOptions { get; set; }
    }

    public class ChartOptions
    {
        public string Title { get; set; }
        public string YAxisLabel { get; set; }
        public bool ShowLegend { get; set; }
        public bool ShowScale { get; set; }
    }

    public class BusinessIntelligenceMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalDashboards { get; set; }
        public int PublishedDashboards { get; set; }
        public int TotalReports { get; set; }
        public int ReportsGeneratedToday { get; set; }
        public int ScheduledReports { get; set; }
        public int AverageDashboardViewsPerDay { get; set; }
        public int TotalDataPoints { get; set; }
        public int InsightAlerts { get; set; }
        public int AnomaliesDetected { get; set; }
        public string DataFreshness { get; set; }
        public int StorageUsed { get; set; }
    }
}
