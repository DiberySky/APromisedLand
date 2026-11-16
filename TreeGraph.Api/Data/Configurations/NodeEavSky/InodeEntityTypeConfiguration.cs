using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class InodeEntityTypeConfiguration : IEntityTypeConfiguration<InodeEntityType>
{
    public void Configure(EntityTypeBuilder<InodeEntityType> e)
    {
        e.ToTable("inode_entitytype");

        // 复合主键：每 (inode, entityType) 只 1 条
        e.HasKey(x => new { x.InodeId, x.EntityType });

        e.Property(x => x.InodeId).HasColumnName("inode_id")
            .HasMaxLength(36).IsRequired();
        e.Property(x => x.EntityType).HasColumnName("entity_type")
            .HasMaxLength(100).IsRequired();
        e.Property(x => x.AttachedAt).HasColumnName("attached_at");

        e.HasIndex(x => x.InodeId).HasDatabaseName("ix_inode_et_inode");
        e.HasIndex(x => x.EntityType).HasDatabaseName("ix_inode_et_type");
    }
}
