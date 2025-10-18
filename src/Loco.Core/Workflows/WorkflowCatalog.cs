using System.Text;
using System.Text.Json;

namespace Loco.Core.Workflows;

/// <summary>
/// Metadata about a workflow in the catalog.
/// </summary>
public class WorkflowMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string FilePath { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string? Category { get; set; }
    public int StepCount { get; set; }
    public bool HasSchedule { get; set; }
    public bool HasDependencies { get; set; }
    public bool HasHooks { get; set; }
    public bool HasEnvironments { get; set; }
    public DateTime? LastModified { get; set; }
}

/// <summary>
/// Manages a catalog of available workflows.
/// </summary>
public class WorkflowCatalog
{
    private readonly List<WorkflowMetadata> _workflows = new();

    /// <summary>
    /// Scans a directory for workflow files and builds the catalog.
    /// </summary>
    public async Task<int> ScanDirectoryAsync(string directory, bool recursive = true)
    {
        if (!Directory.Exists(directory))
            return 0;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var jsonFiles = Directory.GetFiles(directory, "*.json", searchOption);

        int count = 0;

        foreach (var file in jsonFiles)
        {
            try
            {
                var metadata = await ExtractMetadataAsync(file);
                if (metadata != null)
                {
                    _workflows.Add(metadata);
                    count++;
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return count;
    }

    /// <summary>
    /// Extracts metadata from a workflow file.
    /// </summary>
    private async Task<WorkflowMetadata?> ExtractMetadataAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (workflow == null || string.IsNullOrEmpty(workflow.Id))
                return null;

            var fileInfo = new FileInfo(filePath);

            var metadata = new WorkflowMetadata
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description,
                FilePath = filePath,
                StepCount = workflow.Steps?.Count ?? 0,
                HasSchedule = workflow.Schedule != null,
                HasDependencies = workflow.Steps?.Any(s => s.DependsOn != null || s.Dependencies != null) ?? false,
                HasHooks = workflow.Hooks != null,
                HasEnvironments = workflow.Environments != null && workflow.Environments.Count > 0,
                LastModified = fileInfo.LastWriteTime
            };

            // Extract tags from description or filename
            if (!string.IsNullOrEmpty(workflow.Description))
            {
                var desc = workflow.Description.ToLowerInvariant();
                if (desc.Contains("backup")) metadata.Tags.Add("backup");
                if (desc.Contains("deploy")) metadata.Tags.Add("deployment");
                if (desc.Contains("test")) metadata.Tags.Add("testing");
                if (desc.Contains("health")) metadata.Tags.Add("monitoring");
                if (desc.Contains("schedule")) metadata.Tags.Add("scheduled");
            }

            // Determine category
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (fileName.Contains("deploy")) metadata.Category = "Deployment";
            else if (fileName.Contains("backup")) metadata.Category = "Backup";
            else if (fileName.Contains("health") || fileName.Contains("check")) metadata.Category = "Monitoring";
            else if (fileName.Contains("test")) metadata.Category = "Testing";
            else metadata.Category = "General";

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Searches for workflows matching the criteria.
    /// </summary>
    public List<WorkflowMetadata> Search(string? query = null, string? category = null, string? tag = null)
    {
        var results = _workflows.AsEnumerable();

        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLowerInvariant();
            results = results.Where(w =>
                w.Name.ToLowerInvariant().Contains(q) ||
                w.Id.ToLowerInvariant().Contains(q) ||
                (w.Description?.ToLowerInvariant().Contains(q) ?? false));
        }

        if (!string.IsNullOrEmpty(category))
        {
            results = results.Where(w =>
                w.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (!string.IsNullOrEmpty(tag))
        {
            results = results.Where(w =>
                w.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));
        }

        return results.OrderBy(w => w.Category).ThenBy(w => w.Name).ToList();
    }

    /// <summary>
    /// Gets all unique categories.
    /// </summary>
    public List<string> GetCategories()
    {
        return _workflows
            .Select(w => w.Category ?? "Uncategorized")
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// Gets all unique tags.
    /// </summary>
    public List<string> GetTags()
    {
        return _workflows
            .SelectMany(w => w.Tags)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    /// <summary>
    /// Generates a formatted catalog display.
    /// </summary>
    public string GenerateCatalogDisplay(List<WorkflowMetadata>? workflows = null)
    {
        var sb = new StringBuilder();
        var items = workflows ?? _workflows;

        if (items.Count == 0)
        {
            return "No workflows found.";
        }

        sb.AppendLine($"Found {items.Count} workflow(s):");
        sb.AppendLine();

        var groupedByCategory = items.GroupBy(w => w.Category ?? "Uncategorized");

        foreach (var group in groupedByCategory)
        {
            sb.AppendLine($"━━ {group.Key} ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();

            foreach (var workflow in group)
            {
                sb.AppendLine($"  📋 {workflow.Name}");
                sb.AppendLine($"     ID: {workflow.Id}");

                if (!string.IsNullOrEmpty(workflow.Description))
                {
                    var desc = workflow.Description.Length > 80
                        ? workflow.Description.Substring(0, 77) + "..."
                        : workflow.Description;
                    sb.AppendLine($"     {desc}");
                }

                var features = new List<string>();
                features.Add($"{workflow.StepCount} steps");
                if (workflow.HasSchedule) features.Add("scheduled");
                if (workflow.HasDependencies) features.Add("DAG");
                if (workflow.HasHooks) features.Add("hooks");
                if (workflow.HasEnvironments) features.Add("multi-env");

                sb.AppendLine($"     Features: {string.Join(", ", features)}");

                if (workflow.Tags.Count > 0)
                {
                    sb.AppendLine($"     Tags: {string.Join(", ", workflow.Tags)}");
                }

                sb.AppendLine($"     Path: {workflow.FilePath}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a compact table display.
    /// </summary>
    public string GenerateCompactTable(List<WorkflowMetadata>? workflows = null)
    {
        var sb = new StringBuilder();
        var items = workflows ?? _workflows;

        if (items.Count == 0)
        {
            return "No workflows found.";
        }

        sb.AppendLine($"{"ID",-25} {"Name",-35} {"Steps",6} {"Category",-15} {"Features"}");
        sb.AppendLine(new string('-', 120));

        foreach (var w in items)
        {
            var features = new List<string>();
            if (w.HasSchedule) features.Add("S");
            if (w.HasDependencies) features.Add("D");
            if (w.HasHooks) features.Add("H");
            if (w.HasEnvironments) features.Add("E");

            var id = w.Id.Length > 24 ? w.Id.Substring(0, 21) + "..." : w.Id;
            var name = w.Name.Length > 34 ? w.Name.Substring(0, 31) + "..." : w.Name;
            var category = (w.Category ?? "").Length > 14 ? (w.Category ?? "").Substring(0, 11) + "..." : (w.Category ?? "");

            sb.AppendLine($"{id,-25} {name,-35} {w.StepCount,6} {category,-15} {string.Join(",", features)}");
        }

        sb.AppendLine();
        sb.AppendLine("Features: S=Scheduled, D=DAG, H=Hooks, E=Multi-Environment");

        return sb.ToString();
    }
}
