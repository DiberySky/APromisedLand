using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 实体类型纯全局共享语义集成测试。
/// 实体类型仅受宿主 Id（inode）约束，与空间无关；
/// 列表/详情端点不再提供任何空间过滤参数。
/// </summary>
public class EntityTypeGlobalTests : IntegrationTestBase
{
    public EntityTypeGlobalTests(EavApiFactory factory) : base(factory) { }

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>创建实体类型，返回 (statusCode, entityTypeId)。</summary>
    private async Task<(HttpStatusCode Status, string? TypeId)> CreateTypeAsync(
        string displayName)
    {
        var resp = await Client.PostAsJsonAsync("/api/eav/entity-types", new
        {
            displayName
        });
        if (resp.StatusCode != HttpStatusCode.OK)
            return (resp.StatusCode, null);

        var body = await resp.Content.ReadEavAsync<System.Text.Json.Nodes.JsonObject>();
        return (resp.StatusCode, body?["entityTypeId"]?.GetValue<string>());
    }

    // ------------------------------------------------------------
    // 创建：全局共享（无空间归属）
    // ------------------------------------------------------------

    [Fact]
    public async Task Create_ThenGetDetail_ReturnsType()
    {
        var name = NewName("全局类型");
        var (status, typeId) = await CreateTypeAsync(name);

        Assert.Equal(HttpStatusCode.OK, status);
        var detail = await Client.GetEavAsync<EntityTypeDetailDto>(
            $"/api/eav/entity-types/{typeId}");
        Assert.NotNull(detail);
        Assert.Equal(name, detail!.DisplayName);
    }

    // ------------------------------------------------------------
    // 列表/详情：全量返回（无空间过滤）
    // ------------------------------------------------------------

    [Fact]
    public async Task List_ReturnsAllTypes()
    {
        var name = NewName("全量列表");
        await CreateTypeAsync(name);

        var list = await Client.GetEavAsync<List<EntityTypeSummaryDto>>(
            "/api/eav/entity-types") ?? [];
        Assert.Contains(list, t => t.DisplayName == name);
    }

    [Fact]
    public async Task Details_ReturnsAllTypes()
    {
        var name = NewName("全量详情");
        await CreateTypeAsync(name);

        var list = await Client.GetEavAsync<List<EntityTypeDetailDto>>(
            "/api/eav/entity-types/details") ?? [];
        Assert.Contains(list, t => t.DisplayName == name);
    }
}
