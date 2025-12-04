using System;
using System.Diagnostics.CodeAnalysis;

namespace Loco.Core.Performance;

/// <summary>
/// 高性能な値オブジェクト - record struct を使用
///
/// パフォーマンス改善:
/// - スタック割り当て: ヒープアロケーション 0
/// - GC圧力: 構造体のため GC 対象外
/// - 値ベースの等価性: 自動生成
/// - with 式: 不変オブジェクトの効率的なコピー
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record
/// </summary>

/// <summary>
/// ワークフロー実行ID - 不変の識別子
/// </summary>
public readonly record struct ExecutionId : IEquatable<ExecutionId>
{
    public string Value { get; }
    public DateTime CreatedAt { get; }

    public ExecutionId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        CreatedAt = DateTime.UtcNow;
    }

    public static ExecutionId New() => new(Guid.NewGuid().ToString("N"));
    public static ExecutionId Empty => new(string.Empty);

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;

    public static implicit operator string(ExecutionId id) => id.Value;
}

/// <summary>
/// ステップID - ワークフロー内のステップ識別子
/// </summary>
public readonly record struct StepId : IEquatable<StepId>
{
    public string Value { get; }

    public StepId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;

    public static implicit operator string(StepId id) => id.Value;
    public static implicit operator StepId(string value) => new(value);
}

/// <summary>
/// ワークフローID - ワークフロー定義の識別子
/// </summary>
public readonly record struct WorkflowId : IEquatable<WorkflowId>
{
    public string Value { get; }

    public WorkflowId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;

    public static implicit operator string(WorkflowId id) => id.Value;
    public static implicit operator WorkflowId(string value) => new(value);
}

/// <summary>
/// 実行時間 - 高精度の時間計測
/// </summary>
public readonly record struct Duration : IComparable<Duration>
{
    public long Ticks { get; }

    private Duration(long ticks) => Ticks = ticks;

    public static Duration FromTicks(long ticks) => new(ticks);
    public static Duration FromMilliseconds(double ms) => new((long)(ms * TimeSpan.TicksPerMillisecond));
    public static Duration FromSeconds(double seconds) => new((long)(seconds * TimeSpan.TicksPerSecond));
    public static Duration Zero => new(0);

    public double TotalMilliseconds => (double)Ticks / TimeSpan.TicksPerMillisecond;
    public double TotalSeconds => (double)Ticks / TimeSpan.TicksPerSecond;
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Ticks);

    public int CompareTo(Duration other) => Ticks.CompareTo(other.Ticks);

    public static Duration operator +(Duration a, Duration b) => new(a.Ticks + b.Ticks);
    public static Duration operator -(Duration a, Duration b) => new(a.Ticks - b.Ticks);
    public static bool operator <(Duration a, Duration b) => a.Ticks < b.Ticks;
    public static bool operator >(Duration a, Duration b) => a.Ticks > b.Ticks;
    public static bool operator <=(Duration a, Duration b) => a.Ticks <= b.Ticks;
    public static bool operator >=(Duration a, Duration b) => a.Ticks >= b.Ticks;

    public override string ToString() => TotalMilliseconds < 1000
        ? $"{TotalMilliseconds:F2}ms"
        : $"{TotalSeconds:F2}s";
}

/// <summary>
/// ステップ結果 - 不変の実行結果
/// </summary>
public readonly record struct StepExecutionResult
{
    public StepId StepId { get; init; }
    public bool Success { get; init; }
    public Duration Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }

    public static StepExecutionResult Succeeded(StepId stepId, Duration duration) => new()
    {
        StepId = stepId,
        Success = true,
        Duration = duration,
        ErrorMessage = null,
        RetryCount = 0
    };

    public static StepExecutionResult Failed(StepId stepId, Duration duration, string errorMessage, int retryCount = 0) => new()
    {
        StepId = stepId,
        Success = false,
        Duration = duration,
        ErrorMessage = errorMessage,
        RetryCount = retryCount
    };
}

