using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// property_tree（属性树）类型端到端：容器型，挂载关系存 eav_tree_roots 映射表
/// （映射 id 即 rootNode.Id，值不落 attribute_values）。
/// 值契约为对象 { rootId, name }：创建时 name 必填（前后端守卫）；已挂载时
/// 提交同 rootId + 新 name 即行内改名（同步 nodes.name）；显式 null 卸载——
/// rootNode 有子节点时守卫拒删，空树连根清除。
/// </summary>
public class PropertyTreeAttributeTests : IntegrationTestBase
{
    private const string Type = "proptree";
    private const string Attr = "ptree";

    public PropertyTreeAttributeTests(EavApiFactory factory) : base(factory) { }

    private async Task EnsureSchemaAsync()
    {
        var typeResp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "属性树测试" });
        if (typeResp.StatusCode != HttpStatusCode.Conflict)
            typeResp.EnsureSuccessStatusCode();

        var attrResp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = Type,
            attributeName = Attr,
            displayName = "属性树",
            dataType = "property_tree",
            isRequired = false,
            isSearchable = false,
            isSortable = false
        });
        if (attrResp.StatusCode != HttpStatusCode.Conflict)
            attrResp.EnsureSuccessStatusCode();
    }

    /// <summary>读实体属性字典。</summary>
    private async Task<JsonElement> GetPropsAsync(string id)
    {
        var get = await Client.GetAsync($"/api/eav/{Type}/entities/{id}");
        get.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("properties").Clone();
    }

    /// <summary>挂载属性树并返回 { rootId, name } 的 rootId。</summary>
    private async Task<string> MountAsync(string id, string name)
    {
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { name } }))
            .EnsureSuccessStatusCode();

        var props = await GetPropsAsync(id);
        return props.GetProperty(Attr).GetProperty("rootId").GetString()!;
    }

    [Fact]
    public async Task Create_OnTreeHost_Returns400()
    {
        // 树节点宿主（EavTree）禁用 property_tree：避免「树节点的属性又是一棵树」套娃
        var resp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = "EavTree",
            attributeName = "tree_node_ptree",
            displayName = "属性树",
            dataType = "property_tree"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("树节点", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Put_WithName_CreatesRoot_AndReadsBackObject()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93001);

        // 提交 { name } → 后端按指定名建根并回填 { rootId, name }
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { name = "机电属性树" } }))
            .EnsureSuccessStatusCode();

        var props = await GetPropsAsync(id);
        Assert.True(props.TryGetProperty(Attr, out var v), "属性应已写入");

        var rootId = v.GetProperty("rootId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(rootId));
        Assert.True(Guid.TryParse(rootId, out _), "rootId 应为 GUID");
        Assert.Equal("机电属性树", v.GetProperty("name").GetString());

        // rootNode 确已在 EavTree 注册且名为指定名（信封 data.text）
        var tree = await Client.GetAsync($"/EavTree/{rootId}");
        tree.EnsureSuccessStatusCode();
        var treeDoc = JsonDocument.Parse(await tree.Content.ReadAsStringAsync());
        Assert.Equal("机电属性树",
            treeDoc.RootElement.GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Put_WithoutName_Returns400()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93005);

        // 创建时 name 必填：空名 → 400
        var emptyName = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { name = "" } });
        Assert.Equal(HttpStatusCode.BadRequest, emptyName.StatusCode);
        Assert.Contains("属性树名称不能为空", await emptyName.Content.ReadAsStringAsync());

        // 只给空对象（name 缺省）同样 → 400
        var noName = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Contains("属性树名称不能为空", await noName.Content.ReadAsStringAsync());

        // 未建根：属性不存在
        var props = await GetPropsAsync(id);
        Assert.False(props.TryGetProperty(Attr, out _), "校验失败不应留下挂载");
    }

    [Fact]
    public async Task Put_Rename_SyncsRootNodeName()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93006);
        var rootId = await MountAsync(id, "旧名");

        // 行内改名：同 rootId + 新 name
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { rootId, name = "新名" } }))
            .EnsureSuccessStatusCode();

        // 读回对象名为新名
        var props = await GetPropsAsync(id);
        var v = props.GetProperty(Attr);
        Assert.Equal(rootId, v.GetProperty("rootId").GetString());
        Assert.Equal("新名", v.GetProperty("name").GetString());

        // nodes.name（树端唯一真源）已同步（信封 data.text）
        var tree = await Client.GetAsync($"/EavTree/{rootId}");
        tree.EnsureSuccessStatusCode();
        var treeDoc = JsonDocument.Parse(await tree.Content.ReadAsStringAsync());
        Assert.Equal("新名",
            treeDoc.RootElement.GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Put_Null_WhenTreeHasChildren_Returns400()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93002);
        var rootId = await MountAsync(id, "守卫树");

        // 给属性树加一个子节点
        var addChild = await Client.PostAsJsonAsync("/EavTree", new
        {
            text = "子节点",
            parentId = rootId,
            canHaveChildren = true
        });
        addChild.EnsureSuccessStatusCode();

        // 有子节点 → 删属性被拒（400）
        var del = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = null });
        Assert.Equal(HttpStatusCode.BadRequest, del.StatusCode);
        var body = await del.Content.ReadAsStringAsync();
        Assert.Contains("子节点", body);
    }

    [Fact]
    public async Task Put_Null_WhenTreeEmpty_DeletesAttribute()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93003);
        var rootId = await MountAsync(id, "空树");

        // 无子节点 → 允许删除
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = null })).EnsureSuccessStatusCode();

        var props = await GetPropsAsync(id);
        Assert.False(props.TryGetProperty(Attr, out _), "属性应已删除");

        // 空树已连根清除：rootNode 不复存在
        var tree = await Client.GetAsync($"/EavTree/{rootId}");
        Assert.Equal(HttpStatusCode.NotFound, tree.StatusCode);
    }

    [Fact]
    public async Task Put_SameRoot_IsIdempotent_AndDifferentRoot_Returns400()
    {
        await EnsureSchemaAsync();
        var id = GuidFromInt(93004);
        var rootId = await MountAsync(id, "幂等树");

        // 回传同一 rootId + 原名：幂等，不重建不改名
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { rootId, name = "幂等树" } }))
            .EnsureSuccessStatusCode();
        var v = (await GetPropsAsync(id)).GetProperty(Attr);
        Assert.Equal(rootId, v.GetProperty("rootId").GetString());
        Assert.Equal("幂等树", v.GetProperty("name").GetString());

        // 传入不同 rootId：拒绝（换树 = 先卸载再挂载）
        var other = Guid.NewGuid().ToString();
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = new { rootId = other, name = "别的树" } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("已挂载", await resp.Content.ReadAsStringAsync());
    }
}
