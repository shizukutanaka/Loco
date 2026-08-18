using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Validates workflow definitions for correctness and best practices.
    /// </summary>
    public class MainWorkflowValidator
    {
        private readonly ILogger? _logger;

        public MainWorkflowValidator(ILogger? logger = null)
        {
            _logger = logger;
        }

        public MainWorkflowValidationResult Validate(WorkflowDefinition workflow)
        {
            var result = new MainWorkflowValidationResult();

            if (workflow == null)
            {
                result.AddError("Workflow definition is null");
                return result;
            }

            // Validate ID
            if (string.IsNullOrWhiteSpace(workflow.Id))
            {
                result.AddError("Workflow ID is required");
            }
            else if (workflow.Id.Length > 100)
            {
                result.AddWarning("Workflow ID should be less than 100 characters");
            }

            // Validate Name
            if (string.IsNullOrWhiteSpace(workflow.Name))
            {
                result.AddError("Workflow name is required");
            }

            // Validate Steps
            if (workflow.Steps == null || workflow.Steps.Count == 0)
            {
                result.AddWarning("Workflow has no steps");
            }
            else
            {
                ValidateSteps(workflow.Steps, result);
            }

            // Check for duplicate IDs
            var stepIds = workflow.Steps?.Select(s => s.Id).ToList() ?? new List<string>();
            var duplicates = stepIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var duplicate in duplicates)
            {
                result.AddError($"Duplicate step ID found: {duplicate}");
            }

            return result;
        }

        private void ValidateSteps(List<WorkflowStep> steps, MainWorkflowValidationResult result)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var stepContext = $"Step {i + 1} (ID: {step.Id})";

                // Validate step ID
                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    result.AddError($"{stepContext}: Step ID is required");
                }

                // Validate step name
                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    result.AddWarning($"{stepContext}: Step name is recommended");
                }

                // Validate step type
                if (string.IsNullOrWhiteSpace(step.Type))
                {
                    result.AddError($"{stepContext}: Step type is required");
                }
                else
                {
                    ValidateStepType(step, result, stepContext);
                }
            }
        }

        private void ValidateStepType(WorkflowStep step, MainWorkflowValidationResult result, string context)
        {
            var validTypes = new[] { "log", "delay", "file", "process", "command", "http", "api" };
            
            if (!validTypes.Contains(step.Type.ToLowerInvariant()))
            {
                result.AddWarning($"{context}: Unknown step type '{step.Type}'");
            }

            // Type-specific validation
            switch (step.Type.ToLowerInvariant())
            {
                case "log":
                    if (string.IsNullOrWhiteSpace(step.Message))
                    {
                        result.AddWarning($"{context}: Log action should have a message");
                    }
                    break;

                case "delay":
                    if (string.IsNullOrWhiteSpace(step.Duration))
                    {
                        result.AddError($"{context}: Delay action requires a duration");
                    }
                    else if (!TimeSpan.TryParse(step.Duration, out _))
                    {
                        result.AddError($"{context}: Invalid duration format: {step.Duration}");
                    }
                    break;

                case "file":
                    if (string.IsNullOrWhiteSpace(step.Path) && 
                        (string.IsNullOrWhiteSpace(step.Source) || string.IsNullOrWhiteSpace(step.Destination)))
                    {
                        result.AddError($"{context}: File action requires either 'path' or 'source' and 'destination'");
                    }
                    break;

                case "process":
                case "command":
                    if (string.IsNullOrWhiteSpace(step.Command))
                    {
                        result.AddError($"{context}: Process action requires a command");
                    }
                    // Security warning for potentially dangerous commands
                    if (!string.IsNullOrWhiteSpace(step.Command))
                    {
                        var dangerousPatterns = new[] { "rm ", "del ", "format ", "shutdown" };
                        if (dangerousPatterns.Any(p => step.Command.Contains(p, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.AddWarning($"{context}: Command contains potentially dangerous operation");
                        }
                    }
                    break;

                case "http":
                case "api":
                    if (string.IsNullOrWhiteSpace(step.Url))
                    {
                        result.AddError($"{context}: HTTP action requires a URL");
                    }
                    else if (!step.Url.Contains("${") && !Uri.TryCreate(step.Url, UriKind.Absolute, out _))
                    {
                        // Only validate URL format if it doesn't contain variables
                        result.AddError($"{context}: Invalid URL format: {step.Url}");
                    }

                    if (!string.IsNullOrWhiteSpace(step.Method))
                    {
                        var validMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                        if (!validMethods.Contains(step.Method.ToUpperInvariant()))
                        {
                            result.AddWarning($"{context}: Unknown HTTP method: {step.Method}");
                        }
                    }
                    break;
            }

            // Validate retry configuration
            if (step.RetryCount.HasValue)
            {
                if (step.RetryCount.Value < 0)
                {
                    result.AddError($"{context}: RetryCount cannot be negative");
                }
                else if (step.RetryCount.Value > 10)
                {
                    result.AddWarning($"{context}: RetryCount is very high ({step.RetryCount.Value}), consider reducing");
                }
            }

            if (!string.IsNullOrWhiteSpace(step.RetryDelay))
            {
                if (!TimeSpan.TryParse(step.RetryDelay, out var delay))
                {
                    result.AddError($"{context}: Invalid RetryDelay format: {step.RetryDelay}");
                }
                else if (delay.TotalSeconds > 300)
                {
                    result.AddWarning($"{context}: RetryDelay is very long ({delay.TotalSeconds}s)");
                }
            }

            // Validate timeout
            if (step.TimeoutSeconds.HasValue)
            {
                if (step.TimeoutSeconds.Value <= 0)
                {
                    result.AddError($"{context}: TimeoutSeconds must be positive");
                }
                else if (step.TimeoutSeconds.Value > 3600)
                {
                    result.AddWarning($"{context}: TimeoutSeconds is very long ({step.TimeoutSeconds.Value}s)");
                }
            }

            // Validate conditional execution
            if (!string.IsNullOrWhiteSpace(step.RunIf) && !string.IsNullOrWhiteSpace(step.SkipIf))
            {
                result.AddWarning($"{context}: Both RunIf and SkipIf are specified, SkipIf takes precedence");
            }
        }
    }

    /// <summary>
    /// Result of workflow validation containing errors and warnings.
    /// </summary>
    public class MainWorkflowValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void AddError(string error)
        {
            Errors.Add(error);
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public override string ToString()
        {
            var result = new System.Text.StringBuilder();
            
            if (Errors.Count > 0)
            {
                result.AppendLine($"Errors ({Errors.Count}):");
                foreach (var error in Errors)
                {
                    result.AppendLine($"  ✗ {error}");
                }
            }

            if (Warnings.Count > 0)
            {
                result.AppendLine($"Warnings ({Warnings.Count}):");
                foreach (var warning in Warnings)
                {
                    result.AppendLine($"  ⚠ {warning}");
                }
            }

            if (IsValid && Warnings.Count == 0)
            {
                result.AppendLine("✓ Workflow validation passed");
            }

            return result.ToString();
        }
    }
}
