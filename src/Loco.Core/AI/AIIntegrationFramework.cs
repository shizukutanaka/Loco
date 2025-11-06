// Loco AI Integration Framework
// Inspired by n8n's AI nodes and modern LLM orchestration needs
// Built for the AI-native workflow era of 2025

using System.Text.Json;

namespace Loco.Core.AI;

/// <summary>
/// AI provider interface - supports OpenAI, Claude, and custom LLMs
/// </summary>
public interface IAIProvider
{
    string Name { get; }
    Task<AIResponse> CompleteChatAsync(AIRequest request, CancellationToken ct = default);
    Task<AIResponse> CompleteTextAsync(string prompt, AIOptions? options = null, CancellationToken ct = default);
    Task<EmbeddingResponse> CreateEmbeddingAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// AI request with full control over parameters
/// </summary>
public class AIRequest
{
    public List<AIMessage> Messages { get; set; } = new();
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1000;
    public double TopP { get; set; } = 1.0;
    public int N { get; set; } = 1;
    public List<string>? Stop { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AIMessage
{
    public string Role { get; set; } = "user"; // system, user, assistant
    public string Content { get; set; } = "";
}

public class AIOptions
{
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1000;
}

/// <summary>
/// AI response with usage tracking
/// </summary>
public class AIResponse
{
    public string Content { get; set; } = "";
    public string Model { get; set; } = "";
    public AIUsage Usage { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class EmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string Model { get; set; } = "";
    public AIUsage Usage { get; set; } = new();
}

/// <summary>
/// OpenAI provider - GPT-4, GPT-3.5-turbo, embeddings
/// </summary>
public class OpenAIProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public string Name => "OpenAI";

    public OpenAIProvider(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<AIResponse> CompleteChatAsync(AIRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        var payload = new
        {
            model = string.IsNullOrEmpty(request.Model) ? "gpt-4" : request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            n = request.N,
            stop = request.Stop
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var choice = result.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var usage = result.GetProperty("usage");

        var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

        return new AIResponse
        {
            Content = message.GetProperty("content").GetString() ?? "",
            Model = result.GetProperty("model").GetString() ?? "",
            Usage = new AIUsage
            {
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                EstimatedCost = CalculateOpenAICost(request.Model, promptTokens, completionTokens)
            },
            Duration = DateTime.UtcNow - startTime
        };
    }

    public async Task<AIResponse> CompleteTextAsync(string prompt, AIOptions? options = null, CancellationToken ct = default)
    {
        var request = new AIRequest
        {
            Messages = new List<AIMessage> { new() { Role = "user", Content = prompt } },
            Model = options?.Model ?? "gpt-4",
            Temperature = options?.Temperature ?? 0.7,
            MaxTokens = options?.MaxTokens ?? 1000
        };

        return await CompleteChatAsync(request, ct);
    }

    public async Task<EmbeddingResponse> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var payload = new { input = text, model = "text-embedding-ada-002" };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/embeddings", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var embedding = result.GetProperty("data")[0].GetProperty("embedding");
        var embeddingArray = embedding.EnumerateArray().Select(e => e.GetSingle()).ToArray();

        var usage = result.GetProperty("usage");
        var totalTokens = usage.GetProperty("total_tokens").GetInt32();

        return new EmbeddingResponse
        {
            Embedding = embeddingArray,
            Model = "text-embedding-ada-002",
            Usage = new AIUsage { TotalTokens = totalTokens, EstimatedCost = totalTokens * 0.0001m / 1000 }
        };
    }

    private decimal CalculateOpenAICost(string model, int promptTokens, int completionTokens)
    {
        // OpenAI pricing per 1K tokens (approximate 2025 rates)
        return model switch
        {
            "gpt-4" => (promptTokens * 0.03m + completionTokens * 0.06m) / 1000,
            "gpt-4-turbo" => (promptTokens * 0.01m + completionTokens * 0.03m) / 1000,
            "gpt-3.5-turbo" => (promptTokens * 0.0005m + completionTokens * 0.0015m) / 1000,
            _ => (promptTokens * 0.01m + completionTokens * 0.03m) / 1000 // default
        };
    }
}

/// <summary>
/// Anthropic Claude provider - Claude 3 family
/// </summary>
public class ClaudeProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public string Name => "Anthropic Claude";

    public ClaudeProvider(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<AIResponse> CompleteChatAsync(AIRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        var payload = new
        {
            model = string.IsNullOrEmpty(request.Model) ? "claude-3-sonnet-20240229" : request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            max_tokens = request.MaxTokens,
            temperature = request.Temperature
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var contentBlock = result.GetProperty("content")[0];
        var usage = result.GetProperty("usage");

        var inputTokens = usage.GetProperty("input_tokens").GetInt32();
        var outputTokens = usage.GetProperty("output_tokens").GetInt32();

        return new AIResponse
        {
            Content = contentBlock.GetProperty("text").GetString() ?? "",
            Model = result.GetProperty("model").GetString() ?? "",
            Usage = new AIUsage
            {
                PromptTokens = inputTokens,
                CompletionTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                EstimatedCost = CalculateClaudeCost(request.Model, inputTokens, outputTokens)
            },
            Duration = DateTime.UtcNow - startTime
        };
    }

    public async Task<AIResponse> CompleteTextAsync(string prompt, AIOptions? options = null, CancellationToken ct = default)
    {
        var request = new AIRequest
        {
            Messages = new List<AIMessage> { new() { Role = "user", Content = prompt } },
            Model = options?.Model ?? "claude-3-sonnet-20240229",
            Temperature = options?.Temperature ?? 0.7,
            MaxTokens = options?.MaxTokens ?? 1000
        };

        return await CompleteChatAsync(request, ct);
    }

    public Task<EmbeddingResponse> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        throw new NotSupportedException("Claude does not support embeddings. Use OpenAI or another provider.");
    }

    private decimal CalculateClaudeCost(string model, int inputTokens, int outputTokens)
    {
        // Anthropic pricing per 1M tokens (approximate 2025 rates)
        return model switch
        {
            "claude-3-opus-20240229" => (inputTokens * 15m + outputTokens * 75m) / 1_000_000,
            "claude-3-sonnet-20240229" => (inputTokens * 3m + outputTokens * 15m) / 1_000_000,
            "claude-3-haiku-20240307" => (inputTokens * 0.25m + outputTokens * 1.25m) / 1_000_000,
            _ => (inputTokens * 3m + outputTokens * 15m) / 1_000_000 // default to sonnet
        };
    }
}

/// <summary>
/// AI workflow orchestrator - manages prompts, chains, and costs
/// </summary>
public class AIOrchestrator
{
    private readonly Dictionary<string, IAIProvider> _providers = new();
    private readonly List<AIWorkflowStep> _history = new();

    public void RegisterProvider(string name, IAIProvider provider)
    {
        _providers[name] = provider;
    }

    public async Task<AIResponse> ExecuteAsync(string providerName, AIRequest request, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
            throw new InvalidOperationException($"Provider '{providerName}' not registered");

        var response = await provider.CompleteChatAsync(request, ct);

        _history.Add(new AIWorkflowStep
        {
            Provider = providerName,
            Request = request,
            Response = response,
            Timestamp = DateTime.UtcNow
        });

        return response;
    }

    public async Task<AIResponse> ChainAsync(params (string provider, string prompt)[] steps)
    {
        AIResponse? lastResponse = null;
        var combinedPrompt = "";

        foreach (var (provider, prompt) in steps)
        {
            var actualPrompt = lastResponse != null ? $"{combinedPrompt}\n{lastResponse.Content}\n{prompt}" : prompt;

            lastResponse = await ExecuteAsync(provider, new AIRequest
            {
                Messages = new List<AIMessage> { new() { Role = "user", Content = actualPrompt } }
            });

            combinedPrompt = actualPrompt;
        }

        return lastResponse!;
    }

    public AIWorkflowStats GetStats()
    {
        return new AIWorkflowStats
        {
            TotalRequests = _history.Count,
            TotalTokens = _history.Sum(h => h.Response.Usage.TotalTokens),
            TotalCost = _history.Sum(h => h.Response.Usage.EstimatedCost),
            AverageDuration = TimeSpan.FromMilliseconds(_history.Average(h => h.Response.Duration.TotalMilliseconds)),
            ProviderBreakdown = _history.GroupBy(h => h.Provider)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

public class AIWorkflowStep
{
    public string Provider { get; set; } = "";
    public AIRequest Request { get; set; } = null!;
    public AIResponse Response { get; set; } = null!;
    public DateTime Timestamp { get; set; }
}

public class AIWorkflowStats
{
    public int TotalRequests { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public Dictionary<string, int> ProviderBreakdown { get; set; } = new();
}

/// <summary>
/// Prompt template manager - reusable prompts with variables
/// </summary>
public class PromptTemplate
{
    public string Name { get; set; } = "";
    public string Template { get; set; } = "";
    public List<string> Variables { get; set; } = new();

    public string Render(Dictionary<string, string> values)
    {
        var result = Template;
        foreach (var variable in Variables)
        {
            if (values.TryGetValue(variable, out var value))
            {
                result = result.Replace($"{{{{{variable}}}}}", value);
            }
        }
        return result;
    }

    public static PromptTemplate Create(string name, string template)
    {
        // Extract variables from {{variable}} syntax
        var variables = System.Text.RegularExpressions.Regex
            .Matches(template, @"\{\{(\w+)\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        return new PromptTemplate
        {
            Name = name,
            Template = template,
            Variables = variables
        };
    }
}

/// <summary>
/// Example usage demonstrating the AI framework
/// </summary>
public class AIFrameworkExamples
{
    public static async Task DemonstrationAsync()
    {
        // Setup providers
        var openai = new OpenAIProvider("sk-your-openai-key");
        var claude = new ClaudeProvider("sk-your-anthropic-key");

        var orchestrator = new AIOrchestrator();
        orchestrator.RegisterProvider("openai", openai);
        orchestrator.RegisterProvider("claude", claude);

        // Simple completion
        var response = await openai.CompleteTextAsync("Explain quantum computing in one sentence.");
        Console.WriteLine($"Response: {response.Content}");
        Console.WriteLine($"Cost: ${response.Usage.EstimatedCost:F4}");

        // Chat with context
        var chatRequest = new AIRequest
        {
            Messages = new List<AIMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant." },
                new() { Role = "user", Content = "What is the capital of France?" },
                new() { Role = "assistant", Content = "The capital of France is Paris." },
                new() { Role = "user", Content = "What is its population?" }
            },
            Model = "gpt-4",
            Temperature = 0.7,
            MaxTokens = 500
        };

        var chatResponse = await openai.CompleteChatAsync(chatRequest);
        Console.WriteLine($"Chat response: {chatResponse.Content}");

        // Prompt templates
        var template = PromptTemplate.Create("summarize", "Summarize the following text in {{max_words}} words:\n\n{{text}}");
        var prompt = template.Render(new Dictionary<string, string>
        {
            ["max_words"] = "50",
            ["text"] = "Long article text here..."
        });

        // AI chain (multi-step)
        var chainResult = await orchestrator.ChainAsync(
            ("openai", "Generate a creative story idea about space exploration."),
            ("claude", "Expand on this idea with more details about the characters."),
            ("openai", "Write the first paragraph of this story.")
        );

        Console.WriteLine($"Final result: {chainResult.Content}");

        // Get stats
        var stats = orchestrator.GetStats();
        Console.WriteLine($"Total requests: {stats.TotalRequests}");
        Console.WriteLine($"Total cost: ${stats.TotalCost:F4}");
        Console.WriteLine($"Average duration: {stats.AverageDuration.TotalMilliseconds}ms");
    }
}
