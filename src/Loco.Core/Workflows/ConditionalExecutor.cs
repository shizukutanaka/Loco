using System;
using System.Collections.Generic;
using System.Linq;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Evaluates conditions for workflow step execution.
    /// </summary>
    public class ConditionalExecutor
    {
        /// <summary>
        /// Checks if a step should be executed based on RunIf/SkipIf conditions.
        /// </summary>
        public static bool ShouldExecute(
            string? runIf,
            string? skipIf,
            Dictionary<string, object?> context)
        {
            // If SkipIf is specified and evaluates to true, skip execution
            if (!string.IsNullOrEmpty(skipIf))
            {
                if (EvaluateCondition(skipIf, context))
                {
                    Console.WriteLine($"  ⊘ Skipping step (condition: {skipIf})");
                    return false;
                }
            }

            // If RunIf is specified, only execute if it evaluates to true
            if (!string.IsNullOrEmpty(runIf))
            {
                if (!EvaluateCondition(runIf, context))
                {
                    Console.WriteLine($"  ⊘ Skipping step (condition not met: {runIf})");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluates a condition expression against the context.
        /// </summary>
        /// <remarks>
        /// Supports various condition types:
        /// Variable existence, equality checks, inequality, greater than, less than,
        /// success checks, and exit code validation.
        /// </remarks>
        private static bool EvaluateCondition(string condition, Dictionary<string, object?> context)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;

            condition = condition.Trim();

            // Check for comparison operators
            if (condition.Contains("=="))
            {
                var parts = condition.Split(new[] { "==" }, 2, StringSplitOptions.None);
                return EvaluateEquals(parts[0].Trim(), parts[1].Trim(), context);
            }

            if (condition.Contains("!="))
            {
                var parts = condition.Split(new[] { "!=" }, 2, StringSplitOptions.None);
                return !EvaluateEquals(parts[0].Trim(), parts[1].Trim(), context);
            }

            if (condition.Contains(">="))
            {
                var parts = condition.Split(new[] { ">=" }, 2, StringSplitOptions.None);
                return EvaluateGreaterThanOrEqual(parts[0].Trim(), parts[1].Trim(), context);
            }

            if (condition.Contains("<="))
            {
                var parts = condition.Split(new[] { "<=" }, 2, StringSplitOptions.None);
                return EvaluateLessThanOrEqual(parts[0].Trim(), parts[1].Trim(), context);
            }

            if (condition.Contains(">"))
            {
                var parts = condition.Split(new[] { ">" }, 2, StringSplitOptions.None);
                return EvaluateGreaterThan(parts[0].Trim(), parts[1].Trim(), context);
            }

            if (condition.Contains("<"))
            {
                var parts = condition.Split(new[] { "<" }, 2, StringSplitOptions.None);
                return EvaluateLessThan(parts[0].Trim(), parts[1].Trim(), context);
            }

            // Simple variable existence check
            return context.ContainsKey(condition) && context[condition] != null;
        }

        private static bool EvaluateEquals(string left, string right, Dictionary<string, object?> context)
        {
            var leftValue = GetValue(left, context);
            var rightValue = GetValue(right, context);

            if (leftValue == null && rightValue == null)
                return true;

            if (leftValue == null || rightValue == null)
                return false;

            return leftValue.ToString()?.Equals(rightValue.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private static bool EvaluateGreaterThan(string left, string right, Dictionary<string, object?> context)
        {
            var leftValue = GetNumericValue(left, context);
            var rightValue = GetNumericValue(right, context);

            if (!leftValue.HasValue || !rightValue.HasValue)
                return false;

            return leftValue.Value > rightValue.Value;
        }

        private static bool EvaluateLessThan(string left, string right, Dictionary<string, object?> context)
        {
            var leftValue = GetNumericValue(left, context);
            var rightValue = GetNumericValue(right, context);

            if (!leftValue.HasValue || !rightValue.HasValue)
                return false;

            return leftValue.Value < rightValue.Value;
        }

        private static bool EvaluateGreaterThanOrEqual(string left, string right, Dictionary<string, object?> context)
        {
            var leftValue = GetNumericValue(left, context);
            var rightValue = GetNumericValue(right, context);

            if (!leftValue.HasValue || !rightValue.HasValue)
                return false;

            return leftValue.Value >= rightValue.Value;
        }

        private static bool EvaluateLessThanOrEqual(string left, string right, Dictionary<string, object?> context)
        {
            var leftValue = GetNumericValue(left, context);
            var rightValue = GetNumericValue(right, context);

            if (!leftValue.HasValue || !rightValue.HasValue)
                return false;

            return leftValue.Value <= rightValue.Value;
        }

        private static object? GetValue(string key, Dictionary<string, object?> context)
        {
            // Check if it's a variable reference
            if (context.ContainsKey(key))
                return context[key];

            // Otherwise treat as literal value
            return key;
        }

        private static double? GetNumericValue(string key, Dictionary<string, object?> context)
        {
            var value = GetValue(key, context);

            if (value == null)
                return null;

            if (double.TryParse(value.ToString(), out var numericValue))
                return numericValue;

            return null;
        }
    }
}
