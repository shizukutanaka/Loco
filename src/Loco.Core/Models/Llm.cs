namespace Loco.Core.Models;

public enum LlmRole { System, User, Assistant }

public sealed record LlmMessage(LlmRole Role, string Content);

public sealed class LlmRequestOptions
{
    public string Provider { get; set; } = string.Empty; // e.g., openai|anthropic|gemini|ollama
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? SystemPrompt { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}
