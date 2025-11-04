#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.WebAssembly;

public class WasmModule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public int SizeBytes { get; set; }

    [JsonPropertyName("coldStartMs")]
    public double ColdStartMs { get; set; } = 0.5; // Sub-millisecond

    [JsonPropertyName("runtimeMs")]
    public double RuntimeMs { get; set; }

    [JsonPropertyName("memory")]
    public int MemoryMB { get; set; } = 5; // ~5MB per instance
}

public class WebAssemblyEngine
{
    private readonly Dictionary<string, WasmModule> _modules = new();
    private readonly ILogger<WebAssemblyEngine> _logger;

    public WebAssemblyEngine(ILogger<WebAssemblyEngine> logger) => _logger = logger;

    public async Task RegisterModuleAsync(WasmModule module)
    {
        _modules[module.Id] = module;
        _logger.LogInformation("Registered WASM: {Name} ({Size}KB, {ColdStart}ms)", 
            module.Name, module.SizeBytes / 1024, module.ColdStartMs);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["modules"] = _modules.Count,
        ["totalSize"] = _modules.Values.Sum(m => m.SizeBytes)
    };
}

public static class WebAssemblyExtensions
{
    public static IServiceCollection AddWebAssembly(this IServiceCollection services)
    {
        services.AddSingleton<WebAssemblyEngine>();
        return services;
    }
}
