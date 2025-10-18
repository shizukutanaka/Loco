using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Resolves variables in workflow strings (environment variables, workflow variables, dates/times).
    /// </summary>
    public class VariableResolver
    {
        private readonly Dictionary<string, string> _variables;
        private Dictionary<string, object?>? _contextVariables;
        private static readonly Regex VariablePattern = new Regex(@"\$\{([^}]+)\}", RegexOptions.Compiled);

        public VariableResolver(Dictionary<string, string>? variables = null)
        {
            _variables = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Set context variables (runtime variables from step execution).
        /// </summary>
        public void SetContextVariables(Dictionary<string, object?>? contextVariables)
        {
            _contextVariables = contextVariables;
        }

        /// <summary>
        /// Set a workflow variable.
        /// </summary>
        public void SetVariable(string name, string value)
        {
            _variables[name] = value;
        }

        /// <summary>
        /// Get a workflow variable.
        /// </summary>
        public string? GetVariable(string name)
        {
            return _variables.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Resolves all variables in the input string.
        /// Supports: ${env:NAME}, ${var:NAME}, ${date:FORMAT}, ${time:FORMAT}
        /// </summary>
        public string Resolve(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return VariablePattern.Replace(input, match =>
            {
                var variable = match.Groups[1].Value;
                return ResolveVariable(variable) ?? match.Value;
            });
        }

        private string? ResolveVariable(string variable)
        {
            // Context variable (from step execution): ${ctx:variable_name}
            if (variable.StartsWith("ctx:", StringComparison.OrdinalIgnoreCase))
            {
                var varName = variable.Substring(4);
                if (_contextVariables != null && _contextVariables.TryGetValue(varName, out var value))
                {
                    return value?.ToString();
                }
                return null;
            }

            // Environment variable: ${env:VAR_NAME}
            if (variable.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            {
                var envVar = variable.Substring(4);
                return Environment.GetEnvironmentVariable(envVar);
            }

            // Workflow variable: ${var:variable_name}
            if (variable.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
            {
                var varName = variable.Substring(4);
                return GetVariable(varName);
            }

            // Date format: ${date:yyyy-MM-dd}
            if (variable.StartsWith("date:", StringComparison.OrdinalIgnoreCase))
            {
                var format = variable.Substring(5);
                try
                {
                    return DateTime.Now.ToString(format);
                }
                catch
                {
                    return DateTime.Now.ToString("yyyy-MM-dd");
                }
            }

            // Time format: ${time:HH:mm:ss}
            if (variable.StartsWith("time:", StringComparison.OrdinalIgnoreCase))
            {
                var format = variable.Substring(5);
                try
                {
                    return DateTime.Now.ToString(format);
                }
                catch
                {
                    return DateTime.Now.ToString("HH:mm:ss");
                }
            }

            // DateTime format: ${datetime:yyyy-MM-dd HH:mm:ss}
            if (variable.StartsWith("datetime:", StringComparison.OrdinalIgnoreCase))
            {
                var format = variable.Substring(9);
                try
                {
                    return DateTime.Now.ToString(format);
                }
                catch
                {
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }

            // Built-in variables
            switch (variable.ToLowerInvariant())
            {
                case "date":
                    return DateTime.Now.ToString("yyyy-MM-dd");
                case "time":
                    return DateTime.Now.ToString("HH:mm:ss");
                case "datetime":
                    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                case "timestamp":
                    return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                case "user":
                    return Environment.UserName;
                case "machine":
                    return Environment.MachineName;
                case "workdir":
                    return Environment.CurrentDirectory;
                default:
                    // Try as workflow variable without prefix
                    return GetVariable(variable);
            }
        }

        /// <summary>
        /// Resolves variables in all string properties of a WorkflowStep.
        /// </summary>
        public void ResolveStepVariables(WorkflowStep step)
        {
            if (!string.IsNullOrEmpty(step.Message))
                step.Message = Resolve(step.Message);

            if (!string.IsNullOrEmpty(step.Path))
                step.Path = Resolve(step.Path);

            if (!string.IsNullOrEmpty(step.Source))
                step.Source = Resolve(step.Source);

            if (!string.IsNullOrEmpty(step.Destination))
                step.Destination = Resolve(step.Destination);

            if (!string.IsNullOrEmpty(step.Command))
                step.Command = Resolve(step.Command);

            if (!string.IsNullOrEmpty(step.Url))
                step.Url = Resolve(step.Url);
        }
    }
}
