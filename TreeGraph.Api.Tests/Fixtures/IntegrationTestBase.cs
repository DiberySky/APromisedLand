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
        string entityType, string entityId, object payload)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{entityType}/entities/{entityId}", payload);
        resp.EnsureSuccessStatusCode();
        return entityId;
    }
}
