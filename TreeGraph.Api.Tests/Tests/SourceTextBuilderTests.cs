using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Shared.TreeEavSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// SourceTextBuilder 单元测试（EF Core InMemory，无需 Docker）。
/// 守护语义搜索幂等的基础契约：确定性 + 内容指纹敏感性。
/// 契约：BuildAsync 返回 NodeSourceText（节点文本 NodeText/NodeHash + 实体列表 EntityTexts）。
/// </summary>
public class SourceTextBuilderTests
{
    private const string InodeId = "11111111-1111-1111-1111-111111111111";

    /// <summary>
    /// 测试专用 DbContext：仅保留语义搜索文本构造依赖的实体，
    /// 并忽略 JsonDocument 属性（InMemory 不支持映射）。
    /// </summary>
    private sealed class TestDbContext(DbContextOptions<TreeGraphDbContext> options)
        : TreeGraphDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder mb)
        {
            // ★ 先让基类完成全部配置（Npgsql 的 jsonb 转换等），再忽略。
            //   InMemory 不支持 JsonDocument 映射，未查询的实体不影响测试。
            base.OnModelCreating(mb);

            foreach (var et in mb.Model.GetEntityTypes().ToList())
            {
                foreach (var prop in et.GetProperties()
                             .Where(p => p.ClrType == typeof(System.Text.Json.JsonDocument))
                             .ToList())
                {
                    mb.Entity(et.ClrType).Ignore(prop.Name);
                }
            }
        }
    }

    private static TreeGraphDbContext CreateDb(string dbName, bool seed = true)
    {
        var options = new DbContextOptionsBuilder<TreeGraphDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new TestDbContext(options);

        if (seed)
        {
            db.SpaceTrees.Add(new SpaceTree { Id = InodeId, Name = "一楼配电室" });
            db.InodeEntities.Add(new InodeEntity
            {
                InodeId = InodeId, EntityType = "Equipment",
                EntityId = "e-1"
            });
            db.AttributeCatalog.Add(new AttributeDefinition
            {
                AttributeId = "attr-name", EntityType = "Equipment",
                AttributeName = "name", DisplayName = "名称",
                DataType = "string", DisplayOrder = 1
            });
            db.AttributeValues.Add(new AttributeValue
            {
                ValueId = "v-1", EntityType = "Equipment", EntityId = "e-1",
                AttributeId = "attr-name", ValueString = "消防水泵"
            });
            db.SaveChanges();
        }

        return db;
    }

    [Fact]
    public async Task Build_同一份状态两次构造_文本与指纹完全一致()
    {
        var dbName = Guid.NewGuid().ToString();

        NodeSourceText first, second;
        await using (var db = CreateDb(dbName))
        {
            first = (await new SourceTextBuilder(db).BuildAsync(InodeId))!;
        }
        await using (var db = CreateDb(dbName, seed: false))
        {
            second = (await new SourceTextBuilder(db).BuildAsync(InodeId))!;
        }

        // 节点级
        Assert.Equal(first.NodeText, second.NodeText);
        Assert.Equal(first.NodeHash, second.NodeHash);
        Assert.Equal("space_tree", first.HostType);
        Assert.Contains("节点：一楼配电室", first.NodeText);

        // 实体级
        var firstEntity = Assert.Single(first.EntityTexts);
        var secondEntity = Assert.Single(second.EntityTexts);
        Assert.Equal(firstEntity.Text, secondEntity.Text);
        Assert.Equal(firstEntity.Hash, secondEntity.Hash);
        Assert.Contains("名称 = 消防水泵", firstEntity.Text);
    }

    [Fact]
    public async Task Build_属性值变化_实体指纹变化()
    {
        var dbName = Guid.NewGuid().ToString();

        string hashBefore;
        await using (var db = CreateDb(dbName))
        {
            hashBefore = (await new SourceTextBuilder(db).BuildAsync(InodeId))!
                .EntityTexts.Single().Hash;

            db.AttributeValues.Single().ValueString = "消防水泵（备用）";
            await db.SaveChangesAsync();
        }

        string hashAfter;
        await using (var db = CreateDb(dbName, seed: false))
        {
            hashAfter = (await new SourceTextBuilder(db).BuildAsync(InodeId))!
                .EntityTexts.Single().Hash;
        }

        Assert.NotEqual(hashBefore, hashAfter);
    }

    [Fact]
    public async Task Build_无实体节点_仍返回节点文本()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName);
        db.InodeEntities.RemoveRange(db.InodeEntities);
        await db.SaveChangesAsync();

        var result = await new SourceTextBuilder(db).BuildAsync(InodeId);

        // 节点仍在树中（仅无实体）→ 返回节点文本，实体列表为空
        Assert.NotNull(result);
        Assert.Contains("节点：一楼配电室", result!.NodeText);
        Assert.Empty(result.EntityTexts);
    }

    [Fact]
    public async Task Build_节点不在树中且无实体_返回null()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName);
        db.InodeEntities.RemoveRange(db.InodeEntities);
        db.SpaceTrees.RemoveRange(db.SpaceTrees);
        await db.SaveChangesAsync();

        var result = await new SourceTextBuilder(db).BuildAsync(InodeId);

        Assert.Null(result);
    }

    // ============================================================
    // Edge 宿主（方案 A：边接入宿主挂载体系）
    // ============================================================

    private const string EdgeId = "edge-22222222-2222-2222-2222-222222222222";

    /// <summary>向给定库追加一条边宿主（可指定显示名）。</summary>
    private static async Task SeedEdgeAsync(
        TreeGraphDbContext db, string edgeId, string? displayName)
    {
        db.EdgeInstances.Add(new TreeGraph.Api.EdgeSky.Entities.EdgeInstance
        {
            EdgeId = edgeId,
            EdgeTypeId = "et-1",
            FromNodeId = "node-a",
            ToNodeId = "node-b",
            DisplayName = displayName
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Build_边宿主_单行文本含当前节点与显示名()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName, seed: false);
        await SeedEdgeAsync(db, EdgeId, "电缆段-甲");

        var result = await new SourceTextBuilder(db).BuildAsync(EdgeId);

        Assert.NotNull(result);
        Assert.Equal("edge", result!.HostType);
        Assert.Equal("default", result.SpaceId); // 边无空间归属
        // 单行（无层级路径行）：仅「当前节点：电缆段-甲」
        Assert.DoesNotContain("\n", result.NodeText);
        Assert.Contains("当前节点：电缆段-甲", result.NodeText);
        Assert.DoesNotContain("层级路径", result.NodeText);
    }

    [Fact]
    public async Task Build_边宿主_无显示名时回退EdgeId()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = CreateDb(dbName, seed: false);
        await SeedEdgeAsync(db, EdgeId, displayName: null);

        var result = await new SourceTextBuilder(db).BuildAsync(EdgeId);

        Assert.NotNull(result);
        Assert.Contains($"当前节点：{EdgeId}", result!.NodeText);
    }

    [Fact]
    public async Task Build_边宿主_同一份状态两次构造文本一致()
    {
        var dbName = Guid.NewGuid().ToString();

        NodeSourceText first, second;
        await using (var db = CreateDb(dbName, seed: false))
        {
            await SeedEdgeAsync(db, EdgeId, "电缆段-甲");
            first = (await new SourceTextBuilder(db).BuildAsync(EdgeId))!;
        }
        await using (var db = CreateDb(dbName, seed: false))
        {
            second = (await new SourceTextBuilder(db).BuildAsync(EdgeId))!;
        }

        Assert.Equal(first.NodeText, second.NodeText);
        Assert.Equal(first.NodeHash, second.NodeHash);
        Assert.Equal("edge", second.HostType);
    }

    [Fact]
    public async Task Build_边宿主_显示名变化_节点指纹变化()
    {
        var dbName = Guid.NewGuid().ToString();

        string hashBefore;
        await using (var db = CreateDb(dbName, seed: false))
        {
            await SeedEdgeAsync(db, EdgeId, "电缆段-甲");
            hashBefore = (await new SourceTextBuilder(db).BuildAsync(EdgeId))!.NodeHash;

            // 同一上下文内改显示名（InMemory 按 dbName 共享存储，重复 Add 同主键会冲突）
            db.EdgeInstances.Single(e => e.EdgeId == EdgeId).DisplayName = "电缆段-乙";
            await db.SaveChangesAsync();
        }

        string hashAfter;
        await using (var db = CreateDb(dbName, seed: false))
        {
            hashAfter = (await new SourceTextBuilder(db).BuildAsync(EdgeId))!.NodeHash;
        }

        Assert.NotEqual(hashBefore, hashAfter);
    }
}
