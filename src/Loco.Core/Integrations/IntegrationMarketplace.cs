// Phase 6: Integration Marketplace
// Centralized marketplace for workflow extensions, version management, and installation
// Enables ecosystem of community and partner integrations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Integrations;

/// <summary>
/// Integration category
/// </summary>
public enum IntegrationCategory
{
    Messaging = 0,
    CloudStorage = 1,
    CRM = 2,
    ERP = 3,
    Analytics = 4,
    Monitoring = 5,
    PaymentGateway = 6,
    Authentication = 7,
    Scheduling = 8,
    Custom = 9,
}

/// <summary>
/// Integration status in marketplace
/// </summary>
public enum IntegrationStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Published = 3,
    Deprecated = 4,
    Removed = 5,
}

/// <summary>
/// Integration listing in marketplace
/// </summary>
public class IntegrationListing
{
    public string IntegrationId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public IntegrationCategory Category { get; set; }
    public IntegrationStatus Status { get; set; } = IntegrationStatus.Draft;

    // Metadata
    public string Version { get; set; } = "1.0.0";
    public string IconUrl { get; set; } = string.Empty;
    public string DocumentationUrl { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Screenshots { get; set; } = new();

    // Ratings
    public double AverageRating { get; set; } // 1.0-5.0
    public int RatingCount { get; set; }
    public int DownloadCount { get; set; }

    // Dates
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    // SLA
    public bool IsPremium { get; set; }
    public bool HasSla { get; set; }
    public double AvailabilityPercentage { get; set; } = 99.9;
    public int SupportResponseHoursSlot { get; set; } = 24;
}

/// <summary>
/// Integration installation record
/// </summary>
public class IntegrationInstallation
{
    public string InstallationId { get; set; } = Guid.NewGuid().ToString();
    public string IntegrationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    // Version management
    public string InstalledVersion { get; set; } = string.Empty;
    public string LatestAvailableVersion { get; set; } = string.Empty;
    public bool AutoUpdateEnabled { get; set; } = true;

    // Configuration
    public Dictionary<string, object> Configuration { get; set; } = new();
    public Dictionary<string, string> SecretVariables { get; set; } = new();

    // Status
    public bool IsEnabled { get; set; } = true;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public string? LastHealthCheckStatus { get; set; }

    // Usage
    public int ExecutionCount { get; set; }
    public long LastExecutionTime { get; set; } // milliseconds
    public double SuccessRate { get; set; }
}

/// <summary>
/// Integration version
/// </summary>
public class IntegrationVersion
{
    public string VersionId { get; set; } = Guid.NewGuid().ToString();
    public string IntegrationId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty; // semantic versioning

