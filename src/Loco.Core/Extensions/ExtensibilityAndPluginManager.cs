using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Extensions
{
    /// <summary>
    /// Extensibility and plugin architecture system
    /// Phase 26: Plugin management, custom steps, integration marketplace, extension deployment
    /// </summary>
    public interface IExtensibilityAndPluginManager
    {
        Task<Plugin> RegisterPluginAsync(string tenantId, PluginDefinition definition, CancellationToken ct = default);
        Task<List<Plugin>> GetPluginsAsync(string tenantId, string status = null, int limit = 100, CancellationToken ct = default);
        Task<bool> InstallPluginAsync(string tenantId, string pluginId, CancellationToken ct = default);
        Task<bool> UninstallPluginAsync(string tenantId, string pluginId, CancellationToken ct = default);
        Task<CustomStep> RegisterCustomStepAsync(string tenantId, CustomStepDefinition definition, CancellationToken ct = default);
        Task<List<CustomStep>> GetCustomStepsAsync(string tenantId, CancellationToken ct = default);
        Task<bool> ValidatePluginAsync(string tenantId, string pluginId, CancellationToken ct = default);
        Task<PluginMarketplaceItem> PublishToMarketplaceAsync(string tenantId, string pluginId, MarketplaceDefinition definition, CancellationToken ct = default);
        Task<List<PluginMarketplaceItem>> SearchMarketplaceAsync(string tenantId, string query = null, int limit = 50, CancellationToken ct = default);
        Task<ExtensibilityMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class ExtensibilityAndPluginManager : IExtensibilityAndPluginManager
    {
        private readonly ILogger<ExtensibilityAndPluginManager> _logger;
        private readonly Dictionary<string, Plugin> _plugins = new();
        private readonly Dictionary<string, List<CustomStep>> _customSteps = new();
        private readonly Dictionary<string, PluginMarketplaceItem> _marketplace = new();
        private readonly Dictionary<string, List<PluginExecution>> _executions = new();
        private readonly Random _random = new(42);

        public ExtensibilityAndPluginManager(ILogger<ExtensibilityAndPluginManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Plugin> RegisterPluginAsync(string tenantId, PluginDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Registering plugin {PluginName}", definition.Name);
            await Task.Delay(30, ct);

            var plugin = new Plugin
            {
                PluginId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                Description = definition.Description,
                Version = definition.Version ?? "1.0.0",
                Author = definition.Author,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Status = "draft",
                Type = definition.Type, // step, trigger, action, connector, ai-model
                Capabilities = definition.Capabilities ?? new List<string>(),
                Dependencies = definition.Dependencies ?? new List<string>(),
                Configuration = definition.Configuration ?? new Dictionary<string, object>(),
                Permissions = definition.Permissions ?? new List<string>(),
                InstallCount = 0,
                Rating = 0.0,
                IsPublished = false,
                IsVerified = false,
                ErrorRate = 0.0,
                ExecutionTime = 0
            };

            var key = $"{tenantId}:{plugin.PluginId}";
            _plugins[key] = plugin;
            _executions[key] = new List<PluginExecution>();

            return plugin;
        }

        public async Task<List<Plugin>> GetPluginsAsync(string tenantId, string status = null, int limit = 100, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving plugins");
            await Task.Delay(20, ct);

            var plugins = _plugins
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(status))
                plugins = plugins.Where(p => p.Status == status).ToList();

            return plugins.OrderByDescending(p => p.UpdatedAt).Take(limit).ToList();
        }

        public async Task<bool> InstallPluginAsync(string tenantId, string pluginId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Installing plugin {PluginId}", pluginId);
            await Task.Delay(40, ct);

            var key = $"{tenantId}:{pluginId}";
            if (!_plugins.ContainsKey(key))
                return false;

            var plugin = _plugins[key];
            plugin.Status = "installed";
            plugin.InstallCount++;
            plugin.UpdatedAt = DateTimeOffset.UtcNow;

            return _random.NextDouble() > 0.05; // 95% installation success
        }

        public async Task<bool> UninstallPluginAsync(string tenantId, string pluginId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Uninstalling plugin {PluginId}", pluginId);
            await Task.Delay(30, ct);

            var key = $"{tenantId}:{pluginId}";
            if (!_plugins.ContainsKey(key))
                return false;

            var plugin = _plugins[key];
            plugin.Status = "uninstalled";
            plugin.UpdatedAt = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<CustomStep> RegisterCustomStepAsync(string tenantId, CustomStepDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Registering custom step {StepName}", definition.Name);
            await Task.Delay(25, ct);

            var step = new CustomStep
            {
                StepId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = definition.Name,
                DisplayName = definition.DisplayName ?? definition.Name,
                Description = definition.Description,
                PluginId = definition.PluginId,
                Category = definition.Category ?? "custom",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = definition.CreatedBy,
                Status = "draft",
                InputSchema = definition.InputSchema ?? new Dictionary<string, object>(),
                OutputSchema = definition.OutputSchema ?? new Dictionary<string, object>(),
                IsAsync = definition.IsAsync ?? true,
                RetryPolicy = definition.RetryPolicy,
                Timeout = definition.Timeout ?? 30000, // 30 seconds default
                ErrorHandling = definition.ErrorHandling ?? "fail",
                Usage = 0,
                Rating = 0.0
            };

            var key = $"{tenantId}";
            if (!_customSteps.ContainsKey(key))
                _customSteps[key] = new List<CustomStep>();

            _customSteps[key].Add(step);
            return step;
        }

        public async Task<List<CustomStep>> GetCustomStepsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving custom steps");
            await Task.Delay(20, ct);

            var key = $"{tenantId}";
            if (!_customSteps.ContainsKey(key))
                return new List<CustomStep>();

            return _customSteps[key].OrderByDescending(s => s.Usage).ToList();
        }

        public async Task<bool> ValidatePluginAsync(string tenantId, string pluginId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Validating plugin {PluginId}", pluginId);
            await Task.Delay(50, ct);

            var key = $"{tenantId}:{pluginId}";
            if (!_plugins.ContainsKey(key))
                return false;

            var plugin = _plugins[key];
            var isValid = _random.NextDouble() > 0.1; // 90% validation success

            if (isValid)
            {
                plugin.Status = "validated";
                plugin.IsVerified = true;
            }

            return isValid;
        }

        public async Task<PluginMarketplaceItem> PublishToMarketplaceAsync(string tenantId, string pluginId, MarketplaceDefinition definition, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Publishing plugin {PluginId} to marketplace", pluginId);
            await Task.Delay(40, ct);

            var key = $"{tenantId}:{pluginId}";
            if (!_plugins.ContainsKey(key))
                return null;

            var plugin = _plugins[key];
            var item = new PluginMarketplaceItem
            {
                ListingId = Guid.NewGuid().ToString("N"),
                PluginId = pluginId,
                TenantId = tenantId,
                Name = plugin.Name,
                Description = plugin.Description,
                Version = plugin.Version,
                Author = plugin.Author,
                Category = definition.Category ?? "general",
                PublishedAt = DateTimeOffset.UtcNow,
                Status = "published",
                Price = definition.Price ?? 0,
                License = definition.License ?? "MIT",
                SourceUrl = definition.SourceUrl,
                DocumentationUrl = definition.DocumentationUrl,
                SupportUrl = definition.SupportUrl,
                Rating = 0.0,
                DownloadCount = 0,
                Reviews = new List<MarketplaceReview>(),
                Tags = definition.Tags ?? new List<string>(),
                Verified = false
            };

            var marketplaceKey = $"{tenantId}:{item.ListingId}";
            _marketplace[marketplaceKey] = item;

            plugin.IsPublished = true;
            plugin.Status = "published";

            return item;
        }

        public async Task<List<PluginMarketplaceItem>> SearchMarketplaceAsync(string tenantId, string query = null, int limit = 50, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Searching marketplace");
            await Task.Delay(25, ct);

            var items = _marketplace
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(query))
                items = items.Where(i =>
                    i.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

            return items.OrderByDescending(i => i.DownloadCount).Take(limit).ToList();
        }

        public async Task<ExtensibilityMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating extensibility metrics");
            await Task.Delay(30, ct);

            var metrics = new ExtensibilityMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalPlugins = _plugins.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                InstalledPlugins = _plugins.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Status == "installed"),
                PublishedPlugins = _plugins.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.IsPublished),
                VerifiedPlugins = _plugins.Count(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.IsVerified),
                CustomSteps = _customSteps.ContainsKey(tenantId) ? _customSteps[tenantId].Count : 0,
                MarketplaceListings = _marketplace.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                TotalDownloads = _marketplace.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.DownloadCount : 0),
                PluginExecutions = _executions.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0),
                AveragePluginRating = _plugins
                    .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                    .Select(kvp => kvp.Value.Rating)
                    .DefaultIfEmpty(0)
                    .Average(),
                PluginFailureRate = _random.NextDouble() * 0.05, // 0-5%
                AveragePluginErrorRate = _random.NextDouble() * 0.02, // 0-2%
                ExtensionAdoptionScore = _random.Next(40, 95)
            };

            return metrics;
        }
    }

    public class PluginDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Type { get; set; }
        public List<string> Capabilities { get; set; }
        public List<string> Dependencies { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
        public List<string> Permissions { get; set; }
    }

    public class Plugin
    {
        public string PluginId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public List<string> Capabilities { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public int InstallCount { get; set; }
        public double Rating { get; set; }
        public bool IsPublished { get; set; }
        public bool IsVerified { get; set; }
        public double ErrorRate { get; set; }
        public int ExecutionTime { get; set; }
    }

    public class CustomStepDefinition
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string PluginId { get; set; }
        public string Category { get; set; }
        public string CreatedBy { get; set; }
        public Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object> OutputSchema { get; set; }
        public bool? IsAsync { get; set; }
        public RetryPolicy RetryPolicy { get; set; }
        public int? Timeout { get; set; }
        public string ErrorHandling { get; set; }
    }

    public class CustomStep
    {
        public string StepId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string PluginId { get; set; }
        public string Category { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
        public Dictionary<string, object> InputSchema { get; set; } = new();
        public Dictionary<string, object> OutputSchema { get; set; } = new();
        public bool IsAsync { get; set; }
        public RetryPolicy RetryPolicy { get; set; }
        public int Timeout { get; set; }
        public string ErrorHandling { get; set; }
        public int Usage { get; set; }
        public double Rating { get; set; }
    }

    public class RetryPolicy
    {
        public int MaxAttempts { get; set; }
        public int InitialDelayMs { get; set; }
        public int MaxDelayMs { get; set; }
        public double BackoffMultiplier { get; set; }
    }

    public class MarketplaceDefinition
    {
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string License { get; set; }
        public string SourceUrl { get; set; }
        public string DocumentationUrl { get; set; }
        public string SupportUrl { get; set; }
        public List<string> Tags { get; set; }
    }

    public class PluginMarketplaceItem
    {
        public string ListingId { get; set; }
        public string PluginId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string Status { get; set; }
        public decimal Price { get; set; }
        public string License { get; set; }
        public string SourceUrl { get; set; }
        public string DocumentationUrl { get; set; }
        public string SupportUrl { get; set; }
        public double Rating { get; set; }
        public int DownloadCount { get; set; }
        public List<MarketplaceReview> Reviews { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool Verified { get; set; }
    }

    public class MarketplaceReview
    {
        public string ReviewId { get; set; }
        public string ReviewerId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class PluginExecution
    {
        public string ExecutionId { get; set; }
        public string PluginId { get; set; }
        public DateTimeOffset ExecutedAt { get; set; }
        public string Status { get; set; }
        public int DurationMs { get; set; }
    }

    public class ExtensibilityMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalPlugins { get; set; }
        public int InstalledPlugins { get; set; }
        public int PublishedPlugins { get; set; }
        public int VerifiedPlugins { get; set; }
        public int CustomSteps { get; set; }
        public int MarketplaceListings { get; set; }
        public int TotalDownloads { get; set; }
        public int PluginExecutions { get; set; }
        public double AveragePluginRating { get; set; }
        public double PluginFailureRate { get; set; }
        public double AveragePluginErrorRate { get; set; }
        public int ExtensionAdoptionScore { get; set; }
    }
}
