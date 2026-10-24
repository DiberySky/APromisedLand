using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 删除原则的集成测试：所有「移除/删除」一律不采用级联删除，子项为空方可删除本项。
///   - 自定义表：仍有列 / 行数据 → 400
///   - 自定义表列：行数据中已有该列的值 → 400
///   - 组合类型：仍有字段 → 400
///   - 属性：已产生实例数据 → 400
///   - 选项集：仍有活动选项 → 400（见 OptionSetSoftDeleteTests）
///   - 边类型：仍有边实例 → 409（见 EdgeTests）
///   - 边实例：自身仍有属性数据 → 409（边实例有独立行，属性值在外部表）
///   - 树节点：仍有子节点 / 被边引用 / 仍有挂载实体数据 → 409
///   - 线路节点：被边引用 / 仍有挂载实体数据 → 409
///   - 线路：仍有线路节点 / 挂载实体数据 → 409
///   - 类型声明（inode_entitytypes）不含业务数据：宿主删除时随宿主一并清理，不留孤儿声明
/// 被拒绝时本项与其子项均保持原状（不做任何级联清理）。
/// </summary>
public class DeletePrincipleTests : IntegrationTestBase
{
    private const string EntityType = "DelPrinciple";

    public DeletePrincipleTests(EavApiFactory factory) : base(factory) { }

    private async Task EnsureEntityTypeAsync()
    {
        await TestData.EnsureSchemaAsync(Client);
        await TestData.EnsureEntityTypeAsync(EntityType, "删除原则测试");
    }

    // ---------- 元数据构造辅助 ----------

