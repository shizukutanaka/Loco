#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.eBPF;

public class eBPFProgram
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // kprobe, tracepoint, XDP

    [JsonPropertyName("attached")]
    public bool Attached { get; set; } = false;

    [JsonPropertyName("events")]
    public long EventsProcessed { get; set; }
}

public class eBPFMetric
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class eBPFEngine
{
    private readonly Dictionary<string, eBPFProgram> _programs = new();
    private readonly List<eBPFMetric> _metrics = new();
    private readonly ILogger<eBPFEngine> _logger;

    public eBPFEngine(ILogger<eBPFEngine> logger) => _logger = logger;

    public async Task RegisterProgramAsync(eBPFProgram program)
    {
        _programs[program.Id] = program;
        _logger.LogInformation("Registered eBPF program: {Name} ({Type})", program.Name, program.Type);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["programs"] = _programs.Count,
        ["metrics"] = _metrics.Count
    };
}

public static class eBPFExtensions
{
    public static IServiceCollection AddeBPF(this IServiceCollection services)
    {
        services.AddSingleton<eBPFEngine>();
        return services;
    }
}
