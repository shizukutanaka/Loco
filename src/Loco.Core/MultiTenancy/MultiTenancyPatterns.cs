#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.MultiTenancy;

public class Tenant
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isolationLevel")]
    public string IsolationLevel { get; set; } = "shared-schema";

    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";
}

public class TenantContext
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    [JsonPropertyName("dataFilters")]
    public Dictionary<string, string> DataFilters { get; set; } = new();
}

public class MultiTenancyEngine
{
    private readonly Dictionary<string, Tenant> _tenants = new();
    private readonly ILogger<MultiTenancyEngine> _logger;

    public MultiTenancyEngine(ILogger<MultiTenancyEngine> logger) => _logger = logger;

    public async Task RegisterTenantAsync(Tenant tenant)
    {
        _tenants[tenant.Id] = tenant;
        _logger.LogInformation("Registered tenant: {Name} (isolation={Level})", tenant.Name, tenant.IsolationLevel);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["tenants"] = _tenants.Count,
        ["sharedSchemas"] = _tenants.Values.Count(t => t.IsolationLevel == "shared-schema")
    };
}

public static class MultiTenancyExtensions
{
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services)
    {
        services.AddSingleton<MultiTenancyEngine>();
        return services;
    }
}
