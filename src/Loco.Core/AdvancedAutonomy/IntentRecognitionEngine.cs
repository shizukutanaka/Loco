// Phase 14: Intent Recognition and Auto-Adaptation Engine
// Understands user intent and automatically adjusts workflow behavior
// Pattern recognition, behavior learning, and intelligent adaptation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.AdvancedAutonomy;

/// <summary>
/// User intent detected from actions and context
/// </summary>
public class UserIntent
{
    public string IntentId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string IntentType { get; set; } = string.Empty; // speed_up, reduce_cost, increase_reliability, improve_usability, debug, optimize
    public string IntentDescription { get; set; } = string.Empty;
    public double ConfidenceLevel { get; set; } // 0-100
    public List<string> ContextClues { get; set; } = new(); // Observations that led to intent inference
    public Dictionary<string, object> ImpliedRequirements { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Behavioral pattern discovered from usage
/// </summary>
public class BehavioralPattern
{
    public string PatternId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty; // execution_time, resource_usage, error_handling, input_patterns, output_patterns
    public string Description { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public double FrequencyPercent { get; set; }
    public List<string> CharacteristicBehaviors { get; set; } = new();
    public List<string> AssociatedOutcomes { get; set; } = new();
    public double PatternStrength { get; set; } // 0-100
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Automatic adaptation decision
/// </summary>
public class AdaptationDecision
{
    public string DecisionId { get; set; } = Guid.NewGuid().ToString();
    public string IntentId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public List<string> AdaptationActions { get; set; } = new();
    public Dictionary<string, object> ConfigurationChanges { get; set; } = new();
    public string ReasonForAdaptation { get; set; } = string.Empty;
    public double ConfidenceInAdaptation { get; set; } // 0-100
    public string Status { get; set; } = string.Empty; // pending, proposed, approved, applied, reverted
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User preference learning
/// </summary>
public class UserPreference
{
    public string PreferenceId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty; // performance, cost, reliability, usability, automation_level
    public string PreferenceValue { get; set; } = string.Empty;
    public double Weight { get; set; } // How important is this preference (0-1)
    public int TimesMentioned { get; set; }
    public List<DateTime> ObservationDates { get; set; } = new();
    public bool IsExplicit { get; set; } // User stated vs inferred
    public double InferenceConfidence { get; set; } // 0-100 for inferred preferences
}

/// <summary>
/// Workflow adaptation history
/// </summary>
public class AdaptationHistory
{
    public string HistoryId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime AdaptedAt { get; set; }
    public string AdaptationReason { get; set; } = string.Empty;
    public List<string> ChangesApplied { get; set; } = new();
    public bool WasSuccessful { get; set; }
    public double PerformanceChangePercent { get; set; }
    public List<string> UserFeedback { get; set; } = new();
    public double UserSatisfactionScore { get; set; } // 0-100 if feedback provided
}

/// <summary>
/// Context information for intent inference
/// </summary>
public class ExecutionContext
{
    public string ContextId { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowId { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public Dictionary<string, object> ContextMetrics { get; set; } = new();
    public List<string> UserActions { get; set; } = new();
    public List<string> SystemObservations { get; set; } = new();
    public Dictionary<string, string> EnvironmentFactors { get; set; } = new();
}

/// <summary>
/// Intent recognition interface
/// </summary>
public interface IIntentRecognitionEngine
{
    // Intent detection
    Task<UserIntent> DetectUserIntentAsync(
        string userId,
        string workflowId,
        List<string> contextClues,
        CancellationToken ct = default);

    Task<List<UserIntent>> GetUserIntentsAsync(
        string userId,
        CancellationToken ct = default);

    // Pattern learning
    Task<List<BehavioralPattern>> DiscoverBehavioralPatternsAsync(
        string userId,
        CancellationToken ct = default);

    Task<BehavioralPattern> GetPatternAsync(
        string patternId,
        CancellationToken ct = default);

    // Preference learning
    Task<UserPreference> LearnUserPreferenceAsync(
        string userId,
        string preferenceType,
        string preferenceValue,
        bool isExplicit = false,
        CancellationToken ct = default);

    Task<List<UserPreference>> GetUserPreferencesAsync(
        string userId,
        CancellationToken ct = default);

    // Adaptation decisions
    Task<AdaptationDecision> GenerateAdaptationAsync(
        string intendId,
        string workflowId,
        CancellationToken ct = default);

    Task<bool> ApplyAdaptationAsync(
        string decisionId,
        CancellationToken ct = default);

    // Adaptation tracking
    Task<List<AdaptationHistory>> GetAdaptationHistoryAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<bool> RecordAdaptationFeedbackAsync(
        string historyId,
        double satisfactionScore,
        List<string> feedback,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> GetIntentRecognitionAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// Intent recognition engine implementation
/// </summary>
public class IntentRecognitionEngine : IIntentRecognitionEngine
{
    private readonly ILogger<IntentRecognitionEngine> _logger;
    private readonly Dictionary<string, List<UserIntent>> _intents;
    private readonly Dictionary<string, List<BehavioralPattern>> _patterns;
    private readonly Dictionary<string, List<UserPreference>> _preferences;
    private readonly Dictionary<string, AdaptationDecision> _adaptations;
    private readonly Dictionary<string, List<AdaptationHistory>> _history;

    public IntentRecognitionEngine(ILogger<IntentRecognitionEngine> logger)
    {
        _logger = logger;
        _intents = new Dictionary<string, List<UserIntent>>();
        _patterns = new Dictionary<string, List<BehavioralPattern>>();
        _preferences = new Dictionary<string, List<UserPreference>>();
        _adaptations = new Dictionary<string, AdaptationDecision>();
        _history = new Dictionary<string, List<AdaptationHistory>>();
    }

    // Intent detection
    public async Task<UserIntent> DetectUserIntentAsync(
        string userId,
        string workflowId,
        List<string> contextClues,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate analysis

        var intent = new UserIntent
        {
            UserId = userId,
            WorkflowId = workflowId,
            IntentType = InferIntentType(contextClues),
            IntentDescription = GenerateIntentDescription(contextClues),
            ConfidenceLevel = CalculateIntentConfidence(contextClues),
            ContextClues = contextClues,
            ImpliedRequirements = GenerateImpliedRequirements(contextClues)
        };

        if (!_intents.ContainsKey(userId))
        {
            _intents[userId] = new List<UserIntent>();
        }

        _intents[userId].Add(intent);

        _logger.LogInformation(
            \"User intent detected: UserId={UserId}, WorkflowId={WorkflowId}, Type={Type}, Confidence={Conf:F1}%\",
            userId, workflowId, intent.IntentType, intent.ConfidenceLevel);

        return intent;
    }

    public async Task<List<UserIntent>> GetUserIntentsAsync(
        string userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_intents.TryGetValue(userId, out var intents))
        {
            return intents.OrderByDescending(i => i.DetectedAt).ToList();
        }

        return new List<UserIntent>();
    }

    // Pattern learning
    public async Task<List<BehavioralPattern>> DiscoverBehavioralPatternsAsync(
        string userId,
        CancellationToken ct = default)
    {
        await Task.Delay(200, ct); // Simulate discovery

        var patterns = new List<BehavioralPattern>
        {
            new BehavioralPattern
            {
                UserId = userId,
                PatternType = \"execution_time\",
                Description = \"Prefers fast execution with minimal latency\",
                OccurrenceCount = 142,
                FrequencyPercent = 78.5,
                CharacteristicBehaviors = new List<string>
                {
                    \"Runs workflows during business hours (9-5)\",
                    \"Prefers parallel execution over sequential\",
                    \"Sets aggressive timeout values\"
                },
                AssociatedOutcomes = new List<string> { \"High throughput\", \"Some timeouts\", \"Resource utilization\" },
                PatternStrength = 85.0
            },
            new BehavioralPattern
            {
                UserId = userId,
                PatternType = \"resource_usage\",
                Description = \"Cost-conscious with budget awareness\",
                OccurrenceCount = 95,
                FrequencyPercent = 52.3,
                CharacteristicBehaviors = new List<string>
                {
                    \"Monitors cost dashboards regularly\",
                    \"Enables cost optimization recommendations\",
                    \"Uses scheduled execution for off-peak processing\"
                },
                AssociatedOutcomes = new List<string> { \"Lower operational costs\", \"Delayed non-critical tasks\", \"Budget compliance\" },
                PatternStrength = 72.0
            },
            new BehavioralPattern
            {
                UserId = userId,
                PatternType = \"error_handling\",
                Description = \"Strict error handling preferences\",
                OccurrenceCount = 108,
                FrequencyPercent = 59.7,
                CharacteristicBehaviors = new List<string>
                {
                    \"Enables comprehensive error logging\",
                    \"Sets up alerting for failures\",
                    \"Reviews error reports after execution\"
                },
                AssociatedOutcomes = new List<string> { \"Better issue detection\", \"Faster debugging\", \"Higher visibility\" },
                PatternStrength = 79.0
            }
        };

        if (!_patterns.ContainsKey(userId))
        {
            _patterns[userId] = new List<BehavioralPattern>();
        }

        _patterns[userId].AddRange(patterns);

        _logger.LogInformation(
            \"Behavioral patterns discovered: UserId={UserId}, PatternCount={Count}\",
            userId, patterns.Count);

        return patterns;
    }

    public async Task<BehavioralPattern> GetPatternAsync(
        string patternId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var patterns in _patterns.Values)
        {
            var pattern = patterns.FirstOrDefault(p => p.PatternId == patternId);
            if (pattern != null)
                return pattern;
        }

        return null;
    }

    // Preference learning
    public async Task<UserPreference> LearnUserPreferenceAsync(
        string userId,
        string preferenceType,
        string preferenceValue,
        bool isExplicit = false,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var preference = new UserPreference
        {
            UserId = userId,
            PreferenceType = preferenceType,
            PreferenceValue = preferenceValue,
            Weight = isExplicit ? 1.0 : 0.7,
            TimesMentioned = 1,
            IsExplicit = isExplicit,
            InferenceConfidence = isExplicit ? 100.0 : (60.0 + Random.Shared.NextDouble() * 35),
            ObservationDates = new List<DateTime> { DateTime.UtcNow }
        };

        if (!_preferences.ContainsKey(userId))
        {
            _preferences[userId] = new List<UserPreference>();
        }

        _preferences[userId].Add(preference);

        _logger.LogInformation(
            \"User preference learned: UserId={UserId}, Type={Type}, Value={Value}, Explicit={Explicit}, Confidence={Conf:F1}%\",
            userId, preferenceType, preferenceValue, isExplicit, preference.InferenceConfidence);

        return preference;
    }

    public async Task<List<UserPreference>> GetUserPreferencesAsync(
        string userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_preferences.TryGetValue(userId, out var preferences))
        {
            return preferences.OrderByDescending(p => p.Weight * p.TimesMentioned).ToList();
        }

        return new List<UserPreference>();
    }

    // Adaptation decisions
    public async Task<AdaptationDecision> GenerateAdaptationAsync(
        string intentId,
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.Delay(150, ct); // Simulate generation

        // Find the intent
        UserIntent intent = null;
        foreach (var intents in _intents.Values)
        {
            intent = intents.FirstOrDefault(i => i.IntentId == intentId);
            if (intent != null)
                break;
        }

        if (intent == null)
            return null;

        var decision = new AdaptationDecision
        {
            IntentId = intentId,
            WorkflowId = workflowId,
            AdaptationActions = GenerateAdaptationActions(intent.IntentType),
            ConfigurationChanges = GenerateConfigurationChanges(intent.IntentType),
            ReasonForAdaptation = $\"Detected user intent: {intent.IntentDescription}\",
            ConfidenceInAdaptation = intent.ConfidenceLevel * 0.95,
            Status = \"proposed\",
            ProposedAt = DateTime.UtcNow
        };

        _adaptations[decision.DecisionId] = decision;

        _logger.LogInformation(
            \"Adaptation decision generated: IntentId={IntId}, WorkflowId={WorkflowId}, Actions={Count}, Confidence={Conf:F1}%\",
            intentId, workflowId, decision.AdaptationActions.Count, decision.ConfidenceInAdaptation);

        return decision;
    }

    public async Task<bool> ApplyAdaptationAsync(
        string decisionId,
        CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate application

        if (_adaptations.TryGetValue(decisionId, out var decision))
        {
            decision.Status = \"applied\";

            if (!_history.ContainsKey(decision.WorkflowId))
            {
                _history[decision.WorkflowId] = new List<AdaptationHistory>();
            }

            var historyEntry = new AdaptationHistory
            {
                WorkflowId = decision.WorkflowId,
                AdaptedAt = DateTime.UtcNow,
                AdaptationReason = decision.ReasonForAdaptation,
                ChangesApplied = decision.AdaptationActions,
                WasSuccessful = true,
                PerformanceChangePercent = Random.Shared.NextDouble() * 30 - 5
            };

            _history[decision.WorkflowId].Add(historyEntry);

            _logger.LogInformation(
                \"Adaptation applied: DecisionId={DecId}, WorkflowId={WorkflowId}, ActionsApplied={Count}\",
                decisionId, decision.WorkflowId, decision.AdaptationActions.Count);

            return true;
        }

        return false;
    }

    // Adaptation tracking
    public async Task<List<AdaptationHistory>> GetAdaptationHistoryAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        if (_history.TryGetValue(workflowId, out var history))
        {
            return history.OrderByDescending(h => h.AdaptedAt).ToList();
        }

        return new List<AdaptationHistory>();
    }

    public async Task<bool> RecordAdaptationFeedbackAsync(
        string historyId,
        double satisfactionScore,
        List<string> feedback,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        foreach (var historyList in _history.Values)
        {
            var entry = historyList.FirstOrDefault(h => h.HistoryId == historyId);
            if (entry != null)
            {
                entry.UserSatisfactionScore = Math.Clamp(satisfactionScore, 0, 100);
                entry.UserFeedback = feedback;

                _logger.LogInformation(
                    \"Adaptation feedback recorded: HistoryId={HistId}, Satisfaction={Score:F1}%, FeedbackCount={Count}\",
                    historyId, satisfactionScore, feedback.Count);

                return true;
            }
        }

        return false;
    }

    public async Task<Dictionary<string, object>> GetIntentRecognitionAnalyticsAsync(
        string tenantId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var allIntents = _intents.Values.SelectMany(i => i).ToList();
        var allPatterns = _patterns.Values.SelectMany(p => p).ToList();
        var allPreferences = _preferences.Values.SelectMany(p => p).ToList();
        var allHistory = _history.Values.SelectMany(h => h).ToList();

        var successfulAdaptations = allHistory.Count(h => h.WasSuccessful);
        var avgSatisfaction = allHistory.Count(h => h.UserSatisfactionScore > 0)
            ? allHistory.Where(h => h.UserSatisfactionScore > 0).Average(h => h.UserSatisfactionScore)
            : 0;

        return new Dictionary<string, object>
        {
            [\"total_intents_detected\"] = allIntents.Count,
            [\"intent_detection_accuracy\"] = allIntents.Count > 0 ? allIntents.Average(i => i.ConfidenceLevel) : 0,
            [\"patterns_discovered\"] = allPatterns.Count,
            [\"average_pattern_strength\"] = allPatterns.Count > 0 ? allPatterns.Average(p => p.PatternStrength) : 0,
            [\"user_preferences_learned\"] = allPreferences.Count,
            [\"explicit_vs_inferred_ratio\"] = allPreferences.Count > 0
                ? (allPreferences.Count(p => p.IsExplicit) / (double)allPreferences.Count)
                : 0,
            [\"total_adaptations\"] = allHistory.Count,
            [\"successful_adaptations\"] = successfulAdaptations,
            [\"adaptation_success_rate\"] = allHistory.Count > 0 ? (successfulAdaptations / (double)allHistory.Count) * 100 : 0,
            [\"average_user_satisfaction\"] = avgSatisfaction,
            [\"most_common_intent_type\"] = allIntents.GroupBy(i => i.IntentType).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? \"none\"
        };
    }

    // Helpers
    private string InferIntentType(List<string> contextClues)
    {
        var cluesLower = string.Join(\" \", contextClues).ToLower();

        return cluesLower switch
        {
            _ when cluesLower.Contains(\"fast\") || cluesLower.Contains(\"speed\") => \"speed_up\",
            _ when cluesLower.Contains(\"cost\") || cluesLower.Contains(\"budget\") => \"reduce_cost\",
            _ when cluesLower.Contains(\"reliable\") || cluesLower.Contains(\"error\") => \"increase_reliability\",
            _ when cluesLower.Contains(\"easy\") || cluesLower.Contains(\"user\") => \"improve_usability\",
            _ when cluesLower.Contains(\"debug\") || cluesLower.Contains(\"issue\") => \"debug\",
            _ => \"optimize\"
        };
    }

    private string GenerateIntentDescription(List<string> contextClues)
    {
        var intentType = InferIntentType(contextClues);
        return intentType switch
        {
            \"speed_up\" => \"User wants faster workflow execution\",
            \"reduce_cost\" => \"User is focused on cost optimization\",
            \"increase_reliability\" => \"User prioritizes workflow reliability\",
            \"improve_usability\" => \"User wants better user experience\",
            \"debug\" => \"User is investigating workflow issues\",
            _ => \"User wants to optimize workflow\"
        };
    }

    private double CalculateIntentConfidence(List<string> contextClues)
    {
        var baseConfidence = 60.0;
        var clueBonus = Math.Min(35, contextClues.Count * 5);
        return Math.Min(95, baseConfidence + clueBonus);
    }

    private Dictionary<string, object> GenerateImpliedRequirements(List<string> contextClues)
    {
        return new Dictionary<string, object>
        {
            [\"auto_optimize\"] = true,
            [\"monitor_progress\"] = true,
            [\"provide_feedback\"] = true,
            [\"enable_learning\"] = true
        };
    }

    private List<string> GenerateAdaptationActions(string intentType)
    {
        return intentType switch
        {
            \"speed_up\" => new List<string>
            {
                \"Increase parallelism level\",
                \"Reduce timeout values\",
                \"Enable result caching\",
                \"Optimize resource allocation\"
            },
            \"reduce_cost\" => new List<string>
            {
                \"Scale down off-peak resources\",
                \"Enable cost-saving optimizations\",
                \"Batch process where possible\",
                \"Use cheaper alternatives for non-critical tasks\"
            },
            \"increase_reliability\" => new List<string>
            {
                \"Add redundancy\",
                \"Implement comprehensive error handling\",
                \"Enable automatic recovery\",
                \"Increase monitoring and alerting\"
            },
            \"improve_usability\" => new List<string>
            {
                \"Simplify workflow interface\",
                \"Add helpful progress indicators\",
                \"Improve error messages\",
                \"Enable one-click operations\"
            },
            _ => new List<string> { \"Analyze and optimize workflow\", \"Apply best practices\" }
        };
    }

    private Dictionary<string, object> GenerateConfigurationChanges(string intentType)
    {
        return intentType switch
        {
            \"speed_up\" => new Dictionary<string, object>
            {
                [\"parallelism\"] = 8,
                [\"timeout_ms\"] = 5000,
                [\"cache_enabled\"] = true
            },
            \"reduce_cost\" => new Dictionary<string, object>
            {
                [\"peak_hours_scaling\"] = true,
                [\"batch_processing\"] = true,
                [\"cost_optimization\"] = true
            },
            \"increase_reliability\" => new Dictionary<string, object>
            {
                [\"retry_enabled\"] = true,
                [\"redundancy_level\"] = 2,
                [\"health_monitoring\"] = true
            },
            _ => new Dictionary<string, object> { [\"auto_optimize\"] = true }
        };
    }
}
