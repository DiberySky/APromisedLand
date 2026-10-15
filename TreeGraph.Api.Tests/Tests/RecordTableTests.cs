using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 时序记录（record）的集成测试：
///   - 「时序记录」在建自定义表时选定，服务端自动预置时间维度列并落 IsRecordTable/HasTimeColumn
///   - 属性的 IsRecordMode 由所引表推导（引用时序表 → 自动为时序记录），可换绑、可多属性共享
///   - 时序表的时间维度列（第 1 列日期、可选第 2 列时间）锁定：不可删、不可调顺序
///   - 业务列（第 2 或第 3 列起）自由增删改
/// </summary>
public class RecordTableTests : IntegrationTestBase
{
    private const string EntityType = "RecordTest";

    public RecordTableTests(EavApiFactory factory) : base(factory) { }

    private async Task EnsureEntityTypeAsync()
    {
        await TestData.EnsureSchemaAsync(Client);   // 注入静态 client（幂等）
        await TestData.EnsureEntityTypeAsync(EntityType, "时序测试");
    }

    /// <summary>建自定义表；isRecordTable = true 时服务端预置时序列。</summary>
    private async Task<string> CreateTableAsync(
        string tableName, bool isRecordTable = false, bool hasTimeColumn = true)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/custom-tables",
            new
            {
                entityType = EntityType,
                tableName,
                displayName = tableName,
                isRecordTable,
                hasTimeColumn
            });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadEavAsync<TableIdResponse>())!.TableDefinitionId;
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

    private async Task<AttributeDetailDto> GetAttributeAsync(string attributeId)
    {
        var resp = await Client.GetAsync(
            $"/api/eav/metadata/attributes?entityType={EntityType}");
        var attrs = await resp.Content.ReadEavAsync<List<AttributeDetailDto>>();
        return attrs!.Single(a => a.AttributeId == attributeId);
    }

    private async Task<CustomTableDetailDto> GetTableAsync(string tableId)
    {
        var resp = await Client.GetAsync($"/api/eav/metadata/custom-tables/{tableId}");
        return (await resp.Content.ReadEavAsync<CustomTableDetailDto>())!;
    }

    // ---------- 建表：自动预置时序列 ----------

    [Fact]
    public async Task CreateRecordTable_PresetsDateAndTimeColumns()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_with_time", isRecordTable: true);

        var table = await GetTableAsync(tableId);
        Assert.True(table.IsRecordTable);
        Assert.True(table.HasTimeColumn);

        var active = table.Columns.Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder).ToList();
        Assert.Equal(2, active.Count);
        Assert.Equal("date", active[0].DataType);
        Assert.Equal(0, active[0].DisplayOrder);
        Assert.Equal("time", active[1].DataType);
        Assert.Equal(1, active[1].DisplayOrder);
    }

    [Fact]
    public async Task CreateRecordTable_WithoutTimeColumn_PresetsDateOnly()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync(
            "rec_t_no_time", isRecordTable: true, hasTimeColumn: false);

        var table = await GetTableAsync(tableId);
        Assert.True(table.IsRecordTable);
        Assert.False(table.HasTimeColumn);

        var active = table.Columns.Where(c => !c.IsDeleted).ToList();
        Assert.Single(active);
        Assert.Equal("date", active[0].DataType);
    }

    [Fact]
    public async Task CreatePlainTable_NotMarkedAsRecord()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_plain");

        var table = await GetTableAsync(tableId);
        Assert.False(table.IsRecordTable);
        Assert.DoesNotContain(table.Columns, c => !c.IsDeleted);
    }

    // ---------- 属性：IsRecordMode 由所引表推导 ----------

    [Fact]
    public async Task CreateAttribute_ReferencingRecordTable_IsRecordMode()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_derive", isRecordTable: true);
        var attrId = await CreateTableAttributeAsync("attr_rec_derive", tableId);

        var attr = await GetAttributeAsync(attrId);
        Assert.True(attr.IsRecordMode);
        Assert.Equal(tableId, attr.RefTableDefinitionId);
    }

    [Fact]
    public async Task CreateAttribute_ReferencingPlainTable_NotRecordMode()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_plain_ref");
        var attrId = await CreateTableAttributeAsync("attr_rec_plain_ref", tableId);

        var attr = await GetAttributeAsync(attrId);
        Assert.False(attr.IsRecordMode);
    }

    [Fact]
    public async Task UpdateAttribute_SwapTable_ReDerivesRecordMode()
    {
        await EnsureEntityTypeAsync();
        var recTableId = await CreateTableAsync("rec_t_swap_rec", isRecordTable: true);
        var plainTableId = await CreateTableAsync("rec_t_swap_plain");
        var attrId = await CreateTableAttributeAsync("attr_rec_swap", recTableId);
        Assert.True((await GetAttributeAsync(attrId)).IsRecordMode);

        // 时序表 → 普通表：不再是时序记录
        var toPlain = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/attributes/{attrId}",
            new { refTableDefinitionId = plainTableId });
        Assert.Equal(HttpStatusCode.NoContent, toPlain.StatusCode);
        Assert.False((await GetAttributeAsync(attrId)).IsRecordMode);

        // 普通表 → 时序表：自动恢复为时序记录
        var toRecord = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/attributes/{attrId}",
            new { refTableDefinitionId = recTableId });
        Assert.Equal(HttpStatusCode.NoContent, toRecord.StatusCode);
        Assert.True((await GetAttributeAsync(attrId)).IsRecordMode);
    }

    [Fact]
    public async Task RecordTable_CanBeReferencedByMultipleAttributes()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_shared", isRecordTable: true);

        var first = await CreateTableAttributeAsync("attr_rec_shared1", tableId);
        var second = await CreateTableAttributeAsync("attr_rec_shared2", tableId);

        Assert.True((await GetAttributeAsync(first)).IsRecordMode);
        Assert.True((await GetAttributeAsync(second)).IsRecordMode);
    }

    // ---------- 列锁定 ----------

    [Fact]
    public async Task DeleteColumn_TimeDimensionColumns_Rejected()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_del_lock", isRecordTable: true);

        var table = await GetTableAsync(tableId);
        var dateCol = table.Columns.Single(c => c.DataType == "date");
        var timeCol = table.Columns.Single(c => c.DataType == "time");

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{dateCol.ColumnId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{timeCol.ColumnId}")).StatusCode);
    }

    [Fact]
    public async Task AddColumn_BelowLockedZone_Rejected_BusinessColumn_Allowed()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_add", isRecordTable: true);

        // DisplayOrder < 2（锁定区）→ 400
        var bad = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns",
            new { columnName = "sneaky", displayName = "越界", dataType = "string", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // 业务列正常添加 → 200
        var ok = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns",
            new { columnName = "note", displayName = "备注", dataType = "string", displayOrder = 2 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task UpdateColumn_LockedReorder_Rejected_BusinessColumn_Free()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_upd", isRecordTable: true);

        // 加一个业务列
        var addResp = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns",
            new { columnName = "note", displayName = "备注", dataType = "string", displayOrder = 2 });
        addResp.EnsureSuccessStatusCode();
        var biz = (await addResp.Content.ReadEavAsync<ColumnIdResponse>())!;

        var table = await GetTableAsync(tableId);
        var dateCol = table.Columns.Single(c => c.DataType == "date");

        // 锁定列调顺序 → 400
        var lockResp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{dateCol.ColumnId}",
            new { displayOrder = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, lockResp.StatusCode);

        // 锁定列改显示名 → 允许（仅顺序锁定）
        var renameResp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{dateCol.ColumnId}",
            new { displayName = "记录日期" });
        Assert.Equal(HttpStatusCode.NoContent, renameResp.StatusCode);

        // 业务列移入锁定区 → 400
        var moveInResp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{biz.ColumnId}",
            new { displayOrder = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, moveInResp.StatusCode);

        // 业务列自由调整 → 204
        var freeResp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{biz.ColumnId}",
            new { displayOrder = 9 });
        Assert.Equal(HttpStatusCode.NoContent, freeResp.StatusCode);

        // 业务列可删 → 204
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{biz.ColumnId}")).StatusCode);
    }

    [Fact]
    public async Task PlainTable_NotAffected_ByLockRules()
    {
        await EnsureEntityTypeAsync();
        var tableId = await CreateTableAsync("rec_t_plain_free");

        var colResp = await Client.PostAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns",
            new { columnName = "d", displayName = "日期", dataType = "date", displayOrder = 0 });
        colResp.EnsureSuccessStatusCode();
        var col = (await colResp.Content.ReadEavAsync<ColumnIdResponse>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsJsonAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{col.ColumnId}",
            new { displayOrder = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync(
            $"/api/eav/metadata/custom-tables/{tableId}/columns/{col.ColumnId}")).StatusCode);
    }

    private sealed record AttrIdResponse(string AttributeId);
    private sealed record TableIdResponse(string TableDefinitionId);
    private sealed record ColumnIdResponse(string ColumnId);
}
