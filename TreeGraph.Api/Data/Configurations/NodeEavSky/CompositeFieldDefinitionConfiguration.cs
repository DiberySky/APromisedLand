using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class CompositeFieldDefinitionConfiguration : IEntityTypeConfiguration<CompositeFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CompositeFieldDefinition> e)
    {
        // CHECK：single_choice 才能绑选项集
        e.ToTable("composite_field_definitions", t =>
        {
            // ★ #8：single_choice 才能绑选项集
            t.HasCheckConstraint(
                "ck_composite_field_single_choice_optionset",
                "data_type = 'single_choice' OR ref_option_set_id IS NULL");
        });

        e.HasKey(x => x.FieldId);
        e.Property(x => x.FieldId)
            .HasColumnName("field_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.CompositeTypeId).HasColumnName("composite_type_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
        e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);

        // ★ #8：选项集外键列
        e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);

        e.Property(x => x.IsArray).HasColumnName("is_array");
        e.Property(x => x.IsRequired).HasColumnName("is_required");
        e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
        e.Property(x => x.IsSortable).HasColumnName("is_sortable");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
        e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
        e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");

        e.HasIndex(x => new { x.CompositeTypeId, x.FieldName })
            .IsUnique().HasDatabaseName("uq_composite_field");

        // 不采用级联删除：组合字段非空时由应用层拒绝删除组合类型
        e.HasOne(x => x.CompositeType).WithMany(t => t.Fields)
            .HasForeignKey(x => x.CompositeTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.RefCompositeType).WithMany()
            .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);

        // ★ #8：选项集外键（Restrict）
        e.HasOne(x => x.RefOptionSet).WithMany()
            .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
    }
}
