using System.Globalization;

namespace Loco.Core.Workflows;

/// <summary>
/// The condition node's comparison semantics.
///
/// Extracted from the engine's handler so there is one definition rather than a
/// switch buried in a lambda, and so the editor's simulator has something
/// specific to mirror. The shared cases both sides must satisfy live in
/// <c>tests/shared/condition-truth-table.json</c>.
///
/// Two defects prompted this:
///
///   `equals` was .NET object equality (<c>Equals(left, right)</c>), while
///   `greater_than` coerced with <c>Convert.ToDouble</c>. So a {{amount}}
///   resolving to the number 150 did not equal the "150" typed into the panel,
///   and `not_equals` on the same pair returned true - confidently wrong rather
///   than merely unhelpful.
///
///   Ordering a non-number threw a raw <see cref="FormatException"/> reading
///   "The input string 'abc' was not in a correct format", which names neither
///   the node nor the operation.
///
/// The numeric rule here is deliberately stricter than either host language's
/// default parse. .NET's <c>double.TryParse</c> accepts thousands separators and
/// "Infinity"; JavaScript's <c>Number</c> accepts "" as 0 and "0x10" as 16.
/// Adopting either default would guarantee the two implementations disagree.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>Operations the engine implements. Anything else is false.</summary>
    public static readonly IReadOnlyList<string> SupportedOperations = new[]
    {
        "equals", "not_equals", "greater_than", "less_than", "contains",
    };

    /// <summary>
    /// Evaluates one comparison.
    /// </summary>
    /// <param name="nodeName">Only used to name the node in an error message.</param>
    /// <exception cref="InvalidOperationException">
    /// An ordering comparison whose operands are not both finite numbers. Ordering
    /// text silently would be a guess that looks like it works.
    /// </exception>
    public static bool Evaluate(object? left, string operation, object? right, string nodeName = "")
    {
        if (!SupportedOperations.Contains(operation))
            return false;

        switch (operation)
        {
            case "equals":
                return AreEqual(left, right);

            case "not_equals":
                return !AreEqual(left, right);

            case "contains":
                return (AsText(left) ?? "").Contains(AsText(right) ?? "", StringComparison.Ordinal);

            case "greater_than":
            case "less_than":
            {
                var leftNumber = AsNumber(left) ?? throw Unorderable(nodeName, operation, left, right);
                var rightNumber = AsNumber(right) ?? throw Unorderable(nodeName, operation, left, right);

                return operation == "greater_than"
                    ? leftNumber > rightNumber
                    : leftNumber < rightNumber;
            }

            default:
                return false;
        }
    }

    private static bool AreEqual(object? left, object? right)
    {
        var leftNumber = AsNumber(left);
        var rightNumber = AsNumber(right);

        if (leftNumber.HasValue && rightNumber.HasValue)
            return leftNumber.Value == rightNumber.Value;

        // Ordinal, not culture-aware: a comparison whose answer depends on the
        // server's locale is not a comparison the user can reason about.
        return string.Equals(AsText(left), AsText(right), StringComparison.Ordinal);
    }

    private static InvalidOperationException Unorderable(
        string nodeName, string operation, object? left, string right) =>
        Unorderable(nodeName, operation, left, (object?)right);

    private static InvalidOperationException Unorderable(
        string nodeName, string operation, object? left, object? right)
    {
        var subject = string.IsNullOrEmpty(nodeName) ? "Condition" : $"Condition '{nodeName}'";
        return new InvalidOperationException(
            $"{subject} cannot use '{operation}' on {Describe(left)} and {Describe(right)}: " +
            "both sides must be numbers. Use 'equals' or 'contains' to compare text, or " +
            "check that the reference on each side resolves to a value.");
    }

    private static string Describe(object? value) =>
        value is null ? "an unresolved value" : $"'{AsText(value)}'";

    /// <summary>
    /// The value's text form. Booleans render lowercase so this agrees with
    /// JavaScript - .NET's default "True" would not.
    /// </summary>
    private static string? AsText(object? value) => value switch
    {
        null => null,
        bool b => b ? "true" : "false",
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>
    /// The value as a finite number, or null when it is not one.
    ///
    /// The pattern is applied before parsing so that hex, thousands separators,
    /// empty strings and "Infinity" are rejected identically on both sides,
    /// regardless of what each host's own parser would accept.
    /// </summary>
    private static double? AsNumber(object? value)
    {
        switch (value)
        {
            case null:
            case bool:
                return null;
            case double d:
                return double.IsFinite(d) ? d : null;
            case float f:
                return float.IsFinite(f) ? f : null;
            case sbyte or byte or short or ushort or int or uint or long or ulong or decimal:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        var text = AsText(value)?.Trim();
        if (string.IsNullOrEmpty(text) || !IsNumericLiteral(text))
            return null;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// ^[+-]?(digits[.digits] | .digits)([eE][+-]?digits)?$ - written out rather
    /// than as a Regex so the rule is legible and allocation-free.
    /// </summary>
    private static bool IsNumericLiteral(string text)
    {
        var i = 0;

        if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;

        var integerDigits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i])) { i++; integerDigits++; }

        var fractionDigits = 0;
        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i])) { i++; fractionDigits++; }
        }

        if (integerDigits == 0 && fractionDigits == 0) return false;

        if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
        {
            i++;
            if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;

            var exponentDigits = 0;
            while (i < text.Length && char.IsAsciiDigit(text[i])) { i++; exponentDigits++; }
            if (exponentDigits == 0) return false;
        }

        return i == text.Length;
    }
}
