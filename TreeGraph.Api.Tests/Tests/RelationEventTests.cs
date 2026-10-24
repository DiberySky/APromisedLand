using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.Messaging;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R6 关系值事件：Insert / Update / Delete / 删除实体时批量 Delete 均发布
/// RelationChangedEvent，且载荷自带完整端点与图边类型（真实 PG 容器 + 记录型发布器）。
/// </summary>
[Collection("LineStructureEvents")]
public class RelationEventTests
{
    private const string Type = "relevt";
    private const string Attr = "rel_evt";

    private readonly RecordingApiFactory _factory;
    private readonly HttpClient _client;

    public RelationEventTests(RecordingApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task EnsureSchemaAsync()
    {
        var typeResp = await _client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "关系事件测试" });
        if (typeResp.StatusCode != HttpStatusCode.Conflict)
            typeResp.EnsureSuccessStatusCode();

        var attrResp = await _client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = Type,
            attributeName = Attr,
            displayName = Attr,
            dataType = "relation",
            relationType = "feeds"
        });
        if (attrResp.StatusCode != HttpStatusCode.Conflict)
            attrResp.EnsureSuccessStatusCode();
    }

    private async Task<string> CreateNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString();
        (await _client.PostAsJsonAsync("/api/Node", new { id, name }))
            .EnsureSuccessStatusCode();
        return id;
    }

    private async Task PutRelationAsync(string from, string to, string? name = null)
    {
        var resp = await _client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>
            {
                [Attr] = new { toNodeId = to, name }
            });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    private RelationChangedEvent SingleEvent()
    {
        var events = _factory.Publisher.Of<RelationChangedEvent>();
        return Assert.Single(events);
    }

    [Fact]
    public async Task PutRelation_PublishesInsertEvent()
    {
        await EnsureSchemaAsync();
        var from = Guid.NewGuid().ToString();
        var target = await CreateNodeAsync("事件目标一");
        _factory.Publisher.Clear();

        await PutRelationAsync(from, target, "主边");

        var evt = SingleEvent();
        Assert.Equal(RelationChangeKind.Insert, evt.Kind);
        Assert.Equal(from, evt.FromNodeId);
        Assert.Equal(target, evt.ToNodeId);
        Assert.Equal("feeds", evt.RelationType);
        Assert.Equal("主边", evt.Name);
        Assert.False(string.IsNullOrWhiteSpace(evt.ValueId));
        Assert.False(string.IsNullOrWhiteSpace(evt.AttributeId));
    }

    [Fact]
    public async Task UpdateRelation_PublishesUpdateEventWithSameValueId()
    {
        await EnsureSchemaAsync();
        var from = Guid.NewGuid().ToString();
        var n1 = await CreateNodeAsync("旧目标");
        var n2 = await CreateNodeAsync("新目标");
        _factory.Publisher.Clear();

        await PutRelationAsync(from, n1);
        var valueId = SingleEvent().ValueId;

        _factory.Publisher.Clear();
        await PutRelationAsync(from, n2, "改后");

        var evt = SingleEvent();
        Assert.Equal(RelationChangeKind.Update, evt.Kind);
        Assert.Equal(valueId, evt.ValueId);
        Assert.Equal(n2, evt.ToNodeId);
        Assert.Equal("改后", evt.Name);
    }

    [Fact]
    public async Task ClearRelation_PublishesDeleteEvent()
    {
        await EnsureSchemaAsync();
        var from = Guid.NewGuid().ToString();
        var target = await CreateNodeAsync("删除目标");
        _factory.Publisher.Clear();

        await PutRelationAsync(from, target);
        _factory.Publisher.Clear();

        (await _client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [Attr] = null }))
            .EnsureSuccessStatusCode();

        var evt = SingleEvent();
        Assert.Equal(RelationChangeKind.Delete, evt.Kind);
        Assert.Equal(from, evt.FromNodeId);
        Assert.Equal(target, evt.ToNodeId);
    }

    [Fact]
    public async Task DeleteEntity_PublishesDeleteEventForEachRelation()
    {
        await EnsureSchemaAsync();
        var from = Guid.NewGuid().ToString();
        var target = await CreateNodeAsync("连带删除目标");
        _factory.Publisher.Clear();

        await PutRelationAsync(from, target);
        var valueId = SingleEvent().ValueId;
        _factory.Publisher.Clear();

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/eav/{Type}/entities/{from}")).StatusCode);

        var evt = SingleEvent();
        Assert.Equal(RelationChangeKind.Delete, evt.Kind);
        Assert.Equal(valueId, evt.ValueId);
    }
}
