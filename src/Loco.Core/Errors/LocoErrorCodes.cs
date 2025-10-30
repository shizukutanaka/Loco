namespace Loco.Core.Errors
{
    /// <summary>
    /// Standardized error codes for Loco platform.
    /// Error codes follow pattern: LOCO[Category][Severity]###
    /// Example: LOCO_E_RULE_NOT_FOUND = Rule not found error
    /// </summary>
    public static class LocoErrorCodes
    {
        // Configuration Errors (C prefix)
        public const string CONFIG_FILE_NOT_FOUND = "LOCO_C_001";
        public const string CONFIG_INVALID_FORMAT = "LOCO_C_002";
        public const string CONFIG_INVALID_PATH = "LOCO_C_003";
        public const string CONFIG_PERMISSION_DENIED = "LOCO_C_004";
        public const string CONFIG_ENVIRONMENT_VAR_MISSING = "LOCO_C_005";

        // Rule Errors (R prefix)
        public const string RULE_NOT_FOUND = "LOCO_R_001";
        public const string RULE_INVALID_TRIGGER = "LOCO_R_002";
        public const string RULE_INVALID_ACTION = "LOCO_R_003";
        public const string RULE_EXECUTION_FAILED = "LOCO_R_004";
        public const string RULE_TIMEOUT = "LOCO_R_005";
        public const string RULE_ALREADY_EXISTS = "LOCO_R_006";
        public const string RULE_INVALID_SCHEDULE = "LOCO_R_007";

        // Flow Errors (F prefix)
        public const string FLOW_NOT_FOUND = "LOCO_F_001";
        public const string FLOW_INVALID_STEP = "LOCO_F_002";
        public const string FLOW_EXECUTION_FAILED = "LOCO_F_003";
        public const string FLOW_TIMEOUT = "LOCO_F_004";
        public const string FLOW_CIRCULAR_DEPENDENCY = "LOCO_F_005";

        // Storage Errors (S prefix)
        public const string STORAGE_NOT_AVAILABLE = "LOCO_S_001";
        public const string STORAGE_CORRUPTED = "LOCO_S_002";
        public const string STORAGE_FULL = "LOCO_S_003";
        public const string STORAGE_READ_ERROR = "LOCO_S_004";
        public const string STORAGE_WRITE_ERROR = "LOCO_S_005";

        // Security Errors (SEC prefix)
        public const string SECURITY_INVALID_PATH = "LOCO_SEC_001";
        public const string SECURITY_PATH_TRAVERSAL = "LOCO_SEC_002";
        public const string SECURITY_PERMISSION_DENIED = "LOCO_SEC_003";
        public const string SECURITY_INVALID_CREDENTIALS = "LOCO_SEC_004";

        // Resource Errors (RES prefix)
        public const string RESOURCE_INSUFFICIENT_MEMORY = "LOCO_RES_001";
        public const string RESOURCE_INSUFFICIENT_DISK = "LOCO_RES_002";
        public const string RESOURCE_LIMIT_EXCEEDED = "LOCO_RES_003";
        public const string RESOURCE_NOT_AVAILABLE = "LOCO_RES_004";

        // Engine Errors (E prefix)
        public const string ENGINE_NOT_RUNNING = "LOCO_E_001";
        public const string ENGINE_ALREADY_RUNNING = "LOCO_E_002";
        public const string ENGINE_INITIALIZATION_FAILED = "LOCO_E_003";
        public const string ENGINE_SHUTDOWN_FAILED = "LOCO_E_004";

        // General/Unknown Errors (G prefix)
        public const string GENERAL_ERROR = "LOCO_G_001";
        public const string INVALID_ARGUMENT = "LOCO_G_002";
        public const string OPERATION_CANCELLED = "LOCO_G_003";
        public const string NOT_IMPLEMENTED = "LOCO_G_004";
    }

    /// <summary>
    /// Standard error messages for Loco platform errors.
    /// </summary>
    public static class LocoErrorMessages
    {
        // Configuration Error Messages
        public static string GetConfigFileNotFoundMessage(string filePath) =>
            $"Configuration file not found: {filePath}";

        public static string GetConfigInvalidFormatMessage(string filePath, string reason) =>
            $"Configuration file has invalid format: {filePath}. Reason: {reason}";

        public static string GetConfigInvalidPathMessage(string path) =>
            $"Configuration contains invalid path: {path}";

        // Rule Error Messages
        public static string GetRuleNotFoundMessage(string ruleId) =>
            $"Rule not found: {ruleId}";

        public static string GetRuleExecutionFailedMessage(string ruleId, string reason) =>
            $"Rule execution failed: {ruleId}. Reason: {reason}";

        public static string GetRuleTimeoutMessage(string ruleId, int timeoutSeconds) =>
            $"Rule execution timed out after {timeoutSeconds}s: {ruleId}";

        public static string GetRuleAlreadyExistsMessage(string ruleId) =>
            $"Rule already exists: {ruleId}";

        // Flow Error Messages
        public static string GetFlowNotFoundMessage(string flowId) =>
            $"Flow not found: {flowId}";

        public static string GetFlowExecutionFailedMessage(string flowId, string reason) =>
            $"Flow execution failed: {flowId}. Reason: {reason}";

        public static string GetFlowTimeoutMessage(string flowId, int timeoutSeconds) =>
            $"Flow execution timed out after {timeoutSeconds}s: {flowId}";

        // Storage Error Messages
        public static string GetStorageNotAvailableMessage(string reason) =>
            $"Storage is not available: {reason}";

        public static string GetStorageCorruptedMessage(string filePath) =>
            $"Storage is corrupted: {filePath}";

        // Security Error Messages
        public static string GetSecurityInvalidPathMessage(string path) =>
            $"Invalid path (security check failed): {path}";

        public static string GetSecurityPathTraversalMessage(string path) =>
            $"Path traversal attempt detected: {path}";

        // Resource Error Messages
        public static string GetResourceInsufficientMemoryMessage(long required, long available) =>
            $"Insufficient memory: required {required} bytes, available {available} bytes";

        public static string GetResourceInsufficientDiskMessage(long required, long available) =>
            $"Insufficient disk space: required {required} bytes, available {available} bytes";

        public static string GetResourceLimitExceededMessage(string limitName, int limit) =>
            $"Resource limit exceeded: {limitName} (limit: {limit})";

        // Engine Error Messages
        public static string GetEngineNotRunningMessage() =>
            "Engine is not running";

        public static string GetEngineAlreadyRunningMessage() =>
            "Engine is already running";

        public static string GetEngineInitializationFailedMessage(string reason) =>
            $"Engine initialization failed: {reason}";

        // General Error Messages
        public static string GetGeneralErrorMessage(string reason) =>
            $"An error occurred: {reason}";

        public static string GetInvalidArgumentMessage(string parameterName, string reason) =>
            $"Invalid argument '{parameterName}': {reason}";

        public static string GetOperationCancelledMessage() =>
            "Operation was cancelled";

        public static string GetNotImplementedMessage(string feature) =>
            $"Feature not implemented: {feature}";
    }
}
