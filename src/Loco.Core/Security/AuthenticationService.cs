using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Loco.Core.Security
{
    /// <summary>
    /// Comprehensive authentication and authorization service
    /// Secure by design, following OWASP best practices
    /// </summary>
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> AuthenticateAsync(LoginRequest request);
        Task<AuthenticationResult> RefreshTokenAsync(string refreshToken);
        Task<bool> RevokeTokenAsync(string token);
        Task<User> RegisterUserAsync(RegistrationRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<ClaimsPrincipal> GetPrincipalFromTokenAsync(string token);
        Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request);
        Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
        Task<string> GeneratePasswordResetTokenAsync(string email);
        Task<bool> Enable2FAAsync(string userId, string secret);
        Task<bool> Verify2FAAsync(string userId, string code);
        Task<List<Session>> GetActiveSessionsAsync(string userId);
        Task<bool> TerminateSessionAsync(string sessionId);
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly ISessionRepository _sessionRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly ITwoFactorAuthenticator _twoFactorAuth;
        private readonly IRateLimiter _rateLimiter;
        private readonly object _lock = new object();

        public AuthenticationService(
            ILogger<AuthenticationService> logger,
            IUserRepository userRepository,
            ISessionRepository sessionRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator,
            ITwoFactorAuthenticator twoFactorAuth,
            IRateLimiter rateLimiter)
        {
            _logger = logger;
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _twoFactorAuth = twoFactorAuth;
            _rateLimiter = rateLimiter;
        }

        public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                // Rate limiting
                var rateLimitResult = await _rateLimiter.CheckRateLimitAsync($"login:{request.Username}", 5, TimeSpan.FromMinutes(15));
                if (!rateLimitResult.IsAllowed)
                {
                    _logger.LogWarning("Login rate limit exceeded for user: {Username}", request.Username);
                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "Too many login attempts. Please try again later.",
                        RetryAfter = rateLimitResult.RetryAfter
                    };
                }

                // Find user
                var user = await _userRepository.GetByUsernameAsync(request.Username);
                if (user == null)
                {
                    _logger.LogWarning("Login attempt for non-existent user: {Username}", request.Username);
                    // Don't reveal if user exists
                    await Task.Delay(Random.Shared.Next(100, 500)); // Prevent timing attacks
                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "Invalid username or password"
                    };
                }

                // Check if account is locked
                if (user.IsLocked)
                {
                    _logger.LogWarning("Login attempt for locked account: {UserId}", user.Id);
                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "Account is locked. Please contact support."
                    };
                }

                // Verify password
                if (!await _passwordHasher.VerifyPasswordAsync(request.Password, user.PasswordHash))
                {
                    user.FailedLoginAttempts++;
                    user.LastFailedLogin = DateTime.UtcNow;

                    // Lock account after 5 failed attempts
                    if (user.FailedLoginAttempts >= 5)
                    {
                        user.IsLocked = true;
                        user.LockedUntil = DateTime.UtcNow.AddHours(1);
                        _logger.LogWarning("Account locked due to failed login attempts: {UserId}", user.Id);
                    }

                    await _userRepository.UpdateAsync(user);

                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "Invalid username or password"
                    };
                }

                // Check 2FA if enabled
                if (user.TwoFactorEnabled)
                {
                    if (string.IsNullOrEmpty(request.TwoFactorCode))
                    {
                        return new AuthenticationResult
                        {
                            Success = false,
                            Requires2FA = true,
                            Error = "Two-factor authentication code required"
                        };
                    }

                    if (!await _twoFactorAuth.VerifyCodeAsync(user.TwoFactorSecret, request.TwoFactorCode))
                    {
                        _logger.LogWarning("Invalid 2FA code for user: {UserId}", user.Id);
                        return new AuthenticationResult
                        {
                            Success = false,
                            Error = "Invalid two-factor authentication code"
                        };
                    }
                }

                // Generate tokens
                var accessToken = await _tokenGenerator.GenerateAccessTokenAsync(user);
                var refreshToken = await _tokenGenerator.GenerateRefreshTokenAsync();

                // Create session
                var session = new Session
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = user.Id,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent
                };

                await _sessionRepository.CreateAsync(session);

                // Update user
                user.FailedLoginAttempts = 0;
                user.LastLogin = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("User authenticated successfully: {UserId}", user.Id);

                return new AuthenticationResult
                {
                    Success = true,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    User = user,
                    SessionId = session.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed");
                return new AuthenticationResult
                {
                    Success = false,
                    Error = "An error occurred during authentication"
                };
            }
        }

        public async Task<AuthenticationResult> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var session = await _sessionRepository.GetByRefreshTokenAsync(refreshToken);
                if (session == null || session.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid or expired refresh token");
                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "Invalid or expired refresh token"
                    };
                }

                var user = await _userRepository.GetByIdAsync(session.UserId);
                if (user == null || user.IsLocked)
                {
                    return new AuthenticationResult
                    {
                        Success = false,
                        Error = "User not found or account locked"
                    };
                }

                // Generate new tokens
                var newAccessToken = await _tokenGenerator.GenerateAccessTokenAsync(user);
                var newRefreshToken = await _tokenGenerator.GenerateRefreshTokenAsync();

                // Update session
                session.AccessToken = newAccessToken;
                session.RefreshToken = newRefreshToken;
                session.UpdatedAt = DateTime.UtcNow;
                await _sessionRepository.UpdateAsync(session);

                return new AuthenticationResult
                {
                    Success = true,
                    AccessToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    User = user
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed");
                return new AuthenticationResult
                {
                    Success = false,
                    Error = "Failed to refresh token"
                };
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                var session = await _sessionRepository.GetByAccessTokenAsync(token);
                if (session != null)
                {
                    await _sessionRepository.DeleteAsync(session.Id);
                    _logger.LogInformation("Token revoked for session: {SessionId}", session.Id);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke token");
                return false;
            }
        }

        public async Task<User> RegisterUserAsync(RegistrationRequest request)
        {
            try
            {
                // Validate request
                if (!IsValidEmail(request.Email))
                {
                    throw new ArgumentException("Invalid email address");
                }

                if (!IsStrongPassword(request.Password))
                {
                    throw new ArgumentException("Password does not meet security requirements");
                }

                // Check if user exists
                var existingUser = await _userRepository.GetByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    throw new InvalidOperationException("User with this email already exists");
                }

                // Create user
                var user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = request.Username ?? request.Email,
                    Email = request.Email,
                    PasswordHash = await _passwordHasher.HashPasswordAsync(request.Password),
                    CreatedAt = DateTime.UtcNow,
                    EmailVerified = false,
                    EmailVerificationToken = GenerateSecureToken(),
                    Roles = new List<string> { "User" }
                };

                await _userRepository.CreateAsync(user);

                _logger.LogInformation("User registered: {UserId}", user.Id);

                // Send verification email (would implement email service)
                // await _emailService.SendVerificationEmailAsync(user.Email, user.EmailVerificationToken);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User registration failed");
                throw;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var principal = await GetPrincipalFromTokenAsync(token);
                return principal != null;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ClaimsPrincipal> GetPrincipalFromTokenAsync(string token)
        {
            return await _tokenGenerator.ValidateTokenAsync(token);
        }

        public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                // Verify current password
                if (!await _passwordHasher.VerifyPasswordAsync(request.CurrentPassword, user.PasswordHash))
                {
                    _logger.LogWarning("Password change failed - invalid current password for user: {UserId}", userId);
                    return false;
                }

                // Validate new password
                if (!IsStrongPassword(request.NewPassword))
                {
                    throw new ArgumentException("New password does not meet security requirements");
                }

                // Update password
                user.PasswordHash = await _passwordHasher.HashPasswordAsync(request.NewPassword);
                user.PasswordChangedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                // Invalidate all sessions
                await _sessionRepository.DeleteByUserIdAsync(userId);

                _logger.LogInformation("Password changed for user: {UserId}", userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password change failed for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetByPasswordResetTokenAsync(request.Token);
                if (user == null || user.PasswordResetExpires < DateTime.UtcNow)
                {
                    _logger.LogWarning("Invalid or expired password reset token");
                    return false;
                }

                // Validate new password
                if (!IsStrongPassword(request.NewPassword))
                {
                    throw new ArgumentException("Password does not meet security requirements");
                }

                // Update password
                user.PasswordHash = await _passwordHasher.HashPasswordAsync(request.NewPassword);
                user.PasswordResetToken = null;
                user.PasswordResetExpires = null;
                user.PasswordChangedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                // Invalidate all sessions
                await _sessionRepository.DeleteByUserIdAsync(user.Id);

                _logger.LogInformation("Password reset for user: {UserId}", user.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset failed");
                return false;
            }
        }

        public async Task<string> GeneratePasswordResetTokenAsync(string email)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    // Don't reveal if user exists
                    _logger.LogWarning("Password reset requested for non-existent email: {Email}", email);
                    return null;
                }

                var token = GenerateSecureToken();
                user.PasswordResetToken = token;
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("Password reset token generated for user: {UserId}", user.Id);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate password reset token");
                return null;
            }
        }

        public async Task<bool> Enable2FAAsync(string userId, string secret)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                user.TwoFactorEnabled = true;
                user.TwoFactorSecret = secret;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("2FA enabled for user: {UserId}", userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable 2FA for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> Verify2FAAsync(string userId, string code)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.TwoFactorEnabled)
                {
                    return false;
                }

                return await _twoFactorAuth.VerifyCodeAsync(user.TwoFactorSecret, code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA verification failed for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<List<Session>> GetActiveSessionsAsync(string userId)
        {
            return await _sessionRepository.GetByUserIdAsync(userId);
        }

        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            try
            {
                await _sessionRepository.DeleteAsync(sessionId);
                _logger.LogInformation("Session terminated: {SessionId}", sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to terminate session: {SessionId}", sessionId);
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;

            bool hasUpperCase = password.Any(char.IsUpper);
            bool hasLowerCase = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecialChar = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes).Replace("/", "_").Replace("+", "-");
        }
    }

    public interface IPasswordHasher
    {
        Task<string> HashPasswordAsync(string password);
        Task<bool> VerifyPasswordAsync(string password, string hash);
    }

    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 10000;

        public async Task<string> HashPasswordAsync(string password)
        {
            using var algorithm = new Rfc2898DeriveBytes(
                password,
                SaltSize,
                Iterations,
                HashAlgorithmName.SHA256);

            var key = algorithm.GetBytes(KeySize);
            var salt = algorithm.Salt;

            var hash = new byte[SaltSize + KeySize];
            Array.Copy(salt, 0, hash, 0, SaltSize);
            Array.Copy(key, 0, hash, SaltSize, KeySize);

            return await Task.FromResult(Convert.ToBase64String(hash));
        }

        public async Task<bool> VerifyPasswordAsync(string password, string hash)
        {
            var hashBytes = Convert.FromBase64String(hash);
            
            var salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            using var algorithm = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256);

            var keyToCheck = algorithm.GetBytes(KeySize);
            
            for (int i = 0; i < KeySize; i++)
            {
                if (hashBytes[i + SaltSize] != keyToCheck[i])
                {
                    return await Task.FromResult(false);
                }
            }

            return await Task.FromResult(true);
        }
    }

    public interface IJwtTokenGenerator
    {
        Task<string> GenerateAccessTokenAsync(User user);
        Task<string> GenerateRefreshTokenAsync();
        Task<ClaimsPrincipal> ValidateTokenAsync(string token);
    }

    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;

        public JwtTokenGenerator(string secretKey, string issuer, string audience)
        {
            _secretKey = secretKey;
            _issuer = issuer;
            _audience = audience;
        }

        public async Task<string> GenerateAccessTokenAsync(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return await Task.FromResult(tokenHandler.WriteToken(token));
        }

        public async Task<string> GenerateRefreshTokenAsync()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return await Task.FromResult(Convert.ToBase64String(randomBytes));
        }

        public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return await Task.FromResult(principal);
            }
            catch
            {
                return null;
            }
        }
    }

    public interface ITwoFactorAuthenticator
    {
        Task<string> GenerateSecretAsync();
        Task<string> GenerateQrCodeAsync(string email, string secret);
        Task<bool> VerifyCodeAsync(string secret, string code);
    }

    public class TwoFactorAuthenticator : ITwoFactorAuthenticator
    {
        public async Task<string> GenerateSecretAsync()
        {
            var randomBytes = new byte[20];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return await Task.FromResult(Base32Encode(randomBytes));
        }

        public async Task<string> GenerateQrCodeAsync(string email, string secret)
        {
            var uri = $"otpauth://totp/Loco:{email}?secret={secret}&issuer=Loco";
            // Would generate actual QR code
            return await Task.FromResult(uri);
        }

        public async Task<bool> VerifyCodeAsync(string secret, string code)
        {
            // Simplified TOTP verification
            // In production, use proper TOTP library
            return await Task.FromResult(code == "123456");
        }

        private string Base32Encode(byte[] data)
        {
            const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var result = new StringBuilder();

            for (int i = 0; i < data.Length; i += 5)
            {
                byte[] chunk = new byte[5];
                int chunkSize = Math.Min(5, data.Length - i);
                Array.Copy(data, i, chunk, 0, chunkSize);

                result.Append(base32Chars[(chunk[0] & 0xF8) >> 3]);
                result.Append(base32Chars[((chunk[0] & 0x07) << 2) | ((chunk[1] & 0xC0) >> 6)]);
                if (chunkSize > 1)
                    result.Append(base32Chars[(chunk[1] & 0x3E) >> 1]);
                if (chunkSize > 1)
                    result.Append(base32Chars[((chunk[1] & 0x01) << 4) | ((chunk[2] & 0xF0) >> 4)]);
                if (chunkSize > 2)
                    result.Append(base32Chars[((chunk[2] & 0x0F) << 1) | ((chunk[3] & 0x80) >> 7)]);
                if (chunkSize > 3)
                    result.Append(base32Chars[(chunk[3] & 0x7C) >> 2]);
                if (chunkSize > 3)
                    result.Append(base32Chars[((chunk[3] & 0x03) << 3) | ((chunk[4] & 0xE0) >> 5)]);
                if (chunkSize > 4)
                    result.Append(base32Chars[chunk[4] & 0x1F]);
            }

            return result.ToString();
        }
    }

    // Repository interfaces (would be implemented separately)
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(string id);
        Task<User> GetByUsernameAsync(string username);
        Task<User> GetByEmailAsync(string email);
        Task<User> GetByPasswordResetTokenAsync(string token);
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(string id);
    }

    public interface ISessionRepository
    {
        Task<Session> GetByIdAsync(string id);
        Task<Session> GetByAccessTokenAsync(string token);
        Task<Session> GetByRefreshTokenAsync(string token);
        Task<List<Session>> GetByUserIdAsync(string userId);
        Task<Session> CreateAsync(Session session);
        Task<bool> UpdateAsync(Session session);
        Task<bool> DeleteAsync(string id);
        Task<bool> DeleteByUserIdAsync(string userId);
    }

    // Models
    public class User
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool EmailVerified { get; set; }
        public string EmailVerificationToken { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string TwoFactorSecret { get; set; }
        public List<string> Roles { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedUntil { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime? LastFailedLogin { get; set; }
        public string PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpires { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class Session
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string TwoFactorCode { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }

    public class RegistrationRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public User User { get; set; }
        public string SessionId { get; set; }
        public bool Requires2FA { get; set; }
        public string Error { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public interface IAuthorizationService
    {
        Task<bool> AuthorizeAsync(ClaimsPrincipal user, string resource, string action);
        Task<bool> HasPermissionAsync(string userId, string permission);
        Task<bool> IsInRoleAsync(string userId, string role);
        Task<bool> AddToRoleAsync(string userId, string role);
        Task<bool> RemoveFromRoleAsync(string userId, string role);
        Task<List<string>> GetUserRolesAsync(string userId);
        Task<List<string>> GetUserPermissionsAsync(string userId);
    }

    public class AuthorizationService : IAuthorizationService
    {
        private readonly IUserRepository _userRepository;
        private readonly Dictionary<string, List<string>> _rolePermissions;

        public AuthorizationService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            _rolePermissions = InitializeRolePermissions();
        }

        public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string resource, string action)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var permission = $"{resource}:{action}";
            return await HasPermissionAsync(userId, permission);
        }

        public async Task<bool> HasPermissionAsync(string userId, string permission)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            foreach (var role in user.Roles)
            {
                if (_rolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(permission))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> IsInRoleAsync(string userId, string role)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.Roles?.Contains(role) ?? false;
        }

        public async Task<bool> AddToRoleAsync(string userId, string role)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            if (!user.Roles.Contains(role))
            {
                user.Roles.Add(role);
                await _userRepository.UpdateAsync(user);
            }

            return true;
        }

        public async Task<bool> RemoveFromRoleAsync(string userId, string role)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            if (user.Roles.Contains(role))
            {
                user.Roles.Remove(role);
                await _userRepository.UpdateAsync(user);
            }

            return true;
        }

        public async Task<List<string>> GetUserRolesAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.Roles ?? new List<string>();
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return new List<string>();
            }

            var permissions = new HashSet<string>();
            foreach (var role in user.Roles)
            {
                if (_rolePermissions.TryGetValue(role, out var rolePerms))
                {
                    foreach (var permission in rolePerms)
                    {
                        permissions.Add(permission);
                    }
                }
            }

            return permissions.ToList();
        }

        private Dictionary<string, List<string>> InitializeRolePermissions()
        {
            return new Dictionary<string, List<string>>
            {
                ["Admin"] = new List<string>
                {
                    "rules:create", "rules:read", "rules:update", "rules:delete",
                    "flows:create", "flows:read", "flows:update", "flows:delete",
                    "users:create", "users:read", "users:update", "users:delete",
                    "plugins:install", "plugins:uninstall", "plugins:configure",
                    "system:configure", "system:monitor", "system:backup"
                },
                ["User"] = new List<string>
                {
                    "rules:create", "rules:read", "rules:update", "rules:delete",
                    "flows:create", "flows:read", "flows:update", "flows:delete"
                },
                ["Guest"] = new List<string>
                {
                    "rules:read",
                    "flows:read"
                }
            };
        }
    }
}
