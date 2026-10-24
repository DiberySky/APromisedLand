using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// LineNode CRUD 集成测试：EF Core InMemory 提供器，无需 Docker。
///
/// R2 Name 收编后：LineNode.Name 为 [NotMapped] 读穿 Node.Name；
/// 名称唯一真源在 nodes 表，本测试同时维护 Node 行并通过 Node.Name 断言。
/// </summary>
public class LineNodeCrudTests
{
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(LineNode) && t.ClrType != typeof(Node))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }
            mb.Entity<Node>(e => e.HasKey(x => x.Id));
            mb.Entity<LineNode>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Node)
                    .WithOne()
                    .HasForeignKey<LineNode>(x => x.Id)
                    .OnDelete(DeleteBehavior.Restrict);
            });
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

    /// <summary>创建 LineNode 时同步创建对应 Node 行（名称唯一真源）。</summary>
    private static async Task<LineNode> CreateLineNodeAsync(TreeGraphDbContext db, string name, string? description = null)
    {
        var node = new LineNode { Name = name, Description = description };
        db.Nodes.Add(new Node { Id = node.Id, HostType = "LineNode", Name = name });
        db.LineNodes.Add(node);
        await db.SaveChangesAsync();
        return node;
    }

    [Fact]
    public async Task Create_PersistsLineNode()
    {
        await using var db = CreateContext();
        await CreateLineNodeAsync(db, "跨越点 #001", "10kV 线路跨越公路");

        var loaded = await db.LineNodes.Include(x => x.Node).FirstAsync(x => x.Id == db.LineNodes.First().Id);
        Assert.NotNull(loaded);
        Assert.Equal("跨越点 #001", loaded.Name);
        Assert.Equal("10kV 线路跨越公路", loaded.Description);
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        await using var db = CreateContext();
        var node = await CreateLineNodeAsync(db, "旧名称");

        node.Name = "新名称";
        node.Description = "更新后的描述";
        node.Node!.Name = "新名称";
        await db.SaveChangesAsync();

        var loaded = await db.LineNodes.Include(x => x.Node).FirstAsync(x => x.Id == node.Id);
        Assert.Equal("新名称", loaded!.Name);
        Assert.Equal("更新后的描述", loaded.Description);
    }

    [Fact]
    public async Task Delete_RemovesLineNode()
    {
        await using var db = CreateContext();
        var node = await CreateLineNodeAsync(db, "待删除");

        db.LineNodes.Remove(node);
        db.Nodes.Remove(db.Nodes.Find(node.Id)!);
        await db.SaveChangesAsync();

        var loaded = await db.LineNodes.FindAsync(node.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetAll_WithSearch_FiltersByName()
    {
        await using var db = CreateContext();
        await CreateLineNodeAsync(db, "跨越点 A");
        await CreateLineNodeAsync(db, "跨越点 B");
        await CreateLineNodeAsync(db, "杆塔 C");

        var results = await db.LineNodes
            .Include(x => x.Node)
            .Where(n => n.Node!.Name.Contains("跨越点"))
            .ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("跨越点", r.Name));
    }

    [Fact]
    public void LineNode_ImplementsInode()
    {
        var node = new LineNode { Id = "test-id", Name = "测试节点" };
        Assert.Equal("test-id", node.Id);
        Assert.Equal("测试节点", node.Name);
    }
}
