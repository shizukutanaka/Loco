using FluentAssertions;
using Loco.Api.Execution;
using Loco.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loco.Api.Tests;

/// <summary>
/// Tests for JsonFileExecutionStore - execution history that survives a restart.
///
/// Before this store existed, a client polling GET /executions/{id} across a
/// deploy got a 404 for a run that had actually succeeded. The properties worth
/// pinning are that a finished run round-trips with its outcome intact, and that
/// a store failure degrades to "no history" rather than taking a request down.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class JsonFileExecutionStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonFileExecutionStore _store;

    public JsonFileExecutionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"loco-exec-{Guid.NewGuid():N}");
        _store = new JsonFileExecutionStore(_dir, NullLogger<JsonFileExecutionStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static PersistedExecution Finished(
        string id = "exec1",
        WorkflowExecutionStatus status = WorkflowExecutionStatus.Success)
    {
        var context = new WorkflowExecutionContext
        {
            ExecutionId = id,
            WorkflowId = "wf1",
            Status = status,
            EndTime = DateTime.UtcNow,
            ExecutionLog = { "started", "finished" },
        };

        context.NodeResults["n1"] = new NodeExecutionResult
        {
            NodeId = "n1",
            NodeName = "Send message",
            Success = status == WorkflowExecutionStatus.Success,
            Data = "ok",
        };

        return new PersistedExecution(id, "wf1", DateTime.UtcNow, context);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsTheOutcome()
    {
        await _store.SaveAsync(Finished());

        var loaded = await _store.GetAsync("exec1");

        loaded.Should().NotBeNull();
        loaded!.WorkflowId.Should().Be("wf1");
        loaded.Context.Status.Should().Be(WorkflowExecutionStatus.Success);
        loaded.Context.ExecutionLog.Should().Contain("finished");
        loaded.Context.NodeResults.Should().ContainKey("n1");
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        (await _store.GetAsync("never-ran")).Should().BeNull();
    }

    [Fact]
    public async Task FailedExecution_KeepsItsError()
    {
        var failed = Finished("exec-fail", WorkflowExecutionStatus.Failed);
        failed.Context.Error = "connector refused";

        await _store.SaveAsync(failed);

        var loaded = await _store.GetAsync("exec-fail");
        loaded!.Context.Status.Should().Be(WorkflowExecutionStatus.Failed);
        loaded.Context.Error.Should().Be("connector refused");
    }

    [Fact]
    public async Task History_SurvivesANewInstance()
    {
        await _store.SaveAsync(Finished());

        var reopened = new JsonFileExecutionStore(_dir, NullLogger<JsonFileExecutionStore>.Instance);

        // This is the whole point: a different process/instance can still answer.
        (await reopened.GetAsync("exec1"))!.Context.Status
            .Should().Be(WorkflowExecutionStatus.Success);
    }

    [Fact]
    public async Task SaveAsync_LeavesNoTempFileBehind()
    {
        await _store.SaveAsync(Finished());

        Directory.GetFiles(_dir, "*.tmp", SearchOption.AllDirectories)
            .Should().BeEmpty("writes go to .tmp then move, so none should remain");
    }

    [Fact]
    public async Task CorruptRecord_ReadsAsNoHistoryRatherThanThrowing()
    {
        await _store.SaveAsync(Finished());
        var file = Directory.GetFiles(_dir, "exec1.json", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(file, "{not json");

        // A damaged record must not fault the request; the caller sees what it
        // would have seen before this store existed.
        (await _store.GetAsync("exec1")).Should().BeNull();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with/slash")]
    [InlineData("")]
    public void IsValidId_RejectsIdsThatCouldEscapeTheDirectory(string id)
    {
        JsonFileExecutionStore.IsValidId(id).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_InvalidId_IsIgnoredRatherThanWritingOutsideTheDirectory()
    {
        await _store.SaveAsync(Finished("../escape"));

        Directory.GetFiles(_dir, "*.json", SearchOption.AllDirectories).Should().BeEmpty();
    }
}
