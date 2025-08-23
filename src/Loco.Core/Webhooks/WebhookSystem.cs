using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Loco.Core.Webhooks
{
    public interface IWebhookSystem
    {
        Task<Guid> RegisterWebhookAsync(WebhookRegistration registration);
        Task UnregisterWebhookAsync(Guid webhookId);
        Task<WebhookDeliveryResult> SendWebhookAsync(string eventType, object payload);
        Task<IEnumerable<WebhookRegistration>> GetActiveWebhooksAsync(string eventType);
        Task<WebhookStatistics> GetStatisticsAsync(Guid webhookId);
    }

    public class WebhookSystem : IWebhookSystem
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebhookSystem> _logger;
        private readonly IWebhookStore _store;
        private readonly IWebhookSecurity _security;
        private readonly Dictionary<Guid, IAsyncPolicy<HttpResponseMessage>> _policies;

        public WebhookSystem(
            IHttpClientFactory httpClientFactory,
            ILogger<WebhookSystem> logger,
            IWebhookStore store,
            IWebhookSecurity security)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _store = store;
            _security = security;
            _policies = new Dictionary<Guid, IAsyncPolicy<HttpResponseMessage>>();
        }

        public async Task<Guid> RegisterWebhookAsync(WebhookRegistration registration)
        {
            // Validate URL
            if (!IsValidUrl(registration.Url))
            {
                throw new ArgumentException("Invalid webhook URL");
            }

            // Generate secret for HMAC signature
            registration.Secret = _security.GenerateSecret();
            registration.Id = Guid.NewGuid();
            registration.CreatedAt = DateTime.UtcNow;
            registration.IsActive = true;

            // Create retry policy for this webhook
            var policy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Webhook retry {RetryCount} after {Delay}ms for {WebhookId}",
                            retryCount, timespan.TotalMilliseconds, registration.Id);
                    })
                .WrapAsync(Policy
                    .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                    .CircuitBreakerAsync(
                        5,
                        TimeSpan.FromMinutes(1),
                        onBreak: (result, duration) =>
                        {
                            _logger.LogError("Circuit breaker opened for {WebhookId} for {Duration}",
                                registration.Id, duration);
                        },
                        onReset: () =>
                        {
                            _logger.LogInformation("Circuit breaker reset for {WebhookId}", registration.Id);
                        }));

            _policies[registration.Id] = policy;

            await _store.SaveWebhookAsync(registration);
            _logger.LogInformation("Webhook registered: {WebhookId} for {EventTypes}",
                registration.Id, string.Join(", ", registration.EventTypes));

            return registration.Id;
        }

        public async Task UnregisterWebhookAsync(Guid webhookId)
        {
            await _store.DeleteWebhookAsync(webhookId);
            _policies.Remove(webhookId);
            _logger.LogInformation("Webhook unregistered: {WebhookId}", webhookId);
        }

        public async Task<WebhookDeliveryResult> SendWebhookAsync(string eventType, object payload)
        {
            var webhooks = await GetActiveWebhooksAsync(eventType);
            var results = new List<WebhookDeliveryAttempt>();

            var tasks = webhooks.Select(async webhook =>
            {
                var attempt = await DeliverWebhookAsync(webhook, eventType, payload);
                results.Add(attempt);
                await _store.SaveDeliveryAttemptAsync(attempt);
            });

            await Task.WhenAll(tasks);

            return new WebhookDeliveryResult
            {
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                TotalWebhooks = webhooks.Count(),
                SuccessfulDeliveries = results.Count(r => r.Success),
                FailedDeliveries = results.Count(r => !r.Success),
                Attempts = results
            };
        }

        private async Task<WebhookDeliveryAttempt> DeliverWebhookAsync(
            WebhookRegistration webhook,
            string eventType,
            object payload)
        {
            var attempt = new WebhookDeliveryAttempt
            {
                Id = Guid.NewGuid(),
                WebhookId = webhook.Id,
                EventType = eventType,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var client = _httpClientFactory.CreateClient("webhook");
                client.Timeout = TimeSpan.FromSeconds(30);

                var webhookPayload = new WebhookPayload
                {
                    Id = Guid.NewGuid(),
                    EventType = eventType,
                    Timestamp = DateTime.UtcNow,
                    Data = payload
                };

                var json = JsonSerializer.Serialize(webhookPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add HMAC signature
                var signature = _security.GenerateSignature(json, webhook.Secret);
                content.Headers.Add("X-Webhook-Signature", signature);
                content.Headers.Add("X-Webhook-Id", webhook.Id.ToString());
                content.Headers.Add("X-Event-Type", eventType);

                // Apply retry policy
                var policy = _policies.GetValueOrDefault(webhook.Id) ?? Policy.NoOpAsync<HttpResponseMessage>();
                
                var response = await policy.ExecuteAsync(async () =>
                    await client.PostAsync(webhook.Url, content));

                attempt.StatusCode = (int)response.StatusCode;
                attempt.Success = response.IsSuccessStatusCode;
                attempt.ResponseBody = await response.Content.ReadAsStringAsync();

                if (attempt.Success)
                {
                    _logger.LogInformation("Webhook delivered successfully to {WebhookId}", webhook.Id);
                }
                else
                {
                    _logger.LogWarning("Webhook delivery failed for {WebhookId}: {StatusCode}",
                        webhook.Id, response.StatusCode);
                }
            }
            catch (BrokenCircuitException ex)
            {
                attempt.Success = false;
                attempt.Error = "Circuit breaker is open";
                _logger.LogError(ex, "Circuit breaker prevented webhook delivery to {WebhookId}", webhook.Id);
            }
            catch (HttpRequestException ex)
            {
                attempt.Success = false;
                attempt.Error = ex.Message;
                _logger.LogError(ex, "HTTP error delivering webhook to {WebhookId}", webhook.Id);
            }
            catch (TaskCanceledException ex)
            {
                attempt.Success = false;
                attempt.Error = "Request timeout";
                _logger.LogError(ex, "Timeout delivering webhook to {WebhookId}", webhook.Id);
            }
            catch (Exception ex)
            {
                attempt.Success = false;
                attempt.Error = ex.Message;
                _logger.LogError(ex, "Unexpected error delivering webhook to {WebhookId}", webhook.Id);
            }

            attempt.Duration = DateTime.UtcNow - attempt.Timestamp;
            return attempt;
        }

        public async Task<IEnumerable<WebhookRegistration>> GetActiveWebhooksAsync(string eventType)
        {
            var webhooks = await _store.GetWebhooksAsync(eventType);
            return webhooks.Where(w => w.IsActive && w.EventTypes.Contains(eventType));
        }

        public async Task<WebhookStatistics> GetStatisticsAsync(Guid webhookId)
        {
            var attempts = await _store.GetDeliveryAttemptsAsync(webhookId, DateTime.UtcNow.AddDays(-7));
            
            return new WebhookStatistics
            {
                WebhookId = webhookId,
                TotalDeliveries = attempts.Count(),
                SuccessfulDeliveries = attempts.Count(a => a.Success),
                FailedDeliveries = attempts.Count(a => !a.Success),
                AverageResponseTime = attempts.Any() 
                    ? attempts.Average(a => a.Duration.TotalMilliseconds) 
                    : 0,
                LastDeliveryAt = attempts.MaxBy(a => a.Timestamp)?.Timestamp,
                SuccessRate = attempts.Any() 
                    ? (double)attempts.Count(a => a.Success) / attempts.Count() * 100 
                    : 0
            };
        }

        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // Only allow HTTPS in production
            return uri.Scheme == Uri.UriSchemeHttps;
        }
    }

    // Supporting interfaces and models
    public interface IWebhookStore
    {
        Task SaveWebhookAsync(WebhookRegistration webhook);
        Task DeleteWebhookAsync(Guid webhookId);
        Task<IEnumerable<WebhookRegistration>> GetWebhooksAsync(string eventType);
        Task SaveDeliveryAttemptAsync(WebhookDeliveryAttempt attempt);
        Task<IEnumerable<WebhookDeliveryAttempt>> GetDeliveryAttemptsAsync(Guid webhookId, DateTime since);
    }

    public interface IWebhookSecurity
    {
        string GenerateSecret();
        string GenerateSignature(string payload, string secret);
        bool VerifySignature(string payload, string signature, string secret);
    }

    public class WebhookSecurity : IWebhookSecurity
    {
        public string GenerateSecret()
        {
            var bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        public string GenerateSignature(string payload, string secret)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return Convert.ToBase64String(hash);
            }
        }

        public bool VerifySignature(string payload, string signature, string secret)
        {
            var expectedSignature = GenerateSignature(payload, secret);
            return string.Equals(signature, expectedSignature, StringComparison.Ordinal);
        }
    }

    // Models
    public class WebhookRegistration
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public string Secret { get; set; }
        public List<string> EventTypes { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Description { get; set; }
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class WebhookPayload
    {
        public Guid Id { get; set; }
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public object Data { get; set; }
    }

    public class WebhookDeliveryAttempt
    {
        public Guid Id { get; set; }
        public Guid WebhookId { get; set; }
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        public string ResponseBody { get; set; }
        public string Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class WebhookDeliveryResult
    {
        public string EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public int TotalWebhooks { get; set; }
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
        public List<WebhookDeliveryAttempt> Attempts { get; set; }
    }

    public class WebhookStatistics
    {
        public Guid WebhookId { get; set; }
        public int TotalDeliveries { get; set; }
        public int SuccessfulDeliveries { get; set; }
        public int FailedDeliveries { get; set; }
        public double AverageResponseTime { get; set; }
        public DateTime? LastDeliveryAt { get; set; }
        public double SuccessRate { get; set; }
    }
}