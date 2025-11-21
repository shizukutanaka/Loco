using Microsoft.EntityFrameworkCore;
using Loco.Core.Data;

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
    }
}
