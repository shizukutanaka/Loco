using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;

namespace Loco.Core.RateLimiting
{
    /// <summary>
    /// Rate limiting and tenant quotas engine
    /// Phase 19: Token bucket algorithm for API rate limiting, usage quotas, and fairness
    /// </summary>
    public interface IRateLimitingEngine
    {
        Task<RateLimitCheckResult> CheckRateLimitAsync(string tenantId, string endpoint, CancellationToken cancellationToken = default);
        Task<QuotaStatus> GetQuotaStatusAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<QuotaPlan> GetQuotaPlanAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<QuotaPlan> UpdateQuotaPlanAsync(string tenantId, QuotaPlan newPlan, CancellationToken cancellationToken = default);
        Task<bool> ConsumeQuotaAsync(string tenantId, string operation, int units = 1, CancellationToken cancellationToken = default);
        Task<List<QuotaViolation>> GetQuotaViolationsAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<RateLimitMetrics> GetRateLimitMetricsAsync(string tenantId, CancellationToken cancellationToken = default);
        Task ResetQuotasAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<UsageReport> GenerateUsageReportAsync(string tenantId, CancellationToken cancellationToken = default);
    }

    public class RateLimitingEngine : IRateLimitingEngine
    {
        private readonly ILogger<RateLimitingEngine> _logger;
        private readonly Dictionary<string, TokenBucket> _buckets = new();
        private readonly Dictionary<string, QuotaPlan> _plans = new();
        private readonly Dictionary<string, QuotaUsage> _usage = new();
        private readonly Dictionary<string, List<QuotaViolation>> _violations = new();
        private readonly Random _random = new(42);

        private const int DEFAULT_REQUESTS_PER_MINUTE = 100;
        private const int DEFAULT_EXECUTIONS_PER_DAY = 10000;
        private const int DEFAULT_API_CALLS_PER_MONTH = 1000000;

        public RateLimitingEngine(ILogger<RateLimitingEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RateLimitCheckResult> CheckRateLimitAsync(string tenantId, string endpoint, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint is required", nameof(endpoint));

            _logger.LogInformation("Checking rate limit for {TenantId} on {Endpoint}", tenantId, endpoint);

            await Task.Delay(5, cancellationToken);

            var key = $"{tenantId}:{endpoint}";

            if (!_buckets.ContainsKey(key))
            {
                _buckets[key] = new TokenBucket
                {
                    Capacity = DEFAULT_REQUESTS_PER_MINUTE,
                    Tokens = DEFAULT_REQUESTS_PER_MINUTE,
                    RefillRate = 1,
                    LastRefillAt = DateTimeOffset.UtcNow
                };
            }

            var bucket = _buckets[key];

            // Refill tokens based on time elapsed
            var timeSinceLastRefill = DateTimeOffset.UtcNow - bucket.LastRefillAt;
            var tokensToAdd = (int)(timeSinceLastRefill.TotalSeconds * bucket.RefillRate);
            bucket.Tokens = Math.Min(bucket.Capacity, bucket.Tokens + tokensToAdd);
            bucket.LastRefillAt = DateTimeOffset.UtcNow;

            var allowed = bucket.Tokens >= 1;
            if (allowed)
                bucket.Tokens--;

            var result = new RateLimitCheckResult
            {
                Allowed = allowed,
                TenantId = tenantId,
                Endpoint = endpoint,
                CheckedAt = DateTimeOffset.UtcNow,
                RemainingTokens = bucket.Tokens,
                ResetAt = DateTimeOffset.UtcNow.AddMinutes(1),
                RateLimitStatus = allowed ? "allowed" : "rate_limit_exceeded"
            };

            if (!allowed)
                LogViolation(tenantId, $"Rate limit exceeded on {endpoint}");

            return result;
        }

        public async Task<QuotaStatus> GetQuotaStatusAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Getting quota status for {TenantId}", tenantId);

            await Task.Delay(30, cancellationToken);

            var plan = _plans.ContainsKey(tenantId)
                ? _plans[tenantId]
                : GetDefaultPlan(tenantId);

            var usage = _usage.ContainsKey(tenantId)
                ? _usage[tenantId]
                : new QuotaUsage { TenantId = tenantId, DailyExecutions = 0, MonthlyApiCalls = 0, ConcurrentWorkflows = 0 };

            var status = new QuotaStatus
            {
                TenantId = tenantId,
                CheckedAt = DateTimeOffset.UtcNow,
                Plan = plan,
                CurrentUsage = usage,
                ExecutionsRemaining = plan.DailyExecutions - usage.DailyExecutions,
                ApiCallsRemaining = plan.MonthlyApiCalls - usage.MonthlyApiCalls,
                ExecutionPercentage = (usage.DailyExecutions / (double)plan.DailyExecutions) * 100,
                ApiCallPercentage = (usage.MonthlyApiCalls / (double)plan.MonthlyApiCalls) * 100,
                OverQuota = usage.DailyExecutions > plan.DailyExecutions || usage.MonthlyApiCalls > plan.MonthlyApiCalls
            };

            return status;
        }

