using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Shared.NodeSky.Entities;
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
            // R2 Name 收编：名称唯一真源在 nodes，需同步创建 Node 行。
            db.Nodes.Add(new Node { Id = InodeId, HostType = "SpaceTree", Name = "一楼配电室" });
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
}
