using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Loco.Core.Storage;
using Loco.Core.Workflows;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Storage;

/// <summary>
/// Tests for JsonFileWorkflowStore - file-per-workflow JSON persistence.
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class JsonFileWorkflowStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly JsonFileWorkflowStore _store;

    public JsonFileWorkflowStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"loco-wf-tests-{Guid.NewGuid()}");
        _store = new JsonFileWorkflowStore(_testDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDirectory, recursive: true); } catch { /* best effort */ }
    }

    private static StoredWorkflow MakeWorkflow(string id, string name = "wf", string updatedAt = "2026-01-01T00:00:00.000Z")
        => new()
        {
            Id = id,
            Name = name,
            Nodes = new List<StoredWorkflowNode>
            {
                new()
                {
                    Id = "n1",
                    Type = "trigger",
                    Position = new StoredPosition { X = 10, Y = 20 },
                    Data = new StoredNodeData { Label = "Start" },
                },
            },
            Edges = new List<StoredWorkflowEdge>(),
            Metadata = new StoredWorkflowMetadata { Version = "1.0.0", IsPublic = false },
            CreatedAt = "2026-01-01T00:00:00.000Z",
            UpdatedAt = updatedAt,
        };

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"loco-wf-init-{Guid.NewGuid()}");
        _ = new JsonFileWorkflowStore(dir);

        Directory.Exists(dir).Should().BeTrue();

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyDirectory()
    {
        var action = () => new JsonFileWorkflowStore("");
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Upsert_ThenGet_RoundTripsAllFields()
    {
        var wf = MakeWorkflow("wf-1", "My Workflow");
        wf.Description = "desc";
        wf.Metadata.Tags = new List<string> { "a", "b" };

        await _store.UpsertAsync(wf);
        var loaded = await _store.GetAsync("wf-1");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("My Workflow");
        loaded.Description.Should().Be("desc");
        loaded.Nodes.Should().HaveCount(1);
        loaded.Nodes[0].Data.Label.Should().Be("Start");
        loaded.Metadata.Tags.Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public async Task Upsert_WritesOneFilePerWorkflow()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-a"));
        await _store.UpsertAsync(MakeWorkflow("wf-b"));

        File.Exists(Path.Combine(_testDirectory, "wf-a.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testDirectory, "wf-b.json")).Should().BeTrue();
    }

    [Fact]
    public async Task Upsert_SameId_ReplacesExisting()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-1", "old"));
        await _store.UpsertAsync(MakeWorkflow("wf-1", "new"));

        var loaded = await _store.GetAsync("wf-1");
        loaded!.Name.Should().Be("new");

        var (_, total) = await _store.GetPageAsync(1, 10);
        total.Should().Be(1);
    }

    [Fact]
    public async Task Upsert_RejectsPathUnsafeIds()
    {
        // Ids become file names; anything path-like must be rejected outright.
        var traversal = MakeWorkflow("ok");
        traversal.Id = "../evil";

        var action = () => _store.UpsertAsync(traversal);
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a.json")]
    [InlineData("")]
    public async Task Get_PathUnsafeId_ReturnsNullNotFileAccess(string id)
    {
        (await _store.GetAsync(id)).Should().BeNull();
        (await _store.ExistsAsync(id)).Should().BeFalse();
        (await _store.DeleteAsync(id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ExistingWorkflow_RemovesFileAndReturnsTrue()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-1"));

        var removed = await _store.DeleteAsync("wf-1");

        removed.Should().BeTrue();
        File.Exists(Path.Combine(_testDirectory, "wf-1.json")).Should().BeFalse();
        (await _store.GetAsync("wf-1")).Should().BeNull();
    }

    [Fact]
    public async Task Delete_MissingWorkflow_ReturnsFalseWithoutError()
    {
        (await _store.DeleteAsync("nope")).Should().BeFalse();
    }

    [Fact]
    public async Task GetPage_OrdersByUpdatedAtDescending_AndPaginates()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-old", updatedAt: "2026-01-01T00:00:00.000Z"));
        await _store.UpsertAsync(MakeWorkflow("wf-mid", updatedAt: "2026-02-01T00:00:00.000Z"));
        await _store.UpsertAsync(MakeWorkflow("wf-new", updatedAt: "2026-03-01T00:00:00.000Z"));

        var (page1, total) = await _store.GetPageAsync(page: 1, pageSize: 2);
        var (page2, _) = await _store.GetPageAsync(page: 2, pageSize: 2);

        total.Should().Be(3);
        page1.Select(w => w.Id).Should().ContainInOrder("wf-new", "wf-mid");
        page2.Select(w => w.Id).Should().ContainSingle().Which.Should().Be("wf-old");
    }

    [Fact]
    public async Task GetPage_NormalizesOutOfRangeArguments()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-1"));

        var (items, total) = await _store.GetPageAsync(page: 0, pageSize: -5);

        total.Should().Be(1);
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadFromDisk_NewStoreInstance_SeesPersistedWorkflows()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-persist", "persisted"));

        // A brand-new instance over the same directory must read the file back.
        var fresh = new JsonFileWorkflowStore(_testDirectory);
        var loaded = await fresh.GetAsync("wf-persist");

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("persisted");
    }

    [Fact]
    public async Task LoadFromDisk_CorruptFileIsSkipped_OthersStillLoad()
    {
        await _store.UpsertAsync(MakeWorkflow("wf-good"));
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "corrupt.json"), "{ not json !!");

        var fresh = new JsonFileWorkflowStore(_testDirectory);
        var (items, total) = await fresh.GetPageAsync(1, 10);

        total.Should().Be(1);
        items.Single().Id.Should().Be("wf-good");
    }

    [Fact]
    public async Task ConcurrentUpserts_AllPersist()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => _store.UpsertAsync(MakeWorkflow($"wf-{i}")));

        await Task.WhenAll(tasks);

        var (_, total) = await _store.GetPageAsync(1, 100);
        total.Should().Be(20);
    }

    [Fact]
    public async Task ExtensionData_UnknownJsonFields_SurviveRoundTrip()
    {
        // Simulate a newer frontend adding a field this server version doesn't know.
        var json = """
            {
              "id": "wf-ext",
              "name": "with-extras",
              "nodes": [],
              "edges": [],
              "metadata": { "version": "1.0.0", "isPublic": false },
              "createdAt": "2026-01-01T00:00:00.000Z",
              "updatedAt": "2026-01-01T00:00:00.000Z",
              "futureField": { "nested": [1, 2, 3] }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "wf-ext.json"), json);

        var fresh = new JsonFileWorkflowStore(_testDirectory);
        var loaded = await fresh.GetAsync("wf-ext");

        loaded.Should().NotBeNull();
        loaded!.ExtensionData.Should().NotBeNull();
        loaded.ExtensionData!.Should().ContainKey("futureField");

        // And saving it back must keep the unknown field on disk.
        await fresh.UpsertAsync(loaded);
        var roundTripped = await File.ReadAllTextAsync(Path.Combine(_testDirectory, "wf-ext.json"));
        roundTripped.Should().Contain("futureField");
    }
}
