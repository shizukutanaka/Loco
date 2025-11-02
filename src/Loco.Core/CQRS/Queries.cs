using System;
using System.Collections.Generic;

namespace Loco.Core.CQRS
{
    /// <summary>
    /// クエリ インターフェース
    /// Base interface for all queries
    ///
    /// Queries represent read operations that don't modify state
    /// Used for retrieving data without side effects
    /// </summary>
    public interface IQuery<out TResult>
    {
        /// <summary>
        /// クエリのトレース ID
        /// Unique identifier for tracking/auditing
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        /// クエリ実行時刻
        /// Timestamp of query creation
        /// </summary>
        DateTime Timestamp { get; }
    }

    /// <summary>
    /// クエリハンドラー インターフェース
    /// Processes queries and returns results
    /// </summary>
    /// <typeparam name="TQuery">Query type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
    {
        System.Threading.Tasks.Task<TResult> HandleAsync(TQuery query);
    }

    // ==================== Workflow Queries ====================

    /// <summary>
    /// ワークフロー一覧取得クエリ
    /// Query to list all workflows
    /// </summary>
    public class ListWorkflowsQuery : IQuery<System.Collections.Generic.List<WorkflowReadModel>>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ページオフセット
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// 取得件数
        /// </summary>
        public int Take { get; set; } = 20;

        /// <summary>
        /// フィルター
        /// </summary>
        public string? Filter { get; set; }
    }

    /// <summary>
    /// ワークフロー詳細取得クエリ
    /// Query to get workflow details
    /// </summary>
    public class GetWorkflowQuery : IQuery<WorkflowReadModel?>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 取得するワークフロー ID
        /// </summary>
        public string WorkflowId { get; set; } = "";
    }

    /// <summary>
    /// ワークフロー実行履歴取得クエリ
    /// Query to get workflow execution history
    /// </summary>
    public class GetWorkflowExecutionHistoryQuery : IQuery<System.Collections.Generic.List<ExecutionHistoryReadModel>>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 対象ワークフロー ID
        /// </summary>
        public string WorkflowId { get; set; } = "";

        /// <summary>
        /// ページオフセット
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// 取得件数
        /// </summary>
        public int Take { get; set; } = 50;

        /// <summary>
        /// ステータスフィルター (null=全て)
        /// </summary>
        public string? StatusFilter { get; set; }
    }

    // ==================== Rule Queries ====================

    /// <summary>
    /// ルール一覧取得クエリ
    /// Query to list all rules
    /// </summary>
    public class ListRulesQuery : IQuery<System.Collections.Generic.List<RuleReadModel>>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ページオフセット
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// 取得件数
        /// </summary>
        public int Take { get; set; } = 20;

        /// <summary>
        /// 有効なルールのみ取得するか
        /// </summary>
        public bool? EnabledOnly { get; set; }
    }

    /// <summary>
    /// ルール詳細取得クエリ
    /// Query to get rule details
    /// </summary>
    public class GetRuleQuery : IQuery<RuleReadModel?>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 取得するルール ID
        /// </summary>
        public string RuleId { get; set; } = "";
    }

    // ==================== Execution Queries ====================

    /// <summary>
    /// 実行ステータス取得クエリ
    /// Query to get execution status
    /// </summary>
    public class GetExecutionStatusQuery : IQuery<ExecutionStatusReadModel?>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 対象実行 ID
        /// </summary>
        public string ExecutionId { get; set; } = "";

        /// <summary>
        /// 対象ワークフロー ID
        /// </summary>
        public string WorkflowId { get; set; } = "";
    }

    /// <summary>
    /// 実行中の実行一覧取得クエリ
    /// Query to list running executions
    /// </summary>
    public class ListRunningExecutionsQuery : IQuery<System.Collections.Generic.List<ExecutionStatusReadModel>>
    {
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ページオフセット
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// 取得件数
        /// </summary>
        public int Take { get; set; } = 20;
    }

    // ==================== Read Models ====================

    /// <summary>
    /// ワークフロー読み取りモデル
    /// Optimized read model for workflow queries
    /// </summary>
    public class WorkflowReadModel
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string Status { get; set; } = "Draft";

        public int StepCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int ExecutionCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailureCount { get; set; }

        public double SuccessRate { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// ルール読み取りモデル
    /// Optimized read model for rule queries
    /// </summary>
    public class RuleReadModel
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public bool Enabled { get; set; } = true;

        public string Priority { get; set; } = "Normal";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Dictionary<string, object> Configuration { get; set; } = new();

        public int ApplicationCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailureCount { get; set; }
    }

    /// <summary>
    /// 実行履歴読み取りモデル
    /// Optimized read model for execution history
    /// </summary>
    public class ExecutionHistoryReadModel
    {
        public string ExecutionId { get; set; } = "";

        public string WorkflowId { get; set; } = "";

        public string WorkflowName { get; set; } = "";

        public string Status { get; set; } = "";

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int Progress { get; set; }

        public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;

        public string? ErrorMessage { get; set; }

        public Dictionary<string, object>? Result { get; set; }
    }

    /// <summary>
    /// 実行ステータス読み取りモデル
    /// Optimized read model for execution status
    /// </summary>
    public class ExecutionStatusReadModel
    {
        public string ExecutionId { get; set; } = "";

        public string WorkflowId { get; set; } = "";

        public string Status { get; set; } = "Queued";

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int Progress { get; set; }

        public System.Collections.Generic.List<StepExecutionReadModel> StepExecutions { get; set; } = new();

        public Dictionary<string, object>? Result { get; set; }

        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// ステップ実行読み取りモデル
    /// Optimized read model for step execution details
    /// </summary>
    public class StepExecutionReadModel
    {
        public string StepId { get; set; } = "";

        public string StepName { get; set; } = "";

        public int Order { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public TimeSpan Duration => CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : TimeSpan.Zero;

        public Dictionary<string, object>? Result { get; set; }

        public string? ErrorMessage { get; set; }

        public int RetryCount { get; set; }
    }
}