        public async Task<QuotaPlan> GetQuotaPlanAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving quota plan for {TenantId}", tenantId);

            await Task.Delay(20, cancellationToken);

            if (!_plans.ContainsKey(tenantId))
                _plans[tenantId] = GetDefaultPlan(tenantId);

            return _plans[tenantId];
        }

        public async Task<QuotaPlan> UpdateQuotaPlanAsync(string tenantId, QuotaPlan newPlan, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (newPlan == null)
                throw new ArgumentNullException(nameof(newPlan));

            _logger.LogInformation("Updating quota plan for {TenantId}", tenantId);

            await Task.Delay(40, cancellationToken);

            newPlan.TenantId = tenantId;
            newPlan.UpdatedAt = DateTimeOffset.UtcNow;

            _plans[tenantId] = newPlan;

            return newPlan;
        }

        public async Task<bool> ConsumeQuotaAsync(string tenantId, string operation, int units = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Operation is required", nameof(operation));

            _logger.LogInformation("Consuming {Units} quota units for {Operation} in {TenantId}", units, operation, tenantId);

            await Task.Delay(10, cancellationToken);

            if (!_usage.ContainsKey(tenantId))
                _usage[tenantId] = new QuotaUsage { TenantId = tenantId };

            var plan = _plans.ContainsKey(tenantId) ? _plans[tenantId] : GetDefaultPlan(tenantId);
            var usage = _usage[tenantId];

            switch (operation.ToLower())
            {
                case "workflow-execution":
                    if (usage.DailyExecutions + units > plan.DailyExecutions)
                    {
                        LogViolation(tenantId, $"Daily execution quota exceeded");
                        return false;
                    }
                    usage.DailyExecutions += units;
                    break;

                case "api-call":
                    if (usage.MonthlyApiCalls + units > plan.MonthlyApiCalls)
                    {
                        LogViolation(tenantId, $"Monthly API call quota exceeded");
                        return false;
                    }
                    usage.MonthlyApiCalls += units;
                    break;

                case "concurrent-workflow":
                    if (usage.ConcurrentWorkflows + units > plan.MaxConcurrentWorkflows)
                    {
                        LogViolation(tenantId, $"Concurrent workflow limit exceeded");
                        return false;
                    }
                    usage.ConcurrentWorkflows += units;
                    break;

                default:
                    return false;
            }

            _usage[tenantId] = usage;
            return true;
        }

        public async Task<List<QuotaViolation>> GetQuotaViolationsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Retrieving quota violations for {TenantId}", tenantId);

            await Task.Delay(30, cancellationToken);

            if (!_violations.ContainsKey(tenantId))
                return new List<QuotaViolation>();

