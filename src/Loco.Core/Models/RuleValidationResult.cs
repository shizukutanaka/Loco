namespace Loco.Core.Models;

public sealed class RuleValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string RuleId { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;

    public static RuleValidationResult Ok() => new() { IsValid = true };
    public static RuleValidationResult Fail(params string[] errors) => new() { IsValid = false, Errors = errors };
    public static RuleValidationResult Fail(string ruleId, string ruleName, params string[] errors) =>
        new() { IsValid = false, Errors = errors, RuleId = ruleId, RuleName = ruleName };
}
