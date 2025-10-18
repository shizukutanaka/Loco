using System;
using System.Threading.Tasks;
using Loco.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Workflows
{
    /// <summary>
    /// Action that sets a variable in the workflow context.
    /// </summary>
    public class SetVariableAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string VariableName { get; }
        public object? Value { get; }

        public SetVariableAction(string id, string name, string variableName, object? value)
        {
            Id = id;
            Name = name;
            VariableName = variableName;
            Value = value;
        }

        public Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("SetVariable: {Name} = {Value}", VariableName, Value);
                Console.WriteLine($"[VARIABLE] Set {VariableName} = {Value}");

                // Store in context variables
                context.Variables[VariableName] = Value;

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "SetVariableAction failed");
                Console.WriteLine($"  ✗ Error: {ex.Message}");
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// Action that executes conditionally based on a variable value.
    /// </summary>
    public class ConditionalAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public string VariableName { get; }
        public string Operator { get; }
        public object? CompareValue { get; }
        public IAction ThenAction { get; }
        public IAction? ElseAction { get; }

        public ConditionalAction(
            string id,
            string name,
            string variableName,
            string op,
            object? compareValue,
            IAction thenAction,
            IAction? elseAction = null)
        {
            Id = id;
            Name = name;
            VariableName = variableName;
            Operator = op;
            CompareValue = compareValue;
            ThenAction = thenAction;
            ElseAction = elseAction;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("Conditional: Checking {Variable}", VariableName);
                Console.WriteLine($"[CONDITIONAL] Evaluating {VariableName} {Operator} {CompareValue}");

                // Get variable value from context
                object? actualValue = null;
                if (context.Variables.TryGetValue(VariableName, out var value))
                {
                    actualValue = value;
                }

                bool condition = EvaluateCondition(actualValue, Operator, CompareValue);

                Console.WriteLine($"  Result: {condition}");

                if (condition)
                {
                    Console.WriteLine($"  Executing THEN branch");
                    return await ThenAction.ExecuteAsync(context);
                }
                else if (ElseAction != null)
                {
                    Console.WriteLine($"  Executing ELSE branch");
                    return await ElseAction.ExecuteAsync(context);
                }

                return true;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "ConditionalAction failed");
                Console.WriteLine($"  ✗ Error: {ex.Message}");
                return false;
            }
        }

        private bool EvaluateCondition(object? actual, string op, object? expected)
        {
            if (actual == null && expected == null)
                return op.Equals("equals", StringComparison.OrdinalIgnoreCase);

            if (actual == null || expected == null)
                return op.Equals("notequals", StringComparison.OrdinalIgnoreCase);

            switch (op.ToLowerInvariant())
            {
                case "equals":
                case "eq":
                    return actual.Equals(expected) || actual.ToString() == expected.ToString();

                case "notequals":
                case "neq":
                    return !actual.Equals(expected) && actual.ToString() != expected.ToString();

                case "contains":
                    return actual.ToString()?.Contains(expected.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;

                case "greaterthan":
                case "gt":
                    if (double.TryParse(actual.ToString(), out var actualNum) &&
                        double.TryParse(expected.ToString(), out var expectedNum))
                    {
                        return actualNum > expectedNum;
                    }
                    return string.Compare(actual.ToString(), expected.ToString(), StringComparison.Ordinal) > 0;

                case "lessthan":
                case "lt":
                    if (double.TryParse(actual.ToString(), out var actualNum2) &&
                        double.TryParse(expected.ToString(), out var expectedNum2))
                    {
                        return actualNum2 < expectedNum2;
                    }
                    return string.Compare(actual.ToString(), expected.ToString(), StringComparison.Ordinal) < 0;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Action that loops through a range.
    /// </summary>
    public class LoopAction : IAction
    {
        public string Id { get; }
        public string Name { get; }
        public int Count { get; }
        public IAction BodyAction { get; }
        public string? IteratorVariableName { get; }

        public LoopAction(string id, string name, int count, IAction bodyAction, string? iteratorVar = "i")
        {
            Id = id;
            Name = name;
            Count = count;
            BodyAction = bodyAction;
            IteratorVariableName = iteratorVar;
        }

        public async Task<bool> ExecuteAsync(IActionContext context)
        {
            try
            {
                context.Logger?.LogInformation("Loop: Executing {Count} iterations", Count);
                Console.WriteLine($"[LOOP] Starting {Count} iterations");

                for (int i = 0; i < Count; i++)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"  ⚠ Loop cancelled at iteration {i}");
                        return false;
                    }

                    // Set iterator variable
                    if (!string.IsNullOrEmpty(IteratorVariableName))
                    {
                        context.Variables[IteratorVariableName] = i;
                    }

                    Console.WriteLine($"  Iteration {i + 1}/{Count}");

                    var success = await BodyAction.ExecuteAsync(context);
                    if (!success)
                    {
                        Console.WriteLine($"  ✗ Loop failed at iteration {i + 1}");
                        return false;
                    }
                }

                Console.WriteLine($"  ✓ Loop completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                context.Logger?.LogError(ex, "LoopAction failed");
                Console.WriteLine($"  ✗ Error: {ex.Message}");
                return false;
            }
        }
    }
}
