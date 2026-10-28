using System.Net.Http.Json;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

[Collection("Integration")]
public abstract class IntegrationTestBase
{
    protected readonly EavApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(EavApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>种子动态空间「武汉供电段」Id（迁移 HasData）：Edge/Line 空间化测试的归属空间。</summary>
    protected const string TestSpaceId = "7a1e3f5d-9b2c-4a8e-1f0d-3c5b7e9a1c2d";

    /// <summary>空间内 EdgeSky 路由前缀（EdgeType/Edge 均为图空间级资源）。</summary>
    protected static string EdgeBase => $"/api/spaces/{TestSpaceId}/edge";

    /// <summary>
    /// 把测试用整数 n 转换为一个合法 GUID 字符串。
    /// 格式：00000000-0000-0000-0000-{n:D12}
    /// 便于在测试日志 / DB 中按数字识别。
    /// </summary>
    protected static string GuidFromInt(long n)
        => $"00000000-0000-0000-0000-{n:D12}";

    /// <summary>
    /// PUT 实体（全量替换语义）。返回 entityId（GUID 字符串）。
    /// </summary>
    protected async Task<string> PutEntityAsync(
        string entityId, object payload)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/entities/{entityId}", payload);
        resp.EnsureSuccessStatusCode();
        return entityId;
    }
}
