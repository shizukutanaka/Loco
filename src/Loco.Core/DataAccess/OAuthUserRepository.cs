// Phase 3: OAuth 2.0 User Repository
// Persistent user management with EF Core

using Microsoft.EntityFrameworkCore;
using Loco.Core.Models;

namespace Loco.Core.DataAccess;

/// <summary>
/// Repository for OAuth 2.0 User management
/// Uses EF Core for write operations with ACID guarantees
/// </summary>
public interface IOAuthUserRepository
{
    Task<OAuthUserEntity?> GetByIdAsync(string userId, CancellationToken ct = default);
    Task<OAuthUserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<OAuthUserEntity?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<OAuthUserEntity> CreateAsync(OAuthUserEntity user, CancellationToken ct = default);
    Task<OAuthUserEntity> UpdateAsync(OAuthUserEntity user, CancellationToken ct = default);
    Task<bool> DeleteAsync(string userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<OAuthUserEntity>> GetActiveUsersAsync(CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of OAuth User repository
/// </summary>
public class OAuthUserRepository : IOAuthUserRepository
{
    private readonly LocoDbContext _context;
    private readonly ILogger<OAuthUserRepository> _logger;

    public OAuthUserRepository(LocoDbContext context, ILogger<OAuthUserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OAuthUserEntity?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
            throw;
        }
    }

    public async Task<OAuthUserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username && u.DeletedAt == null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by username: {Username}", username);
            throw;
        }
    }

    public async Task<OAuthUserEntity?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email: {Email}", email);
            throw;
        }
    }

    public async Task<OAuthUserEntity> CreateAsync(OAuthUserEntity user, CancellationToken ct = default)
    {
        try
        {
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            _context.OAuthUsers.Add(user);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User created: {UserId} ({Username})", user.Id, user.Username);
            return user;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint") ?? false)
        {
            _logger.LogWarning("User creation failed - duplicate username/email: {Username}", user.Username);
            throw new InvalidOperationException("Username or email already exists", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", user.Username);
            throw;
        }
    }

    public async Task<OAuthUserEntity> UpdateAsync(OAuthUserEntity user, CancellationToken ct = default)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;

            _context.OAuthUsers.Update(user);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User updated: {UserId}", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var user = await _context.OAuthUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return false;

            // Soft delete
            user.DeletedAt = DateTime.UtcNow;
            _context.OAuthUsers.Update(user);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User deleted (soft): {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthUsers
                .AsNoTracking()
                .AnyAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user existence: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<OAuthUserEntity>> GetActiveUsersAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.OAuthUsers
                .AsNoTracking()
                .Where(u => u.IsActive && u.DeletedAt == null)
                .OrderByDescending(u => u.LastLoginAt)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active users");
            throw;
        }
    }
}
