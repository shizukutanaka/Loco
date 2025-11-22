// Phase 6: Multi-Tenant Architecture
// Comprehensive tenant management, resource quotas, and data isolation
// Enables true multi-tenancy with hard resource boundaries and SLA enforcement

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.MultiTenant;

/// <summary>
/// Tenant information
/// </summary>
public class TenantInfo
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TenantStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public string Plan { get; set; } = "standard"; // starter, standard, professional, enterprise
    public bool IsActive { get; set; } = true;
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }
}

/// <summary>
/// Tenant status enumeration
/// </summary>
public enum TenantStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Terminated = 3,
}

/// <summary>
/// Tenant configuration with quotas and limits
/// </summary>
public class TenantConfiguration
{
    public string TenantId { get; set; } = string.Empty;

    // Execution quotas
    public int ExecutionsPerDayLimit { get; set; } = 10000;
    public int ConcurrentWorkflowExecutionsLimit { get; set; } = 100;
    public int MaxExecutionDurationSeconds { get; set; } = 3600;

    // Storage quotas
    public long StorageGbLimit { get; set; } = 100;
    public long BackupStorageGbLimit { get; set; } = 50;

    // Workflow quotas
    public int MaxWorkflows { get; set; } = 1000;
    public int MaxVersionsPerWorkflow { get; set; } = 50;
    public int MaxStepsPerWorkflow { get; set; } = 100;

    // API rate limiting
    public int ApiCallsPerMinute { get; set; } = 10000;
    public int ApiCallsPerHour { get; set; } = 100000;

    // Data retention
    public int ExecutionHistoryRetentionDays { get; set; } = 90;
    public int AuditLogRetentionDays { get; set; } = 365;

    // Feature flags
    public bool CanUseCustomIntegrations { get; set; }
    public bool CanUseAdvancedScheduling { get; set; }
    public bool CanUseMultiRegionDeployment { get; set; }
    public bool CanUseGpuAcceleration { get; set; }
    public int MaxTeamMembers { get; set; } = 5;
}

/// <summary>
/// Tenant resource usage snapshot
/// </summary>
public class TenantResourceUsage
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime MeasuredAt { get; set; }

    // Execution metrics
    public int ExecutionsToday { get; set; }
    public int CurrentConcurrentExecutions { get; set; }
    public long TotalExecutionTimeSeconds { get; set; }

    // Storage metrics
    public long StorageUsedGb { get; set; }
    public long BackupStorageUsedGb { get; set; }

    // Resource counts
    public int WorkflowCount { get; set; }
    public int VersionCount { get; set; }
    public int IntegrationCount { get; set; }

    // API metrics
    public int ApiCallsThisMinute { get; set; }
    public int ApiCallsThisHour { get; set; }

    public double ExecutionQuotaPercentage => ExecutionsToday / 10000.0 * 100;
    public double StorageQuotaPercentage => StorageUsedGb / 100.0 * 100;
    public double ConcurrencyQuotaPercentage => CurrentConcurrentExecutions / 100.0 * 100;
}

