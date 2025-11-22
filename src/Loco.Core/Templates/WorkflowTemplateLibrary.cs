// Phase 7: Advanced Workflow Templates Library
// Pre-built templates for common business scenarios with customization support
// Accelerates workflow creation and ensures best practices

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Templates;

/// <summary>
/// Template category
/// </summary>
public enum TemplateCategory
{
    OrderProcessing = 0,
    DataPipeline = 1,
    EmailCampaign = 2,
    ApprovalWorkflow = 3,
    NotificationSystem = 4,
    DataValidation = 5,
    ReportGeneration = 6,
    Integration = 7,
    Monitoring = 8,
    Custom = 9,
}

/// <summary>
/// Template difficulty level
/// </summary>
public enum TemplateDifficulty
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2,
    Expert = 3,
}

/// <summary>
/// Template variable for customization
/// </summary>
public class TemplateVariable
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "string"; // string, integer, boolean, object, array
    public object? DefaultValue { get; set; }
    public bool IsRequired { get; set; } = true;
    public List<object>? AllowedValues { get; set; }
    public string? ValidationPattern { get; set; }
}

/// <summary>
/// Workflow template metadata
/// </summary>
public class WorkflowTemplate
{
    public string TemplateId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TemplateCategory Category { get; set; }
    public TemplateDifficulty Difficulty { get; set; }
    public string IconUrl { get; set; } = string.Empty;

    // Content
    public string DefinitionJson { get; set; } = string.Empty; // Serialized workflow definition
    public List<TemplateVariable> Variables { get; set; } = new();
    public Dictionary<string, object>? SampleInput { get; set; }

    // Metadata
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? DocumentationUrl { get; set; }
    public string? VideoUrl { get; set; }

    // Ratings
    public double AverageRating { get; set; } // 1.0-5.0
    public int RatingCount { get; set; }
    public int UsageCount { get; set; }

    // Dates
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }

    // Performance
    public long AverageExecutionTimeMs { get; set; }
    public double SuccessRatePercentage { get; set; }
}

