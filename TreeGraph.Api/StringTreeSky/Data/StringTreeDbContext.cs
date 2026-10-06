using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.StringTreeSky.Entities;

namespace TreeGraph.Api.StringTreeSky.Data;

public class StringTreeDbContext : DbContext
{
    public StringTreeDbContext(DbContextOptions<StringTreeDbContext> options) : base(options) { }

    public DbSet<StringNodeEntity> StringNodes => Set<StringNodeEntity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        var e = mb.Entity<StringNodeEntity>();
        e.ToTable("StringTreeNodes");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1024);

        e.HasOne(x => x.Parent)
         .WithMany(x => x.Children)
         .HasForeignKey(x => x.ParentId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.ParentId);
        e.HasIndex(x => new { x.ParentId, x.SortOrder });
    }
}
