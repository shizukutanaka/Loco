namespace Loco.Core.Models;

public sealed class RuleValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public static RuleValidationResult Ok() => new() { IsValid = true };
    public static RuleValidationResult Fail(params string[] errors) => new() { IsValid = false, Errors = errors };
}
