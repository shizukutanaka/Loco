#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Sustainability;

public class CarbonFootprint
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("kgCO2")]
    public double KgCO2 { get; set; }

    [JsonPropertyName("energyWh")]
    public double EnergyWh { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("gridIntensity")]
    public double GridIntensity { get; set; } = 0.5; // kg CO2 per kWh

    [JsonPropertyName("renewablePercent")]
    public double RenewablePercent { get; set; } = 0;
}

public class SustainabilityEngine
{
    private readonly List<CarbonFootprint> _footprints = new();
    private readonly ILogger<SustainabilityEngine> _logger;

    public SustainabilityEngine(ILogger<SustainabilityEngine> logger) => _logger = logger;

    public async Task RecordCarbonAsync(CarbonFootprint footprint)
    {
        _footprints.Add(footprint);
        _logger.LogInformation("Carbon tracked: {Resource} {KgCO2}kg CO2", footprint.ResourceId, footprint.KgCO2);
    }

    public double GetTotalCarbonKgCO2() => _footprints.Sum(f => f.KgCO2);

    public Dictionary<string, object> GetStats() => new()
    {
        ["totalCarbonKgCO2"] = GetTotalCarbonKgCO2(),
        ["readings"] = _footprints.Count
    };
}

public static class SustainabilityExtensions
{
    public static IServiceCollection AddSustainability(this IServiceCollection services)
    {
        services.AddSingleton<SustainabilityEngine>();
        return services;
    }
}
