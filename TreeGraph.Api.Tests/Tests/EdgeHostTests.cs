using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// Edge 作为 EAV 宿主（接入宿主挂载体系）集成测试（R4 全局路由版）。
///
/// 行为：
///   - 边注册进 nodes（host_type="EdgeInstance"、name=null），可作为宿主声明类型、挂载实体；
///   - 删除守卫：边仍挂载实体数据 → 409；仅声明 → 可删且声明随删；
///   - 边不再有 DisplayName/Weight 字段（业务属性并入 EAV）。
/// </summary>
public class EdgeHostTests : IntegrationTestBase
{
    private const string EntityType = "EdgeHostTest";

    public EdgeHostTests(EavApiFactory factory) : base(factory) { }

    private static string NewTypeName() => $"EdgeHost_{Guid.NewGuid():N}";

    // ============================================================
    // 声明 / 挂载
    // ============================================================

    [Fact]
    public async Task Edge_AsHost_DeclareAndAttachEntity_Succeeds()
    {
        var edge = await CreateEdgeAsync();
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        // 声明类型
        var attach = await Client.PostAsync(
            $"/api/inode/{edge.EdgeId}/types/{EntityType}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, attach.StatusCode);

        // 挂载实体（PUT 自动建映射）
        var put = await Client.PutAsJsonAsync(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}", new { name = "边宿主实体" });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 读回
        var entity = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}");
        Assert.NotNull(entity);
        Assert.Equal("边宿主实体", entity!.Properties["name"].GetString());

        // 宿主卡片（声明 + 有实体）
        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{edge.EdgeId}/types");
        var card = Assert.Single(cards!);
        Assert.Equal(EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.True(card.HasEntity);
    }

    [Fact]
    public async Task Edge_AsHost_NoDeclaration_ReturnsEmptyCards()
    {
        var edge = await CreateEdgeAsync();
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{edge.EdgeId}/types");

        Assert.NotNull(cards);
        Assert.Empty(cards!);
    }

    // ============================================================
    // 删除守卫
    // ============================================================

    [Fact]
    public async Task Edge_Delete_WhenEntityMounted_Returns409()
    {
        var edge = await CreateEdgeAsync();
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        // 挂载实体
        await Client.PutAsJsonAsync(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}", new { name = "守卫" });

        // 边仍挂载实体数据 → 拒绝删除
        var del = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Contains("实体数据", await del.Content.ReadAsStringAsync());

        // 清空实体后可删
        var clear = await Client.DeleteAsync(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var del2 = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del2.StatusCode);
    }

    [Fact]
    public async Task Edge_Delete_WithDeclarationOnly_Succeeds_AndCleansDeclaration()
    {
        var edge = await CreateEdgeAsync();
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        // 仅声明（不挂载实体）
        await Client.PostAsync($"/api/inode/{edge.EdgeId}/types/{EntityType}", content: null);

        // 删除边 → 声明随删
        var del = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 声明已清理（孤儿声明检测为空）
        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{edge.EdgeId}/types");
        Assert.NotNull(cards);
        Assert.Empty(cards!);
    }

    // ============================================================
    // helpers
    // ============================================================

    private async Task<EdgeInstanceDto> CreateEdgeAsync()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = NewTypeName(),
                FromNodeId = await CreateFreeNodeAsync("宿主起点"),
                ToNodeId = await CreateFreeNodeAsync("宿主终点")
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task<string> CreateFreeNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var resp = await Client.PostAsJsonAsync("/api/Node", new { id, name });
        resp.EnsureSuccessStatusCode();
        return id;
    }
}
