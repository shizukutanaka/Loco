using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.DurableExecution;

/// <summary>
/// Durable Workflowの基底クラス
/// Temporal風の耐久性のある実行を提供
/// </summary>
public abstract class DurableWorkflow
{
    protected IWorkflowContext Context { get; private set; } = null!;

    public void Initialize(IWorkflowContext context)
    {
        Context = context;
    }

    public abstract Task<WorkflowResult> ExecuteAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// ワークフローコンテキスト
/// </summary>
public interface IWorkflowContext
{
    string WorkflowId { get; }
    string ExecutionId { get; }
    Dictionary<string, object> Variables { get; }
    
    /// <summary>
    /// アクティビティ実行 (自動リトライ付き)
    /// </summary>
    Task<TResult> ExecuteActivityAsync<TResult>(
        string activityName,
        object input,
        ActivityOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 子ワークフロー実行
    /// </summary>
    Task<TResult> ExecuteChildWorkflowAsync<TResult>(
        string workflowType,
        object input,
        ChildWorkflowOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// タイマー (永続化される)
    /// </summary>
    Task DelayAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 外部イベント待機
    /// </summary>
    Task<TEvent> WaitForEventAsync<TEvent>(
        string eventName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saga パターン用補償アクション登録
    /// </summary>
    void RegisterCompensation(Func<Task> compensationAction);

    /// <summary>
    /// 補償アクションの実行
    /// </summary>
    Task ExecuteCompensationsAsync();

    /// <summary>
    /// 入力データの取得
    /// </summary>
    T GetInput<T>();
}

/// <summary>
/// アクティビティオプション
/// </summary>
public class ActivityOptions
{
    public RetryPolicy? RetryPolicy { get; set; }
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// リトライポリシー
/// </summary>
public class RetryPolicy
{
    public int MaxAttempts { get; set; } = 3;
    public double BackoffCoefficient { get; set; } = 2.0;
    public TimeSpan InitialInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// 子ワークフローオプション
/// </summary>
public class ChildWorkflowOptions
{
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// ワークフロー結果
/// </summary>
public class WorkflowResult
{
    public bool Success { get; set; }
    public object? Output { get; set; }
    public string? ErrorMessage { get; set; }

    public static WorkflowResult Successful(object? output = null)
    {
        return new WorkflowResult
        {
            Success = true,
            Output = output
        };
    }

    public static WorkflowResult Failed(string errorMessage)
    {
        return new WorkflowResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
