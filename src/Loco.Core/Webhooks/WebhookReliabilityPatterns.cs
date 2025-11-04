#nullable enable

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Webhooks;

/// <summary>
/// Webhook reliability patterns
/// - Idempotency: Handle duplicate deliveries gracefully
/// - Retry: Exponential backoff for failed deliveries
/// - Outbox Pattern: Guarantees at-least-once delivery
/// - Dead Letter Queue: Failed webhooks for investigation
/// </summary>

/// <summary>
/// Webhook event for delivery
/// </summary>
public class WebhookEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? IdempotencyKey { get; set; }
    public Dictionary<string, object> Headers { get; set; } = new();

    public string ComputeSignature(string secret)
    {
        var payload = JsonSerializer.Serialize(new { EventType, Data, CreatedAt });
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// Webhook subscription
/// </summary>
public class WebhookSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public List<string> EventTypes { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Webhook delivery attempt
/// </summary>
public class WebhookDeliveryAttempt
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WebhookEventId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }
    public long ResponseTimeMs { get; set; }
    public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Outbox pattern: Guarantees at-least-once delivery
/// Store webhook events in local database, process asynchronously
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Adds webhook event to outbox
    /// </summary>
    Task AddAsync(WebhookEvent @event);

    /// <summary>
    /// Gets pending events for delivery
    /// </summary>
    Task<List<WebhookEvent>> GetPendingAsync(int batchSize = 100);

    /// <summary>
    /// Marks event as processed
    /// </summary>
    Task MarkAsProcessedAsync(string eventId);

    /// <summary>
    /// Records delivery attempt
    /// </summary>
    Task RecordAttemptAsync(WebhookDeliveryAttempt attempt);
}

/// <summary>
/// In-memory outbox implementation (for demo)
/// Production: Use database persistence
/// </summary>
public class InMemoryOutbox : IOutbox
{
    private readonly ConcurrentDictionary<string, WebhookEvent> _pending = new();
    private readonly ConcurrentDictionary<string, List<WebhookDeliveryAttempt>> _attempts = new();
    private readonly ILogger<InMemoryOutbox> _logger;

    public InMemoryOutbox(ILogger<InMemoryOutbox> logger)
    {
        _logger = logger;
    }

    public Task AddAsync(WebhookEvent @event)
    {
        _pending[@event.Id] = @event;
        _logger.LogInformation("Webhook event added to outbox: {EventId} ({EventType})",
            @event.Id, @event.EventType);
        return Task.CompletedTask;
    }

    public Task<List<WebhookEvent>> GetPendingAsync(int batchSize = 100)
    {
        var pending = _pending.Values
            .Take(batchSize)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task MarkAsProcessedAsync(string eventId)
    {
        _pending.TryRemove(eventId, out _);
        _logger.LogInformation("Webhook event marked as processed: {EventId}", eventId);
        return Task.CompletedTask;
    }

    public Task RecordAttemptAsync(WebhookDeliveryAttempt attempt)
    {
        _attempts.AddOrUpdate(attempt.WebhookEventId,
            new List<WebhookDeliveryAttempt> { attempt },
            (_, attempts) =>
            {
                attempts.Add(attempt);
                return attempts;
            });

        _logger.LogInformation(
            "Webhook delivery attempt recorded: {EventId} Attempt {Attempt} - {Status}",
            attempt.WebhookEventId,
            attempt.AttemptNumber,
            attempt.Success ? "Success" : "Failed");

        return Task.CompletedTask;
    }

    public List<WebhookDeliveryAttempt> GetAttempts(string eventId)
    {
        _attempts.TryGetValue(eventId, out var attempts);
        return attempts ?? new List<WebhookDeliveryAttempt>();
    }
}

/// <summary>
/// Idempotency key tracking: Prevents duplicate processing
/// </summary>
public interface IIdempotencyKeyStore
{
    /// <summary>
    /// Records that a key was processed
    /// </summary>
    Task RecordAsync(string key, object result);

    /// <summary>
    /// Checks if key was already processed
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Gets result of previous execution
    /// </summary>
    Task<object?> GetResultAsync(string key);
}

/// <summary>
/// In-memory idempotency key store
/// </summary>
public class InMemoryIdempotencyKeyStore : IIdempotencyKeyStore
{
    private readonly ConcurrentDictionary<string, (DateTime, object)> _keys = new();
    private readonly ILogger<InMemoryIdempotencyKeyStore> _logger;
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(24);

