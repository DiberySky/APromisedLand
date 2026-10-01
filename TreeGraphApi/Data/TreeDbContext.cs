using Microsoft.EntityFrameworkCore;
using TreeGraphApi.Entities;

namespace TreeGraphApi.Data;

public class TreeDbContext : DbContext
{
    public DbSet<TreeNodeEntity> TreeNodes => Set<TreeNodeEntity>();

    public TreeDbContext(DbContextOptions<TreeDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 不再依赖 PostgreSQL ltree 扩展,纯标准 SQL 实现
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TreeDbContext).Assembly);
    }
}
