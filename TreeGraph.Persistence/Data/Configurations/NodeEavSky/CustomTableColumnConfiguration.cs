using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class CustomTableColumnConfiguration : IEntityTypeConfiguration<CustomTableColumn>
{
    public void Configure(EntityTypeBuilder<CustomTableColumn> e)
    {
        e.ToTable("custom_table_columns");

        e.HasKey(x => x.ColumnId);
        e.Property(x => x.ColumnId)
            .HasColumnName("column_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.ColumnName).HasColumnName("column_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
        e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
        e.Property(x => x.IsRequired).HasColumnName("is_required");
        e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
        e.Property(x => x.IsSortable).HasColumnName("is_sortable");
        e.Property(x => x.IsUnique).HasColumnName("is_unique");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
        e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
        e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");

        e.HasIndex(x => new { x.TableDefinitionId, x.ColumnName })
            .IsUnique().HasDatabaseName("uq_custom_table_column");

        // 不采用级联删除：列非空时由应用层拒绝删除自定义表
        e.HasOne(x => x.Table).WithMany(t => t.Columns)
            .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.RefCompositeType).WithMany()
            .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
