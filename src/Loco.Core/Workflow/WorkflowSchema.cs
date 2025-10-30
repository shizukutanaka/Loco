using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Loco.Core.Workflow
{
    /// <summary>
    /// Cross-platform workflow definition that works across Android, iOS, Windows, Mac, and Linux.
    /// クロスプラットフォームワークフロー定義
    /// </summary>
    public class WorkflowDefinition
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("platforms")]
        public List<string> Platforms { get; set; } = new();

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("triggers")]
        public List<WorkflowTrigger> Triggers { get; set; } = new();

        [JsonPropertyName("constraints")]
        public List<WorkflowConstraint> Constraints { get; set; } = new();

        [JsonPropertyName("actions")]
        public List<WorkflowAction> Actions { get; set; } = new();

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Workflow trigger definition - what starts the workflow.
    /// ワークフロートリガー定義
    /// </summary>
    public class WorkflowTrigger
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Workflow constraint definition - conditions that must be met.
    /// ワークフロー制約定義
    /// </summary>
    public class WorkflowConstraint
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("operator")]
        public string Operator { get; set; } = "equals";

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Workflow action definition - what the workflow does.
    /// ワークフローアクション定義
    /// </summary>
    public class WorkflowAction
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("onError")]
        public ActionErrorHandling? OnError { get; set; }

        [JsonPropertyName("retry")]
        public ActionRetryPolicy? Retry { get; set; }
    }

    /// <summary>
    /// Error handling configuration for actions.
    /// エラーハンドリング設定
    /// </summary>
    public class ActionErrorHandling
    {
        [JsonPropertyName("strategy")]
        public string Strategy { get; set; } = "stop"; // stop, continue, fallback

        [JsonPropertyName("fallbackAction")]
        public WorkflowAction? FallbackAction { get; set; }

        [JsonPropertyName("logError")]
        public bool LogError { get; set; } = true;
    }

    /// <summary>
    /// Retry policy configuration for actions.
    /// リトライポリシー設定
    /// </summary>
    public class ActionRetryPolicy
    {
        [JsonPropertyName("maxAttempts")]
        public int MaxAttempts { get; set; } = 3;

        [JsonPropertyName("delayMs")]
        public int DelayMs { get; set; } = 1000;

        [JsonPropertyName("backoffStrategy")]
        public string BackoffStrategy { get; set; } = "exponential"; // fixed, linear, exponential
    }

    /// <summary>
    /// Platform-specific capabilities and supported features.
    /// プラットフォーム固有の機能とサポートされる機能
    /// </summary>
    public static class PlatformCapabilities
    {
        public static readonly Dictionary<string, HashSet<string>> SupportedTriggers = new()
        {
            ["android"] = new HashSet<string>
            {
                "time", "location", "battery", "wifi", "bluetooth", "sms", "call",
                "app_launch", "notification", "sensor", "screen_state", "headphones"
            },
            ["ios"] = new HashSet<string>
            {
                "time", "location", "nfc", "app_launch", "shortcut", "focus_mode",
                "charger", "wifi", "bluetooth", "carplay"
            },
            ["windows"] = new HashSet<string>
            {
                "time", "file_system", "process", "hotkey", "window", "usb",
                "network", "power", "login", "idle"
            },
            ["mac"] = new HashSet<string>
            {
                "time", "file_system", "process", "hotkey", "window", "usb",
                "network", "power", "login", "idle", "clipboard"
            },
            ["linux"] = new HashSet<string>
            {
                "time", "file_system", "process", "hotkey", "window", "usb",
                "network", "power", "login", "idle", "dbus"
            }
        };

        public static readonly Dictionary<string, HashSet<string>> SupportedActions = new()
        {
            ["android"] = new HashSet<string>
            {
                "notification", "toast", "vibrate", "sound", "sms", "call", "email",
                "open_app", "open_url", "wifi_toggle", "bluetooth_toggle", "volume",
                "brightness", "screen_rotation", "airplane_mode", "http_request"
            },
            ["ios"] = new HashSet<string>
            {
                "notification", "share", "clipboard", "open_url", "open_app",
                "run_shortcut", "focus_mode", "http_request", "wait", "script"
            },
            ["windows"] = new HashSet<string>
            {
                "notification", "run_program", "file_operation", "http_request",
                "clipboard", "keyboard", "mouse", "window_control", "sound",
                "powershell", "cmd", "registry"
            },
            ["mac"] = new HashSet<string>
            {
                "notification", "run_program", "file_operation", "http_request",
                "clipboard", "keyboard", "mouse", "window_control", "sound",
                "applescript", "shell", "spotlight"
            },
            ["linux"] = new HashSet<string>
            {
                "notification", "run_program", "file_operation", "http_request",
                "clipboard", "keyboard", "mouse", "window_control", "sound",
                "shell", "dbus", "systemd"
            }
        };

        public static bool IsTriggerSupported(string platform, string triggerType)
        {
            return SupportedTriggers.TryGetValue(platform, out var triggers) &&
                   triggers.Contains(triggerType);
        }

        public static bool IsActionSupported(string platform, string actionType)
        {
            return SupportedActions.TryGetValue(platform, out var actions) &&
                   actions.Contains(actionType);
        }
    }
}
