using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// OptionSet 软删除 / 恢复的集成测试。
///
/// 覆盖：
///   - 软删后默认 GET 404 / includeDeleted=true 返回 isDeleted=true
///   - 软删后默认 List 排除 / includeDeleted=true 包含
///   - partial unique index：软删后同名可重建；恢复撞同名 → 409
///   - 恢复：集合 + 选项一并恢复
///   - 幂等：未删的恢复是 No-Op 204
///   - 软删 = 集合 + 全部选项都标 IsDeleted
///   - GetReferences 不受 IsDeleted 影响
///
/// 边界（通过 API 不可构造，不测）：
///   - 已引用集合被软删（DeleteSet 拒绝被引用的集合）
///   - 软删后同名活动集合与已删集合共存（partial unique 允许，但恢复会 409）
///
/// 隔离策略：本类所有 entityType 以 "OptSoftTest*" 开头，与其它测试类的
/// "TestProduct" / "Product" 等互不干扰。
/// </summary>
public class OptionSetSoftDeleteTests : IntegrationTestBase
{
    public OptionSetSoftDeleteTests(EavApiFactory factory) : base(factory) { }

    private sealed record IdResponse(string OptionSetId);

    // ============================================================
    // 辅助
    // ============================================================

    private async Task<string> CreateSetAsync(
        string entityType, string setName,
        params (string Value, string Label)[] items)
    {
        // 确保实体类型存在（否则后续 CreateAttribute 400）
        await TestData.EnsureEntityTypeAsync(entityType, entityType);

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType, setName, displayName = setName });
        resp.EnsureSuccessStatusCode();

        var created = await resp.Content.ReadEavAsync<IdResponse>();
        Assert.NotNull(created);

        foreach (var (value, label) in items)
        {
            var r = await Client.PostAsJsonAsync(
                $"/api/eav/metadata/option-sets/{created!.OptionSetId}/items",
                new { value, label, displayOrder = 0, isDefault = false });
            Assert.True(r.IsSuccessStatusCode,
                $"Create item '{value}' failed: {r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }

        return created!.OptionSetId;
    }

    private async Task DeleteSetAsync(string setId)
    {
        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<HttpResponseMessage> UndeleteSetRawAsync(string setId)
        => await Client.PostAsync(
            $"/api/eav/metadata/option-sets/{setId}/undelete", content: null);

    // ============================================================
    // 软删基础语义
    // ============================================================

    [Fact]
    public async Task SoftDelete_DefaultGet_Returns404()
    {
        var setId = await CreateSetAsync("OptSoftGet404", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync($"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SoftDelete_IncludeDeletedGet_Returns200WithIsDeletedTrue()
    {
        var setId = await CreateSetAsync("OptSoftGetInc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}?includeDeleted=true");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var detail = await resp.Content.ReadEavAsync<OptionSetDetailDto>();
        Assert.NotNull(detail);
        Assert.True(detail!.IsDeleted);
        Assert.Equal(setId, detail.OptionSetId);
    }

    [Fact]
    public async Task SoftDelete_DefaultList_ExcludesDeleted()
    {
        var setId = await CreateSetAsync("OptSoftListExc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            "/api/eav/metadata/option-sets?entityType=OptSoftListExc");
        resp.EnsureSuccessStatusCode();

        var list = await resp.Content.ReadEavAsync<List<OptionSetSummaryDto>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, s => s.OptionSetId == setId);
    }

    [Fact]
    public async Task SoftDelete_IncludeDeletedList_ContainsDeleted()
    {
        var setId = await CreateSetAsync("OptSoftListInc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            "/api/eav/metadata/option-sets?entityType=OptSoftListInc&includeDeleted=true");
        resp.EnsureSuccessStatusCode();

        var list = await resp.Content.ReadEavAsync<List<OptionSetSummaryDto>>();
        Assert.NotNull(list);
        var found = list!.FirstOrDefault(s => s.OptionSetId == setId);
        Assert.NotNull(found);
        Assert.True(found!.IsDeleted);
    }

    // ============================================================
    // Partial unique index 语义
    // ============================================================

    [Fact]
    public async Task Create_WhenActiveNameExists_ReturnsConflict()
    {
        await CreateSetAsync("OptSoftDupActive", "dup", ("a", "A"));

        // 同名活动集合 → 唯一索引拒绝
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType = "OptSoftDupActive", setName = "dup", displayName = "dup2" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Create_AfterSoftDelete_SameName_AllowedByPartialIndex()
    {
        var setId1 = await CreateSetAsync("OptSoftDupAfterDel", "dup", ("a", "A"));
        await DeleteSetAsync(setId1);

        // 软删后同名可重建（partial unique index: WHERE is_deleted = false）
        var setId2 = await CreateSetAsync("OptSoftDupAfterDel", "dup", ("b", "B"));
        Assert.NotEqual(setId1, setId2);
    }

    [Fact]
    public async Task Undelete_WhenActiveDuplicateExists_ReturnsConflict()
    {
        var setId1 = await CreateSetAsync("OptSoftUndupConflict", "dup", ("a", "A"));
        await DeleteSetAsync(setId1);

        // 重建同名活动集合
        await CreateSetAsync("OptSoftUndupConflict", "dup", ("b", "B"));

        // 恢复 setId1 → 409
        var resp = await UndeleteSetRawAsync(setId1);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("同名", body);
    }

    // ============================================================
    // 恢复语义
    // ============================================================

    [Fact]
    public async Task Undelete_RestoresSetAndAllItems()
    {
        var setId = await CreateSetAsync("OptSoftRestoreAll", "s",
            ("a", "A"), ("b", "B"), ("c", "C"));

        await DeleteSetAsync(setId);

        var resp = await UndeleteSetRawAsync(setId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 默认 GET 应 200
        var getResp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        getResp.EnsureSuccessStatusCode();

        var detail = await getResp.Content.ReadEavAsync<OptionSetDetailDto>();
        Assert.NotNull(detail);
        Assert.False(detail!.IsDeleted);
        Assert.Equal(3, detail.Items.Count);
        Assert.All(detail.Items, i => Assert.False(i.IsDeleted));
    }

    [Fact]
    public async Task Undelete_OnActiveSet_Idempotent_204()
    {
        var setId = await CreateSetAsync("OptSoftIdempotent", "s", ("a", "A"));

        // 未删除 → 幂等 204
        var resp = await UndeleteSetRawAsync(setId);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // 软删的连带效果
    // ============================================================

    [Fact]
    public async Task DeleteSet_SoftDeletesAllItems()
    {
        var setId = await CreateSetAsync("OptSoftCascade", "s",
            ("a", "A"), ("b", "B"));

        await DeleteSetAsync(setId);

        // ListOptionItems 不按 IsDeleted 过滤（管理页需要看到已删项）
        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/items");
        resp.EnsureSuccessStatusCode();

        var items = await resp.Content
            .ReadEavAsync<List<OptionItemDetailDto>>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.All(items, i => Assert.True(i.IsDeleted));
    }

    // ============================================================
    // GetReferences 不受 IsDeleted 影响
    // ============================================================

    [Fact]
    public async Task GetReferences_ReturnsActiveReferences_OnActiveSet()
    {
        var setId = await CreateSetAsync("OptSoftRefs", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptSoftRefsEntity", "引用测试");

        // 建属性引用
        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "OptSoftRefsEntity",
                attributeName = "choice",
                displayName = "选择",
                dataType = "single_choice",
                refOptionSetId = setId,
                displayOrder = 1
            });
        Assert.True(attrResp.IsSuccessStatusCode,
            $"Create attribute failed: {attrResp.StatusCode} {await attrResp.Content.ReadAsStringAsync()}");

        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/references");
        resp.EnsureSuccessStatusCode();

        var refs = await resp.Content
            .ReadEavAsync<List<OptionSetReferenceDto>>();
        Assert.NotNull(refs);
        Assert.Single(refs!);
        Assert.Equal("choice", refs![0].AttributeName);
    }

    // ============================================================
    // DeleteSet 拒绝被引用的集合（保持既有保守策略）
    // ============================================================

    [Fact]
    public async Task DeleteSet_WhenReferencedByActiveAttribute_Returns400()
    {
        var setId = await CreateSetAsync("OptSoftRefGuard", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptSoftRefGuardEntity", "引用守卫");

        await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "OptSoftRefGuardEntity",
                attributeName = "choice",
                displayName = "选择",
                dataType = "single_choice",
                refOptionSetId = setId,
                displayOrder = 1
            });

        // 被引用 → DeleteSet 应 400
        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("引用", body);
    }
}
