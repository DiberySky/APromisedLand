using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class BatchDeleteTests : IntegrationTestBase
{
    public BatchDeleteTests(EavApiFactory factory) : base(factory) { }

    [Fact]
    public async Task BatchDelete_SplitsDeletedAndNotFound()
    {
        await TestData.EnsureSchemaAsync(Client);

        // 创建 3 个
        var existing = new[] { GuidFromInt(94001), GuidFromInt(94002), GuidFromInt(94003) };
        foreach (var id in existing)
        {
            await PutEntityAsync(TestData.EntityType, id,
                new Dictionary<string, object?> { ["amount"] = 1L });
        }

        // 请求里混合：2 个存在 + 1 个不存在 + 1 个重复
        var req = new BatchDeleteRequest
        {
            EntityIds = new List<string> { GuidFromInt(94001), GuidFromInt(94002), GuidFromInt(94001), GuidFromInt(94999) }
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/batch-delete", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<BatchDeleteResultDto>();
        Assert.NotNull(result);
        Assert.Equal(new[] { GuidFromInt(94001), GuidFromInt(94002) }, result!.Deleted.OrderBy(x => x));
        Assert.Contains(GuidFromInt(94999), result.NotFound);
        Assert.DoesNotContain(GuidFromInt(94003), result.Deleted);   // 94003 未在请求中
    }

    [Fact]
    public async Task BatchDelete_EmptyList_ReturnsEmpty()
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/batch-delete",
            new BatchDeleteRequest { EntityIds = new List<string>() });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<BatchDeleteResultDto>();
        Assert.NotNull(result);
        Assert.Empty(result!.Deleted);
        Assert.Empty(result.NotFound);
    }
}
