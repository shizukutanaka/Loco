#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.IncidentManagement;

public class Incident
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty; // Critical, Major, Minor

    [JsonPropertyName("status")]
    public string Status { get; set; } = "open"; // open, resolved, closed

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
}

public class BlamelessPostmortem
{
    [JsonPropertyName("incidentId")]
    public string IncidentId { get; set; } = string.Empty;

    [JsonPropertyName("timeline")]
    public List<string> Timeline { get; set; } = new();

    [JsonPropertyName("rootCauses")]
    public List<string> RootCauses { get; set; } = new();

    [JsonPropertyName("actionItems")]
    public List<string> ActionItems { get; set; } = new();

    [JsonPropertyName("lessonsLearned")]
    public string LessonsLearned { get; set; } = string.Empty;

    [JsonPropertyName("blameFree")]
    public bool BlameFree { get; set; } = true;
}

public class IncidentResponseEngine
{
    private readonly Dictionary<string, Incident> _incidents = new();
    private readonly List<BlamelessPostmortem> _postmortems = new();
    private readonly ILogger<IncidentResponseEngine> _logger;

    public IncidentResponseEngine(ILogger<IncidentResponseEngine> logger) => _logger = logger;

    public async Task CreateIncidentAsync(Incident incident)
    {
        _incidents[incident.Id] = incident;
        _logger.LogInformation("Incident created: {Title} ({Severity})", incident.Title, incident.Severity);
    }

    public double GetMTTR() => _incidents.Values
        .Where(i => i.EndTime.HasValue)
        .Average(i => (i.EndTime!.Value - i.StartTime).TotalMinutes);

    public Dictionary<string, object> GetStats() => new()
    {
        ["openIncidents"] = _incidents.Values.Count(i => i.Status == "open"),
        ["postmortems"] = _postmortems.Count,
        ["MTTR"] = GetMTTR()
    };
}

public static class IncidentExtensions
{
    public static IServiceCollection AddIncidentResponse(this IServiceCollection services)
    {
        services.AddSingleton<IncidentResponseEngine>();
        return services;
    }
}
