#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.DataGovernance;

public class DataAsset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty; // Public, Internal, Confidential, Restricted

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("containsPII")]
    public bool ContainsPII { get; set; }

    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }
}

public class DifferentialPrivacy
{
    [JsonPropertyName("epsilon")]
    public double Epsilon { get; set; } = 1.0; // Privacy budget (lower = more private)

    [JsonPropertyName("delta")]
    public double Delta { get; set; } = 0.00001; // Probability of privacy breach

    [JsonPropertyName("mechanism")]
    public string Mechanism { get; set; } = "Laplace"; // Laplace, Gaussian
}

public class DataGovernanceEngine
{
    private readonly Dictionary<string, DataAsset> _assets = new();
    private readonly ILogger<DataGovernanceEngine> _logger;

    public DataGovernanceEngine(ILogger<DataGovernanceEngine> logger) => _logger = logger;

    public async Task RegisterDataAssetAsync(DataAsset asset)
    {
        _assets[asset.Id] = asset;
        _logger.LogInformation("Registered asset: {Name} ({Classification})", asset.Name, asset.Classification);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["assets"] = _assets.Count,
        ["withPII"] = _assets.Values.Count(a => a.ContainsPII)
    };
}

public static class DataGovernanceExtensions
{
    public static IServiceCollection AddDataGovernance(this IServiceCollection services)
    {
        services.AddSingleton<DataGovernanceEngine>();
        return services;
    }
}
