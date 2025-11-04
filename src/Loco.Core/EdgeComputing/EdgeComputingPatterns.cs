#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.EdgeComputing;

public class EdgeLocation
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("latencyToOriginMs")]
    public int LatencyToOriginMs { get; set; }

    [JsonPropertyName("capacity")]
    public int CapacityMB { get; set; }

    [JsonPropertyName("utilization")]
    public double UtilizationPercent { get; set; }
}

public class EdgeFunction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("codeSize")]
    public int CodeSizeBytes { get; set; }

    [JsonPropertyName("executionTime")]
    public int ExecutionTimeMs { get; set; }

    [JsonPropertyName("requestsPerSecond")]
    public double RequestsPerSecond { get; set; }
}

public class EdgeComputingEngine
{
    private readonly Dictionary<string, EdgeLocation> _locations = new();
    private readonly Dictionary<string, EdgeFunction> _functions = new();
    private readonly ILogger<EdgeComputingEngine> _logger;

    public EdgeComputingEngine(ILogger<EdgeComputingEngine> logger) => _logger = logger;

    public async Task RegisterEdgeLocationAsync(EdgeLocation location)
    {
        _locations[location.Id] = location;
        _logger.LogInformation("Registered edge location: {Name} ({Region})", location.Name, location.Region);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["edgeLocations"] = _locations.Count,
        ["edgeFunctions"] = _functions.Count
    };
}

public static class EdgeComputingExtensions
{
    public static IServiceCollection AddEdgeComputing(this IServiceCollection services)
    {
        services.AddSingleton<EdgeComputingEngine>();
        return services;
    }
}