/// <summary>
/// Template instantiation result
/// </summary>
public class TemplateInstantiation
{
    public string InstantiationId { get; set; } = Guid.NewGuid().ToString();
    public string TemplateId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<string, object>? CustomVariables { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Template usage analytics
/// </summary>
public class TemplateAnalytics
{
    public string TemplateId { get; set; } = string.Empty;
    public int TotalInstantiations { get; set; }
    public int ActiveWorkflows { get; set; }
    public long TotalExecutions { get; set; }
    public double SuccessRate { get; set; }
    public long AverageExecutionTimeMs { get; set; }
    public Dictionary<string, int>? UsageByTenant { get; set; }
    public Dictionary<string, int>? UsageByDay { get; set; }
    public List<string> MostCommonCustomizations { get; set; } = new();
}

/// <summary>
/// Workflow template library interface
/// </summary>
public interface IWorkflowTemplateLibrary
{
    // Template discovery
    Task<WorkflowTemplate?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    Task<List<WorkflowTemplate>> ListTemplatesAsync(
        TemplateCategory? category = null,
        TemplateDifficulty? difficulty = null,
        int limit = 50,
        CancellationToken ct = default);

    Task<List<WorkflowTemplate>> SearchTemplatesAsync(
        string query,
        CancellationToken ct = default);

    Task<List<WorkflowTemplate>> GetFeaturedTemplatesAsync(
        int limit = 10,
        CancellationToken ct = default);

    Task<List<WorkflowTemplate>> GetTrendingTemplatesAsync(
        int limit = 10,
        CancellationToken ct = default);

    // Template instantiation
    Task<TemplateInstantiation> InstantiateTemplateAsync(
        string templateId,
        string tenantId,
        Dictionary<string, object>? variables = null,
        CancellationToken ct = default);

    Task<List<TemplateInstantiation>> GetInstantiationsAsync(
        string templateId,
        string? tenantId = null,
        CancellationToken ct = default);

    // Template management
    Task<WorkflowTemplate> CreateTemplateAsync(
        WorkflowTemplate template,
        CancellationToken ct = default);

    Task<WorkflowTemplate> UpdateTemplateAsync(
        string templateId,
        WorkflowTemplate template,
        CancellationToken ct = default);

    Task<bool> PublishTemplateAsync(
        string templateId,
        CancellationToken ct = default);

    Task<bool> DeprecateTemplateAsync(
        string templateId,
        string? replacementTemplateId = null,
        CancellationToken ct = default);

    // Analytics
    Task<TemplateAnalytics> GetAnalyticsAsync(
        string templateId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default);

    Task<Dictionary<string, int>> GetLibraryStatisticsAsync(
        CancellationToken ct = default);
}

/// <summary>
/// Workflow template library implementation
/// </summary>
public class WorkflowTemplateLibrary : IWorkflowTemplateLibrary
{
    private readonly ILogger<WorkflowTemplateLibrary> _logger;
    private readonly Dictionary<string, WorkflowTemplate> _templates;
    private readonly Dictionary<string, List<TemplateInstantiation>> _instantiations;
    private readonly Dictionary<string, TemplateAnalytics> _analytics;

    public WorkflowTemplateLibrary(ILogger<WorkflowTemplateLibrary> logger)
    {
        _logger = logger;
        _templates = new Dictionary<string, WorkflowTemplate>();
        _instantiations = new Dictionary<string, List<TemplateInstantiation>>();
        _analytics = new Dictionary<string, TemplateAnalytics>();

        // Initialize with built-in templates
        InitializeBuiltInTemplates();
    }

    public async Task<WorkflowTemplate?> GetTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _templates.TryGetValue(templateId, out var template);
        return template;
    }

    public async Task<List<WorkflowTemplate>> ListTemplatesAsync(
        TemplateCategory? category = null,
        TemplateDifficulty? difficulty = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _templates.Values
            .Where(t => t.IsPublished)
            .Where(t => category == null || t.Category == category)
            .Where(t => difficulty == null || t.Difficulty == difficulty)
            .OrderByDescending(t => t.IsFeatured)
            .ThenByDescending(t => t.AverageRating)
            .Take(limit)
            .ToList();

        return results;
    }

    public async Task<List<WorkflowTemplate>> SearchTemplatesAsync(
        string query,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _templates.Values
            .Where(t => t.IsPublished)
            .Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(t => t.AverageRating)
            .ThenByDescending(t => t.UsageCount)
            .ToList();

        return results;
    }

    public async Task<List<WorkflowTemplate>> GetFeaturedTemplatesAsync(
        int limit = 10,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _templates.Values
            .Where(t => t.IsPublished && t.IsFeatured)
            .OrderByDescending(t => t.AverageRating)
            .Take(limit)
            .ToList();
    }

    public async Task<List<WorkflowTemplate>> GetTrendingTemplatesAsync(
        int limit = 10,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _templates.Values
            .Where(t => t.IsPublished)
            .OrderByDescending(t => t.UsageCount)
            .Take(limit)
            .ToList();
    }

    public async Task<TemplateInstantiation> InstantiateTemplateAsync(
        string templateId,
        string tenantId,
        Dictionary<string, object>? variables = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_templates.TryGetValue(templateId, out var template))
        {
            throw new KeyNotFoundException($"Template not found: {templateId}");
        }

        if (!template.IsPublished)
        {
            throw new InvalidOperationException($"Template not published: {templateId}");
        }

        var instantiation = new TemplateInstantiation
        {
            TemplateId = templateId,
            TenantId = tenantId,
            CustomVariables = variables ?? new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
        };

        if (!_instantiations.ContainsKey(templateId))
        {
            _instantiations[templateId] = new List<TemplateInstantiation>();
        }

        _instantiations[templateId].Add(instantiation);
        template.UsageCount++;

        _logger.LogInformation(
            "Template instantiated: {TemplateId}, Tenant: {TenantId}",
            templateId, tenantId);

        return instantiation;
    }

    public async Task<List<TemplateInstantiation>> GetInstantiationsAsync(
        string templateId,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_instantiations.TryGetValue(templateId, out var instantiations))
        {
            return new List<TemplateInstantiation>();
        }

        if (tenantId != null)
        {
            return instantiations
                .Where(i => i.TenantId == tenantId)
                .ToList();
        }

