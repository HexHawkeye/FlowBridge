using FlowBridge.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlowBridge.Infrastructure.Data;

public sealed class FlowBridgeDbContext(DbContextOptions<FlowBridgeDbContext> options) : DbContext(options)
{
    public DbSet<IntegrationDefinition> Integrations => Set<IntegrationDefinition>();
    public DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntegrationDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.VariablesJson).IsRequired();
        });

        modelBuilder.Entity<ExecutionRecord>()
            .HasOne(x => x.IntegrationDefinition)
            .WithMany(x => x.Executions)
            .HasForeignKey(x => x.IntegrationDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
