// John Carmack: "Simple code is not necessarily easy to write"
// Rob Pike: "Design the data structures and the code will follow"

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loco.Core.Practical;

namespace Loco.Core.AI;

/// <summary>
/// Azure OpenAI provider for workflow generation
/// Supports GPT-4, GPT-4-Turbo, GPT-3.5-Turbo
/// </summary>
public sealed class AzureOpenAIProvider : ILLMProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _deploymentName;
    private readonly string _apiVersion;
    private readonly SimpleLogger _logger;

    public string Name => "Azure OpenAI";
    public bool IsAvailable { get; private set; }

    public AzureOpenAIProvider(
        string endpoint,
        string apiKey,
        string deploymentName,
        string apiVersion = "2024-02-15-preview")
    {
        _endpoint = endpoint.TrimEnd('/');
        _deploymentName = deploymentName;
        _apiVersion = apiVersion;
        _logger = SimpleLoggerFactory.GetLogger(nameof(AzureOpenAIProvider));

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        _httpClient.Timeout = TimeSpan.FromMinutes(2);

        IsAvailable = !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey);
    }

    public async Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        var url = $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}";

        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are an expert workflow automation engineer. Generate valid JSON workflows." },
                new { role = "user", content = prompt }
            },
            temperature = temperature,
            max_tokens = 4000,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"Azure OpenAI error: {response.StatusCode} - {responseContent}");
                throw new Exception($"Azure OpenAI API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
        }
        catch (Exception ex)
        {
            _logger.Error($"Azure OpenAI completion failed", ex);
            throw;
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var url = $"{_endpoint}/openai/deployments/{_deploymentName}/embeddings?api-version={_apiVersion}";

        var request = new { input = text, model = "text-embedding-ada-002" };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Embedding failed: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent);
        return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// Ollama provider for local LLM inference
/// Supports Llama 3, Mixtral, CodeLlama, and other open models
/// </summary>
public sealed class OllamaProvider : ILLMProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly SimpleLogger _logger;

    public string Name => "Ollama";
    public bool IsAvailable { get; private set; }

    public OllamaProvider(
        string endpoint = "http://localhost:11434",
        string model = "llama3:70b")
    {
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
        _logger = SimpleLoggerFactory.GetLogger(nameof(OllamaProvider));

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Local inference can be slow

        // Check availability
        CheckAvailabilityAsync().ConfigureAwait(false);
    }

    private async Task CheckAvailabilityAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_endpoint}/api/tags");
            IsAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        var url = $"{_endpoint}/api/generate";

        var request = new
        {
            model = _model,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = temperature,
                num_predict = 4000
            },
            format = "json"
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"Ollama error: {response.StatusCode} - {responseContent}");
                throw new Exception($"Ollama API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
            return result?.Response ?? "";
        }
        catch (Exception ex)
        {
            _logger.Error($"Ollama completion failed", ex);
            throw;
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var url = $"{_endpoint}/api/embeddings";

        var request = new { model = _model, prompt = text };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Ollama embedding failed: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseContent);
        return result?.Embedding ?? Array.Empty<float>();
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// OpenAI API provider (non-Azure)
/// </summary>
public sealed class OpenAIProvider : ILLMProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly SimpleLogger _logger;

    public string Name => "OpenAI";
    public bool IsAvailable { get; }

    public OpenAIProvider(string apiKey, string model = "gpt-4-turbo-preview")
    {
        _model = model;
        _logger = SimpleLoggerFactory.GetLogger(nameof(OpenAIProvider));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        IsAvailable = !string.IsNullOrEmpty(apiKey);
    }

    public async Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are an expert workflow automation engineer. Generate valid JSON workflows." },
                new { role = "user", content = prompt }
            },
            temperature = temperature,
            max_tokens = 4000,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/chat/completions", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error($"OpenAI error: {response.StatusCode} - {responseContent}");
            throw new Exception($"OpenAI API error: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new { model = "text-embedding-3-small", input = text };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/embeddings", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenAI embedding failed: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent);
        return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// Anthropic Claude provider
/// </summary>
public sealed class ClaudeProvider : ILLMProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly SimpleLogger _logger;

    public string Name => "Claude";
    public bool IsAvailable { get; }

    public ClaudeProvider(string apiKey, string model = "claude-3-sonnet-20240229")
    {
        _model = model;
        _logger = SimpleLoggerFactory.GetLogger(nameof(ClaudeProvider));

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        IsAvailable = !string.IsNullOrEmpty(apiKey);
    }

    public async Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            max_tokens = 4000,
            temperature = temperature,
            system = "You are an expert workflow automation engineer. Generate valid JSON workflows.",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("v1/messages", content, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error($"Claude error: {response.StatusCode} - {responseContent}");
            throw new Exception($"Claude API error: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseContent);
        return result?.Content?.FirstOrDefault()?.Text ?? "";
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // Claude doesn't have a native embedding API
        throw new NotSupportedException("Claude does not support embeddings. Use OpenAI or Ollama.");
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// LLM router that tries multiple providers
/// </summary>
public sealed class LLMRouter : ILLMProvider
{
    private readonly List<ILLMProvider> _providers;
    private readonly SimpleLogger _logger;

    public string Name => "Router";
    public bool IsAvailable => _providers.Any(p => p.IsAvailable);

    public LLMRouter(params ILLMProvider[] providers)
    {
        _providers = providers.ToList();
        _logger = SimpleLoggerFactory.GetLogger(nameof(LLMRouter));
    }

    public async Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        foreach (var provider in _providers.Where(p => p.IsAvailable))
        {
            try
            {
                _logger.Debug($"Trying provider: {provider.Name}");
                return await provider.CompleteAsync(prompt, temperature, ct);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Provider {provider.Name} failed: {ex.Message}");
            }
        }

        throw new Exception("All LLM providers failed");
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        foreach (var provider in _providers.Where(p => p.IsAvailable))
        {
            try
            {
                return await provider.EmbedAsync(text, ct);
            }
            catch (NotSupportedException)
            {
                continue;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Provider {provider.Name} embedding failed: {ex.Message}");
            }
        }

        throw new Exception("All embedding providers failed");
    }
}

/// <summary>
/// Mock LLM provider for testing
/// </summary>
public sealed class MockLLMProvider : ILLMProvider
{
    private readonly Dictionary<string, string> _responses = new();
    private string _defaultResponse = "{}";

    public string Name => "Mock";
    public bool IsAvailable => true;

    public void SetResponse(string promptContains, string response)
    {
        _responses[promptContains.ToLowerInvariant()] = response;
    }

    public void SetDefaultResponse(string response)
    {
        _defaultResponse = response;
    }

    public Task<string> CompleteAsync(
        string prompt,
        float temperature = 0.7f,
        CancellationToken ct = default)
    {
        var lowerPrompt = prompt.ToLowerInvariant();

        foreach (var kvp in _responses)
        {
            if (lowerPrompt.Contains(kvp.Key))
            {
                return Task.FromResult(kvp.Value);
            }
        }

        return Task.FromResult(_defaultResponse);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // Return a deterministic fake embedding based on text hash
        var hash = text.GetHashCode();
        var embedding = new float[1536];
        var random = new Random(hash);

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return Task.FromResult(embedding);
    }
}

// Response DTOs
internal sealed class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAIChoice>? Choices { get; set; }
}

internal sealed class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }
}

internal sealed class OpenAIMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal sealed class OllamaResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
}

internal sealed class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}

internal sealed class EmbeddingResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingData>? Data { get; set; }
}

internal sealed class EmbeddingData
{
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}

internal sealed class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContent>? Content { get; set; }
}

internal sealed class ClaudeContent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
