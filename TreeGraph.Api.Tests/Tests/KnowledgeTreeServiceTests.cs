using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// KnowledgeTreeService 单元测试：EF Core InMemory 提供器，无需 Docker。
/// KnowledgeTree 无生产种子数据；测试夹具 Fixture()（Sample Root + Sample 1/2/3 及其子级，
/// 共 11 节点）由 TestDbContext HasData 注入。
///
/// 与 UnitTree 的差异：
/// - HasChildren / Parent 均为 [NotMapped]，不落库；
///   QueryNodesAsync 的 OnlyWithChildren 因引用未映射属性无法转译，用例固化为抛错。
/// - MoveNodeAsync 移动到非空父节点路径依赖 Npgsql 原生递归 SQL
///   （IsDescendantAsync），InMemory 不支持，该路径由集成测试覆盖。
/// </summary>
public class KnowledgeTreeServiceTests
{
    // 夹具中的已知节点（GUID 固定，见本文件 Fixture()）
    private const string RootId = "beef0000-0000-4000-8000-000000000001";   // Sample Root
    private const string Sample1Id = "beef0000-0000-4000-8000-000000000002"; // Sample 1
    private const string Sample2Id = "beef0000-0000-4000-8000-000000000004"; // Sample 2
    private const string Sample3Id = "beef0000-0000-4000-8000-000000000007"; // Sample 3
    private const string Sample33Id = "beef0000-0000-4000-8000-000000000010"; // Sample 3.3
    private const string Sample331Id = "beef0000-0000-4000-8000-000000000011"; // Sample 3.3.1

    /// <summary>测试夹具：Sample Root + Sample 1/2/3 及其子级，共 11 节点（含 1 归档节点 2.2）。</summary>
    private static List<KnowledgeTree> Fixture() => new()
    {
        new() { Id = "beef0000-0000-4000-8000-000000000001", Name = "Sample Root", Description = "根知识示例", CanHaveChildren = true, SortOrder = 0, ParentId = null, HasChildren = true },
        new() { Id = "beef0000-0000-4000-8000-000000000002", Name = "Sample 1", Description = "子知识 1", CanHaveChildren = true, SortOrder = 1, ParentId = "beef0000-0000-4000-8000-000000000001", HasChildren = true },
        new() { Id = "beef0000-0000-4000-8000-000000000003", Name = "Sample 1.1", Description = "子知识 1.1", CanHaveChildren = false, SortOrder = 0, ParentId = "beef0000-0000-4000-8000-000000000002", HasChildren = false },
        new() { Id = "beef0000-0000-4000-8000-000000000004", Name = "Sample 2", Description = "子知识 2", CanHaveChildren = true, SortOrder = 2, ParentId = "beef0000-0000-4000-8000-000000000001", HasChildren = true },
        new() { Id = "beef0000-0000-4000-8000-000000000005", Name = "Sample 2.1", Description = "子知识 2.1", CanHaveChildren = false, SortOrder = 0, ParentId = "beef0000-0000-4000-8000-000000000004", HasChildren = false },
        new() { Id = "beef0000-0000-4000-8000-000000000006", Name = "Sample 2.2", Description = "子知识 2.2 [Archived]", CanHaveChildren = false, IsArchived = true, SortOrder = 1, ParentId = "beef0000-0000-4000-8000-000000000004", HasChildren = false },
        new() { Id = "beef0000-0000-4000-8000-000000000007", Name = "Sample 3", Description = "子知识 3", CanHaveChildren = true, SortOrder = 3, ParentId = "beef0000-0000-4000-8000-000000000001", HasChildren = true },
        new() { Id = "beef0000-0000-4000-8000-000000000008", Name = "Sample 3.1", Description = "子知识 3.1", CanHaveChildren = false, SortOrder = 0, ParentId = "beef0000-0000-4000-8000-000000000007", HasChildren = false },
        new() { Id = "beef0000-0000-4000-8000-000000000009", Name = "Sample 3.2", Description = "子知识 3.2", CanHaveChildren = false, SortOrder = 1, ParentId = "beef0000-0000-4000-8000-000000000007", HasChildren = false },
        new() { Id = "beef0000-0000-4000-8000-000000000010", Name = "Sample 3.3", Description = "子知识 3.3", CanHaveChildren = true, SortOrder = 2, ParentId = "beef0000-0000-4000-8000-000000000007", HasChildren = true },
        new() { Id = "beef0000-0000-4000-8000-000000000011", Name = "Sample 3.3.1", Description = "子知识 3.3.1", CanHaveChildren = false, SortOrder = 0, ParentId = "beef0000-0000-4000-8000-000000000010", HasChildren = false },
    };