    // Release details
    public string ReleaseNotes { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;

    // Package info
    public string PackageUrl { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty; // SHA256
    public long PackageSizeBytes { get; set; }

    // Compatibility
    public string MinimumLocoVersion { get; set; } = "1.0.0";
    public List<string> CompatiblePlatforms { get; set; } = new() { "windows", "linux", "macos" };
    public List<string> DependencyIds { get; set; } = new();

    // Testing
    public bool IsStable { get; set; }
    public List<string> TestCoverage { get; set; } = new();
    public double UnitTestCoverage { get; set; }
}

/// <summary>
/// Integration review
/// </summary>
public class IntegrationReview
{
    public string ReviewId { get; set; } = Guid.NewGuid().ToString();
    public string IntegrationId { get; set; } = string.Empty;
    public string ReviewerId { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;

    public double Rating { get; set; } // 1.0-5.0
    public string ReviewText { get; set; } = string.Empty;
    public List<string> VerifiedBenefits { get; set; } = new();
    public List<string> KnownIssues { get; set; } = new();

    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int HelpfulCount { get; set; }
}

/// <summary>
/// Integration marketplace interface
/// </summary>
public interface IIntegrationMarketplace
{
    // Listing operations
    Task<IntegrationListing> SubmitIntegrationAsync(
        IntegrationListing listing,
        CancellationToken ct = default);

    Task<IntegrationListing?> GetIntegrationAsync(
        string integrationId,
        CancellationToken ct = default);

    Task<List<IntegrationListing>> SearchIntegrationsAsync(
        string query,
        IntegrationCategory? category = null,
        int limit = 50,
        CancellationToken ct = default);

    Task<List<IntegrationListing>> GetFeaturedIntegrationsAsync(
        int limit = 10,
        CancellationToken ct = default);

    Task<List<IntegrationListing>> GetCategoryIntegrationsAsync(
        IntegrationCategory category,
        int limit = 50,
        CancellationToken ct = default);

    // Version management
    Task<IntegrationVersion> PublishVersionAsync(
        string integrationId,
        IntegrationVersion version,
        CancellationToken ct = default);

    Task<List<IntegrationVersion>> GetVersionsAsync(
        string integrationId,
        CancellationToken ct = default);

    Task<IntegrationVersion?> GetLatestVersionAsync(
        string integrationId,
        CancellationToken ct = default);

    // Installation
    Task<IntegrationInstallation> InstallIntegrationAsync(
        string tenantId,
        string integrationId,
        Dictionary<string, object>? configuration = null,
        CancellationToken ct = default);

    Task<List<IntegrationInstallation>> GetInstalledIntegrationsAsync(
        string tenantId,
        CancellationToken ct = default);

    Task UpdateInstallationAsync(
        string installationId,
        Dictionary<string, object> configuration,
        CancellationToken ct = default);

    Task UninstallIntegrationAsync(
        string installationId,
        CancellationToken ct = default);

    Task<bool> CheckForUpdatesAsync(
        string installationId,
        CancellationToken ct = default);

    Task UpdateIntegrationAsync(
        string installationId,
        CancellationToken ct = default);

    // Reviews
    Task<IntegrationReview> SubmitReviewAsync(
        string integrationId,
        IntegrationReview review,
        CancellationToken ct = default);

    Task<List<IntegrationReview>> GetReviewsAsync(
        string integrationId,
        CancellationToken ct = default);

    Task<double> GetAverageRatingAsync(
        string integrationId,
        CancellationToken ct = default);

    // Statistics
    Task<Dictionary<string, int>> GetMarketplaceStatisticsAsync(
        CancellationToken ct = default);

    Task<(int Total, int Weekly, int Monthly)> GetDownloadStatsAsync(
        string integrationId,
        CancellationToken ct = default);
}

/// <summary>
/// Integration marketplace implementation
/// </summary>
public class IntegrationMarketplace : IIntegrationMarketplace
{
    private readonly ILogger<IntegrationMarketplace> _logger;
    private readonly Dictionary<string, IntegrationListing> _listings;
    private readonly Dictionary<string, List<IntegrationVersion>> _versions;
    private readonly Dictionary<string, IntegrationInstallation> _installations;
    private readonly Dictionary<string, List<IntegrationReview>> _reviews;
    private readonly Dictionary<string, List<DateTime>> _downloadHistory; // For statistics

    public IntegrationMarketplace(ILogger<IntegrationMarketplace> logger)
    {
        _logger = logger;
        _listings = new Dictionary<string, IntegrationListing>();
        _versions = new Dictionary<string, List<IntegrationVersion>>();
        _installations = new Dictionary<string, IntegrationInstallation>();
        _reviews = new Dictionary<string, List<IntegrationReview>>();
        _downloadHistory = new Dictionary<string, List<DateTime>>();
    }

    public async Task<IntegrationListing> SubmitIntegrationAsync(
        IntegrationListing listing,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        listing.Status = IntegrationStatus.Submitted;
        listing.CreatedAt = DateTime.UtcNow;
        listing.UpdatedAt = DateTime.UtcNow;

        _listings[listing.IntegrationId] = listing;

        _logger.LogInformation(
            "Integration submitted: {IntegrationId} ({Name}), Publisher: {Publisher}",
            listing.IntegrationId, listing.Name, listing.Publisher);

        return listing;
    }

    public async Task<IntegrationListing?> GetIntegrationAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        _listings.TryGetValue(integrationId, out var listing);
        return listing;
    }

    public async Task<List<IntegrationListing>> SearchIntegrationsAsync(
        string query,
        IntegrationCategory? category = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var results = _listings.Values
            .Where(l => l.Status == IntegrationStatus.Published)
            .Where(l => l.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       l.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       l.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Where(l => category == null || l.Category == category)
            .OrderByDescending(l => l.AverageRating)
            .ThenByDescending(l => l.DownloadCount)
            .Take(limit)
            .ToList();

        return results;
    }

    public async Task<List<IntegrationListing>> GetFeaturedIntegrationsAsync(
        int limit = 10,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _listings.Values
            .Where(l => l.Status == IntegrationStatus.Published && l.AverageRating >= 4.0)
            .OrderByDescending(l => l.DownloadCount)
            .Take(limit)
            .ToList();
    }

    public async Task<List<IntegrationListing>> GetCategoryIntegrationsAsync(
        IntegrationCategory category,
        int limit = 50,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _listings.Values
            .Where(l => l.Status == IntegrationStatus.Published && l.Category == category)
            .OrderByDescending(l => l.AverageRating)
            .Take(limit)
            .ToList();
    }

    public async Task<IntegrationVersion> PublishVersionAsync(
        string integrationId,
        IntegrationVersion version,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        version.IntegrationId = integrationId;
        version.ReleasedAt = DateTime.UtcNow;

        if (!_versions.ContainsKey(integrationId))
        {
            _versions[integrationId] = new List<IntegrationVersion>();
        }

        _versions[integrationId].Add(version);

        // Update listing
        if (_listings.TryGetValue(integrationId, out var listing))
        {
            listing.Version = version.Version;
            listing.UpdatedAt = DateTime.UtcNow;
            if (listing.Status == IntegrationStatus.Approved)
            {
                listing.Status = IntegrationStatus.Published;
                listing.PublishedAt = DateTime.UtcNow;
            }
        }

        _logger.LogInformation(
            "Integration version published: {IntegrationId}, Version: {Version}",
            integrationId, version.Version);

        return version;
    }

    public async Task<List<IntegrationVersion>> GetVersionsAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_versions.TryGetValue(integrationId, out var versions))
        {
            return versions.OrderByDescending(v => v.ReleasedAt).ToList();
        }

        return new List<IntegrationVersion>();
    }

    public async Task<IntegrationVersion?> GetLatestVersionAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_versions.TryGetValue(integrationId, out var versions))
        {
            return versions.OrderByDescending(v => v.ReleasedAt).FirstOrDefault();
        }

