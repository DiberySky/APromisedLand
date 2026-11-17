using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Persistence.NodeEavSky.Entities;

namespace TreeGraph.Persistence.Data.Configurations.NodeEavSky;

public class CompositeTypeDefinitionConfiguration : IEntityTypeConfiguration<CompositeTypeDefinition>
{
    public void Configure(EntityTypeBuilder<CompositeTypeDefinition> e)
    {
        e.ToTable("composite_type_definitions");
        e.HasKey(x => x.CompositeTypeId);
        e.Property(x => x.CompositeTypeId)
            .HasColumnName("composite_type_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Version).HasColumnName("version");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 组合类型全局化：唯一键不再含实体类型维度
        e.HasIndex(x => new { x.TypeName, x.Version })
            .IsUnique().HasDatabaseName("uq_composite_type");
    }
}
