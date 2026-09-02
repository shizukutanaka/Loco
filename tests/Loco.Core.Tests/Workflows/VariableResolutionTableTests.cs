using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Loco.Core.Workflows;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Workflows;

/// <summary>
/// Runs tests/shared/variable-resolution-table.json against
/// WorkflowVariableResolver. The editor's variableResolution.test.ts runs the
/// same file against its own implementation.
///
/// The companion of ConditionTruthTableTests, and it exists for the same
/// reason. Probing both implementations on identical input found the simulator
/// missing the `.data.` form the engine supports and resolving names in the
/// opposite order - and found a defect both shared, in which a string that
/// both begins and ends with a reference resolved to null.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class VariableResolutionTableTests
{
    public record Case(string Expression, JsonElement Expect, string Why)
    {
        public override string ToString() =>
            Expression.Length == 0 ? "(empty string)" : Expression;
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Loco.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the tests must be able to find the repository root");
        return dir!.FullName;
    }

    private static JsonElement Table()
    {
        var path = Path.Combine(RepositoryRoot(), "tests", "shared", "variable-resolution-table.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    /// <summary>
    /// The shared context: workflow variables, plus one node result per entry
    /// whose Data is the JSON object given. JsonElement is what a node result
    /// actually holds when a workflow round-trips through storage, so this is
    /// the realistic shape rather than a convenient one.
    /// </summary>
    private static WorkflowExecutionContext Context()
    {
        var context = Table().GetProperty("context");

        var variables = new Dictionary<string, object>();
        foreach (var variable in context.GetProperty("variables").EnumerateObject())
        {
            var value = ToClrValue(variable.Value);
            if (value is not null) variables[variable.Name] = value;
        }

        var results = new Dictionary<string, NodeExecutionResult>();
        foreach (var node in context.GetProperty("nodeResults").EnumerateObject())
        {
            results[node.Name] = new NodeExecutionResult
            {
                NodeId = node.Name,
                Success = true,
                Data = node.Value.Clone(),
            };
        }

        return new WorkflowExecutionContext { Variables = variables, NodeResults = results };
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        _ => element.Clone(),
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var element in Table().GetProperty("cases").EnumerateArray())
        {
            yield return new object[]
            {
                new Case(
                    element.GetProperty("expression").GetString()!,
                    element.GetProperty("expect").Clone(),
                    element.GetProperty("why").GetString()!),
            };
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesTheSharedResolutionTable(Case testCase)
    {
        var actual = WorkflowVariableResolver.Resolve(testCase.Expression, Context());

        switch (testCase.Expect.ValueKind)
        {
            case JsonValueKind.Null:
                actual.Should().BeNull(testCase.Why);
                break;

            case JsonValueKind.Number:
                // Compared numerically rather than by identity so neither side
                // is pinned to a particular numeric representation.
                actual.Should().NotBeNull(testCase.Why);
                Convert.ToDouble(actual, CultureInfo.InvariantCulture)
                    .Should().Be(testCase.Expect.GetDouble(), testCase.Why);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                actual.Should().Be(testCase.Expect.GetBoolean(), testCase.Why);
                break;

            default:
                actual.Should().Be(testCase.Expect.GetString(), testCase.Why);
                break;
        }
    }

    [Fact]
    public void TheTableIsActuallyLoaded()
    {
        // A table that failed to load would make the Theory above vacuous.
        Cases().Count().Should().BeGreaterThan(15);
    }

    [Fact]
    public void TheTableCoversTheShapesThatLetTheMultiReferenceBugSurvive()
    {
        // The bug needed a template that BOTH begins and ends with a reference.
        // A table holding only one of the two shapes would pass against the
        // broken implementation, so both must be present.
        var expressions = Cases().Select(c => ((Case)c[0]).Expression).ToList();

        expressions.Any(e =>
                e.StartsWith("{{", StringComparison.Ordinal)
                && e.EndsWith("}}", StringComparison.Ordinal)
                && e.Split("{{").Length > 2)
            .Should().BeTrue(
                "a case must both begin and end with a reference - the shape that resolved to null");

        expressions.Any(e => !e.StartsWith("{{", StringComparison.Ordinal) && e.Contains("{{"))
            .Should().BeTrue(
                "a case must have a reference that is not at the start - the shape that always worked");
    }
}
