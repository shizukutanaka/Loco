#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.SupplyChain;

public class SBOM
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = "CycloneDX"; // CycloneDX, SPDX

    [JsonPropertyName("dependencies")]
    public List<Dependency> Dependencies { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Dependency
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("license")]
    public string License { get; set; } = string.Empty;

    [JsonPropertyName("vulnerabilities")]
    public List<string> Vulnerabilities { get; set; } = new();
}

public class SLSAAttestation
{
    [JsonPropertyName("artifact")]
    public string Artifact { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 2; // 1-4

    [JsonPropertyName("builderVersion")]
    public string BuilderVersion { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class SupplyChainEngine
{
    private readonly Dictionary<string, SBOM> _sboms = new();
    private readonly List<SLSAAttestation> _attestations = new();
    private readonly ILogger<SupplyChainEngine> _logger;

    public SupplyChainEngine(ILogger<SupplyChainEngine> logger) => _logger = logger;

    public async Task RegisterSBOMAsync(SBOM sbom)
    {
        _sboms[sbom.Id] = sbom;
        _logger.LogInformation("Registered SBOM v{Version} with {Count} dependencies", sbom.Version, sbom.Dependencies.Count);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["sboms"] = _sboms.Count,
        ["attestations"] = _attestations.Count
    };
}

public static class SupplyChainExtensions
{
    public static IServiceCollection AddSupplyChainSecurity(this IServiceCollection services)
    {
        services.AddSingleton<SupplyChainEngine>();
        return services;
    }
}
