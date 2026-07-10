using APromisedLand.Api.Projects.SeaweedFS.Models;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Projects.SeaweedFS.Data;

public class SeaweedFsDbContext(DbContextOptions<SeaweedFsDbContext> options)
    : DbContext(options)
{
    public DbSet<FileMetadata> Files { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileMetadata>()
            .Property(e => e.Metadata)
            .HasColumnType("jsonb");
    }
}