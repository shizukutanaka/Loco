using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Templates;

/// <summary>
/// Template marketplace manager for sharing and discovering workflow templates
/// Based on 2025 best practices: GitHub Actions marketplace, Zapier templates, n8n workflows
/// テンプレートマーケットプレイス - ワークフローテンプレートの共有と発見
/// </summary>
public class TemplateManager
{
    private readonly string _templatesDirectory;
    private readonly string _cacheDirectory;
    private readonly ILogger<TemplateManager>? _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string DefaultRegistryUrl = "https://raw.githubusercontent.com/loco-automation/templates/main/registry.json";
    private const string LocalRegistryFile = "local-registry.json";

    public TemplateManager(string? templatesDirectory = null, ILogger<TemplateManager>? logger = null)
    {
        _templatesDirectory = templatesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Loco",
            "Templates");

        _cacheDirectory = Path.Combine(_templatesDirectory, ".cache");
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        EnsureDirectoriesExist();
    }

    /// <summary>
    /// Search for templates by keyword
    /// キーワードでテンプレートを検索
    /// </summary>
    public async Task<WorkflowTemplate[]> SearchTemplatesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var registry = await LoadRegistryAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(query))
        {
            return registry.Templates.ToArray();
        }

        var queryLower = query.ToLowerInvariant();

        return registry.Templates
            .Where(t =>
                t.Name.ToLowerInvariant().Contains(queryLower) ||
                t.Description.ToLowerInvariant().Contains(queryLower) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(queryLower)))
            .OrderByDescending(t => CalculateRelevanceScore(t, queryLower))
            .ToArray();
    }

    /// <summary>
    /// Install a template by ID
    /// IDでテンプレートをインストール
    /// </summary>
    public async Task<WorkflowTemplate> InstallTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var registry = await LoadRegistryAsync(cancellationToken);
        var template = registry.Templates.FirstOrDefault(t => t.Id == templateId);

        if (template == null)
        {
            throw new TemplateNotFoundException($"Template '{templateId}' not found in registry");
        }

        // Download template content if it has a URL
        if (!string.IsNullOrEmpty(template.SourceUrl))
        {
            _logger?.LogInformation("Downloading template {TemplateId} from {Url}", templateId, template.SourceUrl);

            try
            {
                var content = await _httpClient.GetStringAsync(template.SourceUrl, cancellationToken);
                template.WorkflowDefinition = JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to download template {TemplateId}", templateId);
                throw new TemplateDownloadException($"Failed to download template '{templateId}'", ex);
            }
        }

        // Save to local templates directory
        var templatePath = Path.Combine(_templatesDirectory, $"{templateId}.json");
        var templateJson = JsonSerializer.Serialize(template, _jsonOptions);
        await File.WriteAllTextAsync(templatePath, templateJson, cancellationToken);

        // Update installation metadata
        template.InstalledAt = DateTime.UtcNow;
        template.InstallCount++;

        await UpdateLocalRegistryAsync(template, cancellationToken);

        _logger?.LogInformation("Template {TemplateId} installed to {Path}", templateId, templatePath);

        return template;
    }

    /// <summary>
    /// List installed templates
    /// インストール済みテンプレートを一覧表示
    /// </summary>
    public async Task<WorkflowTemplate[]> ListInstalledTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new List<WorkflowTemplate>();

        if (!Directory.Exists(_templatesDirectory))
        {
            return Array.Empty<WorkflowTemplate>();
        }

        var files = Directory.GetFiles(_templatesDirectory, "*.json")
            .Where(f => !f.EndsWith(LocalRegistryFile));

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var template = JsonSerializer.Deserialize<WorkflowTemplate>(json, _jsonOptions);

                if (template != null)
                {
                    templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load template from {File}", file);
            }
        }

        return templates.OrderBy(t => t.Name).ToArray();
    }

    /// <summary>
    /// Publish a new template to the local registry
    /// 新しいテンプレートをローカルレジストリに公開
    /// </summary>
    public async Task<WorkflowTemplate> PublishTemplateAsync(
        WorkflowDefinition workflow,
        TemplateMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var template = new WorkflowTemplate
        {
            Id = metadata.Id ?? Guid.NewGuid().ToString("N"),
            Name = metadata.Name,
            Description = metadata.Description,
            Author = metadata.Author ?? Environment.UserName,
            Version = metadata.Version ?? "1.0.0",
            Tags = metadata.Tags ?? Array.Empty<string>(),
            Category = metadata.Category ?? "General",
            WorkflowDefinition = JsonSerializer.SerializeToElement(workflow, _jsonOptions),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save template file
        var templatePath = Path.Combine(_templatesDirectory, $"{template.Id}.json");
        var templateJson = JsonSerializer.Serialize(template, _jsonOptions);
        await File.WriteAllTextAsync(templatePath, templateJson, cancellationToken);

        // Update local registry
        await UpdateLocalRegistryAsync(template, cancellationToken);

        _logger?.LogInformation("Template {TemplateId} published successfully", template.Id);

        return template;
    }

    /// <summary>
    /// Delete an installed template
    /// インストール済みテンプレートを削除
    /// </summary>
    public async Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(_templatesDirectory, $"{templateId}.json");

        if (!File.Exists(templatePath))
        {
            return false;
        }

        File.Delete(templatePath);

        // Remove from local registry
        await RemoveFromLocalRegistryAsync(templateId, cancellationToken);

        _logger?.LogInformation("Template {TemplateId} deleted", templateId);

        return true;
    }

    /// <summary>
    /// Get template by ID
    /// IDでテンプレートを取得
    /// </summary>
    public async Task<WorkflowTemplate?> GetTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(_templatesDirectory, $"{templateId}.json");

        if (!File.Exists(templatePath))
        {
            // Try to find in registry
            var registry = await LoadRegistryAsync(cancellationToken);
            return registry.Templates.FirstOrDefault(t => t.Id == templateId);
        }

        try
        {
            var json = await File.ReadAllTextAsync(templatePath, cancellationToken);
            return JsonSerializer.Deserialize<WorkflowTemplate>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load template {TemplateId}", templateId);
            return null;
        }
    }

    /// <summary>
    /// Refresh template registry from remote source
    /// リモートソースからテンプレートレジストリを更新
    /// </summary>
    public async Task RefreshRegistryAsync(
        string? registryUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = registryUrl ?? DefaultRegistryUrl;

        try
        {
            _logger?.LogInformation("Refreshing template registry from {Url}", url);

            var json = await _httpClient.GetStringAsync(url, cancellationToken);
            var registry = JsonSerializer.Deserialize<TemplateRegistry>(json, _jsonOptions);

            if (registry != null)
            {
                var cacheFile = Path.Combine(_cacheDirectory, "remote-registry.json");
                await File.WriteAllTextAsync(cacheFile, json, cancellationToken);

                _logger?.LogInformation("Template registry refreshed. Found {Count} templates", registry.Templates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to refresh template registry from {Url}", url);
            // Don't throw - use cached version if available
        }
    }

    private async Task<TemplateRegistry> LoadRegistryAsync(CancellationToken cancellationToken)
    {
        // Try to load from cache first
        var cacheFile = Path.Combine(_cacheDirectory, "remote-registry.json");

        if (File.Exists(cacheFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(cacheFile, cancellationToken);
                var registry = JsonSerializer.Deserialize<TemplateRegistry>(json, _jsonOptions);

                if (registry != null)
                {
                    // Merge with local registry
                    var localRegistry = await LoadLocalRegistryAsync(cancellationToken);
                    registry.Templates.AddRange(localRegistry.Templates);

                    return registry;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load cached registry");
            }
        }

        // Fallback to local registry only
        return await LoadLocalRegistryAsync(cancellationToken);
    }

    private async Task<TemplateRegistry> LoadLocalRegistryAsync(CancellationToken cancellationToken)
    {
        var localRegistryFile = Path.Combine(_templatesDirectory, LocalRegistryFile);

        if (File.Exists(localRegistryFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localRegistryFile, cancellationToken);
                var registry = JsonSerializer.Deserialize<TemplateRegistry>(json, _jsonOptions);

                if (registry != null)
                {
                    return registry;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load local registry");
            }
        }

        // Return empty registry
        return new TemplateRegistry
        {
            Version = "1.0",
            Templates = new List<WorkflowTemplate>()
        };
    }

    private async Task UpdateLocalRegistryAsync(WorkflowTemplate template, CancellationToken cancellationToken)
    {
        var registry = await LoadLocalRegistryAsync(cancellationToken);

        // Update or add template
        var existing = registry.Templates.FindIndex(t => t.Id == template.Id);
        if (existing >= 0)
        {
            registry.Templates[existing] = template;
        }
        else
        {
            registry.Templates.Add(template);
        }

        var localRegistryFile = Path.Combine(_templatesDirectory, LocalRegistryFile);
        var json = JsonSerializer.Serialize(registry, _jsonOptions);
        await File.WriteAllTextAsync(localRegistryFile, json, cancellationToken);
    }

    private async Task RemoveFromLocalRegistryAsync(string templateId, CancellationToken cancellationToken)
    {
        var registry = await LoadLocalRegistryAsync(cancellationToken);
        registry.Templates.RemoveAll(t => t.Id == templateId);

        var localRegistryFile = Path.Combine(_templatesDirectory, LocalRegistryFile);
        var json = JsonSerializer.Serialize(registry, _jsonOptions);
        await File.WriteAllTextAsync(localRegistryFile, json, cancellationToken);
    }

    private double CalculateRelevanceScore(WorkflowTemplate template, string query)
    {
        double score = 0;

        // Name match is most important
        if (template.Name.ToLowerInvariant().Contains(query))
        {
            score += 10;

            // Exact match
            if (template.Name.ToLowerInvariant() == query)
            {
                score += 20;
            }
        }

        // Description match
        if (template.Description.ToLowerInvariant().Contains(query))
        {
            score += 5;
        }

        // Tag match
        foreach (var tag in template.Tags)
        {
            if (tag.ToLowerInvariant().Contains(query))
            {
                score += 7;

                // Exact tag match
                if (tag.ToLowerInvariant() == query)
                {
                    score += 10;
                }
            }
        }

        // Boost popular templates
        score += Math.Log10(template.InstallCount + 1);

        return score;
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_templatesDirectory);
        Directory.CreateDirectory(_cacheDirectory);
    }
}

/// <summary>
/// Template registry containing available templates
/// 利用可能なテンプレートを含むレジストリ
/// </summary>
public class TemplateRegistry
{
    public string Version { get; set; } = "1.0";
    public List<WorkflowTemplate> Templates { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Workflow template with metadata
/// メタデータ付きワークフローテンプレート
/// </summary>
public class WorkflowTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = "General";
    public string? SourceUrl { get; set; }
    public int InstallCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? InstalledAt { get; set; }
    public JsonElement? WorkflowDefinition { get; set; }
}

/// <summary>
/// Workflow definition (simplified for template system)
/// ワークフロー定義（テンプレートシステム用に簡略化）
/// </summary>
public class WorkflowDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<WorkflowStep> Steps { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
}

public class WorkflowStep
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Config { get; set; } = new();
}

/// <summary>
/// Template metadata for publishing
/// 公開用テンプレートメタデータ
/// </summary>
public class TemplateMetadata
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string[]? Tags { get; set; }
    public string? Category { get; set; }
}

public class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(string message) : base(message) { }
}

public class TemplateDownloadException : Exception
{
    public TemplateDownloadException(string message, Exception innerException)
        : base(message, innerException) { }
}
