using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Loco.Core.Performance;

/// <summary>
/// IAsyncEnumerable を活用した高性能ストリーミング
///
/// パフォーマンス改善:
/// - メモリ効率: 全データをロードせずにストリーミング処理
/// - 応答性: 準備できた順に結果を返す
/// - キャンセル: CancellationToken の適切な伝播
///
/// ValueTask 使用により:
/// - 同期完了時のアロケーション削減
/// - 非同期境界でのオーバーヘッド軽減
///
/// 参考: https://qiita.com/TsuyoshiUshio@github/items/c4b9929d88d1cd8cabb1
/// </summary>
public static class AsyncStreaming
{
    /// <summary>
    /// 非同期シーケンスをバッチ処理
    /// メモリ効率的に大量データを処理
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
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// 非同期シーケンスをフィルタリング
    /// </summary>
    public static async IAsyncEnumerable<T> WhereAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// 非同期シーケンスを変換
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, TResult> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return selector(item);
        }
    }

    /// <summary>
    /// 非同期シーケンスを非同期変換
    /// </summary>
    public static async IAsyncEnumerable<TResult> SelectAwaitAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, ValueTask<TResult>> selector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return await selector(item, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 非同期シーケンスの先頭N件を取得
    /// </summary>
    public static async IAsyncEnumerable<T> TakeAsync<T>(
        IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var taken = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (taken >= count) yield break;
            yield return item;
            taken++;
        }
    }

    /// <summary>
    /// 非同期シーケンスの先頭N件をスキップ
    /// </summary>
    public static async IAsyncEnumerable<T> SkipAsync<T>(
        IAsyncEnumerable<T> source,
        int count,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var skipped = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (skipped < count)
            {
                skipped++;
                continue;
            }
            yield return item;
        }
    }

    /// <summary>
    /// タイムアウト付き非同期シーケンス
    /// </summary>
    public static async IAsyncEnumerable<T> WithTimeoutAsync<T>(
        IAsyncEnumerable<T> source,
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        await foreach (var item in source.WithCancellation(timeoutCts.Token).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// 非同期シーケンスをリストに変換
    /// </summary>
    public static async ValueTask<List<T>> ToListAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>
    /// 非同期シーケンスの要素数をカウント
    /// </summary>
    public static async ValueTask<int> CountAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        var count = 0;
        await foreach (var _ in source.WithCancellation(ct).ConfigureAwait(false))
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// 非同期シーケンスの最初の要素を取得
    /// </summary>
    public static async ValueTask<T?> FirstOrDefaultAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return item;
        }
        return default;
    }

    /// <summary>
    /// 非同期シーケンスに要素が存在するかチェック
    /// </summary>
    public static async ValueTask<bool> AnyAsync<T>(
        IAsyncEnumerable<T> source,
        CancellationToken ct = default)
    {
        await foreach (var _ in source.WithCancellation(ct).ConfigureAwait(false))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 非同期シーケンスに条件を満たす要素が存在するかチェック
    /// </summary>
    public static async ValueTask<bool> AnyAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, bool> predicate,
        CancellationToken ct = default)
    {
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (predicate(item)) return true;
        }
        return false;
    }

    /// <summary>
    /// 進捗報告付き非同期シーケンス
    /// </summary>
    public static async IAsyncEnumerable<T> WithProgressAsync<T>(
        IAsyncEnumerable<T> source,
        IProgress<int>? progress,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var count = 0;
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return item;
            count++;
            progress?.Report(count);
        }
    }

    /// <summary>
    /// 範囲から非同期シーケンスを生成
    /// </summary>
    public static async IAsyncEnumerable<int> RangeAsync(
        int start,
        int count,
        TimeSpan delay = default,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            yield return start + i;
        }
    }

    /// <summary>
    /// 複数のソースをマージ (到着順)
    /// </summary>
    public static async IAsyncEnumerable<T> MergeAsync<T>(
        IEnumerable<IAsyncEnumerable<T>> sources,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var tasks = new List<Task>();
        foreach (var source in sources)
        {
            tasks.Add(ProcessSourceAsync(source, channel.Writer, ct));
        }

        _ = Task.WhenAll(tasks).ContinueWith(
            _ => channel.Writer.Complete(),
            TaskScheduler.Default);

        await foreach (var item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private static async Task ProcessSourceAsync<T>(
        IAsyncEnumerable<T> source,
        ChannelWriter<T> writer,
        CancellationToken ct)
    {
        try
        {
            await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
            {
                await writer.WriteAsync(item, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルは正常終了
        }
    }

    /// <summary>
    /// デバウンス (連続した要素をまとめる)
    /// </summary>
    public static async IAsyncEnumerable<T> DebounceAsync<T>(
        IAsyncEnumerable<T> source,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        T? lastItem = default;
        var hasItem = false;
        var lastTime = DateTime.UtcNow;

        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;

            if (hasItem && (now - lastTime) >= interval)
            {
                yield return lastItem!;
            }

            lastItem = item;
            hasItem = true;
            lastTime = now;
        }

        if (hasItem)
        {
            yield return lastItem!;
        }
    }

    /// <summary>
    /// 重複を除去
    /// </summary>
    public static async IAsyncEnumerable<T> DistinctAsync<T>(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<T>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            if (seen.Add(item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// キー別に重複を除去
    /// </summary>
    public static async IAsyncEnumerable<T> DistinctByAsync<T, TKey>(
        IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var seen = new HashSet<TKey>();
        await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var key = keySelector(item);
            if (seen.Add(key))
            {
                yield return item;
            }
        }
    }
}

/// <summary>
/// ワークフロー実行のストリーミング結果
/// </summary>
public readonly record struct StreamingStepResult<T>
{
    public StepId StepId { get; init; }
    public int StepIndex { get; init; }
    public T? Result { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Duration Duration { get; init; }

    public static StreamingStepResult<T> Succeeded(StepId stepId, int index, T result, Duration duration) => new()
    {
        StepId = stepId,
        StepIndex = index,
        Result = result,
        Success = true,
        Duration = duration
    };

    public static StreamingStepResult<T> Failed(StepId stepId, int index, string error, Duration duration) => new()
    {
        StepId = stepId,
        StepIndex = index,
        Success = false,
        Error = error,
        Duration = duration
    };
}

/// <summary>
/// ワークフローストリーミング実行器
/// </summary>
public static class WorkflowStreamingExecutor
{
    /// <summary>
    /// ステップを順次ストリーミング実行
    /// </summary>
    public static async IAsyncEnumerable<StreamingStepResult<T>> ExecuteStepsAsync<T>(
        IEnumerable<(StepId Id, Func<CancellationToken, ValueTask<T>> Action)> steps,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var index = 0;
        foreach (var (stepId, action) in steps)
        {
            var startTime = Timestamp.Now;

            StreamingStepResult<T> result;
            try
            {
                var value = await action(ct).ConfigureAwait(false);
                var duration = startTime.Until(Timestamp.Now);
                result = StreamingStepResult<T>.Succeeded(stepId, index, value, duration);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var duration = startTime.Until(Timestamp.Now);
                result = StreamingStepResult<T>.Failed(stepId, index, ex.Message, duration);
            }

            yield return result;
            index++;

            if (!result.Success)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// ステップを並列ストリーミング実行 (到着順で結果を返す)
    /// </summary>
    public static async IAsyncEnumerable<StreamingStepResult<T>> ExecuteParallelAsync<T>(
        IEnumerable<(StepId Id, Func<CancellationToken, ValueTask<T>> Action)> steps,
        int maxParallelism = 4,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<StreamingStepResult<T>>(
            new BoundedChannelOptions(maxParallelism)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        var stepsList = steps.ToList();
        using var semaphore = new SemaphoreSlim(maxParallelism);

        var processingTask = Task.Run(async () =>
        {
            var tasks = new List<Task>();
            var index = 0;

            foreach (var (stepId, action) in stepsList)
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                var currentIndex = index++;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var startTime = Timestamp.Now;
                        StreamingStepResult<T> result;

                        try
                        {
                            var value = await action(ct).ConfigureAwait(false);
                            var duration = startTime.Until(Timestamp.Now);
                            result = StreamingStepResult<T>.Succeeded(stepId, currentIndex, value, duration);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            var duration = startTime.Until(Timestamp.Now);
                            result = StreamingStepResult<T>.Failed(stepId, currentIndex, ex.Message, duration);
                        }

                        await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            channel.Writer.Complete();
        }, ct);

        await foreach (var result in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return result;
        }

        await processingTask.ConfigureAwait(false);
    }
}
