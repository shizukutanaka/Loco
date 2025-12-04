// John Carmack: "If you can't explain it simply, you don't understand it well enough"
// Rob Pike: "Clear is better than clever"

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Integrations.Core;

/// <summary>
/// Webhook receiver for handling incoming trigger events from external services
/// Provides signature validation, routing, and event dispatching
/// </summary>
public sealed class WebhookReceiver : IDisposable
{
    private readonly ConcurrentDictionary<string, WebhookRegistration> _registrations = new();
    private readonly ConcurrentDictionary<string, WebhookSecret> _secrets = new();
    private bool _disposed;

    /// <summary>
    /// Event raised when a webhook is received and validated
    /// </summary>
    public event Func<WebhookEvent, CancellationToken, Task>? OnWebhookReceived;

    /// <summary>
    /// Register a webhook endpoint for a trigger
    /// </summary>
    public WebhookEndpoint RegisterWebhook(WebhookRegistrationRequest request)
    {
        var id = GenerateWebhookId();
        var secret = GenerateSecret();
        var path = request.CustomPath ?? $"/webhooks/{request.ConnectorId}/{id}";

        var registration = new WebhookRegistration
        {
            Id = id,
            ConnectorId = request.ConnectorId,
            TriggerId = request.TriggerId,
            WorkflowId = request.WorkflowId,
            Path = path,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            Filters = request.Filters ?? new Dictionary<string, string>()
        };

        _registrations[id] = registration;
        _secrets[id] = new WebhookSecret { Secret = secret, Algorithm = request.SignatureAlgorithm };

        return new WebhookEndpoint
        {
            Id = id,
            Path = path,
            Secret = secret,
            SignatureHeader = GetSignatureHeader(request.ConnectorId)
        };
    }

    /// <summary>
    /// Unregister a webhook endpoint
    /// </summary>
    public bool UnregisterWebhook(string webhookId)
    {
        _secrets.TryRemove(webhookId, out _);
        return _registrations.TryRemove(webhookId, out _);
    }

