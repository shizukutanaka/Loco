using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Loco.Core.Performance;

/// <summary>
/// AsyncLocal ベースの実行コンテキスト
///
/// 機能:
/// - 相関ID/トレースIDの自動伝播
/// - 非同期処理間での状態共有
/// - 構造化ログのためのコンテキスト情報
/// - 分散トレーシング対応
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1
/// </summary>
public sealed class ExecutionScope : IDisposable
{
    private static readonly AsyncLocal<ExecutionScope?> CurrentScope = new();

    private readonly ExecutionScope? _parent;
    private readonly Dictionary<string, object?> _properties;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    /// <summary>
    /// 現在のスコープ (存在しない場合は null)
    /// </summary>
    public static ExecutionScope? Current => CurrentScope.Value;

    /// <summary>
    /// 相関ID
    /// </summary>
    public CorrelationId CorrelationId { get; }

    /// <summary>
    /// 実行ID (ワークフロー実行ごとにユニーク)
    /// </summary>
    public ExecutionId ExecutionId { get; }

    /// <summary>
    /// 親スコープのID (ネストしている場合)
    /// </summary>
    public string? ParentId => _parent?.ExecutionId.Value;

    /// <summary>
    /// スコープの深さ (ルート=0)
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// スコープ作成時刻
    /// </summary>
    public Timestamp StartTime { get; }

    /// <summary>
    /// 経過時間
    /// </summary>
    public Duration Elapsed => Duration.FromMilliseconds(_stopwatch.Elapsed.TotalMilliseconds);

    /// <summary>
    /// スコープ名 (ログ用)
    /// </summary>
    public string Name { get; }

    private ExecutionScope(
        string name,
        CorrelationId correlationId,
        ExecutionId executionId,
        ExecutionScope? parent)
    {
        Name = name;
        CorrelationId = correlationId;
        ExecutionId = executionId;
        _parent = parent;
        _properties = new Dictionary<string, object?>();
        _stopwatch = Stopwatch.StartNew();
        StartTime = Timestamp.Now;
        Depth = parent?.Depth + 1 ?? 0;
    }

    /// <summary>
    /// 新しいルートスコープを作成
    /// </summary>
    public static ExecutionScope CreateRoot(string name = "root")
    {
        var scope = new ExecutionScope(
            name,
            CorrelationId.New(),
            ExecutionId.New(),
            null);

        CurrentScope.Value = scope;
        return scope;
    }

    /// <summary>
    /// 現在のスコープの子スコープを作成
    /// </summary>
    public static ExecutionScope CreateChild(string name)
    {
        var parent = Current;
        var correlationId = parent?.CorrelationId ?? CorrelationId.New();

        var scope = new ExecutionScope(
            name,
            correlationId,
            ExecutionId.New(),
            parent);

        CurrentScope.Value = scope;
        return scope;
    }

    /// <summary>
    /// スコープにプロパティを設定
    /// </summary>
    public void SetProperty(string key, object? value)
    {
        _properties[key] = value;
    }

    /// <summary>
    /// スコープからプロパティを取得
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (_properties.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }

