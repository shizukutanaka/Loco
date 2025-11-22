using Microsoft.EntityFrameworkCore;
using Loco.Core.Data;
using Loco.Core.Models;
using Loco.Core.Workflows.DurableExecution;

namespace Loco.Core.DataAccess;

/// <summary>
/// Entity Framework Core DbContext for Loco workflow engine (Phase 2)
/// Provides write operations with ACID guarantees
/// Read operations use Dapper for optimal performance
/// </summary>
public class LocoDbContext : DbContext
{
    public LocoDbContext(DbContextOptions<LocoDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Workflows table
    /// </summary>
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();

    /// <summary>
    /// Execution history table
    /// </summary>
    public DbSet<ExecutionHistoryEntity> ExecutionHistories => Set<ExecutionHistoryEntity>();

    /// <summary>
    /// OAuth 2.0 Users table
    /// </summary>
    public DbSet<OAuthUserEntity> OAuthUsers => Set<OAuthUserEntity>();

    /// <summary>
    /// OAuth 2.0 Clients table
    /// </summary>
    public DbSet<OAuthClientEntity> OAuthClients => Set<OAuthClientEntity>();

    /// <summary>
    /// OAuth 2.0 Authorization Codes table
    /// </summary>
    public DbSet<OAuthAuthorizationCodeEntity> OAuthAuthorizationCodes => Set<OAuthAuthorizationCodeEntity>();

    /// <summary>
    /// OAuth 2.0 Refresh Tokens table
    /// </summary>
    public DbSet<OAuthRefreshTokenEntity> OAuthRefreshTokens => Set<OAuthRefreshTokenEntity>();

    /// <summary>
    /// OAuth 2.0 Scopes table
    /// </summary>
    public DbSet<OAuthScopeEntity> OAuthScopes => Set<OAuthScopeEntity>();

    /// <summary>
    /// Workflow Execution Events table (Event Sourcing)
    /// </summary>
    public DbSet<WorkflowExecutionEventEntity> WorkflowExecutionEvents => Set<WorkflowExecutionEventEntity>();

    /// <summary>
    /// Workflow Execution Snapshots table (Event Sourcing Optimization)
    /// </summary>
    public DbSet<WorkflowExecutionSnapshot> WorkflowExecutionSnapshots => Set<WorkflowExecutionSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Workflow entity
        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Definition).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Version).HasDefaultValue(1);

            // Indexes for common queries
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure ExecutionHistory entity
        modelBuilder.Entity<ExecutionHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.WorkflowId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.Result).HasColumnType("TEXT");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.Parameters).HasColumnType("TEXT");

            // Indexes for common queries
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt).IsDescending();
        });

        // Configure OAuthUser entity
        modelBuilder.Entity<OAuthUserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Roles).HasMaxLength(500);
            entity.Property(e => e.Metadata).HasColumnType("TEXT");

            // Unique constraints
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique().Filter("[Email] IS NOT NULL");

            // Indexes for common queries
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.LastLoginAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure OAuthClient entity
        modelBuilder.Entity<OAuthClientEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.SecretHash).HasMaxLength(500);
            entity.Property(e => e.GrantTypes).HasMaxLength(500);
            entity.Property(e => e.RedirectUris).HasColumnType("TEXT");
            entity.Property(e => e.Scopes).HasMaxLength(1000);
            entity.Property(e => e.PkceRequirement).HasMaxLength(20);

            // Indexes for common queries
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure OAuthAuthorizationCode entity
        modelBuilder.Entity<OAuthAuthorizationCodeEntity>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(255);
            entity.Property(e => e.ClientId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.RedirectUri).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Scopes).HasMaxLength(1000);
            entity.Property(e => e.CodeChallenge).HasMaxLength(128);
            entity.Property(e => e.CodeChallengeMethod).HasMaxLength(10);
            entity.Property(e => e.Nonce).HasMaxLength(255);

            // Indexes for common queries
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Configure OAuthRefreshToken entity
        modelBuilder.Entity<OAuthRefreshTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Token);
            entity.Property(e => e.Token).HasMaxLength(255);
            entity.Property(e => e.ClientId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Scopes).HasMaxLength(1000);

            // Indexes for common queries
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.IsRevoked);
        });

        // Configure OAuthScope entity
        modelBuilder.Entity<OAuthScopeEntity>(entity =>
        {
            entity.HasKey(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Configure WorkflowExecutionEvent entity (Event Sourcing)
        modelBuilder.Entity<WorkflowExecutionEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.ExecutionId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.WorkflowId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EventData).HasColumnType("TEXT");
            entity.Property(e => e.CorrelationId).HasMaxLength(255);
            entity.Property(e => e.UserId).HasMaxLength(255);

            // Indexes for event sourcing queries
            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => new { e.ExecutionId, e.SequenceNumber }).IsUnique();
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.IsCommitted);
        });

        // Configure WorkflowExecutionSnapshot entity
        modelBuilder.Entity<WorkflowExecutionSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(255);
            entity.Property(e => e.ExecutionId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.State).HasColumnType("TEXT");

            // Indexes for snapshot queries
            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => new { e.ExecutionId, e.SequenceNumber }).IsUnique();
            entity.HasIndex(e => e.CreatedAt).IsDescending();
        });
    }
}
