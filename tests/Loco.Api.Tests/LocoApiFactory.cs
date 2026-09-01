using Loco.Core.Security;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Loco.Api.Security;

namespace Loco.Api.Tests;

/// <summary>
/// WebApplicationFactory wired with test configuration: a known JWT secret,
/// one configured user (PBKDF2-hashed at fixture construction), and a
/// per-fixture temp workflow store directory that is deleted on dispose.
/// </summary>
public class LocoApiFactory : WebApplicationFactory<Program>
{
    public const string TestUsername = "test-admin";
    public const string TestPassword = "integration-test-password";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), $"loco-api-tests-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("Jwt:SecretKey", "integration-test-signing-key-0123456789ABCDEF");
        builder.UseSetting("Auth:Issuer", "loco-api");
        builder.UseSetting("Auth:Audience", "loco-clients");
        builder.UseSetting("Auth:Users:0:Username", TestUsername);
        builder.UseSetting("Auth:Users:0:PasswordHash", PasswordHasher.Hash(TestPassword));
        builder.UseSetting("Auth:Users:0:Scopes:0", "workflows:read");
        builder.UseSetting("Auth:Users:0:Scopes:1", "workflows:manage");
        builder.UseSetting("Auth:Users:0:Scopes:2", "workflows:execute");
        builder.UseSetting("Storage:DataDirectory", _dataDirectory);
    }

    /// <summary>Create a client that has already exchanged credentials for a JWT.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/authentication/token",
            new { username = TestUsername, password = TestPassword });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("data").GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_dataDirectory, recursive: true); } catch { /* best effort */ }
    }
}
