using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class OptionSetConfiguration : IEntityTypeConfiguration<OptionSet>
{
    public void Configure(EntityTypeBuilder<OptionSet> e)
    {
        e.ToTable("option_sets");
        e.HasKey(x => x.OptionSetId);
        e.Property(x => x.OptionSetId)
            .HasColumnName("option_set_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        e.Property(x => x.SetName).HasColumnName("set_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");   // ★ 新增

        // ★ 改为 partial unique index：软删的集合不占用 SetName
        e.HasIndex(x => new { x.EntityType, x.SetName })
            .IsUnique()
            .HasDatabaseName("uq_option_set")
            .HasFilter("is_deleted = false");
    }
}
