using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loco.Core.MultiTenancy
{
    public interface IMultiTenancyService
    {
        Task<Tenant> GetCurrentTenantAsync();
        Task<Tenant> GetTenantByIdAsync(Guid tenantId);
        Task<Tenant> GetTenantByIdentifierAsync(string identifier);
        Task<Tenant> CreateTenantAsync(TenantCreateRequest request);
        Task UpdateTenantAsync(Guid tenantId, TenantUpdateRequest request);
        Task<bool> DeleteTenantAsync(Guid tenantId);
        Task<IEnumerable<Tenant>> GetAllTenantsAsync();
        Task<TenantStatistics> GetTenantStatisticsAsync(Guid tenantId);
        Task<bool> ValidateTenantAccessAsync(Guid tenantId, string userId);
        void SetCurrentTenant(Tenant tenant);
    }

    public class MultiTenancyService : IMultiTenancyService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITenantStore _tenantStore;
        private readonly IDistributedCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MultiTenancyService> _logger;
        private readonly ITenantResolver _tenantResolver;
        private readonly AsyncLocal<Tenant> _currentTenant;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _tenantLocks;

        public MultiTenancyService(
            IHttpContextAccessor httpContextAccessor,
            ITenantStore tenantStore,
            IDistributedCache cache,
            IConfiguration configuration,
            ILogger<MultiTenancyService> logger,
            ITenantResolver tenantResolver)
        {
            _httpContextAccessor = httpContextAccessor;
            _tenantStore = tenantStore;
            _cache = cache;
            _configuration = configuration;
            _logger = logger;
            _tenantResolver = tenantResolver;
            _currentTenant = new AsyncLocal<Tenant>();
            _tenantLocks = new ConcurrentDictionary<Guid, SemaphoreSlim>();
        }

        public async Task<Tenant> GetCurrentTenantAsync()
        {
            // Check AsyncLocal first
            if (_currentTenant.Value != null)
            {
                return _currentTenant.Value;
            }

            // Try to resolve from HTTP context
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var tenant = await _tenantResolver.ResolveAsync(httpContext);
                if (tenant != null)
                {
                    _currentTenant.Value = tenant;
                    return tenant;
                }
            }

            // Fallback to default tenant
            var defaultTenantId = _configuration["MultiTenancy:DefaultTenantId"];
            if (!string.IsNullOrEmpty(defaultTenantId) && Guid.TryParse(defaultTenantId, out var tenantId))
            {
                return await GetTenantByIdAsync(tenantId);
            }

            throw new TenantNotFoundException("Unable to determine current tenant");
        }

        public async Task<Tenant> GetTenantByIdAsync(Guid tenantId)
        {
            // Check cache first
            var cacheKey = $"tenant:{tenantId}";
            var cachedTenant = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedTenant))
            {
                var tenant = System.Text.Json.JsonSerializer.Deserialize<Tenant>(cachedTenant);
                if (tenant != null && tenant.IsActive)
                {
                    return tenant;
                }
            }

            // Load from store
            var storedTenant = await _tenantStore.GetByIdAsync(tenantId);
            if (storedTenant == null)
            {
                throw new TenantNotFoundException($"Tenant {tenantId} not found");
            }

            // Cache the tenant
            await _cache.SetStringAsync(cacheKey, 
                System.Text.Json.JsonSerializer.Serialize(storedTenant),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(15)
                });

            return storedTenant;
        }

        public async Task<Tenant> GetTenantByIdentifierAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Tenant identifier cannot be empty", nameof(identifier));
            }

            // Check cache
            var cacheKey = $"tenant:identifier:{identifier}";
            var cachedId = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedId) && Guid.TryParse(cachedId, out var tenantId))
            {
                return await GetTenantByIdAsync(tenantId);
            }

            // Load from store
            var tenant = await _tenantStore.GetByIdentifierAsync(identifier);
            if (tenant == null)
            {
                throw new TenantNotFoundException($"Tenant with identifier '{identifier}' not found");
            }

            // Cache the mapping
            await _cache.SetStringAsync(cacheKey, tenant.Id.ToString(),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(15)
                });

            return tenant;
        }

        public async Task<Tenant> CreateTenantAsync(TenantCreateRequest request)
        {
            // Validate request
            await ValidateTenantRequestAsync(request);

            // Check for duplicate identifier
            var existing = await _tenantStore.GetByIdentifierAsync(request.Identifier);
            if (existing != null)
            {
                throw new TenantAlreadyExistsException($"Tenant with identifier '{request.Identifier}' already exists");
            }

            // Create tenant
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Identifier = request.Identifier,
                ConnectionString = await GenerateConnectionStringAsync(request.Identifier),
                Features = request.Features ?? new List<string>(),
                Settings = request.Settings ?? new Dictionary<string, string>(),
                StorageQuotaGB = request.StorageQuotaGB ?? 10,
                UserQuota = request.UserQuota ?? 100,
                ApiRateLimit = request.ApiRateLimit ?? 1000,
                IsActive = true,
                IsTrial = request.IsTrial,
                TrialEndsAt = request.IsTrial ? DateTime.UtcNow.AddDays(30) : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetCurrentUserId()
            };

            // Initialize tenant infrastructure
            await InitializeTenantInfrastructureAsync(tenant);

            // Save tenant
            await _tenantStore.CreateAsync(tenant);

            // Create audit log
            await CreateAuditLogAsync("TenantCreated", tenant.Id, tenant);

            _logger.LogInformation("Tenant created: {TenantId} ({TenantIdentifier})", 
                tenant.Id, tenant.Identifier);

            return tenant;
        }

        public async Task UpdateTenantAsync(Guid tenantId, TenantUpdateRequest request)
        {
            var semaphore = _tenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var tenant = await GetTenantByIdAsync(tenantId);
                
                // Update properties
                if (!string.IsNullOrEmpty(request.Name))
                    tenant.Name = request.Name;
                
                if (request.Features != null)
                    tenant.Features = request.Features;
                
                if (request.Settings != null)
                    tenant.Settings = request.Settings;
                
                if (request.StorageQuotaGB.HasValue)
                    tenant.StorageQuotaGB = request.StorageQuotaGB.Value;
                
                if (request.UserQuota.HasValue)
                    tenant.UserQuota = request.UserQuota.Value;
                
                if (request.ApiRateLimit.HasValue)
                    tenant.ApiRateLimit = request.ApiRateLimit.Value;
                
                if (request.IsActive.HasValue)
                    tenant.IsActive = request.IsActive.Value;

                tenant.UpdatedAt = DateTime.UtcNow;
                tenant.UpdatedBy = GetCurrentUserId();

                // Update tenant
                await _tenantStore.UpdateAsync(tenant);

                // Invalidate cache
                await InvalidateTenantCacheAsync(tenantId);

                // Create audit log
                await CreateAuditLogAsync("TenantUpdated", tenantId, request);

                _logger.LogInformation("Tenant updated: {TenantId}", tenantId);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<bool> DeleteTenantAsync(Guid tenantId)
        {
            var semaphore = _tenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                var tenant = await GetTenantByIdAsync(tenantId);
                
                // Soft delete
                tenant.IsActive = false;
                tenant.DeletedAt = DateTime.UtcNow;
                tenant.DeletedBy = GetCurrentUserId();

                await _tenantStore.UpdateAsync(tenant);

                // Clean up tenant infrastructure
                await CleanupTenantInfrastructureAsync(tenant);

                // Invalidate cache
                await InvalidateTenantCacheAsync(tenantId);

                // Create audit log
                await CreateAuditLogAsync("TenantDeleted", tenantId, null);

                _logger.LogInformation("Tenant deleted: {TenantId}", tenantId);

                return true;
            }
            finally
            {
                semaphore.Release();
                _tenantLocks.TryRemove(tenantId, out _);
            }
        }

        public async Task<IEnumerable<Tenant>> GetAllTenantsAsync()
        {
            return await _tenantStore.GetAllAsync();
        }

        public async Task<TenantStatistics> GetTenantStatisticsAsync(Guid tenantId)
        {
            var tenant = await GetTenantByIdAsync(tenantId);
            
            return new TenantStatistics
            {
                TenantId = tenantId,
                UserCount = await _tenantStore.GetUserCountAsync(tenantId),
                StorageUsedGB = await _tenantStore.GetStorageUsageAsync(tenantId),
                ApiCallsToday = await _tenantStore.GetApiCallCountAsync(tenantId, DateTime.UtcNow.Date),
                LastActivityAt = await _tenantStore.GetLastActivityAsync(tenantId),
                IsOverQuota = await CheckQuotaExceededAsync(tenant)
            };
        }

        public async Task<bool> ValidateTenantAccessAsync(Guid tenantId, string userId)
        {
            var tenant = await GetTenantByIdAsync(tenantId);
            if (!tenant.IsActive)
            {
                return false;
            }

            return await _tenantStore.ValidateUserAccessAsync(tenantId, userId);
        }

        public void SetCurrentTenant(Tenant tenant)
        {
            _currentTenant.Value = tenant;
        }

        private async Task ValidateTenantRequestAsync(TenantCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Tenant name is required");

            if (string.IsNullOrWhiteSpace(request.Identifier))
                throw new ArgumentException("Tenant identifier is required");

            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Identifier, @"^[a-z0-9-]+$"))
                throw new ArgumentException("Tenant identifier must contain only lowercase letters, numbers, and hyphens");

            await Task.CompletedTask;
        }

        private async Task<string> GenerateConnectionStringAsync(string identifier)
        {
            var template = _configuration["MultiTenancy:ConnectionStringTemplate"];
            return template?.Replace("{TenantIdentifier}", identifier) 
                ?? $"Server=localhost;Database=loco_{identifier};Trusted_Connection=true;";
        }

        private async Task InitializeTenantInfrastructureAsync(Tenant tenant)
        {
            // Create database
            await CreateTenantDatabaseAsync(tenant);

            // Initialize storage
            await InitializeTenantStorageAsync(tenant);

            // Set up default data
            await SeedTenantDataAsync(tenant);
        }

        private async Task CreateTenantDatabaseAsync(Tenant tenant)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseSqlServer(tenant.ConnectionString);

            using var context = new TenantDbContext(optionsBuilder.Options);
            await context.Database.MigrateAsync();
            
            _logger.LogInformation("Database created for tenant {TenantId}", tenant.Id);
        }

        private async Task InitializeTenantStorageAsync(Tenant tenant)
        {
            // Initialize blob storage, file system, etc.
            await Task.CompletedTask;
        }

        private async Task SeedTenantDataAsync(Tenant tenant)
        {
            // Add default roles, settings, etc.
            await Task.CompletedTask;
        }

        private async Task CleanupTenantInfrastructureAsync(Tenant tenant)
        {
            // Archive data, clean up resources, etc.
            await Task.CompletedTask;
        }

        private async Task InvalidateTenantCacheAsync(Guid tenantId)
        {
            var cacheKey = $"tenant:{tenantId}";
            await _cache.RemoveAsync(cacheKey);
        }

        private async Task<bool> CheckQuotaExceededAsync(Tenant tenant)
        {
            var stats = await GetTenantStatisticsAsync(tenant.Id);
            return stats.StorageUsedGB > tenant.StorageQuotaGB ||
                   stats.UserCount > tenant.UserQuota ||
                   stats.ApiCallsToday > tenant.ApiRateLimit;
        }

        private string GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? "system";
        }

        private async Task CreateAuditLogAsync(string action, Guid tenantId, object data)
        {
            // Implement audit logging
            await Task.CompletedTask;
        }
    }

    // Tenant resolver strategies
    public interface ITenantResolver
    {
        Task<Tenant> ResolveAsync(HttpContext context);
    }

    public class CompositeTenantResolver : ITenantResolver
    {
        private readonly IEnumerable<ITenantResolutionStrategy> _strategies;
        private readonly ILogger<CompositeTenantResolver> _logger;

        public CompositeTenantResolver(
            IEnumerable<ITenantResolutionStrategy> strategies,
            ILogger<CompositeTenantResolver> logger)
        {
            _strategies = strategies.OrderBy(s => s.Priority);
            _logger = logger;
        }

        public async Task<Tenant> ResolveAsync(HttpContext context)
        {
            foreach (var strategy in _strategies)
            {
                var identifier = await strategy.GetTenantIdentifierAsync(context);
                if (!string.IsNullOrEmpty(identifier))
                {
                    _logger.LogDebug("Tenant identifier '{Identifier}' resolved using {Strategy}", 
                        identifier, strategy.GetType().Name);
                    
                    var service = context.RequestServices.GetRequiredService<IMultiTenancyService>();
                    return await service.GetTenantByIdentifierAsync(identifier);
                }
            }

            return null;
        }
    }

    public interface ITenantResolutionStrategy
    {
        int Priority { get; }
        Task<string> GetTenantIdentifierAsync(HttpContext context);
    }

    public class HeaderTenantResolutionStrategy : ITenantResolutionStrategy
    {
        public int Priority => 1;

        public Task<string> GetTenantIdentifierAsync(HttpContext context)
        {
            context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId);
            return Task.FromResult(tenantId.FirstOrDefault());
        }
    }

    public class SubdomainTenantResolutionStrategy : ITenantResolutionStrategy
    {
        public int Priority => 2;

        public Task<string> GetTenantIdentifierAsync(HttpContext context)
        {
            var host = context.Request.Host.Host;
            var subdomain = host.Split('.').FirstOrDefault();
            
            if (subdomain != "www" && subdomain != "api")
            {
                return Task.FromResult(subdomain);
            }

            return Task.FromResult<string>(null);
        }
    }

    public class QueryStringTenantResolutionStrategy : ITenantResolutionStrategy
    {
        public int Priority => 3;

        public Task<string> GetTenantIdentifierAsync(HttpContext context)
        {
            return Task.FromResult(context.Request.Query["tenant"].FirstOrDefault());
        }
    }

    public class ClaimsTenantResolutionStrategy : ITenantResolutionStrategy
    {
        public int Priority => 4;

        public Task<string> GetTenantIdentifierAsync(HttpContext context)
        {
            var tenantClaim = context.User?.FindFirst("tenant_id");
            return Task.FromResult(tenantClaim?.Value);
        }
    }

    // Models
    public class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Identifier { get; set; }
        public string ConnectionString { get; set; }
        public List<string> Features { get; set; }
        public Dictionary<string, string> Settings { get; set; }
        public int StorageQuotaGB { get; set; }
        public int UserQuota { get; set; }
        public int ApiRateLimit { get; set; }
        public bool IsActive { get; set; }
        public bool IsTrial { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string DeletedBy { get; set; }
    }

    public class TenantCreateRequest
    {
        public string Name { get; set; }
        public string Identifier { get; set; }
        public List<string> Features { get; set; }
        public Dictionary<string, string> Settings { get; set; }
        public int? StorageQuotaGB { get; set; }
        public int? UserQuota { get; set; }
        public int? ApiRateLimit { get; set; }
        public bool IsTrial { get; set; }
    }

    public class TenantUpdateRequest
    {
        public string Name { get; set; }
        public List<string> Features { get; set; }
        public Dictionary<string, string> Settings { get; set; }
        public int? StorageQuotaGB { get; set; }
        public int? UserQuota { get; set; }
        public int? ApiRateLimit { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TenantStatistics
    {
        public Guid TenantId { get; set; }
        public int UserCount { get; set; }
        public double StorageUsedGB { get; set; }
        public int ApiCallsToday { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public bool IsOverQuota { get; set; }
    }

    // Exceptions
    public class TenantNotFoundException : Exception
    {
        public TenantNotFoundException(string message) : base(message) { }
    }

    public class TenantAlreadyExistsException : Exception
    {
        public TenantAlreadyExistsException(string message) : base(message) { }
    }

    // Interfaces
    public interface ITenantStore
    {
        Task<Tenant> GetByIdAsync(Guid tenantId);
        Task<Tenant> GetByIdentifierAsync(string identifier);
        Task<IEnumerable<Tenant>> GetAllAsync();
        Task CreateAsync(Tenant tenant);
        Task UpdateAsync(Tenant tenant);
        Task<int> GetUserCountAsync(Guid tenantId);
        Task<double> GetStorageUsageAsync(Guid tenantId);
        Task<int> GetApiCallCountAsync(Guid tenantId, DateTime date);
        Task<DateTime?> GetLastActivityAsync(Guid tenantId);
        Task<bool> ValidateUserAccessAsync(Guid tenantId, string userId);
    }

    // DbContext
    public class TenantDbContext : DbContext
    {
        public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure tenant-specific entities
            base.OnModelCreating(modelBuilder);
        }
    }
}