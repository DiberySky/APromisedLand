using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class AttributeAuditLogConfiguration : IEntityTypeConfiguration<AttributeAuditLog>
{
    public void Configure(EntityTypeBuilder<AttributeAuditLog> e)
    {
        // 审计只增不减，普通表 + 两个索引即可（此前按月分区，仅为归档便利，已取消）。
        e.ToTable("attribute_audit_log");
        e.HasKey(x => x.AuditId);
        e.Property(x => x.AuditId)
            .HasColumnName("audit_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.OldValue).HasColumnName("old_value");
        e.Property(x => x.NewValue).HasColumnName("new_value");
        e.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(20).IsRequired();
        e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(200).IsRequired();
        e.Property(x => x.ChangedAt).HasColumnName("changed_at");
        e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        e.Property(x => x.ClientIp).HasColumnName("client_ip").HasMaxLength(50);

        e.HasIndex(x => new { x.EntityId, x.ChangedAt })
            .HasDatabaseName("ix_audit_entity")
            .IsDescending(false, true);
        e.HasIndex(x => x.ChangedAt).HasDatabaseName("ix_audit_time");
    }
}
