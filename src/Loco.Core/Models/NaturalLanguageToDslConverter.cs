using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Models;

/// <summary>
/// Natural language to DSL converter - Simple keyword-based implementation
/// Following KISS principle (Keep It Simple, Stupid)
/// </summary>
public class NaturalLanguageToDslConverter
{
    private readonly ILogger<NaturalLanguageToDslConverter> _logger;
    private readonly Dictionary<string, List<string>> _keywordMappings = new();
    
    public NaturalLanguageToDslConverter(ILogger<NaturalLanguageToDslConverter> logger = null)
    {
        _logger = logger;
        InitializeKeywordMappings();
    }
    
    private void InitializeKeywordMappings()
    {
        _keywordMappings.Clear();
        _keywordMappings["time"] = new() { "毎朝", "毎晩", "毎日", "時", "分", "朝", "昼", "夜", "every morning", "every evening", "daily", "hourly" };
        _keywordMappings["file"] = new() { "ファイル", "フォルダ", "ディレクトリ", "保存", "コピー", "移動", "削除", "file", "folder", "directory", "save", "copy", "move", "delete" };
        _keywordMappings["notification"] = new() { "通知", "お知らせ", "アラート", "notification", "notify", "alert" };
        _keywordMappings["http"] = new() { "API", "URL", "取得", "送信", "ウェブ", "サイト", "ニュース", "天気", "fetch", "send", "web", "news", "weather" };
        _keywordMappings["tts"] = new() { "読み上げ", "音声", "話す", "speak", "voice", "say", "read" };
        _keywordMappings["email"] = new() { "メール", "送信", "email", "send", "mail" };
        _keywordMappings["llm"] = new() { "AI", "要約", "翻訳", "分析", "summarize", "translate", "analyze" };
    }
    
