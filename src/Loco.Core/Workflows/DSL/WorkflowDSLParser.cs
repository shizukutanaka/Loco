// Phase 9: Workflow Templates & DSL Parser
// Domain-Specific Language for workflow definition and YAML/JSON template support
// Declarative workflow definition with template reusability

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows.DSL;

/// <summary>
/// Workflow template
/// </summary>
public class WorkflowTemplate
{
    public string TemplateId { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TemplateVersion { get; set; } = "1.0.0";
    public string TemplateFormat { get; set; } = string.Empty; // yaml, json, dsl
    public string TemplateContent { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public bool IsPublic { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Template variable
/// </summary>
public class TemplateVariable
{
    public string VariableName { get; set; } = string.Empty;
    public string VariableType { get; set; } = string.Empty; // string, number, boolean, object, array
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
    public object? ValidationRules { get; set; }
}

/// <summary>
/// Template instantiation
/// </summary>
public class TemplateInstantiation
{
    public string InstantiationId { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public Dictionary<string, object> VariableValues { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// DSL parse result
/// </summary>
public class DSLParseResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, object> WorkflowDefinition { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<TemplateVariable> RequiredVariables { get; set; } = new();
    public long ParseTimeMs { get; set; }
}

/// <summary>
/// Template library entry
/// </summary>
public class TemplateLibraryEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public double Rating { get; set; }
    public int DownloadCount { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Workflow DSL interface
/// </summary>
public interface IWorkflowDSLParser
{
    // Template management
    Task<WorkflowTemplate> CreateTemplateAsync(
        string tenantId,
        string templateName,
        string format,
        string content,
        CancellationToken ct = default);

    Task<WorkflowTemplate?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    Task<List<WorkflowTemplate>> GetTemplatesAsync(
        string tenantId,
        string? category = null,
        CancellationToken ct = default);

    Task<bool> UpdateTemplateAsync(
        string templateId,
        string content,
        CancellationToken ct = default);

    Task<bool> DeleteTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    // Parsing
    Task<DSLParseResult> ParseDSLAsync(
        string dslContent,
        CancellationToken ct = default);

    Task<DSLParseResult> ParseYAMLAsync(
        string yamlContent,
        CancellationToken ct = default);

    Task<DSLParseResult> ParseJSONAsync(
        string jsonContent,
        CancellationToken ct = default);

    // Instantiation
    Task<TemplateInstantiation> InstantiateTemplateAsync(
        string templateId,
        Dictionary<string, object> variables,
        CancellationToken ct = default);

    Task<List<TemplateVariable>> GetTemplateVariablesAsync(
        string templateId,
        CancellationToken ct = default);

    Task<bool> ValidateVariablesAsync(
        string templateId,
        Dictionary<string, object> variables,
        CancellationToken ct = default);

    // Library
    Task<List<TemplateLibraryEntry>> SearchTemplateLibraryAsync(
        string searchQuery,
        string? category = null,
        CancellationToken ct = default);

    Task<TemplateLibraryEntry?> PublishTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    // Analytics
    Task<Dictionary<string, object>> GetDSLAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Workflow DSL parser implementation
/// </summary>
public class WorkflowDSLParser : IWorkflowDSLParser
{
    private readonly ILogger<WorkflowDSLParser> _logger;
    private readonly Dictionary<string, WorkflowTemplate> _templates;
    private readonly Dictionary<string, List<TemplateInstantiation>> _instantiations;
    private readonly Dictionary<string, TemplateLibraryEntry> _libraryEntries;

    public WorkflowDSLParser(ILogger<WorkflowDSLParser> logger)
    {
        _logger = logger;
        _templates = new Dictionary<string, WorkflowTemplate>();
        _instantiations = new Dictionary<string, List<TemplateInstantiation>>();
        _libraryEntries = new Dictionary<string, TemplateLibraryEntry>();
    }

    // Template management
    public async Task<WorkflowTemplate> CreateTemplateAsync(
        string tenantId,
        string templateName,
        string format,
        string content,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var template = new WorkflowTemplate
        {
            TenantId = tenantId,
            TemplateName = templateName,
            TemplateFormat = format,
            TemplateContent = content,
        };

        _templates[template.TemplateId] = template;

        _logger.LogInformation(
            "Workflow template created: TemplateId={TemplateId}, Name={TemplateName}, Format={Format}",
            template.TemplateId, templateName, format);

        return template;
    }

    public async Task<WorkflowTemplate?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _templates.TryGetValue(templateId, out var template);
        return template;
    }

    public async Task<List<WorkflowTemplate>> GetTemplatesAsync(
        string tenantId,
        string? category = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _templates.Values
            .Where(t => t.TenantId == tenantId || t.IsPublic)
            .Where(t => category == null || t.Categories.Contains(category))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return results;
    }

    public async Task<bool> UpdateTemplateAsync(
        string templateId,
        string content,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_templates.TryGetValue(templateId, out var template))
        {
            return false;
        }

        template.TemplateContent = content;
        template.ModifiedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Workflow template updated: TemplateId={TemplateId}",
            templateId);

        return true;
    }

    public async Task<bool> DeleteTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_templates.Remove(templateId))
        {
            _logger.LogInformation(
                "Workflow template deleted: TemplateId={TemplateId}",
                templateId);
            return true;
        }

        return false;
    }

    // Parsing
    public async Task<DSLParseResult> ParseDSLAsync(
        string dslContent,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate parsing

        var startTime = DateTime.UtcNow;
        var result = new DSLParseResult { IsValid = true };

        try
        {
            // Simplified DSL parsing logic
            var lines = dslContent.Split('\n');
            var definition = new Dictionary<string, object>();
            var variables = new List<TemplateVariable>();

            foreach (var line in lines.Where(l => !l.TrimStart().StartsWith("#")))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("define"))
                {
                    // Parse variable definition
                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                    {
                        variables.Add(new TemplateVariable
                        {
                            VariableName = parts[1],
                            VariableType = parts[2],
                            IsRequired = !line.Contains("optional"),
                        });
                    }
                }
                else if (line.StartsWith("step"))
                {
                    // Parse step definition
                    definition[$"step_{definition.Count}"] = line;
                }
            }

            result.WorkflowDefinition = definition;
            result.RequiredVariables = variables.Where(v => v.IsRequired).ToList();
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        result.ParseTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation(
            "DSL parsed: Valid={Valid}, Steps={StepCount}, Variables={VariableCount}, Time={TimeMs}ms",
            result.IsValid, result.WorkflowDefinition.Count, result.RequiredVariables.Count, result.ParseTimeMs);

        return result;
    }

    public async Task<DSLParseResult> ParseYAMLAsync(
        string yamlContent,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate parsing

        var result = new DSLParseResult { IsValid = true };

        try
        {
            // Simplified YAML parsing (in production, use YamlDotNet)
            var definition = new Dictionary<string, object>
            {
                ["workflow"] = "parsed_from_yaml",
                ["format"] = "yaml",
                ["content_lines"] = yamlContent.Split('\n').Length,
            };

            result.WorkflowDefinition = definition;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"YAML parse error: {ex.Message}");
        }

        return result;
    }

    public async Task<DSLParseResult> ParseJSONAsync(
        string jsonContent,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate parsing

        var result = new DSLParseResult { IsValid = true };

        try
        {
            // Simplified JSON parsing (in production, use System.Text.Json)
            var definition = new Dictionary<string, object>
            {
                ["workflow"] = "parsed_from_json",
                ["format"] = "json",
                ["content_length"] = jsonContent.Length,
            };

            result.WorkflowDefinition = definition;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"JSON parse error: {ex.Message}");
        }

        return result;
    }

    // Instantiation
    public async Task<TemplateInstantiation> InstantiateTemplateAsync(
        string templateId,
        Dictionary<string, object> variables,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var template = await GetTemplateAsync(templateId, ct);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template not found: {templateId}");
        }

        var instantiation = new TemplateInstantiation
        {
            TemplateId = templateId,
            VariableValues = new Dictionary<string, object>(variables),
        };

        if (!_instantiations.ContainsKey(templateId))
        {
            _instantiations[templateId] = new List<TemplateInstantiation>();
        }

        _instantiations[templateId].Add(instantiation);
        template.UsageCount++;

        _logger.LogInformation(
            "Template instantiated: TemplateId={TemplateId}, Variables={VariableCount}",
            templateId, variables.Count);

        return instantiation;
    }

