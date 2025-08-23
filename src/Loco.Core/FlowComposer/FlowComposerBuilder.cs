using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Loco.Core.Models;
using Loco.Core.Interfaces;

namespace Loco.Core.FlowComposer;

/// <summary>
/// Flow Composer Builder - Main flow creation engine
/// Following John Carmack's performance focus and Rob Pike's simplicity
/// </summary>
public class FlowComposerBuilder
{
    private readonly ILogger<FlowComposerBuilder> _logger;
    private readonly List<ComponentCategory> _categories;
    
    public FlowComposerBuilder(ILogger<FlowComposerBuilder> logger)
    {
        _logger = logger;
        _categories = InitializeCategories();
    }
    
    public FlowBuilder StartFlow(string name, string description = null)
    {
        return new FlowBuilder(name, description, _logger);
    }
    
    public List<ComponentCategory> GetCategories()
    {
        return _categories.ToList();
    }
    
    public RuleValidationResult ValidateFlow(FlowDefinition flow)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(flow?.Name))
            errors.Add("Flow name is required");
        
        if (flow?.Triggers == null || !flow.Triggers.Any())
            errors.Add("At least one trigger is required");
        
        if (flow?.Actions == null || !flow.Actions.Any())
            errors.Add("At least one action is required");
        
        return new RuleValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    public ComponentDefinition GetComponent(string componentId)
    {
        foreach (var category in _categories)
        {
            var component = category.Components.FirstOrDefault(c => c.Id == componentId);
            if (component != null)
                return component;
        }
        return null;
    }
    
    private List<ComponentCategory> InitializeCategories()
    {
        return new List<ComponentCategory>
        {
            new ComponentCategory
            {
                Id = "triggers",
                Name = "トリガー",
                Icon = "⚡",
                Description = "フローを開始するイベント",
                Components = new List<ComponentDefinition>
                {
                    new ComponentDefinition
                    {
                        Id = "time.schedule",
                        Name = "時刻スケジュール",
                        Icon = "⏰",
                        Description = "指定時刻に実行",
                        Type = ComponentType.Trigger,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "hour", DisplayName = "時", Type = "number", Required = true, Min = 0, Max = 23 },
                            new ParameterDefinition { Name = "minute", DisplayName = "分", Type = "number", Required = true, Min = 0, Max = 59 },
                            new ParameterDefinition { Name = "weekdays", DisplayName = "曜日", Type = "multiselect", Options = new[] { "月", "火", "水", "木", "金", "土", "日" } }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "file.change",
                        Name = "ファイル変更",
                        Icon = "📁",
                        Description = "ファイルの変更を検知",
                        Type = ComponentType.Trigger,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "path", DisplayName = "監視パス", Type = "directory", Required = true },
                            new ParameterDefinition { Name = "pattern", DisplayName = "パターン", Type = "string", Default = "*.*" },
                            new ParameterDefinition { Name = "recursive", DisplayName = "サブフォルダ含む", Type = "boolean", Default = false }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "app.launch",
                        Name = "アプリ起動",
                        Icon = "🚀",
                        Description = "アプリの起動を検知",
                        Type = ComponentType.Trigger,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "processName", DisplayName = "プロセス名", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "system.event",
                        Name = "システムイベント",
                        Icon = "💻",
                        Description = "システムイベントを監視",
                        Type = ComponentType.Trigger,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "eventType", DisplayName = "イベント", Type = "select", Required = true, Options = new[] { "起動", "シャットダウン", "スリープ", "復帰" } }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "webhook.receive",
                        Name = "Webhook受信",
                        Icon = "🌐",
                        Description = "HTTPリクエストを受信",
                        Type = ComponentType.Trigger,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "port", DisplayName = "ポート", Type = "number", Default = 8080 },
                            new ParameterDefinition { Name = "path", DisplayName = "パス", Type = "string", Default = "/webhook" }
                        }
                    }
                }
            },
            new ComponentCategory
            {
                Id = "conditions",
                Name = "条件",
                Icon = "❓",
                Description = "実行条件を設定",
                Components = new List<ComponentDefinition>
                {
                    new ComponentDefinition
                    {
                        Id = "time.range",
                        Name = "時間帯",
                        Icon = "⏱️",
                        Description = "指定時間帯内かチェック",
                        Type = ComponentType.Condition,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "startTime", DisplayName = "開始時刻", Type = "string", Required = true },
                            new ParameterDefinition { Name = "endTime", DisplayName = "終了時刻", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "file.exists",
                        Name = "ファイル存在",
                        Icon = "📄",
                        Description = "ファイルの存在確認",
                        Type = ComponentType.Condition,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "path", DisplayName = "パス", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "variable.compare",
                        Name = "変数比較",
                        Icon = "🔀",
                        Description = "変数値を比較",
                        Type = ComponentType.Condition,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "variable", DisplayName = "変数名", Type = "string", Required = true },
                            new ParameterDefinition { Name = "operator", DisplayName = "演算子", Type = "select", Required = true, Options = new[] { "==", "!=", ">", "<", "contains" } },
                            new ParameterDefinition { Name = "value", DisplayName = "値", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "network.status",
                        Name = "ネットワーク接続",
                        Icon = "📡",
                        Description = "ネットワーク状態確認",
                        Type = ComponentType.Condition,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "host", DisplayName = "ホスト", Type = "string", Default = "google.com" },
                            new ParameterDefinition { Name = "timeout", DisplayName = "タイムアウト(秒)", Type = "number", Default = 5 }
                        }
                    }
                }
            },
            new ComponentCategory
            {
                Id = "actions",
                Name = "アクション",
                Icon = "⚙️",
                Description = "実行する処理",
                Components = new List<ComponentDefinition>
                {
                    new ComponentDefinition
                    {
                        Id = "notification.show",
                        Name = "通知表示",
                        Icon = "🔔",
                        Description = "デスクトップ通知を表示",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "title", DisplayName = "タイトル", Type = "string", Required = true },
                            new ParameterDefinition { Name = "message", DisplayName = "メッセージ", Type = "string", Required = true },
                            new ParameterDefinition { Name = "icon", DisplayName = "アイコン", Type = "select", Options = new[] { "info", "warning", "error", "success" }, Default = "info" }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "file.copy",
                        Name = "ファイルコピー",
                        Icon = "📋",
                        Description = "ファイルをコピー",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "source", DisplayName = "コピー元", Type = "string", Required = true },
                            new ParameterDefinition { Name = "destination", DisplayName = "コピー先", Type = "string", Required = true },
                            new ParameterDefinition { Name = "overwrite", DisplayName = "上書き", Type = "boolean", Default = false }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "app.run",
                        Name = "アプリ実行",
                        Icon = "▶️",
                        Description = "アプリケーションを実行",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "path", DisplayName = "実行ファイル", Type = "file", Required = true },
                            new ParameterDefinition { Name = "arguments", DisplayName = "引数", Type = "string" },
                            new ParameterDefinition { Name = "waitForExit", DisplayName = "終了待機", Type = "boolean", Default = false }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "http.request",
                        Name = "HTTPリクエスト",
                        Icon = "🌐",
                        Description = "HTTP通信を実行",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "url", DisplayName = "URL", Type = "string", Required = true },
                            new ParameterDefinition { Name = "method", DisplayName = "メソッド", Type = "select", Options = new[] { "GET", "POST", "PUT", "DELETE" }, Default = "GET" },
                            new ParameterDefinition { Name = "headers", DisplayName = "ヘッダー", Type = "json" },
                            new ParameterDefinition { Name = "body", DisplayName = "ボディ", Type = "string" }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "tts.speak",
                        Name = "音声読み上げ",
                        Icon = "🔊",
                        Description = "テキストを音声で読み上げ",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "text", DisplayName = "テキスト", Type = "string", Required = true },
                            new ParameterDefinition { Name = "voice", DisplayName = "音声", Type = "select", Options = new[] { "男性", "女性", "子供" }, Default = "女性" },
                            new ParameterDefinition { Name = "speed", DisplayName = "速度", Type = "slider", Min = 0.5, Max = 2.0, Default = 1.0 }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "variable.set",
                        Name = "変数設定",
                        Icon = "💾",
                        Description = "変数に値を設定",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "name", DisplayName = "変数名", Type = "string", Required = true },
                            new ParameterDefinition { Name = "value", DisplayName = "値", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "email.send",
                        Name = "メール送信",
                        Icon = "📧",
                        Description = "メールを送信",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "to", DisplayName = "宛先", Type = "string", Required = true },
                            new ParameterDefinition { Name = "subject", DisplayName = "件名", Type = "string", Required = true },
                            new ParameterDefinition { Name = "body", DisplayName = "本文", Type = "string", Required = true }
                        }
                    },
                    new ComponentDefinition
                    {
                        Id = "llm.query",
                        Name = "AI処理",
                        Icon = "🤖",
                        Description = "AIで処理",
                        Type = ComponentType.Action,
                        Parameters = new List<ParameterDefinition>
                        {
                            new ParameterDefinition { Name = "prompt", DisplayName = "プロンプト", Type = "string", Required = true },
                            new ParameterDefinition { Name = "model", DisplayName = "モデル", Type = "select", Options = new[] { "gpt-3.5-turbo", "gpt-4", "local" }, Default = "gpt-3.5-turbo" }
                        }
                    }
                }
            }
        };
    }
}

