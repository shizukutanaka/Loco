#nullable enable

using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Loco.Core.FeatureFlags;

/// <summary>
/// Feature Flags & A/B Testing Framework
/// Enables progressive feature rollout and experimentation
/// Decouples deployment from release
/// </summary>

/// <summary>
/// Feature flag types
/// </summary>
public enum FeatureFlagType
{
    /// <summary>
    /// Release toggle - temporary for incomplete code
    /// </summary>
    ReleaseToggle,

    /// <summary>
    /// Experiment toggle - for A/B testing
    /// </summary>
    ExperimentToggle,

    /// <summary>
    /// Permission toggle - feature gates for user types
    /// </summary>
    PermissionToggle,

    /// <summary>
    /// Operations toggle - infrastructure/configuration
    /// </summary>
    OperationsToggle
}

/// <summary>
/// Feature flag definition
/// </summary>
public class FeatureFlag
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public FeatureFlagType Type { get; set; } = FeatureFlagType.ReleaseToggle;

    /// <summary>
    /// Percentage of users to enable for (0-100)
    /// </summary>
    public int RolloutPercentage { get; set; } = 100;

    /// <summary>
    /// User IDs that should see this feature
    /// </summary>
    public List<string> AllowedUserIds { get; set; } = new();

    /// <summary>
    /// User roles that can see this feature
    /// </summary>
    public List<string> AllowedRoles { get; set; } = new();

    /// <summary>
    /// Feature groups (for segmentation)
    /// </summary>
    public List<string> Groups { get; set; } = new();

    /// <summary>
    /// When created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When scheduled to enable/disable
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Metadata for tracking
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Feature flag context for evaluation
/// </summary>
public class FeatureFlagContext
{
    public string? UserId { get; set; }
    public List<string> UserRoles { get; set; } = new();
    public Dictionary<string, object> CustomAttributes { get; set; } = new();
    public string? TenantId { get; set; }
    public string? EnvironmentName { get; set; }

    /// <summary>
    /// Generates consistent hash for rollout percentage
    /// </summary>
    public int GetConsistentHashCode()
    {
        var value = $"{UserId}:{TenantId}";
        return Math.Abs(value.GetHashCode());
    }
}

/// <summary>
/// Feature flag evaluation result
/// </summary>
public class FeatureFlagResult
{
    public bool Enabled { get; set; }
    public string? Reason { get; set; }
    public Dictionary<string, object> EvaluationDetails { get; set; } = new();
}

/// <summary>
/// Feature flag service - evaluates flags
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Evaluates if feature is enabled for context
    /// </summary>
    Task<bool> IsEnabledAsync(string featureName, FeatureFlagContext context);

    /// <summary>
    /// Gets detailed evaluation result
    /// </summary>
    Task<FeatureFlagResult> EvaluateAsync(string featureName, FeatureFlagContext context);

    /// <summary>
    /// Gets all flags
    /// </summary>
    Task<List<FeatureFlag>> GetAllFlagsAsync();

    /// <summary>
    /// Updates flag
    /// </summary>
    Task UpdateFlagAsync(FeatureFlag flag);

    /// <summary>
    /// Deletes flag
    /// </summary>
    Task DeleteFlagAsync(string featureName);
}

/// <summary>
/// In-memory feature flag service
/// Production: Use LaunchDarkly, Unleash, or similar
/// </summary>
public class InMemoryFeatureFlagService : IFeatureFlagService
{
    private readonly ConcurrentDictionary<string, FeatureFlag> _flags = new();
    private readonly ILogger<InMemoryFeatureFlagService> _logger;

