using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Loco.Core.Performance;

/// <summary>
/// CompositeFormat を活用した高性能文字列フォーマット
///
/// パフォーマンス改善:
/// - string.Format: 約45ms → 26ms (42%削減)
/// - Span版: 約45ms → 10ms (78%削減)
/// - メモリ割り当て: 16.8MB → 2B (99.99%削減)
///
/// 使用場面:
/// - 同じフォーマット文字列を繰り返し使用
/// - ログ出力、メッセージ生成
/// - バッチ処理、リクエスト処理
///
/// 参考: https://codingbolt.net/2023/11/26/increase-performance-by-using-compositeformat/
/// </summary>
public static class OptimizedFormatting
{
    // ワークフロー用の事前パースされたフォーマット

    /// <summary>
    /// ステップ開始ログ: "Step '{0}' started at {1}"
    /// </summary>
    public static readonly CompositeFormat StepStarted =
        CompositeFormat.Parse("Step '{0}' started at {1}");

    /// <summary>
    /// ステップ完了ログ: "Step '{0}' completed in {1:F2}ms"
    /// </summary>
    public static readonly CompositeFormat StepCompleted =
        CompositeFormat.Parse("Step '{0}' completed in {1:F2}ms");

    /// <summary>
    /// ステップ失敗ログ: "Step '{0}' failed: {1}"
    /// </summary>
    public static readonly CompositeFormat StepFailed =
        CompositeFormat.Parse("Step '{0}' failed: {1}");

    /// <summary>
    /// ワークフロー開始ログ: "Workflow '{0}' (ID: {1}) started"
    /// </summary>
    public static readonly CompositeFormat WorkflowStarted =
        CompositeFormat.Parse("Workflow '{0}' (ID: {1}) started");

    /// <summary>
    /// ワークフロー完了ログ: "Workflow '{0}' completed in {1:F2}s with {2} steps"
    /// </summary>
    public static readonly CompositeFormat WorkflowCompleted =
        CompositeFormat.Parse("Workflow '{0}' completed in {1:F2}s with {2} steps");

    /// <summary>
    /// リトライログ: "Retry attempt {0}/{1} for '{2}' after {3}ms"
    /// </summary>
    public static readonly CompositeFormat RetryAttempt =
        CompositeFormat.Parse("Retry attempt {0}/{1} for '{2}' after {3}ms");

    /// <summary>
    /// タイムアウトログ: "Operation '{0}' timed out after {1:F1}s"
    /// </summary>
    public static readonly CompositeFormat OperationTimeout =
        CompositeFormat.Parse("Operation '{0}' timed out after {1:F1}s");

    /// <summary>
    /// エラーログ: "[{0}] Error in {1}: {2}"
    /// </summary>
    public static readonly CompositeFormat ErrorLog =
        CompositeFormat.Parse("[{0}] Error in {1}: {2}");

    /// <summary>
    /// 進捗ログ: "Progress: {0}/{1} ({2:P0})"
    /// </summary>
    public static readonly CompositeFormat Progress =
        CompositeFormat.Parse("Progress: {0}/{1} ({2:P0})");

    /// <summary>
    /// メトリクスログ: "{0}: {1:N0} ops, {2:F2}ms avg, {3:F1}MB memory"
    /// </summary>
    public static readonly CompositeFormat Metrics =
        CompositeFormat.Parse("{0}: {1:N0} ops, {2:F2}ms avg, {3:F1}MB memory");

    /// <summary>
    /// ステップ開始メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatStepStarted(string stepName, DateTime timestamp) =>
        string.Format(null, StepStarted, stepName, timestamp);

    /// <summary>
    /// ステップ完了メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatStepCompleted(string stepName, double elapsedMs) =>
        string.Format(null, StepCompleted, stepName, elapsedMs);

    /// <summary>
    /// ステップ失敗メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatStepFailed(string stepName, string error) =>
        string.Format(null, StepFailed, stepName, error);

