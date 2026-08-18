using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Loco.Core.Storage;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Storage;

/// <summary>
/// Tests for JsonFileConnectionStore - stored connector credentials.
///
/// The behaviour worth pinning is the one the whole credential design rests on:
/// values go in and only come back out through BuildConfigurationAsync, which
/// hands them to a connector. Everything a caller can read is metadata.
///
/// NOTE: authored where dotnet test could not run (NuGet egress blocked by
/// organization policy); the first CI run is what executes these.
/// </summary>
public class JsonFileConnectionStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonFileConnectionStore _store;

    public JsonFileConnectionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"loco-connections-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE", "test-passphrase");
        _store = new JsonFileConnectionStore(_dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE", null);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static Dictionary<string, string> SlackSecrets() =>
        new() { ["botToken"] = "xoxb-secret-value" };

    [Fact]
    public async Task SaveAsync_RecordsFieldNamesButNotValues()
    {
        var saved = await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        saved.ConfiguredFields.Should().BeEquivalentTo(new[] { "botToken" });
        saved.ConnectorId.Should().Be("slack");
        saved.Name.Should().Be("Acme");

        // StoredConnection has no member that could carry a value.
        typeof(StoredConnection).GetProperties().Select(p => p.Name)
            .Should().NotContain(new[] { "Secrets", "Values", "Credentials" });
    }

    [Fact]
    public async Task ConnectionsFile_DoesNotContainTheSecret()
    {
        await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        var contents = string.Concat(
            Directory.GetFiles(_dir, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));

        contents.Should().NotContain("xoxb-secret-value");
    }

    [Fact]
    public async Task BuildConfigurationAsync_IsTheOnlyWayValuesComeBack()
    {
        await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        var config = await _store.BuildConfigurationAsync("c1");

        config.Should().NotBeNull();
        config!.GetCredentialString("botToken").Should().Be("xoxb-secret-value");
    }

    [Fact]
    public async Task BuildConfigurationAsync_UnknownConnection_ReturnsNull()
    {
        // The caller reports a missing credential rather than running the
        // connector uninitialized.
        (await _store.BuildConfigurationAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithoutSecrets_RenamesAndKeepsCredentials()
    {
        await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        var renamed = await _store.SaveAsync("c1", "slack", "Acme Renamed", secrets: null);

        renamed.Name.Should().Be("Acme Renamed");
        renamed.ConfiguredFields.Should().BeEquivalentTo(new[] { "botToken" });
        (await _store.BuildConfigurationAsync("c1"))!
            .GetCredentialString("botToken").Should().Be("xoxb-secret-value");
    }

    [Fact]
    public async Task SaveAsync_WithSecrets_ReplacesTheWholeSet()
    {
        await _store.SaveAsync("c1", "slack", "Acme",
            new Dictionary<string, string> { ["botToken"] = "a", ["signingSecret"] = "b" });

        await _store.SaveAsync("c1", "slack", "Acme",
            new Dictionary<string, string> { ["botToken"] = "c" });

        var config = await _store.BuildConfigurationAsync("c1");

        config!.GetCredentialString("botToken").Should().Be("c");
        config.GetCredentialString("signingSecret").Should().BeNull(
            "a replaced set must not leave the removed field behind");
        (await _store.GetAsync("c1"))!.ConfiguredFields.Should().BeEquivalentTo(new[] { "botToken" });
    }

    [Fact]
    public async Task SaveAsync_PreservesCreatedAtOnUpdate()
    {
        var first = await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());
        var second = await _store.SaveAsync("c1", "slack", "Acme 2", secrets: null);

        second.CreatedAt.Should().Be(first.CreatedAt);
        second.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ListAsync_FiltersByConnector()
    {
        await _store.SaveAsync("c1", "slack", "Slack one", SlackSecrets());
        await _store.SaveAsync("c2", "github", "GitHub one",
            new Dictionary<string, string> { ["token"] = "ghp" });

        (await _store.ListAsync()).Should().HaveCount(2);
        (await _store.ListAsync("slack")).Should().ContainSingle()
            .Which.Name.Should().Be("Slack one");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheConnectionAndItsSecrets()
    {
        await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        (await _store.DeleteAsync("c1")).Should().BeTrue();
        (await _store.GetAsync("c1")).Should().BeNull();
        (await _store.BuildConfigurationAsync("c1")).Should().BeNull();
        (await _store.DeleteAsync("c1")).Should().BeFalse();
    }

    [Fact]
    public async Task Connections_SurviveANewInstance()
    {
        await _store.SaveAsync("c1", "slack", "Acme", SlackSecrets());

        var reopened = new JsonFileConnectionStore(_dir);

        (await reopened.GetAsync("c1"))!.Name.Should().Be("Acme");
        (await reopened.BuildConfigurationAsync("c1"))!
            .GetCredentialString("botToken").Should().Be("xoxb-secret-value");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("with/slash")]
    [InlineData("")]
    public void IsValidId_RejectsIdsThatCouldEscapeTheirNamespace(string id)
    {
        JsonFileConnectionStore.IsValidId(id).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_InvalidId_Throws()
    {
        await _store.Invoking(s => s.SaveAsync("../escape", "slack", "x", SlackSecrets()))
            .Should().ThrowAsync<ArgumentException>();
    }
}
