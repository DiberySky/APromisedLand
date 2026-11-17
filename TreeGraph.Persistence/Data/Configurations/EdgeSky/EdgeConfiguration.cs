using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.EdgeSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.EdgeSky;

public class EdgeConfiguration : IEntityTypeConfiguration<Edge>
{
    public void Configure(EntityTypeBuilder<Edge> e)
    {
        e.ToTable("edges");
        // 五元组复合主键：<SpaceId, EdgeType, 起点 VID, Rank, 终点 VID>（Nebula 边唯一标识；边从属于空间）
        e.HasKey(x => new { x.SpaceId, x.EdgeType, x.SrcVid, x.Rank, x.DstVid });
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.EdgeType).HasColumnName("edge_type").HasMaxLength(100).IsRequired();
        e.Property(x => x.SrcVid).HasColumnName("src_vid").HasMaxLength(36).IsRequired();
        e.Property(x => x.DstVid).HasColumnName("dst_vid").HasMaxLength(36).IsRequired();
        e.Property(x => x.Rank).HasColumnName("rank");
        e.Property(x => x.Props).HasColumnName("props").HasColumnType("jsonb");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        e.HasIndex(x => x.SrcVid).HasDatabaseName("ix_edges_src");
        e.HasIndex(x => x.DstVid).HasDatabaseName("ix_edges_dst");
        e.HasIndex(x => x.Props).HasDatabaseName("ix_edges_props").HasMethod("GIN")
            .HasFilter("props IS NOT NULL");

        // 两端必须是 nodes 中的真实节点；删节点时级联删边（含结构派生 parent_of），
        // 避免 FK 阻塞树节点删除（图侧由 GraphSynchronizer 删顶点后自动移除关联边）
        e.HasOne<Node>()
            .WithMany()
            .HasForeignKey(x => x.SrcVid)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_edges_src_node");
        e.HasOne<Node>()
            .WithMany()
            .HasForeignKey(x => x.DstVid)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_edges_dst_node");

        // 边类型 Restrict：有实例时由服务层拒绝删类型（复合 FK 对齐空间级主键）
        e.HasOne<EdgeType>()
            .WithMany()
            .HasForeignKey(x => new { x.SpaceId, x.EdgeType })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_edges_edge_type");
    }
}
