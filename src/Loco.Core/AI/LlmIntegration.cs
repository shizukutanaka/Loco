using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

/// <summary>
/// Universal LLM integration supporting OpenAI, Azure OpenAI, Anthropic Claude, Local models
/// </summary>
public class LlmIntegration : IDisposable
{
    private readonly LlmConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger? _logger;
    private bool _disposed;

    public LlmIntegration(LlmConfig config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds) };
    }

    /// <summary>
    /// Send prompt to LLM and get response
    /// </summary>
    public async Task<LlmResponse> CompleteAsync(string prompt, LlmOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new LlmOptions();
        var startTime = DateTime.UtcNow;

        try
        {
            var response = _config.Provider.ToLowerInvariant() switch
            {
                "openai" => await CallOpenAIAsync(prompt, options, cancellationToken),
                "azure" => await CallAzureOpenAIAsync(prompt, options, cancellationToken),
                "anthropic" or "claude" => await CallAnthropicAsync(prompt, options, cancellationToken),
                "ollama" or "local" => await CallOllamaAsync(prompt, options, cancellationToken),
                _ => throw new NotSupportedException($"LLM provider '{_config.Provider}' is not supported")
            };

            response.ExecutionTime = DateTime.UtcNow - startTime;
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LLM completion failed");
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Chat completion with conversation history
    /// </summary>
    public async Task<LlmResponse> ChatAsync(List<LlmMessage> messages, LlmOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new LlmOptions();
        var startTime = DateTime.UtcNow;

        try
        {
            var response = _config.Provider.ToLowerInvariant() switch
            {
                "openai" => await ChatOpenAIAsync(messages, options, cancellationToken),
                "azure" => await ChatAzureOpenAIAsync(messages, options, cancellationToken),
                "anthropic" or "claude" => await ChatAnthropicAsync(messages, options, cancellationToken),
                "ollama" or "local" => await ChatOllamaAsync(messages, options, cancellationToken),
                _ => throw new NotSupportedException($"LLM provider '{_config.Provider}' is not supported")
            };

            response.ExecutionTime = DateTime.UtcNow - startTime;
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LLM chat failed");
            return new LlmResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Generate embeddings for text
    /// </summary>
    public async Task<LlmEmbeddingResponse> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var response = _config.Provider.ToLowerInvariant() switch
            {
                "openai" => await EmbedOpenAIAsync(text, cancellationToken),
                "azure" => await EmbedAzureOpenAIAsync(text, cancellationToken),
                "ollama" or "local" => await EmbedOllamaAsync(text, cancellationToken),
                _ => throw new NotSupportedException($"Embeddings not supported for provider '{_config.Provider}'")
            };

            response.ExecutionTime = DateTime.UtcNow - startTime;
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Embedding generation failed");
            return new LlmEmbeddingResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<LlmResponse> CallOpenAIAsync(string prompt, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            prompt = prompt,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            top_p = options.TopP,
            stop = options.StopSequences
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/v1/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("choices")[0].GetProperty("text").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("total_tokens").GetInt32(),
            FinishReason = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
        };
    }

    private async Task<LlmResponse> ChatOpenAIAsync(List<LlmMessage> messages, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            top_p = options.TopP,
            stop = options.StopSequences
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("total_tokens").GetInt32(),
            FinishReason = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
        };
    }

    private async Task<LlmResponse> CallAzureOpenAIAsync(string prompt, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            prompt = prompt,
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            top_p = options.TopP,
            stop = options.StopSequences
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{_config.Endpoint}/openai/deployments/{_config.Model}/completions?api-version=2024-02-01");
        httpRequest.Headers.Add("api-key", _config.ApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Azure OpenAI API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("choices")[0].GetProperty("text").GetString() ?? string.Empty,
            Model = _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("total_tokens").GetInt32(),
            FinishReason = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
        };
    }

    private async Task<LlmResponse> ChatAzureOpenAIAsync(List<LlmMessage> messages, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            max_tokens = options.MaxTokens,
            temperature = options.Temperature,
            top_p = options.TopP,
            stop = options.StopSequences
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{_config.Endpoint}/openai/deployments/{_config.Model}/chat/completions?api-version=2024-02-01");
        httpRequest.Headers.Add("api-key", _config.ApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Azure OpenAI API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
            Model = _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("total_tokens").GetInt32(),
            FinishReason = root.GetProperty("choices")[0].GetProperty("finish_reason").GetString()
        };
    }

    private async Task<LlmResponse> CallAnthropicAsync(string prompt, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            max_tokens = options.MaxTokens,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = options.Temperature,
            top_p = options.TopP
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/v1/messages");
        httpRequest.Headers.Add("x-api-key", _config.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("input_tokens").GetInt32() +
                         root.GetProperty("usage").GetProperty("output_tokens").GetInt32(),
            FinishReason = root.GetProperty("stop_reason").GetString()
        };
    }

    private async Task<LlmResponse> ChatAnthropicAsync(List<LlmMessage> messages, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            max_tokens = options.MaxTokens,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = options.Temperature,
            top_p = options.TopP
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/v1/messages");
        httpRequest.Headers.Add("x-api-key", _config.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.GetProperty("usage").GetProperty("input_tokens").GetInt32() +
                         root.GetProperty("usage").GetProperty("output_tokens").GetInt32(),
            FinishReason = root.GetProperty("stop_reason").GetString()
        };
    }

    private async Task<LlmResponse> CallOllamaAsync(string prompt, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = options.Temperature,
                top_p = options.TopP,
                num_predict = options.MaxTokens
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/api/generate");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("response").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.TryGetProperty("eval_count", out var evalCount) ? evalCount.GetInt32() : 0,
            FinishReason = root.GetProperty("done").GetBoolean() ? "stop" : "length"
        };
    }

    private async Task<LlmResponse> ChatOllamaAsync(List<LlmMessage> messages, LlmOptions options, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.Model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new
            {
                temperature = options.Temperature,
                top_p = options.TopP,
                num_predict = options.MaxTokens
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/api/chat");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        return new LlmResponse
        {
            Success = true,
            Content = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
            Model = root.GetProperty("model").GetString() ?? _config.Model,
            TokensUsed = root.TryGetProperty("eval_count", out var evalCount) ? evalCount.GetInt32() : 0,
            FinishReason = root.GetProperty("done").GetBoolean() ? "stop" : "length"
        };
    }

    private async Task<LlmEmbeddingResponse> EmbedOpenAIAsync(string text, CancellationToken cancellationToken)
    {
        var request = new { input = text, model = _config.EmbeddingModel ?? "text-embedding-ada-002" };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/v1/embeddings");
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI Embeddings API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        var embedding = root.GetProperty("data")[0].GetProperty("embedding")
            .EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();

        return new LlmEmbeddingResponse
        {
            Success = true,
            Embedding = embedding,
            Model = root.GetProperty("model").GetString() ?? _config.EmbeddingModel ?? "text-embedding-ada-002"
        };
    }

    private async Task<LlmEmbeddingResponse> EmbedAzureOpenAIAsync(string text, CancellationToken cancellationToken)
    {
        var request = new { input = text };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{_config.Endpoint}/openai/deployments/{_config.EmbeddingModel}/embeddings?api-version=2024-02-01");
        httpRequest.Headers.Add("api-key", _config.ApiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Azure OpenAI Embeddings API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        var embedding = root.GetProperty("data")[0].GetProperty("embedding")
            .EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();

        return new LlmEmbeddingResponse
        {
            Success = true,
            Embedding = embedding,
            Model = _config.EmbeddingModel ?? "text-embedding-ada-002"
        };
    }

    private async Task<LlmEmbeddingResponse> EmbedOllamaAsync(string text, CancellationToken cancellationToken)
    {
        var request = new { model = _config.EmbeddingModel ?? _config.Model, prompt = text };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_config.Endpoint}/api/embeddings");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Ollama Embeddings API error: {content}");

        var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;
        var embedding = root.GetProperty("embedding")
            .EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();

        return new LlmEmbeddingResponse
        {
            Success = true,
            Embedding = embedding,
            Model = _config.EmbeddingModel ?? _config.Model
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient?.Dispose();
        _disposed = true;
    }
}

public class LlmConfig
{
    public string Provider { get; set; } = "openai"; // openai, azure, anthropic, ollama
    public string Endpoint { get; set; } = "https://api.openai.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string? EmbeddingModel { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}

public class LlmOptions
{
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 1.0;
    public List<string>? StopSequences { get; set; }
}

public class LlmMessage
{
    public string Role { get; set; } = "user"; // system, user, assistant
    public string Content { get; set; } = string.Empty;
}

public class LlmResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int TokensUsed { get; set; }
    public string? FinishReason { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class LlmEmbeddingResponse
{
    public bool Success { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string? Model { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}
