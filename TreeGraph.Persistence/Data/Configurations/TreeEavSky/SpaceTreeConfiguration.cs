using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.TreeEavSky;

public class SpaceTreeConfiguration : IEntityTypeConfiguration<SpaceTree>
{
    public void Configure(EntityTypeBuilder<SpaceTree> e)
    {
        e.ToTable("space_trees");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
        e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);
        e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
        e.Property(x => x.SortOrder).HasColumnName("sort_order");
        e.Property(x => x.HasChildren).HasColumnName("has_children");

        e.HasOne(x => x.Parent)
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // 共享主键：宿主 Id 即 nodes.id，名称唯一真源在 nodes（R2 Name 收编）
        e.HasOne(x => x.Node)
            .WithOne()
            .HasForeignKey<SpaceTree>(x => x.Id)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.ParentId).HasDatabaseName("ix_space_trees_parent");
    }
}
