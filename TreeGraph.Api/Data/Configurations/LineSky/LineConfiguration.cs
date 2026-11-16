using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.Data.Configurations.LineSky;

public class LineConfiguration : IEntityTypeConfiguration<Line>
{
    public void Configure(EntityTypeBuilder<Line> e)
    {
        e.ToTable("lines");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id)
            .HasColumnName("id")
            .HasMaxLength(36).IsRequired()
            .HasDefaultValueSql("gen_random_uuid()::text")
            .HasSentinel("");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
        e.Property(x => x.CreatedAt).HasColumnName("created_at");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // 线路从属于图空间（结构段边 line_segment 写入该空间）；Restrict：有线路时禁止删空间
        e.HasOne<Space>()
            .WithMany()
            .HasForeignKey(x => x.SpaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lines_space");
        e.HasIndex(x => x.SpaceId).HasDatabaseName("ix_lines_space");
    }
}
