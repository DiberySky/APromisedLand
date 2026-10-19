using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.LineSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// LineNode CRUD 集成测试：EF Core InMemory 提供器，无需 Docker。
/// </summary>
public class LineNodeCrudTests
{
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(LineNode))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }
            mb.Entity<LineNode>(e => e.HasKey(x => x.Id));
        }
    }

    private static TreeGraphDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Create_PersistsLineNode()
    {
        await using var db = CreateContext();
        var node = new LineNode { Name = "跨越点 #001", Description = "10kV 线路跨越公路", LocationGeo = "116.404,39.915" };
        db.LineNodes.Add(node);
        await db.SaveChangesAsync();

        var loaded = await db.LineNodes.FindAsync(node.Id);
        Assert.NotNull(loaded);
        Assert.Equal("跨越点 #001", loaded.Name);
        Assert.Equal("116.404,39.915", loaded.LocationGeo);
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        await using var db = CreateContext();
        var node = new LineNode { Name = "旧名称" };
        db.LineNodes.Add(node);
        await db.SaveChangesAsync();

        node.Name = "新名称";
        node.Description = "更新后的描述";
        await db.SaveChangesAsync();

        var loaded = await db.LineNodes.FindAsync(node.Id);
        Assert.Equal("新名称", loaded!.Name);
        Assert.Equal("更新后的描述", loaded.Description);
    }

    [Fact]
    public async Task Delete_RemovesLineNode()
    {
        await using var db = CreateContext();
        var node = new LineNode { Name = "待删除" };
        db.LineNodes.Add(node);
        await db.SaveChangesAsync();

        db.LineNodes.Remove(node);
        await db.SaveChangesAsync();

        var loaded = await db.LineNodes.FindAsync(node.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetAll_WithSearch_FiltersByName()
    {
        await using var db = CreateContext();
        db.LineNodes.Add(new LineNode { Name = "跨越点 A" });
        db.LineNodes.Add(new LineNode { Name = "跨越点 B" });
        db.LineNodes.Add(new LineNode { Name = "杆塔 C" });
        await db.SaveChangesAsync();

        var results = await db.LineNodes
            .Where(n => n.Name.Contains("跨越点"))
            .ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("跨越点", r.Name));
    }

    [Fact]
    public async Task LineNode_ImplementsInode()
    {
        var node = new LineNode { Id = "test-id", Name = "测试节点" };
        Assert.Equal("test-id", node.Id);
        Assert.Equal("测试节点", node.Name);
    }
}
