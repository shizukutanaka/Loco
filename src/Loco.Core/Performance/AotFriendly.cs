using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Loco.Core.Performance;

/// <summary>
/// AOT (Ahead-of-Time) コンパイル対応のユーティリティ
///
/// Native AOT の利点:
/// - 起動時間: 最大40%短縮
/// - メモリ使用量: JITエンジン不要で削減
/// - デプロイサイズ: 最大50%削減
///
/// 注意事項:
/// - リフレクションの制限 (Assembly.LoadFile, Reflection.Emit 不可)
/// - ソースジェネレーター使用を推奨
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
/// </summary>

/// <summary>
/// AOT対応のステップファクトリ - リフレクションを使用しない
/// </summary>
public static class AotStepFactory
{
    /// <summary>
    /// ステップ種別とファクトリのマッピング (FrozenDictionary で高速化)
    /// </summary>
    private static readonly FrozenDictionary<string, Func<Dictionary<string, object?>, IWorkflowStep>> StepFactories;

    static AotStepFactory()
    {
        var factories = new Dictionary<string, Func<Dictionary<string, object?>, IWorkflowStep>>(StringComparer.OrdinalIgnoreCase)
        {
            ["shell"] = p => new ShellStep(p),
            ["command"] = p => new ShellStep(p),
            ["log"] = p => new LogStep(p),
            ["http"] = p => new HttpStep(p),
            ["delay"] = p => new DelayStep(p),
            ["file_copy"] = p => new FileCopyStep(p),
            ["file_write"] = p => new FileWriteStep(p),
            ["condition"] = p => new ConditionStep(p),
            ["parallel"] = p => new ParallelStep(p),
            ["retry"] = p => new RetryStep(p),
            ["timeout"] = p => new TimeoutStep(p),
        };

        StepFactories = factories.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ステップを作成 (AOT対応)
    /// </summary>
    public static IWorkflowStep? CreateStep(string stepType, Dictionary<string, object?>? parameters = null)
    {
        if (StepFactories.TryGetValue(stepType, out var factory))
        {
            return factory(parameters ?? new Dictionary<string, object?>());
        }
        return null;
    }

    /// <summary>
    /// サポートされているステップ種別を取得
    /// </summary>
    public static IReadOnlyCollection<string> SupportedStepTypes => StepFactories.Keys;

    /// <summary>
    /// カスタムステップファクトリを登録するための辞書を取得
    /// (起動時に一度だけ呼び出し、その後FrozenDictionaryに変換)
    /// </summary>
    public static Dictionary<string, Func<Dictionary<string, object?>, IWorkflowStep>> CreateFactoryBuilder()
    {
        return new Dictionary<string, Func<Dictionary<string, object?>, IWorkflowStep>>(
            StepFactories,
            StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// ワークフローステップのインターフェース
/// </summary>
public interface IWorkflowStep
{
    string StepType { get; }
    ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default);
}

/// <summary>
/// ステップの基底クラス
/// </summary>
public abstract class BaseStep : IWorkflowStep
{
    protected readonly Dictionary<string, object?> Parameters;

    protected BaseStep(Dictionary<string, object?> parameters)
    {
        Parameters = parameters;
    }

    public abstract string StepType { get; }

    public abstract ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default);

    protected T? GetParameter<T>(string key, T? defaultValue = default)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            if (value is T typed) return typed;
            if (value is JsonElement jsonElement)
            {
                return DeserializeJsonElement<T>(jsonElement);
            }
        }
        return defaultValue;
    }

    private static T? DeserializeJsonElement<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
/// シェルコマンド実行ステップ
/// </summary>
public sealed class ShellStep : BaseStep
{
    public override string StepType => "shell";

    public ShellStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "shell");
        var command = GetParameter<string>("command") ?? "";
        var startTime = Timestamp.Now;

        try
        {
            // 実際の実行ロジック (簡易版)
            await System.Threading.Tasks.Task.Delay(10, ct);
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (Exception ex)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, ex.Message);
        }
    }
}

/// <summary>
/// ログ出力ステップ
/// </summary>
public sealed class LogStep : BaseStep
{
    public override string StepType => "log";

    public LogStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "log");
        var message = GetParameter<string>("message") ?? "";
        var startTime = Timestamp.Now;

        Console.WriteLine($"[LOG] {message}");

        var duration = startTime.Until(Timestamp.Now);
        return ValueTask.FromResult(StepExecutionResult.Succeeded(stepId, duration));
    }
}

/// <summary>
/// HTTP リクエストステップ
/// </summary>
public sealed class HttpStep : BaseStep
{
    public override string StepType => "http";

    public HttpStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "http");
        var url = GetParameter<string>("url") ?? "";
        var method = GetParameter<string>("method") ?? "GET";
        var startTime = Timestamp.Now;

        try
        {
            // 実際の HTTP 実行はここに実装
            await System.Threading.Tasks.Task.Delay(10, ct);
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (Exception ex)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, ex.Message);
        }
    }
}

