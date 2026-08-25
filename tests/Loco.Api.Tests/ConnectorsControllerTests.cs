using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Loco.Api.Tests;

/// <summary>
/// Integration tests for the connector catalogue, run against the real pipeline
/// via <see cref="LocoApiFactory"/>.
///
/// The catalogue exists to answer one question the browser could not ask: which
/// credential fields does this connector actually read? Before it, the
/// connections form told the user to type the names from memory, and a typo
/// produced a connection that saved cleanly, listed cleanly, showed its fields
/// as set, and then failed at execution with a credential the connector never
/// found.
///
/// So the property worth pinning is not "the endpoint returns 200" - it is that
/// the names it publishes are the names the connectors read, and that nothing
/// in the process starts returning secrets.
///
/// NOTE: these run. scripts/run-tests-offline.sh builds the API as a real
/// executable and the harness launches it on a loopback port, so these talk to
/// actual Kestrel over actual HTTP - the response bodies asserted against here
/// genuinely crossed a socket. What is hand-written is the JwtBearer plumbing
/// that pulls the token off the request; the validation itself is Microsoft's
/// own library, shipped inside the SDK. docs/ci/ci.yml runs the real packages.
/// </summary>
public class ConnectorsApiTests : IClassFixture<LocoApiFactory>
{
    private readonly LocoApiFactory _factory;

    public ConnectorsApiTests(LocoApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> GetDataAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());

        return body.GetProperty("data");
    }

    [Fact]
    public async Task Lists_every_discovered_connector()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var data = await GetDataAsync(client, "/api/v1/connectors");
        var connectors = data.GetProperty("connectors");

        // ConnectorStartupService discovers every IConnector in Loco.Core at
        // startup; a catalogue that came back empty would mean the registry was
        // never populated, which is precisely the failure this must not hide.
        connectors.GetArrayLength().Should().BeGreaterThan(20);
        data.GetProperty("total").GetInt32().Should().Be(connectors.GetArrayLength());
    }

    [Fact]
    public async Task Publishes_the_field_name_the_connector_reads()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var slack = await GetDataAsync(client, "/api/v1/connectors/slack");

        slack.GetProperty("id").GetString().Should().Be("slack");

        var fields = slack.GetProperty("credentialFields")
            .EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .ToList();

        // SlackConnector.InitializeAsync reads GetCredentialString("botToken").
        // If this name and that name ever diverge, the form asks for a
        // credential the connector will not look for.
        fields.Should().Contain("botToken");
    }

    [Fact]
    public async Task Describes_each_field_well_enough_to_render_it()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var slack = await GetDataAsync(client, "/api/v1/connectors/slack");
        var botToken = slack.GetProperty("credentialFields")
            .EnumerateArray()
            .Single(f => f.GetProperty("name").GetString() == "botToken");

        botToken.GetProperty("label").GetString().Should().NotBeNullOrWhiteSpace();
        // A token must be masked; the form keys off this exact value.
        botToken.GetProperty("type").GetString().Should().Be("password");
        botToken.GetProperty("required").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Marks_an_optional_field_as_optional()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var slack = await GetDataAsync(client, "/api/v1/connectors/slack");
        var signingSecret = slack.GetProperty("credentialFields")
            .EnumerateArray()
            .SingleOrDefault(f => f.GetProperty("name").GetString() == "signingSecret");

        // Slack's signing secret belongs to the webhook path, not to sending a
        // message. Requiring it would block a connection that works.
        if (signingSecret.ValueKind != JsonValueKind.Undefined)
        {
            signingSecret.GetProperty("required").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public async Task Reports_the_authentication_style()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var slack = await GetDataAsync(client, "/api/v1/connectors/slack");

        slack.GetProperty("authType").GetString().Should().NotBeNullOrWhiteSpace();
        slack.GetProperty("category").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Returns_404_for_a_connector_that_does_not_exist()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/connectors/not-a-connector");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        // The catalogue holds no secrets, but it does describe the server's
        // integration surface, and the rest of the API is behind auth. An
        // endpoint that quietly is not would be an inconsistency worth catching
        // here rather than in a pen test.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/connectors");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Never_returns_a_credential_value()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/connectors");
        var raw = await response.Content.ReadAsStringAsync();

        // The catalogue describes fields; it must never grow a path to values.
        raw.Should().NotContain("\"value\"");
        raw.Should().NotContain("\"secrets\"");
    }
}
