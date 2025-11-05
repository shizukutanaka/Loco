// Rob Pike: "Data dominates. If you've chosen the right data structures, the algorithms will almost always be self-evident"
// John Carmack: "Make it work, make it right, make it fast - in that order"

using System.Collections.Concurrent;
using System.Text.Json;

namespace Loco.Core.Practical;

/// <summary>
/// Simple feature flags - Control feature rollout, A/B testing, gradual deployment
/// Fast lookups, no external dependencies, runtime updates
/// </summary>
public class SimpleFeatureFlags
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new();
    private readonly SimpleLogger _logger;
    private readonly List<IFeatureFlagProvider> _providers = new();

    public SimpleFeatureFlags(SimpleLogger? logger = null)
    {
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleFeatureFlags));
    }

    // Check if feature is enabled
    public bool IsEnabled(string feature, string? userId = null)
    {
        if (!_flags.TryGetValue(feature, out var flag))
        {
            return false;
        }

        // Check if globally enabled/disabled
        if (!flag.Enabled)
        {
            return false;
        }

        // Check percentage rollout
        if (flag.PercentageRollout.HasValue && userId != null)
        {
            var hash = userId.GetHashCode();
            var percentage = Math.Abs(hash) % 100;
            return percentage < flag.PercentageRollout.Value;
        }

        // Check user whitelist
        if (flag.WhitelistedUsers?.Count > 0 && userId != null)
        {
            return flag.WhitelistedUsers.Contains(userId);
        }

        // Check user blacklist
        if (flag.BlacklistedUsers?.Count > 0 && userId != null)
        {
            return !flag.BlacklistedUsers.Contains(userId);
        }

        return flag.Enabled;
    }

    // Register feature flag
    public void RegisterFlag(string name, bool enabled = false, int? percentageRollout = null)
    {
        _flags[name] = new FeatureFlag
        {
            Name = name,
            Enabled = enabled,
            PercentageRollout = percentageRollout
        };
        _logger.Info($"Feature flag registered: {name} = {enabled}");
    }

    // Update flag
    public void UpdateFlag(string name, bool enabled)
    {
        if (_flags.TryGetValue(name, out var flag))
        {
            flag.Enabled = enabled;
            _logger.Info($"Feature flag updated: {name} = {enabled}");
        }
    }

    // Set percentage rollout
    public void SetPercentageRollout(string name, int percentage)
    {
        if (_flags.TryGetValue(name, out var flag))
        {
            flag.PercentageRollout = Math.Clamp(percentage, 0, 100);
            _logger.Info($"Feature flag rollout: {name} = {percentage}%");
        }
    }

    // Add user to whitelist
    public void WhitelistUser(string feature, string userId)
    {
        if (_flags.TryGetValue(feature, out var flag))
        {
            flag.WhitelistedUsers ??= new HashSet<string>();
            flag.WhitelistedUsers.Add(userId);
        }
    }

    // Add user to blacklist
    public void BlacklistUser(string feature, string userId)
    {
        if (_flags.TryGetValue(feature, out var flag))
        {
            flag.BlacklistedUsers ??= new HashSet<string>();
            flag.BlacklistedUsers.Add(userId);
        }
    }

    // Get all flags
    public Dictionary<string, bool> GetAllFlags(string? userId = null)
    {
        return _flags.ToDictionary(
            kvp => kvp.Key,
            kvp => IsEnabled(kvp.Key, userId));
    }

    // Load flags from JSON
    public void LoadFromJson(string json)
    {
        try
        {
            var flags = JsonSerializer.Deserialize<List<FeatureFlag>>(json);
            if (flags != null)
            {
                foreach (var flag in flags)
                {
                    _flags[flag.Name] = flag;
                }
                _logger.Info($"Loaded {flags.Count} feature flags");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load feature flags", ex);
        }
    }

    // Add provider
    public void AddProvider(IFeatureFlagProvider provider)
    {
        _providers.Add(provider);
    }

    // Refresh from providers
    public async Task RefreshAsync()
    {
        foreach (var provider in _providers)
        {
            try
            {
                var flags = await provider.GetFlagsAsync();
                foreach (var flag in flags)
                {
                    _flags[flag.Name] = flag;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to refresh from provider {provider.GetType().Name}", ex);
            }
        }
    }

    private class FeatureFlag
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public int? PercentageRollout { get; set; }
        public HashSet<string>? WhitelistedUsers { get; set; }
        public HashSet<string>? BlacklistedUsers { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }
}

/// <summary>
/// Feature flag provider interface
/// </summary>
public interface IFeatureFlagProvider
{
    Task<List<FeatureFlag>> GetFlagsAsync();

    public class FeatureFlag
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public int? PercentageRollout { get; set; }
        public HashSet<string>? WhitelistedUsers { get; set; }
        public HashSet<string>? BlacklistedUsers { get; set; }
    }
}

/// <summary>
/// File-based feature flag provider
/// </summary>
public class FileFeatureFlagProvider : IFeatureFlagProvider
{
    private readonly string _filePath;

