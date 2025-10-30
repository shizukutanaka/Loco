using System;
using Xunit;
using Loco.Core.Security;

namespace Loco.Core.Tests.Security
{
    public class SecurityUtilitiesTests
    {
        [Fact]
        public void HashPassword_CreatesValidHash()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash = SecurityUtilities.HashPassword(password);

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            // Hash is Base64 encoded (salt + hash combined)
            Assert.True(hash.Length > 40); // At least 48 bytes (16 salt + 32 hash) in Base64
        }

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            // Arrange
            var password = "TestPassword123!";
            var hash = SecurityUtilities.HashPassword(password);

            // Act
            var result = SecurityUtilities.VerifyPassword(password, hash);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_IncorrectPassword_ReturnsFalse()
        {
            // Arrange
            var password = "TestPassword123!";
            var wrongPassword = "WrongPassword123!";
            var hash = SecurityUtilities.HashPassword(password);

            // Act
            var result = SecurityUtilities.VerifyPassword(wrongPassword, hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_DifferentPasswords_ProduceDifferentHashes()
        {
            // Arrange
            var password1 = "Password1";
            var password2 = "Password2";

            // Act
            var hash1 = SecurityUtilities.HashPassword(password1);
            var hash2 = SecurityUtilities.HashPassword(password2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashPassword_SamePassword_ProduceDifferentHashes()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash1 = SecurityUtilities.HashPassword(password);
            var hash2 = SecurityUtilities.HashPassword(password);

            // Assert - Different salts produce different hashes
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashPassword_Null_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => SecurityUtilities.HashPassword(null!));
        }

        [Theory]
        [InlineData("")]
        public void HashPassword_NullOrEmpty_ThrowsArgumentException(string password)
        {
            // Act & Assert
            // SecurityUtilities throws ArgumentNullException for null or empty
            Assert.Throws<ArgumentNullException>(() => SecurityUtilities.HashPassword(password));
        }

        [Fact]
        public void IsPathSafe_SafePath_ReturnsTrue()
        {
            // Arrange
            var safePath = @"C:\Users\Test\Documents\file.txt";

            // Act
            var result = SecurityUtilities.IsPathSafe(safePath);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(@"C:\Users\..\Windows\System32")]
        [InlineData(@"../../../etc/passwd")]
        [InlineData(@"~\..\..\secret")]
        public void IsPathSafe_PathTraversalAttempt_ReturnsFalse(string unsafePath)
        {
            // Act
            var result = SecurityUtilities.IsPathSafe(unsafePath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void SanitizeInput_RemovesScriptTags()
        {
            // Arrange
            var input = "Hello <script>alert('xss')</script> World";

            // Act
            var result = SecurityUtilities.SanitizeInput(input);

            // Assert
            Assert.DoesNotContain("<script>", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Hello", result);
            Assert.Contains("World", result);
        }

        [Fact]
        public void SanitizeCommandArgument_RemovesInjectionCharacters()
        {
            // Arrange
            var input = "test & echo hacked | rm -rf /";

            // Act
            var result = SecurityUtilities.SanitizeCommandArgument(input);

            // Assert
            Assert.DoesNotContain("&", result);
            Assert.DoesNotContain("|", result);
            Assert.DoesNotContain(";", result);
            Assert.Contains("test", result);
        }

        [Fact]
        public void ValidateFileName_WithValidName_ReturnsTrue()
        {
            // Arrange
            var fileName = "test-file.txt";

            // Act
            var result = SecurityUtilities.ValidateFileName(fileName);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateFileName_WithInvalidCharacters_ReturnsFalse()
        {
            // Arrange
            var fileName = "test<>file.txt";

            // Act
            var result = SecurityUtilities.ValidateFileName(fileName);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GenerateSecureToken_CreatesUniqueTokens()
        {
            // Act
            var token1 = SecurityUtilities.GenerateSecureToken();
            var token2 = SecurityUtilities.GenerateSecureToken();

            // Assert
            Assert.NotEqual(token1, token2);
            Assert.True(token1.Length > 0);
            Assert.True(token2.Length > 0);
        }

        [Fact]
        public void IsValidEmail_WithValidEmail_ReturnsTrue()
        {
            // Assert
            Assert.True(SecurityUtilities.IsValidEmail("user@example.com"));
            Assert.True(SecurityUtilities.IsValidEmail("test.user@subdomain.example.com"));
        }

        [Fact]
        public void IsValidEmail_WithInvalidEmail_ReturnsFalse()
        {
            // Assert
            Assert.False(SecurityUtilities.IsValidEmail("invalid.email"));
            Assert.False(SecurityUtilities.IsValidEmail("@example.com"));
            Assert.False(SecurityUtilities.IsValidEmail("user@"));
        }

        [Fact]
        public void MaskApiKey_ShowsPartialKey()
        {
            // Arrange
            var apiKey = "sk-1234567890abcdefghij1234567890ab";

            // Act
            var masked = SecurityUtilities.MaskApiKey(apiKey);

            // Assert
            Assert.Contains("***", masked);
            Assert.DoesNotContain("1234567890abcdefghij", masked);
        }

        [Fact]
        public void ValidateApiKey_WithOpenAiKey_ValidatesFormat()
        {
            // Arrange - This test verifies OpenAI format requirements (sk- prefix, length)
            var invalidKeyShort = "sk-123"; // Too short
            var invalidKeyNoPrefix = "xyz-1234567890abcdefghijklmnopqrstuvwxyz12345678";

            // Act
            var resultShort = SecurityUtilities.ValidateApiKey(invalidKeyShort, "openai");
            var resultNoPrefix = SecurityUtilities.ValidateApiKey(invalidKeyNoPrefix, "openai");

            // Assert
            Assert.False(resultShort.IsValid);
            Assert.Contains("20 characters", resultShort.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
            Assert.False(resultNoPrefix.IsValid); // xyz- prefix should fail for OpenAI
        }

        [Fact]
        public void RateLimiter_AllowsWithinLimit()
        {
            // Arrange
            var identifier = "test-user-" + Guid.NewGuid();

            // Act & Assert
            Assert.True(SecurityUtilities.RateLimiter.IsAllowed(identifier, 5, TimeSpan.FromMinutes(1)));
            Assert.True(SecurityUtilities.RateLimiter.IsAllowed(identifier, 5, TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public void RateLimiter_BlocksWhenExceedsLimit()
        {
            // Arrange
            var identifier = "test-user-limit-" + Guid.NewGuid();

            // Act
            for (int i = 0; i < 3; i++)
            {
                SecurityUtilities.RateLimiter.IsAllowed(identifier, 3, TimeSpan.FromMinutes(1));
            }

            var blocked = SecurityUtilities.RateLimiter.IsAllowed(identifier, 3, TimeSpan.FromMinutes(1));

            // Assert
            Assert.False(blocked);
        }
    }
}