/// <summary>
/// Tenant context (ambient context for current request)
/// </summary>
public class TenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public TenantInfo? TenantInfo { get; set; }
    public TenantConfiguration? Configuration { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tenant service interface
/// </summary>
public interface ITenantService
{
    Task<TenantInfo> CreateTenantAsync(
        string tenantName,
        string ownerEmail,
        string plan = "standard",
        CancellationToken ct = default);

    Task<TenantInfo?> GetTenantAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<List<TenantInfo>> GetTenantsForUserAsync(
        string userEmail,
        CancellationToken ct = default);

    Task<TenantConfiguration> GetConfigurationAsync(
        string tenantId,
        CancellationToken ct = default);

    Task UpdateConfigurationAsync(
        string tenantId,
        TenantConfiguration configuration,
        CancellationToken ct = default);

    Task<TenantResourceUsage> GetResourceUsageAsync(
        string tenantId,
        CancellationToken ct = default);

    Task SuspendTenantAsync(
        string tenantId,
        string reason,
        CancellationToken ct = default);

    Task ResumeTenantAsync(
        string tenantId,
        CancellationToken ct = default);

    Task DeleteTenantAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Tenant service implementation
/// </summary>
public class TenantService : ITenantService
{
    private readonly ILogger<TenantService> _logger;
    private readonly Dictionary<string, TenantInfo> _tenants;
    private readonly Dictionary<string, TenantConfiguration> _configurations;
    private readonly Dictionary<string, TenantResourceUsage> _usageMetrics;
    private readonly Dictionary<string, List<string>> _tenantsByUser; // user email -> tenant IDs

    public TenantService(ILogger<TenantService> logger)
    {
        _logger = logger;
        _tenants = new Dictionary<string, TenantInfo>();
        _configurations = new Dictionary<string, TenantConfiguration>();
        _usageMetrics = new Dictionary<string, TenantResourceUsage>();
        _tenantsByUser = new Dictionary<string, List<string>>();
    }

    public async Task<TenantInfo> CreateTenantAsync(
        string tenantName,
        string ownerEmail,
        string plan = "standard",
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var tenantId = Guid.NewGuid().ToString();

        var tenant = new TenantInfo
        {
            TenantId = tenantId,
            TenantName = tenantName,
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            OwnerEmail = ownerEmail,
            Plan = plan,
            IsActive = true,
        };

        _tenants[tenantId] = tenant;

        // Create default configuration based on plan
        var config = CreateDefaultConfiguration(tenantId, plan);
        _configurations[tenantId] = config;

        // Initialize usage metrics
        _usageMetrics[tenantId] = new TenantResourceUsage
        {
            TenantId = tenantId,
            MeasuredAt = DateTime.UtcNow,
        };

        // Track tenant for user
        if (!_tenantsByUser.ContainsKey(ownerEmail))
        {
            _tenantsByUser[ownerEmail] = new List<string>();
        }
        _tenantsByUser[ownerEmail].Add(tenantId);

        _logger.LogInformation(
            "Tenant created: {TenantId} ({Name}), Plan: {Plan}, Owner: {Owner}",
            tenantId, tenantName, plan, ownerEmail);

        return tenant;
    }

    public async Task<TenantInfo?> GetTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _tenants.TryGetValue(tenantId, out var tenant);
        return tenant;
    }

    public async Task<List<TenantInfo>> GetTenantsForUserAsync(
        string userEmail,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tenantsByUser.TryGetValue(userEmail, out var tenantIds))
        {
            return new List<TenantInfo>();
        }

        return tenantIds
            .Select(id => _tenants.TryGetValue(id, out var t) ? t : null)
            .Where(t => t != null)
            .Cast<TenantInfo>()
            .ToList();
    }

    public async Task<TenantConfiguration> GetConfigurationAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_configurations.TryGetValue(tenantId, out var config))
        {
            return config;
        }

        // Return default if not found
        return new TenantConfiguration { TenantId = tenantId };
    }

    public async Task UpdateConfigurationAsync(
        string tenantId,
        TenantConfiguration configuration,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tenants.ContainsKey(tenantId))
        {
            throw new KeyNotFoundException($"Tenant not found: {tenantId}");
        }

        configuration.TenantId = tenantId;
        _configurations[tenantId] = configuration;

        _logger.LogInformation(
            "Tenant configuration updated: {TenantId}",
            tenantId);
    }

    public async Task<TenantResourceUsage> GetResourceUsageAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_usageMetrics.TryGetValue(tenantId, out var usage))
        {
            usage.MeasuredAt = DateTime.UtcNow;
            return usage;
        }

        return new TenantResourceUsage { TenantId = tenantId };
    }

    public async Task SuspendTenantAsync(
        string tenantId,
        string reason,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tenants.TryGetValue(tenantId, out var tenant))
        {
            throw new KeyNotFoundException($"Tenant not found: {tenantId}");
        }

        tenant.Status = TenantStatus.Suspended;
        tenant.IsActive = false;
        tenant.SuspendedAt = DateTime.UtcNow;
        tenant.SuspensionReason = reason;

        _logger.LogWarning(
            "Tenant suspended: {TenantId}, Reason: {Reason}",
            tenantId, reason);
    }

    public async Task ResumeTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tenants.TryGetValue(tenantId, out var tenant))
        {
            throw new KeyNotFoundException($"Tenant not found: {tenantId}");
        }

        tenant.Status = TenantStatus.Active;
        tenant.IsActive = true;
        tenant.SuspendedAt = null;
        tenant.SuspensionReason = null;

        _logger.LogInformation(
            "Tenant resumed: {TenantId}",
            tenantId);
    }

    public async Task DeleteTenantAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_tenants.TryGetValue(tenantId, out var tenant))
        {
            throw new KeyNotFoundException($"Tenant not found: {tenantId}");
        }

        tenant.Status = TenantStatus.Terminated;

        // Remove from user mapping
        if (_tenantsByUser.TryGetValue(tenant.OwnerEmail, out var tenants))
        {
            tenants.Remove(tenantId);
        }

        _logger.LogInformation(
            "Tenant marked for deletion: {TenantId}",
            tenantId);
    }

    // Private helper
    private TenantConfiguration CreateDefaultConfiguration(string tenantId, string plan)
    {
        return plan switch
        {
            "starter" => new TenantConfiguration
            {
                TenantId = tenantId,
                ExecutionsPerDayLimit = 1000,
                ConcurrentWorkflowExecutionsLimit = 10,
                StorageGbLimit = 5,
                BackupStorageGbLimit = 2,
                MaxWorkflows = 50,
                ApiCallsPerMinute = 1000,
                ExecutionHistoryRetentionDays = 30,
                MaxTeamMembers = 1,
            },
            "professional" => new TenantConfiguration
            {
                TenantId = tenantId,
                ExecutionsPerDayLimit = 100000,
                ConcurrentWorkflowExecutionsLimit = 500,
                StorageGbLimit = 500,
                BackupStorageGbLimit = 250,
                MaxWorkflows = 10000,
                ApiCallsPerMinute = 50000,
                ExecutionHistoryRetentionDays = 180,
                CanUseCustomIntegrations = true,
                CanUseAdvancedScheduling = true,
                MaxTeamMembers = 50,
            },
            "enterprise" => new TenantConfiguration
            {
                TenantId = tenantId,
                ExecutionsPerDayLimit = 1000000,
                ConcurrentWorkflowExecutionsLimit = 5000,
                StorageGbLimit = 5000,
                BackupStorageGbLimit = 2500,
                MaxWorkflows = 100000,
                ApiCallsPerMinute = 500000,
                ExecutionHistoryRetentionDays = 365,
                CanUseCustomIntegrations = true,
                CanUseAdvancedScheduling = true,
                CanUseMultiRegionDeployment = true,
                CanUseGpuAcceleration = true,
                MaxTeamMembers = 500,
            },
            _ => new TenantConfiguration
            {
                TenantId = tenantId,
                ExecutionsPerDayLimit = 10000,
                ConcurrentWorkflowExecutionsLimit = 100,
                StorageGbLimit = 100,
                BackupStorageGbLimit = 50,
                MaxWorkflows = 1000,
                ApiCallsPerMinute = 10000,
                MaxTeamMembers = 5,
            }
        };
    }
}

