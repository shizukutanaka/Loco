#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Blockchain;

public class SmartContract
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = string.Empty; // Ethereum, Solana, Cardano

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("executions")]
    public long Executions { get; set; }

    [JsonPropertyName("gasUsed")]
    public long GasUsed { get; set; }
}

public class BlockchainTransaction
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, confirmed, failed

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }
}

public class BlockchainEngine
{
    private readonly Dictionary<string, SmartContract> _contracts = new();
    private readonly List<BlockchainTransaction> _transactions = new();
    private readonly ILogger<BlockchainEngine> _logger;

    public BlockchainEngine(ILogger<BlockchainEngine> logger) => _logger = logger;

    public async Task RegisterSmartContractAsync(SmartContract contract)
    {
        _contracts[contract.Address] = contract;
        _logger.LogInformation("Smart contract: {Name} on {Platform}", contract.Name, contract.Platform);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["contracts"] = _contracts.Count,
        ["transactions"] = _transactions.Count
    };
}

public static class BlockchainExtensions
{
    public static IServiceCollection AddBlockchain(this IServiceCollection services)
    {
        services.AddSingleton<BlockchainEngine>();
        return services;
    }
}
