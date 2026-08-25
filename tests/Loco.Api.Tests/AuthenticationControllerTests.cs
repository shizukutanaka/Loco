using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Loco.Api.Security;

namespace Loco.Api.Tests;

/// <summary>
/// Integration tests for authentication, run against the real pipeline via
/// WebApplicationFactory (see <see cref="LocoApiFactory"/>).
///
/// The previous tests in this file unit-tested the old accept-any-credentials
/// controller (mocking IConfiguration) and asserted that ANY username/password
/// yields a token - i.e. they enshrined the vulnerability. They also never
/// compiled: this project's csproj failed restore under Central Package
/// Management (NU1008).
///
/// NOTE: these are the tests that still have never executed. They need a live
/// ASP.NET host, which scripts/run-tests-offline.sh cannot provide - it skips
/// this class by name rather than faking a host and reporting a pass. The rest
/// of the suite does run there; these wait for CI (docs/ci/ci.yml).
/// </summary>
public class AuthenticationTests : IClassFixture<LocoApiFactory>
{
    private readonly LocoApiFactory _factory;

    public AuthenticationTests(LocoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/workflows");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithWrongPassword_Returns401Envelope()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/authentication/token",
            new { username = LocoApiFactory.TestUsername, password = "wrong-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Token_WithUnknownUser_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/authentication/token",
            new { username = "nobody", password = "irrelevant" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithEmptyCredentials_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/authentication/token",
            new { username = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_WithValidCredentials_ReturnsEnvelopeWithBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/authentication/token",
            new { username = LocoApiFactory.TestUsername, password = LocoApiFactory.TestPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeTrue();

        var data = body.GetProperty("data");
        data.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("tokenType").GetString().Should().Be("Bearer");
        data.GetProperty("scope").GetString().Should().Contain("workflows:read");
    }

    [Fact]
    public async Task Token_IssuedToken_IsAcceptedByProtectedEndpoints()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/workflows");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymous()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>Unit tests for the PBKDF2 hasher backing Auth:Users.</summary>
public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_RoundTrips()
    {
        var hash = PasswordHasher.Hash("s3cret!");

        PasswordHasher.Verify("s3cret!", hash).Should().BeTrue();
        PasswordHasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSalts()
    {
        PasswordHasher.Hash("pw").Should().NotBe(PasswordHasher.Hash("pw"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2$abc$notbase64$nope")]
    [InlineData("PBKDF2$100000$AAAA")]
    public void Verify_MalformedStoredHash_ReturnsFalseNotThrow(string stored)
    {
        PasswordHasher.Verify("anything", stored).Should().BeFalse();
    }
}
