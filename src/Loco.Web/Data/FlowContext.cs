using Loco.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Loco.Web.Data;

public class FlowContext : DbContext
{
    public FlowContext(DbContextOptions<FlowContext> options) : base(options) { }

    public DbSet<FlowDefinition> Flows { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var jsonSerializerOptions = new JsonSerializerOptions();

        modelBuilder.Entity<FlowDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Triggers)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<TriggerDefinition>>(v, jsonSerializerOptions) ?? new List<TriggerDefinition>());

            entity.Property(e => e.Conditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<ConditionDefinition>>(v, jsonSerializerOptions) ?? new List<ConditionDefinition>());

            entity.Property(e => e.Actions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<List<ActionDefinition>>(v, jsonSerializerOptions) ?? new List<ActionDefinition>());

            entity.Property(e => e.Variables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, jsonSerializerOptions) ?? new Dictionary<string, object>());

            entity.Property(e => e.Permissions)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                      v => JsonSerializer.Deserialize<PermissionSet>(v, jsonSerializerOptions) ?? new PermissionSet());

            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonSerializerOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, jsonSerializerOptions) ?? new Dictionary<string, object>());
        });
    }
}
