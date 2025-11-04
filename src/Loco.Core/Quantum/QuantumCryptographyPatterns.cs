#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Quantum;

public class QuantumAlgorithm
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // KeyEncapsulation, Signature

    [JsonPropertyName("status")]
    public string Status { get; set; } = "standardized"; // standardized, backup, experimental

    [JsonPropertyName("standardVersion")]
    public string StandardVersion { get; set; } = string.Empty; // FIPS 203, 204, 205

    [JsonPropertyName("implementedAt")]
    public DateTime ImplementedAt { get; set; } = DateTime.UtcNow;
}

public class HybridCrypto
{
    [JsonPropertyName("classicalAlgorithm")]
    public string ClassicalAlgorithm { get; set; } = "RSA-2048";

    [JsonPropertyName("postQuantumAlgorithm")]
    public string PostQuantumAlgorithm { get; set; } = "ML-KEM-768";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("encryptionOverhead")]
    public double EncryptionOverhead { get; set; } = 1.15; // 15% larger ciphertexts
}

public class QuantumCryptographyEngine
{
    private readonly List<QuantumAlgorithm> _algorithms = new();
    private readonly HybridCrypto _hybridConfig = new();
    private readonly ILogger<QuantumCryptographyEngine> _logger;

    public QuantumCryptographyEngine(ILogger<QuantumCryptographyEngine> logger) => _logger = logger;

    public async Task RegisterQuantumAlgorithmAsync(QuantumAlgorithm algo)
    {
        _algorithms.Add(algo);
        _logger.LogInformation("Registered: {Name} (NIST {Status})", algo.Name, algo.StandardVersion);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["algorithms"] = _algorithms.Count,
        ["hybridEnabled"] = _hybridConfig.Enabled
    };
}

public static class QuantumExtensions
{
    public static IServiceCollection AddQuantumCryptography(this IServiceCollection services)
    {
        services.AddSingleton<QuantumCryptographyEngine>();
        return services;
    }
}
