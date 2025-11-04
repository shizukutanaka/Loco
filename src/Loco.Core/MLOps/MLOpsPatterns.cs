#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.MLOps;

public class MLModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty;

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("latencyMs")]
    public double LatencyMs { get; set; }
}

public class ModelTrainingPipeline
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("hyperparameters")]
    public Dictionary<string, double> Hyperparameters { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
}

public class ModelDeployment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "deploying";
}

public class MLOpsEngine
{
    private readonly Dictionary<string, MLModel> _models = new();
    private readonly Dictionary<string, ModelTrainingPipeline> _pipelines = new();
    private readonly Dictionary<string, ModelDeployment> _deployments = new();
    private readonly ILogger<MLOpsEngine> _logger;

    public MLOpsEngine(ILogger<MLOpsEngine> logger) => _logger = logger;

    public async Task RegisterModelAsync(MLModel model)
    {
        _models[model.Id] = model;
        _logger.LogInformation("Registered model: {Name} v{Version}", model.Name, model.Version);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["models"] = _models.Count,
        ["deployments"] = _deployments.Count
    };
}

public static class MLOpsExtensions
{
    public static IServiceCollection AddMLOps(this IServiceCollection services)
    {
        services.AddSingleton<MLOpsEngine>();
        return services;
    }
}
