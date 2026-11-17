using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class CustomTableRowConfiguration : IEntityTypeConfiguration<CustomTableRow>
{
    public void Configure(EntityTypeBuilder<CustomTableRow> e)
    {
        e.ToTable("custom_table_rows");
        e.HasKey(x => x.RowId);
        e.Property(x => x.RowId)
            .HasColumnName("row_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.ParentEntityId).HasColumnName("parent_entity_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.ParentEntityType).HasColumnName("parent_entity_type").HasMaxLength(100).IsRequired();
        e.Property(x => x.RowData).HasColumnName("row_data").HasColumnType("jsonb").IsRequired();
        e.Property(x => x.RowOrder).HasColumnName("row_order");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        e.HasIndex(x => new { x.ParentEntityType, x.ParentEntityId, x.AttributeId })
            .HasDatabaseName("ix_ctr_parent");
        e.HasIndex(x => x.TableDefinitionId).HasDatabaseName("ix_ctr_table");
        e.HasIndex(x => x.RowData).HasDatabaseName("ix_ctr_rowdata").HasMethod("GIN");
        e.HasIndex(x => new { x.ParentEntityId, x.AttributeId, x.RowOrder })
            .HasDatabaseName("ix_ctr_order");

        // 不采用级联删除：属性有实例数据（含表行）时由应用层拒绝删除该属性
        e.HasOne(x => x.Attribute).WithMany()
            .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Table).WithMany()
            .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
