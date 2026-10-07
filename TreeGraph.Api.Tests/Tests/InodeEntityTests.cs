using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 归属层（inode_entity）集成测试。
/// 覆盖 GetOrCreate / Patch / Delete / 隔离。
/// </summary>
public class InodeEntityTests : IntegrationTestBase
{
    private const string EntityType = "InodeEntityTest";

    public InodeEntityTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 首次创建
    // ============================================================

    [Fact]
    public async Task Put_FirstTime_CreatesEntity()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "first",
                ["amount"] = 42L
            });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.NotNull(dto);
        Assert.False(string.IsNullOrEmpty(dto!.EntityId));
        Assert.Equal(EntityType, dto.EntityType);
        Assert.Equal("first", dto.Properties["name"].GetString());
        Assert.Equal(42L, dto.Properties["amount"].GetInt64());
    }

    [Fact]
    public async Task Put_SecondTime_SameEntityId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "first" });

        var dto1 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "second" });

        var dto2 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal(dto1!.EntityId, dto2!.EntityId);
        Assert.Equal("second", dto2.Properties["name"].GetString());
    }

    // ============================================================
    // 读取
    // ============================================================

    [Fact]
    public async Task Get_NotCreated_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.GetAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // PATCH
    // ============================================================

    [Fact]
    public async Task Patch_PartialUpdate_KeepsOthers()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "orig", ["amount"] = 1L });

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/inode/{inodeId}/entities/{EntityType}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 99L
            })
        };
        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.Equal("orig", dto!.Properties["name"].GetString());
        Assert.Equal(99L, dto.Properties["amount"].GetInt64());
    }

    // ============================================================
    // DELETE
    // ============================================================

    [Fact]
    public async Task Delete_RemovesEntityAndMapping()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        var del = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 实体已不可读
        var get = await Client.GetAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_NotCreated_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Put_ThenDelete_CanRecreateWithNewId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        var dto1 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v2" });

        var dto2 = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        // 新 GUID
        Assert.NotEqual(dto1!.EntityId, dto2!.EntityId);
        Assert.Equal("v2", dto2.Properties["name"].GetString());
    }

    // ============================================================
    // 列表
    // ============================================================

    [Fact]
    public async Task List_ReturnsAllCreatedEntities()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}B",
            new Dictionary<string, object?> { ["name"] = "B" });

        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task List_EmptyInode_ReturnsEmpty()
    {
        var inodeId = NewInodeId();

        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");

        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    // ============================================================
    // 隔离
    // ============================================================

    [Fact]
    public async Task TwoInodes_SameType_AreIsolated()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeA}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A-value" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeB}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "B-value" });

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeA}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeB}/entities/{EntityType}");

        // 两个不同 entityId
        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);

        // 数据隔离
        Assert.Equal("A-value", dtoA.Properties["name"].GetString());
        Assert.Equal("B-value", dtoB.Properties["name"].GetString());
    }

    /// <summary>
    /// 回归：声明但无属性的类型，实体可创建但无法写入。
    ///
    /// 修复前：GetOrCreateEntityIdAsync 误查 attribute_catalog，
    ///         对无属性类型 PUT 直接 400"实体类型不存在"。
    /// 修复后：实体创建成功，写入阶段因"未知属性"被 400 拒绝。
    /// </summary>
    [Fact]
    public async Task Put_OnTypeWithoutAttributes_FailsValidation()
    {
        // 建类型（不建属性）
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { displayName = $"无属性写入_{Guid.NewGuid():N}" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        Assert.NotNull(created);

        var inodeId = NewInodeId();

        // PUT 会走到 EavValidationException（未知属性）
        var resp = await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{created!.EntityType}",
            new Dictionary<string, object?> { ["whatever"] = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("未知属性", body);
    }

    private sealed record CreateTypeResponse(string EntityTypeId, string EntityType);
}