    public InMemoryFeatureFlagService(ILogger<InMemoryFeatureFlagService> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsEnabledAsync(string featureName, FeatureFlagContext context)
    {
        var result = EvaluateFlag(featureName, context);
        return Task.FromResult(result.Enabled);
    }

    public Task<FeatureFlagResult> EvaluateAsync(string featureName, FeatureFlagContext context)
    {
        var result = EvaluateFlag(featureName, context);
        return Task.FromResult(result);
    }

    public Task<List<FeatureFlag>> GetAllFlagsAsync()
    {
        return Task.FromResult(_flags.Values.ToList());
    }

    public Task UpdateFlagAsync(FeatureFlag flag)
    {
        _flags[flag.Name] = flag;
        _logger.LogInformation("Feature flag updated: {FlagName}", flag.Name);
        return Task.CompletedTask;
    }

    public Task DeleteFlagAsync(string featureName)
    {
        _flags.TryRemove(featureName, out _);
        _logger.LogInformation("Feature flag deleted: {FlagName}", featureName);
        return Task.CompletedTask;
    }

    private FeatureFlagResult EvaluateFlag(string featureName, FeatureFlagContext context)
    {
        if (!_flags.TryGetValue(featureName, out var flag))
        {
            _logger.LogWarning("Feature flag not found: {FlagName}", featureName);
            return new FeatureFlagResult
            {
                Enabled = false,
                Reason = "Flag not found"
            };
        }

        var evaluationDetails = new Dictionary<string, object>
        {
            ["FlagName"] = featureName,
            ["Type"] = flag.Type.ToString(),
            ["CreatedAt"] = flag.CreatedAt
        };

        // Check if scheduled
        if (flag.ScheduledAt.HasValue && DateTime.UtcNow < flag.ScheduledAt)
        {
            return new FeatureFlagResult
            {
                Enabled = false,
                Reason = "Scheduled flag not yet active",
                EvaluationDetails = evaluationDetails
            };
        }

        // Base enabled check
        if (!flag.Enabled)
        {
            return new FeatureFlagResult
            {
                Enabled = false,
                Reason = "Flag is disabled",
                EvaluationDetails = evaluationDetails
            };
        }

        // Check explicit user allowlist
        if (flag.AllowedUserIds.Any() && !flag.AllowedUserIds.Contains(context.UserId))
        {
            return new FeatureFlagResult
            {
                Enabled = false,
                Reason = "User not in allowlist",
                EvaluationDetails = evaluationDetails
            };
        }

        // Check role-based access
        if (flag.AllowedRoles.Any())
        {
            var hasRole = context.UserRoles.Any(role => flag.AllowedRoles.Contains(role));
            if (!hasRole)
            {
                return new FeatureFlagResult
                {
                    Enabled = false,
                    Reason = "User doesn't have required role",
                    EvaluationDetails = evaluationDetails
                };
            }
        }

        // Check rollout percentage
        if (flag.RolloutPercentage < 100)
        {
            var hash = context.GetConsistentHashCode();
            var percentage = Math.Abs(hash % 100);

            evaluationDetails["RolloutPercentage"] = flag.RolloutPercentage;
            evaluationDetails["UserPercentage"] = percentage;

            if (percentage >= flag.RolloutPercentage)
            {
                return new FeatureFlagResult
                {
                    Enabled = false,
                    Reason = "User not selected for rollout",
                    EvaluationDetails = evaluationDetails
                };
            }
        }

        evaluationDetails["Enabled"] = true;

        return new FeatureFlagResult
        {
            Enabled = true,
            Reason = "All conditions passed",
            EvaluationDetails = evaluationDetails
        };
    }
}

/// <summary>
/// Feature flag middleware
/// </summary>
public class FeatureFlagMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FeatureFlagMiddleware> _logger;

    public FeatureFlagMiddleware(RequestDelegate next, ILogger<FeatureFlagMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IFeatureFlagService featureFlagService)
    {
        // Extract user context from request
        var userId = context.User?.FindFirst("sub")?.Value ?? "anonymous";
        var roles = context.User?.FindAll("role")
            ?.Select(c => c.Value)
            .ToList() ?? new List<string>();

        var flagContext = new FeatureFlagContext
        {
            UserId = userId,
            UserRoles = roles,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        };

        // Store in context items for use in controllers
        context.Items["FeatureFlagContext"] = flagContext;

        await _next(context).ConfigureAwait(false);
    }
}

/// <summary>
/// Attribute for feature flag gating
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireFeatureFlagAttribute : Attribute
{
    public string FlagName { get; set; }

    public RequireFeatureFlagAttribute(string flagName)
    {
        FlagName = flagName;
    }
}

