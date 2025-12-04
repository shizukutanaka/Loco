using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// TimeProvider を活用した時間抽象化
///
/// .NET 8 の TimeProvider の利点:
/// - テスト可能: FakeTimeProvider でモック可能
/// - 予測可能: 時間依存コードのテストが容易
/// - 標準化: 独自の IDateTimeProvider 不要
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview
/// </summary>

/// <summary>
/// ワークフロー実行に特化した時間操作
/// </summary>
public sealed class WorkflowTimeProvider
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// システムデフォルトのTimeProviderを使用
    /// </summary>
    public WorkflowTimeProvider() : this(TimeProvider.System) { }

    /// <summary>
    /// カスタムTimeProviderを使用 (テスト用)
    /// </summary>
    public WorkflowTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 現在のUTC時刻
    /// </summary>
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    /// <summary>
    /// 現在のローカル時刻
    /// </summary>
    public DateTimeOffset LocalNow => _timeProvider.GetLocalNow();

    /// <summary>
    /// ローカルタイムゾーン
    /// </summary>
    public TimeZoneInfo LocalTimeZone => _timeProvider.LocalTimeZone;

    /// <summary>
    /// 高精度タイムスタンプ
    /// </summary>
    public long GetTimestamp() => _timeProvider.GetTimestamp();

    /// <summary>
    /// 2つのタイムスタンプ間の経過時間
    /// </summary>
    public TimeSpan GetElapsedTime(long startingTimestamp) =>
        _timeProvider.GetElapsedTime(startingTimestamp);

    /// <summary>
    /// 2つのタイムスタンプ間の経過時間
    /// </summary>
    public TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
        _timeProvider.GetElapsedTime(startingTimestamp, endingTimestamp);

    /// <summary>
    /// 非同期遅延 (テスト可能)
    /// </summary>
    public Task DelayAsync(TimeSpan delay, CancellationToken ct = default) =>
        DelayUsingTimer(_timeProvider, delay, ct);

    /// <summary>
    /// タイマーを作成 (テスト可能)
    /// </summary>
    public ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _timeProvider.CreateTimer(callback, state, dueTime, period);

    /// <summary>
    /// TimeProviderを使用した遅延実装 (timer ベース)
    /// </summary>
    internal static Task DelayUsingTimer(TimeProvider provider, TimeSpan delay, CancellationToken ct = default)
    {
        if (delay == TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        var timer = provider.CreateTimer(
            _ => tcs.TrySetResult(),
            null,
            delay,
            Timeout.InfiniteTimeSpan);

        return tcs.Task.ContinueWith(_ => timer.Dispose(), TaskScheduler.Default);
    }
}

/// <summary>
/// 実行時間を計測するストップウォッチ (TimeProvider対応)
/// </summary>
public sealed class WorkflowStopwatch
{
    private readonly TimeProvider _timeProvider;
    private readonly long _frequency;
    private long _startTimestamp;
    private long _stopTimestamp;
    private bool _isRunning;

    public WorkflowStopwatch() : this(TimeProvider.System) { }

    public WorkflowStopwatch(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _frequency = timeProvider.TimestampFrequency;
        Reset();
    }

    /// <summary>
    /// 計測を開始
    /// </summary>
    public void Start()
    {
        if (!_isRunning)
        {
            _startTimestamp = _timeProvider.GetTimestamp();
            _isRunning = true;
        }
    }

    /// <summary>
    /// 計測を停止
    /// </summary>
    public void Stop()
    {
        if (_isRunning)
        {
            _stopTimestamp = _timeProvider.GetTimestamp();
            _isRunning = false;
        }
    }

    /// <summary>
    /// 計測をリセット
    /// </summary>
    public void Reset()
    {
        _startTimestamp = 0;
        _stopTimestamp = 0;
        _isRunning = false;
    }

    /// <summary>
    /// 計測をリセットして開始
    /// </summary>
    public void Restart()
    {
        Reset();
        Start();
    }

    /// <summary>
    /// 計測中かどうか
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 経過時間
    /// </summary>
    public TimeSpan Elapsed
    {
        get
        {
            var endTimestamp = _isRunning ? _timeProvider.GetTimestamp() : _stopTimestamp;
            return _timeProvider.GetElapsedTime(_startTimestamp, endTimestamp);
        }
    }

    /// <summary>
    /// 経過ミリ秒
    /// </summary>
    public double ElapsedMilliseconds => Elapsed.TotalMilliseconds;

    /// <summary>
    /// 新しいインスタンスを作成して開始
    /// </summary>
    public static WorkflowStopwatch StartNew(TimeProvider? timeProvider = null)
    {
        var sw = new WorkflowStopwatch(timeProvider ?? TimeProvider.System);
        sw.Start();
        return sw;
    }
}

