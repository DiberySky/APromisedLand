using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Fixtures;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly EavApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(EavApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>内置边类型不再随启动播种（生产改「预置关系」按需创建），测试库由 fixture 补种。</summary>
    public virtual Task InitializeAsync() => EdgeTypeTestSeed.EnsureAsync(Factory);

    public virtual Task DisposeAsync() => Task.CompletedTask;

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
        string entityType, string entityId, object payload)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{entityType}/entities/{entityId}", payload);
        resp.EnsureSuccessStatusCode();
        return entityId;
    }

    /// <summary>拼出 iNode 下某条命名绑定的路由。</summary>
    protected static string InodeBindingUrl(string inodeId, string bindingId)
        => $"/api/inode/{inodeId}/bindings/{bindingId}";

    /// <summary>
    /// POST 新建命名绑定（每条绑定自带一份私有 EAV 数据），
    /// 返回服务端生成的 (bindingId, entityId)。
    /// </summary>
    protected async Task<(string BindingId, string EntityId)> CreateInodeBindingAsync(
        string inodeId, string entityType, string name)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{inodeId}/bindings",
            new CreateBindingRequest { EntityType = entityType, Name = name });
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"创建绑定失败 {resp.StatusCode}: " +
                await resp.Content.ReadAsStringAsync());
        }

        using var doc = JsonDocument.Parse(
            await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data)
            || !data.TryGetProperty("bindingId", out var bindingProp)
            || !data.TryGetProperty("entityId", out var entityProp)
            || bindingProp.ValueKind != JsonValueKind.String
            || entityProp.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(
                "创建绑定的响应缺少 data.bindingId / data.entityId：" + root.GetRawText());
        }

        var bindingId = bindingProp.GetString();
        var entityId = entityProp.GetString();
        if (string.IsNullOrEmpty(bindingId) || string.IsNullOrEmpty(entityId))
            throw new InvalidOperationException(
                "创建绑定的响应 bindingId / entityId 为空");

        return (bindingId!, entityId!);
    }
}
