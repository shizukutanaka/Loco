using System;
using System.Collections.Generic;

namespace Loco.Core.CQRS
{
    /// <summary>
    /// CQRS コマンド インターフェース
    /// Base interface for all commands
    ///
    /// Commands represent actions that modify state
    /// Each command should be processed idempotently
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// コマンドのトレース ID
        /// Unique identifier for tracking/auditing
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// コマンド実行時刻
        /// Timestamp of command creation
        /// </summary>
        DateTime Timestamp { get; }
    }

    /// <summary>
    /// 戻り値を返すコマンド インターフェース
    /// Command that returns a result
    /// </summary>
    /// <typeparam name="TResult">Return type</typeparam>
    public interface ICommand<out TResult> : ICommand
    {
    }

    /// <summary>
    /// コマンドハンドラー インターフェース
    /// Processes commands and updates state
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        System.Threading.Tasks.Task HandleAsync(TCommand command);
    }

    /// <summary>
    /// 戻り値付きコマンドハンドラー インターフェース
    /// Command handler that returns result
    /// </summary>
    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        System.Threading.Tasks.Task<TResult> HandleAsync(TCommand command);
    }

    // ==================== Workflow Execution Commands ====================

    /// <summary>
    /// ワークフロー実行コマンド
    /// Initiates execution of a workflow
    /// </summary>
    public class ExecuteWorkflowCommand : ICommand<ExecutionResult>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 実行するワークフロー ID
        /// </summary>
        public string WorkflowId { get; set; } = "";

        /// <summary>
        /// ワークフロー実行パラメーター
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// 非同期実行するか
        /// </summary>
        public bool AsyncExecution { get; set; } = true;

        /// <summary>
        /// べき等性キー
        /// </summary>
        public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// ワークフロー実行結果
    /// </summary>
    public class ExecutionResult
    {
        public string ExecutionId { get; set; } = "";

        public string WorkflowId { get; set; } = "";

        public string Status { get; set; } = "Queued";

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public int Progress { get; set; }

        public object? Result { get; set; }

        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// ワークフロー実行をキャンセルするコマンド
    /// Cancels an ongoing workflow execution
    /// </summary>
    public class CancelExecutionCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ExecutionId { get; set; } = "";

        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// ワークフロー実行を一時停止するコマンド
    /// Pauses an ongoing workflow execution
    /// </summary>
    public class PauseExecutionCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ExecutionId { get; set; } = "";
    }

    /// <summary>
    /// ワークフロー実行を再開するコマンド
    /// Resumes a paused workflow execution
    /// </summary>
    public class ResumeExecutionCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string ExecutionId { get; set; } = "";
    }

    // ==================== Rule Management Commands ====================

    /// <summary>
    /// ルール作成コマンド
    /// Creates a new rule
    /// </summary>
    public class CreateRuleCommand : ICommand<string>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// ルール更新コマンド
    /// Updates an existing rule
    /// </summary>
    public class UpdateRuleCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string RuleId { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    /// <summary>
    /// ルール削除コマンド
    /// Deletes a rule
    /// </summary>
    public class DeleteRuleCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string RuleId { get; set; } = "";
    }

    /// <summary>
    /// ルール有効/無効切り替えコマンド
    /// Enables or disables a rule
    /// </summary>
    public class ToggleRuleCommand : ICommand
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string RuleId { get; set; } = "";

        public bool Enabled { get; set; }
    }

    // ==================== Event Sourcing Aggregate ====================

    /// <summary>
    /// ワークフロー実行アグリゲート
    /// Aggregate root for workflow executions
    /// Maintains consistency of execution state
    /// </summary>
    public class WorkflowExecutionAggregate
    {
        public string ExecutionId { get; private set; } = "";

        public string WorkflowId { get; private set; } = "";

        public string Status { get; private set; } = "Queued";

        public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; private set; }

        public int Progress { get; private set; }

        public List<DomainEvent> UncommittedEvents { get; private set; } = new();

        private int _version = 0;

        public int Version => _version;

        /// <summary>
        /// ワークフロー実行を開始
        /// </summary>
        public static WorkflowExecutionAggregate StartExecution(
            string executionId,
            string workflowId,
            Dictionary<string, object> parameters)
        {
            var aggregate = new WorkflowExecutionAggregate
            {
                ExecutionId = executionId,
                WorkflowId = workflowId,
                Status = "Queued"
            };

            aggregate.RaiseEvent(new WorkflowStartedEvent
            {
                ExecutionId = executionId,
                WorkflowId = workflowId,
                Parameters = parameters
            });

            return aggregate;
        }

        /// <summary>
        /// 実行を進行中にマーク
        /// </summary>
        public void MarkAsRunning()
        {
            if (Status != "Queued")
                throw new InvalidOperationException($"Cannot run from {Status}");

            RaiseEvent(new WorkflowRunningEvent { ExecutionId = ExecutionId });
            Status = "Running";
        }

        /// <summary>
        /// 実行を完了
        /// </summary>
        public void Complete(object? result = null)
        {
            if (Status != "Running" && Status != "Paused")
                throw new InvalidOperationException($"Cannot complete from {Status}");

            RaiseEvent(new WorkflowCompletedEvent
            {
                ExecutionId = ExecutionId,
                Result = result
            });
            Status = "Completed";
            CompletedAt = DateTime.UtcNow;
            Progress = 100;
        }

        /// <summary>
        /// 実行を失敗
        /// </summary>
        public void Fail(string errorMessage)
        {
            if (Status == "Completed" || Status == "Failed")
                throw new InvalidOperationException($"Cannot fail from {Status}");

            RaiseEvent(new WorkflowFailedEvent
            {
                ExecutionId = ExecutionId,
                ErrorMessage = errorMessage
            });
            Status = "Failed";
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 進捗を更新
        /// </summary>
        public void UpdateProgress(int progress)
        {
            if (progress < Progress)
                throw new InvalidOperationException("Progress cannot decrease");

            if (progress > 100)
                throw new InvalidOperationException("Progress cannot exceed 100");

            RaiseEvent(new ProgressUpdatedEvent
            {
                ExecutionId = ExecutionId,
                Progress = progress
            });
            Progress = progress;
        }

        private void RaiseEvent(DomainEvent @event)
        {
            @event.AggregateId = ExecutionId;
            @event.Version = ++_version;
            @event.Timestamp = DateTime.UtcNow;
            UncommittedEvents.Add(@event);
        }
    }

    // ==================== Domain Events ====================

    /// <summary>
    /// ドメイン イベント ベースクラス
    /// Base class for all domain events
    /// </summary>
    public abstract class DomainEvent
    {
        public string AggregateId { get; set; } = "";

        public int Version { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string EventType => GetType().Name;
    }

    public class WorkflowStartedEvent : DomainEvent
    {
        public string ExecutionId { get; set; } = "";

        public string WorkflowId { get; set; } = "";

        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class WorkflowRunningEvent : DomainEvent
    {
        public string ExecutionId { get; set; } = "";
    }

    public class WorkflowCompletedEvent : DomainEvent
    {
        public string ExecutionId { get; set; } = "";

        public object? Result { get; set; }
    }

    public class WorkflowFailedEvent : DomainEvent
    {
        public string ExecutionId { get; set; } = "";

        public string ErrorMessage { get; set; } = "";
    }

    public class ProgressUpdatedEvent : DomainEvent
    {
        public string ExecutionId { get; set; } = "";

        public int Progress { get; set; }
    }
}
