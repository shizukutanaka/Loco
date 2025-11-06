// John Carmack: "Security should be simple and correct"
// Rob Pike: "Clear is better than clever"

using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Loco.Core.Practical;

/// <summary>
/// Simple authentication - JWT tokens, password hashing, user management
/// Secure, fast, no external dependencies except JWT library
/// </summary>
public class SimpleAuth
{
    private readonly string _jwtSecret;
    private readonly int _tokenExpirationMinutes;
    private readonly SimpleLogger _logger;

    public SimpleAuth(string jwtSecret, int tokenExpirationMinutes = 60, SimpleLogger? logger = null)
    {
        _jwtSecret = jwtSecret;
        _tokenExpirationMinutes = tokenExpirationMinutes;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(SimpleAuth));
    }

    // Hash password
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    // Hash password with salt (more secure)
    public (string hash, string salt) HashPasswordWithSalt(string password)
    {
        var salt = GenerateSalt();
        var hash = HashPasswordWithSalt(password, salt);
        return (hash, salt);
    }

    private string HashPasswordWithSalt(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var combined = password + salt;
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(bytes);
    }

    // Verify password
    public bool VerifyPassword(string password, string hash, string? salt = null)
    {
        var computedHash = salt != null
            ? HashPasswordWithSalt(password, salt)
            : HashPassword(password);

        return computedHash == hash;
    }

    // Generate JWT token
    public string GenerateToken(string userId, Dictionary<string, string>? claims = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSecret);

        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (claims != null)
        {
            foreach (var claim in claims)
            {
                claimsList.Add(new Claim(claim.Key, claim.Value));
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claimsList),
            Expires = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // Validate JWT token
    public (bool valid, ClaimsPrincipal? principal) ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSecret);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            return (true, principal);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Token validation failed: {ex.Message}");
            return (false, null);
        }
    }

    // Get user ID from token
    public string? GetUserIdFromToken(string token)
    {
        var (valid, principal) = ValidateToken(token);
        if (!valid || principal == null) return null;

        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    // Generate random salt
    private string GenerateSalt()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    // Generate API key
    public string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..32];
    }
}

/// <summary>
/// User store interface
/// </summary>
public interface IUserStore
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<bool> DeleteAsync(string id);
}

