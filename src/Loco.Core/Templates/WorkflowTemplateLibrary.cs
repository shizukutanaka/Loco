using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Templates
{
    public interface IWorkflowTemplateLibrary
    {
        Task<WorkflowTemplate> CreateTemplateAsync(string tenantId, string name, CancellationToken ct = default);
        Task<WorkflowTemplate> GetTemplateAsync(string tenantId, string templateId, CancellationToken ct = default);
        Task<List<WorkflowTemplate>> SearchTemplatesAsync(string tenantId, string category = null, int limit = 50, CancellationToken ct = default);
        Task<bool> PublishTemplateAsync(string tenantId, string templateId, CancellationToken ct = default);
        Task<TemplateLibraryMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class WorkflowTemplateLibrary : IWorkflowTemplateLibrary
    {
        private readonly Dictionary<string, WorkflowTemplate> _templates = new();
        private readonly ILogger<WorkflowTemplateLibrary> _logger;
        private readonly Random _random = new(42);

        public WorkflowTemplateLibrary(ILogger<WorkflowTemplateLibrary> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WorkflowTemplate> CreateTemplateAsync(string tenantId, string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Creating template {Name}", name);
            await Task.Delay(20, ct);

            var template = new WorkflowTemplate
            {
                TemplateId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow,
                Status = "draft",
                UsageCount = 0,
                Rating = 0.0
            };

            var key = $"{tenantId}:{template.TemplateId}";
            _templates[key] = template;
            return template;
        }

        public async Task<WorkflowTemplate> GetTemplateAsync(string tenantId, string templateId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting template {TemplateId}", templateId);
            await Task.Delay(10, ct);

            var key = $"{tenantId}:{templateId}";
            return !_templates.ContainsKey(key) ? throw new InvalidOperationException("Not found") : _templates[key];
        }

        public async Task<List<WorkflowTemplate>> SearchTemplatesAsync(string tenantId, string category = null, int limit = 50, CancellationToken ct = default)
        {
            _logger.LogInformation("Searching templates");
            await Task.Delay(20, ct);

            return _templates
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .OrderByDescending(t => t.UsageCount)
                .Take(limit)
                .ToList();
        }

        public async Task<bool> PublishTemplateAsync(string tenantId, string templateId, CancellationToken ct = default)
        {
            _logger.LogInformation("Publishing template {TemplateId}", templateId);
            await Task.Delay(15, ct);

            var key = $"{tenantId}:{templateId}";
            if (!_templates.ContainsKey(key))
                return false;

            _templates[key].Status = "published";
            _templates[key].PublishedAt = DateTimeOffset.UtcNow;
            return true;
        }

        public async Task<TemplateLibraryMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting metrics");
            await Task.Delay(25, ct);

            var templates = _templates.Where(k => k.Key.StartsWith($"{tenantId}:")).Select(k => k.Value).ToList();
            return new TemplateLibraryMetrics
            {
                TenantId = tenantId,
                TotalTemplates = templates.Count,
                PublishedTemplates = templates.Count(t => t.Status == "published"),
                AverageRating = templates.Count > 0 ? templates.Average(t => t.Rating) : 0,
                CalculatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public class WorkflowTemplate
    {
        public string TemplateId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string Status { get; set; }
        public int UsageCount { get; set; }
        public double Rating { get; set; }
    }

    public class TemplateLibraryMetrics
    {
        public string TenantId { get; set; }
        public int TotalTemplates { get; set; }
        public int PublishedTemplates { get; set; }
        public double AverageRating { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
    }
}
