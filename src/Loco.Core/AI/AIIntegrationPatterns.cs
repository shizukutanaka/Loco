#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AI;

public class LLMRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty; // gpt-4, claude-opus

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }

    [JsonPropertyName("latencyMs")]
    public double LatencyMs { get; set; }
}

public class PromptTemplate
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("template")]
    public string Template { get; set; } = string.Empty;

    [JsonPropertyName("variables")]
    public List<string> Variables { get; set; } = new();

    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = new();
}

public class AIEngine
{
    private readonly List<LLMRequest> _requests = new();
    private readonly Dictionary<string, PromptTemplate> _templates = new();
    private readonly ILogger<AIEngine> _logger;

    public AIEngine(ILogger<AIEngine> logger) => _logger = logger;

    public async Task RecordRequestAsync(LLMRequest request)
    {
        _requests.Add(request);
        _logger.LogInformation("LLM request: {Model} {Tokens} tokens", request.Model, request.Tokens);
    }

    public decimal GetTotalCost() => _requests.Sum(r => r.Cost);

    public Dictionary<string, object> GetStats() => new()
    {
        ["requests"] = _requests.Count,
        ["totalCost"] = GetTotalCost(),
        ["avgLatency"] = _requests.Average(r => r.LatencyMs)
    };
}

public static class AIExtensions
{
    public static IServiceCollection AddAIIntegration(this IServiceCollection services)
    {
        services.AddSingleton<AIEngine>();
        return services;
    }
}
