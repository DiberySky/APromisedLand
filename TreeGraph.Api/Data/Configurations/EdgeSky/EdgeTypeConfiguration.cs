using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.SpaceSky.Entities;

namespace TreeGraph.Api.Data.Configurations.EdgeSky;

public class EdgeTypeConfiguration : IEntityTypeConfiguration<EdgeType>
{
    public void Configure(EntityTypeBuilder<EdgeType> e)
    {
        // TTL 列与时长成对出现（Nebula：TTL_COL 须配 TTL_DURATION）
        e.ToTable("edge_types", t => t.HasCheckConstraint(
            "ck_edge_types_ttl",
            "(ttl_col IS NULL) = (ttl_duration_seconds IS NULL)"));
        // Nebula 对齐：EdgeType 是空间级对象，(space_id, name) 复合主键，同名可跨空间
        e.HasKey(x => new { x.SpaceId, x.Name });
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.TtlCol).HasColumnName("ttl_col").HasMaxLength(200);
        e.Property(x => x.TtlDurationSeconds).HasColumnName("ttl_duration_seconds");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 空间 Restrict：空间内仍有边类型时禁止删空间（服务层先校验）
        e.HasOne<Space>()
            .WithMany()
            .HasForeignKey(x => x.SpaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_edge_types_space");
        // 内置边类型不在迁移/启动播种：由「关系设置 → 创建默认边（预置关系）」按需创建
    }
}
