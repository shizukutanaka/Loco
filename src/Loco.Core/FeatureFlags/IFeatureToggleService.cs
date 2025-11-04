namespace Loco.Core.FeatureFlags;

/// <summary>
/// Feature toggle service interface for feature flag management
/// </summary>
public interface IFeatureToggleService
{
    /// <summary>
    /// Checks if a feature is enabled
    /// </summary>
    Task<bool> IsEnabledAsync(string featureKey, FeatureContext? context = null);

    /// <summary>
    /// Gets feature flag details
    /// </summary>
    Task<FeatureFlag?> GetFeatureFlagAsync(string featureKey);

    /// <summary>
    /// Gets all feature flags
    /// </summary>
    Task<IEnumerable<FeatureFlag>> GetAllFeaturesAsync();

    /// <summary>
    /// Creates a new feature flag
    /// </summary>
    Task<FeatureFlag> CreateFeatureFlagAsync(FeatureFlag flag);

    /// <summary>
    /// Updates a feature flag
    /// </summary>
    Task<bool> UpdateFeatureFlagAsync(string featureKey, FeatureFlag flag);

    /// <summary>
    /// Deletes a feature flag
    /// </summary>
    Task<bool> DeleteFeatureFlagAsync(string featureKey);

    /// <summary>
    /// Enables a feature flag
    /// </summary>
    Task<bool> EnableFeatureAsync(string featureKey);

    /// <summary>
    /// Disables a feature flag
    /// </summary>
    Task<bool> DisableFeatureAsync(string featureKey);

    /// <summary>
    /// Gets feature metrics
    /// </summary>
    Task<FeatureFlagMetrics> GetMetricsAsync(string featureKey);
}

/// <summary>
/// Feature flag definition
/// </summary>
public class FeatureFlag
{
    /// <summary>
    /// Feature key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Feature name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Feature description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Is feature enabled globally
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Feature type
    /// </summary>
    public FeatureFlagType Type { get; set; } = FeatureFlagType.Boolean;

    /// <summary>
    /// Enable percentage (0-100) for gradual rollout
    /// </summary>
    public int Percentage { get; set; } = 100;

    /// <summary>
    /// User allowlist
    /// </summary>
    public List<string> AllowedUsers { get; set; } = new();

    /// <summary>
    /// User blocklist
    /// </summary>
    public List<string> BlockedUsers { get; set; } = new();

    /// <summary>
    /// Group allowlist
    /// </summary>
    public List<string> AllowedGroups { get; set; } = new();

    /// <summary>
    /// Variations for A/B testing
    /// </summary>
    public Dictionary<string, object?> Variations { get; set; } = new();

    /// <summary>
    /// Default variation key
    /// </summary>
    public string? DefaultVariation { get; set; }

    /// <summary>
    /// Rules for advanced targeting
    /// </summary>
    public List<FeatureFlagRule> Rules { get; set; } = new();

    /// <summary>
    /// Owner email
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated date
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Scheduled enable date
    /// </summary>
    public DateTime? ScheduledEnableDate { get; set; }

    /// <summary>
    /// Scheduled disable date
    /// </summary>
    public DateTime? ScheduledDisableDate { get; set; }

    /// <summary>
    /// Feature status
    /// </summary>
    public FeatureStatus Status { get; set; } = FeatureStatus.Draft;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Feature flag type
/// </summary>
public enum FeatureFlagType
{
    Boolean,
    String,
    Integer,
    Json
}

/// <summary>
/// Feature status
/// </summary>
public enum FeatureStatus
{
    Draft,
    Active,
    Scheduled,
    Deprecated,
    Archived
}

/// <summary>
/// Feature flag rule
/// </summary>
public class FeatureFlagRule
{
    /// <summary>
    /// Rule ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Rule name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Operator for condition
    /// </summary>
    public RuleOperator Operator { get; set; }

    /// <summary>
    /// Property name to check
    /// </summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>
    /// Value to match
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Result variation if rule matches
    /// </summary>
    public string? ResultVariation { get; set; }

    /// <summary>
    /// Rule priority
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Is rule enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Rule operator enumeration
/// </summary>
public enum RuleOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    In,
    NotIn
}

/// <summary>
/// Feature context for evaluating flags
/// </summary>
public class FeatureContext
{
    /// <summary>
    /// User ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// User groups
    /// </summary>
    public List<string> UserGroups { get; set; } = new();

    /// <summary>
    /// Organization ID
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Custom attributes
    /// </summary>
    public Dictionary<string, object?> Attributes { get; set; } = new();
}

/// <summary>
/// Feature flag metrics
/// </summary>
public class FeatureFlagMetrics
{
    /// <summary>
    /// Feature key
    /// </summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>
    /// Total evaluations
    /// </summary>
    public long TotalEvaluations { get; set; }

    /// <summary>
    /// Enabled count
    /// </summary>
    public long EnabledCount { get; set; }

    /// <summary>
    /// Disabled count
    /// </summary>
    public long DisabledCount { get; set; }

    /// <summary>
    /// Variation counts
    /// </summary>
    public Dictionary<string, long> VariationCounts { get; set; } = new();

    /// <summary>
    /// Enable percentage
    /// </summary>
    public double EnablePercentage => TotalEvaluations > 0 ? (double)EnabledCount / TotalEvaluations * 100 : 0;

    /// <summary>
    /// Last evaluated time
    /// </summary>
    public DateTime? LastEvaluatedTime { get; set; }

    /// <summary>
    /// Period start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Period end date
    /// </summary>
    public DateTime? EndDate { get; set; }
}
