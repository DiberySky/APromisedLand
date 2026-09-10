using APromisedLand.Api.MafRag.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace APromisedLand.Api.Data;

public class MafRagContext(DbContextOptions<MafRagContext> options) : DbContext(options)
{
    public DbSet<DocumentMetadataEntity> DocumentMetadata { get; set; }
    public DbSet<DocumentAuditEntity> DocumentAudits { get; set; }
    public DbSet<IndexTaskEntity> IndexTasks { get; set; }
    public DbSet<DomainEventEntity> DomainEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // UTC 转换（与现有应用保持一致）
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) ||
                    property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTimeOffset, DateTimeOffset>(
                            v => v.ToUniversalTime(),
                            v => v));
                }
            }
        }

        // DocumentMetadata
        modelBuilder.Entity<DocumentMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocId, e.Version, e.Tenant }).IsUnique();
            entity.HasIndex(e => new { e.Tenant, e.DocId });
            entity.Property(e => e.Status).HasDefaultValue("active");
        });

        // DocumentAudit
        modelBuilder.Entity<DocumentAuditEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DocId, e.Tenant });
            entity.HasOne(e => e.Document)
                  .WithMany(d => d.Audits)
                  .HasForeignKey(e => e.DocId)
                  .HasPrincipalKey(d => d.DocId);
        });

        // IndexTask
        modelBuilder.Entity<IndexTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        // DomainEvent
        modelBuilder.Entity<DomainEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StreamId, e.Version }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}