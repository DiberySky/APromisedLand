using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraphApi.Entities;

namespace TreeGraphApi.Data.Configurations;

public class TreeNodeEntityConfiguration : IEntityTypeConfiguration<TreeNodeEntity>
{
    public void Configure(EntityTypeBuilder<TreeNodeEntity> builder)
    {
        builder.ToTable("tree_nodes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(36).IsRequired();
        builder.Property(x => x.ParentId).HasMaxLength(36);
        builder.Property(x => x.Text).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(200);
        builder.Property(x => x.NodeType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SortOrder).HasDefaultValue(0);

        // Path 用普通字符串列,长度足够承载深层级路径
        builder.Property(x => x.Path).HasMaxLength(2048).IsRequired();

        // 优化 #7:乐观并发,映射到 PostgreSQL xmin 系统列
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Parent)
               .WithMany(x => x.Children)
               .HasForeignKey(x => x.ParentId)
               .OnDelete(DeleteBehavior.Restrict);

        // 索引:ParentId 用于查子节点;Path 用 B-tree(支持 LIKE 前缀匹配)
        builder.HasIndex(x => x.ParentId).HasDatabaseName("ix_tree_nodes_parent_id");
        builder.HasIndex(x => x.Path).HasDatabaseName("ix_tree_nodes_path");
    }
}
