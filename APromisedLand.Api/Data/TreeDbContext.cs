// APromisedLand.Api.Data/TreeDbContext.cs
using Microsoft.EntityFrameworkCore;
using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.Api.Data;

public class TreeDbContext(DbContextOptions<TreeDbContext> options) : DbContext(options)
{
    public DbSet<CategoryTree> CategoryTrees { get; set; }

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

        base.OnModelCreating(modelBuilder);
    }
}