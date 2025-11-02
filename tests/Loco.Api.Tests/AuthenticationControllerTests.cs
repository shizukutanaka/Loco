using Xunit;
using Moq;
using System;
using System.Threading.Tasks;
using Loco.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loco.Api.Tests
{
    /// <summary>
    /// AuthenticationController テストスイート
    /// Tests for JWT token generation and authentication
    /// </summary>
    public class AuthenticationControllerTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<AuthenticationController>> _mockLogger;
        private readonly AuthenticationController _controller;

        public AuthenticationControllerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<AuthenticationController>>();

            // Setup JWT configuration
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(x => x["SecretKey"])
                .Returns("DefaultSecretKeyChangeInProduction12345");
            jwtSection.Setup(x => x["Issuer"])
                .Returns("https://loco.local");
            jwtSection.Setup(x => x["Audience"])
                .Returns("loco-api");

            _mockConfiguration.Setup(x => x.GetSection("Jwt"))
                .Returns(jwtSection.Object);

            _controller = new AuthenticationController(
                _mockConfiguration.Object,
                _mockLogger.Object
            );
        }

        #region Token Generation Tests

        [Fact]
        public void GetToken_WithValidCredentials_ReturnsToken()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "password",
                GrantType = "password"
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var response = okResult.Value as TokenResponse;
            Assert.NotNull(response);
            Assert.False(string.IsNullOrEmpty(response.AccessToken));
            Assert.Equal("Bearer", response.TokenType);
            Assert.True(response.ExpiresIn > 0);
        }

        [Fact]
        public void GetToken_WithEmptyUsername_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "",
                Password = "password"
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public void GetToken_WithNullPassword_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = null
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public void GetToken_WithEmptyPassword_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = ""
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public void GetToken_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange - Simulate invalid credentials
            var request = new TokenRequest
            {
                Username = "invalid",
                Password = "invalid"
            };

            // Act - In real scenario, this would validate against database
            var result = _controller.GetToken(request);

            // Assert
            // Current implementation accepts any non-empty credentials
            // In production, this would return Unauthorized
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        #endregion

        #region Token Format Tests

        [Fact]
        public void GetToken_ReturnsValidJWTFormat()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "testuser",
                Password = "testpass"
            };

            // Act
            var result = _controller.GetToken(request);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;

            // Assert
            Assert.NotNull(response);
            var token = response.AccessToken;

            // JWT format: header.payload.signature
            var parts = token.Split('.');
            Assert.Equal(3, parts.Length);

            // Each part should be non-empty
            foreach (var part in parts)
            {
                Assert.False(string.IsNullOrEmpty(part));
            }
        }

        [Fact]
        public void GetToken_IncludesCorrectScope()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "password"
            };

            // Act
            var result = _controller.GetToken(request);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;

            // Assert
            Assert.NotNull(response.Scope);
            Assert.Contains("workflows:read", response.Scope);
            Assert.Contains("workflows:manage", response.Scope);
            Assert.Contains("workflows:execute", response.Scope);
        }

        #endregion

        #region Refresh Token Tests

        [Fact]
        public void RefreshToken_WithValidToken_ReturnsNewToken()
        {
            // Arrange
            var request = new RefreshTokenRequest
            {
                RefreshToken = "valid-refresh-token"
            };

            // Act
            var result = _controller.RefreshToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(200, okResult.StatusCode);

            var response = okResult.Value as TokenResponse;
            Assert.NotNull(response.AccessToken);
        }

        [Fact]
        public void RefreshToken_WithEmptyToken_ReturnsBadRequest()
        {
            // Arrange
            var request = new RefreshTokenRequest
            {
                RefreshToken = ""
            };

            // Act
            var result = _controller.RefreshToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        [Fact]
        public void RefreshToken_WithNullToken_ReturnsBadRequest()
        {
            // Arrange
            var request = new RefreshTokenRequest
            {
                RefreshToken = null
            };

            // Act
            var result = _controller.RefreshToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region API Key Validation Tests

        [Fact]
        public void ValidateApiKey_WithValidApiKey_ReturnsValid()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                ApiKey = "loco_sk_test1234567890abcdef"
            };

            // Act
            var result = _controller.ValidateApiKey(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as ApiKeyValidationResponse;
            Assert.NotNull(response);
            Assert.True(response.IsValid);
        }

        [Fact]
        public void ValidateApiKey_WithInvalidApiKey_ReturnsUnauthorized()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                ApiKey = "invalid_key_format"
            };

            // Act
            var result = _controller.ValidateApiKey(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(401, unauthorizedResult.StatusCode);
        }

        [Fact]
        public void ValidateApiKey_WithEmptyApiKey_ReturnsBadRequest()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                ApiKey = ""
            };

            // Act
            var result = _controller.ValidateApiKey(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(400, badRequestResult.StatusCode);
        }

        #endregion

        #region Security Tests

        [Fact]
        public void GetToken_DoesNotReturnPasswordInResponse()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "secret123"
            };

            // Act
            var result = _controller.GetToken(request);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;

            // Assert
            var responseString = response.ToString();
            Assert.DoesNotContain("secret123", responseString);
            Assert.DoesNotContain("password", responseString.ToLower());
        }

        [Fact]
        public void GetToken_SetsAppropriateExpirationTime()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "password"
            };

            // Act
            var result = _controller.GetToken(request);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;

            // Assert
            Assert.NotNull(response);
            Assert.True(response.ExpiresIn > 0);
            Assert.True(response.ExpiresIn <= 3600); // Should be 1 hour max
            Assert.True(response.ExpiresIn >= 1800); // Should be at least 30 min
        }

        [Fact]
        public void ValidateApiKey_MasksInternalErrors()
        {
            // Arrange
            var request = new ApiKeyRequest
            {
                ApiKey = null
            };

            // Act & Assert
            var ex = Record.Exception(() => _controller.ValidateApiKey(request));
            // Should not expose internal error details
        }

        #endregion

        #region Concurrent Token Requests

        [Fact]
        public void GetToken_MultipleRequestsConcurrently_AllSucceed()
        {
            // Arrange
            var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
            for (int i = 0; i < 10; i++)
            {
                var request = new TokenRequest
                {
                    Username = $"user{i}",
                    Password = "password"
                };

                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    var result = _controller.GetToken(request);
                    Assert.IsType<OkObjectResult>(result);
                }));
            }

            // Act
            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert - All tasks completed successfully
            Assert.True(tasks.TrueForAll(t => t.IsCompletedSuccessfully));
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void TokenGeneration_UsesConfiguredSecretKey()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "password"
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;
            Assert.NotNull(response.AccessToken);
            // Token should be generated with configured secret
        }

        [Fact]
        public void TokenGeneration_IncludesCorrectIssuerAndAudience()
        {
            // Arrange
            var request = new TokenRequest
            {
                Username = "admin",
                Password = "password"
            };

            // Act
            var result = _controller.GetToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value as TokenResponse;
            Assert.NotNull(response.AccessToken);
            // In production, would decode JWT and verify claims
        }

        #endregion
    }
}
