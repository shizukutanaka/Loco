using System;
using System.Collections.Generic;
using System.Linq;

namespace Loco.Core.Workflow
{
    /// <summary>
    /// Validates workflow definitions for correctness and platform compatibility.
    /// ワークフロー定義の正確性とプラットフォーム互換性を検証
    /// </summary>
    public class WorkflowValidator
    {
        private readonly List<string> _validPlatforms = new() { "android", "ios", "windows", "mac", "linux" };

        /// <summary>
        /// Validates a workflow definition.
        /// </summary>
        public ValidationResult Validate(WorkflowDefinition workflow)
        {
            var result = new ValidationResult();

            ValidateBasicProperties(workflow, result);
            ValidatePlatforms(workflow, result);
            ValidateTriggers(workflow, result);
            ValidateConstraints(workflow, result);
            ValidateActions(workflow, result);

            return result;
        }

        private void ValidateBasicProperties(WorkflowDefinition workflow, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(workflow.Id))
            {
                result.AddError("Workflow ID is required");
            }

            if (string.IsNullOrWhiteSpace(workflow.Name))
            {
                result.AddError("Workflow name is required");
            }

            if (string.IsNullOrWhiteSpace(workflow.Version))
            {
                result.AddError("Workflow version is required");
            }
            else if (!IsValidVersion(workflow.Version))
            {
                result.AddError($"Invalid version format: {workflow.Version}");
            }
        }

        private void ValidatePlatforms(WorkflowDefinition workflow, ValidationResult result)
        {
            if (workflow.Platforms == null || workflow.Platforms.Count == 0)
            {
                result.AddError("At least one platform must be specified");
                return;
            }

            foreach (var platform in workflow.Platforms)
            {
                if (!_validPlatforms.Contains(platform.ToLowerInvariant()))
                {
                    result.AddError($"Invalid platform: {platform}. Valid platforms are: {string.Join(", ", _validPlatforms)}");
                }
            }
        }

        private void ValidateTriggers(WorkflowDefinition workflow, ValidationResult result)
        {
            if (workflow.Triggers == null || workflow.Triggers.Count == 0)
            {
                result.AddError("At least one trigger is required");
                return;
            }

            for (int i = 0; i < workflow.Triggers.Count; i++)
            {
                var trigger = workflow.Triggers[i];

                if (string.IsNullOrWhiteSpace(trigger.Type))
                {
                    result.AddError($"Trigger[{i}]: Type is required");
                    continue;
                }

                // Check platform compatibility
                foreach (var platform in workflow.Platforms)
                {
                    if (!PlatformCapabilities.IsTriggerSupported(platform, trigger.Type))
                    {
                        result.AddWarning($"Trigger[{i}]: Type '{trigger.Type}' is not supported on platform '{platform}'");
                    }
                }

                // Validate trigger-specific parameters
                ValidateTriggerParameters(trigger, i, result);
            }
        }

        private void ValidateTriggerParameters(WorkflowTrigger trigger, int index, ValidationResult result)
        {
            switch (trigger.Type)
            {
                case "time":
                    if (!trigger.Parameters.ContainsKey("schedule") && !trigger.Parameters.ContainsKey("time"))
                    {
                        result.AddError($"Trigger[{index}]: Time trigger requires 'schedule' or 'time' parameter");
                    }
                    break;

                case "location":
                    if (!trigger.Parameters.ContainsKey("latitude") || !trigger.Parameters.ContainsKey("longitude"))
                    {
                        result.AddError($"Trigger[{index}]: Location trigger requires 'latitude' and 'longitude' parameters");
                    }
                    break;

                case "file_system":
                    if (!trigger.Parameters.ContainsKey("path"))
                    {
                        result.AddError($"Trigger[{index}]: File system trigger requires 'path' parameter");
                    }
                    break;
            }
        }

        private void ValidateConstraints(WorkflowDefinition workflow, ValidationResult result)
        {
            if (workflow.Constraints == null) return;

            for (int i = 0; i < workflow.Constraints.Count; i++)
            {
                var constraint = workflow.Constraints[i];

                if (string.IsNullOrWhiteSpace(constraint.Type))
                {
                    result.AddError($"Constraint[{i}]: Type is required");
                }

                var validOperators = new[] { "equals", "not_equals", "greater_than", "less_than", "contains", "matches" };
                if (!validOperators.Contains(constraint.Operator))
                {
                    result.AddError($"Constraint[{i}]: Invalid operator '{constraint.Operator}'. Valid operators: {string.Join(", ", validOperators)}");
                }
            }
        }

        private void ValidateActions(WorkflowDefinition workflow, ValidationResult result)
        {
            if (workflow.Actions == null || workflow.Actions.Count == 0)
            {
                result.AddError("At least one action is required");
                return;
            }

            for (int i = 0; i < workflow.Actions.Count; i++)
            {
                var action = workflow.Actions[i];

                if (string.IsNullOrWhiteSpace(action.Id))
                {
                    result.AddError($"Action[{i}]: ID is required");
                }

                if (string.IsNullOrWhiteSpace(action.Type))
                {
                    result.AddError($"Action[{i}]: Type is required");
                    continue;
                }

                // Check platform compatibility
                foreach (var platform in workflow.Platforms)
                {
                    if (!PlatformCapabilities.IsActionSupported(platform, action.Type))
                    {
                        result.AddWarning($"Action[{i}]: Type '{action.Type}' is not supported on platform '{platform}'");
                    }
                }

                // Validate error handling
                if (action.OnError != null)
                {
                    var validStrategies = new[] { "stop", "continue", "fallback" };
                    if (!validStrategies.Contains(action.OnError.Strategy))
                    {
                        result.AddError($"Action[{i}]: Invalid error strategy '{action.OnError.Strategy}'");
                    }

                    if (action.OnError.Strategy == "fallback" && action.OnError.FallbackAction == null)
                    {
                        result.AddError($"Action[{i}]: Fallback strategy requires a fallback action");
                    }
                }

                // Validate retry policy
                if (action.Retry != null)
                {
                    if (action.Retry.MaxAttempts < 1 || action.Retry.MaxAttempts > 10)
                    {
                        result.AddError($"Action[{i}]: Retry maxAttempts must be between 1 and 10");
                    }

                    if (action.Retry.DelayMs < 0)
                    {
                        result.AddError($"Action[{i}]: Retry delayMs must be non-negative");
                    }

                    var validBackoffs = new[] { "fixed", "linear", "exponential" };
                    if (!validBackoffs.Contains(action.Retry.BackoffStrategy))
                    {
                        result.AddError($"Action[{i}]: Invalid backoff strategy '{action.Retry.BackoffStrategy}'");
                    }
                }
            }
        }

        private bool IsValidVersion(string version)
        {
            // Simple semantic versioning check: X.Y or X.Y.Z
            var parts = version.Split('.');
            if (parts.Length < 2 || parts.Length > 3) return false;

            return parts.All(part => int.TryParse(part, out _));
        }
    }

    /// <summary>
    /// Result of workflow validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public override string ToString()
        {
            var lines = new List<string>();

            if (IsValid)
            {
                lines.Add("✓ Validation passed");
            }
            else
            {
                lines.Add("✗ Validation failed");
                lines.Add("");
                lines.Add("Errors:");
                foreach (var error in Errors)
                {
                    lines.Add($"  - {error}");
                }
            }

            if (Warnings.Count > 0)
            {
                lines.Add("");
                lines.Add("Warnings:");
                foreach (var warning in Warnings)
                {
                    lines.Add($"  - {warning}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
