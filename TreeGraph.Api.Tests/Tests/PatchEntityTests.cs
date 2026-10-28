using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// PATCH /entities/{id} 集成测试。
///
/// 语义关键点（与 PUT 的唯一差异）：
///   - values 中**未出现**的属性：保持不变（PUT 会删除）
///   - values 中值为 null 的属性：删除（与 PUT 一致）
///
/// 复用 TestData.EnsureSchemaAsync 里的 amount / price / label / flag 四个属性。
/// </summary>
public class PatchEntityTests : IntegrationTestBase
{
    public PatchEntityTests(EavApiFactory factory) : base(factory) { }

    // ============================================================
    // 部分更新：未提供的属性保持不动
    // ============================================================

    [Fact]
    public async Task Patch_OnlyUpdatesProvidedFields_LeavesOthersIntact()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95001);
        // 初始：amount=1, price=100, label="original"
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["price"] = 100m,
            ["label"] = "original"
        });

        // PATCH 只更新 price
        var patch = new Dictionary<string, object?>
        {
            ["price"] = 200m
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 核对：price=200，其它未变
        var after = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.Equal(1L, after!.Properties["amount"].GetInt64());
        Assert.Equal(200m, after.Properties["price"].GetDecimal());
        Assert.Equal("original", after.Properties["label"].GetString());
    }

    // ============================================================
    // 显式 null 删除
    // ============================================================

    [Fact]
    public async Task Patch_NullValue_DeletesField()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95002);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "to-be-removed"
        });

        // PATCH 显式 null → 删除 label
        var patch = new Dictionary<string, object?>
        {
            ["label"] = null
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var after = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.False(after!.Properties.ContainsKey("label"));   // 已删除
        Assert.Equal(1L, after.Properties["amount"].GetInt64()); // 未动
    }

    // ============================================================
    // 未知属性
    // ============================================================

    [Fact]
    public async Task Patch_UnknownAttribute_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95003);
        var patch = new Dictionary<string, object?>
        {
            ["amoutn"] = 2L   // typo
        };
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("amoutn", body);
    }

    // ============================================================
    // 乐观锁
    // ============================================================

    [Fact]
    public async Task Patch_StaleUpdatedAt_Returns409()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95004);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 1L
        });

        // 用陈旧时间
        var stale = DateTimeOffset.UtcNow.AddDays(-1);

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 99L
            })
        };
        req.Headers.Add("X-Expected-Updated-At", stale.ToString("O"));

        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ============================================================
    // 空 PATCH 不产生任何变更
    // ============================================================

    [Fact]
    public async Task Patch_EmptyBody_NoOp()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(95005);
        await PutEntityAsync(TestData.EntityType, id, new Dictionary<string, object?>
        {
            ["amount"] = 42L
        });

        var patch = new Dictionary<string, object?>();
        var resp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", patch);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var after = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        Assert.NotNull(after);
        Assert.Equal(42L, after!.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 与 PUT 的差异对比
    // ============================================================

    [Fact]
    public async Task Put_OmitsAttribute_DeletesIt_WhilePatchDoesNot()
    {
        await TestData.EnsureSchemaAsync(Client);

        // === 场景 A：PUT 只提供 amount → label 被删除 ===
        var idA = GuidFromInt(95006);
        await PutEntityAsync(TestData.EntityType, idA, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "will-be-deleted-by-put"
        });

        await PutEntityAsync(TestData.EntityType, idA, new Dictionary<string, object?>
        {
            ["amount"] = 2L   // 没提供 label
        });

        var afterA = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{idA}");
        Assert.NotNull(afterA);
        Assert.False(afterA!.Properties.ContainsKey("label"));   // PUT 删除了

        // === 场景 B：PATCH 只提供 amount → label 保留 ===
        var idB = GuidFromInt(95007);
        await PutEntityAsync(TestData.EntityType, idB, new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["label"] = "will-be-kept-by-patch"
        });

        var patch = new Dictionary<string, object?>
        {
            ["amount"] = 2L
        };
        var patchResp = await Client.PatchAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{idB}", patch);
        Assert.Equal(HttpStatusCode.NoContent, patchResp.StatusCode);

        var afterB = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{TestData.EntityType}/entities/{idB}");
        Assert.NotNull(afterB);
        Assert.Equal("will-be-kept-by-patch", afterB!.Properties["label"].GetString());
    }
}
