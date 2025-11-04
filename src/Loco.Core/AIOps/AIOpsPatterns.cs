#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AIOps;

public class AnomalyDetection
{
    [JsonPropertyName("metricName")]
    public string MetricName { get; set; } = string.Empty;

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    [JsonPropertyName("sensitivity")]
    public double Sensitivity { get; set; } = 0.9;

    [JsonPropertyName("enabledML")]
    public bool EnabledML { get; set; } = true;
}

public class RemediationAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("anomalyType")]
    public string AnomalyType { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("autoExecute")]
    public bool AutoExecute { get; set; } = false;

    [JsonPropertyName("successRate")]
    public double SuccessRate { get; set; }
}

public class AIOpsEngine
{
    private readonly ConcurrentDictionary<string, AnomalyDetection> _detections = new();
    private readonly List<RemediationAction> _remediations = new();
    private readonly ILogger<AIOpsEngine> _logger;

    public AIOpsEngine(ILogger<AIOpsEngine> logger) => _logger = logger;

    public async Task RegisterAnomalyDetectionAsync(AnomalyDetection detection)
    {
        _detections[detection.MetricName] = detection;
        _logger.LogInformation("Registered anomaly detection: {Metric}", detection.MetricName);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["detections"] = _detections.Count,
        ["remediations"] = _remediations.Count
    };
}

public static class AIOpsExtensions
{
    public static IServiceCollection AddAIOps(this IServiceCollection services)
    {
        services.AddSingleton<AIOpsEngine>();
        return services;
    }
}
