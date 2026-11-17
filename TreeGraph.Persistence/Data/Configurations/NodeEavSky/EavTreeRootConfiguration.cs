using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class EavTreeRootConfiguration : IEntityTypeConfiguration<EavTreeRoot>
{
    public void Configure(EntityTypeBuilder<EavTreeRoot> e)
    {
        e.ToTable("eav_tree_roots");
        e.HasKey(x => x.Id);
        // 共享主键：id 即 rootNode.Id（nodes.id / eav_trees.id）
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.BindingId).HasColumnName("binding_id").HasMaxLength(36);
        e.Property(x => x.CreatedAt).HasColumnName("created_at");

        // 删除绑定须先经 DeleteEntityAsync 守卫清理映射与根节点，Restrict 防绕过
        e.HasOne(x => x.Binding)
            .WithMany()
            .HasForeignKey(x => x.BindingId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.EntityId, x.AttributeId })
            .IsUnique().HasDatabaseName("uq_etr_entity_attr");

        e.HasIndex(x => x.BindingId).HasDatabaseName("ix_etr_binding");
    }
}
