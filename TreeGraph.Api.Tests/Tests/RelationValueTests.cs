using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// relation 值读写（R6）：写入 / 读取 / 单值覆盖 / 删除 / 目标节点校验 /
/// 同类型端点冲突 409 / 自环 / 乐观锁 / 审计 Insert-Update-Delete。
/// </summary>
public class RelationValueTests : IntegrationTestBase
{
    private const string Type = "relval";
    private const string AttrA = "rel_a";
    private const string AttrB = "rel_b";
    private const string AttrC = "rel_c";

    public RelationValueTests(EavApiFactory factory) : base(factory) { }

    private async Task EnsureSchemaAsync()
    {
        var typeResp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "关系值测试" });
        if (typeResp.StatusCode != HttpStatusCode.Conflict)
            typeResp.EnsureSuccessStatusCode();

        await CreateRelationAttributeAsync(AttrA, "belongs_to");
        await CreateRelationAttributeAsync(AttrB, "belongs_to");
        await CreateRelationAttributeAsync(AttrC, "owns");
    }

    private async Task CreateRelationAttributeAsync(string name, string relationType)
    {
        var resp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = Type,
            attributeName = name,
            displayName = name,
            dataType = "relation",
            relationType
        });
        if (resp.StatusCode != HttpStatusCode.Conflict)
            resp.EnsureSuccessStatusCode();
    }

    private async Task<string> CreateNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString();
        var resp = await Client.PostAsJsonAsync("/api/Node", new { id, name });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    private static string NewEntityId() => Guid.NewGuid().ToString();

    private async Task<JsonElement> GetPropertiesAsync(string entityId)
    {
        var resp = await Client.GetAsync($"/api/eav/{Type}/entities/{entityId}");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("properties").Clone();
    }

    [Fact]
    public async Task PutRelation_ThenGet_RoundTripsValueIdTargetAndName()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var target = await CreateNodeAsync("一号站");

        var put = await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>
            {
                [AttrA] = new { toNodeId = target, name = "主供" }
            });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var props = await GetPropertiesAsync(from);
        var rel = props.GetProperty(AttrA);
        Assert.False(string.IsNullOrWhiteSpace(rel.GetProperty("valueId").GetString()));
        Assert.Equal(target, rel.GetProperty("toNodeId").GetString());
        Assert.Equal("主供", rel.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PutRelation_SameAttribute_KeepsSingleValueAndOverridesTarget()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var n1 = await CreateNodeAsync("目标一");
        var n2 = await CreateNodeAsync("目标二");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = n1 } }))
            .EnsureSuccessStatusCode();
        var valueIdBefore = (await GetPropertiesAsync(from))
            .GetProperty(AttrA).GetProperty("valueId").GetString();

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = n2, name = "备用" } }))
            .EnsureSuccessStatusCode();

        var rel = (await GetPropertiesAsync(from)).GetProperty(AttrA);
        Assert.Equal(n2, rel.GetProperty("toNodeId").GetString());
        Assert.Equal("备用", rel.GetProperty("name").GetString());
        // 单值语义：更新既有行，valueId 不变
        Assert.Equal(valueIdBefore, rel.GetProperty("valueId").GetString());
    }

    [Fact]
    public async Task PutRelation_MissingToNodeId_Returns400()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();

        var resp = await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { name = "无目标" } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("toNodeId", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutRelation_TargetNodeNotExist_Returns400()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var ghost = Guid.NewGuid().ToString();

        var resp = await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = ghost } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("目标节点不存在", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutRelation_SameTypeSameEndpoints_DifferentAttributes_Returns409()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var target = await CreateNodeAsync("同一端点");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = target } }))
            .EnsureSuccessStatusCode();

        // PATCH 保留 AttrA，再写入同类型同端点的 AttrB → 拒绝
        var resp = await Client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrB] = new { toNodeId = target } });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("该起点已指向该终点", await resp.Content.ReadAsStringAsync());

        // 原值仍在，冲突属性未落库
        var props = await GetPropertiesAsync(from);
        Assert.Equal(target, props.GetProperty(AttrA).GetProperty("toNodeId").GetString());
        Assert.False(props.TryGetProperty(AttrB, out _));
    }

    [Fact]
    public async Task PutRelation_SameTypeDifferentEndpoints_Coexist()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var n1 = await CreateNodeAsync("端点一");
        var n2 = await CreateNodeAsync("端点二");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = n1 } }))
            .EnsureSuccessStatusCode();
        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>
            {
                [AttrA] = new { toNodeId = n1 },
                [AttrB] = new { toNodeId = n2 }
            })).EnsureSuccessStatusCode();

        var props = await GetPropertiesAsync(from);
        Assert.Equal(n1, props.GetProperty(AttrA).GetProperty("toNodeId").GetString());
        Assert.Equal(n2, props.GetProperty(AttrB).GetProperty("toNodeId").GetString());
    }

    [Fact]
    public async Task PutRelation_SelfLoop_IsAllowed()
    {
        await EnsureSchemaAsync();
        var node = await CreateNodeAsync("自环节点");

        var resp = await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{node}",
            new Dictionary<string, object?> { [AttrC] = new { toNodeId = node, name = "自环" } });

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        var rel = (await GetPropertiesAsync(node)).GetProperty(AttrC);
        Assert.Equal(node, rel.GetProperty("toNodeId").GetString());
    }

    [Fact]
    public async Task PatchRelationNull_DeletesValue()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var target = await CreateNodeAsync("待删除目标");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = target } }))
            .EnsureSuccessStatusCode();

        var resp = await Client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = null });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        Assert.False((await GetPropertiesAsync(from)).TryGetProperty(AttrA, out _));
    }

    [Fact]
    public async Task PutWithoutRelation_DeletesValue()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var target = await CreateNodeAsync("全量替换目标");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = target } }))
            .EnsureSuccessStatusCode();

        // PUT 全量替换：未出现的 relation 属性一并删除
        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>())).EnsureSuccessStatusCode();

        Assert.False((await GetPropertiesAsync(from)).TryGetProperty(AttrA, out _));
    }

    [Fact]
    public async Task PutRelation_OptimisticLock_UsesRelationUpdatedAt()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var target = await CreateNodeAsync("锁目标");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = target } }))
            .EnsureSuccessStatusCode();

        var getResp = await Client.GetAsync($"/api/eav/{Type}/entities/{from}");
        using var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        var updatedAt = doc.RootElement.GetProperty("data").GetProperty("updatedAt").GetDateTimeOffset();

        using var okReq = new HttpRequestMessage(
            HttpMethod.Put, $"/api/eav/{Type}/entities/{from}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                [AttrA] = new { toNodeId = target, name = "重命名" }
            })
        };
        okReq.Headers.Add("X-Expected-Updated-At", updatedAt.ToString("O"));
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(okReq)).StatusCode);

        using var staleReq = new HttpRequestMessage(
            HttpMethod.Put, $"/api/eav/{Type}/entities/{from}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                [AttrA] = new { toNodeId = target }
            })
        };
        staleReq.Headers.Add("X-Expected-Updated-At",
            updatedAt.AddDays(-1).ToString("O"));
        Assert.Equal(HttpStatusCode.Conflict, (await Client.SendAsync(staleReq)).StatusCode);
    }

    [Fact]
    public async Task History_RecordsRelationInsertUpdateDelete()
    {
        await EnsureSchemaAsync();
        var from = NewEntityId();
        var n1 = await CreateNodeAsync("审计目标一");
        var n2 = await CreateNodeAsync("审计目标二");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = new { toNodeId = n1, name = "初" } }))
            .EnsureSuccessStatusCode();
        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>
            {
                [AttrA] = new { toNodeId = n2, name = "改" }
            })).EnsureSuccessStatusCode();
        (await Client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [AttrA] = null }))
            .EnsureSuccessStatusCode();

        var resp = await Client.GetAsync(
            $"/api/eav/{Type}/entities/{from}/history");
        resp.EnsureSuccessStatusCode();
        var history = await resp.Content.ReadEavAsync<List<JsonElement>>();
        Assert.NotNull(history);

        var related = history!
            .Where(h => h.GetProperty("attributeName").GetString() == AttrA)
            .ToList();
        Assert.Contains(related, h => h.GetProperty("changeType").GetString() == "Insert");
        Assert.Contains(related, h => h.GetProperty("changeType").GetString() == "Update");
        Assert.Contains(related, h => h.GetProperty("changeType").GetString() == "Delete");

        // 审计载荷是 { valueId, toNodeId, name } 的 JSON
        var insert = related.Single(h => h.GetProperty("changeType").GetString() == "Insert");
        using var newValue = JsonDocument.Parse(insert.GetProperty("newValue").GetString()!);
        Assert.Equal(n1, newValue.RootElement.GetProperty("toNodeId").GetString());
        Assert.Equal("初", newValue.RootElement.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            newValue.RootElement.GetProperty("valueId").GetString()));
    }
}
