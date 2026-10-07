using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 声明层（inode_entitytype）集成测试。
/// </summary>
public class InodeEntityTypeTests : IntegrationTestBase
{
    private const string EntityType = "InodeEtTest";

    public InodeEntityTypeTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // Attach
    // ============================================================

    [Fact]
    public async Task Attach_ThenList_ContainsType()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var attach = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");

        Assert.NotNull(list);
        var card = Assert.Single(list!);
        Assert.Equal(EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.False(card.HasEntity);
    }

    [Fact]
    public async Task Attach_IsIdempotent()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var r1 = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);
        var r2 = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        Assert.Equal(HttpStatusCode.NoContent, r1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, r2.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.Single(list!);
    }

    [Fact]
    public async Task Attach_UnknownType_Returns400()
    {
        var inodeId = NewInodeId();
        var unknown = $"Nonexistent_{Guid.NewGuid():N}";

        var resp = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{unknown}", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("不存在", body);
    }

    // ============================================================
    // Detach
    // ============================================================

    [Fact]
    public async Task Detach_WithoutEntity_RemovesDeclaration()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        var detach = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, detach.StatusCode);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Detach_NotDeclared_Returns404()
    {
        var inodeId = NewInodeId();

        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Detach_WithEntity_Returns400()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 首次 PUT 会自动建立声明 + 实体
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        // Detach 应被拒绝
        var resp = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/types/{EntityType}");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("实体", body);
    }

    // ============================================================
    // List 混合场景
    // ============================================================

    [Fact]
    public async Task List_MultipleDeclarations_ReturnsAll()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();
        await Client.PostAsync($"/api/inode/{inodeId}/types/{EntityType}", null);
        await Client.PostAsync($"/api/inode/{inodeId}/types/{EntityType}B", null);

        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, c => c.EntityType == EntityType);
        Assert.Contains(list, c => c.EntityType == EntityType + "B");
    }

    /// <summary>
    /// 回归：允许声明"尚未定义任何属性"的类型。
    ///
    /// 修复前：InodeEntityService.AttachAsync 误查 attribute_catalog，
    ///         导致无属性的类型被判定为不存在 → 400。
    /// </summary>
    [Fact]
    public async Task Attach_TypeWithoutAttributes_Succeeds()
    {
        // 直接建类型（不建属性）
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { displayName = $"无属性类型_{Guid.NewGuid():N}" });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        Assert.NotNull(created);

        var inodeId = NewInodeId();

        // 声明应该成功（修复前会 400）
        var resp = await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{created!.EntityType}", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 列表能看到
        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.NotNull(list);
        var card = Assert.Single(list!);
        Assert.Equal(created.EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.False(card.HasEntity);
    }

    private sealed record CreateTypeResponse(string EntityTypeId, string EntityType);
}