        // 親スコープを検索
        return _parent != null ? _parent.GetProperty<T>(key) : default;
    }

    /// <summary>
    /// すべてのプロパティを取得 (親スコープ含む)
    /// </summary>
    public IReadOnlyDictionary<string, object?> GetAllProperties()
    {
        var result = new Dictionary<string, object?>();

        // 親から順に追加 (子で上書き)
        CollectProperties(_parent, result);
        foreach (var kvp in _properties)
        {
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private static void CollectProperties(ExecutionScope? scope, Dictionary<string, object?> result)
    {
        if (scope == null) return;
        CollectProperties(scope._parent, result);
        foreach (var kvp in scope._properties)
        {
            result[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// 構造化ログ用のコンテキスト情報を取得
    /// </summary>
    public Dictionary<string, object> GetLogContext()
    {
        return new Dictionary<string, object>
        {
            ["CorrelationId"] = CorrelationId.Value,
            ["ExecutionId"] = ExecutionId.Value,
            ["ScopeName"] = Name,
            ["Depth"] = Depth,
            ["ElapsedMs"] = Elapsed.TotalMilliseconds
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopwatch.Stop();
        CurrentScope.Value = _parent;
    }
}

/// <summary>
/// スコープ付き操作のヘルパー
/// </summary>
public static class ExecutionScopeExtensions
{
    /// <summary>
    /// スコープ内で操作を実行
    /// </summary>
    public static T ExecuteInScope<T>(string scopeName, Func<ExecutionScope, T> action)
    {
        using var scope = ExecutionScope.CreateChild(scopeName);
        return action(scope);
    }

    /// <summary>
    /// スコープ内で非同期操作を実行
    /// </summary>
    public static async ValueTask<T> ExecuteInScopeAsync<T>(
        string scopeName,
        Func<ExecutionScope, ValueTask<T>> action)
    {
        using var scope = ExecutionScope.CreateChild(scopeName);
        return await action(scope).ConfigureAwait(false);
    }

    /// <summary>
    /// 現在のスコープにワークフロー情報を設定
    /// </summary>
    public static void SetWorkflowInfo(this ExecutionScope scope, WorkflowId workflowId, string workflowName)
    {
        scope.SetProperty("WorkflowId", workflowId.Value);
        scope.SetProperty("WorkflowName", workflowName);
    }

    /// <summary>
    /// 現在のスコープにステップ情報を設定
    /// </summary>
    public static void SetStepInfo(this ExecutionScope scope, StepId stepId, string stepName, int stepIndex)
    {
        scope.SetProperty("StepId", stepId.Value);
        scope.SetProperty("StepName", stepName);
        scope.SetProperty("StepIndex", stepIndex);
    }
}

/// <summary>
/// 実行コンテキストのスナップショット
/// async/await の境界を越えてコンテキストを明示的にキャプチャ
/// </summary>
public readonly struct ExecutionContextSnapshot
{
    private readonly CorrelationId _correlationId;
    private readonly ExecutionId _executionId;
    private readonly IReadOnlyDictionary<string, object?> _properties;

    public CorrelationId CorrelationId => _correlationId;
    public ExecutionId ExecutionId => _executionId;
    public IReadOnlyDictionary<string, object?> Properties => _properties;

    private ExecutionContextSnapshot(
        CorrelationId correlationId,
        ExecutionId executionId,
        IReadOnlyDictionary<string, object?> properties)
    {
        _correlationId = correlationId;
        _executionId = executionId;
        _properties = properties;
    }

    /// <summary>
    /// 現在のスコープのスナップショットを作成
    /// </summary>
    public static ExecutionContextSnapshot Capture()
    {
        var current = ExecutionScope.Current;
        if (current == null)
        {
            return new ExecutionContextSnapshot(
                CorrelationId.Empty,
                ExecutionId.Empty,
                new Dictionary<string, object?>());
        }

        return new ExecutionContextSnapshot(
            current.CorrelationId,
            current.ExecutionId,
            current.GetAllProperties());
    }

    /// <summary>
    /// スナップショットを復元
    /// </summary>
    public ExecutionScope Restore(string scopeName = "restored")
    {
        var scope = ExecutionScope.CreateChild(scopeName);
        foreach (var kvp in _properties)
        {
            scope.SetProperty(kvp.Key, kvp.Value);
        }
        return scope;
    }
}

/// <summary>
/// 操作計測ヘルパー
/// </summary>
public readonly struct OperationTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly Action<Duration>? _onComplete;
    private readonly string _operationName;

    public OperationTimer(string operationName, Action<Duration>? onComplete = null)
    {
        _operationName = operationName;
        _onComplete = onComplete;
        _stopwatch = Stopwatch.StartNew();
    }

    public Duration Elapsed => Duration.FromMilliseconds(_stopwatch.Elapsed.TotalMilliseconds);

    public void Dispose()
    {
        _stopwatch.Stop();
        _onComplete?.Invoke(Elapsed);
    }
}

/// <summary>
/// 軽量な操作スコープ (スタック割り当て)
/// </summary>
public ref struct LightweightScope
{
    private readonly Stopwatch _stopwatch;
    public string Name { get; }
    public Duration Elapsed => Duration.FromMilliseconds(_stopwatch.Elapsed.TotalMilliseconds);

    public LightweightScope(string name)
    {
        Name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Stop() => _stopwatch.Stop();
}
