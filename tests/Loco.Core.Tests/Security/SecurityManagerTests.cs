using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Loco.Core.Security;

namespace Loco.Core.Tests.Security
{
    public class SecurityManagerTests
    {
        private readonly SecurityManager _securityManager;
        private readonly Mock<ILogger<SecurityManager>> _loggerMock;

        public SecurityManagerTests()
        {
            _loggerMock = new Mock<ILogger<SecurityManager>>();
            _securityManager = new SecurityManager(_loggerMock.Object);
        }

        [Theory]
        [InlineData("SELECT * FROM users", "")]
        [InlineData("'; DROP TABLE users; --", "")]
        [InlineData("admin' OR '1'='1", "admin&#39; OR &#39;1&#39;=&#39;1")]
        [InlineData("<script>alert('XSS')</script>", "&lt;script&gt;alert(&#39;XSS&#39;)&lt;/script&gt;")]
        public void SanitizeInput_RemovesDangerousPatterns(string input, string expected)
        {
            var result = _securityManager.SanitizeInput(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/safe/path/file.txt", true)]
        [InlineData("../../../etc/passwd", false)]
        [InlineData("~/sensitive/file", false)]
        [InlineData("./relative/path", false)]
        public void IsPathSafe_ValidatesPathCorrectly(string path, bool expected)
        {
            var result = _securityManager.IsPathSafe(path);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task CheckRateLimit_EnforcesLimit()
        {
            var identifier = "test-user";
            var maxRequests = 5;
            var period = TimeSpan.FromSeconds(1);

            // Should allow initial requests
            for (int i = 0; i < maxRequests; i++)
            {
                var allowed = _securityManager.CheckRateLimit(identifier, maxRequests, period);
                Assert.True(allowed);
            }

            // Should block after limit
            var blocked = _securityManager.CheckRateLimit(identifier, maxRequests, period);
            Assert.False(blocked);

            // Should allow after period
            await Task.Delay(period.Add(TimeSpan.FromMilliseconds(100)));
            var allowedAgain = _securityManager.CheckRateLimit(identifier, maxRequests, period);
            Assert.True(allowedAgain);
        }

        [Fact]
        public void CheckAccountLockout_BlocksAfterMaxAttempts()
        {
            var username = "testuser";
            var maxAttempts = 3;

            // Should allow initially
            Assert.True(_securityManager.CheckAccountLockout(username, maxAttempts));

            // Record failed attempts
            for (int i = 0; i < maxAttempts; i++)
            {
                _securityManager.RecordFailedAttempt(username);
            }

            // Should block after max attempts
            Assert.False(_securityManager.CheckAccountLockout(username, maxAttempts));

            // Should allow after reset
            _securityManager.ResetFailedAttempts(username);
            Assert.True(_securityManager.CheckAccountLockout(username, maxAttempts));
        }

        [Theory]
        [InlineData("weak", false)]
        [InlineData("NoDigits!", false)]
        [InlineData("no_uppercase123!", false)]
        [InlineData("NO_LOWERCASE123!", false)]
        [InlineData("NoSpecialChar123", false)]
        [InlineData("Strong@Pass123", true)]
        [InlineData("MyP@ssw0rd!2024", true)]
        public void ValidatePasswordStrength_ValidatesCorrectly(string password, bool expected)
        {
            var result = _securityManager.ValidatePasswordStrength(password);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GenerateSecureToken_ProducesUniqueTokens()
        {
            var token1 = _securityManager.GenerateSecureToken();
            var token2 = _securityManager.GenerateSecureToken();

            Assert.NotNull(token1);
            Assert.NotNull(token2);
            Assert.NotEqual(token1, token2);
            Assert.True(token1.Length > 0);
        }

        [Fact]
        public void HashPassword_ProducesConsistentHash()
        {
            var password = "TestPassword123!";
            
            var hash1 = _securityManager.HashPassword(password);
            var hash2 = _securityManager.HashPassword(password);

            // Hashes should be different due to different salts
            Assert.NotEqual(hash1, hash2);

            // Should verify correctly
            Assert.True(_securityManager.VerifyPassword(password, hash1));
            Assert.True(_securityManager.VerifyPassword(password, hash2));
            Assert.False(_securityManager.VerifyPassword("WrongPassword", hash1));
        }

        [Fact]
        public void IPBlacklisting_WorksCorrectly()
        {
            var ipAddress = "192.168.1.100";

            // Should not be blacklisted initially
            Assert.False(_securityManager.IsIPBlacklisted(ipAddress));

            // Blacklist IP
            _securityManager.BlacklistIP(ipAddress);

            // Should be blacklisted
            Assert.True(_securityManager.IsIPBlacklisted(ipAddress));
        }

        [Fact]
        public void CSRFToken_GenerationAndValidation()
        {
            var sessionId = "test-session";
            
            // Generate token
            var token = _securityManager.GenerateCSRFToken(sessionId);
            Assert.NotNull(token);

            // Should validate correct token
            Assert.True(_securityManager.ValidateCSRFToken(sessionId, token));

            // Should not validate wrong token
            Assert.False(_securityManager.ValidateCSRFToken(sessionId, "wrong-token"));

            // Should not validate for wrong session
            Assert.False(_securityManager.ValidateCSRFToken("wrong-session", token));
        }

        [Fact]
        public void SessionManagement_CompleteLifecycle()
        {
            var userId = "user123";
            var timeout = TimeSpan.FromMinutes(30);

            // Create session
            var sessionId = _securityManager.CreateSession(userId, timeout);
            Assert.NotNull(sessionId);

            // Validate active session
            Assert.True(_securityManager.ValidateSession(sessionId));

            // Invalidate session
            _securityManager.InvalidateSession(sessionId);

            // Should not validate after invalidation
            Assert.False(_securityManager.ValidateSession(sessionId));
        }

        [Fact]
        public void LogSecurityEvent_LogsCorrectly()
        {
            // Arrange
            var eventType = "LOGIN_ATTEMPT";
            var userId = "user123";
            var details = "Successful login";
            var ipAddress = "192.168.1.1";

            // Act
            _securityManager.LogSecurityEvent(eventType, userId, details, ipAddress);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("SECURITY_AUDIT")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}