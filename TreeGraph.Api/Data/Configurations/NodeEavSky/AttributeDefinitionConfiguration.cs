using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> e)
    {
        e.ToTable("attribute_catalog");

        e.HasKey(x => x.AttributeId);
        e.Property(x => x.AttributeId)
            .HasColumnName("attribute_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
        e.Property(x => x.IsRequired).HasColumnName("is_required");
        e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
        e.Property(x => x.IsSortable).HasColumnName("is_sortable");
        e.Property(x => x.Version).HasColumnName("version");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);

        e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");
        e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
        e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
        e.Property(x => x.RefTableDefinitionId).HasColumnName("ref_table_definition_id").HasMaxLength(36);
        e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 属性为实体类型私有：同一类型内属性名唯一，不同类型可有同名属性
        e.HasIndex(x => new { x.EntityType, x.AttributeName })
            .IsUnique()
            .HasDatabaseName("uq_attr_catalog");

        e.HasIndex(x => x.EntityType)
            .HasDatabaseName("ix_attr_catalog_entity");

        e.HasOne(x => x.RefCompositeType)
            .WithMany().HasForeignKey(x => x.RefCompositeTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.RefTableDefinition).WithMany()
            .HasForeignKey(x => x.RefTableDefinitionId).OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.RefOptionSet).WithMany()
            .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
    }
}
