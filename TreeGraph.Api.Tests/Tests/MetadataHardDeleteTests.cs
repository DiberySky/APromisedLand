using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 属性物理删除的集成测试。
///
/// 属性表已取消软删除：删除即物理删除，名称立即可复用
/// （原先软删行仍占用 uq_attr_catalog 唯一约束）。
/// </summary>
public class MetadataHardDeleteTests : IntegrationTestBase
{
    public MetadataHardDeleteTests(EavApiFactory factory) : base(factory) { }

    private const string EntityType = "HardDeleteTest";

    [Fact]
    public async Task DeleteAttribute_ThenRecreateSameName_Succeeds()
    {
        await TestData.EnsureEntityTypeAsync(EntityType, "物理删除测试");

        var first = await CreateAttributeAsync("foo");
        Assert.NotNull(first);

        var delResp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{first!.AttributeId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // 删除后列表不再包含
        var listResp = await Client.GetAsync(
            $"/api/eav/metadata/attributes?entityType={EntityType}");
        var list = await listResp.Content
            .ReadEavAsync<List<AttributeDetailDto>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, a => a.AttributeId == first.AttributeId);

        // 同名重建不再 409，且获得新的属性 Id
        var second = await CreateAttributeAsync("foo");
        Assert.NotNull(second);
        Assert.NotEqual(first.AttributeId, second!.AttributeId);
    }

    private async Task<IdResponse?> CreateAttributeAsync(string attributeName)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = EntityType,
                attributeName,
                displayName = attributeName,
                dataType = "string",
                displayOrder = 1
            });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadEavAsync<IdResponse>();
    }

    private sealed record IdResponse(string AttributeId);
}
