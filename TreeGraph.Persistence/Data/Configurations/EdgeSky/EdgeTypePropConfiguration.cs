using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.EdgeSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.EdgeSky;

public class EdgeTypePropConfiguration : IEntityTypeConfiguration<EdgeTypeProp>
{
    public void Configure(EntityTypeBuilder<EdgeTypeProp> e)
    {
        // Nebula 约束：NOT NULL 必须配 DEFAULT
        e.ToTable("edge_type_props", t => t.HasCheckConstraint(
            "ck_edge_type_props_notnull_default",
            "nullable OR default_value IS NOT NULL"));
        e.HasKey(x => new { x.SpaceId, x.EdgeTypeName, x.PropName });
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.EdgeTypeName).HasColumnName("edge_type_name").HasMaxLength(100).IsRequired();
        e.Property(x => x.PropName).HasColumnName("prop_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
        e.Property(x => x.Nullable).HasColumnName("nullable");
        e.Property(x => x.DefaultValue).HasColumnName("default_value").HasColumnType("jsonb");
        e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");

        // 属性定义是类型的内在组成：删类型前由服务层先删属性行（Restrict 兜底）
        e.HasOne(x => x.EdgeType).WithMany(t => t.Props)
            .HasForeignKey(x => new { x.SpaceId, x.EdgeTypeName }).OnDelete(DeleteBehavior.Restrict);
    }
}