    private async Task<string> CreateTableAsync(string tableName)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/custom-tables",
            new { entityType = EntityType, tableName, displayName = tableName });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<TableIdResponse>())!.TableDefinitionId;
    }

    private async Task<string> CreateTableColumnAsync(
        string tableId, string columnName)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns",
            new { columnName, displayName = columnName, dataType = "string", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<ColumnIdResponse>())!.ColumnId;
    }

    private async Task<string> CreateTableAttributeAsync(string attributeName, string tableId)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = EntityType,
                attributeName,
                displayName = attributeName,
                dataType = "table",
                refTableDefinitionId = tableId
            });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<AttrIdResponse>())!.AttributeId;
    }

    private async Task<string> CreateStringAttributeAsync(string attributeName)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = EntityType,
                attributeName,
                displayName = attributeName,
                dataType = "string"
            });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<AttrIdResponse>())!.AttributeId;
    }

    private async Task<string> CreateCompositeTypeAsync(string typeName)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/composite-types",
            new { entityType = EntityType, typeName, displayName = typeName });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<CompositeTypeIdResponse>())!.CompositeTypeId;
    }

    private async Task AddCompositeFieldAsync(string typeId, string fieldName)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/composite-types/{typeId}/fields",
            new { fieldName, displayName = fieldName, dataType = "string", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    private async Task<CustomTableDetailDto> GetTableAsync(string tableId)
    {
        var resp = await Client.GetAsync($"/api/eav/metadata/custom-tables/{tableId}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadEavAsync<CustomTableDetailDto>())!;
    }

    private async Task<CompositeTypeDetailDto> GetCompositeTypeAsync(string typeId)
    {
        var resp = await Client.GetAsync($"/api/eav/metadata/composite-types/{typeId}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadEavAsync<CompositeTypeDetailDto>())!;
    }

    // ============================================================
    // 自定义表：列 / 行数据
    // ============================================================

    [Fact]
    public async Task DeleteCustomTable_WhenColumnsRemain_Returns400()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("del_t_with_col");
        await CreateTableColumnAsync(tableId, "note");

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("列", await resp.Content.ReadAsStringAsync());

        // 表与列都还在（未级联软删）
        var table = await GetTableAsync(tableId);
        var col = table.Columns.Single(c => !c.IsDeleted);
        Assert.Equal("note", col.ColumnName);

        // 子项清空后即可删除本项
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{col.ColumnId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteTableColumn_WhenRowDataExists_Returns400()
    {
        await EnsureEntityTypeAsync();
        const string tableName = "del_t_col_data";
        var tableId = await CreateTableAsync(tableName);
        var colId = await CreateTableColumnAsync(tableId, "note");
        await CreateTableAttributeAsync(tableName, tableId);

        // 写一行，使该列产生数据
        var rowResp = await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{GuidFromInt(97401)}/tables/{tableName}/rows",
            new
            {
                rowId = (string?)null,
                rowOrder = 1,
                fields = new Dictionary<string, object?> { ["note"] = "x" }
            });
        Assert.Equal(HttpStatusCode.NoContent, rowResp.StatusCode);

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{colId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("行数据", await resp.Content.ReadAsStringAsync());

        // 列未被软删
        var table = await GetTableAsync(tableId);
        Assert.Contains(table.Columns, c => c.ColumnId == colId && !c.IsDeleted);
    }

    // ============================================================
    // 组合类型：字段
    // ============================================================

    [Fact]
    public async Task DeleteCompositeType_WhenFieldsRemain_Returns400()
    {
        await EnsureEntityTypeAsync();
        var typeId = await CreateCompositeTypeAsync("del_ct_with_field");
        await AddCompositeFieldAsync(typeId, "f1");

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/composite-types/{typeId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("字段", await resp.Content.ReadAsStringAsync());

        // 类型与字段都还在
        var type = await GetCompositeTypeAsync(typeId);
        Assert.False(type.IsDeleted);
        var field = type.Fields.Single(f => !f.IsDeleted);

        // 子项清空后即可删除本项
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/composite-types/{typeId}/fields/{field.FieldId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/composite-types/{typeId}")).StatusCode);
    }

    // ============================================================
    // 属性：实例数据
    // ============================================================

    [Fact]
    public async Task DeleteAttribute_WhenInstanceDataExists_Returns400()
    {
        await EnsureEntityTypeAsync();
        const string attributeName = "del_attr_val";
        var attrId = await CreateStringAttributeAsync(attributeName);

        await PutEntityAsync(EntityType, GuidFromInt(97402),
            new Dictionary<string, object?> { [attributeName] = "v" });

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{attrId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("实例数据", await resp.Content.ReadAsStringAsync());

        // 属性仍可用（未被软删）
        var listResp = await Client.GetAsync(
            $"/api/eav/metadata/attributes?entityType={EntityType}");
        listResp.EnsureSuccessStatusCode();
        var attrs = await listResp.Content.ReadEavAsync<List<AttributeDetailDto>>();
        var attr = attrs!.Single(a => a.AttributeId == attrId);
        Assert.False(attr.IsDeleted);
    }

    // ============================================================
    // 树节点：子节点
    // ============================================================

    [Theory]
    [InlineData("/UnitTree")]
    [InlineData("/CategoryTree")]
    [InlineData("/SpaceTree")]
    public async Task TreeNodeDelete_WhenChildrenExist_Returns409(string tree)
    {
        var parentId = $"P_{Guid.NewGuid():N}";
        var childId = $"C_{Guid.NewGuid():N}";

        var parentResp = await Client.PostAsJsonAsync(tree, new
        {
            id = parentId, text = "父", parentId = (string?)null,
            sortOrder = 0, canHaveChildren = true
        });
        parentResp.EnsureSuccessStatusCode();

        var childResp = await Client.PostAsJsonAsync(tree, new
        {
            id = childId, text = "子", parentId,
            sortOrder = 0, canHaveChildren = false
        });
        childResp.EnsureSuccessStatusCode();

        // 仍有子节点 → 409，不做级联删除
        var resp = await Client.DeleteAsync($"{tree}/{parentId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("子节点", await resp.Content.ReadAsStringAsync());

        // 父节点与其子节点都还在 → 都能正常删除
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"{tree}/{childId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"{tree}/{parentId}")).StatusCode);
    }

    // ============================================================
    // 边实例自身数据：先清空自身数据才允许删本项
    // （边实例有独立行，其 EAV 属性值存于外部表）
    // ============================================================

    private static string NewEdgeTypeName() => $"DelPrincipleEdge_{Guid.NewGuid():N}";

    /// <summary>创建自由节点（nodes 注册表），作为边端点。</summary>
    private async Task<string> CreateFreeNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var resp = await Client.PostAsJsonAsync("/api/Node", new { id, name });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<EdgeInstanceDto> CreateEdgeAsync(
        string typeName, string fromNodeId, string toNodeId)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId
            });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<EdgeInstanceDto>())!;
    }

    private async Task<string> CreateNodeAsync(string tree)
    {
        var id = $"{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync(tree, new
        {
            id,
            text = "n",
            parentId = (string?)null,
            sortOrder = 0,
            canHaveChildren = true
        });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    [Fact]
    public async Task EdgeInstanceDelete_WhenOwnPropertiesExist_Returns409()
    {
        await EnsureEntityTypeAsync();
        var typeName = NewEdgeTypeName();
        // R4：边类型名即 EAV entity_type，需先登记实体类型才能定义属性
        await TestData.EnsureEntityTypeAsync(typeName, typeName);
        var fromNode = await CreateFreeNodeAsync("删除起点");
        var toNode = await CreateFreeNodeAsync("删除终点");
        var edge = await CreateEdgeAsync(typeName, fromNode, toNode);

        // 在边类型上定义属性，并给该边实例写入自身属性值
        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = typeName,
                attributeName = "strength",
                displayName = "强度",
                dataType = "decimal",
                isRequired = false
            });
        attrResp.EnsureSuccessStatusCode();

        var put = await Client.PutAsJsonAsync(
            $"/api/eav/{typeName}/entities/{edge.EdgeId}", new { strength = 1.5 });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 自身数据非空 → 409，且不做级联删除
        var del = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Contains("属性数据", await del.Content.ReadAsStringAsync());

        // 边实例与其属性值都还在
        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{edge.EdgeId}");
        Assert.NotNull(got);
        Assert.True(got!.Properties!.ContainsKey("strength"));

        // 先清空自身数据 → 即可删除本项
        var clear = await Client.PutAsJsonAsync(
            $"/api/eav/{typeName}/entities/{edge.EdgeId}", new { });
        Assert.True(clear.IsSuccessStatusCode, await clear.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}")).StatusCode);
    }

    // ============================================================
    // 树节点：被边引用 / 自身挂载实体数据
    // ============================================================

    [Theory]
    [InlineData("/UnitTree")]
    [InlineData("/CategoryTree")]
    [InlineData("/SpaceTree")]
    public async Task TreeNodeDelete_WhenReferencedByEdge_Returns409(string tree)
    {
        var nodeId = await CreateNodeAsync(tree);
        var otherNode = await CreateFreeNodeAsync("边对端");
        var edge = await CreateEdgeAsync(NewEdgeTypeName(), nodeId, otherNode);

        var resp = await Client.DeleteAsync($"{tree}/{nodeId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("边引用", await resp.Content.ReadAsStringAsync());

        // 删掉引用该节点的边 → 节点即可删除
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/edges/{edge.EdgeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"{tree}/{nodeId}")).StatusCode);
    }

    [Fact]
    public async Task TreeNodeDelete_WhenAttachedEntityExists_Returns409()
    {
        await TestData.EnsureSchemaAsync(Client);
        await TestData.EnsureEntityTypeAsync(TestData.EntityType, "测试商品");
        var nodeId = await CreateNodeAsync("/UnitTree");

        // 在节点上挂载实体（自动创建映射 + 属性值）
        var put = await Client.PutAsJsonAsync(
            $"/api/inode/{nodeId}/entities/{TestData.EntityType}", new { amount = 1 });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 节点自身数据非空 → 409
        var resp = await Client.DeleteAsync($"/UnitTree/{nodeId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("实体数据", await resp.Content.ReadAsStringAsync());

        // 先清空节点自身数据 → 即可删除本项
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/inode/{nodeId}/entities/{TestData.EntityType}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/UnitTree/{nodeId}")).StatusCode);
    }

    // ============================================================
    // 宿主删除：类型声明（无业务数据）随宿主一并清理，不留孤儿声明
    // ============================================================

    [Fact]
    public async Task TreeNodeDelete_WhenOnlyDeclarationsExist_Succeeds_AndDeclarationsRemoved()
    {
        await TestData.EnsureSchemaAsync(Client);
        await TestData.EnsureEntityTypeAsync(TestData.EntityType, "测试商品");
        var nodeId = await CreateNodeAsync("/UnitTree");

        // 仅声明类型（不产生实体数据）
        var attach = await Client.PostAsync(
            $"/api/inode/{nodeId}/types/{TestData.EntityType}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var before = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{nodeId}/types");
        Assert.Single(before!);

        // 无实体数据 → 节点可删
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/UnitTree/{nodeId}")).StatusCode);

        // 声明已随宿主清理（不残留孤儿声明）
        var after = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{nodeId}/types");
        Assert.Empty(after!);
    }

    // ============================================================
    // 线路节点：自身挂载实体数据 → 先清空自身数据才允许删本项
    // ============================================================

    [Fact]
    public async Task LineNodeDelete_WhenAttachedEntityExists_Returns409()
    {
        await TestData.EnsureSchemaAsync(Client);
        await TestData.EnsureEntityTypeAsync(TestData.EntityType, "测试商品");

        // 线路 + 线路节点
        var lineId = $"L_{Guid.NewGuid():N}";
        var lineResp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "删除原则线路" });
        lineResp.EnsureSuccessStatusCode();
        var nodeId = $"LN_{Guid.NewGuid():N}";
        var nodeResp = await Client.PostAsJsonAsync(
            "/api/LineNode", new { id = nodeId, name = "删除原则杆塔", lineId });
        nodeResp.EnsureSuccessStatusCode();

        // 在线路节点上挂载实体
        var put = await Client.PutAsJsonAsync(
            $"/api/inode/{nodeId}/entities/{TestData.EntityType}", new { amount = 1 });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 节点自身数据非空 → 409，且不做级联删除
        var resp = await Client.DeleteAsync($"/api/LineNode/{nodeId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("实体数据", await resp.Content.ReadAsStringAsync());

        // 节点与其声明都还在
        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{nodeId}/types");
        Assert.Single(cards!);

        // 先清空节点自身数据 → 即可删除本项（声明随宿主清理）
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/inode/{nodeId}/entities/{TestData.EntityType}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/LineNode/{nodeId}")).StatusCode);

        var after = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{nodeId}/types");
        Assert.Empty(after!);

        // 清理线路
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/Lines/{lineId}")).StatusCode);
    }

    // ============================================================
    // 线路：自身挂载实体数据 → 先清空自身数据才允许删本项
    // ============================================================

    [Fact]
    public async Task LineDelete_WhenAttachedEntityExists_Returns409()
    {
        await TestData.EnsureSchemaAsync(Client);
        await TestData.EnsureEntityTypeAsync(TestData.EntityType, "测试商品");

        var lineId = $"L_{Guid.NewGuid():N}";
        var lineResp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "删除原则线路" });
        lineResp.EnsureSuccessStatusCode();

        // 线路上直接挂载实体
        var put = await Client.PutAsJsonAsync(
            $"/api/inode/{lineId}/entities/{TestData.EntityType}", new { amount = 1 });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 线路自身数据非空 → 409，且不做级联删除
        var resp = await Client.DeleteAsync($"/api/Lines/{lineId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("实体数据", await resp.Content.ReadAsStringAsync());

        // 先清空自身数据 → 即可删除本项（声明随宿主清理）
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/inode/{lineId}/entities/{TestData.EntityType}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/Lines/{lineId}")).StatusCode);

        var after = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{lineId}/types");
        Assert.Empty(after!);
    }

    private sealed record TableIdResponse(string TableDefinitionId);
    private sealed record ColumnIdResponse(string ColumnId);
    private sealed record AttrIdResponse(string AttributeId);
    private sealed record CompositeTypeIdResponse(string CompositeTypeId);
}
