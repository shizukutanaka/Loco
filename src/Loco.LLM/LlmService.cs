using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Loco.Core.Models;
using Loco.Core.Interfaces;

namespace Loco.Llm;

/// <summary>
/// LLM service implementation - Simplified Rob Pike style
/// </summary>
public class LlmService : ILlmService
{
    private readonly ILogger<LlmService> _logger;
    private readonly LlmConfiguration _configuration;
    private readonly HttpClient _httpClient;
    
    public LlmService(
        ILogger<LlmService> logger,
        IOptions<LlmConfiguration> configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration.Value;
        _httpClient = httpClient;

        // Ensure an upper-bound timeout to avoid indefinite hangs at the HTTP layer
        try
        {
            var configured = _configuration.HttpTimeoutMs > 0 ? _configuration.HttpTimeoutMs : 30000;
            var timeoutMs = Math.Clamp(configured, 1000, 600000);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
        }
        catch { /* best-effort; HttpClientFactory may also set this */ }
    }
    
    public async Task<string> GenerateTextAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LlmRequest
            {
                Model = _configuration.Model,
                Prompt = prompt,
                MaxTokens = _configuration.MaxTokens,
                Temperature = _configuration.Temperature
            };
            
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
            
            // Link external cancellation with a hard per-request timeout guard
            var configured = _configuration.HttpTimeoutMs > 0 ? _configuration.HttpTimeoutMs : 30000;
            var timeoutMs = Math.Clamp(configured, 1000, 600000);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            _logger.LogInformation("LLM POST(start): endpoint={Endpoint} model={Model} promptLen={Len} timeoutMs={Timeout}",
                _configuration.ApiEndpoint, request.Model, request.Prompt?.Length ?? 0, timeoutMs);

            var response = await _httpClient.PostAsync(
                _configuration.ApiEndpoint,
                content,
                cts.Token);
            
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            var result = JsonSerializer.Deserialize<LlmResponse>(responseJson);
            
            _logger.LogInformation("LLM POST(done): status={Status} bytes={Bytes}", (int)response.StatusCode, responseJson?.Length ?? 0);
            return result?.Text ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request canceled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text with LLM");
            throw;
        }
    }

    // Per-call override for model and generation parameters
    public async Task<string> GenerateTextAsync(
        string prompt,
        string? modelOverride,
        int? maxTokensOverride,
        double? temperatureOverride,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LlmRequest
            {
                Model = string.IsNullOrWhiteSpace(modelOverride) ? _configuration.Model : modelOverride,
                Prompt = prompt,
                MaxTokens = maxTokensOverride ?? _configuration.MaxTokens,
                Temperature = temperatureOverride ?? _configuration.Temperature
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);

            // Link external cancellation with a hard per-request timeout guard
            var configured = _configuration.HttpTimeoutMs > 0 ? _configuration.HttpTimeoutMs : 30000;
            var timeoutMs = Math.Clamp(configured, 1000, 600000);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            _logger.LogInformation("LLM POST(start): endpoint={Endpoint} model={Model} promptLen={Len} timeoutMs={Timeout}",
                _configuration.ApiEndpoint, request.Model, request.Prompt?.Length ?? 0, timeoutMs);

            var response = await _httpClient.PostAsync(
                _configuration.ApiEndpoint,
                content,
                cts.Token);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            var result = JsonSerializer.Deserialize<LlmResponse>(responseJson);

            _logger.LogInformation("LLM POST(done): status={Status} bytes={Bytes}", (int)response.StatusCode, responseJson?.Length ?? 0);
            return result?.Text ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request canceled (override)");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text with LLM (override)");
            throw;
        }
    }
    
    public async Task<string> TranslateFlowToCodeAsync(
        string flowDescription,
        string targetLanguage = "csharp",
        CancellationToken cancellationToken = default)
    {
        var prompt = $"Convert this flow description to {targetLanguage} code:\n{flowDescription}";
        return await GenerateTextAsync(prompt, cancellationToken);
    }
}

public class LlmConfiguration
{
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "gpt-4";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/completions";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public int HttpTimeoutMs { get; set; } = 30000;
}

public class LlmRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
}

public class LlmResponse
{
    public string Text { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
}