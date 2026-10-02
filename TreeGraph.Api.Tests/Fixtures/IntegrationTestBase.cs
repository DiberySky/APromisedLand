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
    /// PUT 实体并返回 id。payload 用匿名对象或 Dictionary。
    /// 默认用 PUT（全量替换语义）。
    /// </summary>
    protected async Task<long> PutEntityAsync(
        string entityType, long entityId, object payload)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{entityType}/entities/{entityId}", payload);
        resp.EnsureSuccessStatusCode();
        return entityId;
    }
}
