using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.SpaceNodeSky.Entities;
using TreeGraph.Api.SpaceSky.Entities;

namespace TreeGraph.Api.Data.Configurations.SpaceNodeSky;

public class TagProjectionConfiguration : IEntityTypeConfiguration<TagProjection>
{
    public void Configure(EntityTypeBuilder<TagProjection> e)
    {
        e.ToTable("tag_projections");
        e.HasKey(x => new { x.SpaceId, x.TagName });
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.TagName).HasColumnName("tag_name").HasMaxLength(100).IsRequired();
        e.Property(x => x.SourceAttributeId)
            .HasColumnName("source_attribute_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.SourceEntityType)
            .HasColumnName("source_entity_type").HasMaxLength(100).IsRequired();
        e.Property(x => x.SchemaSnapshot)
            .HasColumnName("schema_snapshot").HasColumnType("jsonb").IsRequired();
        e.Property(x => x.Version).HasColumnName("version").IsRequired();
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();
        e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        e.HasOne<Space>()
            .WithMany()
            .HasForeignKey(x => x.SpaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tag_projections_space");

        e.HasIndex(x => x.SourceAttributeId)
            .HasDatabaseName("ix_tag_projections_source_attribute");
    }
}
