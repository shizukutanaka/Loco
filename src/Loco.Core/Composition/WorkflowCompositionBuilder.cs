using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Composition
{
    public interface IWorkflowCompositionBuilder
    {
        Task<ComposedWorkflow> CreateFromTemplatesAsync(string tenantId, List<string> templateIds, CancellationToken ct = default);
        Task<ComposedWorkflow> GetCompositionAsync(string tenantId, string compositionId, CancellationToken ct = default);
        Task<List<ComposedWorkflow>> GetCompositionsAsync(string tenantId, int limit = 50, CancellationToken ct = default);
        Task<bool> AddStepAsync(string tenantId, string compositionId, string stepName, CancellationToken ct = default);
        Task<bool> RemoveStepAsync(string tenantId, string compositionId, string stepName, CancellationToken ct = default);
        Task<CompositionValidationResult> ValidateCompositionAsync(string tenantId, string compositionId, CancellationToken ct = default);
        Task<CompositionMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class WorkflowCompositionBuilder : IWorkflowCompositionBuilder
    {
        private readonly Dictionary<string, ComposedWorkflow> _compositions = new();
        private readonly ILogger<WorkflowCompositionBuilder> _logger;

        public WorkflowCompositionBuilder(ILogger<WorkflowCompositionBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ComposedWorkflow> CreateFromTemplatesAsync(string tenantId, List<string> templateIds, CancellationToken ct = default)
        {
            _logger.LogInformation("Creating composition from templates");
            await Task.Delay(30, ct);

            var composition = new ComposedWorkflow
            {
                CompositionId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                TemplateIds = templateIds,
                CreatedAt = DateTimeOffset.UtcNow,
                Steps = new List<string>(),
                Status = "draft"
            };

            var key = $"{tenantId}:{composition.CompositionId}";
            _compositions[key] = composition;
            return composition;
        }

        public async Task<ComposedWorkflow> GetCompositionAsync(string tenantId, string compositionId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting composition {CompositionId}", compositionId);
            await Task.Delay(10, ct);

            var key = $"{tenantId}:{compositionId}";
            if (!_compositions.ContainsKey(key))
                throw new InvalidOperationException("Not found");
            return _compositions[key];
        }

        public async Task<List<ComposedWorkflow>> GetCompositionsAsync(string tenantId, int limit = 50, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting compositions");
            await Task.Delay(20, ct);

            return _compositions
                .Where(k => k.Key.StartsWith($"{tenantId}:"))
                .Select(k => k.Value)
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task<bool> AddStepAsync(string tenantId, string compositionId, string stepName, CancellationToken ct = default)
        {
            _logger.LogInformation("Adding step {StepName}", stepName);
            await Task.Delay(15, ct);

            var key = $"{tenantId}:{compositionId}";
            if (!_compositions.ContainsKey(key))
                return false;

            _compositions[key].Steps.Add(stepName);
            return true;
        }

        public async Task<bool> RemoveStepAsync(string tenantId, string compositionId, string stepName, CancellationToken ct = default)
        {
            _logger.LogInformation("Removing step {StepName}", stepName);
            await Task.Delay(15, ct);

            var key = $"{tenantId}:{compositionId}";
            if (!_compositions.ContainsKey(key))
                return false;

            _compositions[key].Steps.Remove(stepName);
            return true;
        }

        public async Task<CompositionValidationResult> ValidateCompositionAsync(string tenantId, string compositionId, CancellationToken ct = default)
        {
            _logger.LogInformation("Validating composition");
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{compositionId}";
            if (!_compositions.ContainsKey(key))
                return new CompositionValidationResult { IsValid = false };

            var composition = _compositions[key];
            return new CompositionValidationResult
            {
                IsValid = composition.Steps.Count > 0,
                ValidatedAt = DateTimeOffset.UtcNow
            };
        }

        public async Task<CompositionMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting metrics");
            await Task.Delay(25, ct);

            var compositions = _compositions.Where(k => k.Key.StartsWith($"{tenantId}:")).Select(k => k.Value).ToList();
            return new CompositionMetrics
            {
                TenantId = tenantId,
                TotalCompositions = compositions.Count,
                CalculatedAt = DateTimeOffset.UtcNow
            };
        }
    }

    public class ComposedWorkflow
    {
        public string CompositionId { get; set; }
        public string TenantId { get; set; }
        public List<string> TemplateIds { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public List<string> Steps { get; set; } = new();
        public string Status { get; set; }
    }

    public class CompositionValidationResult
    {
        public bool IsValid { get; set; }
        public DateTimeOffset ValidatedAt { get; set; }
    }

    public class CompositionMetrics
    {
        public string TenantId { get; set; }
        public int TotalCompositions { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
    }
}
