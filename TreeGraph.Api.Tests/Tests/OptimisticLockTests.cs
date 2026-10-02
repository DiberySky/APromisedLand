using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Eav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class OptimisticLockTests : IntegrationTestBase
{
    public OptimisticLockTests(EavApiFactory factory) : base(factory) { }

    /// <summary>
    /// 同一瞬间、不同 offset（+08:00）回传，应视为"匹配"。
    /// 修复前：DateTimeOffset.!= 会比较 offset → 误判 409。
    /// </summary>
    [Fact]
    public async Task Put_SameInstantDifferentOffset_Succeeds()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(90001);
        await PutEntityAsync(TestData.EntityType, id,
            new Dictionary<string, object?> { ["amount"] = 10L });

        // 拿 UpdatedAt
        var getResp = await Client.GetAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}");
        var entity = await getResp.Content.ReadFromJsonAsync<DynamicEntityDto>();
        Assert.NotNull(entity?.UpdatedAt);

        // 把 UTC 时间转 +08:00，序列化后回传
        var plus8 = entity!.UpdatedAt!.Value.ToOffset(TimeSpan.FromHours(8));
        var header = plus8.ToString("O");

        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 10L,
                ["label"] = "patched"
            })
        };
        req.Headers.Add("X-Expected-Updated-At", header);

        var resp = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    /// <summary>
    /// 陈旧版本号 → 应返回 409 + currentUpdatedAt。
    /// </summary>
    [Fact]
    public async Task Put_StaleUpdatedAt_Returns409WithCurrentUpdatedAt()
    {
        await TestData.EnsureSchemaAsync(Client);

        var id = GuidFromInt(90002);
        await PutEntityAsync(TestData.EntityType, id,
            new Dictionary<string, object?> { ["amount"] = 20L });

        // 用错误的期望时间（比真实时间早 1 天）
        var stale = DateTimeOffset.UtcNow.AddDays(-1);

        using var req = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/eav/{TestData.EntityType}/entities/{id}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 21L
            })
        };
        req.Headers.Add("X-Expected-Updated-At", stale.ToString("O"));

        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("currentUpdatedAt", body);
    }
}
