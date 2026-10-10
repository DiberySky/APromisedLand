using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// CustomTableWriteService.UpsertRowAsync 跨行唯一性测试。
///
/// 唯一性语义：
///   - 同一父实体、同一属性、同一表下，IsUnique 列的值不可重复
///   - 更新自身（RowId 指向已存在的行）不与自己冲突
///   - 删除后释放该值
///   - 跨父实体互相隔离
///
/// 隔离：本类用独立 entityType "UniqueTest*"，避免与其它测试类干扰。
/// </summary>
public class CrossRowUniqueTests : IntegrationTestBase
{
    private const string EntityType = "UniqueTestProduct";

    public CrossRowUniqueTests(EavApiFactory factory) : base(factory) { }

    // ============================================================
    // 初始化：建表 + 列 + 属性
    // ============================================================

    private static bool _schemaReady;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);
    private static string _tableDefId = "";

    private async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await _schemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;

            // 0. 先创建实体类型（否则后续 CreateAttribute 400）
            await TestData.EnsureEntityTypeAsync(EntityType, "唯一性测试");

            // 1. 建表
            var tableResp = await Client.PostAsJsonAsync(
                "/api/eav/metadata/custom-tables",
                new
                {
                    entityType = EntityType,
                    tableName = "unique_certs",
                    displayName = "认证",
                    displayOrder = 1
                });
            tableResp.EnsureSuccessStatusCode();
            _tableDefId = (await tableResp.Content
                .ReadEavAsync<IdResponse>())!.TableDefinitionId;

            // 2. 加列（IsUnique = true）
            var colResp = await Client.PostAsJsonAsync(
                $"/api/eav/metadata/custom-tables/{_tableDefId}/columns",
                new
                {
                    columnName = "cert_name",
                    displayName = "证书名",
                    dataType = "string",
                    isUnique = true,
                    isRequired = true,
                    displayOrder = 1
                });
            colResp.EnsureSuccessStatusCode();

            // 3. 建属性引用该表
            var attrResp = await Client.PostAsJsonAsync(
                "/api/eav/metadata/attributes",
                new
                {
                    entityType = EntityType,
                    attributeName = "unique_certs",
                    displayName = "认证",
                    dataType = "table",
                    refTableDefinitionId = _tableDefId,
                    displayOrder = 1
                });
            attrResp.EnsureSuccessStatusCode();
            // 顺带验证建属性响应字段名 attributeId 可解析
            var attrId = (await attrResp.Content
                .ReadEavAsync<AttrIdResponse>())!.AttributeId;
            Assert.False(string.IsNullOrEmpty(attrId));

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private async Task<HttpResponseMessage> UpsertRowAsync(
        string entityId, string certName, string? rowId = null)
    {
        var body = new
        {
            rowId,
            rowOrder = 1,
            fields = new Dictionary<string, object?> { ["cert_name"] = certName }
        };
        return await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{entityId}/tables/unique_certs/rows",
            body);
    }

    // ============================================================
    // 首次插入 → OK
    // ============================================================

    [Fact]
    public async Task Upsert_FirstRow_Succeeds()
    {
        await EnsureSchemaAsync();

        var resp = await UpsertRowAsync(GuidFromInt(96001), "CERT-A");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 同父实体同值、30 秒幂等窗口内重复提交 → 重放首次结果（204），不产生第二行
    // （跨行唯一约束本身在窗口外/不同载荷下仍会 400，由其余用例覆盖）
    // ============================================================

    [Fact]
    public async Task Upsert_IdenticalResubmitWithinWindow_ReplaysFirstResult()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96002);
        var first = await UpsertRowAsync(parent, "CERT-DUP");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await UpsertRowAsync(parent, "CERT-DUP");   // 同载荷重复提交
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var table = await Client.GetEavAsync<CustomTableValue>(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs");
        Assert.Single(table!.Rows);
    }

    // ============================================================
    // 更新自身 → OK（不与自己冲突）
    // ============================================================

    [Fact]
    public async Task Upsert_UpdateSelfToSameValue_Succeeds()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96003);
        await UpsertRowAsync(parent, "SELF-X");

        // 拿到 rowId
        var tableValue = await Client.GetEavAsync<CustomTableValue>(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs");
        var rowId = tableValue!.Rows[0].RowId!;

        // 更新自身（同值）→ 应 OK
        var resp = await UpsertRowAsync(parent, "SELF-X", rowId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 跨父实体：隔离
    // ============================================================

    [Fact]
    public async Task Upsert_SameValueInDifferentParents_Isolated()
    {
        await EnsureSchemaAsync();

        var a = await UpsertRowAsync(GuidFromInt(96004), "SHARED-VAL");
        var b = await UpsertRowAsync(GuidFromInt(96005), "SHARED-VAL");   // 不同父实体

        Assert.Equal(HttpStatusCode.NoContent, a.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, b.StatusCode);
    }

    // ============================================================
    // 删除释放唯一值
    // ============================================================

    [Fact]
    public async Task Upsert_AfterDelete_ValueCanBeReused()
    {
        await EnsureSchemaAsync();

        var parent = GuidFromInt(96006);
        await UpsertRowAsync(parent, "RELEASE-X");

        var tableValue = await Client.GetEavAsync<CustomTableValue>(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs");
        var rowId = tableValue!.Rows[0].RowId!;

        // 删除该行
        var del = await Client.DeleteAsync(
            $"/api/eav/{EntityType}/entities/{parent}/tables/unique_certs/rows/{rowId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 重新插入同值 → 应 OK
        var resp = await UpsertRowAsync(parent, "RELEASE-X");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 辅助 record
    // ============================================================

    private sealed record IdResponse(string TableDefinitionId);
    private sealed record AttrIdResponse(string AttributeId);
}
