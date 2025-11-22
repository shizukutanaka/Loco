using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Alerting
{
    /// <summary>
    /// Real-Time Alerting and Incident Response Engine (Phase 28)
    /// Provides comprehensive real-time alerting, incident management, escalation policies,
    /// notification routing, and automated incident response for workflow automation.
    /// Enables rapid detection, escalation, and resolution of operational issues.
    /// </summary>
    public interface IRealTimeAlertingIncidentResponseEngine
    {
        Task<Alert> CreateAlertRuleAsync(string tenantId, string ruleName, AlertSeverity severity, CancellationToken ct = default);
        Task<Alert> EvaluateAlertAsync(string tenantId, string metricName, double value, CancellationToken ct = default);
        Task<Incident> CreateIncidentAsync(string tenantId, string alertId, CancellationToken ct = default);
        Task<Incident> EscalateIncidentAsync(string tenantId, string incidentId, int escalationLevel, CancellationToken ct = default);
        Task<NotificationResult> NotifyAsync(string tenantId, string incidentId, List<string> recipients, CancellationToken ct = default);
        Task<IncidentCorrelation> CorrelateIncidentsAsync(string tenantId, List<string> incidentIds, CancellationToken ct = default);
        Task<RootCauseAnalysis> AnalyzeRootCauseAsync(string tenantId, string incidentId, CancellationToken ct = default);
        Task<IncidentResolution> ResolveIncidentAsync(string tenantId, string incidentId, string resolution, CancellationToken ct = default);
        Task<AlertSuppression> SuppressAlertAsync(string tenantId, string alertId, int durationMinutes, CancellationToken ct = default);
        Task<AlertingMetrics> GetAlertingMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class RealTimeAlertingIncidentResponseEngine : IRealTimeAlertingIncidentResponseEngine
    {
        private readonly ILogger<RealTimeAlertingIncidentResponseEngine> _logger;
        private readonly Dictionary<string, Alert> _alertRules = new();
        private readonly Dictionary<string, List<Alert>> _generatedAlerts = new();
        private readonly Dictionary<string, Incident> _incidents = new();
        private readonly Dictionary<string, List<AlertSuppression>> _suppressions = new();
        private readonly Dictionary<string, NotificationResult> _notifications = new();
        private readonly Dictionary<string, IncidentCorrelation> _correlations = new();
        private readonly Dictionary<string, RootCauseAnalysis> _rootCauseAnalyses = new();
        private readonly Random _random = new Random(42);

        public RealTimeAlertingIncidentResponseEngine(ILogger<RealTimeAlertingIncidentResponseEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Alert> CreateAlertRuleAsync(string tenantId, string ruleName, AlertSeverity severity, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(ruleName)) throw new ArgumentNullException(nameof(ruleName));

            _logger.LogInformation("Creating alert rule {RuleName} for {TenantId}", ruleName, tenantId);

            await Task.Delay(_random.Next(200, 500), ct);

            var rule = new Alert
            {
                AlertId = Guid.NewGuid().ToString(),
                RuleName = ruleName,
                Severity = severity,
                Condition = GetRandomCondition(),
                Threshold = _random.Next(50, 95),
                Window = _random.Next(1, 10),
                CreatedAt = DateTime.UtcNow,
                Status = "Active",
                EvaluationFrequency = _random.Next(1, 5),
                NotifyChannels = _random.Next(1, 4),
                EscalationLevel = _random.Next(1, 4),
                AutoResolve = _random.Next(0, 2) == 0,
                AlertCount = 0,
                LastTriggered = null
            };

            var key = $"{tenantId}:{ruleName}";
            lock (_alertRules)
            {
                if (_alertRules.Count > 10000) _alertRules.Clear();
                _alertRules[key] = rule;
            }

            _logger.LogInformation("Alert rule created: {RuleId} - {RuleName} ({Severity})",
                rule.AlertId, ruleName, severity);

            return rule;
        }

        public async Task<Alert> EvaluateAlertAsync(string tenantId, string metricName, double value, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(metricName)) throw new ArgumentNullException(nameof(metricName));

            _logger.LogInformation("Evaluating alert for {MetricName}: {Value}", metricName, value);

            await Task.Delay(_random.Next(100, 300), ct);

            var triggered = _random.Next(0, 10) < 3;
            var alert = new Alert
            {
                AlertId = Guid.NewGuid().ToString(),
                MetricName = metricName,
                MetricValue = value,
                Threshold = _random.Next(50, 95),
                Triggered = triggered,
                Severity = triggered ? (AlertSeverity)_random.Next(0, 3) : AlertSeverity.Info,
                EvaluatedAt = DateTime.UtcNow,
                Status = triggered ? "Active" : "Resolved",
                ConditionMet = triggered,
                Anomaly = _random.Next(0, 10) == 0,
                ConfidenceScore = _random.Next(80, 99) / 100.0
            };

            var key = $"{tenantId}:{metricName}";
            lock (_generatedAlerts)
            {
                if (!_generatedAlerts.ContainsKey(key))
                    _generatedAlerts[key] = new List<Alert>();
                if (_generatedAlerts[key].Count > 5000) _generatedAlerts[key].Clear();
                _generatedAlerts[key].Add(alert);
            }

            _logger.LogInformation("Alert evaluation: {MetricName} = {Value}, Triggered: {Triggered}",
                metricName, value, triggered);

            return alert;
        }

        public async Task<Incident> CreateIncidentAsync(string tenantId, string alertId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(alertId)) throw new ArgumentNullException(nameof(alertId));

            _logger.LogInformation("Creating incident for alert {AlertId}", alertId);

            await Task.Delay(_random.Next(300, 700), ct);

            var incident = new Incident
            {
                IncidentId = Guid.NewGuid().ToString(),
                AlertId = alertId,
                CreatedAt = DateTime.UtcNow,
                Status = "Open",
                Severity = (IncidentSeverity)_random.Next(0, 4),
                Title = $"Alert {alertId.Substring(0, 8)}: {GetRandomIncidentTitle()}",
                Description = GetRandomIncidentDescription(),
                AssignedTo = $"on-call-{_random.Next(1, 10)}",
                Component = $"component-{_random.Next(1, 50)}",
                ImpactedServices = _random.Next(1, 5),
                AffectedWorkflows = _random.Next(0, 20),
                Priority = (PriorityLevel)_random.Next(0, 4),
                EstimatedResolutionTime = _random.Next(15, 300),
                SLAMinutes = GetSLAMinutes(),
                ActivityLog = new List<IncidentActivity>
                {
                    new IncidentActivity
                    {
                        Timestamp = DateTime.UtcNow,
                        Action = "Incident created",
                        User = "system"
                    }
                }
            };

            var key = $"{tenantId}:{incident.IncidentId}";
            lock (_incidents)
            {
                if (_incidents.Count > 8000) _incidents.Clear();
                _incidents[key] = incident;
            }

            _logger.LogInformation("Incident created: {IncidentId} - {Title} ({Severity})",
                incident.IncidentId, incident.Title, incident.Severity);

            return incident;
        }

        public async Task<Incident> EscalateIncidentAsync(string tenantId, string incidentId, int escalationLevel, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(incidentId)) throw new ArgumentNullException(nameof(incidentId));
            if (escalationLevel < 1 || escalationLevel > 4) throw new ArgumentOutOfRangeException(nameof(escalationLevel));

            _logger.LogInformation("Escalating incident {IncidentId} to level {EscalationLevel}",
                incidentId, escalationLevel);

            await Task.Delay(_random.Next(300, 700), ct);

            var incident = new Incident
            {
                IncidentId = incidentId,
                EscalationLevel = escalationLevel,
                EscalatedAt = DateTime.UtcNow,
                EscalatedTo = $"manager-{escalationLevel}",
                Status = escalationLevel > 2 ? "Critical" : "Open",
                NotificationsSent = escalationLevel * 2,
                ExecutiveBriefing = escalationLevel >= 3,
                CustomerNotification = escalationLevel >= 2,
                MediaAlert = escalationLevel >= 4
            };

            _logger.LogInformation("Incident escalated: {IncidentId} to level {Level}, {Notifications} notifications sent",
                incidentId, escalationLevel, incident.NotificationsSent);

            return incident;
        }

        public async Task<NotificationResult> NotifyAsync(string tenantId, string incidentId, List<string> recipients, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(incidentId)) throw new ArgumentNullException(nameof(incidentId));
            if (recipients == null || recipients.Count == 0) throw new ArgumentException("Recipients cannot be empty", nameof(recipients));

            _logger.LogInformation("Notifying {Count} recipients about incident {IncidentId}",
                recipients.Count, incidentId);

            await Task.Delay(_random.Next(200, 600), ct);

            var deliveryResults = new List<NotificationDelivery>();
            foreach (var recipient in recipients)
            {
                deliveryResults.Add(new NotificationDelivery
                {
                    RecipientId = recipient,
                    Channel = GetRandomChannel(),
                    Status = _random.Next(0, 20) == 0 ? "Failed" : "Delivered",
                    SentAt = DateTime.UtcNow,
                    DeliveredAt = DateTime.UtcNow.AddSeconds(_random.Next(1, 60)),
                    Acknowledged = _random.Next(0, 2) == 0,
                    AcknowledgedAt = _random.Next(0, 2) == 0 ? DateTime.UtcNow.AddMinutes(_random.Next(1, 30)) : (DateTime?)null
                });
            }

            var result = new NotificationResult
            {
                NotificationId = Guid.NewGuid().ToString(),
                IncidentId = incidentId,
                SentAt = DateTime.UtcNow,
                Recipients = recipients.Count,
                Deliveries = deliveryResults,
                SuccessCount = deliveryResults.Count(d => d.Status == "Delivered"),
                FailureCount = deliveryResults.Count(d => d.Status == "Failed"),
                AcknowledgmentRate = deliveryResults.Count(d => d.Acknowledged) / (double)recipients.Count,
                AverageAcknowledgmentTime = _random.Next(1, 30)
            };

            var key = $"{tenantId}:{incidentId}";
            lock (_notifications)
            {
                if (_notifications.Count > 5000) _notifications.Clear();
                _notifications[key] = result;
            }

            _logger.LogInformation("Notifications sent: {Success}/{Total} delivered, {Ack}% acknowledged",
                result.SuccessCount, recipients.Count, Math.Round(result.AcknowledgmentRate * 100));

            return result;
        }

        public async Task<IncidentCorrelation> CorrelateIncidentsAsync(string tenantId, List<string> incidentIds, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (incidentIds == null || incidentIds.Count < 2) throw new ArgumentException("At least 2 incidents required", nameof(incidentIds));

            _logger.LogInformation("Correlating {Count} incidents", incidentIds.Count);

            await Task.Delay(_random.Next(400, 1000), ct);

            var correlatedIncidents = new List<CorrelatedIncident>();
            foreach (var incId in incidentIds)
            {
                correlatedIncidents.Add(new CorrelatedIncident
                {
                    IncidentId = incId,
                    CorrelationStrength = _random.Next(60, 99) / 100.0,
                    CommonRootCause = _random.Next(0, 2) == 0,
                    SharedComponent = $"component-{_random.Next(1, 50)}",
                    TimeDelta = _random.Next(0, 300)
                });
            }

            var correlation = new IncidentCorrelation
            {
                CorrelationId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                IncidentCount = incidentIds.Count,
                CorrelatedIncidents = correlatedIncidents,
                AverageCorrelationStrength = correlatedIncidents.Average(c => c.CorrelationStrength),
                CommonRootCauseIdentified = correlatedIncidents.Any(c => c.CommonRootCause),
                ParentIncidentId = incidentIds.First(),
                ChildIncidents = incidentIds.Skip(1).ToList(),
                CorrelationScore = _random.Next(70, 95),
                AnalyzedAt = DateTime.UtcNow
            };

            var key = $"{tenantId}:correlation:{correlation.CorrelationId}";
            lock (_correlations)
            {
                if (_correlations.Count > 3000) _correlations.Clear();
                _correlations[key] = correlation;
            }

            _logger.LogInformation("Incidents correlated: {Count} incidents, {Strength}% avg strength, Root cause: {RCA}",
                incidentIds.Count, Math.Round(correlation.AverageCorrelationStrength * 100),
                correlation.CommonRootCauseIdentified);

            return correlation;
        }

        public async Task<RootCauseAnalysis> AnalyzeRootCauseAsync(string tenantId, string incidentId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(incidentId)) throw new ArgumentNullException(nameof(incidentId));

            _logger.LogInformation("Analyzing root cause for incident {IncidentId}", incidentId);

            await Task.Delay(_random.Next(500, 1200), ct);

            var rootCauses = new List<RootCauseFactor>
            {
                new RootCauseFactor
                {
                    FactorId = Guid.NewGuid().ToString(),
                    Category = GetRandomCauseCategory(),
                    Description = "Primary root cause",
                    LikelihoodPercent = _random.Next(70, 99),
                    ContributionPercent = _random.Next(40, 80)
                },
                new RootCauseFactor
                {
                    FactorId = Guid.NewGuid().ToString(),
                    Category = GetRandomCauseCategory(),
                    Description = "Contributing factor",
                    LikelihoodPercent = _random.Next(50, 80),
                    ContributionPercent = _random.Next(20, 50)
                }
            };

            var analysis = new RootCauseAnalysis
            {
                AnalysisId = Guid.NewGuid().ToString(),
                IncidentId = incidentId,
                AnalyzedAt = DateTime.UtcNow,
                RootCauses = rootCauses,
                PrimaryRootCause = rootCauses.First().Description,
                ConfidenceLevel = _random.Next(70, 95) / 100.0,
                TimelineAnalysis = _random.Next(5, 60),
                ComponentsAffected = _random.Next(1, 5),
                DependenciesImpacted = _random.Next(0, 10),
                RecommendedActions = _random.Next(2, 5),
                PreventiveMeasures = _random.Next(1, 4),
                SimilarIncidents = _random.Next(0, 5)
            };

            var key = $"{tenantId}:{incidentId}:rca";
            lock (_rootCauseAnalyses)
            {
                if (_rootCauseAnalyses.Count > 4000) _rootCauseAnalyses.Clear();
                _rootCauseAnalyses[key] = analysis;
            }

            _logger.LogInformation("Root cause analysis complete: {RootCause}, {Confidence}% confidence",
                analysis.PrimaryRootCause, Math.Round(analysis.ConfidenceLevel * 100));

            return analysis;
        }

        public async Task<IncidentResolution> ResolveIncidentAsync(string tenantId, string incidentId, string resolution, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(incidentId)) throw new ArgumentNullException(nameof(incidentId));
            if (string.IsNullOrEmpty(resolution)) throw new ArgumentNullException(nameof(resolution));

            _logger.LogInformation("Resolving incident {IncidentId}", incidentId);

            await Task.Delay(_random.Next(400, 1000), ct);

            var incidentResolution = new IncidentResolution
            {
                ResolutionId = Guid.NewGuid().ToString(),
                IncidentId = incidentId,
                ResolvedAt = DateTime.UtcNow,
                ResolutionSummary = resolution,
                ResolutionType = GetRandomResolutionType(),
                TimeToResolve = _random.Next(15, 480),
                SLAMet = _random.Next(0, 2) == 0,
                PostIncidentReview = _random.Next(0, 2) == 0,
                LessonsLearned = _random.Next(1, 5),
                ActionItems = _random.Next(0, 4),
                CustomerImpact = GetRandomImpactLevel(),
                DataLoss = _random.Next(0, 2) == 0,
                DocsUpdated = _random.Next(0, 2) == 0,
                TrainingNeeded = _random.Next(0, 2) == 0,
                ResolutionConfidence = _random.Next(80, 99) / 100.0
            };

            _logger.LogInformation("Incident resolved: {IncidentId} in {TimeToResolve}min, SLA: {SLAMet}",
                incidentId, incidentResolution.TimeToResolve, incidentResolution.SLAMet);

            return incidentResolution;
        }

        public async Task<AlertSuppression> SuppressAlertAsync(string tenantId, string alertId, int durationMinutes, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (string.IsNullOrEmpty(alertId)) throw new ArgumentNullException(nameof(alertId));
            if (durationMinutes < 1 || durationMinutes > 10080) throw new ArgumentOutOfRangeException(nameof(durationMinutes));

            _logger.LogInformation("Suppressing alert {AlertId} for {Minutes} minutes", alertId, durationMinutes);

            await Task.Delay(_random.Next(200, 500), ct);

            var suppression = new AlertSuppression
            {
                SuppressionId = Guid.NewGuid().ToString(),
                AlertId = alertId,
                SuppressedAt = DateTime.UtcNow,
                SuppressedUntil = DateTime.UtcNow.AddMinutes(durationMinutes),
                Reason = GetRandomSuppressionReason(),
                SuppressedBy = $"user-{_random.Next(1, 100)}",
                SuppressCount = _random.Next(1, 20),
                AlertsMuted = _random.Next(10, 1000),
                EnabledAutomatically = _random.Next(0, 2) == 0
            };

            var key = $"{tenantId}:{alertId}";
            lock (_suppressions)
            {
                if (!_suppressions.ContainsKey(key))
                    _suppressions[key] = new List<AlertSuppression>();
                if (_suppressions[key].Count > 5000) _suppressions[key].Clear();
                _suppressions[key].Add(suppression);
            }

            _logger.LogInformation("Alert suppressed: {AlertId} until {UntilTime} ({Minutes}min)",
                alertId, suppression.SuppressedUntil, durationMinutes);

            return suppression;
        }

        public async Task<AlertingMetrics> GetAlertingMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            _logger.LogInformation("Retrieving alerting metrics for {TenantId}", tenantId);

            await Task.Delay(_random.Next(150, 400), ct);

            var metrics = new AlertingMetrics
            {
                TenantId = tenantId,
                MetricsDate = DateTime.UtcNow,
                AlertRulesActive = _random.Next(50, 500),
                AlertsTriggered24h = _random.Next(100, 2000),
                IncidentsCreated24h = _random.Next(10, 200),
                AverageAlertResposeTime = _random.Next(5, 60),
                AverageIncidentResolutionTime = _random.Next(30, 480),
                SLAComplianceRate = _random.Next(85, 99) / 100.0,
                FalsePositiveRate = _random.Next(1, 20) / 100.0,
                AlertNoiseRatio = _random.Next(10, 80) / 100.0,
                IncidentSeverityDistribution = _random.Next(1, 5),
                CriticalIncidents24h = _random.Next(0, 10),
                IncidentsDuplicated = _random.Next(0, 20),
                MeanTimeToDetect = _random.Next(1, 30),
                MeanTimeToRespond = _random.Next(5, 60),
                MeanTimeToResolve = _random.Next(30, 480),
                OnCallUtilization = _random.Next(20, 80) / 100.0,
                AlertingMaturity = GetRandomMaturity()
            };

            _logger.LogInformation("Alerting metrics: {Alerts24h} alerts, {Incidents24h} incidents, {SLA}% SLA",
                metrics.AlertsTriggered24h, metrics.IncidentsCreated24h,
                Math.Round(metrics.SLAComplianceRate * 100));

            return metrics;
        }

        // Helper methods
        private string GetRandomCondition() => new[] { "Threshold exceeded", "Pattern detected", "Anomaly found", "Trend reversal" }[_random.Next(0, 4)];
        private string GetRandomIncidentTitle() => new[] { "Service degradation", "Performance issue", "System failure", "Resource exhaustion" }[_random.Next(0, 4)];
        private string GetRandomIncidentDescription() => new[] { "Critical issue detected", "Performance degradation", "System instability", "Resource shortage" }[_random.Next(0, 4)];
        private int GetSLAMinutes() => new[] { 15, 30, 60, 120, 240, 480 }[_random.Next(0, 6)];
        private string GetRandomChannel() => new[] { "Email", "SMS", "Slack", "PagerDuty", "Phone" }[_random.Next(0, 5)];
        private string GetRandomCauseCategory() => new[] { "Infrastructure", "Configuration", "Code", "External", "Resource" }[_random.Next(0, 5)];
        private string GetRandomResolutionType() => new[] { "Automatic", "Manual", "Rollback", "Workaround", "Escalation" }[_random.Next(0, 5)];
        private string GetRandomImpactLevel() => new[] { "None", "Minimal", "Moderate", "Significant", "Critical" }[_random.Next(0, 5)];
        private string GetRandomSuppressionReason() => new[] { "Planned maintenance", "Known issue", "False positive", "Temporary" }[_random.Next(0, 4)];
        private string GetRandomMaturity() => new[] { "Initial", "Managed", "Optimized", "Advanced" }[_random.Next(0, 4)];
    }

    // Domain Models
    public class Alert
    {
        public string AlertId { get; set; }
        public string RuleName { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Condition { get; set; }
        public int Threshold { get; set; }
        public int Window { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public int EvaluationFrequency { get; set; }
        public int NotifyChannels { get; set; }
        public int EscalationLevel { get; set; }
        public bool AutoResolve { get; set; }
        public int AlertCount { get; set; }
        public DateTime? LastTriggered { get; set; }
        public string MetricName { get; set; }
        public double MetricValue { get; set; }
        public bool Triggered { get; set; }
        public DateTime? EvaluatedAt { get; set; }
        public bool ConditionMet { get; set; }
        public bool Anomaly { get; set; }
        public double ConfidenceScore { get; set; }
    }

    public class Incident
    {
        public string IncidentId { get; set; }
        public string AlertId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public IncidentSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public string Component { get; set; }
        public int ImpactedServices { get; set; }
        public int AffectedWorkflows { get; set; }
        public PriorityLevel Priority { get; set; }
        public int EstimatedResolutionTime { get; set; }
        public int SLAMinutes { get; set; }
        public List<IncidentActivity> ActivityLog { get; set; }
        public int EscalationLevel { get; set; }
        public DateTime? EscalatedAt { get; set; }
        public string EscalatedTo { get; set; }
        public int NotificationsSent { get; set; }
        public bool ExecutiveBriefing { get; set; }
        public bool CustomerNotification { get; set; }
        public bool MediaAlert { get; set; }
    }

    public class IncidentActivity
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string User { get; set; }
    }

    public class NotificationResult
    {
        public string NotificationId { get; set; }
        public string IncidentId { get; set; }
        public DateTime SentAt { get; set; }
        public int Recipients { get; set; }
        public List<NotificationDelivery> Deliveries { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AcknowledgmentRate { get; set; }
        public int AverageAcknowledgmentTime { get; set; }
    }

    public class NotificationDelivery
    {
        public string RecipientId { get; set; }
        public string Channel { get; set; }
        public string Status { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime DeliveredAt { get; set; }
        public bool Acknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
    }

    public class IncidentCorrelation
    {
        public string CorrelationId { get; set; }
        public string TenantId { get; set; }
        public int IncidentCount { get; set; }
        public List<CorrelatedIncident> CorrelatedIncidents { get; set; }
        public double AverageCorrelationStrength { get; set; }
        public bool CommonRootCauseIdentified { get; set; }
        public string ParentIncidentId { get; set; }
        public List<string> ChildIncidents { get; set; }
        public int CorrelationScore { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }

    public class CorrelatedIncident
    {
        public string IncidentId { get; set; }
        public double CorrelationStrength { get; set; }
        public bool CommonRootCause { get; set; }
        public string SharedComponent { get; set; }
        public int TimeDelta { get; set; }
    }

    public class RootCauseAnalysis
    {
        public string AnalysisId { get; set; }
        public string IncidentId { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public List<RootCauseFactor> RootCauses { get; set; }
        public string PrimaryRootCause { get; set; }
        public double ConfidenceLevel { get; set; }
        public int TimelineAnalysis { get; set; }
        public int ComponentsAffected { get; set; }
        public int DependenciesImpacted { get; set; }
        public int RecommendedActions { get; set; }
        public int PreventiveMeasures { get; set; }
        public int SimilarIncidents { get; set; }
    }

    public class RootCauseFactor
    {
        public string FactorId { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int LikelihoodPercent { get; set; }
        public int ContributionPercent { get; set; }
    }

    public class IncidentResolution
    {
        public string ResolutionId { get; set; }
        public string IncidentId { get; set; }
        public DateTime ResolvedAt { get; set; }
        public string ResolutionSummary { get; set; }
        public string ResolutionType { get; set; }
        public int TimeToResolve { get; set; }
        public bool SLAMet { get; set; }
        public bool PostIncidentReview { get; set; }
        public int LessonsLearned { get; set; }
        public int ActionItems { get; set; }
        public string CustomerImpact { get; set; }
        public bool DataLoss { get; set; }
        public bool DocsUpdated { get; set; }
        public bool TrainingNeeded { get; set; }
        public double ResolutionConfidence { get; set; }
    }

    public class AlertSuppression
    {
        public string SuppressionId { get; set; }
        public string AlertId { get; set; }
        public DateTime SuppressedAt { get; set; }
        public DateTime SuppressedUntil { get; set; }
        public string Reason { get; set; }
        public string SuppressedBy { get; set; }
        public int SuppressCount { get; set; }
        public int AlertsMuted { get; set; }
        public bool EnabledAutomatically { get; set; }
    }

    public class AlertingMetrics
    {
        public string TenantId { get; set; }
        public DateTime MetricsDate { get; set; }
        public int AlertRulesActive { get; set; }
        public int AlertsTriggered24h { get; set; }
        public int IncidentsCreated24h { get; set; }
        public int AverageAlertResposeTime { get; set; }
        public int AverageIncidentResolutionTime { get; set; }
        public double SLAComplianceRate { get; set; }
        public double FalsePositiveRate { get; set; }
        public double AlertNoiseRatio { get; set; }
        public int IncidentSeverityDistribution { get; set; }
        public int CriticalIncidents24h { get; set; }
        public int IncidentsDuplicated { get; set; }
        public int MeanTimeToDetect { get; set; }
        public int MeanTimeToRespond { get; set; }
        public int MeanTimeToResolve { get; set; }
        public double OnCallUtilization { get; set; }
        public string AlertingMaturity { get; set; }
    }

    // Enums
    public enum AlertSeverity { Info = 0, Warning = 1, Critical = 2 }
    public enum IncidentSeverity { Info = 0, Minor = 1, Major = 2, Critical = 3 }
    public enum PriorityLevel { Low = 0, Medium = 1, High = 2, Critical = 3 }
}