/// <summary>
/// User model
/// </summary>
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? PasswordSalt { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// In-memory user store
/// </summary>
public class InMemoryUserStore : IUserStore
{
    private readonly Dictionary<string, User> _users = new();

    public Task<User?> GetByIdAsync(string id)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByUsernameAsync(string username)
    {
        var user = _users.Values.FirstOrDefault(u => u.Username == username);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email)
    {
        var user = _users.Values.FirstOrDefault(u => u.Email == email);
        return Task.FromResult(user);
    }

    public Task<bool> CreateAsync(User user)
    {
        if (_users.ContainsKey(user.Id)) return Task.FromResult(false);
        _users[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<bool> UpdateAsync(User user)
    {
        if (!_users.ContainsKey(user.Id)) return Task.FromResult(false);
        _users[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id)
    {
        return Task.FromResult(_users.Remove(id));
    }
}

/// <summary>
/// Authentication service
/// </summary>
public class AuthService
{
    private readonly SimpleAuth _auth;
    private readonly IUserStore _userStore;
    private readonly SimpleLogger _logger;

    public AuthService(SimpleAuth auth, IUserStore userStore, SimpleLogger? logger = null)
    {
        _auth = auth;
        _userStore = userStore;
        _logger = logger ?? SimpleLoggerFactory.GetLogger(nameof(AuthService));
    }

    // Register new user
    public async Task<(bool success, string? userId, string? error)> RegisterAsync(
        string username,
        string email,
        string password)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, null, "Username and password required");

        if (password.Length < 8)
            return (false, null, "Password must be at least 8 characters");

        // Check if exists
        var existing = await _userStore.GetByUsernameAsync(username);
        if (existing != null)
            return (false, null, "Username already exists");

        existing = await _userStore.GetByEmailAsync(email);
        if (existing != null)
            return (false, null, "Email already exists");

        // Create user
        var (hash, salt) = _auth.HashPasswordWithSalt(password);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt
        };

        var created = await _userStore.CreateAsync(user);
        if (!created)
            return (false, null, "Failed to create user");

        _logger.Info($"User registered: {username}");
        return (true, user.Id, null);
    }

    // Login
    public async Task<(bool success, string? token, string? error)> LoginAsync(
        string usernameOrEmail,
        string password)
    {
        // Find user
        var user = await _userStore.GetByUsernameAsync(usernameOrEmail);
        if (user == null)
            user = await _userStore.GetByEmailAsync(usernameOrEmail);

        if (user == null)
            return (false, null, "Invalid credentials");

        if (!user.IsActive)
            return (false, null, "Account is disabled");

        // Verify password
        var valid = _auth.VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
        if (!valid)
            return (false, null, "Invalid credentials");

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userStore.UpdateAsync(user);

        // Generate token
        var token = _auth.GenerateToken(user.Id, new Dictionary<string, string>
        {
            ["username"] = user.Username,
            ["email"] = user.Email
        });

        _logger.Info($"User logged in: {user.Username}");
        return (true, token, null);
    }

    // Validate token and get user
    public async Task<User?> GetUserFromTokenAsync(string token)
    {
        var userId = _auth.GetUserIdFromToken(token);
        if (userId == null) return null;

        return await _userStore.GetByIdAsync(userId);
    }

    // Change password
    public async Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
    {
        var user = await _userStore.GetByIdAsync(userId);
        if (user == null) return false;

        // Verify old password
        var valid = _auth.VerifyPassword(oldPassword, user.PasswordHash, user.PasswordSalt);
        if (!valid) return false;

        // Set new password
        var (hash, salt) = _auth.HashPasswordWithSalt(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        return await _userStore.UpdateAsync(user);
    }
}

/// <summary>
/// Role-based authorization
/// </summary>
public class AuthorizationService
{
    public bool HasRole(User user, string role)
    {
        return user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasAnyRole(User user, params string[] roles)
    {
        return roles.Any(role => HasRole(user, role));
    }

    public bool HasAllRoles(User user, params string[] roles)
    {
        return roles.All(role => HasRole(user, role));
    }

    public void AddRole(User user, string role)
    {
        if (!HasRole(user, role))
        {
            user.Roles.Add(role);
        }
    }

    public void RemoveRole(User user, string role)
    {
        user.Roles.RemoveAll(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Example usage
/// </summary>
public class AuthExamples
{
    public static async Task Examples()
    {
        var auth = new SimpleAuth("your-secret-key-min-32-chars-long!");
        var userStore = new InMemoryUserStore();
        var authService = new AuthService(auth, userStore);

        // Register user
        var (regSuccess, userId, regError) = await authService.RegisterAsync(
            "john_doe",
            "john@example.com",
            "SecurePassword123!"
        );

        if (regSuccess)
        {
            Console.WriteLine($"User registered: {userId}");
        }

        // Login
        var (loginSuccess, token, loginError) = await authService.LoginAsync(
            "john_doe",
            "SecurePassword123!"
        );

        if (loginSuccess)
        {
            Console.WriteLine($"Login successful. Token: {token}");

            // Validate token
            var user = await authService.GetUserFromTokenAsync(token!);
            if (user != null)
            {
                Console.WriteLine($"Token is valid for user: {user.Username}");
            }
        }

        // Authorization
        var authz = new AuthorizationService();
        if (user != null)
        {
            authz.AddRole(user, "admin");
            authz.AddRole(user, "user");

            if (authz.HasRole(user, "admin"))
            {
                Console.WriteLine("User is admin");
            }
        }
    }
}