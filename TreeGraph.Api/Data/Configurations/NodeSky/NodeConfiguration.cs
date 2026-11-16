using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeSky;

public class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> e)
    {
        e.ToTable("nodes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id)
            .HasColumnName("id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        // 宿主类型名：一期恒为 "Node"；重构期各宿主写入自己的类型名（spec §8.2）
        e.Property(x => x.HostType).HasColumnName("host_type").HasMaxLength(16).IsRequired()
            .HasDefaultValue("Node");
        // 可空：边注册进 nodes 时名字为派生物（from名 → to名），存 null（spec §8.6 R3）
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        // R7 身份锚点：仅 LineNode 必填，恒 = 所属 Line 的 nodes.id（自引用 Restrict）
        e.Property(x => x.LineId).HasColumnName("line_id").HasMaxLength(36);
        e.HasOne<Node>()
            .WithMany()
            .HasForeignKey(x => x.LineId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_nodes_line_id");
        e.HasIndex(x => x.LineId).HasDatabaseName("ix_nodes_line_id");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // R2 Name 收编：宿主树种子对应的 nodes 注册表行（名称唯一真源）
        e.HasData(
            UnitTree.SeedNodes()
                .Concat(CategoryTree.SeedNodes())
                .Concat(KnowledgeTree.SeedNodes())
                .ToList());
    }
}
