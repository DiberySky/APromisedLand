// APromisedLand.Api.Data/TreeDbContext.cs

using APromisedLand.Api.Models;
using Microsoft.EntityFrameworkCore;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Api.Data;

public class DiberyDbContext(DbContextOptions<DiberyDbContext> options) : DbContext(options)
{
    public DbSet<CategoryTree> CategoryTrees { get; set; }
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CategoryTree>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentId);
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict); // 防止意外级联删除
        });

        // 使用 SeedData() 添加种子数据（纯扁平数据，不包含导航属性）
        modelBuilder.Entity<CategoryTree>().HasData(CategoryTree.SeedData());

        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("UnitsOfMeasure");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        
        modelBuilder.Entity<UnitOfMeasure>().HasData(UnitOfMeasure.SeedData());
        
        base.OnModelCreating(modelBuilder);
    }
}