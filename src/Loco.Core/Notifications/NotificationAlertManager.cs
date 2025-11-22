using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Notifications
{
    /// <summary>
    /// Notification and alert management system for multi-channel delivery
    /// Phase 21: Multi-channel alerts, escalation policies, templates, delivery tracking, deduplication
    /// Create and dispatch alerts, manage escalations, track delivery, optimize notification channels
    /// </summary>
    public interface INotificationAlertManager
    {
        Task<AlertNotification> CreateAlertAsync(string tenantId, AlertDefinition alert, CancellationToken cancellationToken = default);
        Task<bool> SendNotificationAsync(string tenantId, string alertId, NotificationChannel channel, CancellationToken cancellationToken = default);
        Task<NotificationDeliveryStatus> GetDeliveryStatusAsync(string tenantId, string notificationId, CancellationToken cancellationToken = default);
        Task<List<NotificationTemplate>> GetTemplatesAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<bool> CreateTemplateAsync(string tenantId, NotificationTemplate template, CancellationToken cancellationToken = default);
        Task<List<AlertNotification>> GetAlertsAsync(string tenantId, string severity = null, int limit = 100, CancellationToken cancellationToken = default);
        Task<bool> AcknowledgeAlertAsync(string tenantId, string alertId, CancellationToken cancellationToken = default);
        Task<bool> ResolveAlertAsync(string tenantId, string alertId, string resolution, CancellationToken cancellationToken = default);
        Task<EscalationResult> EvaluateEscalationAsync(string tenantId, string alertId, CancellationToken cancellationToken = default);
        Task<NotificationMetrics> GetNotificationMetricsAsync(string tenantId, CancellationToken cancellationToken = default);
    }

    public class NotificationAlertManager : INotificationAlertManager
    {
        private readonly ILogger<NotificationAlertManager> _logger;
        private readonly Dictionary<string, AlertNotification> _alerts = new();
        private readonly Dictionary<string, NotificationDeliveryStatus> _deliveries = new();
        private readonly Dictionary<string, NotificationTemplate> _templates = new();
        private readonly Dictionary<string, EscalationPolicy> _escalationPolicies = new();
        private readonly Random _random = new(42);

        public NotificationAlertManager(ILogger<NotificationAlertManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeDefaultTemplates();
            InitializeDefaultPolicies();
        }

        public async Task<AlertNotification> CreateAlertAsync(string tenantId, AlertDefinition alert, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (alert == null)
                throw new ArgumentNullException(nameof(alert));

            _logger.LogInformation("Creating alert {AlertType} with severity {Severity} for tenant {TenantId}",
                alert.AlertType, alert.Severity, tenantId);

            await Task.Delay(20, cancellationToken);

            var notification = new AlertNotification
            {
                AlertId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                AlertType = alert.AlertType,
                Severity = alert.Severity,
                Title = alert.Title,
                Description = alert.Description,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = "open",
                AcknowledgedAt = null,
                ResolvedAt = null,
                AssignedTo = alert.AssignedTo,
                Tags = alert.Tags ?? new List<string>(),
                Context = alert.Context ?? new Dictionary<string, object>(),
                EscalationCount = 0,
                NotificationChannels = DetermineChannels(alert.Severity),
                DeduplicationKey = GenerateDeduplicationKey(alert)
            };

            var alertKey = $"{tenantId}:{notification.AlertId}";
            _alerts[alertKey] = notification;

            // Keep only last 10,000 alerts per tenant
            var tenantAlerts = _alerts.Where(kvp => kvp.Key.StartsWith($"{tenantId}:")).ToList();
            if (tenantAlerts.Count > 10000)
            {
                var toRemove = tenantAlerts.OrderBy(kvp => _alerts[kvp.Key].CreatedAt).Take(tenantAlerts.Count - 10000).ToList();
                foreach (var kvp in toRemove)
                    _alerts.Remove(kvp.Key);
            }

            return notification;
        }

        public async Task<bool> SendNotificationAsync(string tenantId, string alertId, NotificationChannel channel, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID is required", nameof(alertId));

            _logger.LogInformation("Sending {Channel} notification for alert {AlertId}", channel.ChannelType, alertId);

            await Task.Delay(15, cancellationToken);

            var alertKey = $"{tenantId}:{alertId}";
            if (!_alerts.ContainsKey(alertKey))
                return false;

            var alert = _alerts[alertKey];
            var success = _random.NextDouble() > 0.05; // 95% success rate

            var delivery = new NotificationDeliveryStatus
            {
                NotificationId = Guid.NewGuid().ToString("N"),
                AlertId = alertId,
                Channel = channel.ChannelType,
                Recipient = channel.Recipient,
                Status = success ? "delivered" : "failed",
                SentAt = DateTimeOffset.UtcNow,
                DeliveryTime = _random.Next(50, 1000),
                RetryCount = 0,
                StatusCode = success ? 200 : 500,
                Response = success ? "Sent successfully" : "Delivery failed"
            };

            var deliveryKey = $"{tenantId}:{delivery.NotificationId}";
            _deliveries[deliveryKey] = delivery;

            if (!success)
            {
                alert.EscalationCount++;
            }

            return success;
        }

        public async Task<NotificationDeliveryStatus> GetDeliveryStatusAsync(string tenantId, string notificationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(notificationId))
                throw new ArgumentException("Notification ID is required", nameof(notificationId));

            _logger.LogInformation("Retrieving delivery status for {NotificationId}", notificationId);

            await Task.Delay(10, cancellationToken);

            var deliveryKey = $"{tenantId}:{notificationId}";
            if (!_deliveries.ContainsKey(deliveryKey))
                throw new InvalidOperationException($"Notification '{notificationId}' not found");

            return _deliveries[deliveryKey];
        }

        public async Task<List<NotificationTemplate>> GetTemplatesAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving notification templates for tenant {TenantId}", tenantId);

            await Task.Delay(15, cancellationToken);

            return _templates
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:") || kvp.Key.StartsWith("default:"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        public async Task<bool> CreateTemplateAsync(string tenantId, NotificationTemplate template, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (template == null)
                throw new ArgumentNullException(nameof(template));

            _logger.LogInformation("Creating template {TemplateName} for tenant {TenantId}", template.TemplateName, tenantId);

            await Task.Delay(15, cancellationToken);

            template.TemplateId = Guid.NewGuid().ToString("N");
            template.TenantId = tenantId;
            template.CreatedAt = DateTimeOffset.UtcNow;

            var templateKey = $"{tenantId}:{template.TemplateName}";
            _templates[templateKey] = template;

            return true;
        }

        public async Task<List<AlertNotification>> GetAlertsAsync(string tenantId, string severity = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving alerts for tenant {TenantId} with severity {Severity}", tenantId, severity ?? "all");

            await Task.Delay(25, cancellationToken);

            var query = _alerts
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value);

            if (!string.IsNullOrWhiteSpace(severity))
                query = query.Where(a => a.Severity == severity);

            return query
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task<bool> AcknowledgeAlertAsync(string tenantId, string alertId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID is required", nameof(alertId));

            _logger.LogInformation("Acknowledging alert {AlertId}", alertId);

            await Task.Delay(10, cancellationToken);

            var alertKey = $"{tenantId}:{alertId}";
            if (!_alerts.ContainsKey(alertKey))
                return false;

            var alert = _alerts[alertKey];
            if (alert.Status == "open" || alert.Status == "escalated")
            {
                alert.Status = "acknowledged";
                alert.AcknowledgedAt = DateTimeOffset.UtcNow;
                return true;
            }

            return false;
        }

        public async Task<bool> ResolveAlertAsync(string tenantId, string alertId, string resolution, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID is required", nameof(alertId));

            _logger.LogInformation("Resolving alert {AlertId} with resolution: {Resolution}", alertId, resolution);

            await Task.Delay(15, cancellationToken);

            var alertKey = $"{tenantId}:{alertId}";
            if (!_alerts.ContainsKey(alertKey))
                return false;

            var alert = _alerts[alertKey];
            alert.Status = "resolved";
            alert.ResolvedAt = DateTimeOffset.UtcNow;
            alert.Context["resolution"] = resolution;

            return true;
        }

        public async Task<EscalationResult> EvaluateEscalationAsync(string tenantId, string alertId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(alertId))
                throw new ArgumentException("Alert ID is required", nameof(alertId));

            _logger.LogInformation("Evaluating escalation for alert {AlertId}", alertId);

            await Task.Delay(20, cancellationToken);

            var alertKey = $"{tenantId}:{alertId}";
            if (!_alerts.ContainsKey(alertKey))
                throw new InvalidOperationException($"Alert '{alertId}' not found");

            var alert = _alerts[alertKey];
            var escalation = new EscalationResult
            {
                AlertId = alertId,
                EvaluatedAt = DateTimeOffset.UtcNow,
                CurrentSeverity = alert.Severity,
                ShouldEscalate = ShouldEscalate(alert),
                TimeOpenMinutes = (int)(DateTimeOffset.UtcNow - alert.CreatedAt).TotalMinutes,
                EscalationLevel = DetermineEscalationLevel(alert),
                RecommendedChannels = DetermineChannels(alert.Severity),
                NotificationSent = false,
                Details = new List<string>
                {
                    $"Alert open for {(int)(DateTimeOffset.UtcNow - alert.CreatedAt).TotalMinutes} minutes",
                    $"Current escalation count: {alert.EscalationCount}",
                    $"Severity: {alert.Severity}",
                    $"Status: {alert.Status}"
                }
            };

            // Perform escalation if needed
            if (escalation.ShouldEscalate && alert.Status != "resolved")
            {
                alert.Status = "escalated";
                alert.EscalationCount++;
                escalation.NotificationSent = true;
                escalation.Details.Add($"Alert escalated to level {escalation.EscalationLevel}");
            }

            return escalation;
        }

        public async Task<NotificationMetrics> GetNotificationMetricsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Calculating notification metrics for tenant {TenantId}", tenantId);

            await Task.Delay(35, cancellationToken);

            var alerts = _alerts
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            var deliveries = _deliveries
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            var last24hAlerts = alerts.Where(a => a.CreatedAt >= DateTimeOffset.UtcNow.AddHours(-24)).ToList();
            var last24hDeliveries = deliveries.Where(d => d.SentAt >= DateTimeOffset.UtcNow.AddHours(-24)).ToList();

            var metrics = new NotificationMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalAlerts = alerts.Count,
                OpenAlerts = alerts.Count(a => a.Status == "open"),
                AcknowledgedAlerts = alerts.Count(a => a.Status == "acknowledged"),
                EscalatedAlerts = alerts.Count(a => a.Status == "escalated"),
                ResolvedAlerts = alerts.Count(a => a.Status == "resolved"),
                AlertsBySeverity = new Dictionary<string, int>
                {
                    { "critical", alerts.Count(a => a.Severity == "critical") },
                    { "high", alerts.Count(a => a.Severity == "high") },
                    { "medium", alerts.Count(a => a.Severity == "medium") },
                    { "low", alerts.Count(a => a.Severity == "low") }
                },
                TotalNotificationsSent = deliveries.Count,
                SuccessfulDeliveries = deliveries.Count(d => d.Status == "delivered"),
                FailedDeliveries = deliveries.Count(d => d.Status == "failed"),
                DeliveryRate = deliveries.Count > 0 ? (deliveries.Count(d => d.Status == "delivered") / (double)deliveries.Count) * 100 : 0,
                AverageDeliveryTime = deliveries.Count > 0 ? (int)deliveries.Average(d => d.DeliveryTime) : 0,
                ChannelDistribution = CalculateChannelDistribution(deliveries),
                Last24hAlerts = last24hAlerts.Count,
                Last24hNotifications = last24hDeliveries.Count,
                AverageResolutionTime = CalculateAverageResolutionTime(alerts),
                EscalationRate = alerts.Count > 0 ? (alerts.Count(a => a.EscalationCount > 0) / (double)alerts.Count) * 100 : 0,
                AlertCreationRate = alerts.Count / 24.0 // Per hour
            };

            return metrics;
        }

        private void InitializeDefaultTemplates()
        {
            var defaultTemplates = new[]
            {
                new NotificationTemplate
                {
                    TemplateId = "tpl-1",
                    TenantId = "default",
                    TemplateName = "workflow_failed",
                    Subject = "Workflow {workflow_name} has failed",
                    Body = "Workflow {workflow_name} failed at step {step} with error: {error_message}",
                    Channels = new List<string> { "email", "slack" },
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new NotificationTemplate
                {
                    TemplateId = "tpl-2",
                    TenantId = "default",
                    TemplateName = "quota_exceeded",
                    Subject = "Quota Limit Exceeded",
                    Body = "You have exceeded your quota limit for {resource_type}",
                    Channels = new List<string> { "email", "sms", "webhook" },
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new NotificationTemplate
                {
                    TemplateId = "tpl-3",
                    TenantId = "default",
                    TemplateName = "system_alert",
                    Subject = "System Alert: {alert_type}",
                    Body = "Alert raised: {description}",
                    Channels = new List<string> { "webhook", "slack", "email" },
                    CreatedAt = DateTimeOffset.UtcNow
                }
            };

            foreach (var template in defaultTemplates)
            {
                var key = $"{template.TenantId}:{template.TemplateName}";
                _templates[key] = template;
            }
        }

        private void InitializeDefaultPolicies()
        {
            var defaultPolicies = new[]
            {
                new EscalationPolicy
                {
                    PolicyId = "esc-1",
                    Name = "Critical Alert Policy",
                    Severity = "critical",
                    EscalationTimeMinutes = 5,
                    MaxEscalationLevel = 3,
                    EscalationChannels = new List<string> { "email", "sms", "webhook", "pagerduty" }
                },
                new EscalationPolicy
                {
                    PolicyId = "esc-2",
                    Name = "High Alert Policy",
                    Severity = "high",
                    EscalationTimeMinutes = 15,
                    MaxEscalationLevel = 2,
                    EscalationChannels = new List<string> { "email", "slack", "webhook" }
                },
                new EscalationPolicy
                {
                    PolicyId = "esc-3",
                    Name = "Medium Alert Policy",
                    Severity = "medium",
                    EscalationTimeMinutes = 30,
                    MaxEscalationLevel = 1,
                    EscalationChannels = new List<string> { "email", "webhook" }
                }
            };

            foreach (var policy in defaultPolicies)
            {
                var key = $"default:{policy.Name}";
                _escalationPolicies[key] = policy;
            }
        }

        private List<NotificationChannel> DetermineChannels(string severity)
        {
            return severity switch
            {
                "critical" => new List<NotificationChannel>
                {
                    new() { ChannelType = "email", Recipient = "ops@company.com", Enabled = true },
                    new() { ChannelType = "sms", Recipient = "+1234567890", Enabled = true },
                    new() { ChannelType = "webhook", Recipient = "https://incident.example.com/webhook", Enabled = true }
                },
                "high" => new List<NotificationChannel>
                {
                    new() { ChannelType = "email", Recipient = "team@company.com", Enabled = true },
                    new() { ChannelType = "slack", Recipient = "#alerts", Enabled = true }
                },
                "medium" => new List<NotificationChannel>
                {
                    new() { ChannelType = "email", Recipient = "support@company.com", Enabled = true }
                },
                _ => new List<NotificationChannel>
                {
                    new() { ChannelType = "webhook", Recipient = "https://log.example.com/webhook", Enabled = true }
                }
            };
        }

        private string GenerateDeduplicationKey(AlertDefinition alert)
        {
            return $"{alert.AlertType}:{alert.Severity}:{DateTime.UtcNow:yyyy-MM-dd-HH}";
        }

        private bool ShouldEscalate(AlertNotification alert)
        {
            if (alert.Status == "resolved")
                return false;

            var timeSinceOpen = (int)(DateTimeOffset.UtcNow - alert.CreatedAt).TotalMinutes;

            return alert.Severity switch
            {
                "critical" => timeSinceOpen > 5 || alert.EscalationCount > 2,
                "high" => timeSinceOpen > 15 || alert.EscalationCount > 1,
                "medium" => timeSinceOpen > 30,
                _ => false
            };
        }

        private int DetermineEscalationLevel(AlertNotification alert)
        {
            return alert.Severity switch
            {
                "critical" => Math.Min(3, alert.EscalationCount + 1),
                "high" => Math.Min(2, alert.EscalationCount + 1),
                "medium" => 1,
                _ => 0
            };
        }

        private Dictionary<string, int> CalculateChannelDistribution(List<NotificationDeliveryStatus> deliveries)
        {
            return deliveries
                .GroupBy(d => d.Channel)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private int CalculateAverageResolutionTime(List<AlertNotification> alerts)
        {
            var resolved = alerts.Where(a => a.ResolvedAt.HasValue).ToList();
            if (resolved.Count == 0)
                return 0;

            return (int)resolved.Average(a => (a.ResolvedAt.Value - a.CreatedAt).TotalSeconds);
        }
    }

    // Domain Models
    public class AlertDefinition
    {
        public string AlertType { get; set; }
        public string Severity { get; set; } // critical, high, medium, low
        public string Title { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; }
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class AlertNotification
    {
        public string AlertId { get; set; }
        public string TenantId { get; set; }
        public string AlertType { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? AcknowledgedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public string Status { get; set; } // open, acknowledged, escalated, resolved
        public string AssignedTo { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public int EscalationCount { get; set; }
        public List<NotificationChannel> NotificationChannels { get; set; }
        public string DeduplicationKey { get; set; }
    }

    public class NotificationChannel
    {
        public string ChannelType { get; set; } // email, sms, slack, webhook, pagerduty, in-app
        public string Recipient { get; set; }
        public bool Enabled { get; set; }
    }

    public class NotificationDeliveryStatus
    {
        public string NotificationId { get; set; }
        public string AlertId { get; set; }
        public string Channel { get; set; }
        public string Recipient { get; set; }
        public string Status { get; set; } // delivered, failed, pending, bounced
        public DateTimeOffset SentAt { get; set; }
        public int DeliveryTime { get; set; }
        public int RetryCount { get; set; }
        public int StatusCode { get; set; }
        public string Response { get; set; }
    }

    public class NotificationTemplate
    {
        public string TemplateId { get; set; }
        public string TenantId { get; set; }
        public string TemplateName { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<string> Channels { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
    }

    public class EscalationPolicy
    {
        public string PolicyId { get; set; }
        public string Name { get; set; }
        public string Severity { get; set; }
        public int EscalationTimeMinutes { get; set; }
        public int MaxEscalationLevel { get; set; }
        public List<string> EscalationChannels { get; set; }
    }

    public class EscalationResult
    {
        public string AlertId { get; set; }
        public DateTimeOffset EvaluatedAt { get; set; }
        public string CurrentSeverity { get; set; }
        public bool ShouldEscalate { get; set; }
        public int TimeOpenMinutes { get; set; }
        public int EscalationLevel { get; set; }
        public List<NotificationChannel> RecommendedChannels { get; set; }
        public bool NotificationSent { get; set; }
        public List<string> Details { get; set; }
    }

    public class NotificationMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalAlerts { get; set; }
        public int OpenAlerts { get; set; }
        public int AcknowledgedAlerts { get; set; }
        public int EscalatedAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
        public Dictionary<string, int> AlertsBySeverity { get; set; }
        public int TotalNotificationsSent { get; set; }
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
        public double DeliveryRate { get; set; }
        public int AverageDeliveryTime { get; set; }
        public Dictionary<string, int> ChannelDistribution { get; set; }
        public int Last24hAlerts { get; set; }
        public int Last24hNotifications { get; set; }
        public int AverageResolutionTime { get; set; }
        public double EscalationRate { get; set; }
        public double AlertCreationRate { get; set; }
    }
}
