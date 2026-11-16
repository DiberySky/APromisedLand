using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class EntityTypeDefinitionConfiguration : IEntityTypeConfiguration<EntityTypeDefinition>
{
    public void Configure(EntityTypeBuilder<EntityTypeDefinition> e)
    {
        e.ToTable("entity_type_catalog");
        e.HasKey(x => x.EntityTypeId);
        e.Property(x => x.EntityTypeId)
            .HasColumnName("entity_type_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(100).IsRequired();
        e.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(200).IsRequired();
        e.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // partial unique：软删后同名可重建
        e.HasIndex(x => x.EntityType)
            .IsUnique()
            .HasDatabaseName("uq_entity_type")
            .HasFilter("is_deleted = false");
    }
}