    /// <summary>
    /// Process an incoming webhook request
    /// </summary>
    public async Task<WebhookProcessResult> ProcessWebhookAsync(
        string path,
        string method,
        Dictionary<string, string> headers,
        string body,
        CancellationToken ct = default)
    {
        // Find matching registration
        var registration = FindRegistration(path);
        if (registration == null)
        {
            return WebhookProcessResult.NotFound("No webhook registered for this path");
        }

        // Check expiration
        if (registration.ExpiresAt.HasValue && registration.ExpiresAt < DateTime.UtcNow)
        {
            _registrations.TryRemove(registration.Id, out _);
            return WebhookProcessResult.Expired("Webhook has expired");
        }

        // Validate signature
        if (_secrets.TryGetValue(registration.Id, out var secretInfo))
        {
            var signatureHeader = GetSignatureHeader(registration.ConnectorId);
            if (headers.TryGetValue(signatureHeader, out var signature))
            {
                if (!ValidateSignature(body, signature, secretInfo))
                {
                    return WebhookProcessResult.InvalidSignature("Signature validation failed");
                }
            }
        }

        // Parse and create event
        JsonElement? payload = null;
        try
        {
            if (!string.IsNullOrEmpty(body))
            {
                payload = JsonSerializer.Deserialize<JsonElement>(body);
            }
        }
        catch
        {
            // Body might not be JSON, that's OK for some webhooks
        }

        var webhookEvent = new WebhookEvent
        {
            Id = Guid.NewGuid().ToString(),
            WebhookId = registration.Id,
            ConnectorId = registration.ConnectorId,
            TriggerId = registration.TriggerId,
            WorkflowId = registration.WorkflowId,
            ReceivedAt = DateTime.UtcNow,
            Headers = headers,
            RawBody = body,
            Payload = payload,
            Method = method
        };

        // Apply filters
        if (!ApplyFilters(webhookEvent, registration.Filters))
        {
            return WebhookProcessResult.Filtered("Event filtered out by webhook rules");
        }

        // Dispatch event
        try
        {
            if (OnWebhookReceived != null)
            {
                await OnWebhookReceived(webhookEvent, ct);
            }

            return WebhookProcessResult.Success(webhookEvent.Id);
        }
        catch (Exception ex)
        {
            return WebhookProcessResult.Error($"Event processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all active webhook registrations
    /// </summary>
    public IReadOnlyList<WebhookRegistration> GetActiveWebhooks()
    {
        var now = DateTime.UtcNow;
        return _registrations.Values
            .Where(r => !r.ExpiresAt.HasValue || r.ExpiresAt > now)
            .ToList();
    }

    /// <summary>
    /// Get webhook registration by ID
    /// </summary>
    public WebhookRegistration? GetWebhook(string webhookId)
    {
        return _registrations.TryGetValue(webhookId, out var reg) ? reg : null;
    }

    /// <summary>
    /// Verify a webhook signature (for manual verification)
    /// </summary>
    public bool VerifySignature(string webhookId, string payload, string signature)
    {
        if (!_secrets.TryGetValue(webhookId, out var secretInfo))
        {
            return false;
        }
        return ValidateSignature(payload, signature, secretInfo);
    }

    private WebhookRegistration? FindRegistration(string path)
    {
        // Exact match first
        var registration = _registrations.Values.FirstOrDefault(r =>
            r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (registration != null) return registration;

        // Try matching with webhook ID in path
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && segments[0] == "webhooks")
        {
            var id = segments[^1]; // Last segment might be the ID
            _registrations.TryGetValue(id, out registration);
        }

        return registration;
    }

    private bool ValidateSignature(string payload, string signature, WebhookSecret secretInfo)
    {
        var computed = ComputeSignature(payload, secretInfo.Secret, secretInfo.Algorithm);

        // Handle various signature formats
        // GitHub: sha256=xxx
        // Slack: v0=xxx
        // Stripe: t=xxx,v1=xxx
        var normalizedSignature = signature;

        if (signature.Contains('='))
        {
            var parts = signature.Split('=', 2);
            normalizedSignature = parts[^1];
        }

        if (signature.Contains(','))
        {
            // Stripe-style format
            var parts = signature.Split(',');
            var v1Part = parts.FirstOrDefault(p => p.StartsWith("v1="));
            if (v1Part != null)
            {
                normalizedSignature = v1Part.Substring(3);
            }
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(normalizedSignature));
    }

    private static string ComputeSignature(string payload, string secret, SignatureAlgorithm algorithm)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);

        using var hmac = algorithm switch
        {
            SignatureAlgorithm.HmacSha1 => (HMAC)new HMACSHA1(key),
            SignatureAlgorithm.HmacSha256 => new HMACSHA256(key),
            SignatureAlgorithm.HmacSha512 => new HMACSHA512(key),
            _ => new HMACSHA256(key)
        };

        var hash = hmac.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ApplyFilters(WebhookEvent evt, Dictionary<string, string> filters)
    {
        if (filters.Count == 0) return true;

        foreach (var filter in filters)
        {
            var value = GetEventValue(evt, filter.Key);
            if (value == null) continue;

            // Simple contains filter
            if (!value.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetEventValue(WebhookEvent evt, string path)
    {
        if (path.StartsWith("header.", StringComparison.OrdinalIgnoreCase))
        {
            var headerName = path.Substring(7);
            return evt.Headers.TryGetValue(headerName, out var headerValue) ? headerValue : null;
        }

        if (path.StartsWith("body.", StringComparison.OrdinalIgnoreCase) && evt.Payload.HasValue)
        {
            var jsonPath = path.Substring(5);
            return GetJsonPathValue(evt.Payload.Value, jsonPath);
        }

        return path switch
        {
            "method" => evt.Method,
            "body" => evt.RawBody,
            _ => null
        };
    }

    private static string? GetJsonPathValue(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(part, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => current.GetRawText()
        };
    }

    private static string GetSignatureHeader(string connectorId)
    {
        return connectorId.ToLowerInvariant() switch
        {
            "github" => "X-Hub-Signature-256",
            "slack" => "X-Slack-Signature",
            "stripe" => "Stripe-Signature",
            "twilio" => "X-Twilio-Signature",
            "teams" => "Authorization",
            _ => "X-Webhook-Signature"
        };
    }

    private static string GenerateWebhookId()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
    }

    private static string GenerateSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registrations.Clear();
        _secrets.Clear();
    }
}

/// <summary>
/// Webhook registration request
/// </summary>
public sealed class WebhookRegistrationRequest
{
    public required string ConnectorId { get; init; }
    public required string TriggerId { get; init; }
    public required string WorkflowId { get; init; }
    public string? CustomPath { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Dictionary<string, string>? Filters { get; init; }
    public SignatureAlgorithm SignatureAlgorithm { get; init; } = SignatureAlgorithm.HmacSha256;
}

/// <summary>
/// Webhook endpoint information returned after registration
/// </summary>
public sealed class WebhookEndpoint
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required string Secret { get; init; }
    public required string SignatureHeader { get; init; }

    public string GetFullUrl(string baseUrl) =>
        baseUrl.TrimEnd('/') + "/" + Path.TrimStart('/');
}

/// <summary>
/// Webhook registration details
/// </summary>
public sealed class WebhookRegistration
{
    public required string Id { get; init; }
    public required string ConnectorId { get; init; }
    public required string TriggerId { get; init; }
    public required string WorkflowId { get; init; }
    public required string Path { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Dictionary<string, string> Filters { get; init; } = new();
}

/// <summary>
/// Secret and algorithm for webhook signature validation
/// </summary>
internal sealed class WebhookSecret
{
    public required string Secret { get; init; }
    public SignatureAlgorithm Algorithm { get; init; }
}

/// <summary>
/// Signature algorithms for webhook validation
/// </summary>
public enum SignatureAlgorithm
{
    HmacSha1,
    HmacSha256,
    HmacSha512
}

/// <summary>
/// Webhook event received
/// </summary>
public sealed class WebhookEvent
{
    public required string Id { get; init; }
    public required string WebhookId { get; init; }
    public required string ConnectorId { get; init; }
    public required string TriggerId { get; init; }
    public required string WorkflowId { get; init; }
    public DateTime ReceivedAt { get; init; }
    public required string Method { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public string? RawBody { get; init; }
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// Get a value from the payload by JSON path
    /// </summary>
    public T? GetPayloadValue<T>(string path)
    {
        if (!Payload.HasValue) return default;

        var parts = path.Split('.');
        var current = Payload.Value;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return default;

            if (!current.TryGetProperty(part, out current))
                return default;
        }

        try
        {
            return current.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// Result of processing a webhook
/// </summary>
public sealed class WebhookProcessResult
{
    public bool IsSuccess { get; init; }
    public WebhookProcessStatus Status { get; init; }
    public string? Message { get; init; }
    public string? EventId { get; init; }

    public static WebhookProcessResult Success(string eventId) => new()
    {
        IsSuccess = true,
        Status = WebhookProcessStatus.Success,
        EventId = eventId
    };

    public static WebhookProcessResult NotFound(string message) => new()
    {
        IsSuccess = false,
        Status = WebhookProcessStatus.NotFound,
        Message = message
    };

    public static WebhookProcessResult InvalidSignature(string message) => new()
    {
        IsSuccess = false,
        Status = WebhookProcessStatus.InvalidSignature,
        Message = message
    };

    public static WebhookProcessResult Expired(string message) => new()
    {
        IsSuccess = false,
        Status = WebhookProcessStatus.Expired,
        Message = message
    };

    public static WebhookProcessResult Filtered(string message) => new()
    {
        IsSuccess = true, // Filtering is not an error
        Status = WebhookProcessStatus.Filtered,
        Message = message
    };

    public static WebhookProcessResult Error(string message) => new()
    {
        IsSuccess = false,
        Status = WebhookProcessStatus.Error,
        Message = message
    };
}

/// <summary>
/// Webhook processing status
/// </summary>
public enum WebhookProcessStatus
{
    Success,
    NotFound,
    InvalidSignature,
    Expired,
    Filtered,
    Error
}

/// <summary>
/// Webhook event dispatcher for routing events to workflows
/// </summary>
public sealed class WebhookEventDispatcher
{
    private readonly ConcurrentDictionary<string, List<Func<WebhookEvent, CancellationToken, Task>>> _handlers = new();

    /// <summary>
    /// Subscribe to webhook events for a specific connector/trigger combination
    /// </summary>
    public IDisposable Subscribe(string connectorId, string triggerId, Func<WebhookEvent, CancellationToken, Task> handler)
    {
        var key = $"{connectorId}:{triggerId}";

        _handlers.AddOrUpdate(key,
            _ => [handler],
            (_, list) => { list.Add(handler); return list; });

        return new Subscription(() =>
        {
            if (_handlers.TryGetValue(key, out var list))
            {
                list.Remove(handler);
            }
        });
    }

    /// <summary>
    /// Dispatch a webhook event to all subscribed handlers
    /// </summary>
    public async Task DispatchAsync(WebhookEvent evt, CancellationToken ct = default)
    {
        var key = $"{evt.ConnectorId}:{evt.TriggerId}";

        if (!_handlers.TryGetValue(key, out var handlers))
            return;

        var tasks = handlers.Select(h => h(evt, ct));
        await Task.WhenAll(tasks);
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
