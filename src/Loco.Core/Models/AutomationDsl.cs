using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Models;

/// <summary>
/// Domain Specific Language (DSL) for automation rules
/// Clean architecture following Robert C. Martin principles
/// </summary>
public class AutomationDsl
{
    /// <summary>
    /// Automation rule definition
    /// </summary>
    public class Rule
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
        
        // Alias for compatibility
        [JsonIgnore]
        public bool IsEnabled => Enabled;
        
        [JsonPropertyName("trigger")]
        public TriggerDefinition Trigger { get; set; } = new();
        
        [JsonPropertyName("conditions")]
        public List<ConditionDefinition> Conditions { get; set; } = new();
        
        [JsonPropertyName("actions")]
        public List<ActionDefinition> Actions { get; set; } = new();
        
        [JsonPropertyName("variables")]
        public Dictionary<string, object> Variables { get; set; } = new();
        
        [JsonPropertyName("permissions")]
        public PermissionSet Permissions { get; set; } = new();
        
        [JsonPropertyName("metadata")]
        public RuleMetadata Metadata { get; set; } = new();
        
        // Search/filtering properties (computed from metadata)
        [JsonIgnore]
        public DateTime? CreatedAt => Metadata?.CreatedAt;
        
        [JsonIgnore]
        public DateTime? ModifiedAt => Metadata?.UpdatedAt;
        
        [JsonIgnore]
        public List<string> Tags => Metadata?.Tags ?? new List<string>();
        
        [JsonPropertyName("executionCount")]
        public int? ExecutionCount { get; set; } = 0;
    }

    /// <summary>
    /// Trigger definition
    /// </summary>
    public class TriggerDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("delay")]
        public int? DelayMs { get; set; }
        
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 5;
    }

    /// <summary>
    /// Condition definition
    /// </summary>
    public class ConditionDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("operator")]
        public string Operator { get; set; } = string.Empty;
        
        [JsonPropertyName("value")]
        public object Value { get; set; } = new();
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("negate")]
        public bool Negate { get; set; } = false;
    }

    /// <summary>
    /// Action definition
    /// </summary>
    public class ActionDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("timeout")]
        public int? TimeoutMs { get; set; }
        
        [JsonPropertyName("retryCount")]
        public int RetryCount { get; set; } = 0;
        
        [JsonPropertyName("continueOnError")]
        public bool ContinueOnError { get; set; } = false;
    }

    /// <summary>
    /// Permission set for rule execution
    /// </summary>
    public class PermissionSet
    {
        [JsonPropertyName("network")]
        public bool Network { get; set; } = false;
        
        [JsonPropertyName("fileSystem")]
        public bool FileSystem { get; set; } = false;
        
        [JsonPropertyName("shell")]
        public bool Shell { get; set; } = false;
        
        [JsonPropertyName("llm")]
        public bool Llm { get; set; } = false;
        
        [JsonPropertyName("notification")]
        public bool Notification { get; set; } = true;
        
        [JsonPropertyName("allowedDomains")]
        public List<string> AllowedDomains { get; set; } = new();
        
        [JsonPropertyName("allowedPaths")]
        public List<string> AllowedPaths { get; set; } = new();
    }

    /// <summary>
    /// Rule metadata
    /// </summary>
    public class RuleMetadata
    {
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";
        
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;
        
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }
}

/// <summary>
/// Legacy natural language to DSL converter (kept for reference)
/// Uses LLM for intelligent parsing
/// </summary>
internal class LegacyNaturalLanguageToDslConverter
{
    private readonly ILogger<LegacyNaturalLanguageToDslConverter> _logger;
    private readonly Dictionary<string, TriggerTemplate> _triggerTemplates;
    private readonly Dictionary<string, ActionTemplate> _actionTemplates;

    public LegacyNaturalLanguageToDslConverter(ILogger<LegacyNaturalLanguageToDslConverter> logger)
    {
        _logger = logger;
        _triggerTemplates = InitializeTriggerTemplates();
        _actionTemplates = InitializeActionTemplates();
    }

