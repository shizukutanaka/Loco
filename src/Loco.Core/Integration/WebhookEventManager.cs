using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.Integration
{
    /// <summary>
    /// Webhook event management system
    /// Phase 20: Event-driven integration with external systems
    /// Register webhooks, trigger events, retry delivery, track status
    /// </summary>
    public interface IWebhookEventManager
    {
        Task<WebhookRegistration> RegisterWebhookAsync(string tenantId, WebhookConfig config, CancellationToken cancellationToken = default);
        Task<WebhookConfig> GetWebhookAsync(string tenantId, string webhookId, CancellationToken cancellationToken = default);
        Task<List<WebhookConfig>> GetTenantWebhooksAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<bool> UpdateWebhookAsync(string tenantId, string webhookId, WebhookConfig updatedConfig, CancellationToken cancellationToken = default);
        Task<bool> DeleteWebhookAsync(string tenantId, string webhookId, CancellationToken cancellationToken = default);
        Task<EventDelivery> TriggerEventAsync(string tenantId, string eventType, object eventData, CancellationToken cancellationToken = default);
        Task<WebhookDeliveryStatus> GetDeliveryStatusAsync(string tenantId, string deliveryId, CancellationToken cancellationToken = default);
        Task<List<WebhookDeliveryStatus>> GetDeliveryHistoryAsync(string tenantId, string webhookId, int limit = 100, CancellationToken cancellationToken = default);
        Task<WebhookMetrics> GetWebhookMetricsAsync(string tenantId, CancellationToken cancellationToken = default);
    }

    public class WebhookEventManager : IWebhookEventManager
    {
        private readonly ILogger<WebhookEventManager> _logger;
        private readonly Dictionary<string, WebhookConfig> _webhooks = new();
        private readonly Dictionary<string, List<WebhookDeliveryStatus>> _deliveries = new();
        private readonly Random _random = new(42);

        public WebhookEventManager(ILogger<WebhookEventManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WebhookRegistration> RegisterWebhookAsync(string tenantId, WebhookConfig config, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _logger.LogInformation("Registering webhook for {EventType} on {Url}", config.EventType, config.Url);

            await Task.Delay(50, cancellationToken);

            var webhookId = Guid.NewGuid().ToString("N");
            config.Id = webhookId;
            config.TenantId = tenantId;
            config.CreatedAt = DateTimeOffset.UtcNow;
            config.Status = "active";
            config.Secret = GenerateSecret();

            var key = $"{tenantId}:{webhookId}";
            _webhooks[key] = config;

            var registration = new WebhookRegistration
            {
                WebhookId = webhookId,
                TenantId = tenantId,
                EventType = config.EventType,
                Url = config.Url,
                RegisteredAt = config.CreatedAt,
                Status = "active",
                Secret = config.Secret,
                TestUrl = $"https://loco.app/webhooks/{webhookId}/test"
            };

            return registration;
        }

        public async Task<WebhookConfig> GetWebhookAsync(string tenantId, string webhookId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(webhookId))
                throw new ArgumentException("Webhook ID is required", nameof(webhookId));

            _logger.LogInformation("Retrieving webhook {WebhookId}", webhookId);

            await Task.Delay(20, cancellationToken);

            var key = $"{tenantId}:{webhookId}";
            if (!_webhooks.ContainsKey(key))
                throw new InvalidOperationException($"Webhook '{webhookId}' not found");

            return _webhooks[key];
        }

        public async Task<List<WebhookConfig>> GetTenantWebhooksAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving webhooks for tenant {TenantId}", tenantId);

            await Task.Delay(30, cancellationToken);

            return _webhooks
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();
        }

        public async Task<bool> UpdateWebhookAsync(string tenantId, string webhookId, WebhookConfig updatedConfig, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (updatedConfig == null)
                throw new ArgumentNullException(nameof(updatedConfig));

            _logger.LogInformation("Updating webhook {WebhookId}", webhookId);

            await Task.Delay(30, cancellationToken);

            var key = $"{tenantId}:{webhookId}";
            if (!_webhooks.ContainsKey(key))
                return false;

            var existing = _webhooks[key];
            existing.Url = updatedConfig.Url;
            existing.EventType = updatedConfig.EventType;
            existing.Active = updatedConfig.Active;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<bool> DeleteWebhookAsync(string tenantId, string webhookId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(webhookId))
                throw new ArgumentException("Webhook ID is required", nameof(webhookId));

            _logger.LogInformation("Deleting webhook {WebhookId}", webhookId);

            await Task.Delay(20, cancellationToken);

            var key = $"{tenantId}:{webhookId}";
            if (!_webhooks.ContainsKey(key))
                return false;

            _webhooks.Remove(key);
            return true;
        }

        public async Task<EventDelivery> TriggerEventAsync(string tenantId, string eventType, object eventData, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException("Event type is required", nameof(eventType));

            _logger.LogInformation("Triggering event {EventType} for tenant {TenantId}", eventType, tenantId);

            await Task.Delay(50, cancellationToken);

            var delivery = new EventDelivery
            {
                EventId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                EventType = eventType,
                TriggeredAt = DateTimeOffset.UtcNow,
                EventData = eventData,
                Webhooks = new List<WebhookDeliveryStatus>()
            };

            // Find matching webhooks
            var matchingWebhooks = _webhooks
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.EventType == eventType && kvp.Value.Active)
                .Select(kvp => kvp.Value)
                .ToList();

            // Deliver to each webhook
            foreach (var webhook in matchingWebhooks)
            {
                var deliveryStatus = await DeliverToWebhookAsync(tenantId, webhook, eventType, eventData, cancellationToken);
                delivery.Webhooks.Add(deliveryStatus);

                // Track delivery
                var deliveryKey = $"{tenantId}:{webhook.Id}";
                if (!_deliveries.ContainsKey(deliveryKey))
                    _deliveries[deliveryKey] = new List<WebhookDeliveryStatus>();

                _deliveries[deliveryKey].Add(deliveryStatus);
            }

            delivery.DeliveryCount = delivery.Webhooks.Count;
            delivery.SuccessCount = delivery.Webhooks.Count(w => w.Status == "delivered");
            delivery.FailureCount = delivery.Webhooks.Count(w => w.Status == "failed");

            return delivery;
        }

        public async Task<WebhookDeliveryStatus> GetDeliveryStatusAsync(string tenantId, string deliveryId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(deliveryId))
                throw new ArgumentException("Delivery ID is required", nameof(deliveryId));

            _logger.LogInformation("Getting delivery status {DeliveryId}", deliveryId);

            await Task.Delay(20, cancellationToken);

            var allDeliveries = _deliveries
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .FirstOrDefault(d => d.DeliveryId == deliveryId);

            if (allDeliveries == null)
                throw new InvalidOperationException($"Delivery '{deliveryId}' not found");

            return allDeliveries;
        }

        public async Task<List<WebhookDeliveryStatus>> GetDeliveryHistoryAsync(string tenantId, string webhookId, int limit = 100, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(webhookId))
                throw new ArgumentException("Webhook ID is required", nameof(webhookId));

            _logger.LogInformation("Retrieving delivery history for webhook {WebhookId}", webhookId);

            await Task.Delay(40, cancellationToken);

            var key = $"{tenantId}:{webhookId}";
            if (!_deliveries.ContainsKey(key))
                return new List<WebhookDeliveryStatus>();

            return _deliveries[key]
                .OrderByDescending(d => d.DeliveredAt)
                .Take(limit)
                .ToList();
        }

        public async Task<WebhookMetrics> GetWebhookMetricsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Calculating webhook metrics for tenant {TenantId}", tenantId);

            await Task.Delay(60, cancellationToken);

            var tenantWebhooks = _webhooks.Where(kvp => kvp.Key.StartsWith($"{tenantId}:")).Count();
            var allDeliveries = _deliveries
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .SelectMany(kvp => kvp.Value)
                .ToList();

            var metrics = new WebhookMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalWebhooks = tenantWebhooks,
                ActiveWebhooks = _webhooks.Where(kvp => kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Active).Count(),
                TotalDeliveries = allDeliveries.Count,
                SuccessfulDeliveries = allDeliveries.Count(d => d.Status == "delivered"),
                FailedDeliveries = allDeliveries.Count(d => d.Status == "failed"),
                AverageDeliveryTime = allDeliveries.Count > 0 ? allDeliveries.Average(d => d.DeliveryTime) : 0,
                SuccessRate = allDeliveries.Count > 0 ? (allDeliveries.Count(d => d.Status == "delivered") / (double)allDeliveries.Count) * 100 : 0,
                RetryCount = allDeliveries.Sum(d => d.RetryCount),
                Last24hDeliveries = allDeliveries.Count(d => d.DeliveredAt >= DateTimeOffset.UtcNow.AddHours(-24))
            };

            return metrics;
        }

        private async Task<WebhookDeliveryStatus> DeliverToWebhookAsync(string tenantId, WebhookConfig webhook, string eventType, object eventData, CancellationToken cancellationToken)
        {
            await Task.Delay(10, cancellationToken);

            var status = "delivered";
            var statusCode = 200;
            var retries = 0;

            // Simulate delivery with possibility of failure
            if (_random.NextDouble() < 0.05) // 5% failure rate
            {
                status = "failed";
                statusCode = 500;
            }

            return new WebhookDeliveryStatus
            {
                DeliveryId = Guid.NewGuid().ToString("N"),
                WebhookId = webhook.Id,
                EventType = eventType,
                Url = webhook.Url,
                DeliveredAt = DateTimeOffset.UtcNow,
                Status = status,
                StatusCode = statusCode,
                DeliveryTime = _random.Next(50, 500),
                RetryCount = retries,
                Response = status == "delivered" ? "OK" : "Internal Server Error"
            };
        }

        private string GenerateSecret()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 32);
        }
    }

    // Domain Models
    public class WebhookConfig
    {
        public string Id { get; set; }
        public string TenantId { get; set; }
        public string Url { get; set; }
        public string EventType { get; set; }
        public bool Active { get; set; } = true;
        public string Secret { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public string Status { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public List<string> Events { get; set; } = new();
    }

    public class WebhookRegistration
    {
        public string WebhookId { get; set; }
        public string TenantId { get; set; }
        public string EventType { get; set; }
        public string Url { get; set; }
        public DateTimeOffset RegisteredAt { get; set; }
        public string Status { get; set; }
        public string Secret { get; set; }
        public string TestUrl { get; set; }
    }

    public class EventDelivery
    {
        public string EventId { get; set; }
        public string TenantId { get; set; }
        public string EventType { get; set; }
        public DateTimeOffset TriggeredAt { get; set; }
        public object EventData { get; set; }
        public List<WebhookDeliveryStatus> Webhooks { get; set; }
        public int DeliveryCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }

    public class WebhookDeliveryStatus
    {
        public string DeliveryId { get; set; }
        public string WebhookId { get; set; }
        public string EventType { get; set; }
        public string Url { get; set; }
        public DateTimeOffset DeliveredAt { get; set; }
        public string Status { get; set; } // "delivered", "failed", "pending"
        public int StatusCode { get; set; }
        public int DeliveryTime { get; set; } // milliseconds
        public int RetryCount { get; set; }
        public string Response { get; set; }
    }

    public class WebhookMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalWebhooks { get; set; }
        public int ActiveWebhooks { get; set; }
        public int TotalDeliveries { get; set; }
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
        public double AverageDeliveryTime { get; set; }
        public double SuccessRate { get; set; }
        public int RetryCount { get; set; }
        public int Last24hDeliveries { get; set; }
    }
}
