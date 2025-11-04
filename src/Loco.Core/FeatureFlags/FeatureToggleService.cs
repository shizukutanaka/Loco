using Microsoft.Extensions.Logging;

namespace Loco.Core.FeatureFlags;

/// <summary>
/// In-memory feature toggle service implementation
/// </summary>
public class InMemoryFeatureToggleService : IFeatureToggleService
{
    private readonly Dictionary<string, FeatureFlag> _flags = new();
    private readonly Dictionary<string, FeatureFlagMetrics> _metrics = new();
    private readonly object _lock = new();
    private readonly ILogger<InMemoryFeatureToggleService> _logger;

    public InMemoryFeatureToggleService(ILogger<InMemoryFeatureToggleService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureKey, FeatureContext? context = null)
    {
        try
        {
            lock (_lock)
            {
                if (!_flags.TryGetValue(featureKey, out var flag))
                {
                    _logger.LogWarning("Feature flag not found: {FeatureKey}", featureKey);
                    return false;
                }

                var enabled = EvaluateFeature(flag, context ?? new FeatureContext());
                RecordMetric(featureKey, enabled);

                return enabled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating feature flag: {FeatureKey}", featureKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<FeatureFlag?> GetFeatureFlagAsync(string featureKey)
    {
        lock (_lock)
        {
            _flags.TryGetValue(featureKey, out var flag);
            return flag;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FeatureFlag>> GetAllFeaturesAsync()
    {
        lock (_lock)
        {
            return _flags.Values.ToList();
        }
    }

    /// <inheritdoc />
    public async Task<FeatureFlag> CreateFeatureFlagAsync(FeatureFlag flag)
    {
        try
        {
            lock (_lock)
            {
                if (_flags.ContainsKey(flag.Key))
                {
                    throw new InvalidOperationException($"Feature flag '{flag.Key}' already exists");
                }

                flag.CreatedAt = DateTime.UtcNow;
                flag.UpdatedAt = DateTime.UtcNow;
                _flags[flag.Key] = flag;
                _metrics[flag.Key] = new FeatureFlagMetrics
                {
                    FeatureKey = flag.Key,
                    StartDate = DateTime.UtcNow
                };

                _logger.LogInformation("Feature flag created: {FeatureKey}", flag.Key);
                return flag;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating feature flag: {FeatureKey}", flag.Key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateFeatureFlagAsync(string featureKey, FeatureFlag flag)
    {
        try
        {
            lock (_lock)
            {
                if (!_flags.ContainsKey(featureKey))
                {
                    return false;
                }

                flag.Key = featureKey;
                flag.UpdatedAt = DateTime.UtcNow;
                _flags[featureKey] = flag;

                _logger.LogInformation("Feature flag updated: {FeatureKey}", featureKey);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature flag: {FeatureKey}", featureKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFeatureFlagAsync(string featureKey)
    {
        try
        {
            lock (_lock)
            {
                var removed = _flags.Remove(featureKey);
                if (removed)
                {
                    _metrics.Remove(featureKey);
                    _logger.LogInformation("Feature flag deleted: {FeatureKey}", featureKey);
                }

                return removed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feature flag: {FeatureKey}", featureKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnableFeatureAsync(string featureKey)
    {
        try
        {
            lock (_lock)
            {
                if (!_flags.TryGetValue(featureKey, out var flag))
                {
                    return false;
                }

                flag.Enabled = true;
                flag.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("Feature flag enabled: {FeatureKey}", featureKey);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling feature flag: {FeatureKey}", featureKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DisableFeatureAsync(string featureKey)
    {
        try
        {
            lock (_lock)
            {
                if (!_flags.TryGetValue(featureKey, out var flag))
                {
                    return false;
                }

                flag.Enabled = false;
                flag.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("Feature flag disabled: {FeatureKey}", featureKey);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling feature flag: {FeatureKey}", featureKey);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<FeatureFlagMetrics> GetMetricsAsync(string featureKey)
    {
        lock (_lock)
        {
            if (_metrics.TryGetValue(featureKey, out var metrics))
            {
                metrics.EndDate = DateTime.UtcNow;
                return metrics;
            }

            return new FeatureFlagMetrics { FeatureKey = featureKey };
        }
    }

    private bool EvaluateFeature(FeatureFlag flag, FeatureContext context)
    {
        // Check if scheduled enable/disable dates apply
        var now = DateTime.UtcNow;
        if (flag.ScheduledDisableDate.HasValue && now >= flag.ScheduledDisableDate)
        {
            return false;
        }

        if (flag.ScheduledEnableDate.HasValue && now < flag.ScheduledEnableDate)
        {
            return false;
        }

        // Check global enable state
        if (!flag.Enabled)
        {
            return false;
        }

        // Check user blocklist
        if (context.UserId != null && flag.BlockedUsers.Contains(context.UserId))
        {
            return false;
        }

        // Check user allowlist
        if (flag.AllowedUsers.Any() && context.UserId != null && !flag.AllowedUsers.Contains(context.UserId))
        {
            return false;
        }

        // Check group allowlist
        if (flag.AllowedGroups.Any())
        {
            var hasMatchingGroup = context.UserGroups.Any(g => flag.AllowedGroups.Contains(g));
            if (!hasMatchingGroup)
            {
                return false;
            }
        }

        // Evaluate rules
        foreach (var rule in flag.Rules.Where(r => r.Enabled).OrderBy(r => r.Priority))
        {
            if (EvaluateRule(rule, context))
            {
                return true;
            }
        }

        // Check percentage for gradual rollout
        if (flag.Percentage < 100)
        {
            var hash = GetUserHash(context.UserId ?? context.OrganizationId ?? "default");
            return (hash % 100) < flag.Percentage;
        }

        return true;
    }

    private bool EvaluateRule(FeatureFlagRule rule, FeatureContext context)
    {
        var value = context.Attributes.TryGetValue(rule.Property, out var v) ? v : null;

        return rule.Operator switch
        {
            RuleOperator.Equals => Equals(value, rule.Value),
            RuleOperator.NotEquals => !Equals(value, rule.Value),
            RuleOperator.Contains => value?.ToString()?.Contains(rule.Value?.ToString() ?? "") ?? false,
            RuleOperator.NotContains => !(value?.ToString()?.Contains(rule.Value?.ToString() ?? "") ?? false),
            RuleOperator.StartsWith => value?.ToString()?.StartsWith(rule.Value?.ToString() ?? "") ?? false,
            RuleOperator.EndsWith => value?.ToString()?.EndsWith(rule.Value?.ToString() ?? "") ?? false,
            RuleOperator.In => rule.Value is IEnumerable<object> list && list.Contains(value),
            RuleOperator.NotIn => !(rule.Value is IEnumerable<object> list && list.Contains(value)),
            _ => false
        };
    }

    private void RecordMetric(string featureKey, bool enabled)
    {
        if (_metrics.TryGetValue(featureKey, out var metrics))
        {
            metrics.TotalEvaluations++;
            metrics.LastEvaluatedTime = DateTime.UtcNow;

            if (enabled)
            {
                metrics.EnabledCount++;
            }
            else
            {
                metrics.DisabledCount++;
            }
        }
    }

    private int GetUserHash(string identifier)
    {
        // Generate a consistent hash for user-based percentage calculation
        var hash = 0;
        foreach (var c in identifier)
        {
            hash = ((hash << 5) - hash) + c;
            hash = hash & hash; // Convert to 32-bit integer
        }

        return Math.Abs(hash);
    }
}