    public async Task<List<TemplateVariable>> GetTemplateVariablesAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var template = await GetTemplateAsync(templateId, ct);
        if (template == null)
        {
            return new List<TemplateVariable>();
        }

        // Parse variables from template content
        var variables = new List<TemplateVariable>();

        // Simplified variable extraction
        if (template.TemplateFormat == "dsl")
        {
            var lines = template.TemplateContent.Split('\n');
            foreach (var line in lines.Where(l => l.StartsWith("define")))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 3)
                {
                    variables.Add(new TemplateVariable
                    {
                        VariableName = parts[1],
                        VariableType = parts[2],
                        IsRequired = !line.Contains("optional"),
                    });
                }
            }
        }

        return variables;
    }

    public async Task<bool> ValidateVariablesAsync(
        string templateId,
        Dictionary<string, object> variables,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var templateVariables = await GetTemplateVariablesAsync(templateId, ct);
        var requiredVariables = templateVariables.Where(v => v.IsRequired).ToList();

        // Check that all required variables are provided
        var providedKeys = variables.Keys.ToList();
        foreach (var required in requiredVariables)
        {
            if (!providedKeys.Contains(required.VariableName))
            {
                return false;
            }
        }

        return true;
    }

    // Library
    public async Task<List<TemplateLibraryEntry>> SearchTemplateLibraryAsync(
        string searchQuery,
        string? category = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _libraryEntries.Values
            .Where(e => e.TemplateName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
            .Where(e => category == null || e.Categories.Contains(category))
            .OrderByDescending(e => e.DownloadCount)
            .ToList();

        return results;
    }

    public async Task<TemplateLibraryEntry?> PublishTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var template = await GetTemplateAsync(templateId, ct);
        if (template == null)
        {
            return null;
        }

        var entry = new TemplateLibraryEntry
        {
            TemplateId = templateId,
            TemplateName = template.TemplateName,
            Categories = template.Categories,
            Rating = 4.5,
            DownloadCount = 0,
        };

        _libraryEntries[entry.EntryId] = entry;
        template.IsPublic = true;

        _logger.LogInformation(
            "Template published to library: TemplateId={TemplateId}, Name={TemplateName}",
            templateId, template.TemplateName);

        return entry;
    }

    // Analytics
    public async Task<Dictionary<string, object>> GetDSLAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var templates = _templates.Values
            .Where(t => t.TenantId == tenantId)
            .ToList();

        var totalInstantiations = _instantiations.Values.Sum(i => i.Count);

        return new Dictionary<string, object>
        {
            ["total_templates"] = templates.Count,
            ["public_templates"] = templates.Count(t => t.IsPublic),
            ["total_instantiations"] = totalInstantiations,
            ["average_usage_per_template"] = templates.Count > 0
                ? templates.Average(t => t.UsageCount)
                : 0,
            ["library_entries"] = _libraryEntries.Count,
            ["format_distribution"] = GetFormatDistribution(templates),
        };
    }

    // Helpers
    private Dictionary<string, int> GetFormatDistribution(List<WorkflowTemplate> templates)
    {
        return templates
            .GroupBy(t => t.TemplateFormat)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
