using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Api.Data;
using TreeGraph.Api.Data.Seeding;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.StringTreeSky.Contracts;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 验证 StringTreeDemoSeeder 的端到端种子数据：
///   1 个 Space（demo-space-001）→ 5 个 StringTree 节点（≥3）→ 13 种 DataType 全覆盖的 EAV 属性。
///
/// 测试策略：
///   - DB 白盒断言（节点结构、EntityType 注册、属性定义、属性值、选项集、组合类型、自定义表）
///   - API 黑盒断言（GET /spaces、GET /entities/{id}、GET /schema）
///
/// 依赖：EavApiFactory 启动 Testing 环境 + Testcontainers PostgreSQL，
/// Program.cs 中的 seeder 链会自动播种 StringTreeDemoSeeder.SeedAsync。
/// </summary>
[Collection("Integration")]
public class StringTreeDemoSeederTests(EavApiFactory factory)
{
    private EavApiFactory Factory => factory;
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private TreeGraphDbContext CreateDb()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TreeGraphDbContext>();
    }

    // ============================================================
    // 1. Space 节点存在 + 独立 EntityType
    // ============================================================

    [Fact]
    public async Task SpaceNode_Exists_WithIndependentEntityType()
    {
        await using var db = CreateDb();
        var space = await db.StringTreeSkyNodes
            .AsNoTracking()
            .FirstAsync(n => n.Id == StringTreeDemoSeeder.SpaceNodeId);

        Assert.Equal("演示空间", space.Name);
        Assert.Null(space.ParentId);
        Assert.Equal(StringTreeDemoSeeder.SpaceEntityType, space.EntityType);
    }

    // ============================================================
    // 2. StringTree 节点结构（≥3 + 父子关系）
    // ============================================================

    [Fact]
    public async Task StringTree_HasAtLeastThreeNodes_WithCorrectHierarchy()
    {
        await using var db = CreateDb();
        var nodes = await db.StringTreeSkyNodes
            .AsNoTracking()
            .Where(n => n.EntityType == StringTreeDemoSeeder.SpaceEntityType)
            .ToListAsync();

        // ≥3 个节点（demo 实际播种 5 个）
        Assert.True(nodes.Count >= 3, $"节点数应 ≥3，实际 {nodes.Count}");

        // 根节点 1 个
        var roots = nodes.Where(n => n.ParentId == null).ToList();
        Assert.Single(roots);
        Assert.Equal(StringTreeDemoSeeder.SpaceNodeId, roots[0].Id);

        // demo-node-001 ~ 003 都是 space 的直接子节点
        Assert.Contains(nodes, n => n.Id == "demo-node-001" && n.ParentId == StringTreeDemoSeeder.SpaceNodeId);
        Assert.Contains(nodes, n => n.Id == "demo-node-002" && n.ParentId == StringTreeDemoSeeder.SpaceNodeId);
        Assert.Contains(nodes, n => n.Id == "demo-node-003" && n.ParentId == StringTreeDemoSeeder.SpaceNodeId);

        // demo-node-004 是 demo-node-001 的子节点（3 层）
        Assert.Contains(nodes, n => n.Id == "demo-node-004" && n.ParentId == "demo-node-001");
    }

    // ============================================================
    // 3. EntityType 已注册到 catalog
    // ============================================================

    [Fact]
    public async Task EntityType_Registered_InCatalog()
    {
        await using var db = CreateDb();
        var type = await db.EntityTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EntityType == StringTreeDemoSeeder.SpaceEntityType);

        Assert.NotNull(type);
        Assert.False(type!.IsDeleted);
        Assert.Equal("演示空间", type.DisplayName);
    }

    // ============================================================
    // 4. 属性定义覆盖 13 种 DataType
    // ============================================================

    [Fact]
    public async Task AttributeDefinitions_CoverAllDataTypes()
    {
        await using var db = CreateDb();
        var attrs = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType && !a.IsDeleted)
            .ToListAsync();

        // 15 个属性，覆盖 12 种 DataType（string 用 2 次、decimal 用 2 次、single_choice 用 2 次）
        Assert.Equal(15, attrs.Count);

        var dataTypes = attrs.Select(a => a.DataType).Distinct().ToList();
        Assert.Contains(EavDataTypes.String, dataTypes);
        Assert.Contains(EavDataTypes.Int, dataTypes);
        Assert.Contains(EavDataTypes.Decimal, dataTypes);
        Assert.Contains(EavDataTypes.Bool, dataTypes);
        Assert.Contains(EavDataTypes.Datetime, dataTypes);
        Assert.Contains(EavDataTypes.Date, dataTypes);
        Assert.Contains(EavDataTypes.Time, dataTypes);
        Assert.Contains(EavDataTypes.File, dataTypes);
        Assert.Contains(EavDataTypes.Json, dataTypes);
        Assert.Contains(EavDataTypes.SingleChoice, dataTypes);
        Assert.Contains(EavDataTypes.Composite, dataTypes);
        Assert.Contains(EavDataTypes.Table, dataTypes);
    }

    // ============================================================
    // 5. 属性定义的关键约束（单位/引用/选项集）
    // ============================================================

    [Fact]
    public async Task AttributeDefinitions_HaveCorrectReferences()
    {
        await using var db = CreateDb();
        var attrs = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType && !a.IsDeleted)
            .ToDictionaryAsync(a => a.AttributeName);

        // weight 绑定 kg 单位
        Assert.True(attrs["weight"].UnitId.HasValue, "weight 应绑定单位");

        // priority / status 引用选项集
        Assert.NotNull(attrs["priority"].RefOptionSetId);
        Assert.NotNull(attrs["status"].RefOptionSetId);

        // specs 引用组合类型
        Assert.NotNull(attrs["specs"].RefCompositeTypeId);

        // certifications 引用自定义表
        Assert.NotNull(attrs["certifications"].RefTableDefinitionId);
    }

    // ============================================================
    // 6. 属性值写入（demo-node-001/002/003 完整）
    // ============================================================

    [Fact]
    public async Task AttributeValues_Written_ForDemoNode001()
    {
        await using var db = CreateDb();
        var values = await db.AttributeValues
            .Include(v => v.Attribute)
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && v.EntityId == "demo-node-001")
            .ToListAsync();

        // demo-node-001 写入 13 个属性值（15 个属性中除 table 外都写值；table 走 custom_table_rows）
        Assert.True(values.Count >= 12, $"demo-node-001 应 ≥12 条值，实际 {values.Count}");

        // 关键值类型断言
        var title = values.FirstOrDefault(v => v.Attribute.AttributeName == "title");
        Assert.NotNull(title);
        Assert.Equal("旗舰手机", title!.ValueString);

        var qty = values.FirstOrDefault(v => v.Attribute.AttributeName == "quantity");
        Assert.NotNull(qty);
        Assert.Equal(100L, qty!.ValueInt);

        var price = values.FirstOrDefault(v => v.Attribute.AttributeName == "price");
        Assert.NotNull(price);
        Assert.Equal(5999.00m, price!.ValueDecimal);

        var active = values.FirstOrDefault(v => v.Attribute.AttributeName == "isActive");
        Assert.NotNull(active);
        Assert.True(active!.ValueBool);

        var priority = values.FirstOrDefault(v => v.Attribute.AttributeName == "priority");
        Assert.NotNull(priority);
        Assert.Equal("high", priority!.ValueString);

        var specs = values.FirstOrDefault(v => v.Attribute.AttributeName == "specs");
        Assert.NotNull(specs);
        Assert.NotNull(specs!.ValueJsonb);

        var metadata = values.FirstOrDefault(v => v.Attribute.AttributeName == "metadata");
        Assert.NotNull(metadata);
        Assert.NotNull(metadata!.ValueJsonb);
    }

    // ============================================================
    // 7. 选项集 priority / status 存在且选项齐全
    // ============================================================

    [Fact]
    public async Task OptionSets_PriorityAndStatus_ExistWithItems()
    {
        await using var db = CreateDb();
        var sets = await db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.EntityType == StringTreeDemoSeeder.SpaceEntityType)
            .ToListAsync();

        var priority = sets.FirstOrDefault(s => s.SetName == "priority");
        Assert.NotNull(priority);
        Assert.Equal(4, priority!.Items.Count);
        Assert.Contains(priority.Items, i => i.Value == "urgent");
        Assert.Contains(priority.Items, i => i.Value == "normal" && i.IsDefault);

        var status = sets.FirstOrDefault(s => s.SetName == "status");
        Assert.NotNull(status);
        Assert.Equal(3, status!.Items.Count);
        Assert.Contains(status.Items, i => i.Value == "draft" && i.IsDefault);
        Assert.Contains(status.Items, i => i.Value == "active");
    }

    // ============================================================
    // 8. 组合类型 Specs + Brand 存在
    // ============================================================

    [Fact]
    public async Task CompositeTypes_SpecsAndBrand_ExistWithFields()
    {
        await using var db = CreateDb();
        var types = await db.CompositeTypes
            .Include(t => t.Fields)
            .AsNoTracking()
            .Where(t => t.EntityType == StringTreeDemoSeeder.SpaceEntityType && !t.IsDeleted)
            .ToListAsync();

        var brand = types.FirstOrDefault(t => t.TypeName == "Brand");
        Assert.NotNull(brand);
        Assert.Equal(2, brand!.Fields.Count(f => !f.IsDeleted));
        Assert.Contains(brand.Fields, f => f.FieldName == "name");

        var specs = types.FirstOrDefault(t => t.TypeName == "Specs");
        Assert.NotNull(specs);
        var specFields = specs!.Fields.Where(f => !f.IsDeleted).ToList();
        Assert.Equal(3, specFields.Count);
        Assert.Contains(specFields, f => f.FieldName == "color");
        Assert.Contains(specFields, f => f.FieldName == "weight" && f.UnitId.HasValue);
        Assert.Contains(specFields, f => f.FieldName == "brand" && f.RefCompositeTypeId == brand.CompositeTypeId);
    }

    // ============================================================
    // 9. 自定义表 certifications + 列
    // ============================================================

    [Fact]
    public async Task CustomTable_Certifications_ExistsWithColumns()
    {
        await using var db = CreateDb();
        var tables = await db.CustomTables
            .Include(t => t.Columns)
            .AsNoTracking()
            .Where(t => t.EntityType == StringTreeDemoSeeder.SpaceEntityType)
            .ToListAsync();

        var certs = tables.FirstOrDefault(t => t.TableName == "certifications");
        Assert.NotNull(certs);
        Assert.Equal(3, certs!.Columns.Count);
        Assert.Contains(certs.Columns, c => c.ColumnName == "cert_name" && c.IsUnique);
        Assert.Contains(certs.Columns, c => c.ColumnName == "issuer");
        Assert.Contains(certs.Columns, c => c.ColumnName == "issued_date");
    }

    // ============================================================
    // 10. 自定义表行（demo-node-001 的 2 行证书）
    // ============================================================

    [Fact]
    public async Task CustomTableRows_TwoCerts_ForDemoNode001()
    {
        await using var db = CreateDb();
        var rows = await db.CustomTableRows
            .AsNoTracking()
            .Where(r => r.ParentEntityType == StringTreeDemoSeeder.SpaceEntityType
                     && r.ParentEntityId == "demo-node-001")
            .OrderBy(r => r.RowOrder)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(0, rows[0].RowOrder);
        Assert.Equal(1, rows[1].RowOrder);

        // 第 0 行：3C 认证
        var row0 = JsonDocument.Parse(rows[0].RowData.RootElement.GetRawText()).RootElement;
        Assert.Equal("3C 认证", row0.GetProperty("cert_name").GetString());
        Assert.Equal("中国质量认证中心", row0.GetProperty("issuer").GetString());

        // 第 1 行：CE 认证
        var row1 = JsonDocument.Parse(rows[1].RowData.RootElement.GetRawText()).RootElement;
        Assert.Equal("CE 认证", row1.GetProperty("cert_name").GetString());
        Assert.Equal("欧盟标准委员会", row1.GetProperty("issuer").GetString());
    }

    // ============================================================
    // 11. API 端到端：GET /spaces 返回演示空间
    // ============================================================

    [Fact]
    public async Task Api_GetSpaces_ReturnsDemoSpace()
    {
        var resp = await Factory.CreateClient().GetAsync("/api/string-tree/spaces");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<StringTreeResponse<List<SpaceDto>>>(JsonOpt);
        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);

        var demo = body.Data!.FirstOrDefault(s => s.Id == StringTreeDemoSeeder.SpaceNodeId);
        Assert.NotNull(demo);
        Assert.Equal("演示空间", demo!.Name);
        Assert.Equal(StringTreeDemoSeeder.SpaceEntityType, demo.EntityType);
    }

    // ============================================================
    // 12. API 端到端：GET /entities/{id} 返回属性
    // ============================================================

    [Fact]
    public async Task Api_GetEntity_ReturnsAllProperties()
    {
        var resp = await Factory.CreateClient()
            .GetAsync($"/api/eav/{StringTreeDemoSeeder.SpaceEntityType}/entities/demo-node-001");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<DynamicEntityDto>(JsonOpt);
        Assert.NotNull(body);
        Assert.Equal("demo-node-001", body!.EntityId);
        Assert.Equal(StringTreeDemoSeeder.SpaceEntityType, body.EntityType);

        // 至少应有这些键
        var keys = body.Properties.Keys;
        Assert.Contains("title", keys);
        Assert.Contains("quantity", keys);
        Assert.Contains("price", keys);
        Assert.Contains("isActive", keys);
        Assert.Contains("priority", keys);
        Assert.Contains("status", keys);
        Assert.Contains("specs", keys);
        Assert.Contains("metadata", keys);
        Assert.Contains("attachment", keys);

        // title 值正确
        Assert.Equal("旗舰手机", body.Properties["title"].GetString());

        // ★ attachment 是 file 类型，返回 JsonElement（ValueFileMeta 整段）
        //    全字段断言（与 #21 镜像）：docId/fileName/contentType/size/sha256/version/status
        var att = body.Properties["attachment"];
        Assert.Equal(JsonValueKind.Object, att.ValueKind);
        Assert.Equal("demo-doc-001",           att.GetProperty("docId").GetString());
        Assert.Equal("手机使用说明书.pdf",      att.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf",         att.GetProperty("contentType").GetString());
        Assert.Equal(524288L,                  att.GetProperty("size").GetInt64());
        Assert.Equal("a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
                     att.GetProperty("sha256").GetString());
        Assert.Equal(1,                        att.GetProperty("version").GetInt32());
        Assert.Equal("active",                 att.GetProperty("status").GetString());
    }

    // ============================================================
    // 13. API 端到端：GET /schema 返回属性定义
    // ============================================================

    [Fact]
    public async Task Api_GetSchema_ReturnsAllAttributeDefinitions()
    {
        var resp = await Factory.CreateClient()
            .GetAsync($"/api/eav/{StringTreeDemoSeeder.SpaceEntityType}/schema");
        resp.EnsureSuccessStatusCode();

        // 直接拿原始字符串解析——避免 camelCase / PascalCase 假设
        var rawJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        // EntityType 字段（PascalCase 序列化）
        var entityTypeProp = root.EnumerateObject()
            .FirstOrDefault(p => p.NameEquals("EntityType") || p.NameEquals("entityType"));
        Assert.Equal(StringTreeDemoSeeder.SpaceEntityType, entityTypeProp.Value.GetString());

        // Attributes 数组
        var attrsProp = root.EnumerateObject()
            .FirstOrDefault(p => p.NameEquals("Attributes") || p.NameEquals("attributes"));
        var attrs = attrsProp.Value.EnumerateArray().ToList();
        Assert.Equal(15, attrs.Count);

        // 验证 priority 引用 optionSet——通过遍历找到 attributeName=priority 的元素
        var priorityRaw = attrs.FirstOrDefault(a =>
        {
            foreach (var p in a.EnumerateObject())
            {
                if ((p.NameEquals("attributeName") || p.NameEquals("AttributeName"))
                    && p.Value.ValueKind == JsonValueKind.String
                    && p.Value.GetString() == "priority")
                    return true;
            }
            return false;
        });
        Assert.NotEqual(default(JsonElement), priorityRaw);

        // 该 priority 元素应包含 optionSet 字段
        var hasOptionSet = priorityRaw.EnumerateObject()
            .Any(p => (p.NameEquals("optionSet") || p.NameEquals("OptionSet"))
                   && p.Value.ValueKind == JsonValueKind.Object);
        Assert.True(hasOptionSet, "priority 属性应引用 optionSet");
    }

    // ============================================================
    // 14. 多节点共享 EntityType，但属性实例独立
    //     4 个节点（demo-node-001/002/003/004）共用同一套 15 个
    //     AttributeDefinition，但每个节点有独立的 13 条 AttributeValue
    //     （以 EntityId 区分）。同属性在不同节点上值各异。
    // ============================================================

    [Fact]
    public async Task MultiNodes_ShareEntityType_ButHaveIndependentAttributeValues()
    {
        await using var db = CreateDb();
        var nodeIds = new[] { "demo-node-001", "demo-node-002", "demo-node-003", "demo-node-004" };

        // 1. 拉取所有 4 个节点的属性值（用 AttributeId 关联到 AttributeDefinition 取名字）
        var rows = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && nodeIds.Contains(v.EntityId))
            .Join(db.AttributeCatalog,
                  v => v.AttributeId,
                  a => a.AttributeId,
                  (v, a) => new
                  {
                      v.EntityId,
                      v.EntityType,
                      v.AttributeId,
                      AttributeName = a.AttributeName,
                      v.ValueString,
                      v.ValueInt,
                      v.ValueDecimal,
                      v.ValueBool
                  })
            .ToListAsync();

        // 2. 所有 4 个节点共享同一个 EntityType
        var entityTypes = rows.Select(r => r.EntityType).Distinct().ToList();
        Assert.Single(entityTypes);
        Assert.Equal(StringTreeDemoSeeder.SpaceEntityType, entityTypes[0]);

        // 3. 每个节点都有完整的 14 条属性值（15 个属性中除 table 外都写入，含 attachment）
        foreach (var id in nodeIds)
        {
            var count = rows.Count(r => r.EntityId == id);
            Assert.True(count >= 14, $"{id} 应 ≥14 条属性值，实际 {count}");
        }

        // 4. 总行数 = 4 节点 × 14 属性 = 56（说明每节点都有完整独立的实例集合）
        Assert.Equal(56, rows.Count);

        // 5. 每个属性在 4 个节点各有一行（不重复，EntityId 不同）
        var attrGroups = rows.GroupBy(r => r.AttributeId).ToList();
        Assert.Equal(14, attrGroups.Count); // 14 个属性都写了值（table 走 CustomTableRow 不计入）
        foreach (var grp in attrGroups)
        {
            Assert.Equal(4, grp.Count());
            Assert.Equal(4, grp.Select(r => r.EntityId).Distinct().Count());
        }

        // 6. 同属性在不同节点上的值不同（用 title 验证 string 类型）
        var titles = rows
            .Where(r => r.AttributeName == "title")
            .Select(r => r.ValueString)
            .Distinct()
            .ToList();
        Assert.Equal(4, titles.Count);
        Assert.Contains("旗舰手机", titles);
        Assert.Contains("超薄笔记本", titles);
        Assert.Contains("实木办公桌", titles);
        Assert.Contains("iPhone 15 Pro", titles);

        // 7. 同属性在不同节点上的值不同（用 priority 验证 single_choice 类型）
        var priorities = rows
            .Where(r => r.AttributeName == "priority")
            .Select(r => r.ValueString)
            .Distinct()
            .ToList();
        Assert.Equal(4, priorities.Count);
        Assert.Contains("high", priorities);
        Assert.Contains("normal", priorities);
        Assert.Contains("low", priorities);
        Assert.Contains("urgent", priorities);

        // 8. 同属性在不同节点上的值不同（用 quantity 验证 int 类型）
        var quantities = rows
            .Where(r => r.AttributeName == "quantity")
            .Select(r => r.ValueInt)
            .Distinct()
            .ToList();
        Assert.Equal(4, quantities.Count);
        Assert.Contains(100L, quantities);
        Assert.Contains(50L, quantities);
        Assert.Contains(20L, quantities);
        Assert.Contains(30L, quantities);

        // 9. 同属性在不同节点上的值不同（用 price 验证 decimal 类型）
        var prices = rows
            .Where(r => r.AttributeName == "price")
            .Select(r => r.ValueDecimal)
            .Distinct()
            .ToList();
        Assert.Equal(4, prices.Count);
        Assert.Contains(5999.00m, prices);
        Assert.Contains(12999.00m, prices);
        Assert.Contains(1899.00m, prices);
        Assert.Contains(7999.00m, prices);

        // 10. 同属性在不同节点上的值不同（用 isActive 验证 bool 类型）
        //     3 节点 true、1 节点 false → 不同实例的同一属性可以是相同值，但行是独立的
        var actives = rows
            .Where(r => r.AttributeName == "isActive")
            .Select(r => r.ValueBool ?? false)
            .ToList();
        Assert.Equal(4, actives.Count);
        Assert.Equal(3, actives.Count(b => b));
        Assert.Equal(1, actives.Count(b => !b));
    }

    // ============================================================
    // 15. 单个节点的值改动不影响其他节点（实例隔离）
    //     模拟：单独查询 demo-node-001 的 title，不应返回其他节点的值
    // ============================================================

    [Fact]
    public async Task SingleNode_QueryAttribute_DoesNotIncludeOtherNodes()
    {
        await using var db = CreateDb();

        var titleAttrId = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && a.AttributeName == "title")
            .Select(a => a.AttributeId)
            .FirstAsync();

        // 只查 demo-node-001 的 title
        var node001Value = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && v.EntityId == "demo-node-001"
                     && v.AttributeId == titleAttrId)
            .FirstOrDefaultAsync();

        Assert.NotNull(node001Value);
        Assert.Equal("旗舰手机", node001Value!.ValueString);

        // 验证同属性的 demo-node-002 是不同的行（不同 EntityId、不同 ValueString）
        var node002Value = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && v.EntityId == "demo-node-002"
                     && v.AttributeId == titleAttrId)
            .FirstOrDefaultAsync();

        Assert.NotNull(node002Value);
        Assert.NotEqual(node001Value.EntityId, node002Value!.EntityId);
        Assert.NotEqual(node001Value.ValueString, node002Value.ValueString);
        Assert.NotEqual(node001Value.ValueId, node002Value.ValueId);
    }

    // ============================================================
    // 16. 第二个 Space（demo-space-002）存在且 EntityType 独占
    // ============================================================

    [Fact]
    public async Task SecondSpace_Exists_WithDifferentEntityType()
    {
        await using var db = CreateDb();
        var space2 = await db.StringTreeSkyNodes
            .AsNoTracking()
            .FirstAsync(n => n.Id == StringTreeDemoSeeder.SecondSpaceNodeId);

        Assert.Equal("演示空间二", space2.Name);
        Assert.Null(space2.ParentId);
        Assert.Equal(StringTreeDemoSeeder.SecondSpaceEntityType, space2.EntityType);

        // 两个 Space 的 EntityType 必须不同
        Assert.NotEqual(StringTreeDemoSeeder.SpaceEntityType,
                       StringTreeDemoSeeder.SecondSpaceEntityType);
    }

    // ============================================================
    // 17. 第二个 Space 有 3 个节点（根 + 2 子，满足"最少三个 node"）
    // ============================================================

    [Fact]
    public async Task SecondSpace_HasThreeNodes_WithCorrectHierarchy()
    {
        await using var db = CreateDb();
        var nodes = await db.StringTreeSkyNodes
            .AsNoTracking()
            .Where(n => n.EntityType == StringTreeDemoSeeder.SecondSpaceEntityType)
            .ToListAsync();

        Assert.Equal(3, nodes.Count);

        var roots = nodes.Where(n => n.ParentId == null).ToList();
        Assert.Single(roots);
        Assert.Equal(StringTreeDemoSeeder.SecondSpaceNodeId, roots[0].Id);

        Assert.Contains(nodes, n => n.Id == "demo-node-101"
                                  && n.ParentId == StringTreeDemoSeeder.SecondSpaceNodeId);
        Assert.Contains(nodes, n => n.Id == "demo-node-102"
                                  && n.ParentId == StringTreeDemoSeeder.SecondSpaceNodeId);
    }

    // ============================================================
    // 18. ★ 同名属性在两个 EntityType 下是不同的 AttributeDefinition
    //     验证 EAV 独占性：title / quantity / priority / status / weight
    //     在 demo-space-001 和 demo-space-002 下各有独立的属性定义行
    // ============================================================

    [Fact]
    public async Task SameAttributeName_AcrossEntityTypes_AreDifferentDefinitions()
    {
        await using var db = CreateDb();
        var sharedNames = new[] { "title", "quantity", "priority", "status", "weight" };

        var space1Attrs = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && sharedNames.Contains(a.AttributeName) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.AttributeName);

        var space2Attrs = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SecondSpaceEntityType
                     && sharedNames.Contains(a.AttributeName) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.AttributeName);

        // 两个 EntityType 都有这 5 个同名属性
        Assert.Equal(5, space1Attrs.Count);
        Assert.Equal(5, space2Attrs.Count);

        // ★ 关键断言：同名属性的 AttributeId 在两个 EntityType 下不同（独立定义行）
        foreach (var name in sharedNames)
        {
            Assert.NotEqual(space1Attrs[name].AttributeId,
                            space2Attrs[name].AttributeId);
            // EntityType 字段也不同
            Assert.NotEqual(space1Attrs[name].EntityType,
                            space2Attrs[name].EntityType);
        }

        // ★ priority 属性引用的 OptionSetId 也不同（独立选项集）
        Assert.NotEqual(space1Attrs["priority"].RefOptionSetId,
                        space2Attrs["priority"].RefOptionSetId);

        // ★ status 属性的引用配置不同：
        //   demo-space-001 的 status 引用选项集；demo-space-002 的 status 不引用
        Assert.NotNull(space1Attrs["status"].RefOptionSetId);
        Assert.Null(space2Attrs["status"].RefOptionSetId);
    }

    // ============================================================
    // 19. ★ 同名选项集 priority 在两个 EntityType 下是不同 OptionSet
    //     且选项值不同（low/normal/high/urgent vs p1/p2/p3）
    // ============================================================

    [Fact]
    public async Task SameOptionSetName_AcrossEntityTypes_AreDifferentSets()
    {
        await using var db = CreateDb();
        var sets1 = await db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && s.SetName == "priority")
            .ToListAsync();

        var sets2 = await db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.EntityType == StringTreeDemoSeeder.SecondSpaceEntityType
                     && s.SetName == "priority")
            .ToListAsync();

        Assert.Single(sets1);
        Assert.Single(sets2);

        // ★ OptionSetId 不同（独立行）
        Assert.NotEqual(sets1[0].OptionSetId, sets2[0].OptionSetId);

        // 选项值集合不同：space1 是 low/normal/high/urgent（4 个）
        //                 space2 是 p1/p2/p3（3 个）
        Assert.Equal(4, sets1[0].Items.Count);
        Assert.Equal(3, sets2[0].Items.Count);

        var values1 = sets1[0].Items.Select(i => i.Value).OrderBy(v => v).ToList();
        var values2 = sets2[0].Items.Select(i => i.Value).OrderBy(v => v).ToList();
        Assert.Equal(new[] { "high", "low", "normal", "urgent" }, values1);
        Assert.Equal(new[] { "p1", "p2", "p3" }, values2);
    }

    // ============================================================
    // 20. ★ 两个 Space 的属性值互不干扰
    //     demo-node-101 写入的 title 是 "Galaxy Watch"，
    //     不影响 demo-node-001 的 "旗舰手机"。
    //     虽然两个 EntityType 都有 title 属性，但 EntityId 不同 + EntityType 不同
    //     → 完全独立的 AttributeValue 行。
    // ============================================================

    [Fact]
    public async Task AttributeValues_AcrossEntityTypes_DoNotInterfere()
    {
        await using var db = CreateDb();
        var space2NodeIds = new[] { "demo-node-101", "demo-node-102" };

        var rows = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SecondSpaceEntityType
                     && space2NodeIds.Contains(v.EntityId))
            .Join(db.AttributeCatalog,
                  v => v.AttributeId,
                  a => a.AttributeId,
                  (v, a) => new
                  {
                      v.EntityId,
                      v.EntityType,
                      v.AttributeId,
                      AttributeName = a.AttributeName,
                      v.ValueString,
                      v.ValueInt,
                      v.ValueDecimal
                  })
            .ToListAsync();

        // 10 条独立属性值（demo-node-101 + demo-node-102 各 5 个）
        Assert.Equal(10, rows.Count);
        Assert.All(rows, r =>
            Assert.Equal(StringTreeDemoSeeder.SecondSpaceEntityType, r.EntityType));

        // demo-node-101 的 title 是 "Galaxy Watch"
        var node101Title = rows.First(r =>
            r.EntityId == "demo-node-101" && r.AttributeName == "title");
        Assert.Equal("Galaxy Watch", node101Title.ValueString);

        // demo-node-102 的 title 是 "iPad Air"
        var node102Title = rows.First(r =>
            r.EntityId == "demo-node-102" && r.AttributeName == "title");
        Assert.Equal("iPad Air", node102Title.ValueString);

        // ★ 跨 EntityType 同名属性值对比：
        //   demo-node-001（Space1）title = "旗舰手机"
        //   demo-node-101（Space2）title = "Galaxy Watch"
        //   两条值完全独立（不同 AttributeId、不同 EntityType、不同 EntityId）
        var space1Title = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                     && v.EntityId == "demo-node-001")
            .Join(db.AttributeCatalog,
                  v => v.AttributeId,
                  a => a.AttributeId,
                  (v, a) => new { a.AttributeName, v.ValueString, v.AttributeId, v.EntityType })
            .FirstAsync(r => r.AttributeName == "title");

        Assert.NotEqual(space1Title.AttributeId,    node101Title.AttributeId);
        Assert.NotEqual(space1Title.EntityType,    node101Title.EntityType);
        Assert.NotEqual(space1Title.ValueString,   node101Title.ValueString);
    }

    // ============================================================
    // 21. ★ file 属性元数据写入（demo-node-001 的 attachment）
    //     验证 ValueFileMeta JSON 与 FileMetadataDto 对齐：
    //     docId / fileName / contentType / size / sha256 / version / status
    //
    // ★ 设计说明：
    //   - seeder 直接构造元数据 JSON 写入，不调用 FileStorageApi 二进制上下传
    //   - 测试环境（EavApiFactory）不启动 FileStorageApi 微服务
    //   - 真正的上下传集成测试应在 FileStorageApiTests 项目（需 SeaweedFS 容器）
    // ============================================================

    [Fact]
    public async Task FileAttribute_Metadata_Written_ForDemoNode001()
    {
        await using var db = CreateDb();
        var attachAttr = await db.AttributeCatalog
            .AsNoTracking()
            .FirstAsync(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType
                          && a.AttributeName == "attachment");

        // 断言属性定义正确
        Assert.Equal(EavDataTypes.File, attachAttr.DataType);

        // 取 demo-node-001 的 attachment 值
        var value = await db.AttributeValues
            .AsNoTracking()
            .FirstAsync(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                          && v.EntityId == "demo-node-001"
                          && v.AttributeId == attachAttr.AttributeId);

        // ValueFileMeta 不为 null
        Assert.NotNull(value.ValueFileMeta);

        // 解析 JSON 字段（与 FileMetadataDto 对齐）
        var root = value.ValueFileMeta!.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("demo-doc-001",       root.GetProperty("docId").GetString());
        Assert.Equal("手机使用说明书.pdf", root.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf",    root.GetProperty("contentType").GetString());
        Assert.Equal(524288L,             root.GetProperty("size").GetInt64());
        Assert.Equal("a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
                     root.GetProperty("sha256").GetString());
        Assert.Equal(1,                    root.GetProperty("version").GetInt32());
        Assert.Equal("active",             root.GetProperty("status").GetString());

        // 其他类型化列应为 null（file 类型只写 ValueFileMeta）
        Assert.Null(value.ValueString);
        Assert.Null(value.ValueInt);
        Assert.Null(value.ValueDecimal);
        Assert.Null(value.ValueBool);
        Assert.Null(value.ValueJsonb);
    }

    // ============================================================
    // 22. ★ 多节点 file 属性元数据独立
    //     4 个节点（demo-node-001/002/003/004）的 attachment 各自独立：
    //       - DocId 各异（demo-doc-001/002/003/004）
    //       - FileName 各异
    //       - Size 各异
    //       - ValueId 各异（不同 AttributeValue 行）
    //     证明：同 EntityType + 同 AttributeId 下，多节点 file 属性实例完全独立。
    // ============================================================

    [Fact]
    public async Task FileAttribute_Metadata_Differs_AcrossNodes()
    {
        await using var db = CreateDb();
        var nodeIds = new[] { "demo-node-001", "demo-node-002", "demo-node-003", "demo-node-004" };

        var attachAttrId = await db.AttributeCatalog
            .AsNoTracking()
            .Where(a => a.EntityType == StringTreeDemoSeeder.SpaceEntityType
                      && a.AttributeName == "attachment")
            .Select(a => a.AttributeId)
            .FirstAsync();

        var values = await db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                      && v.AttributeId == attachAttrId
                      && nodeIds.Contains(v.EntityId))
            .ToListAsync();

        // 4 个节点都有 attachment 属性值
        Assert.Equal(4, values.Count);

        // 4 个 ValueId 各异（独立行）
        var valueIds = values.Select(v => v.ValueId).Distinct().ToList();
        Assert.Equal(4, valueIds.Count);

        // 解析每个节点的元数据
        var metas = values.ToDictionary(
            v => v.EntityId,
            v => v.ValueFileMeta!.RootElement);

        // DocId 各异
        var docIds = metas.Select(kv => kv.Value.GetProperty("docId").GetString()).Distinct().ToList();
        Assert.Equal(4, docIds.Count);
        Assert.Contains("demo-doc-001", docIds);
        Assert.Contains("demo-doc-002", docIds);
        Assert.Contains("demo-doc-003", docIds);
        Assert.Contains("demo-doc-004", docIds);

        // FileName 各异
        var fileNames = metas.Select(kv => kv.Value.GetProperty("fileName").GetString()).Distinct().ToList();
        Assert.Equal(4, fileNames.Count);

        // Size 各异
        var sizes = metas.Select(kv => kv.Value.GetProperty("size").GetInt64()).Distinct().ToList();
        Assert.Equal(4, sizes.Count);
        Assert.Contains(524288L,   sizes);
        Assert.Contains(262144L,   sizes);
        Assert.Contains(1572864L,  sizes);
        Assert.Contains(2097152L,  sizes);

        // ContentType 至少包含 2 种（pdf / jpeg / png）
        var contentTypes = metas
            .Select(kv => kv.Value.GetProperty("contentType").GetString())
            .Distinct()
            .ToList();
        Assert.True(contentTypes.Count >= 2, $"至少应有 2 种 contentType，实际 {contentTypes.Count}");

        // 抽查：demo-node-004 是 iPhone 钛金属特写 PNG
        Assert.Equal("image/png",      metas["demo-node-004"].GetProperty("contentType").GetString());
        Assert.Equal("iPhone钛金属特写.png", metas["demo-node-004"].GetProperty("fileName").GetString());
    }

    // ============================================================
    // 23. ★ API PATCH round-trip：FileMetadataDto-shaped attachment 经 EAV API 往返保持
    //
    // 验证后端契约（EavController.Patch → EavWriteService.ApplyChangesAsync）：
    //   - PATCH /api/eav/{entityType}/entities/{id} 接受仅 attachment 一键的部分更新
    //   - 服务端把 JsonElement 原样存入 ValueFileMeta（无 shape 校验、无投影）
    //   - GET 回读 7 字段全部保持（docId/fileName/contentType/size/sha256/version/status）
    //
    // 策略：新建 GUID id 的 StringTree 节点（满足 owner guard + GUID 校验）
    //   → PATCH attachment → GET 断言 7 字段 → finally 删除节点 + EAV 清理。
    //   - EavWriteService 要求 entityId 为 GUID 格式（L103-L106）；
    //     seeder 的 demo-node-* 非 GUID，故必须新建 GUID 节点。
    //   - PATCH 模式（fullReplace=false）下未提供的键不参与必填检查，仅 attachment 即可。
    //   - EavApiFactory 不启动 FileStorageApi，本测试只验证 EAV API 对 file 元数据 JSON 的
    //     透明往返，不涉及真实二进制上下传。
    // ============================================================

    [Fact]
    public async Task Api_PatchEntity_RoundTripsFileAttachmentMetadata()
    {
        var client = Factory.CreateClient();
        var guidId = Guid.NewGuid().ToString();

        // 1. 在 StringTreeSkyNodes 插入 GUID 节点（满足 owner guard + EavWriteService GUID 校验）
        await using (var db = CreateDb())
        {
            db.StringTreeSkyNodes.Add(new StringNodeEntity
            {
                Id = guidId,
                Name = "round-trip-test",
                ParentId = StringTreeDemoSeeder.SpaceNodeId,
                EntityType = StringTreeDemoSeeder.SpaceEntityType
            });
            await db.SaveChangesAsync();
        }

        var url = $"/api/eav/{StringTreeDemoSeeder.SpaceEntityType}/entities/{guidId}";

        try
        {
            // 2. PATCH attachment（仅 attachment 一键，PATCH 模式不检查其他必填字段）
            var newMeta = JsonSerializer.SerializeToElement(new
            {
                docId = "put-test-doc",
                fileName = "round-trip.pdf",
                contentType = "application/pdf",
                size = 1024L,
                sha256 = "abc123",
                version = 2,
                status = "active",
            });
            var payload = new Dictionary<string, object?> { ["attachment"] = newMeta };
            var patch = await client.PatchAsJsonAsync(url, payload);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, patch.StatusCode);

            // 3. GET 验证 7 字段全部保持
            var get = await client.GetAsync(url);
            get.EnsureSuccessStatusCode();
            var body = await get.Content.ReadFromJsonAsync<DynamicEntityDto>(JsonOpt);
            Assert.NotNull(body);

            var att = body!.Properties["attachment"];
            Assert.Equal(JsonValueKind.Object, att.ValueKind);
            Assert.Equal("put-test-doc",    att.GetProperty("docId").GetString());
            Assert.Equal("round-trip.pdf",  att.GetProperty("fileName").GetString());
            Assert.Equal("application/pdf", att.GetProperty("contentType").GetString());
            Assert.Equal(1024L,            att.GetProperty("size").GetInt64());
            Assert.Equal("abc123",          att.GetProperty("sha256").GetString());
            Assert.Equal(2,                att.GetProperty("version").GetInt32());
            Assert.Equal("active",          att.GetProperty("status").GetString());
        }
        finally
        {
            // 4. 清理：删 EAV 属性值 + 删节点
            await using var db2 = CreateDb();
            var node = await db2.StringTreeSkyNodes.FindAsync(guidId);
            if (node is not null)
            {
                var vals = await db2.AttributeValues
                    .Where(v => v.EntityType == StringTreeDemoSeeder.SpaceEntityType
                              && v.EntityId == guidId)
                    .ToListAsync();
                db2.AttributeValues.RemoveRange(vals);
                db2.StringTreeSkyNodes.Remove(node);
                await db2.SaveChangesAsync();
            }
        }
    }
}
