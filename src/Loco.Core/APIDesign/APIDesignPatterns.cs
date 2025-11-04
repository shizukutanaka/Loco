#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.APIDesign;

public class APIVersion
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("deprecationDate")]
    public DateTime? DeprecationDate { get; set; }

    [JsonPropertyName("endpoints")]
    public List<string> Endpoints { get; set; } = new();
}

public class APIEndpoint
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("requestSchema")]
    public string RequestSchema { get; set; } = string.Empty;

    [JsonPropertyName("responseSchema")]
    public string ResponseSchema { get; set; } = string.Empty;

    [JsonPropertyName("rateLimit")]
    public int RateLimit { get; set; } = 100;

    [JsonPropertyName("authentication")]
    public string Authentication { get; set; } = "required";
}

public class APIGatewayConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("versions")]
    public List<APIVersion> Versions { get; set; } = new();

    [JsonPropertyName("rateLimitingStrategy")]
    public string RateLimitingStrategy { get; set; } = "token-bucket";

    [JsonPropertyName("caching")]
    public bool CachingEnabled { get; set; } = true();

    [JsonPropertyName("compression")]
    public bool CompressionEnabled { get; set; } = true();
}

public class APIDesignEngine
{
    private readonly Dictionary<string, APIEndpoint> _endpoints = new();
    private readonly Dictionary<string, APIVersion> _versions = new();
    private readonly ILogger<APIDesignEngine> _logger;

    public APIDesignEngine(ILogger<APIDesignEngine> logger) => _logger = logger;

    public async Task RegisterEndpointAsync(APIEndpoint endpoint)
    {
        _endpoints[endpoint.Path] = endpoint;
        _logger.LogInformation("Registered endpoint: {Method} {Path}", endpoint.Method, endpoint.Path);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["endpoints"] = _endpoints.Count,
        ["versions"] = _versions.Count
    };
}

public static class APIDesignExtensions
{
    public static IServiceCollection AddAPIDesign(this IServiceCollection services)
    {
        services.AddSingleton<APIDesignEngine>();
        return services;
    }
}
