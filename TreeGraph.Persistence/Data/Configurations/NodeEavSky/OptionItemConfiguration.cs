using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class OptionItemConfiguration : IEntityTypeConfiguration<OptionItem>
{
    public void Configure(EntityTypeBuilder<OptionItem> e)
    {
        e.ToTable("option_items");
        e.HasKey(x => x.OptionItemId);
        e.Property(x => x.OptionItemId)
            .HasColumnName("option_item_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.OptionSetId).HasColumnName("option_set_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();
        e.Property(x => x.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.IsDefault).HasColumnName("is_default");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");

        e.HasIndex(x => new { x.OptionSetId, x.Value })
            .IsUnique().HasDatabaseName("uq_option_item_value");
        e.HasIndex(x => x.OptionSetId).HasDatabaseName("ix_option_items_set");

        // 不采用级联删除：选项非空时由应用层拒绝删除选项集
        e.HasOne(x => x.OptionSet).WithMany(s => s.Items)
            .HasForeignKey(x => x.OptionSetId).OnDelete(DeleteBehavior.Restrict);
    }
}
