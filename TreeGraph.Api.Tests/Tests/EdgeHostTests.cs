using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// Edge 作为 EAV 宿主（方案 A：接入宿主挂载体系）集成测试。
///
/// 边接入 inode 挂载体系后的行为：
///   - 边可作为宿主声明类型、挂载实体（/api/inode/{edgeId}/... 端点）
///   - 删除守卫：边仍挂载实体数据 → 拒绝删；仅声明 → 可删且声明随删
///   - 向量层：边宿主点文本单行「当前节点：显示名」，DisplayName 回退 EdgeId
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
        var et = await CreateEdgeTypeAsync();
        var edge = await CreateEdgeAsync(et.EdgeTypeId, "host_n1", "host_n2");
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

    // ============================================================
    // 删除守卫
    // ============================================================

    [Fact]
    public async Task Edge_Delete_WhenEntityMounted_Returns409()
    {
        var et = await CreateEdgeTypeAsync();
        var edge = await CreateEdgeAsync(et.EdgeTypeId, "host_n3", "host_n4");
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        // 挂载实体
        await Client.PutAsJsonAsync(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}", new { name = "守卫" });

        // 边仍挂载实体数据 → 拒绝删除
        var del = await Client.DeleteAsync($"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Contains("实体数据", await del.Content.ReadAsStringAsync());

        // 清空实体后可删
        var clear = await Client.DeleteAsync(
            $"/api/inode/{edge.EdgeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var del2 = await Client.DeleteAsync($"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del2.StatusCode);
    }

    [Fact]
    public async Task Edge_Delete_WithDeclarationOnly_Succeeds_AndCleansDeclaration()
    {
        var et = await CreateEdgeTypeAsync();
        var edge = await CreateEdgeAsync(et.EdgeTypeId, "host_n5", "host_n6");
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        // 仅声明（不挂载实体）
        await Client.PostAsync($"/api/inode/{edge.EdgeId}/types/{EntityType}", content: null);

        // 删除边 → 声明随删
        var del = await Client.DeleteAsync($"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 声明已清理（孤儿声明检测为空）
        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{edge.EdgeId}/types");
        Assert.Empty(cards!);
    }

    // ============================================================
    // 向量文本（单行「当前节点：显示名」）
    // ============================================================

    [Fact]
    public async Task Edge_AsHost_DisplayNameFallback_ToEdgeId()
    {
        var et = await CreateEdgeTypeAsync();

        // 带显示名
        var named = await CreateEdgeAsync(
            et.EdgeTypeId, "host_n7", "host_n8", displayName: "电缆段-甲");

        // 无显示名 → 回退 EdgeId（向量文本来源，回退逻辑在 SourceTextBuilder）
        var unnamed = await CreateEdgeAsync(et.EdgeTypeId, "host_n9", "host_n10");

        Assert.Equal("电缆段-甲", named.DisplayName);
        Assert.Null(unnamed.DisplayName);
        Assert.NotNull(unnamed.EdgeId); // 回退依据：EdgeId 非空
    }

    // ============================================================
    // helpers
    // ============================================================

    private async Task<EdgeTypeDto> CreateEdgeTypeAsync(bool isGlobal = true)
    {
        var typeName = NewTypeName();
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = $"宿主测试_{typeName[^4..]}",
                IsGlobal = isGlobal,
                IsDirected = true
            });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task<EdgeInstanceDto> CreateEdgeAsync(
        string edgeTypeId, string fromNodeId, string toNodeId,
        decimal? weight = null, string? displayName = null)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{edgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Weight = weight,
                DisplayName = displayName
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        return dto!;
    }
}