/// <summary>
/// タイムアウト処理のヘルパー
/// </summary>
public sealed class TimeoutHandler : IDisposable
{
    private readonly CancellationTokenSource _timeoutCts;
    private readonly CancellationTokenSource _linkedCts;
    private bool _disposed;

    public TimeoutHandler(TimeSpan timeout, CancellationToken linkedToken = default)
        : this(timeout, TimeProvider.System, linkedToken) { }

    public TimeoutHandler(TimeSpan timeout, TimeProvider timeProvider, CancellationToken linkedToken = default)
    {
        _timeoutCts = new CancellationTokenSource();
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_timeoutCts.Token, linkedToken);

        // TimeProviderを使用してタイムアウトをスケジュール
        timeProvider.CreateTimer(
            _ => _timeoutCts.Cancel(),
            null,
            timeout,
            Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// タイムアウト付きのCancellationToken
    /// </summary>
    public CancellationToken Token => _linkedCts.Token;

    /// <summary>
    /// タイムアウトしたかどうか
    /// </summary>
    public bool IsTimedOut => _timeoutCts.IsCancellationRequested;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timeoutCts.Dispose();
        _linkedCts.Dispose();
    }
}

/// <summary>
/// 周期的なタスク実行 (TimeProvider対応)
/// </summary>
public sealed class PeriodicTimer : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _period;
    private ITimer? _timer;
    private TaskCompletionSource<bool>? _tcs;
    private bool _disposed;

    public PeriodicTimer(TimeSpan period) : this(period, TimeProvider.System) { }

    public PeriodicTimer(TimeSpan period, TimeProvider timeProvider)
    {
        _period = period;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 次のティックを待機
    /// </summary>
    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = ct.Register(() =>
        {
            _tcs?.TrySetResult(false);
        });

        _timer = _timeProvider.CreateTimer(
            _ => _tcs?.TrySetResult(true),
            null,
            _period,
            Timeout.InfiniteTimeSpan);

        try
        {
            return await _tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _tcs?.TrySetResult(false);
    }
}

/// <summary>
/// 遅延実行ヘルパー (TimeProvider対応)
/// </summary>
public static class DelayHelper
{
    /// <summary>
    /// 指数バックオフ付き遅延
    /// </summary>
    public static async Task ExponentialBackoffAsync(
        int attempt,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        TimeProvider? timeProvider = null,
        CancellationToken ct = default)
    {
        var provider = timeProvider ?? TimeProvider.System;
        var delay = TimeSpan.FromMilliseconds(
            Math.Min(
                baseDelay.TotalMilliseconds * Math.Pow(2, attempt),
                maxDelay.TotalMilliseconds));

        await WorkflowTimeProvider.DelayUsingTimer(provider, delay, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// ジッター付き遅延 (サンダリングハード問題対策)
    /// </summary>
    public static async Task DelayWithJitterAsync(
        TimeSpan delay,
        double jitterFactor = 0.2,
        TimeProvider? timeProvider = null,
        CancellationToken ct = default)
    {
        var provider = timeProvider ?? TimeProvider.System;
        var jitter = delay.TotalMilliseconds * jitterFactor * Random.Shared.NextDouble();
        var actualDelay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);

        await WorkflowTimeProvider.DelayUsingTimer(provider, actualDelay, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// スケジュール実行のヘルパー
/// </summary>
public sealed class ScheduledExecution : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly Func<CancellationToken, Task> _action;
    private ITimer? _timer;
    private bool _disposed;

    public ScheduledExecution(
        DateTimeOffset scheduledTime,
        Func<CancellationToken, Task> action,
        TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _action = action ?? throw new ArgumentNullException(nameof(action));

        var delay = scheduledTime - _timeProvider.GetUtcNow();
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _timer = _timeProvider.CreateTimer(
            OnTimerCallback,
            null,
            delay,
            Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// 実行完了を待機
    /// </summary>
    public Task CompletionTask => _completionTcs.Task;
    private readonly TaskCompletionSource _completionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async void OnTimerCallback(object? state)
    {
        try
        {
            await _action(CancellationToken.None).ConfigureAwait(false);
            _completionTcs.TrySetResult();
        }
        catch (Exception ex)
        {
            _completionTcs.TrySetException(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _completionTcs.TrySetCanceled();
    }
}

/// <summary>
/// タイムスタンプ付きログエントリ
/// </summary>
public readonly record struct TimestampedEntry<T>
{
    public DateTimeOffset Timestamp { get; init; }
    public T Value { get; init; }
    public TimeSpan SinceStart { get; init; }

    public static TimestampedEntry<T> Create(T value, DateTimeOffset timestamp, DateTimeOffset startTime)
    {
        return new TimestampedEntry<T>
        {
            Timestamp = timestamp,
            Value = value,
            SinceStart = timestamp - startTime
        };
    }
}
