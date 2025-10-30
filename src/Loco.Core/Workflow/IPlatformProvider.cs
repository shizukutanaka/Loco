using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loco.Core.Workflow
{
    /// <summary>
    /// Platform-specific provider interface for workflow execution.
    /// プラットフォーム固有のワークフロー実行プロバイダーインターフェース
    /// </summary>
    public interface IPlatformProvider
    {
        /// <summary>
        /// Gets the platform identifier (android, ios, windows, mac, linux).
        /// </summary>
        string Platform { get; }

        /// <summary>
        /// Checks if a specific trigger type is supported on this platform.
        /// </summary>
        bool IsTriggerSupported(string triggerType);

        /// <summary>
        /// Checks if a specific action type is supported on this platform.
        /// </summary>
        bool IsActionSupported(string actionType);

        /// <summary>
        /// Registers a trigger to monitor for workflow activation.
        /// </summary>
        Task<ITriggerHandle> RegisterTriggerAsync(
            WorkflowTrigger trigger,
            Func<TriggerContext, Task> callback,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates a constraint on this platform.
        /// </summary>
        Task<bool> EvaluateConstraintAsync(
            WorkflowConstraint constraint,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an action on this platform.
        /// </summary>
        Task<ActionResult> ExecuteActionAsync(
            WorkflowAction action,
            ActionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets platform-specific capabilities and metadata.
        /// </summary>
        PlatformInfo GetPlatformInfo();
    }

    /// <summary>
    /// Handle for a registered trigger that can be disposed to unregister.
    /// </summary>
    public interface ITriggerHandle : IDisposable
    {
        string TriggerId { get; }
        bool IsActive { get; }
        Task StopAsync();
    }

    /// <summary>
    /// Context passed to trigger callbacks.
    /// </summary>
    public class TriggerContext
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string TriggerId { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Context passed to action execution.
    /// </summary>
    public class ActionContext
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public Dictionary<string, object> Variables { get; set; } = new();
        public TriggerContext? TriggerContext { get; set; }
    }

    /// <summary>
    /// Result of action execution.
    /// </summary>
    public class ActionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public Exception? Error { get; set; }
        public TimeSpan Duration { get; set; }

        public static ActionResult Succeeded(string? message = null, Dictionary<string, object>? data = null)
        {
            return new ActionResult
            {
                Success = true,
                Message = message,
                OutputData = data
            };
        }

        public static ActionResult Failed(string message, Exception? error = null)
        {
            return new ActionResult
            {
                Success = false,
                Message = message,
                Error = error
            };
        }
    }

    /// <summary>
    /// Platform information and capabilities.
    /// </summary>
    public class PlatformInfo
    {
        public string Platform { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public Dictionary<string, bool> Capabilities { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<string> Limitations { get; set; } = new();
    }
}
