using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.Api.StringTreeSky.Services;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.StringTreeSky.Contracts;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 混合式 EntityType 下，删除空间/节点时的 EAV 级联清理测试。
///
/// 覆盖：
///   1. 独立空间（StringTreeNode:{guid}）删除 → 全部清空
///   2. 共享空间（StringTreeNode）删除 → 全部清空
///   3. 混合场景：一棵树下既有独立又有共享 EntityType 的节点
///   4. 子树多级递归：孙节点也要清
///   5. 属性值 + 自定义表行 都清
/// </summary>
[Collection("Integration")]
public class StringTreeEavCleanupTests(EavApiFactory factory)
{
    private EavApiFactory Factory => factory;

    // ============================================================
    // 工厂
    // ============================================================
    //
    // 适配说明（相对设计文档）：
    //   文档原 Fixture 用 EF InMemory，但 DeleteNodeAsync 内部使用
    //   ExecuteDeleteAsync，InMemory Provider 不支持；而 TreeGraphDbContext 模型
    //   含 jsonb/GIN/timestamptz 等 PostgreSQL 专属配置，SQLite 也无法建库。
    //   因此接入仓库标准的 Testcontainers PostgreSQL（Integration 集合，
    //   全测试共享容器、串行执行），直接构造 EfStringTreeService 做白盒测试。
    //   配套差异：
    //     1. attribute_values / custom_table_rows 有真实外键，随机 GUID 不合法，
    //        改为 get-or-create 真实的 AttributeDefinition / CustomTableDefinition；
    //     2. 全表 Count 断言改为按本 Fixture 播种集合计数（共享库中存在其它测试数据）；
    //     3. 播种前若残留同 Id 节点先级联清除，保证可重复执行。

    private sealed class Fixture : IAsyncDisposable
    {
        public TreeGraphDbContext Db { get; }
        public DbContextOptions<TreeGraphDbContext> Options { get; }
        public EfStringTreeService Service { get; }

        private readonly IServiceScope _scope;
        private readonly HashSet<string> _nodeIds = new();
        private readonly List<(string EntityType, string EntityId)> _valueOwners = new();
        private readonly List<(string EntityType, string EntityId)> _rowOwners = new();

        public Fixture(EavApiFactory factory)
        {
            _scope = factory.Services.CreateScope();
            var sp = _scope.ServiceProvider;
            Options = sp.GetRequiredService<DbContextOptions<TreeGraphDbContext>>();
            Db = sp.GetRequiredService<TreeGraphDbContext>();
            Service = new EfStringTreeService(Db, Options);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            if (_scope is IAsyncDisposable ad)
                await ad.DisposeAsync();
            else
                _scope.Dispose();
        }

        // ============ 按本 Fixture 播种集合计数（共享容器下隔离断言） ============

        public Task<int> CountNodesAsync() =>
            Db.StringTreeSkyNodes.CountAsync(n => _nodeIds.Contains(n.Id));

        public async Task<int> CountAttributeValuesAsync()
        {
            var types = _valueOwners.Select(o => o.EntityType).Distinct().ToArray();
            var rows = await Db.AttributeValues
                .Where(v => types.Contains(v.EntityType))
                .Select(v => new { v.EntityType, v.EntityId })
                .ToListAsync();
            return rows.Count(v => _valueOwners.Contains((v.EntityType, v.EntityId)));
        }

        public async Task<int> CountCustomTableRowsAsync()
        {
            var types = _rowOwners.Select(o => o.EntityType).Distinct().ToArray();
            var rows = await Db.CustomTableRows
                .Where(r => types.Contains(r.ParentEntityType))
                .Select(r => new { r.ParentEntityType, r.ParentEntityId })
                .ToListAsync();
            return rows.Count(r => _rowOwners.Contains((r.ParentEntityType, r.ParentEntityId)));
        }

        // ============ 便捷构造 ============

