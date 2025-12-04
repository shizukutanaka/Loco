using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Loco.Core.Performance;

/// <summary>
/// FrozenDictionary / FrozenSet を使った高速読み取り専用コレクション
///
/// パフォーマンス特性 (.NET 8):
/// - Dictionary より約43-50%高速な読み取り
/// - 作成コストは高いが、読み取りは最速
/// - スレッドセーフ（不変）
/// - 設定データやルックアップテーブルに最適
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozendictionary-2
/// </summary>
public static class FrozenCollections
{
    /// <summary>
    /// ワークフローステップタイプのルックアップテーブル
    /// アプリケーション起動時に一度作成、以後は高速読み取り
    /// </summary>
    public static readonly FrozenDictionary<string, WorkflowStepType> StepTypes =
        new Dictionary<string, WorkflowStepType>(StringComparer.OrdinalIgnoreCase)
        {
            ["shell"] = WorkflowStepType.Shell,
            ["command"] = WorkflowStepType.Shell,
            ["script"] = WorkflowStepType.Script,
            ["http"] = WorkflowStepType.Http,
            ["rest"] = WorkflowStepType.Http,
            ["api"] = WorkflowStepType.Http,
            ["conditional"] = WorkflowStepType.Conditional,
            ["if"] = WorkflowStepType.Conditional,
            ["branch"] = WorkflowStepType.Conditional,
            ["parallel"] = WorkflowStepType.Parallel,
            ["foreach"] = WorkflowStepType.ForEach,
            ["loop"] = WorkflowStepType.ForEach,
            ["delay"] = WorkflowStepType.Delay,
            ["wait"] = WorkflowStepType.Delay,
            ["sleep"] = WorkflowStepType.Delay,
            ["notification"] = WorkflowStepType.Notification,
            ["notify"] = WorkflowStepType.Notification,
            ["email"] = WorkflowStepType.Notification,
            ["transform"] = WorkflowStepType.Transform,
            ["map"] = WorkflowStepType.Transform,
            ["subprocess"] = WorkflowStepType.SubWorkflow,
            ["subworkflow"] = WorkflowStepType.SubWorkflow,
            ["call"] = WorkflowStepType.SubWorkflow
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HTTPメソッドの有効値セット
    /// Contains() が高速
    /// </summary>
    public static readonly FrozenSet<string> ValidHttpMethods =
        new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 危険なシェルコマンドのブラックリスト
    /// セキュリティチェック用
    /// </summary>
    public static readonly FrozenSet<string> DangerousCommands =
        new[]
        {
            "rm -rf", "del /f /s /q", "format", "mkfs",
            "dd if=", "shutdown", "reboot", "halt",
            "chmod 777", "chmod -R 777",
            "> /dev/sda", "> /dev/null",
            ":(){ :|:& };:", // fork bomb
            "wget -O- | sh", "curl | sh",
            "base64 -d | sh"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ファイル拡張子とMIMEタイプのマッピング
    /// </summary>
    public static readonly FrozenDictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".json"] = "application/json",
            [".xml"] = "application/xml",
            [".yaml"] = "application/x-yaml",
            [".yml"] = "application/x-yaml",
            [".txt"] = "text/plain",
            [".html"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "application/javascript",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".pdf"] = "application/pdf",
            [".zip"] = "application/zip"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ステップタイプを取得（高速ルックアップ）
    /// </summary>
    public static WorkflowStepType GetStepType(string type)
    {
        return StepTypes.TryGetValue(type, out var stepType)
            ? stepType
            : WorkflowStepType.Unknown;
    }

    /// <summary>
    /// HTTPメソッドが有効かチェック
    /// </summary>
    public static bool IsValidHttpMethod(string method)
    {
        return ValidHttpMethods.Contains(method);
    }

    /// <summary>
    /// コマンドが危険かチェック
    /// </summary>
    public static bool IsDangerousCommand(string command)
    {
        return DangerousCommands.Any(dangerous =>
            command.Contains(dangerous, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// MIMEタイプを取得
    /// </summary>
    public static string GetMimeType(string extension)
    {
        return MimeTypes.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }
}

/// <summary>
/// ワークフローステップタイプ
/// </summary>
public enum WorkflowStepType
{
    Unknown = 0,
    Shell,
    Script,
    Http,
    Conditional,
    Parallel,
    ForEach,
    Delay,
    Notification,
    Transform,
    SubWorkflow
}

/// <summary>
/// 動的に作成するFrozenDictionary用ビルダー
/// </summary>
public sealed class FrozenLookupBuilder<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;
    private readonly IEqualityComparer<TKey>? _comparer;

    public FrozenLookupBuilder(IEqualityComparer<TKey>? comparer = null)
    {
        _comparer = comparer;
        _items = comparer != null
            ? new Dictionary<TKey, TValue>(comparer)
            : new Dictionary<TKey, TValue>();
    }

    public FrozenLookupBuilder<TKey, TValue> Add(TKey key, TValue value)
    {
        _items[key] = value;
        return this;
    }

    public FrozenLookupBuilder<TKey, TValue> AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var item in items)
        {
            _items[item.Key] = item.Value;
        }
        return this;
    }

    /// <summary>
    /// FrozenDictionaryを構築
    /// 注意: この操作はコストが高い（一度だけ実行すること）
    /// </summary>
    public FrozenDictionary<TKey, TValue> Build()
    {
        return _comparer != null
            ? _items.ToFrozenDictionary(_comparer)
            : _items.ToFrozenDictionary();
    }
}

/// <summary>
/// ワークフロー設定のFrozenキャッシュ
/// </summary>
public sealed class FrozenWorkflowConfig
{
    private readonly FrozenDictionary<string, object?> _settings;
    private readonly FrozenSet<string> _enabledFeatures;

    private FrozenWorkflowConfig(
        FrozenDictionary<string, object?> settings,
        FrozenSet<string> enabledFeatures)
    {
        _settings = settings;
        _enabledFeatures = enabledFeatures;
    }

    public T? GetSetting<T>(string key)
    {
        return _settings.TryGetValue(key, out var value) && value is T typed
            ? typed
            : default;
    }

    public bool IsFeatureEnabled(string feature)
    {
        return _enabledFeatures.Contains(feature);
    }

    public static FrozenWorkflowConfigBuilder CreateBuilder()
    {
        return new FrozenWorkflowConfigBuilder();
    }

    public sealed class FrozenWorkflowConfigBuilder
    {
        private readonly Dictionary<string, object?> _settings = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _features = new(StringComparer.OrdinalIgnoreCase);

        public FrozenWorkflowConfigBuilder AddSetting(string key, object? value)
        {
            _settings[key] = value;
            return this;
        }

        public FrozenWorkflowConfigBuilder EnableFeature(string feature)
        {
            _features.Add(feature);
            return this;
        }

        public FrozenWorkflowConfig Build()
        {
            return new FrozenWorkflowConfig(
                _settings.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                _features.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            );
        }
    }
}