/// <summary>
/// Feature flag authorization policy
/// </summary>
public class FeatureFlagAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<FeatureFlagAuthorizationFilter> _logger;

    public FeatureFlagAuthorizationFilter(
        IFeatureFlagService featureFlagService,
        ILogger<FeatureFlagAuthorizationFilter> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata
            .OfType<RequireFeatureFlagAttribute>()
            .FirstOrDefault();

        if (attribute == null)
            return;

        var flagContext = context.HttpContext.Items["FeatureFlagContext"] as FeatureFlagContext
            ?? new FeatureFlagContext();

        var isEnabled = await _featureFlagService.IsEnabledAsync(attribute.FlagName, flagContext)
            .ConfigureAwait(false);

        if (!isEnabled)
        {
            _logger.LogWarning(
                "Feature flag not enabled for user: {Flag} ({User})",
                attribute.FlagName,
                flagContext.UserId);

            context.Result = new NotFoundResult();
        }
    }
}

/// <summary>
/// Feature flag tag helper for Razor views
/// </summary>
public class FeatureFlagHelper
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagHelper(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public async Task<bool> IsEnabledAsync(string flagName, FeatureFlagContext context)
    {
        return await _featureFlagService.IsEnabledAsync(flagName, context).ConfigureAwait(false);
    }
}

/// <summary>
/// A/B testing experiment
/// </summary>
public class Experiment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of users in experiment (0-100)
    /// </summary>
    public int SamplePercentage { get; set; } = 50;

    /// <summary>
    /// Control group feature flag
    /// </summary>
    public string ControlFlagName { get; set; } = string.Empty;

    /// <summary>
    /// Variant group feature flag
    /// </summary>
    public string VariantFlagName { get; set; } = string.Empty;

    /// <summary>
    /// When experiment started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When experiment ends (nullable - no end date)
    /// </summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>
    /// Experiment metrics/results
    /// </summary>
    public Dictionary<string, double> Metrics { get; set; } = new();
}

/// <summary>
/// A/B testing service
/// </summary>
public class ExperimentService
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<ExperimentService> _logger;

    public ExperimentService(
        IFeatureFlagService featureFlagService,
        ILogger<ExperimentService> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<string> GetVariantAsync(string experimentId, FeatureFlagContext context)
    {
        var hash = context.GetConsistentHashCode();
        var isInVariant = Math.Abs(hash % 100) < 50; // 50% split

        var variantName = isInVariant ? "variant_treatment" : "control_baseline";

        _logger.LogInformation(
            "User assigned to variant: {Experiment} -> {Variant} ({User})",
            experimentId,
            variantName,
            context.UserId);

        return variantName;
    }
}

/// <summary>
/// Extension methods
/// </summary>
public static class FeatureFlagsExtensions
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagService, InMemoryFeatureFlagService>();
        services.AddSingleton<ExperimentService>();
        services.AddSingleton<FeatureFlagHelper>();
        return services;
    }

    public static IApplicationBuilder UseFeatureFlags(this IApplicationBuilder app)
    {
        return app.UseMiddleware<FeatureFlagMiddleware>();
    }
}

/// <summary>
/// Example usage in controller
/// </summary>
[ApiController]
[Route("api/features")]
public class FeatureFlagsExampleController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagsExampleController(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Gets new workflow UI (behind feature flag)
    /// </summary>
    [HttpGet("workflows/new")]
    [RequireFeatureFlag("new-workflow-ui")]
    public IActionResult GetNewWorkflowUI()
    {
        return Ok(new { message = "New workflow UI" });
    }

    /// <summary>
    /// Gets advanced analytics (premium feature)
    /// </summary>
    [HttpGet("analytics/advanced")]
    [RequireFeatureFlag("advanced-analytics")]
    public IActionResult GetAdvancedAnalytics()
    {
        return Ok(new { message = "Advanced analytics" });
    }

    /// <summary>
    /// Checks if feature is enabled
    /// </summary>
    [HttpGet("{featureName}/enabled")]
    public async Task<IActionResult> IsFeatureEnabledAsync(
        string featureName)
    {
        var context = HttpContext.Items["FeatureFlagContext"] as FeatureFlagContext
            ?? new FeatureFlagContext();

        var result = await _featureFlagService.EvaluateAsync(featureName, context)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Admin: Updates feature flag
    /// </summary>
    [HttpPut("{featureName}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateFeatureFlagAsync(
        string featureName,
        [FromBody] FeatureFlag flag)
    {
        flag.Name = featureName;
        await _featureFlagService.UpdateFlagAsync(flag).ConfigureAwait(false);
        return Ok(flag);
    }
}
