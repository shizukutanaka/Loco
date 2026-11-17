using Loco.Api.Security;
using System;
using System.IO;
using Xunit;

namespace Loco.Api.Tests
{
    /// <summary>
    /// Tests for JwtSecretResolver: the JWT signing secret must never fall
    /// back to a shipped, shared value. Resolution order is configured
    /// secret -> persisted per-installation secret -> explicit failure.
    /// </summary>
    public class JwtSecretResolverTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _secretFilePath;

        public JwtSecretResolverTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "loco-jwt-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _secretFilePath = Path.Combine(_tempDir, JwtSecretResolver.SecretFileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public void Resolve_WithConfiguredSecret_ReturnsItAndCreatesNoFile()
        {
            var resolved = JwtSecretResolver.Resolve("configured-secret-value-0123456789", _secretFilePath);

            Assert.Equal("configured-secret-value-0123456789", resolved);
            Assert.False(File.Exists(_secretFilePath));
        }

        [Fact]
        public void Resolve_WithoutConfiguredSecret_GeneratesAndPersistsSecret()
        {
            var resolved = JwtSecretResolver.Resolve(null, _secretFilePath);

            Assert.True(resolved.Length >= 32);
            Assert.True(File.Exists(_secretFilePath));
            Assert.Equal(resolved, File.ReadAllText(_secretFilePath).Trim());
        }

        [Fact]
        public void Resolve_AcrossRestarts_ReturnsSamePersistedSecret()
        {
            var first = JwtSecretResolver.Resolve(null, _secretFilePath);
            var second = JwtSecretResolver.Resolve(null, _secretFilePath);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Resolve_GeneratesDistinctSecretsPerInstallation()
        {
            var otherPath = Path.Combine(_tempDir, "other", JwtSecretResolver.SecretFileName);

            var first = JwtSecretResolver.Resolve(null, _secretFilePath);
            var second = JwtSecretResolver.Resolve(null, otherPath);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Resolve_WithExistingValidFile_ReturnsStoredSecret()
        {
            File.WriteAllText(_secretFilePath, "  stored-secret-value-0123456789abcdef  ");

            var resolved = JwtSecretResolver.Resolve(null, _secretFilePath);

            Assert.Equal("stored-secret-value-0123456789abcdef", resolved);
        }

        [Fact]
        public void Resolve_WithUnusableExistingFile_ThrowsWithReason()
        {
            File.WriteAllText(_secretFilePath, "too-short");

            var ex = Assert.Throws<InvalidOperationException>(
                () => JwtSecretResolver.Resolve(null, _secretFilePath));
            Assert.Contains("does not contain a usable secret", ex.Message);
        }

        [Fact]
        public void Resolve_WhenSecretCannotBePersisted_ThrowsWithReason()
        {
            // A path whose "directory" is an existing file can never be created.
            var blockerFile = Path.Combine(_tempDir, "blocker");
            File.WriteAllText(blockerFile, "not a directory");
            var impossiblePath = Path.Combine(blockerFile, JwtSecretResolver.SecretFileName);

            var ex = Assert.Throws<InvalidOperationException>(
                () => JwtSecretResolver.Resolve(null, impossiblePath));
            Assert.Contains("could not be persisted", ex.Message);
        }

        [Fact]
        public void GetSecretFilePath_PlacesSecretNextToRuleStore()
        {
            var ruleStorePath = Path.Combine(_tempDir, "data", "rules.json");

            var secretPath = JwtSecretResolver.GetSecretFilePath(ruleStorePath);

            Assert.Equal(Path.Combine(_tempDir, "data", JwtSecretResolver.SecretFileName), secretPath);
        }
    }
}
