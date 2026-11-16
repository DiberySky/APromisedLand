using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class InodeEntityConfiguration : IEntityTypeConfiguration<InodeEntity>
{
    public void Configure(EntityTypeBuilder<InodeEntity> e)
    {
        e.ToTable("inode_entity");

        // 绑定是一等对象：主键为 binding_id，name 为用户命名的绑定名
        e.HasKey(x => x.BindingId);

        e.Property(x => x.BindingId).HasColumnName("binding_id")
            .HasMaxLength(36).IsRequired();
        e.Property(x => x.Name).HasColumnName("name")
            .HasMaxLength(200).IsRequired();
        e.Property(x => x.InodeId).HasColumnName("inode_id")
            .HasMaxLength(36).IsRequired();
        e.Property(x => x.EntityType).HasColumnName("entity_type")
            .HasMaxLength(100).IsRequired();
        e.Property(x => x.EntityId).HasColumnName("entity_id")
            .HasMaxLength(36).IsRequired();
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.AttachedAt).HasColumnName("attached_at");

        // 同一 iNode 同一类型下绑定名唯一
        e.HasIndex(x => new { x.InodeId, x.EntityType, x.Name })
            .IsUnique()
            .HasDatabaseName("ux_inode_entity_name");

        // 一条 EAV 数据只属于一条绑定
        e.HasIndex(x => new { x.EntityType, x.EntityId })
            .IsUnique()
            .HasDatabaseName("ux_inode_entity_type_entity");

        e.HasIndex(x => x.InodeId).HasDatabaseName("ix_inode_entity_inode");
    }
}