    /// <summary>
    /// Convert natural language to DSL rule
    /// </summary>
    public async Task<ConversionResultDetailed> ConvertAsync(string naturalLanguage, string llmModelId = null)
    {
        try
        {
            _logger.LogInformation("Converting natural language to DSL: {Input}", naturalLanguage);
            
            // First, try pattern matching for common patterns
            var patternResult = TryPatternMatching(naturalLanguage);
            if (patternResult != null)
            {
                return new ConversionResultDetailed
                {
                    Success = true,
                    Rules = new[] { patternResult },
                    Confidence = 0.9f,
                    Method = "PatternMatching"
                };
            }
            
            // If pattern matching fails, use LLM if available
            if (!string.IsNullOrEmpty(llmModelId))
            {
                var llmResult = await ConvertWithLlmAsync(naturalLanguage, llmModelId);
                if (llmResult != null)
                {
                    return llmResult;
                }
            }
            
            // Fallback to keyword extraction
            var keywordResult = ExtractKeywordsAndBuildRule(naturalLanguage);
            return new ConversionResultDetailed
            {
                Success = keywordResult != null,
                Rules = keywordResult != null ? new[] { keywordResult } : Array.Empty<AutomationDsl.Rule>(),
                Confidence = 0.5f,
                Method = "KeywordExtraction"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting natural language to DSL");
            return new ConversionResultDetailed
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Try pattern matching for common automation patterns
    /// </summary>
    private AutomationDsl.Rule TryPatternMatching(string input)
    {
        var lowerInput = input.ToLower();
        
        // Pattern: "毎日X時にY"
        if (lowerInput.Contains("毎日") && (lowerInput.Contains("時") || lowerInput.Contains(":")
))
        {
            return CreateTimeBasedRule(input);
        }
        
        // Pattern: "XアプリがYしたらZ"
        if (lowerInput.Contains("したら") || lowerInput.Contains("場合"))
        {
            return CreateConditionalRule(input);
        }
        
        // Pattern: "X分ごとにY"
        if (lowerInput.Contains("ごとに") || lowerInput.Contains("間隔"))
        {
            return CreateIntervalRule(input);
        }
        
        return null;
    }

    /// <summary>
    /// Create time-based rule from natural language
    /// </summary>
    private AutomationDsl.Rule CreateTimeBasedRule(string input)
    {
        var rule = new AutomationDsl.Rule
        {
            Name = "時刻ベースの自動化",
            Description = input,
            Trigger = new AutomationDsl.TriggerDefinition
            {
                Type = "time",
                Parameters = ExtractTimeParameters(input)
            }
        };
        
        // Extract actions
        var actions = ExtractActions(input);
        rule.Actions = actions;
        
        // Set permissions based on actions
        UpdatePermissions(rule);
        
        return rule;
    }

    /// <summary>
    /// Create conditional rule from natural language
    /// </summary>
    private AutomationDsl.Rule CreateConditionalRule(string input)
    {
        var rule = new AutomationDsl.Rule
        {
            Name = "条件付き自動化",
            Description = input
        };
        
        // Extract trigger and conditions
        var triggerPart = ExtractTriggerPart(input);
        if (triggerPart != null)
        {
            rule.Trigger = ParseTrigger(triggerPart);
        }
        
        // Extract actions
        var actions = ExtractActions(input);
        rule.Actions = actions;
        
        // Set permissions
        UpdatePermissions(rule);
        
        return rule;
    }

    /// <summary>
    /// Create interval-based rule
    /// </summary>
    private AutomationDsl.Rule CreateIntervalRule(string input)
    {
        var rule = new AutomationDsl.Rule
        {
            Name = "間隔ベースの自動化",
            Description = input,
            Trigger = new AutomationDsl.TriggerDefinition
            {
                Type = "interval",
                Parameters = ExtractIntervalParameters(input)
            }
        };
        
        // Extract actions
        var actions = ExtractActions(input);
        rule.Actions = actions;
        
        // Set permissions
        UpdatePermissions(rule);
        
        return rule;
    }

    /// <summary>
    /// Extract time parameters from input
    /// </summary>
    private Dictionary<string, object> ExtractTimeParameters(string input)
    {
        var parameters = new Dictionary<string, object>();
        
        // Extract hour and minute
        var timePattern = @"(\d{1,2})[:時](\d{0,2})?";
        var match = System.Text.RegularExpressions.Regex.Match(input, timePattern);
        if (match.Success)
        {
            parameters["hour"] = int.Parse(match.Groups[1].Value);
            if (match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                parameters["minute"] = int.Parse(match.Groups[2].Value);
            }
            else
            {
                parameters["minute"] = 0;
            }
        }
        
        return parameters;
    }

    /// <summary>
    /// Extract interval parameters
    /// </summary>
    private Dictionary<string, object> ExtractIntervalParameters(string input)
    {
        var parameters = new Dictionary<string, object>();
        
        // Extract interval value
        var intervalPattern = @"(\d+)\s*(分|時間|秒)";
        var match = System.Text.RegularExpressions.Regex.Match(input, intervalPattern);
        if (match.Success)
        {
            var value = int.Parse(match.Groups[1].Value);
            var unit = match.Groups[2].Value;
            
            var intervalMs = unit switch
            {
                "秒" => value * 1000,
                "分" => value * 60 * 1000,
                "時間" => value * 60 * 60 * 1000,
                _ => value * 60 * 1000
            };
            
            parameters["intervalMs"] = intervalMs;
        }
        
        return parameters;
    }

    /// <summary>
    /// Extract actions from input
    /// </summary>
    private List<AutomationDsl.ActionDefinition> ExtractActions(string input)
    {
        var actions = new List<AutomationDsl.ActionDefinition>();
        var lowerInput = input.ToLower();
        
        // Check for notification action
        if (lowerInput.Contains("通知") || lowerInput.Contains("知らせ"))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>
                {
                    ["title"] = "自動通知",
                    ["message"] = ExtractNotificationMessage(input)
                }
            });
        }
        
        // Check for TTS action
        if (lowerInput.Contains("読み上げ") || lowerInput.Contains("話す"))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "tts",
                Parameters = new Dictionary<string, object>
                {
                    ["text"] = ExtractTtsText(input)
                }
            });
        }
        
        // Check for app launch
        if (lowerInput.Contains("起動") || lowerInput.Contains("開く"))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "launchApp",
                Parameters = new Dictionary<string, object>
                {
                    ["appName"] = ExtractAppName(input)
                }
            });
        }
        
        // Check for HTTP request
        if (lowerInput.Contains("http") || lowerInput.Contains("api") || lowerInput.Contains("送信"))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "httpRequest",
                Parameters = new Dictionary<string, object>
                {
                    ["method"] = "GET",
                    ["url"] = ExtractUrl(input)
                }
            });
        }
        
        // Default action if none detected
        if (actions.Count == 0)
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "log",
                Parameters = new Dictionary<string, object>
                {
                    ["message"] = input
                }
            });
        }
        
        return actions;
    }

    /// <summary>
    /// Update permissions based on actions
    /// </summary>
    private void UpdatePermissions(AutomationDsl.Rule rule)
    {
        foreach (var action in rule.Actions)
        {
            switch (action.Type)
            {
                case "httpRequest":
                    rule.Permissions.Network = true;
                    break;
                case "fileWrite":
                case "fileRead":
                case "fileDelete":
                    rule.Permissions.FileSystem = true;
                    break;
                case "shell":
                    rule.Permissions.Shell = true;
                    break;
                case "llmQuery":
                    rule.Permissions.Llm = true;
                    break;
                case "notification":
                case "tts":
                    rule.Permissions.Notification = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Extract trigger part from conditional input
    /// </summary>
    private string ExtractTriggerPart(string input)
    {
        var patterns = new[] { "したら", "場合", "とき", "なら" };
        foreach (var pattern in patterns)
        {
            var index = input.IndexOf(pattern);
            if (index > 0)
            {
                return input.Substring(0, index);
            }
        }
        return null;
    }

    /// <summary>
    /// Parse trigger from text
    /// </summary>
    private AutomationDsl.TriggerDefinition ParseTrigger(string triggerText)
    {
        var lower = triggerText.ToLower();
        
        if (lower.Contains("通知"))
        {
            return new AutomationDsl.TriggerDefinition
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>()
            };
        }
        
        if (lower.Contains("アプリ"))
        {
            return new AutomationDsl.TriggerDefinition
            {
                Type = "appLaunch",
                Parameters = new Dictionary<string, object>
                {
                    ["appName"] = ExtractAppName(triggerText)
                }
            };
        }
        
        return new AutomationDsl.TriggerDefinition
        {
            Type = "manual",
            Parameters = new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Extract notification message
    /// </summary>
    private string ExtractNotificationMessage(string input)
    {
        // Simple extraction - can be improved with NLP
        return "自動化ルールが実行されました";
    }

    /// <summary>
    /// Extract TTS text
    /// </summary>
    private string ExtractTtsText(string input)
    {
        // Extract text between quotes if present
        var quotePattern = "「(.+?)」";
        var match = System.Text.RegularExpressions.Regex.Match(input, quotePattern);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return "テキスト読み上げ";
    }

    /// <summary>
    /// Extract app name
    /// </summary>
    private string ExtractAppName(string input)
    {
        // Common app names
        var apps = new[] { "chrome", "edge", "firefox", "メモ帳", "notepad", "calculator", "電卓" };
        var lower = input.ToLower();
        
        foreach (var app in apps)
        {
            if (lower.Contains(app))
            {
                return app;
            }
        }
        
        return "unknown";
    }

    /// <summary>
    /// Extract URL from input
    /// </summary>
    private string ExtractUrl(string input)
    {
        var urlPattern = @"https?://[^\s]+";
        var match = System.Text.RegularExpressions.Regex.Match(input, urlPattern);
        if (match.Success)
        {
            return match.Value;
        }
        
        return "http://localhost";
    }

    /// <summary>
    /// Extract keywords and build rule
    /// </summary>
    private AutomationDsl.Rule ExtractKeywordsAndBuildRule(string input)
    {
        var rule = new AutomationDsl.Rule
        {
            Name = "キーワードベース自動化",
            Description = input,
            Trigger = new AutomationDsl.TriggerDefinition
            {
                Type = "manual",
                Parameters = new Dictionary<string, object>()
            }
        };
        
        rule.Actions = ExtractActions(input);
        UpdatePermissions(rule);
        
        return rule;
    }

    /// <summary>
    /// Convert using LLM (placeholder for actual LLM integration)
    /// </summary>
    private async Task<ConversionResultDetailed> ConvertWithLlmAsync(string naturalLanguage, string modelId)
    {
        // This would integrate with the actual LLM service
        // For now, return null to fall back to other methods
        await Task.Delay(1);
        return null;
    }

    /// <summary>
    /// Initialize trigger templates
    /// </summary>
    private Dictionary<string, TriggerTemplate> InitializeTriggerTemplates()
    {
        return new Dictionary<string, TriggerTemplate>
        {
            ["time"] = new TriggerTemplate { Type = "time", Keywords = new[] { "時", "毎日", "毎週", "AM", "PM" } },
            ["notification"] = new TriggerTemplate { Type = "notification", Keywords = new[] { "通知", "メッセージ", "受信" } },
            ["app"] = new TriggerTemplate { Type = "appLaunch", Keywords = new[] { "アプリ", "起動", "開く" } },
            ["file"] = new TriggerTemplate { Type = "fileChange", Keywords = new[] { "ファイル", "保存", "変更" } }
        };
    }

    /// <summary>
    /// Initialize action templates
    /// </summary>
    private Dictionary<string, ActionTemplate> InitializeActionTemplates()
    {
        return new Dictionary<string, ActionTemplate>
        {
            ["notification"] = new ActionTemplate { Type = "notification", Keywords = new[] { "通知", "知らせる", "アラート" } },
            ["tts"] = new ActionTemplate { Type = "tts", Keywords = new[] { "読み上げ", "話す", "音声" } },
            ["http"] = new ActionTemplate { Type = "httpRequest", Keywords = new[] { "送信", "API", "HTTP" } },
            ["file"] = new ActionTemplate { Type = "fileWrite", Keywords = new[] { "保存", "書き込み", "ファイル" } }
        };
    }

    // Helper classes
    private class TriggerTemplate
    {
        public string Type { get; set; }
        public string[] Keywords { get; set; }
    }

    private class ActionTemplate
    {
        public string Type { get; set; }
        public string[] Keywords { get; set; }
    }
}

/// <summary>
/// Detailed conversion result (legacy converter)
/// </summary>
internal class ConversionResultDetailed
{
    public bool Success { get; set; }
    public AutomationDsl.Rule[] Rules { get; set; }
    public float Confidence { get; set; }
    public string Method { get; set; }
    public string ErrorMessage { get; set; }
}