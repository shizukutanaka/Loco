using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Loco.Api.Tests;

/// <summary>
/// Integration tests for the connections (credentials) API, run against the real
/// pipeline via <see cref="LocoApiFactory"/>.
///
/// The property these exist to defend cannot be checked by unit-testing the
/// store: a secret must never appear in an HTTP RESPONSE. A store that keeps
/// values out of its own return types still leaks if a controller serializes
/// something else, so this asserts against the raw response body.
///
/// NOTE: these run. scripts/run-tests-offline.sh builds the API as a real
/// executable and the harness launches it on a loopback port, so these talk to
/// actual Kestrel over actual HTTP - the response bodies asserted against here
/// genuinely crossed a socket. What is hand-written is the JwtBearer plumbing
/// that pulls the token off the request; the validation itself is Microsoft's
/// own library, shipped inside the SDK. docs/ci/ci.yml runs the real packages.
/// </summary>
public class ConnectionsApiTests : IClassFixture<LocoApiFactory>
{
    private const string SecretValue = "xoxb-super-secret-value";

    private readonly LocoApiFactory _factory;

    public ConnectionsApiTests(LocoApiFactory factory)
    {
        _factory = factory;
    }

    private static object SlackConnectionBody(string name) => new
    {
        connectorId = "slack",
        name,
        secrets = new Dictionary<string, string> { ["botToken"] = SecretValue },
    };

    [Fact]
    public async Task Create_ReturnsMetadataAndNeverTheSecret()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/connections", SlackConnectionBody("Acme workspace"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(SecretValue,
            "a response body is the easiest place for a secret to reach logs and error reporters");

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        var data = body.GetProperty("data");
        data.GetProperty("connectorId").GetString().Should().Be("slack");
        // The field NAME is reported so the UI can show completeness; the value is not.
        data.GetProperty("configuredFields").EnumerateArray()
            .Select(e => e.GetString()).Should().BeEquivalentTo(new[] { "botToken" });
    }

    [Fact]
    public async Task Get_AndList_NeverContainTheSecret()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/v1/connections", SlackConnectionBody("Readback check"));
        var id = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetString()!;

        var single = await client.GetStringAsync($"/api/v1/connections/{id}");
        single.Should().NotContain(SecretValue);

        var list = await client.GetStringAsync("/api/v1/connections?connectorId=slack");
        list.Should().NotContain(SecretValue);
        list.Should().Contain("Readback check", "the connection itself must still be listed");
    }

    [Fact]
    public async Task Create_UnknownConnector_IsRejected()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/connections", new
        {
            connectorId = "not-a-real-connector",
            name = "Nope",
            secrets = new Dictionary<string, string> { ["k"] = "v" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("UNKNOWN_CONNECTOR");
    }

    [Fact]
    public async Task Create_WithoutName_IsRejected()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/connections", new
        {
            connectorId = "slack",
            name = "",
            secrets = new Dictionary<string, string> { ["botToken"] = "v" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_WithoutSecrets_RenamesAndKeepsCredentials()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/v1/connections", SlackConnectionBody("Before rename"));
        var id = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetString()!;

        var updated = await client.PutAsJsonAsync(
            $"/api/v1/connections/{id}", new { name = "After rename" });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonSerializer.Deserialize<JsonElement>(await updated.Content.ReadAsStringAsync());
        var data = body.GetProperty("data");
        data.GetProperty("name").GetString().Should().Be("After rename");
        data.GetProperty("configuredFields").EnumerateArray().Should().HaveCount(1,
            "omitting secrets renames without clearing the stored credentials");
    }

    [Fact]
    public async Task Delete_RemovesTheConnection()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var created = await client.PostAsJsonAsync(
            "/api/v1/connections", SlackConnectionBody("To delete"));
        var id = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetString()!;

        (await client.DeleteAsync($"/api/v1/connections/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/v1/connections/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        (await client.GetAsync("/api/v1/connections/does-not-exist"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        // Unauthenticated client: credentials must not be listable or creatable.
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/connections"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.PostAsJsonAsync("/api/v1/connections", SlackConnectionBody("x")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
