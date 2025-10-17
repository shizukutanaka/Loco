using System;

namespace Loco.Core
{
    /// <summary>
    /// Centralized, user-friendly error messages
    /// </summary>
    public static class ErrorMessages
    {
        // Security errors
        public const string PathNotSafe = "Access denied: The specified path is not allowed for security reasons.";
        public const string PathTraversal = "Invalid path: Path traversal detected. Please use absolute paths only.";
        public const string CommandNotAllowed = "Security error: This command is not in the allowed list.";

        // File operation errors
        public const string FileNotFound = "File not found: {0}";
        public const string DirectoryNotFound = "Directory not found: {0}";
        public const string AccessDenied = "Access denied: You don't have permission to access {0}";
        public const string FileInUse = "File is in use: {0}. Please close any programs using this file.";

        // Network errors
        public const string NetworkTimeout = "Network timeout: The operation took too long to complete.";
        public const string ConnectionFailed = "Connection failed: Unable to connect to {0}";
        public const string InvalidUrl = "Invalid URL: {0}";

        // Configuration errors
        public const string ConfigNotFound = "Configuration file not found. Use 'loco setup' to create one.";
        public const string InvalidConfig = "Invalid configuration: {0}";
        public const string MissingParameter = "Missing required parameter: {0}";

        // Execution errors
        public const string RuleNotFound = "Rule not found: {0}";
        public const string FlowNotFound = "Flow not found: {0}";
        public const string ActionFailed = "Action failed: {0}";
        public const string TimeoutExceeded = "Timeout exceeded: Operation cancelled after {0} seconds.";

        // Helpful suggestions
        public static string WithSuggestion(string error, string suggestion)
        {
            return $"{error}\n💡 Suggestion: {suggestion}";
        }

        public static string FormatError(string template, params object[] args)
        {
            return string.Format(template, args);
        }
    }
}
