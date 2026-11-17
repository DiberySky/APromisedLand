using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class LocationAttributeValueConfiguration : IEntityTypeConfiguration<LocationAttributeValue>
{
    public void Configure(EntityTypeBuilder<LocationAttributeValue> e)
    {
        e.ToTable("location_attribute_values");
        e.HasKey(x => x.ValueId);
        e.Property(x => x.ValueId)
            .HasColumnName("value_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();

        e.Property(x => x.Latitude).HasColumnName("latitude");
        e.Property(x => x.Longitude).HasColumnName("longitude");
        e.Property(x => x.Label).HasColumnName("label").HasMaxLength(200);
        e.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
        e.Property(x => x.CoordinateSystem)
            .HasColumnName("coordinate_system").HasMaxLength(20)
            .HasDefaultValue("gcj02").IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 一个实体的一个位置属性最多一个坐标点
        e.HasIndex(x => new { x.EntityId, x.AttributeId })
            .IsUnique().HasDatabaseName("uq_lav_entity_attr");

        e.HasIndex(x => x.EntityId)
            .HasDatabaseName("ix_lav_entity");

        // 冲突检测：加速 max(UpdatedAt)（与 attribute_values 对齐）
        e.HasIndex(x => new { x.EntityId, x.UpdatedAt })
            .HasDatabaseName("ix_lav_entity_updated")
            .IsDescending(false, true);

        e.HasOne(x => x.Attribute).WithMany()
            .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
    }
}