/// <summary>
/// 遅延ステップ
/// </summary>
public sealed class DelayStep : BaseStep
{
    public override string StepType => "delay";

    public DelayStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "delay");
        var delayMs = GetParameter<int>("milliseconds", 1000);
        var startTime = Timestamp.Now;

        try
        {
            await System.Threading.Tasks.Task.Delay(delayMs, ct);
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (OperationCanceledException)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, "Cancelled");
        }
    }
}

/// <summary>
/// ファイルコピーステップ
/// </summary>
public sealed class FileCopyStep : BaseStep
{
    public override string StepType => "file_copy";

    public FileCopyStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "file_copy");
        var source = GetParameter<string>("source") ?? "";
        var destination = GetParameter<string>("destination") ?? "";
        var startTime = Timestamp.Now;

        try
        {
            if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(destination))
            {
                System.IO.File.Copy(source, destination, overwrite: true);
            }
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (Exception ex)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, ex.Message);
        }
    }
}

/// <summary>
/// ファイル書き込みステップ
/// </summary>
public sealed class FileWriteStep : BaseStep
{
    public override string StepType => "file_write";

    public FileWriteStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "file_write");
        var path = GetParameter<string>("path") ?? "";
        var content = GetParameter<string>("content") ?? "";
        var startTime = Timestamp.Now;

        try
        {
            if (!string.IsNullOrEmpty(path))
            {
                await System.IO.File.WriteAllTextAsync(path, content, ct);
            }
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (Exception ex)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, ex.Message);
        }
    }
}

/// <summary>
/// 条件分岐ステップ
/// </summary>
public sealed class ConditionStep : BaseStep
{
    public override string StepType => "condition";

    public ConditionStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "condition");
        var startTime = Timestamp.Now;

        // 条件評価のロジック
        var duration = startTime.Until(Timestamp.Now);
        return ValueTask.FromResult(StepExecutionResult.Succeeded(stepId, duration));
    }
}

/// <summary>
/// 並列実行ステップ
/// </summary>
public sealed class ParallelStep : BaseStep
{
    public override string StepType => "parallel";

    public ParallelStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "parallel");
        var startTime = Timestamp.Now;

        // 並列実行ロジック
        await System.Threading.Tasks.Task.Delay(10, ct);

        var duration = startTime.Until(Timestamp.Now);
        return StepExecutionResult.Succeeded(stepId, duration);
    }
}

/// <summary>
/// リトライステップ
/// </summary>
public sealed class RetryStep : BaseStep
{
    public override string StepType => "retry";

    public RetryStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "retry");
        var maxAttempts = GetParameter<int>("maxAttempts", 3);
        var startTime = Timestamp.Now;

        // リトライロジック
        await System.Threading.Tasks.Task.Delay(10, ct);

        var duration = startTime.Until(Timestamp.Now);
        return StepExecutionResult.Succeeded(stepId, duration);
    }
}

/// <summary>
/// タイムアウトステップ
/// </summary>
public sealed class TimeoutStep : BaseStep
{
    public override string StepType => "timeout";

    public TimeoutStep(Dictionary<string, object?> parameters) : base(parameters) { }

    public override async ValueTask<StepExecutionResult> ExecuteAsync(
        ExecutionScope scope,
        System.Threading.CancellationToken ct = default)
    {
        var stepId = new StepId(GetParameter<string>("id") ?? "timeout");
        var timeoutMs = GetParameter<int>("milliseconds", 30000);
        var startTime = Timestamp.Now;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            // タイムアウト付き実行
            await System.Threading.Tasks.Task.Delay(10, timeoutCts.Token);

            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Succeeded(stepId, duration);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var duration = startTime.Until(Timestamp.Now);
            return StepExecutionResult.Failed(stepId, duration, $"Timeout after {timeoutMs}ms");
        }
    }
}

/// <summary>
/// AOT対応の型情報ヘルパー
/// </summary>
public static class AotTypeInfo
{
    /// <summary>
    /// 型名から Type を取得 (AOT対応、限定的)
    /// リフレクションを使用せずにサポートされた型のみ返す
    /// </summary>
    private static readonly FrozenDictionary<string, Type> KnownTypes;

    static AotTypeInfo()
    {
        var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = typeof(string),
            ["int"] = typeof(int),
            ["int32"] = typeof(int),
            ["long"] = typeof(long),
            ["int64"] = typeof(long),
            ["bool"] = typeof(bool),
            ["boolean"] = typeof(bool),
            ["double"] = typeof(double),
            ["float"] = typeof(float),
            ["decimal"] = typeof(decimal),
            ["datetime"] = typeof(DateTime),
            ["guid"] = typeof(Guid),
            ["timespan"] = typeof(TimeSpan),
        };

        KnownTypes = types.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 型名から Type を取得
    /// </summary>
    public static Type? GetType(string typeName)
    {
        return KnownTypes.TryGetValue(typeName, out var type) ? type : null;
    }

    /// <summary>
    /// サポートされている型名一覧
    /// </summary>
    public static IReadOnlyCollection<string> SupportedTypeNames => KnownTypes.Keys;
}
