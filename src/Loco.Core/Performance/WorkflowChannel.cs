using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// System.Threading.Channels を使った高性能ワークフローキュー
///
/// パフォーマンス特性:
/// - BlockingCollection より高速
/// - TPL Dataflow より軽量
/// - ValueTask によるアロケーション最小化
/// - バックプレッシャー対応
///
/// 参考: https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
/// </summary>
public sealed class WorkflowChannel<T> : IDisposable
{
    private readonly Channel<T> _channel;
    private readonly ChannelWriter<T> _writer;
    private readonly ChannelReader<T> _reader;
    private bool _disposed;

    /// <summary>
    /// バウンド付きチャネルを作成（メモリ制限あり）
    /// </summary>
    public WorkflowChannel(int capacity = 1000, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = fullMode,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false // デッドロック防止
        };

        _channel = Channel.CreateBounded<T>(options);
        _writer = _channel.Writer;
        _reader = _channel.Reader;
    }

    /// <summary>
    /// アンバウンドチャネルを作成（メモリ制限なし）
    /// 注意: 大量データではメモリ不足の可能性あり
    /// </summary>
    public static WorkflowChannel<T> CreateUnbounded()
    {
        var channel = new WorkflowChannel<T>(int.MaxValue);
        return channel;
    }

    /// <summary>
    /// アイテムを非同期で書き込み（バックプレッシャー対応）
    /// </summary>
    public async ValueTask WriteAsync(T item, CancellationToken ct = default)
    {
        await _writer.WriteAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// アイテムを同期で書き込み（即座に成功/失敗が判明）
    /// </summary>
    public bool TryWrite(T item)
    {
        return _writer.TryWrite(item);
    }

    /// <summary>
    /// アイテムを非同期で読み込み
    /// </summary>
    public async ValueTask<T> ReadAsync(CancellationToken ct = default)
    {
        return await _reader.ReadAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// アイテムを同期で読み込み
    /// </summary>
    public bool TryRead(out T? item)
    {
        return _reader.TryRead(out item);
    }

    /// <summary>
    /// IAsyncEnumerable でストリーミング読み込み
    /// await foreach で使用可能
    /// </summary>
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct = default)
    {
        return _reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// 書き込み完了を通知
    /// これ以降の書き込みは例外をスロー
    /// </summary>
    public void Complete(Exception? error = null)
    {
        _writer.TryComplete(error);
    }

    /// <summary>
    /// チャネルの完了を待機
    /// </summary>
    public Task Completion => _reader.Completion;

    /// <summary>
    /// 読み込み可能なアイテム数（概算）
    /// </summary>
    public int Count => _reader.Count;

    /// <summary>
    /// 読み込み可能かどうか
    /// </summary>
    public bool CanRead => _reader.CanCount;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.TryComplete();
    }
}

/// <summary>
/// ワークフローステップの非同期ストリーム実行
/// IAsyncEnumerable を使用して結果をストリーミング
///
/// メモリ効率:
/// - 全結果をメモリに保持しない
/// - 各結果を即座にクライアントに返却
/// - 大量データでもメモリ使用量一定
/// </summary>
public static class WorkflowStreaming
{
    /// <summary>
    /// ワークフローステップを非同期ストリームで実行
    /// </summary>
    public static async IAsyncEnumerable<StepResult<TResult>> ExecuteStepsAsync<TInput, TResult>(
        IReadOnlyList<Func<TInput, CancellationToken, ValueTask<TResult>>> steps,
        TInput input,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var step = steps[i];
            var startTime = DateTime.UtcNow;

            TResult result;
            Exception? error = null;

            try
            {
                result = await step(input, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = default!;
                error = ex;
            }

            yield return new StepResult<TResult>
            {
                StepIndex = i,
                Result = result,
                Error = error,
                Duration = DateTime.UtcNow - startTime,
                IsSuccess = error == null
            };

            // エラーで中断するかどうかはコンシューマーが決定
        }
    }

