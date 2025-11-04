#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Database;

public class ShardKey
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "range";

    [JsonPropertyName("cardinality")]
    public int Cardinality { get; set; }
}

public class Shard
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("range")]
    public (long min, long max) Range { get; set; }

    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 3;

    [JsonPropertyName("rowCount")]
    public long RowCount { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

public class DatabaseShardingEngine
{
    private readonly Dictionary<int, Shard> _shards = new();
    private readonly ILogger<DatabaseShardingEngine> _logger;

    public DatabaseShardingEngine(ILogger<DatabaseShardingEngine> logger) => _logger = logger;

    public async Task CreateShardAsync(Shard shard)
    {
        _shards[shard.Id] = shard;
        _logger.LogInformation("Created shard {Id}: range={Min}-{Max}", shard.Id, shard.Range.min, shard.Range.max);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["shards"] = _shards.Count,
        ["totalRows"] = _shards.Values.Sum(s => s.RowCount)
    };
}

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseSharding(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseShardingEngine>();
        return services;
    }
}
