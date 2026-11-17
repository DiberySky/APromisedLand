using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class AttributeValueConfiguration : IEntityTypeConfiguration<AttributeValue>
{
    public void Configure(EntityTypeBuilder<AttributeValue> e)
    {
        e.ToTable("attribute_values");
        e.HasKey(x => x.ValueId);
        e.Property(x => x.ValueId)
            .HasColumnName("value_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.ValueString).HasColumnName("value_string").HasMaxLength(2000);
        e.Property(x => x.ValueInt).HasColumnName("value_int");

        e.Property(x => x.ValueDecimal)
            .HasColumnName("value_decimal")
            .HasPrecision(38, 15);

        e.Property(x => x.ValueBool).HasColumnName("value_bool");
        e.Property(x => x.ValueDatetime).HasColumnName("value_datetime").HasColumnType("timestamptz");
        e.Property(x => x.ValueDateOnly).HasColumnName("value_dateonly").HasColumnType("date");
        e.Property(x => x.ValueTime).HasColumnName("value_time").HasColumnType("time(0)");
        e.Property(x => x.ValueFileMeta).HasColumnName("value_file_meta").HasColumnType("jsonb");
        e.Property(x => x.ValueJsonb).HasColumnName("value_jsonb").HasColumnType("jsonb");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        e.HasIndex(x => new { x.EntityId, x.AttributeId })
            .IsUnique().HasDatabaseName("uq_av_entity_attr");

        e.HasIndex(x => x.EntityId)
            .HasDatabaseName("ix_av_entity");

        // ★ #3 冲突检测：加速 max(UpdatedAt) 查询
        e.HasIndex(x => new { x.EntityId, x.UpdatedAt })
            .HasDatabaseName("ix_av_entity_updated")
            .IsDescending(false, true);

        e.HasIndex(x => new { x.AttributeId, x.ValueInt })
            .HasDatabaseName("ix_av_attr_int").HasFilter("value_int IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueDecimal })
            .HasDatabaseName("ix_av_attr_decimal").HasFilter("value_decimal IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueString })
            .HasDatabaseName("ix_av_attr_string").HasFilter("value_string IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueBool })
            .HasDatabaseName("ix_av_attr_bool").HasFilter("value_bool IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueDatetime })
            .HasDatabaseName("ix_av_attr_datetime").HasFilter("value_datetime IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueDateOnly })
            .HasDatabaseName("ix_av_attr_dateonly").HasFilter("value_dateonly IS NOT NULL");
        e.HasIndex(x => new { x.AttributeId, x.ValueTime })
            .HasDatabaseName("ix_av_attr_time").HasFilter("value_time IS NOT NULL");

        e.HasIndex(x => x.ValueJsonb)
            .HasDatabaseName("ix_av_jsonb").HasMethod("GIN")
            .HasFilter("value_jsonb IS NOT NULL");
        e.HasIndex(x => x.ValueFileMeta)
            .HasDatabaseName("ix_av_file_meta").HasMethod("GIN")
            .HasFilter("value_file_meta IS NOT NULL");

        e.HasOne(x => x.Attribute).WithMany()
            .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
    }
}