        return null;
    }

    public async Task<IntegrationInstallation> InstallIntegrationAsync(
        string tenantId,
        string integrationId,
        Dictionary<string, object>? configuration = null,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var listing = await GetIntegrationAsync(integrationId, ct);
        if (listing == null || listing.Status != IntegrationStatus.Published)
        {
            throw new InvalidOperationException($"Integration not available: {integrationId}");
        }

        var latestVersion = await GetLatestVersionAsync(integrationId, ct);
        if (latestVersion == null)
        {
            throw new InvalidOperationException($"No version available for integration: {integrationId}");
        }

        var installation = new IntegrationInstallation
        {
            IntegrationId = integrationId,
            TenantId = tenantId,
            InstalledVersion = latestVersion.Version,
            LatestAvailableVersion = latestVersion.Version,
            Configuration = configuration ?? new Dictionary<string, object>(),
            InstalledAt = DateTime.UtcNow,
        };

        _installations[installation.InstallationId] = installation;

        // Record download
        if (!_downloadHistory.ContainsKey(integrationId))
        {
            _downloadHistory[integrationId] = new List<DateTime>();
        }
        _downloadHistory[integrationId].Add(DateTime.UtcNow);

        // Update listing
        if (_listings.TryGetValue(integrationId, out var intListing))
        {
            intListing.DownloadCount++;
        }

        _logger.LogInformation(
            "Integration installed: {InstallationId}, Integration: {IntegrationId}, Tenant: {TenantId}",
            installation.InstallationId, integrationId, tenantId);

        return installation;
    }