        return instantiations.ToList();
    }

    public async Task<WorkflowTemplate> CreateTemplateAsync(
        WorkflowTemplate template,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;

        _templates[template.TemplateId] = template;

        _logger.LogInformation(
            "Template created: {TemplateId} ({Name}), Category: {Category}",
            template.TemplateId, template.Name, template.Category);

        return template;
    }

    public async Task<WorkflowTemplate> UpdateTemplateAsync(
        string templateId,
        WorkflowTemplate template,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_templates.TryGetValue(templateId, out var existing))
        {
            throw new KeyNotFoundException($"Template not found: {templateId}");
        }

        template.TemplateId = templateId;
        template.CreatedAt = existing.CreatedAt;
        template.UpdatedAt = DateTime.UtcNow;

        _templates[templateId] = template;

        _logger.LogInformation(
            "Template updated: {TemplateId}",
            templateId);

        return template;
    }

    public async Task<bool> PublishTemplateAsync(
        string templateId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_templates.TryGetValue(templateId, out var template))
        {
            return false;
        }

        template.IsPublished = true;
        template.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Template published: {TemplateId}",
            templateId);

        return true;
    }

    public async Task<bool> DeprecateTemplateAsync(
        string templateId,
        string? replacementTemplateId = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_templates.TryGetValue(templateId, out var template))
        {
            return false;
        }

        template.IsPublished = false;
        template.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Template deprecated: {TemplateId}, Replacement: {ReplacementId}",
            templateId, replacementTemplateId ?? "none");

        return true;
    }

    public async Task<TemplateAnalytics> GetAnalyticsAsync(
        string templateId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate aggregation

        if (!_instantiations.TryGetValue(templateId, out var instantiations))
        {
            return new TemplateAnalytics { TemplateId = templateId };
        }

        var filteredInstantiations = instantiations
            .Where(i => from == null || i.CreatedAt >= from)
            .Where(i => to == null || i.CreatedAt <= to)
            .ToList();

        var analytics = new TemplateAnalytics
        {
            TemplateId = templateId,
            TotalInstantiations = filteredInstantiations.Count,
            ActiveWorkflows = filteredInstantiations.Count(i => i.ExecutionCount > 0),
            TotalExecutions = filteredInstantiations.Sum(i => i.ExecutionCount),
            SuccessRate = filteredInstantiations.Count > 0
                ? filteredInstantiations.Average(i => i.SuccessRate)
                : 0,
            UsageByTenant = filteredInstantiations
                .GroupBy(i => i.TenantId)
                .ToDictionary(g => g.Key, g => g.Count()),
        };

        return analytics;
    }

    public async Task<Dictionary<string, int>> GetLibraryStatisticsAsync(
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return new Dictionary<string, int>
        {
            ["total_templates"] = _templates.Count(t => t.Value.IsPublished),
            ["featured_templates"] = _templates.Count(t => t.Value.IsFeatured && t.Value.IsPublished),
            ["total_instantiations"] = _instantiations.Values.Sum(i => i.Count),
            ["categories"] = Enum.GetValues(typeof(TemplateCategory)).Length,
            ["most_used"] = _templates.Values.Max(t => t.UsageCount),
        };
    }

    private void InitializeBuiltInTemplates()
    {
        // Order Processing Template
        var orderProcessing = new WorkflowTemplate
        {
            TemplateId = "tmpl-order-processing",
            Name = "Order Processing Pipeline",
            Description = "Complete order processing with validation, payment, and fulfillment",
            Category = TemplateCategory.OrderProcessing,
            Difficulty = TemplateDifficulty.Intermediate,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "ecommerce", "payment", "fulfillment", "inventory" },
            IsFeatured = true,
            IsPublished = true,
            AverageRating = 4.8,
            RatingCount = 145,
            Variables = new List<TemplateVariable>
            {
                new TemplateVariable
                {
                    Name = "inventory_service_url",
                    DisplayName = "Inventory Service URL",
                    Type = "string",
                    IsRequired = true,
                },
                new TemplateVariable
                {
                    Name = "payment_gateway",
                    DisplayName = "Payment Gateway",
                    Type = "string",
                    AllowedValues = new List<object> { "stripe", "paypal", "square" },
                },
            },
        };

        _templates[orderProcessing.TemplateId] = orderProcessing;

        // Email Campaign Template
        var emailCampaign = new WorkflowTemplate
        {
            TemplateId = "tmpl-email-campaign",
            Name = "Email Marketing Campaign",
            Description = "Segment users and send targeted email campaigns with tracking",
            Category = TemplateCategory.EmailCampaign,
            Difficulty = TemplateDifficulty.Intermediate,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "marketing", "email", "segmentation", "analytics" },
            IsFeatured = true,
            IsPublished = true,
            AverageRating = 4.7,
            RatingCount = 89,
            Variables = new List<TemplateVariable>
            {
                new TemplateVariable
                {
                    Name = "email_provider",
                    DisplayName = "Email Provider",
                    Type = "string",
                    AllowedValues = new List<object> { "sendgrid", "mailchimp", "aws_ses" },
                },
            },
        };

        _templates[emailCampaign.TemplateId] = emailCampaign;

        // Data Pipeline Template
        var dataPipeline = new WorkflowTemplate
        {
            TemplateId = "tmpl-data-pipeline",
            Name = "ETL Data Pipeline",
            Description = "Extract, transform, load data with validation and error handling",
            Category = TemplateCategory.DataPipeline,
            Difficulty = TemplateDifficulty.Advanced,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "etl", "data", "warehouse", "analytics" },
            IsFeatured = true,
            IsPublished = true,
            AverageRating = 4.6,
            RatingCount = 67,
        };

        _templates[dataPipeline.TemplateId] = dataPipeline;

        // Approval Workflow Template
        var approvalWorkflow = new WorkflowTemplate
        {
            TemplateId = "tmpl-approval-workflow",
            Name = "Multi-Level Approval",
            Description = "Route requests through approval chain based on amount/type",
            Category = TemplateCategory.ApprovalWorkflow,
            Difficulty = TemplateDifficulty.Beginner,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "approval", "workflow", "routing", "notifications" },
            IsFeatured = false,
            IsPublished = true,
            AverageRating = 4.5,
            RatingCount = 102,
        };

        _templates[approvalWorkflow.TemplateId] = approvalWorkflow;

        // Data Validation Template
        var dataValidation = new WorkflowTemplate
        {
            TemplateId = "tmpl-data-validation",
            Name = "Data Quality Validation",
            Description = "Comprehensive data validation with rule engine and error reporting",
            Category = TemplateCategory.DataValidation,
            Difficulty = TemplateDifficulty.Advanced,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "validation", "quality", "data", "rules" },
            IsFeatured = false,
            IsPublished = true,
            AverageRating = 4.4,
            RatingCount = 45,
        };

        _templates[dataValidation.TemplateId] = dataValidation;

        // Notification System Template
        var notificationSystem = new WorkflowTemplate
        {
            TemplateId = "tmpl-notification-system",
            Name = "Multi-Channel Notifications",
            Description = "Send notifications via email, SMS, push, and webhooks",
            Category = TemplateCategory.NotificationSystem,
            Difficulty = TemplateDifficulty.Intermediate,
            Version = "1.0.0",
            Author = "Loco Team",
            Tags = new List<string> { "notifications", "multi-channel", "sms", "push" },
            IsFeatured = false,
            IsPublished = true,
            AverageRating = 4.7,
            RatingCount = 156,
        };

        _templates[notificationSystem.TemplateId] = notificationSystem;

        _logger.LogInformation("Initialized {Count} built-in templates", _templates.Count);
    }
}

/// <summary>
/// Built-in template definitions
/// </summary>
public static class BuiltInTemplates
{
    public static class OrderProcessing
    {
        public const string TemplateId = "tmpl-order-processing";

        public static List<string> Steps = new()
        {
            "validate-order",
            "check-inventory",
            "process-payment",
            "create-shipment",
            "send-confirmation",
            "archive-order",
        };
    }

    public static class DataPipeline
    {
        public const string TemplateId = "tmpl-data-pipeline";

        public static List<string> Steps = new()
        {
            "extract-source",
            "validate-data",
            "transform-format",
            "load-warehouse",
            "run-tests",
            "notify-completion",
        };
    }

    public static class EmailCampaign
    {
        public const string TemplateId = "tmpl-email-campaign";

        public static List<string> Steps = new()
        {
            "segment-audience",
            "fetch-content",
            "personalize-emails",
            "send-campaign",
            "track-opens",
            "compile-report",
        };
    }
}