    /// <summary>
    /// ワークフロー開始メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatWorkflowStarted(string workflowName, string workflowId) =>
        string.Format(null, WorkflowStarted, workflowName, workflowId);

    /// <summary>
    /// ワークフロー完了メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatWorkflowCompleted(string workflowName, double elapsedSeconds, int stepCount) =>
        string.Format(null, WorkflowCompleted, workflowName, elapsedSeconds, stepCount);

    /// <summary>
    /// リトライメッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatRetryAttempt(int attempt, int maxAttempts, string operationName, int delayMs) =>
        string.Format(null, RetryAttempt, attempt, maxAttempts, operationName, delayMs);

    /// <summary>
    /// 進捗メッセージをフォーマット
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatProgress(int current, int total) =>
        string.Format(null, Progress, current, total, (double)current / total);
}

/// <summary>
/// ゼロアロケーション文字列構築
/// Span&lt;char&gt; を使用してヒープ割り当てを回避
/// </summary>
public ref struct SpanStringBuilder
{
    private readonly Span<char> _buffer;
    private int _position;

    public SpanStringBuilder(Span<char> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>
    /// 現在の長さ
    /// </summary>
    public int Length => _position;

    /// <summary>
    /// 残り容量
    /// </summary>
    public int Remaining => _buffer.Length - _position;

    /// <summary>
    /// 文字を追加
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Append(char c)
    {
        if (_position >= _buffer.Length) return false;
        _buffer[_position++] = c;
        return true;
    }

    /// <summary>
    /// 文字列を追加
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Append(ReadOnlySpan<char> value)
    {
        if (value.Length > Remaining) return false;
        value.CopyTo(_buffer[_position..]);
        _position += value.Length;
        return true;
    }

    /// <summary>
    /// 整数を追加 (アロケーションなし)
    /// </summary>
    public bool Append(int value)
    {
        if (Remaining < 12) return false;
        if (value.TryFormat(_buffer[_position..], out var written))
        {
            _position += written;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 長整数を追加 (アロケーションなし)
    /// </summary>
    public bool Append(long value)
    {
        if (Remaining < 21) return false;
        if (value.TryFormat(_buffer[_position..], out var written))
        {
            _position += written;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 浮動小数点を追加 (アロケーションなし)
    /// </summary>
    public bool Append(double value, string? format = null)
    {
        if (Remaining < 32) return false;
        if (value.TryFormat(_buffer[_position..], out var written, format))
        {
            _position += written;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 日時を追加 (アロケーションなし)
    /// </summary>
    public bool Append(DateTime value, string? format = null)
    {
        if (Remaining < 64) return false;
        if (value.TryFormat(_buffer[_position..], out var written, format))
        {
            _position += written;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 改行を追加
    /// </summary>
    public bool AppendLine()
    {
        return Append(Environment.NewLine);
    }

    /// <summary>
    /// 改行付きで文字列を追加
    /// </summary>
    public bool AppendLine(ReadOnlySpan<char> value)
    {
        return Append(value) && AppendLine();
    }

    /// <summary>
    /// 結果を ReadOnlySpan として取得
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => _buffer[.._position];

    /// <summary>
    /// 結果を文字列として取得
    /// </summary>
    public override string ToString() => new(_buffer[.._position]);

    /// <summary>
    /// バッファをクリア
    /// </summary>
    public void Clear() => _position = 0;
}

/// <summary>
/// ArrayPool を使用した StringBuilder 代替
/// 大きなバッファでも効率的にメモリを使用
/// </summary>
public sealed class PooledStringBuilder : IDisposable
{
    private char[] _buffer;
    private int _position;
    private bool _disposed;

    public PooledStringBuilder(int initialCapacity = 256)
    {
        _buffer = ArrayPool<char>.Shared.Rent(initialCapacity);
        _position = 0;
    }

    /// <summary>
    /// 現在の長さ
    /// </summary>
    public int Length => _position;

    /// <summary>
    /// 容量
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// 文字を追加
    /// </summary>
    public PooledStringBuilder Append(char c)
    {
        EnsureCapacity(1);
        _buffer[_position++] = c;
        return this;
    }

    /// <summary>
    /// 文字列を追加
    /// </summary>
    public PooledStringBuilder Append(ReadOnlySpan<char> value)
    {
        EnsureCapacity(value.Length);
        value.CopyTo(_buffer.AsSpan()[_position..]);
        _position += value.Length;
        return this;
    }

    /// <summary>
    /// 整数を追加
    /// </summary>
    public PooledStringBuilder Append(int value)
    {
        Span<char> temp = stackalloc char[12];
        if (value.TryFormat(temp, out var written))
        {
            Append(temp[..written]);
        }
        return this;
    }

    /// <summary>
    /// 浮動小数点を追加
    /// </summary>
    public PooledStringBuilder Append(double value, string? format = null)
    {
        Span<char> temp = stackalloc char[32];
        if (value.TryFormat(temp, out var written, format))
        {
            Append(temp[..written]);
        }
        return this;
    }

    /// <summary>
    /// フォーマット付きで追加
    /// </summary>
    public PooledStringBuilder AppendFormat(CompositeFormat format, params object?[] args)
    {
        var formatted = string.Format(null, format, args);
        return Append(formatted);
    }

    /// <summary>
    /// 改行を追加
    /// </summary>
    public PooledStringBuilder AppendLine()
    {
        return Append(Environment.NewLine);
    }

    /// <summary>
    /// 改行付きで文字列を追加
    /// </summary>
    public PooledStringBuilder AppendLine(ReadOnlySpan<char> value)
    {
        Append(value);
        return AppendLine();
    }

    private void EnsureCapacity(int additionalLength)
    {
        var requiredCapacity = _position + additionalLength;
        if (requiredCapacity <= _buffer.Length) return;

        var newCapacity = Math.Max(_buffer.Length * 2, requiredCapacity);
        var newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    /// <summary>
    /// 結果を文字列として取得
    /// </summary>
    public override string ToString() => new(_buffer, 0, _position);

    /// <summary>
    /// バッファをクリア (再利用可能)
    /// </summary>
    public void Clear() => _position = 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = Array.Empty<char>();
    }
}

/// <summary>
/// 構造化ログ用の高速フォーマッター
/// </summary>
public static class StructuredLogFormatter
{
    private static readonly CompositeFormat LogFormat =
        CompositeFormat.Parse("[{0:HH:mm:ss.fff}] [{1}] {2}: {3}");

    private static readonly CompositeFormat LogWithContextFormat =
        CompositeFormat.Parse("[{0:HH:mm:ss.fff}] [{1}] [{2}] {3}: {4}");

    /// <summary>
    /// 構造化ログをフォーマット
    /// </summary>
    public static string Format(DateTime timestamp, string level, string category, string message) =>
        string.Format(null, LogFormat, timestamp, level, category, message);

    /// <summary>
    /// コンテキスト付き構造化ログをフォーマット
    /// </summary>
    public static string Format(DateTime timestamp, string level, string correlationId, string category, string message) =>
        string.Format(null, LogWithContextFormat, timestamp, level, correlationId, category, message);

    /// <summary>
    /// Span を使用した超高速フォーマット
    /// </summary>
    public static int FormatTo(
        Span<char> destination,
        DateTime timestamp,
        ReadOnlySpan<char> level,
        ReadOnlySpan<char> category,
        ReadOnlySpan<char> message)
    {
        var builder = new SpanStringBuilder(destination);

        builder.Append('[');
        builder.Append(timestamp, "HH:mm:ss.fff");
        builder.Append("] [");
        builder.Append(level);
        builder.Append("] ");
        builder.Append(category);
        builder.Append(": ");
        builder.Append(message);

        return builder.Length;
    }
}