    public async Task<List<IntegrationInstallation>> GetInstalledIntegrationsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return _installations.Values
            .Where(i => i.TenantId == tenantId && i.IsEnabled)
            .ToList();
    }

    public async Task UpdateInstallationAsync(
        string installationId,
        Dictionary<string, object> configuration,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_installations.TryGetValue(installationId, out var installation))
        {
            throw new KeyNotFoundException($"Installation not found: {installationId}");
        }

        installation.Configuration = configuration;
        installation.LastUpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Installation updated: {InstallationId}",
            installationId);
    }

    public async Task UninstallIntegrationAsync(
        string installationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_installations.TryGetValue(installationId, out var installation))
        {
            throw new KeyNotFoundException($"Installation not found: {installationId}");
        }

        installation.IsEnabled = false;

        _logger.LogInformation(
            "Integration uninstalled: {InstallationId}",
            installationId);
    }

    public async Task<bool> CheckForUpdatesAsync(
        string installationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_installations.TryGetValue(installationId, out var installation))
        {
            return false;
        }

        var latestVersion = await GetLatestVersionAsync(installation.IntegrationId, ct);
        if (latestVersion == null)
        {
            return false;
        }

        var hasUpdate = latestVersion.Version != installation.InstalledVersion;
        if (hasUpdate)
        {
            installation.LatestAvailableVersion = latestVersion.Version;
        }

        return hasUpdate;
    }

    public async Task UpdateIntegrationAsync(
        string installationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_installations.TryGetValue(installationId, out var installation))
        {
            throw new KeyNotFoundException($"Installation not found: {installationId}");
        }

        var latestVersion = await GetLatestVersionAsync(installation.IntegrationId, ct);
        if (latestVersion == null)
        {
            throw new InvalidOperationException("No version available for update");
        }

        installation.InstalledVersion = latestVersion.Version;
        installation.LatestAvailableVersion = latestVersion.Version;
        installation.LastUpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Integration updated: {InstallationId}, Version: {Version}",
            installationId, latestVersion.Version);
    }

    public async Task<IntegrationReview> SubmitReviewAsync(
        string integrationId,
        IntegrationReview review,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        review.IntegrationId = integrationId;
        review.CreatedAt = DateTime.UtcNow;

        if (!_reviews.ContainsKey(integrationId))
        {
            _reviews[integrationId] = new List<IntegrationReview>();
        }

        _reviews[integrationId].Add(review);

        // Update listing rating
        if (_listings.TryGetValue(integrationId, out var listing))
        {
            var allReviews = _reviews[integrationId];
            listing.AverageRating = allReviews.Average(r => r.Rating);
            listing.RatingCount = allReviews.Count;
        }

        _logger.LogInformation(
            "Review submitted for {IntegrationId}: {Rating} stars",
            integrationId, review.Rating);

        return review;
    }

    public async Task<List<IntegrationReview>> GetReviewsAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_reviews.TryGetValue(integrationId, out var reviews))
        {
            return reviews.OrderByDescending(r => r.CreatedAt).ToList();
        }

        return new List<IntegrationReview>();
    }

    public async Task<double> GetAverageRatingAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_reviews.TryGetValue(integrationId, out var reviews) && reviews.Count > 0)
        {
            return reviews.Average(r => r.Rating);
        }

        return 0.0;
    }

    public async Task<Dictionary<string, int>> GetMarketplaceStatisticsAsync(
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        return new Dictionary<string, int>
        {
            ["total_integrations"] = _listings.Count(l => l.Value.Status == IntegrationStatus.Published),
            ["total_installations"] = _installations.Count(i => i.Value.IsEnabled),
            ["total_versions"] = _versions.Values.Sum(v => v.Count),
            ["total_reviews"] = _reviews.Values.Sum(r => r.Count),
            ["total_downloads"] = _downloadHistory.Values.Sum(d => d.Count),
        };
    }

    public async Task<(int Total, int Weekly, int Monthly)> GetDownloadStatsAsync(
        string integrationId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (!_downloadHistory.TryGetValue(integrationId, out var history))
        {
            return (0, 0, 0);
        }

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        var total = history.Count;
        var weekly = history.Count(d => d >= weekAgo);
        var monthly = history.Count(d => d >= monthAgo);

        return (total, weekly, monthly);
    }
}