    public async Task<ConversionResult> ConvertAsync(string naturalLanguage, string modelId = null)
    {
        try
        {
            _logger?.LogInformation("Converting natural language: {Input}", naturalLanguage);
            
            var rule = new AutomationDsl.Rule
            {
                Id = Guid.NewGuid().ToString(),
                Name = GenerateRuleName(naturalLanguage),
                Description = naturalLanguage,
                Enabled = true,
                Variables = new Dictionary<string, object>(),
                Permissions = new AutomationDsl.PermissionSet()
            };
            
            // Detect and add trigger
            var trigger = DetectTrigger(naturalLanguage);
            if (trigger != null)
            {
                rule.Trigger = trigger;
            }
            
            // Detect and add conditions
            var conditions = DetectConditions(naturalLanguage);
            if (conditions.Any())
            {
                rule.Conditions = conditions;
            }
            
            // Detect and add actions
            var actions = DetectActions(naturalLanguage);
            if (actions.Any())
            {
                rule.Actions = actions;
                UpdatePermissions(rule, actions);
            }
            
            return await Task.FromResult(new ConversionResult
            {
                Success = true,
                Rules = new[] { rule },
                Message = "Successfully converted natural language to rule"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error converting natural language");
            return new ConversionResult
            {
                Success = false,
                Message = $"Conversion failed: {ex.Message}"
            };
        }
    }
    
    private string GenerateRuleName(string naturalLanguage)
    {
        var words = naturalLanguage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3)
        {
            return string.Join(" ", words.Take(3)) + "...";
        }
        return naturalLanguage.Length > 30 ? naturalLanguage.Substring(0, 30) + "..." : naturalLanguage;
    }
    
    private AutomationDsl.TriggerDefinition DetectTrigger(string text)
    {
        var lowerText = text.ToLower();
        
        // Time trigger detection
        if (ContainsAny(lowerText, _keywordMappings["time"]))
        {
            var parameters = new Dictionary<string, object>();
            
            if (lowerText.Contains("毎朝") || lowerText.Contains("朝") || lowerText.Contains("morning"))
            {
                parameters["hour"] = 7;
                parameters["minute"] = 0;
            }
            else if (lowerText.Contains("毎晩") || lowerText.Contains("夜") || lowerText.Contains("evening"))
            {
                parameters["hour"] = 20;
                parameters["minute"] = 0;
            }
            else if (lowerText.Contains("昼") || lowerText.Contains("noon"))
            {
                parameters["hour"] = 12;
                parameters["minute"] = 0;
            }
            else
            {
                // Try to extract time
                var timePattern = @"(\d{1,2})[時:]\s*(\d{1,2})?";
                var match = System.Text.RegularExpressions.Regex.Match(text, timePattern);
                if (match.Success)
                {
                    parameters["hour"] = int.Parse(match.Groups[1].Value);
                    parameters["minute"] = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                }
                else
                {
                    parameters["intervalMs"] = 3600000; // Default 1 hour
                }
            }
            
            return new AutomationDsl.TriggerDefinition
            {
                Type = "time",
                Parameters = parameters
            };
        }
        
        // File trigger detection
        if (ContainsAny(lowerText, _keywordMappings["file"]))
        {
            return new AutomationDsl.TriggerDefinition
            {
                Type = "filesystem",
                Parameters = new Dictionary<string, object>
                {
                    ["path"] = @"C:\Downloads",
                    ["filter"] = "*.*"
                }
            };
        }
        
        // Default manual trigger
        return new AutomationDsl.TriggerDefinition
        {
            Type = "manual",
            Parameters = new Dictionary<string, object>()
        };
    }
    
    private List<AutomationDsl.ConditionDefinition> DetectConditions(string text)
    {
        var conditions = new List<AutomationDsl.ConditionDefinition>();
        var lowerText = text.ToLower();
        
        // Network condition
        if (lowerText.Contains("オンライン") || lowerText.Contains("online") || lowerText.Contains("接続"))
        {
            conditions.Add(new AutomationDsl.ConditionDefinition
            {
                Type = "networkStatus",
                Parameters = new Dictionary<string, object> { ["status"] = "online" }
            });
        }
        
        // Time range condition
        if (lowerText.Contains("平日") || lowerText.Contains("weekday"))
        {
            conditions.Add(new AutomationDsl.ConditionDefinition
            {
                Type = "dayOfWeek",
                Parameters = new Dictionary<string, object> { ["days"] = new[] { "月", "火", "水", "木", "金" } }
            });
        }
        
        return conditions;
    }
    
    private List<AutomationDsl.ActionDefinition> DetectActions(string text)
    {
        var actions = new List<AutomationDsl.ActionDefinition>();
        var lowerText = text.ToLower();
        
        // Notification action
        if (ContainsAny(lowerText, _keywordMappings["notification"]))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>
                {
                    ["title"] = "Loco通知",
                    ["message"] = ExtractQuotedText(text) ?? "タスクが完了しました"
                }
            });
        }
        
        // HTTP request action
        if (ContainsAny(lowerText, _keywordMappings["http"]))
        {
            var url = "https://api.example.com/data";
            if (lowerText.Contains("ニュース") || lowerText.Contains("news"))
            {
                url = "https://news-api.example.com/latest";
            }
            else if (lowerText.Contains("天気") || lowerText.Contains("weather"))
            {
                url = "https://weather-api.example.com/today";
            }
            
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "httpRequest",
                Parameters = new Dictionary<string, object>
                {
                    ["url"] = url,
                    ["method"] = "GET"
                }
            });
        }
        
        // TTS action
        if (ContainsAny(lowerText, _keywordMappings["tts"]))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "tts",
                Parameters = new Dictionary<string, object>
                {
                    ["text"] = ExtractQuotedText(text) ?? "${content}",
                    ["voice"] = lowerText.Contains("男") ? "male" : "female"
                }
            });
        }
        
        // Email action
        if (ContainsAny(lowerText, _keywordMappings["email"]))
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "email",
                Parameters = new Dictionary<string, object>
                {
                    ["to"] = "user@example.com",
                    ["subject"] = "Loco自動メール",
                    ["body"] = ExtractQuotedText(text) ?? "自動送信されたメールです"
                }
            });
        }
        
        // LLM action
        if (ContainsAny(lowerText, _keywordMappings["llm"]))
        {
            string prompt = "Process the following content: ${content}";
            if (lowerText.Contains("要約") || lowerText.Contains("summarize"))
            {
                prompt = "Summarize the following content in Japanese: ${content}";
            }
            else if (lowerText.Contains("翻訳") || lowerText.Contains("translate"))
            {
                prompt = "Translate the following to Japanese: ${content}";
            }
            else if (lowerText.Contains("分析") || lowerText.Contains("analyze"))
            {
                prompt = "Analyze the following data and provide insights: ${content}";
            }
            
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "llmQuery",
                Parameters = new Dictionary<string, object>
                {
                    ["prompt"] = prompt,
                    ["model"] = "gpt-3.5-turbo"
                }
            });
        }
        
        // File action
        if (ContainsAny(lowerText, _keywordMappings["file"]) && !actions.Any())
        {
            if (lowerText.Contains("コピー") || lowerText.Contains("copy"))
            {
                actions.Add(new AutomationDsl.ActionDefinition
                {
                    Type = "file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "copy",
                        ["source"] = "${trigger.filePath}",
                        ["destination"] = @"C:\Backup\"
                    }
                });
            }
            else if (lowerText.Contains("移動") || lowerText.Contains("move"))
            {
                actions.Add(new AutomationDsl.ActionDefinition
                {
                    Type = "file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["operation"] = "move",
                        ["source"] = "${trigger.filePath}",
                        ["destination"] = @"C:\Archive\"
                    }
                });
            }
        }
        
        // Default notification if no actions detected
        if (!actions.Any())
        {
            actions.Add(new AutomationDsl.ActionDefinition
            {
                Type = "notification",
                Parameters = new Dictionary<string, object>
                {
                    ["title"] = "タスク完了",
                    ["message"] = text
                }
            });
        }
        
        return actions;
    }
    
    private bool ContainsAny(string text, List<string> keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword));
    }
    
    private string ExtractQuotedText(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"[「『""]([^」』""]+)[」』""]");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        match = System.Text.RegularExpressions.Regex.Match(text, @"'([^']+)'");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return null;
    }
    
    private void UpdatePermissions(AutomationDsl.Rule rule, List<AutomationDsl.ActionDefinition> actions)
    {
        foreach (var action in actions)
        {
            switch (action.Type.ToLower())
            {
                case "httprequest":
                case "email":
                    rule.Permissions.Network = true;
                    break;
                case "file":
                    rule.Permissions.FileSystem = true;
                    break;
                case "llmquery":
                    rule.Permissions.Llm = true;
                    break;
                case "shell":
                case "process":
                    rule.Permissions.Shell = true;
                    break;
            }
        }
    }
}

public class ConversionResult
{
    public bool Success { get; set; }
    public AutomationDsl.Rule[] Rules { get; set; }
    public string Message { get; set; }
}
