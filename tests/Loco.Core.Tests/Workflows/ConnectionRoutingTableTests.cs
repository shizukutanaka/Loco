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
/// Runs tests/shared/connection-routing-table.json against ConnectionRouter.
/// The editor's connectionRouting.test.ts runs the same file against its own
/// implementation.
///
/// The third of the shared tables, and the starkest of the three divergences:
/// the simulator was not mirroring this at all. Its edge filter read only the
/// source handle and never the edge's condition, so an edge marked 'error' was
/// followed after the node SUCCEEDED - a user marking a cleanup branch as the
/// error path and pressing "Test Workflow" watched it run.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class ConnectionRoutingTableTests
{
    public record Case(
        string? SourceOutput,
        string? Condition,
        bool SourceSucceeded,
        bool? Verdict,
        string Expect,
        string Why)
    {
        public override string ToString() =>
            $"handle={SourceOutput ?? "null"} condition={Condition ?? "null"} " +
            $"ok={SourceSucceeded} verdict={(Verdict?.ToString() ?? "null")}";
    }

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
        var path = Path.Combine(RepositoryRoot(), "tests", "shared", "connection-routing-table.json");
        var document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var element in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            yield return new object[]
            {
                new Case(
                    Text(element, "sourceOutput"),
                    Text(element, "condition"),
                    element.GetProperty("sourceSucceeded").GetBoolean(),
                    Boolean(element, "verdict"),
                    element.GetProperty("expect").GetString()!,
                    element.GetProperty("why").GetString()!),
            };
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty(name).GetString();

    private static bool? Boolean(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty(name).GetBoolean();

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesTheSharedRoutingTable(Case testCase)
    {
        var act = () => ConnectionRouter.ShouldFollow(
            testCase.SourceOutput,
            testCase.Condition,
            testCase.SourceSucceeded,
            testCase.Verdict,
            "Check");

        if (testCase.Expect == "error")
        {
            // Two different refusals: a branch handle with no verdict is an
            // InvalidOperationException, an unevaluatable condition a
            // NotSupportedException. Both are exceptions, which is what the
            // table says; the distinction is the engine's business.
            act.Should().Throw<Exception>(testCase.Why);
            return;
        }

        act().Should().Be(testCase.Expect == "follow", testCase.Why);
    }

    [Fact]
    public void TheTableIsActuallyLoaded()
    {
        // A table that failed to load would make the Theory above vacuous.
        Cases().Count().Should().BeGreaterThan(15);
    }

    [Fact]
    public void TheTableCoversEverySupportedCondition()
    {
        var conditions = Cases().Select(c => ((Case)c[0]).Condition).Distinct().ToList();

        foreach (var condition in ConnectionRouter.SupportedConditions)
        {
            // null stands in for "default", which is why it counts here.
            var covered = conditions.Contains(condition)
                          || (condition == "default" && conditions.Contains(null));

            covered.Should().BeTrue($"no case exercises the '{condition}' condition");
        }
    }

    [Fact]
    public void TheTableCoversAllThreeOutcomes()
    {
        // An implementation that always followed, or always refused, would pass
        // a table holding only one outcome.
        var outcomes = Cases().Select(c => ((Case)c[0]).Expect).Distinct().ToList();

        outcomes.Should().Contain("follow");
        outcomes.Should().Contain("skip");
        outcomes.Should().Contain("error");
    }
}
