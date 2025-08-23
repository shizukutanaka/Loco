using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loco.Core.FeatureFlags
{
    public interface IFeatureFlagService
    {
        Task<bool> IsEnabledAsync(string feature, FeatureContext context = null, CancellationToken cancellationToken = default);
        Task<T> GetVariantAsync<T>(string feature, T defaultValue, FeatureContext context = null, CancellationToken cancellationToken = default);
        Task<FeatureFlag> GetFeatureFlagAsync(string feature, CancellationToken cancellationToken = default);
        Task<List<FeatureFlag>> GetAllFeatureFlagsAsync(CancellationToken cancellationToken = default);
        Task<bool> SetFeatureFlagAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
        Task<bool> DeleteFeatureFlagAsync(string feature, CancellationToken cancellationToken = default);
        void RegisterEvaluator(string name, IFeatureEvaluator evaluator);
        event EventHandler<FeatureFlagChangedEventArgs> FeatureFlagChanged;
    }

    public class FeatureFlagService : IFeatureFlagService
    {
        private readonly ILogger<FeatureFlagService> _logger;
        private readonly IFeatureFlagStore _store;
        private readonly FeatureFlagOptions _options;
        private readonly Dictionary<string, IFeatureEvaluator> _evaluators;
        private readonly Dictionary<string, FeatureFlag> _cache;
        private readonly SemaphoreSlim _cacheLock;
        private readonly Timer _refreshTimer;

        public event EventHandler<FeatureFlagChangedEventArgs> FeatureFlagChanged;

        public FeatureFlagService(
            ILogger<FeatureFlagService> logger,
            IFeatureFlagStore store,
            IOptions<FeatureFlagOptions> options)
        {
            _logger = logger;
            _store = store;
            _options = options?.Value ?? new FeatureFlagOptions();
            _evaluators = new Dictionary<string, IFeatureEvaluator>();
            _cache = new Dictionary<string, FeatureFlag>();
            _cacheLock = new SemaphoreSlim(1, 1);

            RegisterDefaultEvaluators();

            if (_options.EnableAutoRefresh)
            {
                _refreshTimer = new Timer(RefreshCache, null, _options.RefreshInterval, _options.RefreshInterval);
            }
        }

        public async Task<bool> IsEnabledAsync(string feature, FeatureContext context = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var flag = await GetFeatureFlagFromCacheAsync(feature, cancellationToken);
                
                if (flag == null)
                {
                    return _options.DefaultValue;
                }

                if (!flag.Enabled)
                {
                    return false;
                }

                context ??= new FeatureContext();
                
                // Evaluate conditions
                foreach (var condition in flag.Conditions)
                {
                    if (!await EvaluateConditionAsync(condition, context))
                    {
                        return false;
                    }
                }

                // Evaluate rules
                foreach (var rule in flag.Rules)
                {
                    if (await EvaluateRuleAsync(rule, context))
                    {
                        return rule.Enabled;
                    }
                }

                // Check percentage rollout
                if (flag.PercentageRollout.HasValue)
                {
                    var hash = GetStableHash(feature, context.UserId ?? context.SessionId ?? Guid.NewGuid().ToString());
                    var percentage = (hash % 100) + 1;
                    return percentage <= flag.PercentageRollout.Value;
                }

                return flag.Enabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating feature flag {Feature}", feature);
                return _options.DefaultValue;
            }
        }

        public async Task<T> GetVariantAsync<T>(string feature, T defaultValue, FeatureContext context = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var flag = await GetFeatureFlagFromCacheAsync(feature, cancellationToken);
                
                if (flag?.Variants == null || !flag.Variants.Any())
                {
                    return defaultValue;
                }

                context ??= new FeatureContext();

                // Find matching variant based on rules
                foreach (var variant in flag.Variants)
                {
                    if (variant.Rules != null)
                    {
                        var allRulesMatch = true;
                        foreach (var rule in variant.Rules)
                        {
                            if (!await EvaluateRuleAsync(rule, context))
                            {
                                allRulesMatch = false;
                                break;
                            }
                        }

                        if (allRulesMatch)
                        {
                            return DeserializeVariantValue<T>(variant.Value);
                        }
                    }
                }

                // Use weighted random selection if no rules match
                if (flag.Variants.All(v => v.Weight.HasValue))
                {
                    var totalWeight = flag.Variants.Sum(v => v.Weight.Value);
                    var hash = GetStableHash(feature, context.UserId ?? context.SessionId ?? Guid.NewGuid().ToString());
                    var random = (hash % totalWeight) + 1;
                    
                    var currentWeight = 0;
                    foreach (var variant in flag.Variants)
                    {
                        currentWeight += variant.Weight.Value;
                        if (random <= currentWeight)
                        {
                            return DeserializeVariantValue<T>(variant.Value);
                        }
                    }
                }

                // Return first variant as fallback
                return DeserializeVariantValue<T>(flag.Variants.First().Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting variant for feature {Feature}", feature);
                return defaultValue;
            }
        }

        public async Task<FeatureFlag> GetFeatureFlagAsync(string feature, CancellationToken cancellationToken = default)
        {
            return await _store.GetAsync(feature, cancellationToken);
        }

        public async Task<List<FeatureFlag>> GetAllFeatureFlagsAsync(CancellationToken cancellationToken = default)
        {
            return await _store.GetAllAsync(cancellationToken);
        }

        public async Task<bool> SetFeatureFlagAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
        {
            var result = await _store.SetAsync(flag, cancellationToken);
            
            if (result)
            {
                await InvalidateCacheAsync(flag.Name);
                OnFeatureFlagChanged(new FeatureFlagChangedEventArgs
                {
                    Feature = flag.Name,
                    ChangeType = FeatureFlagChangeType.Updated,
                    NewValue = flag
                });
            }

            return result;
        }

        public async Task<bool> DeleteFeatureFlagAsync(string feature, CancellationToken cancellationToken = default)
        {
            var result = await _store.DeleteAsync(feature, cancellationToken);
            
            if (result)
            {
                await InvalidateCacheAsync(feature);
                OnFeatureFlagChanged(new FeatureFlagChangedEventArgs
                {
                    Feature = feature,
                    ChangeType = FeatureFlagChangeType.Deleted
                });
            }

            return result;
        }

        public void RegisterEvaluator(string name, IFeatureEvaluator evaluator)
        {
            _evaluators[name] = evaluator;
        }

        private async Task<FeatureFlag> GetFeatureFlagFromCacheAsync(string feature, CancellationToken cancellationToken)
        {
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.ContainsKey(feature))
                {
                    return _cache[feature];
                }

                var flag = await _store.GetAsync(feature, cancellationToken);
                if (flag != null)
                {
                    _cache[feature] = flag;
                }

                return flag;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task InvalidateCacheAsync(string feature)
        {
            await _cacheLock.WaitAsync();
            try
            {
                _cache.Remove(feature);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async void RefreshCache(object state)
        {
            try
            {
                var flags = await _store.GetAllAsync();
                
                await _cacheLock.WaitAsync();
                try
                {
                    _cache.Clear();
                    foreach (var flag in flags)
                    {
                        _cache[flag.Name] = flag;
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }

                _logger.LogDebug("Feature flag cache refreshed with {Count} flags", flags.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing feature flag cache");
            }
        }

        private async Task<bool> EvaluateConditionAsync(FeatureCondition condition, FeatureContext context)
        {
            if (_evaluators.TryGetValue(condition.Type, out var evaluator))
            {
                return await evaluator.EvaluateAsync(condition, context);
            }

            _logger.LogWarning("No evaluator found for condition type {Type}", condition.Type);
            return false;
        }

        private async Task<bool> EvaluateRuleAsync(FeatureRule rule, FeatureContext context)
        {
            if (rule.Conditions == null || !rule.Conditions.Any())
            {
                return true;
            }

            foreach (var condition in rule.Conditions)
            {
                if (!await EvaluateConditionAsync(condition, context))
                {
                    return false;
                }
            }

            return true;
        }

        private T DeserializeVariantValue<T>(object value)
        {
            if (value == null)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            if (value is JsonElement jsonElement)
            {
                var json = jsonElement.GetRawText();
                return JsonSerializer.Deserialize<T>(json);
            }

            var valueJson = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(valueJson);
        }

        private int GetStableHash(string feature, string identifier)
        {
            var combined = $"{feature}:{identifier}";
            return Math.Abs(combined.GetHashCode());
        }

        private void RegisterDefaultEvaluators()
        {
            RegisterEvaluator("user", new UserEvaluator());
            RegisterEvaluator("time", new TimeEvaluator());
            RegisterEvaluator("percentage", new PercentageEvaluator());
            RegisterEvaluator("environment", new EnvironmentEvaluator());
            RegisterEvaluator("custom", new CustomEvaluator());
        }

        private void OnFeatureFlagChanged(FeatureFlagChangedEventArgs args)
        {
            FeatureFlagChanged?.Invoke(this, args);
        }
    }

    // Models
    public class FeatureFlag
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public List<FeatureCondition> Conditions { get; set; } = new();
        public List<FeatureRule> Rules { get; set; } = new();
        public List<FeatureVariant> Variants { get; set; } = new();
        public int? PercentageRollout { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class FeatureCondition
    {
        public string Type { get; set; }
        public string Property { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
    }

    public class FeatureRule
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public List<FeatureCondition> Conditions { get; set; } = new();
    }

    public class FeatureVariant
    {
        public string Name { get; set; }
        public object Value { get; set; }
        public int? Weight { get; set; }
        public List<FeatureRule> Rules { get; set; } = new();
    }

    public class FeatureContext
    {
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public string Environment { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    public class FeatureFlagOptions
    {
        public bool DefaultValue { get; set; } = false;
        public bool EnableAutoRefresh { get; set; } = true;
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class FeatureFlagChangedEventArgs : EventArgs
    {
        public string Feature { get; set; }
        public FeatureFlagChangeType ChangeType { get; set; }
        public FeatureFlag NewValue { get; set; }
    }

    public enum FeatureFlagChangeType
    {
        Created,
        Updated,
        Deleted
    }

    // Interfaces
    public interface IFeatureFlagStore
    {
        Task<FeatureFlag> GetAsync(string feature, CancellationToken cancellationToken = default);
        Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> SetAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string feature, CancellationToken cancellationToken = default);
    }

    public interface IFeatureEvaluator
    {
        Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context);
    }

    // Default Evaluators
    public class UserEvaluator : IFeatureEvaluator
    {
        public Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context)
        {
            if (string.IsNullOrEmpty(context.UserId))
                return Task.FromResult(false);

            return Task.FromResult(condition.Operator switch
            {
                "equals" => context.UserId == condition.Value?.ToString(),
                "in" => condition.Value is IEnumerable<string> list && list.Contains(context.UserId),
                "not_in" => condition.Value is IEnumerable<string> notList && !notList.Contains(context.UserId),
                _ => false
            });
        }
    }

    public class TimeEvaluator : IFeatureEvaluator
    {
        public Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context)
        {
            var now = DateTime.UtcNow;
            
            return Task.FromResult(condition.Operator switch
            {
                "after" => DateTime.TryParse(condition.Value?.ToString(), out var after) && now > after,
                "before" => DateTime.TryParse(condition.Value?.ToString(), out var before) && now < before,
                _ => false
            });
        }
    }

    public class PercentageEvaluator : IFeatureEvaluator
    {
        public Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context)
        {
            if (!int.TryParse(condition.Value?.ToString(), out var percentage))
                return Task.FromResult(false);

            var identifier = context.UserId ?? context.SessionId ?? Guid.NewGuid().ToString();
            var hash = Math.Abs(identifier.GetHashCode());
            var userPercentage = (hash % 100) + 1;

            return Task.FromResult(userPercentage <= percentage);
        }
    }

    public class EnvironmentEvaluator : IFeatureEvaluator
    {
        public Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context)
        {
            if (string.IsNullOrEmpty(context.Environment))
                return Task.FromResult(false);

            return Task.FromResult(condition.Operator switch
            {
                "equals" => context.Environment.Equals(condition.Value?.ToString(), StringComparison.OrdinalIgnoreCase),
                "in" => condition.Value is IEnumerable<string> list && list.Any(e => e.Equals(context.Environment, StringComparison.OrdinalIgnoreCase)),
                _ => false
            });
        }
    }

    public class CustomEvaluator : IFeatureEvaluator
    {
        public Task<bool> EvaluateAsync(FeatureCondition condition, FeatureContext context)
        {
            if (!context.Properties.TryGetValue(condition.Property, out var value))
                return Task.FromResult(false);

            return Task.FromResult(condition.Operator switch
            {
                "equals" => value?.ToString() == condition.Value?.ToString(),
                "greater_than" => CompareValues(value, condition.Value) > 0,
                "less_than" => CompareValues(value, condition.Value) < 0,
                "contains" => value?.ToString()?.Contains(condition.Value?.ToString() ?? "") ?? false,
                _ => false
            });
        }

        private int CompareValues(object left, object right)
        {
            if (left is IComparable leftComparable && right is IComparable rightComparable)
            {
                return leftComparable.CompareTo(rightComparable);
            }

            return string.Compare(left?.ToString(), right?.ToString(), StringComparison.Ordinal);
        }
    }
}