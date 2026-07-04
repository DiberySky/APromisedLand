using FileTransService.Models;
using Microsoft.EntityFrameworkCore;

namespace FileTransService.Data;

public class FileTransDbContext(DbContextOptions<FileTransDbContext> options)
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