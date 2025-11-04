#nullable enable

using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.GitOps;

public class GitRepository
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "main";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("interval")]
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class ArgocdConfig
{
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "argocd";

    [JsonPropertyName("repositories")]
    public List<GitRepository> Repositories { get; set; } = new();

    [JsonPropertyName("projects")]
    public List<string> Projects { get; set; } = new();
}

public class FluxcdConfig
{
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "flux-system";

    [JsonPropertyName("sources")]
    public List<GitRepository> Sources { get; set; } = new();

    [JsonPropertyName("kustomizations")]
    public List<string> Kustomizations { get; set; } = new();
}

public class GitOpsEngine
{
    private readonly ArgocdConfig _argocd = new();
    private readonly FluxcdConfig _fluxcd = new();
    private readonly ILogger<GitOpsEngine> _logger;

    public GitOpsEngine(ILogger<GitOpsEngine> logger) => _logger = logger;

    public async Task RegisterRepositoryAsync(GitRepository repo)
    {
        _argocd.Repositories.Add(repo);
        _logger.LogInformation("Registered repository: {Url} branch={Branch}", repo.Url, repo.Branch);
    }

    public Dictionary<string, object> GetStats() => new()
    {
        ["repositories"] = _argocd.Repositories.Count,
        ["syncInterval"] = "5m"
    };
}

public static class GitOpsExtensions
{
    public static IServiceCollection AddGitOps(this IServiceCollection services)
    {
        services.AddSingleton<GitOpsEngine>();
        return services;
    }
}