            return _violations[tenantId]
                .OrderByDescending(v => v.Timestamp)
                .Take(100)
                .ToList();
        }

        public async Task<RateLimitMetrics> GetRateLimitMetricsAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Calculating rate limit metrics for {TenantId}", tenantId);

            await Task.Delay(50, cancellationToken);

            var violations = _violations.ContainsKey(tenantId) ? _violations[tenantId] : new List<QuotaViolation>();
            var last24h = violations.Where(v => v.Timestamp >= DateTimeOffset.UtcNow.AddHours(-24)).ToList();
            var last7d = violations.Where(v => v.Timestamp >= DateTimeOffset.UtcNow.AddDays(-7)).ToList();

            var metrics = new RateLimitMetrics
            {
                TenantId = tenantId,
                CalculatedAt = DateTimeOffset.UtcNow,
                TotalViolations = violations.Count,
                Violations24h = last24h.Count,
                Violations7d = last7d.Count,
                AverageViolationsPerDay = violations.Count > 0 ? violations.Count / 30.0 : 0,
                MostViolatedQuota = violations.GroupBy(v => v.QuotaType)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "none",
                ComplianceScore = 1 - (violations.Count / 1000.0), // Simulated
                AlertLevel = violations.Count > 50 ? "high" : violations.Count > 10 ? "medium" : "low"
            };

            return metrics;
        }

        public async Task ResetQuotasAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Resetting quotas for {TenantId}", tenantId);

            await Task.Delay(30, cancellationToken);

            if (_usage.ContainsKey(tenantId))
            {
                _usage[tenantId].DailyExecutions = 0;
                _usage[tenantId].MonthlyApiCalls = 0;
                _usage[tenantId].ConcurrentWorkflows = 0;
            }
        }

        public async Task<UsageReport> GenerateUsageReportAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            _logger.LogInformation("Generating usage report for {TenantId}", tenantId);

            await Task.Delay(80, cancellationToken);

            var plan = _plans.ContainsKey(tenantId) ? _plans[tenantId] : GetDefaultPlan(tenantId);
            var usage = _usage.ContainsKey(tenantId) ? _usage[tenantId] : new QuotaUsage { TenantId = tenantId };

            var report = new UsageReport
            {
                TenantId = tenantId,
                ReportedAt = DateTimeOffset.UtcNow,
                PlanName = plan.Name,
                DailyExecutions = new { Used = usage.DailyExecutions, Limit = plan.DailyExecutions, Percentage = (usage.DailyExecutions / (double)plan.DailyExecutions) * 100 },
                MonthlyApiCalls = new { Used = usage.MonthlyApiCalls, Limit = plan.MonthlyApiCalls, Percentage = (usage.MonthlyApiCalls / (double)plan.MonthlyApiCalls) * 100 },
                ConcurrentWorkflows = new { Used = usage.ConcurrentWorkflows, Limit = plan.MaxConcurrentWorkflows, Percentage = (usage.ConcurrentWorkflows / (double)plan.MaxConcurrentWorkflows) * 100 },
                ResetDate = DateTimeOffset.UtcNow.AddDays(1),
                EstimatedCost = plan.PricePerMonth
            };

            return report;
        }

        private QuotaPlan GetDefaultPlan(string tenantId)
        {
            return new QuotaPlan
            {
                TenantId = tenantId,
                Name = "Standard",
                DailyExecutions = DEFAULT_EXECUTIONS_PER_DAY,
                MonthlyApiCalls = DEFAULT_API_CALLS_PER_MONTH,
                MaxConcurrentWorkflows = 10,
                RequestsPerMinute = DEFAULT_REQUESTS_PER_MINUTE,
                CreatedAt = DateTimeOffset.UtcNow,
                PricePerMonth = 99,
                BillingCycle = "monthly"
            };
        }

        private void LogViolation(string tenantId, string reason)
        {
            if (!_violations.ContainsKey(tenantId))
                _violations[tenantId] = new List<QuotaViolation>();

            _violations[tenantId].Add(new QuotaViolation
            {
                ViolationId = Guid.NewGuid().ToString("N"),
                TenantId = tenantId,
                Timestamp = DateTimeOffset.UtcNow,
                Reason = reason,
                QuotaType = ExtractQuotaType(reason),
                Severity = "warning"
            });
        }

        private string ExtractQuotaType(string reason)
        {
            if (reason.Contains("execution")) return "daily-executions";
            if (reason.Contains("API")) return "monthly-api-calls";
            if (reason.Contains("concurrent")) return "concurrent-workflows";
            return "rate-limit";
        }
    }

    // Domain Models
    public class TokenBucket
    {
        public int Capacity { get; set; }
        public int Tokens { get; set; }
        public double RefillRate { get; set; } // Tokens per second
        public DateTimeOffset LastRefillAt { get; set; }
    }

    public class RateLimitCheckResult
    {
        public bool Allowed { get; set; }
        public string TenantId { get; set; }
        public string Endpoint { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public int RemainingTokens { get; set; }
        public DateTimeOffset ResetAt { get; set; }
        public string RateLimitStatus { get; set; }
    }

    public class QuotaPlan
    {
        public string TenantId { get; set; }
        public string Name { get; set; }
        public int DailyExecutions { get; set; }
        public int MonthlyApiCalls { get; set; }
        public int MaxConcurrentWorkflows { get; set; }
        public int RequestsPerMinute { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public double PricePerMonth { get; set; }
        public string BillingCycle { get; set; }
    }

    public class QuotaUsage
    {
        public string TenantId { get; set; }
        public int DailyExecutions { get; set; }
        public int MonthlyApiCalls { get; set; }
        public int ConcurrentWorkflows { get; set; }
    }

    public class QuotaStatus
    {
        public string TenantId { get; set; }
        public DateTimeOffset CheckedAt { get; set; }
        public QuotaPlan Plan { get; set; }
        public QuotaUsage CurrentUsage { get; set; }
        public int ExecutionsRemaining { get; set; }
        public int ApiCallsRemaining { get; set; }
        public double ExecutionPercentage { get; set; }
        public double ApiCallPercentage { get; set; }
        public bool OverQuota { get; set; }
    }

    public class QuotaViolation
    {
        public string ViolationId { get; set; }
        public string TenantId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Reason { get; set; }
        public string QuotaType { get; set; }
        public string Severity { get; set; }
    }

    public class RateLimitMetrics
    {
        public string TenantId { get; set; }
        public DateTimeOffset CalculatedAt { get; set; }
        public int TotalViolations { get; set; }
        public int Violations24h { get; set; }
        public int Violations7d { get; set; }
        public double AverageViolationsPerDay { get; set; }
        public string MostViolatedQuota { get; set; }
        public double ComplianceScore { get; set; }
        public string AlertLevel { get; set; }
    }

    public class UsageReport
    {
        public string TenantId { get; set; }
        public DateTimeOffset ReportedAt { get; set; }
        public string PlanName { get; set; }
        public dynamic DailyExecutions { get; set; }
        public dynamic MonthlyApiCalls { get; set; }
        public dynamic ConcurrentWorkflows { get; set; }
        public DateTimeOffset ResetDate { get; set; }
        public double EstimatedCost { get; set; }
    }
}