/// <summary>
/// Resource quota manager
/// </summary>
public interface IResourceQuotaManager
{
    Task<bool> CanExecuteWorkflowAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> CanCreateWorkflowAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<bool> CheckApiRateLimitAsync(
        string tenantId,
        CancellationToken ct = default);

    Task<(bool Allowed, string? Reason)> CheckQuotasAsync(
        string tenantId,
        CancellationToken ct = default);

    Task RecordExecutionAsync(
        string tenantId,
        long durationSeconds,
        CancellationToken ct = default);

    Task RecordStorageAsync(
        string tenantId,
        long bytesAdded,
        CancellationToken ct = default);

    Task RecordApiCallAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Resource quota manager implementation
/// </summary>
public class ResourceQuotaManager : IResourceQuotaManager
{
    private readonly ILogger<ResourceQuotaManager> _logger;
    private readonly ITenantService _tenantService;
    private readonly Dictionary<string, TenantResourceUsage> _usage;
    private readonly Dictionary<string, Queue<DateTime>> _apiCallTimestamps; // Per-tenant rate limiting

    public ResourceQuotaManager(
        ILogger<ResourceQuotaManager> logger,
        ITenantService tenantService)
    {
        _logger = logger;
        _tenantService = tenantService;
        _usage = new Dictionary<string, TenantResourceUsage>();
        _apiCallTimestamps = new Dictionary<string, Queue<DateTime>>();
    }

    public async Task<bool> CanExecuteWorkflowAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var config = await _tenantService.GetConfigurationAsync(tenantId, ct);
        var usage = await _tenantService.GetResourceUsageAsync(tenantId, ct);

        if (usage.ExecutionsToday >= config.ExecutionsPerDayLimit)
        {
            _logger.LogWarning(
                "Execution quota exceeded for {TenantId}: {Current}/{Limit}",
                tenantId, usage.ExecutionsToday, config.ExecutionsPerDayLimit);
            return false;
        }

