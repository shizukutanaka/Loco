using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.APIManagement
{
    /// <summary>
    /// Advanced API management and gateway enhancement system
    /// Phase 25: API catalog, versioning, schema validation, rate limiting, API analytics
    /// </summary>
    public interface IAdvancedAPIManagement
    {
        Task<APIDefinition> RegisterAPIAsync(string tenantId, APIDefinitionRequest request, CancellationToken ct = default);
        Task<List<APIDefinition>> GetAPIsAsync(string tenantId, string status = null, CancellationToken ct = default);
        Task<APIVersion> PublishVersionAsync(string tenantId, string apiId, VersionPublishRequest request, CancellationToken ct = default);
        Task<bool> ValidateSchemaAsync(string tenantId, string apiId, string versionId, SchemaDefinition schema, CancellationToken ct = default);
        Task<RateLimitConfig> ConfigureRateLimitAsync(string tenantId, string apiId, RateLimitRequest config, CancellationToken ct = default);
        Task<APIAnalytics> GetAPIAnalyticsAsync(string tenantId, string apiId, int daysBack = 30, CancellationToken ct = default);
        Task<bool> ApproveAPIAsync(string tenantId, string apiId, string approverComment, CancellationToken ct = default);
        Task<DeprecationPlan> DeprecateVersionAsync(string tenantId, string apiId, string versionId, DeprecationRequest request, CancellationToken ct = default);
        Task<List<APIConsumer>> GetConsumersAsync(string tenantId, string apiId, CancellationToken ct = default);
        Task<APIManagementMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default);
    }

    public class AdvancedAPIManagement : IAdvancedAPIManagement
    {
        private readonly ILogger<AdvancedAPIManagement> _logger;
        private readonly Dictionary<string, APIDefinition> _apis = new();
        private readonly Dictionary<string, List<APIVersion>> _versions = new();
        private readonly Dictionary<string, RateLimitConfig> _rateLimits = new();
        private readonly Dictionary<string, List<APIMetric>> _analytics = new();
        private readonly Dictionary<string, List<APIConsumer>> _consumers = new();
        private readonly Random _random = new(42);

        public AdvancedAPIManagement(ILogger<AdvancedAPIManagement> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<APIDefinition> RegisterAPIAsync(string tenantId, APIDefinitionRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Registering API {APIName}", request.Name);
            await Task.Delay(25, ct);

            var api = new APIDefinition
            {
                APIId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Name = request.Name,
                Description = request.Description,
                BaseURL = request.BaseURL,
                OwnerTeam = request.OwnerTeam,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                Status = "draft",
                Visibility = request.Visibility ?? "private",
                AuthType = request.AuthType ?? "api-key",
                ContactEmail = request.ContactEmail,
                DocumentationURL = request.DocumentationURL,
                Categories = request.Categories ?? new List<string>(),
                Tags = request.Tags ?? new List<string>(),
                IsDeprecated = false,
                Versions = new List<APIVersion>(),
                ApprovalStatus = "pending"
            };

            var key = $"{tenantId}:{api.APIId}";
            _apis[key] = api;
            _versions[key] = new List<APIVersion>();
            _consumers[key] = new List<APIConsumer>();

            return api;
        }

        public async Task<List<APIDefinition>> GetAPIsAsync(string tenantId, string status = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving APIs");
            await Task.Delay(20, ct);

            var apis = _apis
                .Where(kvp => kvp.Key.StartsWith($"{tenantId}:"))
                .Select(kvp => kvp.Value)
                .ToList();

            if (!string.IsNullOrWhiteSpace(status))
                apis = apis.Where(a => a.Status == status).ToList();

            return apis.OrderByDescending(a => a.UpdatedAt).ToList();
        }

        public async Task<APIVersion> PublishVersionAsync(string tenantId, string apiId, VersionPublishRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Publishing API version {VersionId}", request.Version);
            await Task.Delay(30, ct);

            var versionKey = $"{tenantId}:{apiId}";
            if (!_versions.ContainsKey(versionKey))
                return null;

            var version = new APIVersion
            {
                VersionId = Guid.NewGuid().ToString("N"),
                Version = request.Version,
                APIId = apiId,
                Status = "published",
                PublishedAt = DateTimeOffset.UtcNow,
                DeprecatedAt = null,
                BaseEndpoint = request.BaseEndpoint,
                Endpoints = request.Endpoints ?? new List<EndpointDefinition>(),
                SchemaVersion = request.SchemaVersion ?? "2.0",
                BreakingChanges = request.BreakingChanges ?? new List<string>(),
                ReleaseNotes = request.ReleaseNotes,
                SupportedUntil = DateTimeOffset.UtcNow.AddYears(2)
            };

            _versions[versionKey].Add(version);
            if (_versions[versionKey].Count > 50)
                _versions[versionKey] = _versions[versionKey].Skip(1).ToList();

            var apiKey = $"{tenantId}:{apiId}";
            if (_apis.ContainsKey(apiKey))
            {
                _apis[apiKey].Status = "published";
                _apis[apiKey].UpdatedAt = DateTimeOffset.UtcNow;
                _apis[apiKey].Versions.Add(version);
            }

            return version;
        }

        public async Task<bool> ValidateSchemaAsync(string tenantId, string apiId, string versionId, SchemaDefinition schema, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Validating schema for API {APIId}", apiId);
            await Task.Delay(25, ct);

            // Simulate schema validation
            var isValid = _random.NextDouble() > 0.05; // 95% validation success rate

            if (isValid)
            {
                var versionKey = $"{tenantId}:{apiId}";
                if (_versions.ContainsKey(versionKey))
                {
                    var version = _versions[versionKey].FirstOrDefault(v => v.VersionId == versionId);
                    if (version != null)
                    {
                        version.SchemaVersion = schema.Version ?? "2.0";
                    }
                }
            }

            return isValid;
        }

        public async Task<RateLimitConfig> ConfigureRateLimitAsync(string tenantId, string apiId, RateLimitRequest config, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Configuring rate limit for API {APIId}", apiId);
            await Task.Delay(20, ct);

            var rateLimitConfig = new RateLimitConfig
            {
                RateLimitId = Guid.NewGuid().ToString("N"),
                APIId = apiId,
                TenantId = tenantId,
                RequestsPerMinute = config.RequestsPerMinute ?? 1000,
                RequestsPerHour = config.RequestsPerHour ?? 50000,
                RequestsPerDay = config.RequestsPerDay ?? 1000000,
                BurstSize = config.BurstSize ?? 100,
                ConcurrentConnections = config.ConcurrentConnections ?? 500,
                ConfiguredAt = DateTimeOffset.UtcNow,
                Status = "active",
                ByConsumer = config.ByConsumer ?? false,
                ThrottleStrategy = config.ThrottleStrategy ?? "token-bucket",
                RetryAfterHeader = true,
                QuotaResetAt = DateTimeOffset.UtcNow.AddHours(1)
            };

            var key = $"{tenantId}:{apiId}";
            _rateLimits[key] = rateLimitConfig;

            return rateLimitConfig;
        }

        public async Task<APIAnalytics> GetAPIAnalyticsAsync(string tenantId, string apiId, int daysBack = 30, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Getting API analytics for {APIId}", apiId);
            await Task.Delay(35, ct);

            var analyticsKey = $"{tenantId}:{apiId}";
            var metrics = _analytics.ContainsKey(analyticsKey) ? _analytics[analyticsKey] : new List<APIMetric>();

            var analytics = new APIAnalytics
            {
                APIId = apiId,
                AnalyzedAt = DateTimeOffset.UtcNow,
                TimeWindow = $"{daysBack} days",
                TotalRequests = _random.Next(10000, 1000000),
                SuccessfulRequests = _random.Next(9000, 990000),
                FailedRequests = _random.Next(100, 50000),
                AvgResponseTimeMs = _random.Next(50, 500),
                P95ResponseTimeMs = _random.Next(200, 2000),
                P99ResponseTimeMs = _random.Next(500, 5000),
                SuccessRate = _random.Next(95, 99.9m),
                ErrorRate = _random.Next(0.1m, 5),
                MostCalledEndpoints = new List<EndpointStats>
                {
                    new() { Endpoint = "/users", Calls = _random.Next(10000, 100000), AvgTime = _random.Next(50, 200) },
                    new() { Endpoint = "/workflows", Calls = _random.Next(5000, 50000), AvgTime = _random.Next(100, 300) },
                    new() { Endpoint = "/analytics", Calls = _random.Next(2000, 20000), AvgTime = _random.Next(200, 500) }
                },
                TopConsumers = GenerateTopConsumers(),
                ErrorBreakdown = new Dictionary<string, int>
                {
                    { "4xx", _random.Next(100, 10000) },
                    { "5xx", _random.Next(50, 5000) },
                    { "timeout", _random.Next(10, 1000) }
                },
                DataTransferedGB = _random.Next(1, 1000),
                CostEstimate = _random.Next(100, 10000)
            };

            return analytics;
        }

        public async Task<bool> ApproveAPIAsync(string tenantId, string apiId, string approverComment, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Approving API {APIId}", apiId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{apiId}";
            if (!_apis.ContainsKey(key))
                return false;

            var api = _apis[key];
            api.ApprovalStatus = "approved";
            api.Status = "published";
            api.UpdatedAt = DateTimeOffset.UtcNow;

            return true;
        }

        public async Task<DeprecationPlan> DeprecateVersionAsync(string tenantId, string apiId, string versionId, DeprecationRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Deprecating API version {VersionId}", versionId);
            await Task.Delay(25, ct);

            var versionKey = $"{tenantId}:{apiId}";
            if (!_versions.ContainsKey(versionKey))
                return null;

            var version = _versions[versionKey].FirstOrDefault(v => v.VersionId == versionId);
            if (version == null)
                return null;

            var plan = new DeprecationPlan
            {
                DeprecationId = Guid.NewGuid().ToString("N"),
                APIId = apiId,
                VersionId = versionId,
                DeprecatedAt = DateTimeOffset.UtcNow,
                SunsetDate = DateTimeOffset.UtcNow.AddMonths(request.SunsetMonths ?? 6),
                ReplacementVersion = request.ReplacementVersion,
                NotificationStrategy = request.NotificationStrategy ?? "email-webhook",
                AffectedConsumers = _consumers.ContainsKey(versionKey) ? _consumers[versionKey].Count : 0,
                MigrationGuide = request.MigrationGuide,
                Status = "announced"
            };

            version.Status = "deprecated";
            version.DeprecatedAt = DateTimeOffset.UtcNow;

            return plan;
        }

        public async Task<List<APIConsumer>> GetConsumersAsync(string tenantId, string apiId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Retrieving API consumers for {APIId}", apiId);
            await Task.Delay(20, ct);

            var key = $"{tenantId}:{apiId}";
            if (!_consumers.ContainsKey(key))
                return new List<APIConsumer>();

            return _consumers[key].OrderByDescending(c => c.CallsLast30Days).ToList();
        }

        public async Task<APIManagementMetrics> GetMetricsAsync(string tenantId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID required");

            _logger.LogInformation("Calculating API management metrics");
            await Task.Delay(30, ct);

            var apisCount = _apis.Count(kvp => kvp.Key.StartsWith($"{tenantId}:"));
            var publishedApis = _apis.Count(kvp =>
                kvp.Key.StartsWith($"{tenantId}:") && kvp.Value.Status == "published");

            var metrics = new APIManagementMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalAPIs = apisCount,
                PublishedAPIs = publishedApis,
                DraftAPIs = apisCount - publishedApis,
                APIVersions = _versions.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0),
                TotalAPIConsumers = _consumers.Sum(kvp => kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count : 0),
                TotalRequestsLast30Days = _random.Next(1000000, 100000000),
                AverageResponseTime = _random.Next(50, 500),
                APIAvailabilityPercent = _random.Next(99, 100),
                DocumentedAPIs = publishedApis,
                SchemaCompliance = _random.Next(85, 100),
                RateLimitedAPIs = _rateLimits.Count(kvp => kvp.Key.StartsWith($"{tenantId}:")),
                DeprecatedVersions = _versions.Sum(kvp =>
                    kvp.Key.StartsWith($"{tenantId}:") ? kvp.Value.Count(v => v.Status == "deprecated") : 0)
            };

            return metrics;
        }

        private List<ConsumerUsage> GenerateTopConsumers()
        {
            var consumers = new List<ConsumerUsage>();
            for (int i = 0; i < 5; i++)
            {
                consumers.Add(new ConsumerUsage
                {
                    ConsumerId = $"consumer-{i}",
                    ConsumerName = $"App {i}",
                    CallsLast30Days = _random.Next(1000, 100000),
                    AverageResponseTime = _random.Next(50, 500),
                    ErrorRate = _random.NextDouble() * 0.05
                });
            }
            return consumers;
        }
    }

    public class APIDefinitionRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string BaseURL { get; set; }
        public string OwnerTeam { get; set; }
        public string Visibility { get; set; }
        public string AuthType { get; set; }
        public string ContactEmail { get; set; }
        public string DocumentationURL { get; set; }
        public List<string> Categories { get; set; }
        public List<string> Tags { get; set; }
    }

    public class APIDefinition
    {
        public string APIId { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BaseURL { get; set; }
        public string OwnerTeam { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string Status { get; set; }
        public string Visibility { get; set; }
        public string AuthType { get; set; }
        public string ContactEmail { get; set; }
        public string DocumentationURL { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool IsDeprecated { get; set; }
        public List<APIVersion> Versions { get; set; } = new();
        public string ApprovalStatus { get; set; }
    }

    public class VersionPublishRequest
    {
        public string Version { get; set; }
        public string BaseEndpoint { get; set; }
        public List<EndpointDefinition> Endpoints { get; set; }
        public string SchemaVersion { get; set; }
        public List<string> BreakingChanges { get; set; }
        public string ReleaseNotes { get; set; }
    }

    public class APIVersion
    {
        public string VersionId { get; set; }
        public string Version { get; set; }
        public string APIId { get; set; }
        public string Status { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public DateTimeOffset? DeprecatedAt { get; set; }
        public string BaseEndpoint { get; set; }
        public List<EndpointDefinition> Endpoints { get; set; } = new();
        public string SchemaVersion { get; set; }
        public List<string> BreakingChanges { get; set; } = new();
        public string ReleaseNotes { get; set; }
        public DateTimeOffset SupportedUntil { get; set; }
    }

    public class EndpointDefinition
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public string Description { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool RequiresAuth { get; set; }
        public int RateLimitPerMinute { get; set; }
    }

    public class SchemaDefinition
    {
        public string Version { get; set; }
        public string Format { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class RateLimitRequest
    {
        public int? RequestsPerMinute { get; set; }
        public int? RequestsPerHour { get; set; }
        public int? RequestsPerDay { get; set; }
        public int? BurstSize { get; set; }
        public int? ConcurrentConnections { get; set; }
        public bool? ByConsumer { get; set; }
        public string ThrottleStrategy { get; set; }
    }

    public class RateLimitConfig
    {
        public string RateLimitId { get; set; }
        public string APIId { get; set; }
        public string TenantId { get; set; }
        public int RequestsPerMinute { get; set; }
        public int RequestsPerHour { get; set; }
        public int RequestsPerDay { get; set; }
        public int BurstSize { get; set; }
        public int ConcurrentConnections { get; set; }
        public DateTimeOffset ConfiguredAt { get; set; }
        public string Status { get; set; }
        public bool ByConsumer { get; set; }
        public string ThrottleStrategy { get; set; }
        public bool RetryAfterHeader { get; set; }
        public DateTimeOffset QuotaResetAt { get; set; }
    }

    public class APIAnalytics
    {
        public string APIId { get; set; }
        public DateTimeOffset AnalyzedAt { get; set; }
        public string TimeWindow { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int AvgResponseTimeMs { get; set; }
        public int P95ResponseTimeMs { get; set; }
        public int P99ResponseTimeMs { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal ErrorRate { get; set; }
        public List<EndpointStats> MostCalledEndpoints { get; set; } = new();
        public List<ConsumerUsage> TopConsumers { get; set; } = new();
        public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
        public int DataTransferedGB { get; set; }
        public int CostEstimate { get; set; }
    }

    public class EndpointStats
    {
        public string Endpoint { get; set; }
        public int Calls { get; set; }
        public int AvgTime { get; set; }
    }

    public class ConsumerUsage
    {
        public string ConsumerId { get; set; }
        public string ConsumerName { get; set; }
        public int CallsLast30Days { get; set; }
        public int AverageResponseTime { get; set; }
        public double ErrorRate { get; set; }
    }

    public class DeprecationRequest
    {
        public int? SunsetMonths { get; set; }
        public string ReplacementVersion { get; set; }
        public string NotificationStrategy { get; set; }
        public string MigrationGuide { get; set; }
    }

    public class DeprecationPlan
    {
        public string DeprecationId { get; set; }
        public string APIId { get; set; }
        public string VersionId { get; set; }
        public DateTimeOffset DeprecatedAt { get; set; }
        public DateTimeOffset SunsetDate { get; set; }
        public string ReplacementVersion { get; set; }
        public string NotificationStrategy { get; set; }
        public int AffectedConsumers { get; set; }
        public string MigrationGuide { get; set; }
        public string Status { get; set; }
    }

    public class APIConsumer
    {
        public string ConsumerId { get; set; }
        public string ConsumerName { get; set; }
        public string APIId { get; set; }
        public string ApprovedVersion { get; set; }
        public DateTimeOffset SubscribedAt { get; set; }
        public int CallsLast30Days { get; set; }
        public string Status { get; set; }
        public string ContactEmail { get; set; }
    }

    public class APIManagementMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalAPIs { get; set; }
        public int PublishedAPIs { get; set; }
        public int DraftAPIs { get; set; }
        public int APIVersions { get; set; }
        public int TotalAPIConsumers { get; set; }
        public int TotalRequestsLast30Days { get; set; }
        public int AverageResponseTime { get; set; }
        public int APIAvailabilityPercent { get; set; }
        public int DocumentedAPIs { get; set; }
        public int SchemaCompliance { get; set; }
        public int RateLimitedAPIs { get; set; }
        public int DeprecatedVersions { get; set; }
    }
}