    /// <summary>
    /// 並列ワークフロー実行をストリーミング
    /// 完了した順に結果を返却
    /// </summary>
    public static async IAsyncEnumerable<StepResult<TResult>> ExecuteParallelAsync<TInput, TResult>(
        IReadOnlyList<Func<TInput, CancellationToken, ValueTask<TResult>>> steps,
        TInput input,
        int maxConcurrency = 4,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = new WorkflowChannel<StepResult<TResult>>(maxConcurrency * 2);
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>();

        for (int i = 0; i < steps.Count; i++)
        {
            var index = i;
            var step = steps[i];

            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var startTime = DateTime.UtcNow;
                    TResult result;
                    Exception? error = null;

                    try
                    {
                        result = await step(input, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        result = default!;
                        error = ex;
                    }

                    await channel.WriteAsync(new StepResult<TResult>
                    {
                        StepIndex = index,
                        Result = result,
                        Error = error,
                        Duration = DateTime.UtcNow - startTime,
                        IsSuccess = error == null
                    }, ct).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);

            tasks.Add(task);
        }

        // 全タスク完了後にチャネルを閉じる
        _ = Task.WhenAll(tasks).ContinueWith(_ => channel.Complete(), ct);

        // 結果をストリーミング
        await foreach (var result in channel.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return result;
        }

        semaphore.Dispose();
        channel.Dispose();
    }

    /// <summary>
    /// データソースからのストリーミング処理
    /// 大量データを効率的に処理
    /// </summary>
    public static async IAsyncEnumerable<TResult> TransformStreamAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> transform,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return await transform(item, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// バッチ処理をストリーミング
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<T>> BatchAsync<T>(
        IAsyncEnumerable<T> source,
        int batchSize,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<T>(batchSize);

        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }

        // 残りのアイテム
        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }
}

/// <summary>
/// ステップ実行結果
/// </summary>
public readonly struct StepResult<T>
{
    public int StepIndex { get; init; }
    public T Result { get; init; }
    public Exception? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public bool IsSuccess { get; init; }

    public override string ToString()
    {
        return IsSuccess
            ? $"Step {StepIndex}: Success ({Duration.TotalMilliseconds:F1}ms)"
            : $"Step {StepIndex}: Failed - {Error?.Message} ({Duration.TotalMilliseconds:F1}ms)";
    }
}

/// <summary>
/// Producer-Consumer パターンのワークフローパイプライン
/// </summary>
public sealed class WorkflowPipeline<TInput, TOutput> : IAsyncDisposable
{
    private readonly Channel<TInput> _inputChannel;
    private readonly Channel<TOutput> _outputChannel;
    private readonly Func<TInput, CancellationToken, ValueTask<TOutput>> _processor;
    private readonly int _concurrency;
    private readonly CancellationTokenSource _cts;
    private readonly Task[] _workers;

    public WorkflowPipeline(
        Func<TInput, CancellationToken, ValueTask<TOutput>> processor,
        int concurrency = 4,
        int inputCapacity = 1000,
        int outputCapacity = 1000)
    {
        _processor = processor;
        _concurrency = concurrency;
        _cts = new CancellationTokenSource();

        _inputChannel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(inputCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _outputChannel = Channel.CreateBounded<TOutput>(new BoundedChannelOptions(outputCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _workers = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            _workers[i] = ProcessAsync(_cts.Token);
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        await foreach (var input in _inputChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var output = await _processor(input, ct).ConfigureAwait(false);
                await _outputChannel.Writer.WriteAsync(output, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // エラーハンドリング（ログ等）
            }
        }
    }

    /// <summary>
    /// 入力を追加
    /// </summary>
    public async ValueTask EnqueueAsync(TInput input, CancellationToken ct = default)
    {
        await _inputChannel.Writer.WriteAsync(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 出力をストリーミング
    /// </summary>
    public IAsyncEnumerable<TOutput> GetResultsAsync(CancellationToken ct = default)
    {
        return _outputChannel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// 入力完了を通知
    /// </summary>
    public void CompleteInput()
    {
        _inputChannel.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        _inputChannel.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _outputChannel.Writer.TryComplete();
        _cts.Dispose();
    }
}
