using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.Data.Configurations.NodeEavSky;

public class CustomTableDefinitionConfiguration : IEntityTypeConfiguration<CustomTableDefinition>
{
    public void Configure(EntityTypeBuilder<CustomTableDefinition> e)
    {
        e.ToTable("custom_table_definitions");
        e.HasKey(x => x.TableDefinitionId);
        e.Property(x => x.TableDefinitionId)
            .HasColumnName("table_definition_id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.TableName).HasColumnName("table_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Version).HasColumnName("version");
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        e.Property(x => x.DisplayOrder).HasColumnName("display_order");
        e.Property(x => x.IsRecordTable).HasColumnName("is_record_table");
        e.Property(x => x.HasTimeColumn).HasColumnName("has_time_column");
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        e.HasIndex(x => new { x.TableName, x.Version })
            .IsUnique().HasDatabaseName("uq_custom_table");
    }
}
