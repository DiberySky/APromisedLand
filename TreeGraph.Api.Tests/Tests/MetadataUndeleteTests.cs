using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 元数据软删除恢复的集成测试。
///
/// 注意：`UndeleteAttribute` 里的"同名活动属性已存在"检查是防御性代码——
/// 由于 `uq_attr_catalog` 唯一约束不区分 `IsDeleted`，通过 API 无法构造
/// "同名活动 + 同名软删"共存的场景，因此不写测试。
/// 若未来唯一约束放宽（如加入 IsDeleted 到键），届时再补。
/// </summary>
public class MetadataUndeleteTests : IntegrationTestBase
{
    public MetadataUndeleteTests(EavApiFactory factory) : base(factory) { }

    /// <summary>软删除属性后 undelete → 应恢复。</summary>
    [Fact]
    public async Task UndeleteAttribute_AfterSoftDelete_Succeeds()
    {
        // 先创建一个独立属性
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = "UndeleteTest",
                attributeName = "foo",
                displayName = "foo",
                dataType = "string",
                displayOrder = 1
            });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content.ReadFromJsonAsync<IdResponse>();
        Assert.NotNull(created);

        // 软删除
        var delResp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{created!.AttributeId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // 恢复
        var unResp = await Client.PostAsync(
            $"/api/eav/metadata/attributes/{created.AttributeId}/undelete",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, unResp.StatusCode);

        // 验证出现在列表
        var listResp = await Client.GetAsync(
            "/api/eav/metadata/attributes?entityType=UndeleteTest");
        var list = await listResp.Content
            .ReadFromJsonAsync<List<AttributeDetailDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, a =>
            a.AttributeId == created.AttributeId && !a.IsDeleted);
    }

    private sealed record IdResponse(string AttributeId);
}