/// <summary>
/// メモリ使用量 - バイト単位の効率的な表現
/// </summary>
public readonly record struct MemorySize : IComparable<MemorySize>
{
    public long Bytes { get; }

    private MemorySize(long bytes) => Bytes = bytes;

    public static MemorySize FromBytes(long bytes) => new(bytes);
    public static MemorySize FromKilobytes(double kb) => new((long)(kb * 1024));
    public static MemorySize FromMegabytes(double mb) => new((long)(mb * 1024 * 1024));
    public static MemorySize FromGigabytes(double gb) => new((long)(gb * 1024 * 1024 * 1024));
    public static MemorySize Zero => new(0);

    public double Kilobytes => Bytes / 1024.0;
    public double Megabytes => Bytes / (1024.0 * 1024.0);
    public double Gigabytes => Bytes / (1024.0 * 1024.0 * 1024.0);

    public int CompareTo(MemorySize other) => Bytes.CompareTo(other.Bytes);

    public static MemorySize operator +(MemorySize a, MemorySize b) => new(a.Bytes + b.Bytes);
    public static MemorySize operator -(MemorySize a, MemorySize b) => new(a.Bytes - b.Bytes);

    public override string ToString()
    {
        if (Bytes < 1024) return $"{Bytes} B";
        if (Bytes < 1024 * 1024) return $"{Kilobytes:F2} KB";
        if (Bytes < 1024 * 1024 * 1024) return $"{Megabytes:F2} MB";
        return $"{Gigabytes:F2} GB";
    }
}

/// <summary>
/// 進捗状況 - 0-100%の範囲
/// </summary>
public readonly record struct Progress : IComparable<Progress>
{
    private readonly int _percentage;

    public int Percentage => _percentage;

    public Progress(int percentage)
    {
        _percentage = Math.Clamp(percentage, 0, 100);
    }

    public static Progress Zero => new(0);
    public static Progress Complete => new(100);
    public static Progress FromRatio(double ratio) => new((int)(Math.Clamp(ratio, 0, 1) * 100));
    public static Progress FromFraction(int current, int total) =>
        total > 0 ? new((int)((double)current / total * 100)) : Zero;

    public bool IsComplete => _percentage >= 100;
    public double Ratio => _percentage / 100.0;

    public int CompareTo(Progress other) => _percentage.CompareTo(other._percentage);

    public override string ToString() => $"{_percentage}%";
}

/// <summary>
/// タイムスタンプ - UTC時刻の不変表現
/// </summary>
public readonly record struct Timestamp : IComparable<Timestamp>
{
    public long UnixMilliseconds { get; }

    private Timestamp(long unixMs) => UnixMilliseconds = unixMs;

    public static Timestamp Now => new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    public static Timestamp FromDateTime(DateTime dt) => new(new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeMilliseconds());
    public static Timestamp FromUnixMilliseconds(long ms) => new(ms);

    public DateTime ToDateTime() => DateTimeOffset.FromUnixTimeMilliseconds(UnixMilliseconds).UtcDateTime;
    public DateTimeOffset ToDateTimeOffset() => DateTimeOffset.FromUnixTimeMilliseconds(UnixMilliseconds);

    public Duration Since(Timestamp other) => Duration.FromMilliseconds(UnixMilliseconds - other.UnixMilliseconds);
    public Duration Until(Timestamp other) => Duration.FromMilliseconds(other.UnixMilliseconds - UnixMilliseconds);

    public int CompareTo(Timestamp other) => UnixMilliseconds.CompareTo(other.UnixMilliseconds);

    public static bool operator <(Timestamp a, Timestamp b) => a.UnixMilliseconds < b.UnixMilliseconds;
    public static bool operator >(Timestamp a, Timestamp b) => a.UnixMilliseconds > b.UnixMilliseconds;

    public override string ToString() => ToDateTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
}

/// <summary>
/// 相関ID - 分散トレーシング用
/// </summary>
public readonly record struct CorrelationId : IEquatable<CorrelationId>
{
    public string Value { get; }

    public CorrelationId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static CorrelationId New() => new(Guid.NewGuid().ToString("N"));
    public static CorrelationId Empty => new(string.Empty);

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;

    public static implicit operator string(CorrelationId id) => id.Value;
}

/// <summary>
/// リトライ情報 - リトライ戦略の状態
/// </summary>
public readonly record struct RetryInfo
{
    public int Attempt { get; init; }
    public int MaxAttempts { get; init; }
    public Duration TotalDelay { get; init; }
    public Duration NextDelay { get; init; }

    public bool CanRetry => Attempt < MaxAttempts;
    public bool IsLastAttempt => Attempt >= MaxAttempts;
    public int RemainingAttempts => Math.Max(0, MaxAttempts - Attempt);

    public static RetryInfo Initial(int maxAttempts, Duration initialDelay) => new()
    {
        Attempt = 0,
        MaxAttempts = maxAttempts,
        TotalDelay = Duration.Zero,
        NextDelay = initialDelay
    };

    public RetryInfo Increment(Duration nextDelay) => this with
    {
        Attempt = Attempt + 1,
        TotalDelay = TotalDelay + NextDelay,
        NextDelay = nextDelay
    };

    public override string ToString() => $"Attempt {Attempt}/{MaxAttempts}, Total delay: {TotalDelay}";
}
