using System;
using System.IO;
using System.Linq;
using Loco.Core.Security;
using Xunit;
using FluentAssertions;

namespace Loco.Core.Tests.Security;

/// <summary>
/// Tests for SecretsManager - AES-256-GCM encrypted local secret storage.
///
/// This class is what connector credentials are stored in, so the properties
/// worth pinning are the security ones: values must not be readable from the
/// file, tampering must be detected rather than silently decrypted, and a wrong
/// passphrase must fail loudly.
///
/// NOTE: authored where dotnet test cannot run (NuGet egress blocked by
/// organization policy). They DO run - scripts/run-tests-offline.sh executes
/// them against the harness in scripts/offline-test-harness/.
/// </summary>
public class SecretsManagerTests : IDisposable
{
    private readonly string _dir;
    private readonly SecretsManager _secrets;

    public SecretsManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"loco-secrets-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE", "test-passphrase");
        _secrets = new SecretsManager(_dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE", null);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void StoreAndGet_RoundTripsTheValue()
    {
        _secrets.StoreSecret("API_KEY", "sk-live-12345");

        _secrets.GetSecret("API_KEY").Should().Be("sk-live-12345");
    }

    [Fact]
    public void GetSecret_UnknownKey_ReturnsNull()
    {
        _secrets.GetSecret("NOPE").Should().BeNull();
    }

    [Fact]
    public void StoredFile_DoesNotContainThePlaintext()
    {
        _secrets.StoreSecret("API_KEY", "sk-live-12345");

        var contents = string.Concat(
            Directory.GetFiles(_dir, "*.json").Select(File.ReadAllText));

        contents.Should().NotContain("sk-live-12345",
            "the point of encrypting at rest is that the file does not carry the value");
    }

    [Fact]
    public void ListSecrets_ReturnsMetadataButNoValues()
    {
        _secrets.StoreSecret("A", "value-a", "first");
        _secrets.StoreSecret("B", "value-b");

        var listed = _secrets.ListSecrets();

        listed.Should().HaveCount(2);
        listed.Select(s => s.Key).Should().BeEquivalentTo(new[] { "A", "B" });
        listed.Single(s => s.Key == "A").Description.Should().Be("first");
        // SecretEntry has no value member at all - this asserts the shape stays that way.
        typeof(SecretEntry).GetProperties().Select(p => p.Name)
            .Should().NotContain(new[] { "Value", "Secret", "Cipher" });
    }

    [Fact]
    public void StoreSecret_Replacing_PreservesCreatedAt()
    {
        _secrets.StoreSecret("K", "one");
        var created = _secrets.ListSecrets().Single().CreatedAt;

        _secrets.StoreSecret("K", "two");
        var after = _secrets.ListSecrets().Single();

        after.CreatedAt.Should().Be(created);
        after.UpdatedAt.Should().NotBeNull();
        _secrets.GetSecret("K").Should().Be("two");
    }

    [Fact]
    public void UpdateSecret_UnknownKey_ReturnsFalse()
    {
        _secrets.UpdateSecret("MISSING", "x").Should().BeFalse();
    }

    [Fact]
    public void DeleteSecret_RemovesIt_AndIsIdempotent()
    {
        _secrets.StoreSecret("K", "v");

        _secrets.DeleteSecret("K").Should().BeTrue();
        _secrets.GetSecret("K").Should().BeNull();
        _secrets.DeleteSecret("K").Should().BeFalse("deleting twice is not an error");
    }

    [Fact]
    public void Secrets_SurviveANewInstance()
    {
        _secrets.StoreSecret("PERSISTED", "value");

        var reopened = new SecretsManager(_dir);

        reopened.GetSecret("PERSISTED").Should().Be("value");
    }

    [Fact]
    public void WrongPassphrase_FailsLoudly_RatherThanReturningGarbage()
    {
        _secrets.StoreSecret("K", "original");

        Environment.SetEnvironmentVariable("LOCO_SECRETS_PASSPHRASE", "a-different-passphrase");
        var other = new SecretsManager(_dir);

        // GCM authenticates, so this is a detected failure, not silent corruption.
        other.Invoking(s => s.GetSecret("K"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*authentication failed*");
    }

    [Fact]
    public void TamperedCiphertext_IsDetected()
    {
        _secrets.StoreSecret("K", "original");

        var file = Directory.GetFiles(_dir, "secrets.json").Single();
        var json = File.ReadAllText(file);

        // Flip a character inside the stored base64 blob.
        var start = json.IndexOf("\"cipher\"", StringComparison.OrdinalIgnoreCase);
        start.Should().BeGreaterThan(-1);
        var valueStart = json.IndexOf('"', json.IndexOf(':', start) + 1) + 1;
        var tampered = json.Remove(valueStart + 4, 1)
                           .Insert(valueStart + 4, json[valueStart + 4] == 'A' ? "B" : "A");
        File.WriteAllText(file, tampered);

        var reopened = new SecretsManager(_dir);

        reopened.Invoking(s => s.GetSecret("K"))
            .Should().Throw<InvalidOperationException>(
                "an unauthenticated cipher would have returned corrupted plaintext instead");
    }

    [Fact]
    public void CorruptStore_ThrowsRatherThanSilentlyStartingEmpty()
    {
        _secrets.StoreSecret("K", "v");
        File.WriteAllText(Directory.GetFiles(_dir, "secrets.json").Single(), "{not json");

        var reopened = new SecretsManager(_dir);

        // Starting fresh here would orphan every stored secret behind a file the
        // next write would overwrite.
        reopened.Invoking(s => s.ListSecrets())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StoreSecret_EmptyKey_Throws()
    {
        _secrets.Invoking(s => s.StoreSecret("", "v"))
            .Should().Throw<ArgumentException>();
    }
}
