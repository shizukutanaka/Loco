using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Loco.Core.Examples;
using Loco.Core.Serialization;

namespace Loco.Core.DurableExecution;

/// <summary>
/// ワークフローオーケストレーター
/// Temporal風の耐久性のある実行を提供
/// </summary>
public class WorkflowOrchestrator
{
    private readonly EventStore _eventStore;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    public WorkflowOrchestrator(
        EventStore eventStore,
        ILogger<WorkflowOrchestrator> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    /// <summary>
    /// ワークフローを実行
    /// </summary>
    public async Task<WorkflowResult> ExecuteAsync(
        DurableWorkflow workflow,
        object input,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();
        var workflowId = workflow.GetType().Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting durable workflow: {WorkflowId} (ExecutionId: {ExecutionId})",
            workflowId,
            executionId);

        var context = new WorkflowContext(
            workflowId,
            executionId,
            input,
            _eventStore,
            _logger);

        workflow.Initialize(context);

        // 開始イベント記録
        await _eventStore.AppendEventAsync(
            workflowId,
            executionId,
            new WorkflowStartedEvent
            {
                Input = input,
                StartedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

        try
        {
            var result = await workflow.ExecuteAsync(cancellationToken);

            // 完了イベント記録
            await _eventStore.AppendEventAsync(
                workflowId,
                executionId,
                new WorkflowCompletedEvent
                {
                    Output = result.Output,
                    CompletedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "Workflow completed: {WorkflowId}, Duration: {Duration}ms",
                workflowId,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed: {WorkflowId}", workflowId);

            // 失敗イベント記録
            await _eventStore.AppendEventAsync(
                workflowId,
                executionId,
                new WorkflowFailedEvent
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    FailedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            // 補償アクション実行 (Saga)
            await context.ExecuteCompensationsAsync();

            throw;
        }
    }

    /// <summary>
    /// ワークフローを再開
    /// </summary>
    public async Task<WorkflowResult> ResumeAsync(
        DurableWorkflow workflow,
        string executionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming workflow: {ExecutionId}", executionId);

        // イベントストアから状態を再構築
        var state = await _eventStore.ReconstructStateAsync(executionId, cancellationToken);

        if (state.Status == WorkflowStatus.Completed)
        {
            return WorkflowResult.Successful(state.Output);
        }

        if (state.Status == WorkflowStatus.Failed)
        {
            return WorkflowResult.Failed(state.Error ?? "Unknown error");
        }

        var context = new WorkflowContext(
            workflow.GetType().Name,
            executionId,
            state.Input!,
            _eventStore,
            _logger);

        // 完了したアクティビティの結果をコンテキストに復元
        foreach (var kvp in state.Variables)
        {
            context.Variables[kvp.Key] = kvp.Value;
        }
        
        // 再生モードであることをコンテキストに通知（必要であれば）
        // context.IsReplaying = true;

        workflow.Initialize(context);

        try
        {
            // ワークフロー実行（決定論的であれば、完了済みステップはスキップされるはず）
            // 注意: WorkflowContext.ExecuteActivityAsync 内で重複実行チェックが必要
            var result = await workflow.ExecuteAsync(cancellationToken);

            // 完了イベント記録（まだ記録されていない場合のみ）
            if (state.Status != WorkflowStatus.Completed)
            {
                await _eventStore.AppendEventAsync(
                    workflow.GetType().Name,
                    executionId,
                    new WorkflowCompletedEvent
                    {
                        Output = result.Output,
                        CompletedAt = DateTimeOffset.UtcNow
                    },
                    cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resumed workflow execution failed: {ExecutionId}", executionId);
            
            // 失敗イベント記録
            await _eventStore.AppendEventAsync(
                workflow.GetType().Name,
                executionId,
                new WorkflowFailedEvent
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    FailedAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            throw;
        }
    }
}

/// <summary>
/// ワークフローコンテキストの実装
/// </summary>
internal class WorkflowContext : IWorkflowContext
{
    private readonly EventStore _eventStore;
    private readonly ILogger _logger;
    private readonly List<Func<Task>> _compensations = new();
    private readonly object _input;

    public string WorkflowId { get; }
    public string ExecutionId { get; }
    public Dictionary<string, object> Variables { get; } = new();

    public WorkflowContext(
        string workflowId,
        string executionId,
        object input,
        EventStore eventStore,
        ILogger logger)
    {
        WorkflowId = workflowId;
        ExecutionId = executionId;
        _input = input;
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<TResult> ExecuteActivityAsync<TResult>(
        string activityName,
        object input,
        ActivityOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 履歴チェック: 既に完了しているか？
        var outputKey = $"activity_{activityName}_output";
        if (Variables.TryGetValue(outputKey, out var cachedOutput))
        {
            _logger.LogInformation("Skipping completed activity: {ActivityName}", activityName);
            
            // JsonElementの場合の処理 (System.Text.Jsonの仕様)
            if (cachedOutput is System.Text.Json.JsonElement jsonElement)
            {
                return jsonElement.Deserialize<TResult>(Loco.Core.Serialization.LocoJsonContext.Default.Options)!;
            }
            
            return (TResult)cachedOutput;
        }

        var retryPolicy = options?.RetryPolicy ?? new RetryPolicy();
        var timeout = options?.Timeout ?? TimeSpan.FromMinutes(5);

        // アクティビティ開始イベント記録
        await _eventStore.AppendEventAsync(
            WorkflowId,
            ExecutionId,
            new ActivityStartedEvent
            {
                ActivityName = activityName,
                Input = input
            },
            cancellationToken);

        Exception? lastException = null;
        for (int attempt = 1; attempt <= retryPolicy.MaxAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "Executing activity: {ActivityName} (Attempt {Attempt}/{MaxAttempts})",
                    activityName,
                    attempt,
                    retryPolicy.MaxAttempts);

                // TODO: 実際のアクティビティ実行ロジック (IActivityExecutorなどを注入して実行)
                // ここではシミュレーションとして、入力に応じたダミー値を返す
                TResult result = default!;
                
                // シミュレーションロジック
                if (typeof(TResult) == typeof(bool))
                {
                    result = (TResult)(object)true;
                }
                else if (typeof(TResult) == typeof(string))
                {
                    result = (TResult)(object)$"Result_{Guid.NewGuid()}";
                }
                else if (typeof(TResult) == typeof(PaymentResult))
                {
                    result = (TResult)(object)new PaymentResult { Success = true, TransactionId = Guid.NewGuid().ToString() };
                }

                // アクティビティ完了イベント記録
                await _eventStore.AppendEventAsync(
                    WorkflowId,
                    ExecutionId,
                    new ActivityCompletedEvent
                    {
                        ActivityName = activityName,
                        Output = result
                    },
                    cancellationToken);
                
                // メモリ内キャッシュ更新
                Variables[outputKey] = result!;

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;

                // アクティビティ失敗イベント記録
                await _eventStore.AppendEventAsync(
                    WorkflowId,
                    ExecutionId,
                    new ActivityFailedEvent
                    {
                        ActivityName = activityName,
                        Error = ex.Message,
                        AttemptNumber = attempt
                    },
                    cancellationToken);

                if (attempt < retryPolicy.MaxAttempts)
                {
                    var delay = CalculateRetryDelay(retryPolicy, attempt);
                    _logger.LogWarning(
                        "Activity failed, retrying in {Delay}ms: {ActivityName}",
                        delay.TotalMilliseconds,
                        activityName);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Activity {activityName} failed after {retryPolicy.MaxAttempts} attempts",
            lastException);
    }

    public Task<TResult> ExecuteChildWorkflowAsync<TResult>(
        string workflowType,
        object input,
        ChildWorkflowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: 子ワークフロー実行の実装
        throw new NotImplementedException();
    }

    public async Task DelayAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var fireAt = DateTimeOffset.UtcNow.Add(duration);

        // タイマー開始イベント記録
        await _eventStore.AppendEventAsync(
            WorkflowId,
            ExecutionId,
            new TimerStartedEvent
            {
                Duration = duration,
                FireAt = fireAt
            },
            cancellationToken);

        await Task.Delay(duration, cancellationToken);

        // タイマー完了イベント記録
        await _eventStore.AppendEventAsync(
            WorkflowId,
            ExecutionId,
            new TimerFiredEvent
            {
                FiredAt = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    public Task<TEvent> WaitForEventAsync<TEvent>(
        string eventName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: 外部イベント待機の実装
        throw new NotImplementedException();
    }

    public void RegisterCompensation(Func<Task> compensationAction)
    {
        _compensations.Add(compensationAction);
    }

    public async Task ExecuteCompensationsAsync()
    {
        _logger.LogInformation(
            "Executing {Count} compensation actions for {ExecutionId}",
            _compensations.Count,
            ExecutionId);

        // 逆順で補償アクションを実行 (LIFO)
        for (int i = _compensations.Count - 1; i >= 0; i--)
        {
            try
            {
                await _compensations[i]();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compensation action failed at index {Index}", i);
            }
        }
    }

    public T GetInput<T>()
    {
        return (T)_input;
    }

    private TimeSpan CalculateRetryDelay(RetryPolicy policy, int attempt)
    {
        var delay = policy.InitialInterval.TotalMilliseconds *
                    Math.Pow(policy.BackoffCoefficient, attempt - 1);

        return TimeSpan.FromMilliseconds(
            Math.Min(delay, policy.MaxInterval.TotalMilliseconds));
    }
}
