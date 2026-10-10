using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// UnitTreeService 单元测试：EF Core InMemory 提供器，无需 Docker。
/// 每个用例使用独立数据库名（Guid），互不干扰；种子数据来自
/// <see cref="TreeGraphDbContext"/> 的 HasData(UnitTree.SeedData())。
///
/// 注：MoveNodeAsync 移动到非空父节点路径依赖 Npgsql 原生递归 SQL
/// （IsDescendantAsync），InMemory 不支持，该路径由集成测试覆盖。
/// </summary>
public class UnitTreeServiceTests
{
    // 种子数据中的已知节点（GUID 固定，见 UnitTree.SeedData）
    private const string RootId = "9AB5700C-68F2-43F3-9D7E-805E7D5C539B"; // 计量单位
    private const string CurrencyId = "c5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f"; // 货币
    private const string YuanId = "f7a8b9c0-d1e2-4f3a-4b5c-6d7e8f9a0b1c"; // 元

    /// <summary>
    /// 测试专用 DbContext：忽略除 UnitTree 外的全部实体（DbSet 约定会
    /// 自动发现基类所有 DbSet，其中 JsonDocument 属性在 InMemory 下无法映射），
    /// 仅保留 UnitTree 配置与种子数据。
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entityType in mb.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(UnitTree))
                         .ToList())
            {
                mb.Ignore(entityType.ClrType);
            }

            mb.Entity<UnitTree>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Parent)
                    .WithMany()
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasData(UnitTree.SeedData());
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

    private static UnitTreeService CreateService(TreeGraphDbContext context) => new(context);

    // ========== 实体 ==========

    [Fact]
    public void Text_WithAbbreviation_AppendsAbbreviation()
    {
        var node = new UnitTree { Name = "元", Abbreviation = "CNY" };
        Assert.Equal("元 【CNY】", node.Text());
    }

    [Fact]
    public void Text_WithoutAbbreviation_ReturnsName()
    {
        var node = new UnitTree { Name = "货币", Abbreviation = "" };
        Assert.Equal("货币", node.Text());
    }

    [Fact]
    public void SeedData_ContainsRootAndCategories()
    {
        var seed = UnitTree.SeedData();
        var root = seed.Single(n => n.ParentId == null);
        Assert.Equal("计量单位", root.Name);
        Assert.Equal(15, seed.Count(n => n.ParentId == root.Id)); // 15 个分类
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
        Assert.Equal(15, root.Children!.Count);
    }

    [Fact]
    public async Task GetRootNodesAsync_WithRootId_ReturnsSingleNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var roots = await service.GetRootNodesAsync(CurrencyId);

        var node = Assert.Single(roots);
        Assert.Equal(CurrencyId, node.Id);
        Assert.True(node.HasChildren); // 货币下挂 7 个币种
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsOrderedChildrenWithParentSet()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var children = await service.GetChildrenAsync(CurrencyId);

        Assert.Equal(7, children.Count); // 元/美元/欧元/英镑/日元/港元/澳元
        Assert.Equal(children.OrderBy(c => c.SortOrder).Select(c => c.Id),
            children.Select(c => c.Id));
        Assert.All(children, c =>
        {
            Assert.NotNull(c.Parent);
            Assert.Equal(CurrencyId, c.Parent!.Id);
            Assert.False(c.HasChildren); // 币种为叶子
        });
    }

    [Fact]
    public async Task QueryNodesAsync_FiltersByParentId()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.QueryNodesAsync(
            new TreeQueryParams { ParentId = CurrencyId, Page = 1, PageSize = 50 });

        Assert.Equal(7, result.Count);
    }

    [Fact]
    public async Task QueryNodesAsync_FiltersBySearchTerm()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 服务约定：ParentId 为 null 表示仅根节点；空串表示不过滤父级（全表搜索）
        var result = await service.QueryNodesAsync(
            new TreeQueryParams { ParentId = "", SearchTerm = "千米", Page = 1, PageSize = 50 });

        Assert.Equal(2, result.Count); // 千米、千米/小时
    }

    [Fact]
    public async Task QueryNodesAsync_OnlyWithChildren()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.QueryNodesAsync(
            new TreeQueryParams { ParentId = "", OnlyWithChildren = true, Page = 1, PageSize = 50 });

        Assert.Equal(16, result.Count); // 根 + 15 分类
    }

    // ========== 创建 / 更新 / 删除 ==========

    [Fact]
    public async Task CreateNodeAsync_SetsParentHasChildren()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 注：TreeNodeDto.Id 默认为 string.Empty，服务用 ?? 判空，须显式传 Id
        var created = await service.CreateNodeAsync(new TreeNodeDto<UnitTree>
        {
            Id = Guid.NewGuid().ToString(),
            Text = "分",
            ParentId = YuanId, // 元 原本为叶子
            SortOrder = 0
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal("分", created.Text);

        var parent = await db.UnitTrees.FindAsync(YuanId);
        Assert.True(parent!.HasChildren);
    }

    [Fact]
    public async Task UpdateNodeAsync_UpdatesNameAndSortOrder()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.UpdateNodeAsync(new TreeNodeDto<UnitTree>
        {
            Id = YuanId,
            Text = "人民币元",
            SortOrder = 99,
            ParentId = CurrencyId
        });

        var entity = await db.UnitTrees.FindAsync(YuanId);
        Assert.Equal("人民币元", entity!.Name);
        Assert.Equal(99, entity.SortOrder);
        Assert.Equal(CurrencyId, entity.ParentId);
    }

    [Fact]
    public async Task UpdateNodeAsync_MissingNode_Throws()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateNodeAsync(new TreeNodeDto<UnitTree> { Id = "not-exist", Text = "x" }));
    }

    [Fact]
    public async Task DeleteNodeAsync_RemovesNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        Assert.True(await service.DeleteNodeAsync(YuanId));
        Assert.Null(await db.UnitTrees.FindAsync(YuanId));
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

        // 移动「元」到根；货币下仍有其它币种
        Assert.True(await service.MoveNodeAsync(YuanId, null));

        var moved = await db.UnitTrees.FindAsync(YuanId);
        var oldParent = await db.UnitTrees.FindAsync(CurrencyId);
        Assert.Null(moved!.ParentId);
        Assert.True(oldParent!.HasChildren); // 仍有子级，保持 true
    }

    [Fact]
    public async Task MoveNodeAsync_SingleChildMove_KeepsOldParentHasChildrenTrue()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // 既有行为固化：服务在 SaveChanges 前统计旧父节点子级数，
        // 被移动节点仍计入，导致独子移走后旧父节点 HasChildren 不会被重置为 false
        // （源 APromisedLand.Api 同此行为，本次仅记录，不修改服务）。
        var parent = new UnitTree { Id = Guid.NewGuid().ToString(), Name = "P", HasChildren = true };
        var child = new UnitTree { Id = Guid.NewGuid().ToString(), Name = "C", ParentId = parent.Id };
        db.UnitTrees.AddRange(parent, child);
        await db.SaveChangesAsync();

        Assert.True(await service.MoveNodeAsync(child.Id, null));

        var oldParent = await db.UnitTrees.FindAsync(parent.Id);
        Assert.True(oldParent!.HasChildren);
    }

    // ========== 祖先链 ==========

    [Fact]
    public async Task GetAncestorPathAsync_ReturnsRootToNode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var path = await service.GetAncestorPathAsync(YuanId);

        Assert.Equal([RootId, CurrencyId, YuanId], path);
    }
}
