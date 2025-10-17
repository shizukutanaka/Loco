using System;
using System.Collections.Generic;

namespace Loco.Core.Exceptions
{
    /// <summary>
    /// Base exception for all Loco-specific exceptions
    /// </summary>
    public class LocoException : Exception
    {
        public string ErrorCode { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();

        public LocoException(string message, string errorCode = "LOCO_000")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public LocoException(string message, Exception innerException, string errorCode = "LOCO_000")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public void AddContext(string key, object value)
        {
            Context[key] = value;
        }
    }

    /// <summary>
    /// Validation-related exceptions
    /// </summary>
    public class ValidationException : LocoException
    {
        public List<string> ValidationErrors { get; set; } = new();

        public ValidationException(string message, string errorCode = "VAL_001")
            : base(message, errorCode)
        {
        }

        public ValidationException(string message, List<string> errors, string errorCode = "VAL_001")
            : base(message, errorCode)
        {
            ValidationErrors = errors;
        }
    }

    /// <summary>
    /// Configuration-related exceptions
    /// </summary>
    public class ConfigurationException : LocoException
    {
        public ConfigurationException(string message, string errorCode = "CFG_001")
            : base(message, errorCode)
        {
        }

        public ConfigurationException(string message, Exception innerException, string errorCode = "CFG_001")
            : base(message, innerException, errorCode)
        {
        }
    }

    /// <summary>
    /// Security-related exceptions
    /// </summary>
    public class SecurityException : LocoException
    {
        public SecurityException(string message, string errorCode = "SEC_001")
            : base(message, errorCode)
        {
        }

        public SecurityException(string message, Exception innerException, string errorCode = "SEC_001")
            : base(message, innerException, errorCode)
        {
        }
    }

    /// <summary>
    /// Resource limit exceptions
    /// </summary>
    public class ResourceLimitException : LocoException
    {
        public string ResourceType { get; set; }
        public long CurrentValue { get; set; }
        public long LimitValue { get; set; }

        public ResourceLimitException(string resourceType, long currentValue, long limitValue, string errorCode = "RES_001")
            : base($"{resourceType} limit exceeded: {currentValue} > {limitValue}", errorCode)
        {
            ResourceType = resourceType;
            CurrentValue = currentValue;
            LimitValue = limitValue;
        }
    }

    /// <summary>
    /// Workflow execution exceptions
    /// </summary>
    public class WorkflowException : LocoException
    {
        public string? WorkflowId { get; set; }
        public string? StepId { get; set; }

        public WorkflowException(string message, string? workflowId = null, string? stepId = null, string errorCode = "WF_001")
            : base(message, errorCode)
        {
            WorkflowId = workflowId;
            StepId = stepId;
        }

        public WorkflowException(string message, Exception innerException, string? workflowId = null, string? stepId = null, string errorCode = "WF_001")
            : base(message, innerException, errorCode)
        {
            WorkflowId = workflowId;
            StepId = stepId;
        }
    }

    /// <summary>
    /// Timeout exceptions
    /// </summary>
    public class TimeoutException : LocoException
    {
        public TimeSpan Timeout { get; set; }

        public TimeoutException(TimeSpan timeout, string errorCode = "TIME_001")
            : base($"Operation timed out after {timeout.TotalSeconds:F1} seconds", errorCode)
        {
            Timeout = timeout;
        }

        public TimeoutException(string message, TimeSpan timeout, string errorCode = "TIME_001")
            : base(message, errorCode)
        {
            Timeout = timeout;
        }
    }

    /// <summary>
    /// Plugin exceptions
    /// </summary>
    public class PluginException : LocoException
    {
        public string? PluginName { get; set; }

        public PluginException(string message, string? pluginName = null, string errorCode = "PLG_001")
            : base(message, errorCode)
        {
            PluginName = pluginName;
        }

        public PluginException(string message, Exception innerException, string? pluginName = null, string errorCode = "PLG_001")
            : base(message, innerException, errorCode)
        {
            PluginName = pluginName;
        }
    }

    /// <summary>
    /// Execution-related exceptions
    /// </summary>
    public class LocoExecutionException : LocoException
    {
        public string? FlowId { get; set; }
        public string? StepName { get; set; }

        public LocoExecutionException(string message, string errorCode = "EXEC_001", string? flowId = null, string? stepName = null)
            : base(message, errorCode)
        {
            FlowId = flowId;
            StepName = stepName;
        }

        public LocoExecutionException(string message, Exception innerException, string errorCode = "EXEC_001", string? flowId = null, string? stepName = null)
            : base(message, innerException, errorCode)
        {
            FlowId = flowId;
            StepName = stepName;
        }
    }

    /// <summary>
    /// Configuration-related exceptions with detailed context
    /// </summary>
    public class LocoConfigurationException : ConfigurationException
    {
        public string? ConfigKey { get; set; }

        public LocoConfigurationException(string message, string? configKey = null, string errorCode = "CFG_002")
            : base(message, errorCode)
        {
            ConfigKey = configKey;
        }

        public LocoConfigurationException(string message, Exception innerException, string? configKey = null, string errorCode = "CFG_002")
            : base(message, innerException, errorCode)
        {
            ConfigKey = configKey;
        }
    }

    /// <summary>
    /// Validation exceptions with field details
    /// </summary>
    public class LocoValidationException : ValidationException
    {
        public string? FieldName { get; set; }
        public object? InvalidValue { get; set; }

        public LocoValidationException(string message, string? fieldName = null, object? invalidValue = null, string errorCode = "VAL_002")
            : base(message, errorCode)
        {
            FieldName = fieldName;
            InvalidValue = invalidValue;
        }

        public LocoValidationException(string message, Exception innerException, string? fieldName = null, string errorCode = "VAL_002")
            : base(message, errorCode)
        {
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// Database operation exceptions
    /// </summary>
    public class LoCoDatabaseException : LocoException
    {
        public string? Database { get; set; }
        public string? Query { get; set; }

        public LoCoDatabaseException(string message, string? database = null, string? query = null, string errorCode = "DB_001")
            : base(message, errorCode)
        {
            Database = database;
            Query = query;
        }

        public LoCoDatabaseException(string message, Exception innerException, string? database = null, string? query = null, string errorCode = "DB_001")
            : base(message, innerException, errorCode)
        {
            Database = database;
            Query = query;
        }
    }
}
