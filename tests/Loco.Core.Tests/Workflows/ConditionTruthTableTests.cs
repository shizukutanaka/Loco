using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Loco.Core.Workflows;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Workflows;

/// <summary>
/// Runs tests/shared/condition-truth-table.json against ConditionEvaluator.
///
/// The editor's simulator runs the same file against its own implementation.
/// Two implementations in two languages cannot share code, so the shared thing
/// is the table: a change to one side that is not a change to the other fails
/// one of the two suites.
///
/// It exists because the two DID drift. `"abc" greater_than "100"` failed the
/// node in the engine and returned false in the simulator, so "Test Workflow"
/// reported a green run for a workflow that would die. Six of seven probed
/// cases agreed, which is exactly how a divergence survives.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class ConditionTruthTableTests
{
    public record Case(object? Left, string Operation, object? Right, object Expect, string Why)
    {
        public override string ToString() =>
            $"{Render(Left)} {Operation} {Render(Right)} -> {Expect}";

        private static string Render(object? v) => v switch
        {
            null => "null",
            string s => $"\"{s}\"",
            _ => v.ToString() ?? "null",
        };
    }

    /// <summary>
    /// The repository root, found by walking up for Loco.sln rather than assuming
    /// a working directory - the offline harness and `dotnet test` do not agree
    /// on one.
    /// </summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Loco.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the tests must be able to find the repository root");
        return dir!.FullName;
    }

    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "shared", "condition-truth-table.json");
        var document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var element in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            yield return new object[]
            {
                new Case(
                    ToClrValue(element.GetProperty("left")),
                    element.GetProperty("operation").GetString()!,
                    ToClrValue(element.GetProperty("right")),
                    ToClrValue(element.GetProperty("expect"))!,
                    element.GetProperty("why").GetString()!),
            };
        }
    }

    /// <summary>
    /// JSON to the CLR values a resolved parameter actually holds. A whole
    /// number becomes long and a fractional one double, which is what
    /// System.Text.Json produces when a workflow is loaded from disk.
    /// </summary>
    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        _ => throw new InvalidOperationException($"unsupported case value: {element.ValueKind}"),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesTheSharedTruthTable(Case testCase)
    {
        if (testCase.Expect is string expectation)
        {
            expectation.Should().Be("error", "the only non-boolean expectation is \"error\"");

            var act = () => ConditionEvaluator.Evaluate(
                testCase.Left, testCase.Operation, testCase.Right, "Check");

            var thrown = act.Should().Throw<InvalidOperationException>(testCase.Why).Which;

            // Naming the node and the operation is the point of the change: the
            // old failure said "The input string 'abc' was not in a correct
            // format" and named neither. Asserted on the message directly
            // rather than through WithMessage, whose real implementation is a
            // wildcard match rather than the substring the offline harness
            // treats it as.
            thrown.Message.Should().Contain("Check");
            thrown.Message.Should().Contain(testCase.Operation);
            return;
        }

        ConditionEvaluator
            .Evaluate(testCase.Left, testCase.Operation, testCase.Right, "Check")
            .Should().Be((bool)testCase.Expect, testCase.Why);
    }

    [Fact]
    public void TheTableIsActuallyLoaded()
    {
        // A table that failed to load would make every Theory above vacuous -
        // xunit reports zero cases as a pass, not a failure.
        Cases().Count().Should().BeGreaterThan(25);
    }

    [Fact]
    public void TheTableCoversEverySupportedOperation()
    {
        var covered = Cases()
            .Select(c => ((Case)c[0]).Operation)
            .Distinct()
            .ToList();

        foreach (var operation in ConditionEvaluator.SupportedOperations)
        {
            covered.Should().Contain(operation,
                "an operation with no case in the table is one the two " +
                "implementations can disagree about freely");
        }
    }

    [Fact]
    public void TheTableCoversBothOutcomesOfEveryOrderingOperation()
    {
        // Guards against a table that only ever expects one answer, which would
        // pass against an implementation that always returned it.
        foreach (var operation in new[] { "equals", "not_equals", "greater_than", "less_than", "contains" })
        {
            var expectations = Cases()
                .Select(c => (Case)c[0])
                .Where(c => c.Operation == operation)
                .Select(c => c.Expect)
                .ToList();

            expectations.Should().Contain(true, $"'{operation}' needs a case that holds");
            expectations.Should().Contain(false, $"'{operation}' needs a case that does not");
        }
    }
}