/// <summary>
/// Flow builder - Fluent interface for building flows
/// </summary>
public class FlowBuilder
{
    private readonly FlowDefinition _flow;
    private readonly ILogger _logger;
    
    public FlowBuilder(string name, string description, ILogger logger)
    {
        _logger = logger;
        _flow = new FlowDefinition
        {
            Name = name,
            Description = description
        };
    }
    
    public FlowBuilder AddTrigger(string type, Dictionary<string, object> parameters)
    {
        _flow.Triggers.Add(new TriggerDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Parameters = parameters ?? new Dictionary<string, object>()
        });
        return this;
    }
    
    public FlowBuilder AddCondition(string type, Dictionary<string, object> parameters)
    {
        _flow.Conditions.Add(new ConditionDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Parameters = parameters ?? new Dictionary<string, object>()
        });
        return this;
    }
    
    public FlowBuilder AddAction(string type, Dictionary<string, object> parameters)
    {
        _flow.Actions.Add(new ActionDefinition
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Parameters = parameters ?? new Dictionary<string, object>()
        });
        return this;
    }
    
    public FlowBuilder SetVariable(string name, object value)
    {
        _flow.Variables[name] = value;
        return this;
    }
    
    public FlowBuilder SetPermissions(PermissionSet permissions)
    {
        _flow.Permissions = permissions;
        return this;
    }
    
    public FlowDefinition Build()
    {
        _flow.UpdatedAt = DateTime.UtcNow;
        return _flow;
    }
    
    public string ToJson()
    {
        return JsonSerializer.Serialize(_flow, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

/// <summary>
/// Component category
/// </summary>
public class ComponentCategory
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Description { get; set; }
    public List<ComponentDefinition> Components { get; set; } = new();
}

/// <summary>
/// Component definition
/// </summary>
public class ComponentDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Icon { get; set; }
    public string Description { get; set; }
    public ComponentType Type { get; set; }
    public List<ParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// Parameter definition
/// </summary>
public class ParameterDefinition
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Type { get; set; }
    public bool Required { get; set; }
    public object Default { get; set; }
    public object[] Options { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
}
