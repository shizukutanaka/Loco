using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Webhook trigger configuration.
/// </summary>
public class WebhookTrigger
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public List<string> AllowedMethods { get; set; } = new() { "POST" };
    public string? Secret { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? WorkflowId { get; set; }
    public Dictionary<string, string> VariableMapping { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Webhook request information.
/// </summary>
public class WebhookRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TriggerId { get; set; } = "";
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? SourceIp { get; set; }
}

/// <summary>
/// Webhook execution result.
/// </summary>
public class WebhookResult
{
    public string RequestId { get; set; } = "";
    public string TriggerId { get; set; } = "";
    public bool Success { get; set; }
    public string? ExecutionId { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
}

/// <summary>
/// Webhook handler that can trigger workflows.
/// </summary>
public class WorkflowWebhookHandler
{
    private readonly ConcurrentDictionary<string, WebhookTrigger> _triggers = new();
    private readonly List<WebhookRequest> _requestHistory = new();
    private readonly int _maxHistorySize;
    private readonly object _historyLock = new();

    public WorkflowWebhookHandler(int maxHistorySize = 1000)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Registers a webhook trigger.
    /// </summary>
    public void RegisterTrigger(WebhookTrigger trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger.Id))
            throw new ArgumentException("Trigger ID is required");

        if (string.IsNullOrWhiteSpace(trigger.Path))
            throw new ArgumentException("Trigger path is required");

        _triggers[trigger.Id] = trigger;
    }

    /// <summary>
    /// Unregisters a webhook trigger.
    /// </summary>
    public bool UnregisterTrigger(string triggerId)
    {
        return _triggers.TryRemove(triggerId, out _);
    }

    /// <summary>
    /// Gets all registered triggers.
    /// </summary>
    public List<WebhookTrigger> GetTriggers()
    {
        return _triggers.Values.ToList();
    }

    /// <summary>
    /// Gets a trigger by ID.
    /// </summary>
    public WebhookTrigger? GetTrigger(string triggerId)
    {
        return _triggers.TryGetValue(triggerId, out var trigger) ? trigger : null;
    }

    /// <summary>
    /// Finds a trigger by path and method.
    /// </summary>
    public WebhookTrigger? FindTrigger(string path, string method)
    {
        return _triggers.Values.FirstOrDefault(t =>
            t.Enabled &&
            t.Path.Equals(path, StringComparison.OrdinalIgnoreCase) &&
            t.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Processes a webhook request.
    /// </summary>
    public async Task<WebhookResult> ProcessWebhookAsync(
        string path,
        string method,
        Dictionary<string, string> headers,
        string body,
        string? sourceIp = null,
        Func<string, Dictionary<string, string>, Task<string>>? workflowExecutor = null)
    {
        var request = new WebhookRequest
        {
            Path = path,
            Method = method,
            Headers = headers,
            Body = body,
            SourceIp = sourceIp
        };

        AddToHistory(request);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Find matching trigger
        var trigger = FindTrigger(path, method);
        if (trigger == null)
        {
            sw.Stop();
            return new WebhookResult
            {
                RequestId = request.Id,
                Success = false,
                ErrorMessage = $"No webhook trigger found for {method} {path}",
                ProcessingTime = sw.Elapsed
            };
        }

        request.TriggerId = trigger.Id;

        // Validate secret if configured
        if (!string.IsNullOrWhiteSpace(trigger.Secret))
        {
            if (!ValidateSecret(headers, body, trigger.Secret))
            {
                sw.Stop();
                return new WebhookResult
                {
                    RequestId = request.Id,
                    TriggerId = trigger.Id,
                    Success = false,
                    ErrorMessage = "Invalid webhook secret",
                    ProcessingTime = sw.Elapsed
                };
            }
        }

        // Validate required headers
        foreach (var requiredHeader in trigger.Headers)
        {
            if (!headers.ContainsKey(requiredHeader.Key))
            {
                sw.Stop();
                return new WebhookResult
                {
                    RequestId = request.Id,
                    TriggerId = trigger.Id,
                    Success = false,
                    ErrorMessage = $"Missing required header: {requiredHeader.Key}",
                    ProcessingTime = sw.Elapsed
                };
            }

            if (!string.IsNullOrWhiteSpace(requiredHeader.Value) &&
                headers[requiredHeader.Key] != requiredHeader.Value)
            {
                sw.Stop();
                return new WebhookResult
                {
                    RequestId = request.Id,
                    TriggerId = trigger.Id,
                    Success = false,
                    ErrorMessage = $"Invalid header value for: {requiredHeader.Key}",
                    ProcessingTime = sw.Elapsed
                };
            }
        }

        // Extract variables from request
        var variables = ExtractVariables(request, trigger);

        // Execute workflow if executor provided
        string? executionId = null;
        if (workflowExecutor != null && !string.IsNullOrWhiteSpace(trigger.WorkflowId))
        {
            try
            {
                executionId = await workflowExecutor(trigger.WorkflowId, variables);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new WebhookResult
                {
                    RequestId = request.Id,
                    TriggerId = trigger.Id,
                    Success = false,
                    ErrorMessage = $"Workflow execution failed: {ex.Message}",
                    ProcessingTime = sw.Elapsed
                };
            }
        }

        sw.Stop();

        return new WebhookResult
        {
            RequestId = request.Id,
            TriggerId = trigger.Id,
            Success = true,
            ExecutionId = executionId,
            ProcessingTime = sw.Elapsed,
            ResponseHeaders = new Dictionary<string, string>
            {
                { "X-Webhook-Request-Id", request.Id },
                { "X-Workflow-Execution-Id", executionId ?? "" }
            }
        };
    }

    /// <summary>
    /// Validates webhook secret.
    /// </summary>
    private bool ValidateSecret(Dictionary<string, string> headers, string body, string secret)
    {
        // Check for common secret header names
        var secretHeaders = new[] { "X-Webhook-Secret", "X-Hub-Signature", "X-Signature" };

        foreach (var headerName in secretHeaders)
        {
            if (headers.TryGetValue(headerName, out var headerValue))
            {
                // Simple comparison (in production, use HMAC)
                if (headerValue == secret)
                    return true;

                // Check HMAC-SHA256 format (GitHub style)
                if (headerValue.StartsWith("sha256="))
                {
                    var signature = headerValue.Substring(7);
                    var expectedSignature = ComputeHmacSha256(body, secret);
                    return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Computes HMAC-SHA256 signature.
    /// </summary>
    private string ComputeHmacSha256(string message, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Extracts variables from webhook request based on trigger mapping.
    /// </summary>
    private Dictionary<string, string> ExtractVariables(WebhookRequest request, WebhookTrigger trigger)
    {
        var variables = new Dictionary<string, string>();

        // Parse JSON body if available
        Dictionary<string, object>? bodyData = null;
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            try
            {
                bodyData = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Body);
            }
            catch
            {
                // Not JSON or invalid
            }
        }

        // Apply variable mapping
        foreach (var mapping in trigger.VariableMapping)
        {
            var source = mapping.Key;
            var target = mapping.Value;

            string? value = null;

            // Extract from different sources
            if (source.StartsWith("header."))
            {
                var headerName = source.Substring(7);
                request.Headers.TryGetValue(headerName, out value);
            }
            else if (source.StartsWith("body.") && bodyData != null)
            {
                var path = source.Substring(5);
                value = GetJsonValue(bodyData, path);
            }
            else if (source == "body")
            {
                value = request.Body;
            }

            if (value != null)
            {
                variables[target] = value;
            }
        }

        // Add default variables
        variables["webhook_id"] = request.Id;
        variables["webhook_trigger"] = trigger.Id;
        variables["webhook_path"] = request.Path;
        variables["webhook_method"] = request.Method;
        variables["webhook_received_at"] = request.ReceivedAt.ToString("o");

        return variables;
    }

    /// <summary>
    /// Gets a value from JSON object by path.
    /// </summary>
    private string? GetJsonValue(Dictionary<string, object> data, string path)
    {
        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(part, out current))
                    return null;
            }
            else if (current is JsonElement element)
            {
                if (element.TryGetProperty(part, out var property))
                    current = property;
                else
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current?.ToString();
    }

    /// <summary>
    /// Adds request to history.
    /// </summary>
    private void AddToHistory(WebhookRequest request)
    {
        lock (_historyLock)
        {
            _requestHistory.Add(request);

            if (_requestHistory.Count > _maxHistorySize)
            {
                _requestHistory.RemoveRange(0, _requestHistory.Count - _maxHistorySize);
            }
        }
    }

    /// <summary>
    /// Gets webhook request history.
    /// </summary>
    public List<WebhookRequest> GetRequestHistory(int limit = 100)
    {
        lock (_historyLock)
        {
            return _requestHistory.TakeLast(limit).ToList();
        }
    }

    /// <summary>
    /// Gets request history for a specific trigger.
    /// </summary>
    public List<WebhookRequest> GetTriggerHistory(string triggerId, int limit = 100)
    {
        lock (_historyLock)
        {
            return _requestHistory
                .Where(r => r.TriggerId == triggerId)
                .TakeLast(limit)
                .ToList();
        }
    }

    /// <summary>
    /// Generates a webhook configuration report.
    /// </summary>
    public string GenerateWebhookReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WEBHOOK CONFIGURATION                                                         ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var triggers = GetTriggers();

        if (triggers.Count == 0)
        {
            sb.AppendLine("No webhook triggers configured.");
            sb.AppendLine();
            return sb.ToString();
        }

        sb.AppendLine($"Total Triggers: {triggers.Count}");
        sb.AppendLine();

        foreach (var trigger in triggers.OrderBy(t => t.Path))
        {
            var status = trigger.Enabled ? "✅" : "❌";
            sb.AppendLine($"{status} {trigger.Name} ({trigger.Id})");
            sb.AppendLine($"   Path: {trigger.Path}");
            sb.AppendLine($"   Methods: {string.Join(", ", trigger.AllowedMethods)}");

            if (!string.IsNullOrWhiteSpace(trigger.WorkflowId))
            {
                sb.AppendLine($"   Workflow: {trigger.WorkflowId}");
            }

            if (trigger.Headers.Count > 0)
            {
                sb.AppendLine($"   Required Headers: {string.Join(", ", trigger.Headers.Keys)}");
            }

            if (trigger.VariableMapping.Count > 0)
            {
                sb.AppendLine($"   Variable Mapping: {trigger.VariableMapping.Count} mappings");
            }

            if (!string.IsNullOrWhiteSpace(trigger.Secret))
            {
                sb.AppendLine("   Secret: Configured ✓");
            }

            // Get recent requests for this trigger
            var recentRequests = GetTriggerHistory(trigger.Id, 10);
            if (recentRequests.Count > 0)
            {
                sb.AppendLine($"   Recent Requests: {recentRequests.Count}");
                var lastRequest = recentRequests.LastOrDefault();
                if (lastRequest != null)
                {
                    sb.AppendLine($"   Last Request: {lastRequest.ReceivedAt:yyyy-MM-dd HH:mm:ss}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a webhook activity report.
    /// </summary>
    public string GenerateActivityReport(int limit = 50)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║ WEBHOOK ACTIVITY                                                              ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        var history = GetRequestHistory(limit);

        if (history.Count == 0)
        {
            sb.AppendLine("No webhook requests recorded.");
            sb.AppendLine();
            return sb.ToString();
        }

        // Statistics
        var byTrigger = history.GroupBy(r => r.TriggerId).ToDictionary(g => g.Key, g => g.Count());
        var byMethod = history.GroupBy(r => r.Method).ToDictionary(g => g.Key, g => g.Count());

        sb.AppendLine($"Total Requests: {history.Count}");
        sb.AppendLine();

        sb.AppendLine("By Trigger:");
        foreach (var kvp in byTrigger.OrderByDescending(x => x.Value))
        {
            var trigger = GetTrigger(kvp.Key);
            var name = trigger?.Name ?? kvp.Key;
            sb.AppendLine($"  {name}: {kvp.Value} requests");
        }
        sb.AppendLine();

        sb.AppendLine("By Method:");
        foreach (var kvp in byMethod.OrderByDescending(x => x.Value))
        {
            sb.AppendLine($"  {kvp.Key}: {kvp.Value} requests");
        }
        sb.AppendLine();

        sb.AppendLine("Recent Requests:");
        foreach (var request in history.OrderByDescending(r => r.ReceivedAt).Take(20))
        {
            var trigger = GetTrigger(request.TriggerId);
            var triggerName = trigger?.Name ?? request.TriggerId;

            sb.AppendLine($"  [{request.ReceivedAt:HH:mm:ss}] {request.Method} {request.Path}");
            sb.AppendLine($"    Trigger: {triggerName}");
            if (!string.IsNullOrWhiteSpace(request.SourceIp))
            {
                sb.AppendLine($"    Source: {request.SourceIp}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// Extends WorkflowDefinition with webhook trigger support.
/// </summary>
public partial class WorkflowDefinition
{
    /// <summary>
    /// Webhook triggers for this workflow.
    /// </summary>
    public List<WebhookTrigger>? Webhooks { get; set; }
}
