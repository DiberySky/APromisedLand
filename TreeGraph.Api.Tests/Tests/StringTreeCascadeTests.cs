using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.StringTree.Contracts;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// StringTreeSky 节点删除级联测试：删除树节点（含子孙）时，
/// EAV attribute_values 中以该节点 Id（GUID）为 EntityId 的数据必须一并清除，
/// 且不影响其它节点的 EAV 数据。
/// </summary>
public class StringTreeCascadeTests(EavApiFactory factory) : IntegrationTestBase(factory)
{
    private const string TreeBase = "/api/string-tree/nodes";
    private const string AttrName = "cascadeNote";

    private async Task EnsureSchemaAsync()
    {
        // 实体类型（幂等：409 视为已存在）
        var etResp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = StringTreeEntityTypes.Node, displayName = "字符串树节点" });
        if (etResp.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.OK))
            etResp.EnsureSuccessStatusCode();

        // 属性定义（幂等：409 视为已存在）
        var attrResp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = StringTreeEntityTypes.Node,
            attributeName = AttrName,
            displayName = "级联备注",
            dataType = "string",
            isRequired = false,
            isSearchable = false,
            isSortable = false,
            displayOrder = 1
        });
        if (attrResp.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.OK))
            attrResp.EnsureSuccessStatusCode();
    }

    private async Task<StringNodeDto> CreateNodeAsync(string name, string? parentId = null)
    {
        var resp = await Client.PostAsJsonAsync(TreeBase,
            new StringNodeDto { Name = name, ParentId = parentId });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content
            .ReadFromJsonAsync<StringTreeResponse<StringNodeDto>>();
        Assert.True(body!.Success, body.Message);
        Assert.True(Guid.TryParse(body.Data!.Id, out _), $"节点 Id 应为 GUID，实际：{body.Data.Id}");
        return body.Data!;
    }

    private async Task PutNoteAsync(string nodeId, string note)
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{StringTreeEntityTypes.Node}/entities/{nodeId}",
            new Dictionary<string, object?> { [AttrName] = note });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<bool> HasNoteAsync(string nodeId)
    {
        var resp = await Client.GetAsync(
            $"/api/eav/{StringTreeEntityTypes.Node}/entities/{nodeId}");
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDoc>();
        return doc!.Properties.ContainsKey(AttrName);
    }

    private sealed record JsonDoc(Dictionary<string, System.Text.Json.JsonElement> Properties);

    [Fact]
    public async Task DeleteNode_CascadesEavValuesOfDescendants_AndKeepsOthers()
    {
        await EnsureSchemaAsync();

        // 被删除的子树：root → child
        var root = await CreateNodeAsync("级联根");
        var child = await CreateNodeAsync("级联子", root.Id);

        // 保留的另一棵子树：other → otherChild
        var other = await CreateNodeAsync("保留根");
        var otherChild = await CreateNodeAsync("保留子", other.Id);

        await PutNoteAsync(child.Id, "将随子树删除");
        await PutNoteAsync(otherChild.Id, "应当保留");

        try
        {
            Assert.True(await HasNoteAsync(child.Id));
            Assert.True(await HasNoteAsync(otherChild.Id));

            var delResp = await Client.DeleteAsync($"{TreeBase}/{root.Id}");
            delResp.EnsureSuccessStatusCode();

            // 树节点已物理删除
            Assert.Equal(HttpStatusCode.NotFound,
                (await Client.GetAsync($"{TreeBase}/{child.Id}")).StatusCode);

            // 子孙的 EAV 属性已级联清除
            Assert.False(await HasNoteAsync(child.Id));

            // 其它节点的 EAV 数据不受影响
            Assert.True(await HasNoteAsync(otherChild.Id));
        }
        finally
        {
            await Client.DeleteAsync($"{TreeBase}/{root.Id}");
            await Client.DeleteAsync($"{TreeBase}/{other.Id}");
        }
    }
}
