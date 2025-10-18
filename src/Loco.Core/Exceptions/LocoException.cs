using System;

namespace Loco.Core.Exceptions
{
    /// <summary>
    /// Base exception for all Loco-specific exceptions.
    /// </summary>
    public class LocoException : Exception
    {
        public string ErrorCode { get; }
        public object? Context { get; }

        public LocoException(string message, string errorCode = "LOCO_ERROR", object? context = null)
            : base(message)
        {
            ErrorCode = errorCode;
            Context = context;
        }

        public LocoException(string message, Exception innerException, string errorCode = "LOCO_ERROR", object? context = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Context = context;
        }
    }

    /// <summary>
    /// Exception thrown when workflow execution fails.
    /// </summary>
    public class WorkflowExecutionException : LocoException
    {
        public string? WorkflowId { get; }
        public string? StepId { get; }

        public WorkflowExecutionException(string message, string? workflowId = null, string? stepId = null)
            : base(message, "WORKFLOW_EXEC_ERROR", new { WorkflowId = workflowId, StepId = stepId })
        {
            WorkflowId = workflowId;
            StepId = stepId;
        }

        public WorkflowExecutionException(string message, Exception innerException, string? workflowId = null, string? stepId = null)
            : base(message, innerException, "WORKFLOW_EXEC_ERROR", new { WorkflowId = workflowId, StepId = stepId })
        {
            WorkflowId = workflowId;
            StepId = stepId;
        }
    }

    /// <summary>
    /// Exception thrown when workflow validation fails.
    /// </summary>
    public class WorkflowValidationException : LocoException
    {
        public string[] ValidationErrors { get; }

        public WorkflowValidationException(string message, string[] validationErrors)
            : base(message, "WORKFLOW_VALIDATION_ERROR", new { Errors = validationErrors })
        {
            ValidationErrors = validationErrors;
        }
    }

    /// <summary>
    /// Exception thrown for action-specific errors.
    /// </summary>
    public class ActionException : LocoException
    {
        public string ActionType { get; }
        public string ActionId { get; }

        public ActionException(string message, string actionType, string actionId)
            : base(message, "ACTION_ERROR", new { ActionType = actionType, ActionId = actionId })
        {
            ActionType = actionType;
            ActionId = actionId;
        }

        public ActionException(string message, Exception innerException, string actionType, string actionId)
            : base(message, innerException, "ACTION_ERROR", new { ActionType = actionType, ActionId = actionId })
        {
            ActionType = actionType;
            ActionId = actionId;
        }
    }

    /// <summary>
    /// Exception thrown for engine-specific errors.
    /// </summary>
    public class EngineException : LocoException
    {
        public EngineException(string message)
            : base(message, "ENGINE_ERROR")
        {
        }

        public EngineException(string message, Exception innerException)
            : base(message, innerException, "ENGINE_ERROR")
        {
        }
    }

    /// <summary>
    /// Exception thrown for resource-related errors.
    /// </summary>
    public class ResourceException : LocoException
    {
        public string ResourceType { get; }
        public string ResourceId { get; }

        public ResourceException(string message, string resourceType, string resourceId)
            : base(message, "RESOURCE_ERROR", new { ResourceType = resourceType, ResourceId = resourceId })
        {
            ResourceType = resourceType;
            ResourceId = resourceId;
        }
    }

    /// <summary>
    /// Exception thrown when operation times out.
    /// </summary>
    public class TimeoutException : LocoException
    {
        public TimeSpan Timeout { get; }

        public TimeoutException(string message, TimeSpan timeout)
            : base(message, "TIMEOUT_ERROR", new { Timeout = timeout })
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// Exception thrown for security-related violations.
    /// </summary>
    public class SecurityException : LocoException
    {
        public SecurityException(string message)
            : base(message, "SECURITY_ERROR")
        {
        }

        public SecurityException(string message, Exception innerException)
            : base(message, innerException, "SECURITY_ERROR")
        {
        }
    }

    /// <summary>
    /// Exception thrown during execution.
    /// </summary>
    public class LocoExecutionException : LocoException
    {
        public LocoExecutionException(string message)
            : base(message, "EXECUTION_ERROR")
        {
        }

        public LocoExecutionException(string message, Exception innerException)
            : base(message, innerException, "EXECUTION_ERROR")
        {
        }

        // Legacy constructor for compatibility
        public LocoExecutionException(string message, string p1, string p2, string p3, string p4)
            : base(message, "EXECUTION_ERROR", new { P1 = p1, P2 = p2, P3 = p3, P4 = p4 })
        {
        }
    }

    /// <summary>
    /// Exception thrown for configuration errors.
    /// </summary>
    public class LocoConfigurationException : LocoException
    {
        public LocoConfigurationException(string message)
            : base(message, "CONFIG_ERROR")
        {
        }

        public LocoConfigurationException(string message, Exception innerException)
            : base(message, innerException, "CONFIG_ERROR")
        {
        }
    }
}