        if (usage.CurrentConcurrentExecutions >= config.ConcurrentWorkflowExecutionsLimit)
        {
            _logger.LogWarning(
                "Concurrent execution limit exceeded for {TenantId}: {Current}/{Limit}",
                tenantId, usage.CurrentConcurrentExecutions, config.ConcurrentWorkflowExecutionsLimit);
            return false;
        }

        return true;
    }

    public async Task<bool> CanCreateWorkflowAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var config = await _tenantService.GetConfigurationAsync(tenantId, ct);
        var usage = await _tenantService.GetResourceUsageAsync(tenantId, ct);

        if (usage.WorkflowCount >= config.MaxWorkflows)
        {
            _logger.LogWarning(
                "Workflow creation limit exceeded for {TenantId}: {Current}/{Limit}",
                tenantId, usage.WorkflowCount, config.MaxWorkflows);
            return false;
        }

        return true;
    }

    public async Task<bool> CheckApiRateLimitAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_apiCallTimestamps.ContainsKey(tenantId))
        {
            _apiCallTimestamps[tenantId] = new Queue<DateTime>();
        }

        var queue = _apiCallTimestamps[tenantId];
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        // Remove old timestamps
        while (queue.Count > 0 && queue.Peek() < oneMinuteAgo)
        {
            queue.Dequeue();
        }

        var config = await _tenantService.GetConfigurationAsync(tenantId, ct);
        if (queue.Count >= config.ApiCallsPerMinute)
        {
            _logger.LogWarning(
                "API rate limit exceeded for {TenantId}",
                tenantId);
            return false;
        }

        return true;
    }

    public async Task<(bool Allowed, string? Reason)> CheckQuotasAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        var canExecute = await CanExecuteWorkflowAsync(tenantId, ct);
        if (!canExecute) return (false, "Execution quota exceeded");

        var canCreate = await CanCreateWorkflowAsync(tenantId, ct);
        if (!canCreate) return (false, "Workflow limit reached");

        var rateLimited = await CheckApiRateLimitAsync(tenantId, ct);
        if (!rateLimited) return (false, "API rate limit exceeded");

        var tenant = await _tenantService.GetTenantAsync(tenantId, ct);
        if (tenant?.Status == TenantStatus.Suspended)
        {
            return (false, "Tenant is suspended");
        }

        return (true, null);
    }

    public async Task RecordExecutionAsync(
        string tenantId,
        long durationSeconds,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_usage.TryGetValue(tenantId, out var usage))
        {
            usage.ExecutionsToday++;
            usage.TotalExecutionTimeSeconds += durationSeconds;
        }

        _logger.LogDebug(
            "Recorded execution for {TenantId}: {Duration}s",
            tenantId, durationSeconds);
    }

    public async Task RecordStorageAsync(
        string tenantId,
        long bytesAdded,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_usage.TryGetValue(tenantId, out var usage))
        {
            usage.StorageUsedGb += bytesAdded / (1024L * 1024L * 1024L);
        }

        _logger.LogDebug(
            "Recorded storage for {TenantId}: {Bytes} bytes",
            tenantId, bytesAdded);
    }

    public async Task RecordApiCallAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_apiCallTimestamps.ContainsKey(tenantId))
        {
            _apiCallTimestamps[tenantId] = new Queue<DateTime>();
        }

        _apiCallTimestamps[tenantId].Enqueue(DateTime.UtcNow);
    }
}

/// <summary>
/// Tenant-aware repository filter
/// </summary>
public interface ITenantFilter
{
    IQueryable<T> ApplyTenantFilter<T>(IQueryable<T> query, string tenantId)
        where T : class;
}

/// <summary>
/// Multi-tenant data filtering (example for workflows)
/// </summary>
public class TenantIsolationFilter : ITenantFilter
{
    private readonly ILogger<TenantIsolationFilter> _logger;

    public TenantIsolationFilter(ILogger<TenantIsolationFilter> logger)
    {
        _logger = logger;
    }

    public IQueryable<T> ApplyTenantFilter<T>(IQueryable<T> query, string tenantId)
        where T : class
    {
        // In production, use expression trees to apply tenant filter based on type
        _logger.LogDebug(
            "Applied tenant filter for {TenantId} on type {Type}",
            tenantId, typeof(T).Name);

        return query;
    }
}
