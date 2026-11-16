using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.SpaceNodeSky.Entities;
using TreeGraph.Api.SpaceSky.Entities;

namespace TreeGraph.Api.Data.Configurations.SpaceNodeSky;

public class SpaceNodeConfiguration : IEntityTypeConfiguration<SpaceNode>
{
    public void Configure(EntityTypeBuilder<SpaceNode> e)
    {
        e.ToTable("space_nodes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36).IsRequired();
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 共享主键：SpaceNode.Id 即 nodes.id，身份/名称唯一真源在 nodes
        e.HasOne(x => x.Node)
            .WithOne()
            .HasForeignKey<SpaceNode>(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_space_nodes_node");

        // 归属空间；Restrict：空间内仍有节点时禁止删空间
        e.HasOne<Space>()
            .WithMany()
            .HasForeignKey(x => x.SpaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_space_nodes_space");

        e.HasIndex(x => x.SpaceId).HasDatabaseName("ix_space_nodes_space");
    }
}