        public async Task<StringNodeEntity> SeedNodeAsync(
            string id, string? parentId, string entityType, string name = "节点")
        {
            // 重跑保护：同 Id 已存在时先级联清除其旧子树
            if (await Db.StringTreeSkyNodes.AnyAsync(n => n.Id == id))
                await Service.DeleteNodeAsync(id);

            var node = new StringNodeEntity
            {
                Id = id,
                Name = name,
                ParentId = parentId,
                SortOrder = 0,
                EntityType = entityType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            Db.StringTreeSkyNodes.Add(node);
            await Db.SaveChangesAsync();
            _nodeIds.Add(id);
            return node;
        }

        /// <summary>
        /// get-or-create 属性定义。uq_attr_catalog 对 (EntityType, AttributeName)
        /// 唯一且无软删过滤，因此查询不带 IsDeleted 条件。
        /// </summary>
        private async Task<string> EnsureAttributeAsync(string entityType, string attrName)
        {
            var existing = await Db.AttributeCatalog
                .FirstOrDefaultAsync(a => a.EntityType == entityType
                                          && a.AttributeName == attrName);
            if (existing is not null)
                return existing.AttributeId;

            var def = new AttributeDefinition
            {
                AttributeId = Guid.NewGuid().ToString("D"),
                EntityType = entityType,
                AttributeName = attrName,
                DisplayName = attrName,
                DataType = "string",
                Version = 1,
                DisplayOrder = 1
            };
            Db.AttributeCatalog.Add(def);
            await Db.SaveChangesAsync();
            return def.AttributeId;
        }

        /// <summary>get-or-create 自定义表定义（uq_custom_table 含 Version）。</summary>
        private async Task<string> EnsureTableAsync(string entityType, string tableName)
        {
            var existing = await Db.CustomTables
                .FirstOrDefaultAsync(t => t.EntityType == entityType
                                          && t.TableName == tableName);
            if (existing is not null)
                return existing.TableDefinitionId;

            var def = new CustomTableDefinition
            {
                TableDefinitionId = Guid.NewGuid().ToString("D"),
                EntityType = entityType,
                TableName = tableName,
                DisplayName = tableName,
                Version = 1,
                DisplayOrder = 1
            };
            Db.CustomTables.Add(def);
            await Db.SaveChangesAsync();
            return def.TableDefinitionId;
        }

        public async Task SeedAttributeValueAsync(
            string entityType, string entityId, string attrName, string value)
        {
            var attrId = await EnsureAttributeAsync(entityType, attrName);
            Db.AttributeValues.Add(new AttributeValue
            {
                ValueId = Guid.NewGuid().ToString("D"),
                EntityId = entityId,
                EntityType = entityType,
                AttributeId = attrId,
                ValueString = value,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync();
            _valueOwners.Add((entityType, entityId));
        }

        public async Task SeedCustomTableRowAsync(
            string entityType, string entityId, string tableName)
        {
            var tableDefId = await EnsureTableAsync(entityType, tableName);
            // custom_table_rows.attribute_id 外键指向 attribute_catalog
            var attrId = await EnsureAttributeAsync(entityType, tableName);
            Db.CustomTableRows.Add(new CustomTableRow
            {
                RowId = Guid.NewGuid().ToString("D"),
                TableDefinitionId = tableDefId,
                AttributeId = attrId,
                ParentEntityId = entityId,
                ParentEntityType = entityType,
                RowData = JsonDocument.Parse($"{{\"table\":\"{tableName}\"}}"),
                RowOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync();
            _rowOwners.Add((entityType, entityId));
        }
    }

    // ============================================================
    // 测试 1：独立空间删除 → EAV 全清
    // ============================================================

    [Fact]
    public async Task Delete_IndependentSpace_CleansAllEav()
    {
        await using var fx = new Fixture(Factory);

        const string spaceId = "SPACE-INDEP-001";
        var entityType = $"{StringTreeEntityTypes.Node}:{spaceId}";

        // 空间 + 两个子节点
        await fx.SeedNodeAsync(spaceId, null, entityType, "独立空间");
        await fx.SeedNodeAsync("CHILD-1", spaceId, entityType, "子节点1");
        await fx.SeedNodeAsync("CHILD-2", spaceId, entityType, "子节点2");

        // 每个节点都写属性 + 自定义表行
        foreach (var nid in new[] { spaceId, "CHILD-1", "CHILD-2" })
        {
            await fx.SeedAttributeValueAsync(entityType, nid, "brand", "华为");
            await fx.SeedCustomTableRowAsync(entityType, nid, "certifications");
        }

        // 前置断言
        Assert.Equal(3, await fx.CountNodesAsync());
        Assert.Equal(3, await fx.CountAttributeValuesAsync());
        Assert.Equal(3, await fx.CountCustomTableRowsAsync());

        // 执行删除
        var ok = await fx.Service.DeleteNodeAsync(spaceId);
        Assert.True(ok);

        // 后置断言：全清
        Assert.Equal(0, await fx.CountNodesAsync());
        Assert.Equal(0, await fx.CountAttributeValuesAsync());
        Assert.Equal(0, await fx.CountCustomTableRowsAsync());
    }

    // ============================================================
    // 测试 2：共享空间删除 → EAV 全清（回归旧行为）
    // ============================================================

    [Fact]
    public async Task Delete_SharedSpace_CleansAllEav()
    {
        await using var fx = new Fixture(Factory);

        const string spaceId = "SPACE-SHARED-001";
        const string entityType = StringTreeEntityTypes.Node;

        await fx.SeedNodeAsync(spaceId, null, entityType, "共享空间");
        await fx.SeedNodeAsync("C1", spaceId, entityType, "子1");

        await fx.SeedAttributeValueAsync(entityType, spaceId, "brand", "小米");
        await fx.SeedAttributeValueAsync(entityType, "C1", "brand", "红米");
        await fx.SeedCustomTableRowAsync(entityType, spaceId, "certifications");

        var ok = await fx.Service.DeleteNodeAsync(spaceId);
        Assert.True(ok);

        Assert.Equal(0, await fx.CountNodesAsync());
        Assert.Equal(0, await fx.CountAttributeValuesAsync());
        Assert.Equal(0, await fx.CountCustomTableRowsAsync());
    }

    // ============================================================
    // 测试 3：混合场景（一棵树下既有独立又有共享 EntityType）
    // ============================================================

    [Fact]
    public async Task Delete_MixedEntityTypesInSubtree_CleansAllGroups()
    {
        await using var fx = new Fixture(Factory);

        const string rootId = "MIX-ROOT";
        const string sharedType = StringTreeEntityTypes.Node;
        const string independentType = "StringTreeNode:mix-indep";

        // 根：共享；子A：独立；子B：共享；孙：独立
        await fx.SeedNodeAsync(rootId, null, sharedType, "根");
        await fx.SeedNodeAsync("A", rootId, independentType, "子A-独立");
        await fx.SeedNodeAsync("B", rootId, sharedType, "子B-共享");
        await fx.SeedNodeAsync("A1", "A", independentType, "孙A1-独立");

        // 每个 EntityType 都写属性
        await fx.SeedAttributeValueAsync(sharedType, rootId, "x", "1");
        await fx.SeedAttributeValueAsync(independentType, "A", "x", "2");
        await fx.SeedAttributeValueAsync(sharedType, "B", "x", "3");
        await fx.SeedAttributeValueAsync(independentType, "A1", "x", "4");

        // 前置（按本 Fixture 播种集合计数）
        Assert.Equal(4, await fx.CountAttributeValuesAsync());

        // 删除根节点
        var ok = await fx.Service.DeleteNodeAsync(rootId);
        Assert.True(ok);

        // 后置：全部清空（两个 EntityType 分组都被清）
        Assert.Equal(0, await fx.CountNodesAsync());
        Assert.Equal(0, await fx.CountAttributeValuesAsync());
    }

    // ============================================================
    // 测试 4：删除兄弟节点不影响其他空间
    // ============================================================

    [Fact]
    public async Task Delete_OneSpace_DoesNotTouchOther()
    {
        await using var fx = new Fixture(Factory);

        const string typeA = "StringTreeNode:space-a";
        const string typeB = "StringTreeNode:space-b";

        await fx.SeedNodeAsync("A-ROOT", null, typeA, "空间A");
        await fx.SeedNodeAsync("B-ROOT", null, typeB, "空间B");

        await fx.SeedAttributeValueAsync(typeA, "A-ROOT", "n", "a");
        await fx.SeedAttributeValueAsync(typeB, "B-ROOT", "n", "b");

        var ok = await fx.Service.DeleteNodeAsync("A-ROOT");
        Assert.True(ok);

        // A 清空
        Assert.False(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "A-ROOT"));
        Assert.False(await fx.Db.AttributeValues.AnyAsync(v => v.EntityId == "A-ROOT"));

        // B 保留
        Assert.True(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "B-ROOT"));
        Assert.True(await fx.Db.AttributeValues.AnyAsync(v => v.EntityId == "B-ROOT"));
    }

    // ============================================================
    // 测试 5：删子节点（非根）也要清
    // ============================================================

    [Fact]
    public async Task Delete_ChildNode_OnlyCleansItsSubtree()
    {
        await using var fx = new Fixture(Factory);

        const string type = "StringTreeNode:sp";
        await fx.SeedNodeAsync("ROOT", null, type, "根");
        await fx.SeedNodeAsync("MID", "ROOT", type, "中");
        await fx.SeedNodeAsync("LEAF", "MID", type, "叶");
        await fx.SeedNodeAsync("OTHER", "ROOT", type, "其他");

        await fx.SeedAttributeValueAsync(type, "MID", "x", "1");
        await fx.SeedAttributeValueAsync(type, "LEAF", "x", "2");
        await fx.SeedAttributeValueAsync(type, "OTHER", "x", "3");

        var ok = await fx.Service.DeleteNodeAsync("MID");
        Assert.True(ok);

        // MID + LEAF 都删
        Assert.False(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "MID"));
        Assert.False(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "LEAF"));
        Assert.False(await fx.Db.AttributeValues.AnyAsync(v => v.EntityId == "MID"));
        Assert.False(await fx.Db.AttributeValues.AnyAsync(v => v.EntityId == "LEAF"));

        // ROOT 和 OTHER 保留
        Assert.True(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "ROOT"));
        Assert.True(await fx.Db.StringTreeSkyNodes.AnyAsync(n => n.Id == "OTHER"));
        Assert.True(await fx.Db.AttributeValues.AnyAsync(v => v.EntityId == "OTHER"));
    }

    // ============================================================
    // 测试 6：删除后该 EntityType 可被复用（无孤儿数据）
    // ============================================================

    [Fact]
    public async Task AfterDelete_SameEntityTypeIsReusable()
    {
        await using var fx = new Fixture(Factory);

        const string type = "StringTreeNode:reusable";
        await fx.SeedNodeAsync("R1", null, type, "第一次");
        await fx.SeedAttributeValueAsync(type, "R1", "v", "1");

        await fx.Service.DeleteNodeAsync("R1");

        // 用同 EntityType 新建节点，应无残留属性
        await fx.SeedNodeAsync("R2", null, type, "第二次");
        var leftover = await fx.Db.AttributeValues
            .Where(v => v.EntityType == type)
            .CountAsync();
        Assert.Equal(0, leftover);

        // 新节点的属性查询应空
        var attrs = await fx.Db.AttributeValues
            .Where(v => v.EntityType == type && v.EntityId == "R2")
            .ToListAsync();
        Assert.Empty(attrs);
    }
}
