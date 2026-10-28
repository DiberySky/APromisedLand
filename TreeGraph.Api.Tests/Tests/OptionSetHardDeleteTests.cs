using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// OptionSet 硬删除的集成测试（软删除已废除）。
///
/// 覆盖：
///   - 删除后 GET 404 / 默认 List 排除
///   - 删除后同名可重建（行已物理移除，唯一索引不再拦截）
///   - 同名活动集合冲突 → 409
///   - 子项为空方可删除：仍有活动选项时 DeleteSet 返回 400（不采用级联删除）
///   - GetReferences 正常返回
///   - DeleteSet 拒绝被引用的集合（保守策略）
///
/// 隔离策略：本类所有 entityType 以 "OptHardTest*" 开头，与其它测试类互不干扰。
/// </summary>
public class OptionSetHardDeleteTests : IntegrationTestBase
{
    public OptionSetHardDeleteTests(EavApiFactory factory) : base(factory) { }

    private sealed record IdResponse(string OptionSetId);

    // ============================================================
    // 辅助
    // ============================================================

    private async Task<string> CreateSetAsync(
        string entityType, string setName,
        params (string Value, string Label)[] items)
    {
        // 先注入静态 HttpClient 并完成共享 schema 初始化（否则 TestData.EnsureEntityTypeAsync 会因 _client 为空抛异常）
        await TestData.EnsureSchemaAsync(Client);

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

    /// <summary>删除集合：先清空活动选项（子项为空方可删除本项），再删除集合本身。</summary>
    private async Task DeleteSetAsync(string setId)
    {
        await DeleteAllItemsAsync(setId);

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    /// <summary>逐个删除集合下的全部活动选项。</summary>
    private async Task DeleteAllItemsAsync(string setId)
    {
        var listResp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/items");
        listResp.EnsureSuccessStatusCode();

        var items = await listResp.Content
            .ReadEavAsync<List<OptionItemDetailDto>>();

        foreach (var item in items ?? [])
        {
            if (item.IsDeleted) continue;

            var resp = await Client.DeleteAsync(
                $"/api/eav/metadata/option-sets/{setId}/items/{item.OptionItemId}");
            Assert.True(resp.IsSuccessStatusCode,
                $"删除选项 '{item.Value}' 失败: {resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        }
    }

    // ============================================================
    // 硬删基础语义
    // ============================================================

    [Fact]
    public async Task HardDelete_DefaultGet_Returns404()
    {
        var setId = await CreateSetAsync("OptHardGet404", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync($"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task HardDelete_IncludeDeletedGet_AlsoReturns404()
    {
        var setId = await CreateSetAsync("OptHardGetInc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        // 行已物理移除，includeDeleted=true 也查不到
        var resp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}?includeDeleted=true");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task HardDelete_DefaultList_ExcludesDeleted()
    {
        var setId = await CreateSetAsync("OptHardListExc", "s", ("a", "A"));
        await DeleteSetAsync(setId);

        var resp = await Client.GetAsync(
            "/api/eav/metadata/option-sets?entityType=OptHardListExc");
        resp.EnsureSuccessStatusCode();

        var list = await resp.Content.ReadEavAsync<List<OptionSetSummaryDto>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, s => s.OptionSetId == setId);
    }

    // ============================================================
    // 唯一索引语义
    // ============================================================

    [Fact]
    public async Task Create_WhenActiveNameExists_ReturnsConflict()
    {
        await CreateSetAsync("OptHardDupActive", "dup", ("a", "A"));

        // 同名活动集合 → 唯一索引拒绝
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType = "OptHardDupActive", setName = "dup", displayName = "dup2" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Create_AfterHardDelete_SameName_Allowed()
    {
        var setId1 = await CreateSetAsync("OptHardDupAfterDel", "dup", ("a", "A"));
        await DeleteSetAsync(setId1);

        // 硬删后同名可重建（行已物理移除）
        var setId2 = await CreateSetAsync("OptHardDupAfterDel", "dup", ("b", "B"));
        Assert.NotEqual(setId1, setId2);
    }

    // ============================================================
    // 删除不级联：子项为空方可删除本项
    // ============================================================

    [Fact]
    public async Task DeleteSet_WhenItemsRemain_Returns400_WithoutCascade()
    {
        var setId = await CreateSetAsync("OptHardNoCascade", "s",
            ("a", "A"), ("b", "B"));

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("选项", await resp.Content.ReadAsStringAsync());

        // 集合仍存在，选项未被级联删除
        var setResp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.OK, setResp.StatusCode);

        var listResp = await Client.GetAsync(
            $"/api/eav/metadata/option-sets/{setId}/items");
        listResp.EnsureSuccessStatusCode();

        var items = await listResp.Content
            .ReadEavAsync<List<OptionItemDetailDto>>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
    }

    [Fact]
    public async Task DeleteSet_AfterItemsRemoved_Succeeds()
    {
        var setId = await CreateSetAsync("OptHardEmptyThenDel", "s", ("a", "A"));

        await DeleteAllItemsAsync(setId);

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/option-sets/{setId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ============================================================
    // GetReferences 正常返回
    // ============================================================

    [Fact]
    public async Task GetReferences_ReturnsActiveReferences_OnActiveSet()
    {
        var setId = await CreateSetAsync("OptHardRefs", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptHardRefsEntity", "引用测试");

        // 建属性引用
        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                attributeName = "choice_hardrefs",
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
        Assert.Equal("choice_hardrefs", refs![0].AttributeName);
    }

    // ============================================================
    // DeleteSet 拒绝被引用的集合（保持既有保守策略）
    // ============================================================

    [Fact]
    public async Task DeleteSet_WhenReferencedByActiveAttribute_Returns400()
    {
        var setId = await CreateSetAsync("OptHardRefGuard", "s", ("a", "A"));

        // 确保属性引用的实体类型也存在
        await TestData.EnsureEntityTypeAsync("OptHardRefGuardEntity", "引用守卫");

        await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                attributeName = "choice_hardguard",
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