    /// <summary>
    /// 测试专用 DbContext：忽略除 KnowledgeTree / EdgeInstance / InodeEntity 外的全部实体
    /// （DbSet 约定会自动发现基类所有 DbSet，其中 JsonDocument 属性在 InMemory 下无法映射），
    /// 保留节点删除守卫所依赖的表配置与种子数据。
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(KnowledgeTree)
                                  && t.ClrType != typeof(EdgeInstance)
                                  && t.ClrType != typeof(InodeEntity))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }

            mb.Entity<KnowledgeTree>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasData(Fixture());
            });

            // 节点删除守卫依赖：被边引用 / 仍有挂载实体数据
            mb.Entity<EdgeInstance>(e => e.HasKey(x => x.EdgeId));
            mb.Entity<InodeEntity>(e => e.HasKey(x => new { x.InodeId, x.EntityType }));
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

    private static KnowledgeTreeService CreateService(TreeGraphDbContext context) => new(context);

    // ========== 实体 ==========

    [Fact]
    public void Text_ReturnsName()
    {
        var node = new KnowledgeTree { Name = "Sample 1" };
        Assert.Equal("Sample 1", node.Text());
    }

    [Fact]
    public void Equals_ComparesById()
    {
        var a = new KnowledgeTree { Id = "x", Name = "A" };
        var b = new KnowledgeTree { Id = "x", Name = "B" };
        var c = new KnowledgeTree { Id = "y", Name = "A" };

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(null));
    }

    // ========== 查询 ==========

    [Fact]
    public async Task GetRootNodesAsync_ReturnsSeededRootWithChildren()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var roots = await service.GetRootNodesAsync();

        var root = Assert.Single(roots);
        Assert.Equal(RootId, root.Id);
        Assert.True(root.HasChildren);
        Assert.True(root.Expanded);
        Assert.Equal(3, root.Children!.Count);
    }

    [Fact]
    public async Task GetRootNodesAsync_WithRootId_ReturnsSingleNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var roots = await service.GetRootNodesAsync(Sample2Id);

        var node = Assert.Single(roots);
        Assert.Equal(Sample2Id, node.Id);
        Assert.True(node.HasChildren); // Sample 2 下挂 2.1 / 2.2
        Assert.Equal(2, node.Children!.Count);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsOrderedChildrenWithParentSet()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var children = await service.GetChildrenAsync(RootId);

        Assert.Equal(3, children.Count); // Sample 1/2/3
        Assert.Equal([Sample1Id, Sample2Id, Sample3Id],
            children.Select(c => c.Id).ToArray());
        Assert.All(children, c =>
        {
            Assert.NotNull(c.Parent);
            Assert.Equal(RootId, c.Parent!.Id);
            Assert.True(c.HasChildren); // 三个知识节点均有子级
        });
    }

    [Fact]
    public async Task QueryNodesAsync_FiltersByParentId()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.QueryNodesAsync(
            new TreeQueryParams { ParentId = Sample3Id, Page = 1, PageSize = 50 });

        Assert.Equal(3, result.Count); // Sample 3.1 / 3.2 / 3.3
    }

    [Fact]
    public async Task QueryNodesAsync_FiltersBySearchTerm()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 服务约定：ParentId 为 null 表示仅根节点；空串表示不过滤父级（全表搜索）
        var result = await service.QueryNodesAsync(
            new TreeQueryParams { ParentId = "", SearchTerm = "Sample 2", Page = 1, PageSize = 50 });

        Assert.Equal(3, result.Count); // Sample 2 / 2.1 / 2.2 [Archived]
    }

    [Fact]
    public async Task QueryNodesAsync_OnlyWithChildren_ThrowsNotTranslatable()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 既有行为固化：HasChildren 为 [NotMapped]，EF 无法转译为 SQL，
        // OnlyWithChildren 路径必然抛 InvalidOperationException（未修改服务）。
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryNodesAsync(
                new TreeQueryParams { ParentId = "", OnlyWithChildren = true, Page = 1, PageSize = 50 }));
    }

    // ========== 创建 / 更新 / 删除 ==========

    [Fact]
    public async Task CreateNodeAsync_PersistsNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 注：TreeNodeDto.Id 默认为 string.Empty，服务用 ?? 判空，须显式传 Id
        var newId = Guid.NewGuid().ToString();
        var created = await service.CreateNodeAsync(new TreeNodeDto<KnowledgeTree>
        {
            Id = newId,
            Text = "Sample 1.2",
            ParentId = Sample1Id,
            SortOrder = 1
        });

        Assert.Equal(newId, created.Id);
        Assert.Equal("Sample 1.2", created.Text);

        var entity = await db.KnowledgeTrees.FindAsync(newId);
        Assert.Equal("Sample 1.2", entity!.Name);
        Assert.Equal(Sample1Id, entity.ParentId);
        Assert.Equal(1, entity.SortOrder);
    }

    [Fact]
    public async Task UpdateNodeAsync_UpdatesNameAndSortOrder()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.UpdateNodeAsync(new TreeNodeDto<KnowledgeTree>
        {
            Id = Sample1Id,
            Text = "Sample One",
            SortOrder = 99,
            ParentId = RootId
        });

        var entity = await db.KnowledgeTrees.FindAsync(Sample1Id);
        Assert.Equal("Sample One", entity!.Name);
        Assert.Equal(99, entity.SortOrder);
        Assert.Equal(RootId, entity.ParentId);
    }

    [Fact]
    public async Task UpdateNodeAsync_MissingNode_Throws()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateNodeAsync(new TreeNodeDto<KnowledgeTree> { Id = "not-exist", Text = "x" }));
    }

    [Fact]
    public async Task UpdateChildrenAsync_AppliesNewSortOrder()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 颠倒根节点三个子级的顺序：3 → 0、1 → 1、2 → 2
        await service.UpdateChildrenAsync(new TreeNodeDto<KnowledgeTree>
        {
            Id = RootId,
            Children =
            [
                new TreeNodeDto<KnowledgeTree> { Id = Sample3Id, SortOrder = 0 },
                new TreeNodeDto<KnowledgeTree> { Id = Sample1Id, SortOrder = 1 },
                new TreeNodeDto<KnowledgeTree> { Id = Sample2Id, SortOrder = 2 },
            ]
        });

        Assert.Equal(1, (await db.KnowledgeTrees.FindAsync(Sample1Id))!.SortOrder);
        Assert.Equal(2, (await db.KnowledgeTrees.FindAsync(Sample2Id))!.SortOrder);
        Assert.Equal(0, (await db.KnowledgeTrees.FindAsync(Sample3Id))!.SortOrder);
    }

    [Fact]
    public async Task DeleteNodeAsync_RemovesNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        Assert.True(await service.DeleteNodeAsync(Sample331Id));
        Assert.Null(await db.KnowledgeTrees.FindAsync(Sample331Id));
    }

    [Fact]
    public async Task DeleteNodeAsync_MissingNode_ReturnsFalse()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        Assert.False(await service.DeleteNodeAsync("not-exist"));
    }

    // ========== 移动（仅限根方向；非空父节点的防环走原生 SQL，见类注释） ==========

    [Fact]
    public async Task MoveNodeAsync_MissingNode_Throws()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.MoveNodeAsync("not-exist", null));
    }

    [Fact]
    public async Task MoveNodeAsync_ToRoot_UpdatesParentId()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        Assert.True(await service.MoveNodeAsync(Sample1Id, null));

        var moved = await db.KnowledgeTrees.FindAsync(Sample1Id);
        Assert.Null(moved!.ParentId);
    }

    // ========== 祖先链 ==========

    [Fact]
    public async Task GetAncestorPathAsync_ReturnsRootToNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var path = await service.GetAncestorPathAsync(Sample331Id);

        Assert.Equal([RootId, Sample3Id, Sample33Id, Sample331Id], path);
    }

    [Fact]
    public async Task GetAncestorPathAsyncAll_ReturnsSamePath()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var path = await service.GetAncestorPathAsyncAll(Sample331Id);

        Assert.Equal([RootId, Sample3Id, Sample33Id, Sample331Id], path);
    }
}
