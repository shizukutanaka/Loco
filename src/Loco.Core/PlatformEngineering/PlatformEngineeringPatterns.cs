#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.PlatformEngineering;

/// <summary>
/// Platform Engineering & Developer Experience Patterns
/// Internal Developer Platforms (IDP), Self-Service, Developer Tools
/// </summary>

/// <summary>
/// Developer template - starter kit for new services
/// </summary>
public class DeveloperTemplate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public Dictionary<string, string> Files { get; set; } = new(); // Path -> Content

    [JsonPropertyName("variables")]
    public Dictionary<string, string> Variables { get; set; } = new(); // For templating

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Platform component - shared library/tool
/// </summary>
public class PlatformComponent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Logging, Monitoring, Security, Database

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("documentation")]
    public string Documentation { get; set; } = string.Empty;

    [JsonPropertyName("examples")]
    public List<CodeExample> Examples { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public List<Dependency> Dependencies { get; set; } = new();

    [JsonPropertyName("maintainers")]
    public List<string> Maintainers { get; set; } = new();
}

/// <summary>
/// Code example for platform component
/// </summary>
public class CodeExample
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Dependency
/// </summary>
public class Dependency
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "runtime"; // runtime, dev, optional
}

/// <summary>
/// Internal Developer Platform (IDP)
/// </summary>
public class InternalDeveloperPlatform
{
    private readonly Dictionary<string, DeveloperTemplate> _templates = new();
    private readonly Dictionary<string, PlatformComponent> _components = new();
    private readonly Dictionary<string, DeveloperService> _services = new();
    private readonly ILogger<InternalDeveloperPlatform> _logger;

    public InternalDeveloperPlatform(ILogger<InternalDeveloperPlatform> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register developer template
    /// </summary>
    public async Task RegisterTemplateAsync(DeveloperTemplate template)
    {
        _templates[template.Id] = template;

        _logger.LogInformation(
            "Registered template: {Name} ({Language}/{Framework})",
            template.Name,
            template.Language,
            template.Framework);
    }

    /// <summary>
    /// Register platform component
    /// </summary>
    public async Task RegisterComponentAsync(PlatformComponent component)
    {
        _components[component.Id] = component;

        _logger.LogInformation(
            "Registered component: {Name} v{Version}",
            component.Name,
            component.Version);
    }

    /// <summary>
    /// Create new service from template
    /// </summary>
    public async Task<DeveloperService> CreateServiceFromTemplateAsync(
        string serviceName,
        string templateId,
        Dictionary<string, string>? variables = null)
    {
        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new InvalidOperationException("Template not found");
        }

        var service = new DeveloperService
        {
            Name = serviceName,
            TemplateId = templateId,
            Repository = $"https://github.com/org/{serviceName}",
            Status = "Provisioning"
        };

        _services[service.Id] = service;

        _logger.LogInformation(
            "Created service from template: {ServiceName} ({TemplateName})",
            serviceName,
            template.Name);

        return service;
    }

    /// <summary>
    /// Get service
    /// </summary>
    public DeveloperService? GetService(string serviceId)
    {
        _services.TryGetValue(serviceId, out var service);
        return service;
    }

    /// <summary>
    /// List all templates
    /// </summary>
    public List<DeveloperTemplate> ListTemplates()
    {
        return _templates.Values.ToList();
    }

    /// <summary>
    /// List all components
    /// </summary>
    public List<PlatformComponent> ListComponents()
    {
        return _components.Values.ToList();
    }

    /// <summary>
    /// List all services
    /// </summary>
    public List<DeveloperService> ListServices()
    {
        return _services.Values.ToList();
    }

    /// <summary>
    /// Get IDP stats
    /// </summary>
    public Dictionary<string, object> GetStats()
    {
        return new()
        {
            ["templatesCount"] = _templates.Count,
            ["componentsCount"] = _components.Count,
            ["servicesCount"] = _services.Count,
            ["runningServices"] = _services.Values.Count(s => s.Status == "Running")
        };
    }
}

/// <summary>
/// Developer service created via IDP
/// </summary>
public class DeveloperService
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // Provisioning, Running, Archived

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public string Team { get; set; } = string.Empty;

    [JsonPropertyName("documentation")]
    public string Documentation { get; set; } = string.Empty;

    [JsonPropertyName("deploymentInfo")]
    public DeploymentInfo? DeploymentInfo { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Service deployment information
/// </summary>
public class DeploymentInfo
{
    [JsonPropertyName("cluster")]
    public string Cluster { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("imageRepository")]
    public string ImageRepository { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; } = 8080;

    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;

    [JsonPropertyName("healthCheck")]
    public HealthCheck? HealthCheck { get; set; }
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheck
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "/health";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 8080;

    [JsonPropertyName("intervalSeconds")]
    public int IntervalSeconds { get; set; } = 10;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 5;

    [JsonPropertyName("failureThreshold")]
    public int FailureThreshold { get; set; } = 3;
}

/// <summary>
/// Platform documentation
/// </summary>
public class PlatformDocumentation
{
    [JsonPropertyName("gettingStarted")]
    public string GettingStarted { get; set; } = string.Empty;

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = string.Empty;

    [JsonPropertyName("bestPractices")]
    public List<string> BestPractices { get; set; } = new();

    [JsonPropertyName("troubleshooting")]
    public List<TroubleshootingGuide> Troubleshooting { get; set; } = new();

    [JsonPropertyName("apiReference")]
    public string ApiReference { get; set; } = string.Empty;
}

/// <summary>
/// Troubleshooting guide
/// </summary>
public class TroubleshootingGuide
{
    [JsonPropertyName("issue")]
    public string Issue { get; set; } = string.Empty;

    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;

    [JsonPropertyName("relatedDocs")]
    public List<string> RelatedDocs { get; set; } = new();
}

/// <summary>
/// Developer experience metrics
/// </summary>
public class DeveloperExperienceMetrics
{
    [JsonPropertyName("timeToFirstDeployment")]
    public TimeSpan TimeToFirstDeployment { get; set; } = TimeSpan.Zero;

    [JsonPropertyName("averageDeploymentTime")]
    public TimeSpan AverageDeploymentTime { get; set; } = TimeSpan.Zero;

    [JsonPropertyName("rollbackFrequency")]
    public double RollbackFrequency { get; set; } = 0.0; // Rollbacks per 100 deployments

    [JsonPropertyName("platformAdoption")]
    public double PlatformAdoption { get; set; } = 0.0; // Percentage of teams using platform

    [JsonPropertyName("supportTickets")]
    public int SupportTickets { get; set; }

    [JsonPropertyName("documentationQuality")]
    public double DocumentationQuality { get; set; } = 0.0; // 0-100
}

/// <summary>
/// Extension methods
/// </summary>
public static class PlatformEngineeringExtensions
{
    public static IServiceCollection AddPlatformEngineering(this IServiceCollection services)
    {
        services.AddSingleton<InternalDeveloperPlatform>();
        return services;
    }
}
