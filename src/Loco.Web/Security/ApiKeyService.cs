using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Loco.Web.Security;

public interface IApiKeyService
{
    Task<ApiKey?> ValidateApiKeyAsync(string apiKey);
    Task<ApiKey> GenerateApiKeyAsync(string userId, string name, string[] scopes);
    Task RevokeApiKeyAsync(string apiKey);
    Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId);
    Task UpdateLastUsedAsync(string apiKey);
}

public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string HashedKey { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public string? IpWhitelist { get; set; }
}

public class ApiKeyService : IApiKeyService
{
    private readonly DbContext _dbContext;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(DbContext dbContext, ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiKey?> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var hashedKey = HashApiKey(apiKey);
        
        var key = await _dbContext.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.HashedKey == hashedKey && k.IsActive);

        if (key == null)
        {
            _logger.LogWarning("Invalid API key attempted: {ApiKeyPrefix}", apiKey.Substring(0, Math.Min(8, apiKey.Length)));
            return null;
        }

        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
        {
            key.IsActive = false;
            await _dbContext.SaveChangesAsync();
            _logger.LogWarning("Expired API key used: {ApiKeyId}", key.Id);
            return null;
        }

        return key;
    }

    public async Task<ApiKey> GenerateApiKeyAsync(string userId, string name, string[] scopes)
    {
        var apiKey = GenerateSecureApiKey();
        var hashedKey = HashApiKey(apiKey);

        var key = new ApiKey
        {
            UserId = userId,
            Name = name,
            Key = apiKey,
            HashedKey = hashedKey,
            Scopes = scopes,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Set<ApiKey>().Add(key);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("API key generated for user {UserId}: {KeyName}", userId, name);

        return key;
    }

    public async Task RevokeApiKeyAsync(string apiKey)
    {
        var hashedKey = HashApiKey(apiKey);
        var key = await _dbContext.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.HashedKey == hashedKey);

        if (key != null)
        {
            key.IsActive = false;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("API key revoked: {ApiKeyId}", key.Id);
        }
    }

    public async Task<IEnumerable<ApiKey>> GetUserApiKeysAsync(string userId)
    {
        return await _dbContext.Set<ApiKey>()
            .Where(k => k.UserId == userId)
            .Select(k => new ApiKey
            {
                Id = k.Id,
                Name = k.Name,
                Scopes = k.Scopes,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt,
                ExpiresAt = k.ExpiresAt,
                IsActive = k.IsActive,
                UsageCount = k.UsageCount,
                RateLimitPerMinute = k.RateLimitPerMinute
            })
            .ToListAsync();
    }

    public async Task UpdateLastUsedAsync(string apiKey)
    {
        var hashedKey = HashApiKey(apiKey);
        var key = await _dbContext.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.HashedKey == hashedKey);

        if (key != null)
        {
            key.LastUsedAt = DateTime.UtcNow;
            key.UsageCount++;
            await _dbContext.SaveChangesAsync();
        }
    }

    private string GenerateSecureApiKey()
    {
        const string prefix = "loco_";
        const int keyLength = 32;
        
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[keyLength];
        rng.GetBytes(bytes);
        
        var key = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
        
        return $"{prefix}{key}";
    }

    private string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