    public InMemoryIdempotencyKeyStore(ILogger<InMemoryIdempotencyKeyStore> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(string key, object result)
    {
        _keys[key] = (DateTime.UtcNow, result);
        _logger.LogDebug("Idempotency key recorded: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        if (_keys.TryGetValue(key, out var entry))
        {
            var (timestamp, _) = entry;
            if (DateTime.UtcNow - timestamp > _expirationTime)
            {
                _keys.TryRemove(key, out _);
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<object?> GetResultAsync(string key)
    {
        if (_keys.TryGetValue(key, out var entry))
        {
            var (_, result) = entry;
            return Task.FromResult<object?>(result);
        }

        return Task.FromResult<object?>(null);
    }
}

/// <summary>
/// Webhook delivery service with retry logic
/// </summary>
public class WebhookDeliveryService
{
    private readonly HttpClient _httpClient;
    private readonly IOutbox _outbox;
    private readonly IIdempotencyKeyStore _idempotencyStore;
    private readonly ILogger<WebhookDeliveryService> _logger;

    public WebhookDeliveryService(
        HttpClient httpClient,
        IOutbox outbox,
        IIdempotencyKeyStore idempotencyStore,
        ILogger<WebhookDeliveryService> logger)
    {
        _httpClient = httpClient;
        _outbox = outbox;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
    }

    /// <summary>
    /// Publishes webhook event (adds to outbox)
    /// </summary>
    public async Task PublishAsync(WebhookEvent webhookEvent)
    {
        webhookEvent.IdempotencyKey ??= Guid.NewGuid().ToString();
        await _outbox.AddAsync(webhookEvent).ConfigureAwait(false);
        _logger.LogInformation("Webhook published: {EventType} ({EventId})",
            webhookEvent.EventType, webhookEvent.Id);
    }

    /// <summary>
    /// Delivers webhook to subscription with retry logic
    /// </summary>
    public async Task<bool> DeliverAsync(
        WebhookEvent webhookEvent,
        WebhookSubscription subscription,
        int attemptNumber = 1)
    {
        // Check idempotency
        var idempotencyKey = webhookEvent.IdempotencyKey ?? webhookEvent.Id;
        if (await _idempotencyStore.ExistsAsync(idempotencyKey).ConfigureAwait(false))
        {
            _logger.LogWarning(
                "Duplicate webhook delivery attempt: {EventId} to {Url}",
                webhookEvent.Id, subscription.Url);
            return true; // Treat as success
        }

        var attempt = new WebhookDeliveryAttempt
        {
            WebhookEventId = webhookEvent.Id,
            SubscriptionId = subscription.Id,
            AttemptNumber = attemptNumber
        };

        try
        {
            var startTime = DateTime.UtcNow;

            // Build request
            var payload = JsonSerializer.Serialize(webhookEvent);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            // Add signature header
            var signature = webhookEvent.ComputeSignature(subscription.Secret);
            _httpClient.DefaultRequestHeaders.Add("X-Webhook-Signature", signature);
            _httpClient.DefaultRequestHeaders.Add("X-Webhook-Id", webhookEvent.Id);
            _httpClient.DefaultRequestHeaders.Add("X-Idempotency-Key", idempotencyKey);

            // Send request with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(subscription.TimeoutSeconds));
            var response = await _httpClient.PostAsync(subscription.Url, content, cts.Token)
                .ConfigureAwait(false);

            attempt.ResponseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            attempt.ResponseStatusCode = (int)response.StatusCode;
            attempt.ResponseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            attempt.Success = response.IsSuccessStatusCode;

            // Record attempt
            await _outbox.RecordAttemptAsync(attempt).ConfigureAwait(false);

            if (attempt.Success)
            {
                // Mark idempotency key
                await _idempotencyStore.RecordAsync(idempotencyKey, true).ConfigureAwait(false);

                // Mark event as processed
                await _outbox.MarkAsProcessedAsync(webhookEvent.Id).ConfigureAwait(false);

                _logger.LogInformation(
                    "Webhook delivered successfully: {EventId} to {Url} ({Time}ms)",
                    webhookEvent.Id, subscription.Url, attempt.ResponseTimeMs);

                return true;
            }

            // Non-success response
            throw new HttpRequestException($"HTTP {response.StatusCode}");
        }
        catch (Exception ex)
        {
            attempt.Success = false;
            attempt.ErrorMessage = ex.Message;

            _logger.LogWarning(
                ex,
                "Webhook delivery failed (attempt {Attempt}): {EventId} to {Url}",
                attemptNumber, webhookEvent.Id, subscription.Url);

            // Schedule retry
            attempt.NextRetryAt = CalculateNextRetryTime(attemptNumber);

            // Record attempt
            await _outbox.RecordAttemptAsync(attempt).ConfigureAwait(false);

            // Check if should retry
            return attemptNumber < subscription.MaxRetries;
        }
    }

    /// <summary>
    /// Processes pending webhooks from outbox
    /// </summary>
    public async Task ProcessPendingWebhooksAsync(
        List<WebhookSubscription> subscriptions,
        CancellationToken cancellationToken = default)
    {
        var pending = await _outbox.GetPendingAsync().ConfigureAwait(false);

        foreach (var webhookEvent in pending)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Find matching subscriptions
            var matchingSubscriptions = subscriptions
                .Where(s => s.IsActive && s.EventTypes.Contains(webhookEvent.EventType))
                .ToList();

            foreach (var subscription in matchingSubscriptions)
            {
                try
                {
                    var success = await DeliverAsync(webhookEvent, subscription)
                        .ConfigureAwait(false);

                    if (success)
                    {
                        // Event delivered, mark for next check
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error delivering webhook");
                }
            }
        }
    }

    private DateTime CalculateNextRetryTime(int attemptNumber)
    {
        // Exponential backoff: 30s, 2min, 8min, 30min, 2h
        var delaySeconds = attemptNumber switch
        {
            1 => 30,
            2 => 120,
            3 => 480,
            4 => 1800,
            _ => 7200
        };

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}

/// <summary>
/// Dead letter queue for failed webhooks
/// </summary>
public class WebhookDeadLetterQueue
{
    private readonly ConcurrentBag<(WebhookEvent, Exception)> _deadLetters = new();
    private readonly ILogger<WebhookDeadLetterQueue> _logger;

    public WebhookDeadLetterQueue(ILogger<WebhookDeadLetterQueue> logger)
    {
        _logger = logger;
    }

    public void Enqueue(WebhookEvent webhookEvent, Exception exception)
    {
        _deadLetters.Add((webhookEvent, exception));
        _logger.LogError(exception, "Webhook moved to dead letter queue: {EventId}", webhookEvent.Id);
    }

    public List<(WebhookEvent, Exception)> GetAll()
    {
        return _deadLetters.ToList();
    }

    public void Clear()
    {
        _deadLetters.Clear();
    }
}

/// <summary>
/// Webhook configuration
/// </summary>
public class WebhookConfig
{
    public int MaxRetries { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 100;
    public int ProcessingIntervalSeconds { get; set; } = 30;
}

/// <summary>
/// Extension methods
/// </summary>
public static class WebhookExtensions
{
    public static IServiceCollection AddWebhookReliability(
        this IServiceCollection services,
        WebhookConfig? config = null)
    {
        config ??= new WebhookConfig();
        services.AddSingleton(config);
        services.AddSingleton<IOutbox, InMemoryOutbox>();
        services.AddSingleton<IIdempotencyKeyStore, InMemoryIdempotencyKeyStore>();
        services.AddSingleton<WebhookDeliveryService>();
        services.AddSingleton<WebhookDeadLetterQueue>();

        return services;
    }

    public static IApplicationBuilder UseWebhookProcessing(
        this IApplicationBuilder app,
        WebhookConfig? config = null)
    {
        config ??= new WebhookConfig();

        // Start background task to process pending webhooks
        var scope = app.ApplicationServices.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<WebhookDeliveryService>();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await deliveryService.ProcessPendingWebhooksAsync(new List<WebhookSubscription>())
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    app.ApplicationServices.GetRequiredService<ILogger<WebhookDeliveryService>>()
                        .LogError(ex, "Error processing pending webhooks");
                }

                await Task.Delay(TimeSpan.FromSeconds(config.ProcessingIntervalSeconds))
                    .ConfigureAwait(false);
            }
        });

        return app;
    }
}

/// <summary>
/// Example webhook event types
/// </summary>
public static class WebhookEventTypes
{
    public const string WorkflowCreated = "workflow.created";
    public const string WorkflowStarted = "workflow.started";
    public const string WorkflowCompleted = "workflow.completed";
    public const string WorkflowFailed = "workflow.failed";
    public const string StepCompleted = "step.completed";
    public const string TaskAssigned = "task.assigned";
}
