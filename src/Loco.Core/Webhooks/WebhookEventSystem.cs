// Phase 7: Webhook & Event-Driven Workflow Triggers
// Comprehensive webhook management with reliability, filtering, and event routing
// Enables external systems to trigger workflows via HTTP webhooks

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Webhooks;

/// <summary>
/// Webhook event type
/// </summary>
public class WebhookEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty; // e.g., "order.created", "user.updated"
    public string Source { get; set; } = string.Empty;   // e.g., "shopify", "salesforce"
    public Dictionary<string, object> Payload { get; set; } = new();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? SourceIp { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

/// <summary>
/// Webhook endpoint configuration
/// </summary>
public class WebhookEndpoint
{
    public string EndpointId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Event subscriptions
    public List<string> EventTypes { get; set; } = new(); // e.g., ["order.*", "user.created"]
    public Dictionary<string, object>? EventFilters { get; set; } // Filter by payload properties

    // Authentication
    public string? AuthenticationMethod { get; set; } = "signature"; // signature, api_key, oauth2
    public string? ApiKey { get; set; }  // Encrypted
    public string SigningSecret { get; set; } = GenerateSigningSecret();

    // Configuration
    public int TimeoutSeconds { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public string? LastDeliveryStatus { get; set; }

    private static string GenerateSigningSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

/// <summary>
/// Webhook delivery record
/// </summary>
public class WebhookDelivery
{
    public string DeliveryId { get; set; } = Guid.NewGuid().ToString();
    public string EndpointId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    // Request details
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, object>? Payload { get; set; }
    public Dictionary<string, string>? Headers { get; set; }

    // Response details
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public long ResponseTimeMs { get; set; }

    // Status
    public string Status { get; set; } = "pending"; // pending, delivered, failed, retrying
    public int AttemptNumber { get; set; } = 1;
    public int MaxAttempts { get; set; } = 3

    ;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? AttemptHistory { get; set; }
}

/// <summary>
/// Webhook trigger configuration for workflows
/// </summary>
public class WebhookTrigger
{
    public string TriggerId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    // Event matching
    public string EventPattern { get; set; } = string.Empty; // e.g., "order.*", "user.created"
    public Dictionary<string, object>? PayloadMapping { get; set; } // Map webhook payload to workflow input

    // Execution options
    public bool IsAsync { get; set; } = true;
    public Dictionary<string, object>? DefaultInput { get; set; }
    public bool ThrottleEnabled { get; set; }
    public int ThrottleWindowSeconds { get; set; } = 60;
    public int MaxExecutionsPerWindow { get; set; } = 10;

    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public DateTime? LastExecutionAt { get; set; }
}

/// <summary>
/// Webhook system interface
/// </summary>
public interface IWebhookEventSystem
{
    // Endpoint management
    Task<WebhookEndpoint> RegisterEndpointAsync(
        string tenantId,
        string url,
        List<string> eventTypes,
        CancellationToken ct = default);

    Task<WebhookEndpoint?> GetEndpointAsync(
        string endpointId,
        CancellationToken ct = default);

    Task<List<WebhookEndpoint>> ListEndpointsAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> UpdateEndpointAsync(
        string endpointId,
        WebhookEndpoint endpoint,
        CancellationToken ct = default);

    Task<bool> DeleteEndpointAsync(
        string endpointId,
        CancellationToken ct = default);

    Task<bool> TestEndpointAsync(
        string endpointId,
        CancellationToken ct = default);

    // Event publishing
    Task<List<WebhookDelivery>> PublishEventAsync(
        WebhookEvent webhookEvent,
        CancellationToken ct = default);

    Task<WebhookDelivery?> GetDeliveryAsync(
        string deliveryId,
        CancellationToken ct = default);

    Task<List<WebhookDelivery>> ListDeliveriesAsync(
        string? endpointId = null,
        string? eventType = null,
        int limit = 50,
        CancellationToken ct = default);

    // Trigger management
    Task<WebhookTrigger> CreateTriggerAsync(
        string workflowId,
        string tenantId,
        string eventPattern,
        CancellationToken ct = default);

    Task<List<WebhookTrigger>> GetTriggersForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<bool> DeleteTriggerAsync(
        string triggerId,
        CancellationToken ct = default);

    // Statistics
    Task<Dictionary<string, object>> GetWebhookStatisticsAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);
}

/// <summary>
/// Webhook event system implementation
/// </summary>
public class WebhookEventSystem : IWebhookEventSystem
{
    private readonly ILogger<WebhookEventSystem> _logger;
    private readonly Dictionary<string, WebhookEndpoint> _endpoints;
    private readonly Dictionary<string, WebhookDelivery> _deliveries;
    private readonly Dictionary<string, List<WebhookTrigger>> _workflowTriggers;
    private readonly Dictionary<string, List<DateTime>> _throttleTracking;

    public WebhookEventSystem(ILogger<WebhookEventSystem> logger)
    {
        _logger = logger;
        _endpoints = new Dictionary<string, WebhookEndpoint>();
        _deliveries = new Dictionary<string, WebhookDelivery>();
        _workflowTriggers = new Dictionary<string, List<WebhookTrigger>>();
        _throttleTracking = new Dictionary<string, List<DateTime>>();
    }

    // Endpoint Management
    public async Task<WebhookEndpoint> RegisterEndpointAsync(
        string tenantId,
        string url,
        List<string> eventTypes,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var endpoint = new WebhookEndpoint
        {
            TenantId = tenantId,
            Url = url,
            EventTypes = eventTypes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _endpoints[endpoint.EndpointId] = endpoint;

        _logger.LogInformation(
            "Webhook endpoint registered: {EndpointId}, Tenant: {TenantId}, Url: {Url}",
            endpoint.EndpointId, tenantId, url);

        return endpoint;
    }

    public async Task<WebhookEndpoint?> GetEndpointAsync(
        string endpointId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _endpoints.TryGetValue(endpointId, out var endpoint);
        return endpoint;
    }

    public async Task<List<WebhookEndpoint>> ListEndpointsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _endpoints.Values
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .ToList();
    }

    public async Task<bool> UpdateEndpointAsync(
        string endpointId,
        WebhookEndpoint endpoint,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_endpoints.TryGetValue(endpointId, out var existing))
        {
            return false;
        }

        endpoint.EndpointId = endpointId;
        endpoint.CreatedAt = existing.CreatedAt;
        endpoint.UpdatedAt = DateTime.UtcNow;

        _endpoints[endpointId] = endpoint;

        _logger.LogInformation(
            "Webhook endpoint updated: {EndpointId}",
            endpointId);

        return true;
    }

    public async Task<bool> DeleteEndpointAsync(
        string endpointId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var removed = _endpoints.Remove(endpointId);

        if (removed)
        {
            _logger.LogInformation(
                "Webhook endpoint deleted: {EndpointId}",
                endpointId);
        }

        return removed;
    }

    public async Task<bool> TestEndpointAsync(
        string endpointId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_endpoints.TryGetValue(endpointId, out var endpoint))
        {
            return false;
        }

        // Simulate test webhook delivery
        try
        {
            var testEvent = new WebhookEvent
            {
                EventType = "webhook.test",
                Source = "loco-platform",
                Payload = new Dictionary<string, object>
                {
                    { "test", true },
                    { "timestamp", DateTime.UtcNow }
                }
            };

            var deliveries = await PublishEventAsync(testEvent, ct);
            var success = deliveries.Any(d => d.Status == "delivered");

            endpoint.LastDeliveryStatus = success ? "success" : "failed";
            endpoint.LastDeliveryAt = DateTime.UtcNow;

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook test failed for {EndpointId}", endpointId);
            endpoint.LastDeliveryStatus = "error";
            return false;
        }
    }

    // Event Publishing
    public async Task<List<WebhookDelivery>> PublishEventAsync(
        WebhookEvent webhookEvent,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var deliveries = new List<WebhookDelivery>();

        // Find matching endpoints
        var matchingEndpoints = _endpoints.Values
            .Where(e => e.IsActive)
            .Where(e => MatchesEventPattern(e.EventTypes, webhookEvent.EventType))
            .ToList();

        foreach (var endpoint in matchingEndpoints)
        {
            var delivery = new WebhookDelivery
            {
                EndpointId = endpoint.EndpointId,
                EventId = webhookEvent.EventId,
                EventType = webhookEvent.EventType,
                Url = endpoint.Url,
                Payload = webhookEvent.Payload,
                CreatedAt = DateTime.UtcNow,
                MaxAttempts = endpoint.MaxRetries + 1,
            };

            _deliveries[delivery.DeliveryId] = delivery;
            deliveries.Add(delivery);

            // Trigger delivery attempt
            _ = DeliverWebhookAsync(endpoint, delivery, webhookEvent, ct);
        }

        _logger.LogInformation(
            "Event published: {EventType}, {Count} deliveries queued",
            webhookEvent.EventType, deliveries.Count);

        return deliveries;
    }

    public async Task<WebhookDelivery?> GetDeliveryAsync(
        string deliveryId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _deliveries.TryGetValue(deliveryId, out var delivery);
        return delivery;
    }

    public async Task<List<WebhookDelivery>> ListDeliveriesAsync(
        string? endpointId = null,
        string? eventType = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _deliveries.Values
            .Where(d => endpointId == null || d.EndpointId == endpointId)
            .Where(d => eventType == null || d.EventType == eventType)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToList();

        return results;
    }

    // Trigger Management
    public async Task<WebhookTrigger> CreateTriggerAsync(
        string workflowId,
        string tenantId,
        string eventPattern,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var trigger = new WebhookTrigger
        {
            WorkflowId = workflowId,
            TenantId = tenantId,
            EventPattern = eventPattern,
            CreatedAt = DateTime.UtcNow,
        };

        if (!_workflowTriggers.ContainsKey(workflowId))
        {
            _workflowTriggers[workflowId] = new List<WebhookTrigger>();
        }

        _workflowTriggers[workflowId].Add(trigger);

        _logger.LogInformation(
            "Webhook trigger created: {TriggerId}, Workflow: {WorkflowId}, Pattern: {Pattern}",
            trigger.TriggerId, workflowId, eventPattern);

        return trigger;
    }

    public async Task<List<WebhookTrigger>> GetTriggersForWorkflowAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_workflowTriggers.TryGetValue(workflowId, out var triggers))
        {
            return triggers.Where(t => t.IsActive).ToList();
        }

        return new List<WebhookTrigger>();
    }

    public async Task<bool> DeleteTriggerAsync(
        string triggerId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var triggers in _workflowTriggers.Values)
        {
            var trigger = triggers.FirstOrDefault(t => t.TriggerId == triggerId);
            if (trigger != null)
            {
                trigger.IsActive = false;
                _logger.LogInformation("Webhook trigger deleted: {TriggerId}", triggerId);
                return true;
            }
        }

        return false;
    }

    // Statistics
    public async Task<Dictionary<string, object>> GetWebhookStatisticsAsync(
        string tenantId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate aggregation

        var tenantEndpoints = _endpoints.Values.Where(e => e.TenantId == tenantId).ToList();
        var endpointIds = tenantEndpoints.Select(e => e.EndpointId).ToList();

        var relevantDeliveries = _deliveries.Values
            .Where(d => endpointIds.Contains(d.EndpointId))
            .Where(d => from == null || d.CreatedAt >= from)
            .Where(d => to == null || d.CreatedAt <= to)
            .ToList();

        var delivered = relevantDeliveries.Count(d => d.Status == "delivered");
        var failed = relevantDeliveries.Count(d => d.Status == "failed");

        return new Dictionary<string, object>
        {
            ["total_endpoints"] = tenantEndpoints.Count,
            ["active_endpoints"] = tenantEndpoints.Count(e => e.IsActive),
            ["total_events"] = relevantDeliveries.Count,
            ["delivered_events"] = delivered,
            ["failed_events"] = failed,
            ["success_rate"] = relevantDeliveries.Count > 0
                ? (delivered / (double)relevantDeliveries.Count) * 100
                : 0,
            ["average_response_time_ms"] = relevantDeliveries.Count > 0
                ? (long)relevantDeliveries.Average(d => d.ResponseTimeMs)
                : 0,
        };
    }

    // Private helpers
    private bool MatchesEventPattern(List<string> eventPatterns, string eventType)
    {
        foreach (var pattern in eventPatterns)
        {
            if (pattern == eventType)
                return true;

            // Support wildcards: "order.*" matches "order.created", "order.updated"
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.TrimEnd('*');
                if (eventType.StartsWith(prefix))
                    return true;
            }
        }

        return false;
    }

    private async Task DeliverWebhookAsync(
        WebhookEndpoint endpoint,
        WebhookDelivery delivery,
        WebhookEvent webhookEvent,
        CancellationToken ct)
    {
        try
        {
            // Simulate HTTP delivery
            await Task.Delay(100);

            delivery.Status = "delivered";
            delivery.ResponseStatusCode = 200;
            delivery.DeliveredAt = DateTime.UtcNow;

            endpoint.SuccessCount++;
            endpoint.LastDeliveryAt = DateTime.UtcNow;
            endpoint.LastDeliveryStatus = "success";

            _logger.LogDebug(
                "Webhook delivered: {DeliveryId}, Endpoint: {EndpointId}",
                delivery.DeliveryId, endpoint.EndpointId);
        }
        catch (Exception ex)
        {
            delivery.Status = "failed";
            delivery.ErrorMessage = ex.Message;
            endpoint.FailureCount++;
            endpoint.LastDeliveryStatus = "failed";

            _logger.LogError(
                ex,
                "Webhook delivery failed: {DeliveryId}, Endpoint: {EndpointId}",
                delivery.DeliveryId, endpoint.EndpointId);
        }
    }
}
