using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 关系值的删除守卫（R6）：属性仍有关系值 → 400；节点作为起点/终点被引用 → 409；
/// 清空关系后节点可删除；节点仍有普通 EAV 值 → 409。
/// </summary>
public class RelationDeleteGuardTests : IntegrationTestBase
{
    private const string Type = "reldel";
    private const string Attr = "rel_link";
    private const string NoteAttr = "note";

    public RelationDeleteGuardTests(EavApiFactory factory) : base(factory) { }

    private async Task EnsureSchemaAsync()
    {
        var typeResp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "关系守卫测试" });
        if (typeResp.StatusCode != HttpStatusCode.Conflict)
            typeResp.EnsureSuccessStatusCode();

        await EnsureAttributeAsync(new
        {
            entityType = Type,
            attributeName = Attr,
            displayName = Attr,
            dataType = "relation",
            relationType = "depends_on"
        });
        await EnsureAttributeAsync(new
        {
            entityType = Type,
            attributeName = NoteAttr,
            displayName = NoteAttr,
            dataType = "string"
        });
    }

    private async Task EnsureAttributeAsync(object body)
    {
        var resp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", body);
        if (resp.StatusCode != HttpStatusCode.Conflict)
            resp.EnsureSuccessStatusCode();
    }

    private async Task<string> CreateNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString();
        (await Client.PostAsJsonAsync("/api/Node", new { id, name }))
            .EnsureSuccessStatusCode();
        return id;
    }

    private async Task WriteRelationAsync(string from, string to)
    {
        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?>
            {
                [Attr] = new { toNodeId = to }
            })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteAttribute_WhenRelationValueExists_Returns400()
    {
        await EnsureSchemaAsync();
        var from = Guid.NewGuid().ToString();
        var target = await CreateNodeAsync("属性守卫目标");
        await WriteRelationAsync(from, target);

        var attrs = await Client.GetEavAsync<List<System.Text.Json.JsonElement>>(
            $"/api/eav/metadata/attributes?entityType={Type}");
        var attrId = attrs!.Single(a =>
            a.GetProperty("attributeName").GetString() == Attr)
            .GetProperty("attributeId").GetString();

        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/attributes/{attrId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("实例数据", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NodeDelete_WhenOutgoingRelation_Returns409()
    {
        await EnsureSchemaAsync();
        var from = await CreateNodeAsync("起点");
        var target = await CreateNodeAsync("终点");
        await WriteRelationAsync(from, target);

        var resp = await Client.DeleteAsync($"/api/Node/{from}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("作为起点", await resp.Content.ReadAsStringAsync());

        // 节点保持原状
        Assert.Equal(HttpStatusCode.OK,
            (await Client.GetAsync($"/api/Node/{from}")).StatusCode);
    }

    [Fact]
    public async Task NodeDelete_WhenIncomingRelation_Returns409()
    {
        await EnsureSchemaAsync();
        var from = await CreateNodeAsync("起点二");
        var target = await CreateNodeAsync("终点二");
        await WriteRelationAsync(from, target);

        var resp = await Client.DeleteAsync($"/api/Node/{target}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("被 1 条关系引用", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NodeDelete_AfterRelationsCleared_Succeeds()
    {
        await EnsureSchemaAsync();
        var from = await CreateNodeAsync("可删起点");
        var target = await CreateNodeAsync("可删终点");
        await WriteRelationAsync(from, target);

        (await Client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{from}",
            new Dictionary<string, object?> { [Attr] = null }))
            .EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/Node/{from}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/Node/{target}")).StatusCode);
    }

    [Fact]
    public async Task NodeDelete_WhenPlainEavValuesExist_Returns409()
    {
        await EnsureSchemaAsync();
        var node = await CreateNodeAsync("有属性节点");

        (await Client.PutAsJsonAsync($"/api/eav/{Type}/entities/{node}",
            new Dictionary<string, object?> { [NoteAttr] = "值" }))
            .EnsureSuccessStatusCode();

        var resp = await Client.DeleteAsync($"/api/Node/{node}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("EAV 属性数据", await resp.Content.ReadAsStringAsync());

        // 清空属性值后可删除
        (await Client.PatchAsJsonAsync($"/api/eav/{Type}/entities/{node}",
            new Dictionary<string, object?> { [NoteAttr] = null }))
            .EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/Node/{node}")).StatusCode);
    }
}