    public FileFeatureFlagProvider(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<List<IFeatureFlagProvider.FeatureFlag>> GetFlagsAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<IFeatureFlagProvider.FeatureFlag>();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<IFeatureFlagProvider.FeatureFlag>>(json)
            ?? new List<IFeatureFlagProvider.FeatureFlag>();
    }
}

/// <summary>
/// A/B testing support
/// </summary>
public class ABTest
{
    private readonly SimpleFeatureFlags _flags;
    private readonly string _testName;
    private readonly Dictionary<string, int> _variants = new();

    public ABTest(SimpleFeatureFlags flags, string testName)
    {
        _flags = flags;
        _testName = testName;
    }

    public ABTest AddVariant(string name, int weight)
    {
        _variants[name] = weight;
        return this;
    }

    public string GetVariant(string userId)
    {
        if (_variants.Count == 0)
            return "control";

        var totalWeight = _variants.Sum(v => v.Value);
        var hash = userId.GetHashCode();
        var position = Math.Abs(hash) % totalWeight;

        var currentPosition = 0;
        foreach (var variant in _variants)
        {
            currentPosition += variant.Value;
            if (position < currentPosition)
            {
                return variant.Key;
            }
        }

        return _variants.First().Key;
    }
}

/// <summary>
/// Feature flag context for dependency injection
/// </summary>
public class FeatureFlagContext
{
    private readonly SimpleFeatureFlags _flags;
    private readonly string? _userId;

    public FeatureFlagContext(SimpleFeatureFlags flags, string? userId = null)
    {
        _flags = flags;
        _userId = userId;
    }

    public bool IsEnabled(string feature) => _flags.IsEnabled(feature, _userId);

    public T ExecuteIfEnabled<T>(string feature, Func<T> action, T defaultValue = default!)
    {
        return IsEnabled(feature) ? action() : defaultValue;
    }

    public async Task<T> ExecuteIfEnabledAsync<T>(string feature, Func<Task<T>> action, T defaultValue = default!)
    {
        return IsEnabled(feature) ? await action() : defaultValue;
    }
}

/// <summary>
/// Example: Using feature flags in an application
/// </summary>
public class ApplicationWithFeatures
{
    private readonly SimpleFeatureFlags _featureFlags;
    private readonly SimpleLogger _logger;

    public ApplicationWithFeatures()
    {
        _featureFlags = new SimpleFeatureFlags();
        _logger = SimpleLoggerFactory.GetLogger(nameof(ApplicationWithFeatures));

        // Register features
        _featureFlags.RegisterFlag("new-ui", enabled: false, percentageRollout: 10);
        _featureFlags.RegisterFlag("advanced-search", enabled: true);
        _featureFlags.RegisterFlag("beta-features", enabled: false);
        _featureFlags.RegisterFlag("dark-mode", enabled: true);
    }

    public async Task<string> ProcessRequestAsync(string userId)
    {
        var context = new FeatureFlagContext(_featureFlags, userId);

        // Check feature flags
        var response = new List<string>();

        if (context.IsEnabled("new-ui"))
        {
            response.Add("Using new UI");
        }
        else
        {
            response.Add("Using legacy UI");
        }

        if (context.IsEnabled("advanced-search"))
        {
            response.Add("Advanced search enabled");
        }

        // A/B testing
        var abTest = new ABTest(_featureFlags, "checkout-flow")
            .AddVariant("variant-a", 50)
            .AddVariant("variant-b", 30)
            .AddVariant("variant-c", 20);

        var variant = abTest.GetVariant(userId);
        response.Add($"Checkout variant: {variant}");

        // Execute conditionally
        var result = await context.ExecuteIfEnabledAsync(
            "beta-features",
            async () => await LoadBetaFeaturesAsync(),
            new List<string>());

        if (result.Any())
        {
            response.Add($"Beta features: {string.Join(", ", result)}");
        }

        return string.Join("; ", response);
    }

    private async Task<List<string>> LoadBetaFeaturesAsync()
    {
        await Task.Delay(10);
        return new List<string> { "Feature1", "Feature2", "Feature3" };
    }
}

/// <summary>
/// Example: Feature flag middleware
/// </summary>
public class FeatureFlagMiddleware
{
    private readonly SimpleFeatureFlags _flags;

    public FeatureFlagMiddleware(SimpleFeatureFlags flags)
    {
        _flags = flags;
    }

    public async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        // Extract user ID from context
        var userId = context.Headers.GetValueOrDefault("User-Id", "anonymous");

        // Add feature flags to response headers
        var enabledFeatures = _flags.GetAllFlags(userId)
            .Where(f => f.Value)
            .Select(f => f.Key);

        context.Headers["X-Features"] = string.Join(",", enabledFeatures);

        // Block access to beta endpoints if not enabled
        if (context.Path.StartsWith("/beta/") && !_flags.IsEnabled("beta-access", userId))
        {
            context.Response = "Beta features not available";
            return;
        }

        await next();
    }
}

/// <summary>
/// Feature toggle attribute for methods
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class FeatureToggleAttribute : Attribute
{
    public string FeatureName { get; }

    public FeatureToggleAttribute(string featureName)
    {
        FeatureName = featureName;
    }
}