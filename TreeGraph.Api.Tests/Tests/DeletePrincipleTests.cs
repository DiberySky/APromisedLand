using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 删除原则的集成测试：所有「移除/删除」一律不采用级联删除，子项为空方可删除本项。
///   - 自定义表：仍有列 / 行数据 → 400
///   - 自定义表列：行数据中已有该列的值 → 400
///   - 组合类型：仍有字段 → 400
///   - 属性：已产生实例数据 → 400
///   - 选项集：仍有活动选项 → 400（见 OptionSetHardDeleteTests）
///   - 关系值：仍作为起点 / 终点被引用 → 409
///   - 树节点：仍有子节点 / 被关系引用 / 仍有挂载实体数据 → 409
///   - 线路节点：被关系引用 / 仍有挂载实体数据 → 409
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

        // 表与列都还在（未级联删除）
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

        // 列未被删除
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

        await PutEntityAsync(GuidFromInt(97402),
            new Dictionary<string, object?> { [attributeName] = "v" });

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{attrId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("实例数据", await resp.Content.ReadAsStringAsync());

        // 属性仍可用（未被删除）
        var listResp = await Client.GetAsync(
            "/api/eav/metadata/attributes");
        listResp.EnsureSuccessStatusCode();
        var attrs = await listResp.Content.ReadEavAsync<List<AttributeDetailDto>>();
        var attr = attrs!.Single(a => a.AttributeId == attrId);
        Assert.Equal(attributeName, attr.AttributeName);
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
    // 树节点：被关系引用 / 自身挂载实体数据
    // ============================================================

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
            "/api/Lines", new { id = lineId, name = "删除原则线路", spaceId = TestSpaceId });
        lineResp.EnsureSuccessStatusCode();
        var nodeId = $"{Guid.NewGuid()}";
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

        // 节点与其声明都还在（挂载的测试实体 = 1 张卡；LineStructure 声明已随 relation 体系下线）
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
            "/api/Lines", new { id = lineId, name = "删除原则线路", spaceId = TestSpaceId });
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

    // ============================================================
    // 线路节点：被段边引用（from 或 to）→ 先拆边才允许删本项
    // ============================================================

    [Theory]
    [InlineData(true)]   // 删 from 端
    [InlineData(false)]  // 删 to 端
    public async Task LineNodeDelete_WhenSegmentEdgeReferences_Returns409(bool deleteFrom)
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var lineResp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "删除原则线路", spaceId = TestSpaceId });
        lineResp.EnsureSuccessStatusCode();

        var fromId = $"{Guid.NewGuid()}";
        var toId = $"{Guid.NewGuid()}";
        var n1 = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = fromId, lineId, name = "节点1",
            description = (string?)null, predecessorId = (string?)null
        });
        n1.EnsureSuccessStatusCode();
        var n2 = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = toId, lineId, name = "节点2",
            description = (string?)null, predecessorId = fromId
        });
        n2.EnsureSuccessStatusCode();

        // 段边任一端被引用 → 409，不做级联删除
        var targetId = deleteFrom ? fromId : toId;
        var resp = await Client.DeleteAsync($"/api/LineNode/{targetId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("边/段边", await resp.Content.ReadAsStringAsync());

        // 两端节点都还在
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/LineNode/{fromId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/LineNode/{toId}")).StatusCode);
    }

    // ============================================================
    // 线路：仍有线路节点（nodes.line_id 锚点）→ 先删节点才允许删线路
    // ============================================================

    [Fact]
    public async Task LineDelete_WhenNodesRemain_Returns409_ThenSucceedsAfterNodesRemoved()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var lineResp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "删除原则线路", spaceId = TestSpaceId });
        lineResp.EnsureSuccessStatusCode();

        var nodeId = $"{Guid.NewGuid()}";
        var nodeResp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name = "杆塔",
            description = (string?)null, predecessorId = (string?)null
        });
        nodeResp.EnsureSuccessStatusCode();

        // 仍有线路节点 → 409，不做级联删除
        var resp = await Client.DeleteAsync($"/api/Lines/{lineId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("仍有线路节点", await resp.Content.ReadAsStringAsync());

        // 线路与节点都还在；先删节点 → 即可删线路（双删 lines + nodes 注册行）
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/LineNode/{nodeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/Lines/{lineId}")).StatusCode);

        // nodes 注册行已删：再以该 lineId 建节点 → 400
        var recreate = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = $"{Guid.NewGuid()}", lineId, name = "杆塔",
            description = (string?)null, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, recreate.StatusCode);
    }

    private sealed record TableIdResponse(string TableDefinitionId);
    private sealed record ColumnIdResponse(string ColumnId);
    private sealed record AttrIdResponse(string AttributeId);
    private sealed record CompositeTypeIdResponse(string CompositeTypeId);
}
