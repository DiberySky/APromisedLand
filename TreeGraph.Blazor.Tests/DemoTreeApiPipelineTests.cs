using TreeGraph.Blazor.Services.DemoTree;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// 演示树“组件 ApiClient → DemoTreeApiHandler → InMemoryTreeStore”完整管道测试。
/// 不发真实 HTTP：DemoTreeApiHandler 是终端 DelegatingHandler。
/// </summary>
public class DemoTreeApiPipelineTests
{
    private static DiberyTreeApiClient<StringTreeNode> CreateClient(InMemoryTreeStore store)
    {
        var http = new HttpClient(new DemoTreeApiHandler(store))
        {
            BaseAddress = new Uri("http://treedemo.local/"),
        };
        return new DiberyTreeApiClient<StringTreeNode>(http);
    }

    private static TreeNodeDto<StringTreeNode> Dto(StringTreeNode value, string parentId)
        => new() { Id = value.Id, ParentId = parentId, Value = value, Text = value.Name };

    [Fact]
    public async Task SeedData_RootsAndChildren_AreLoadedOrdered()
    {
        var api = CreateClient(new InMemoryTreeStore());

        var roots = await api.GetRootNodesAsync();
        Assert.Single(roots);
        Assert.Equal("物品总类", roots[0].Text);

        var electronics = await api.GetChildrenAsync("cat-electronics");
        Assert.Equal(["智能手机", "笔记本电脑"], electronics.Select(n => n.Text!).ToArray());

        var path = await api.GetAncestorPathAsync("leaf-laptop");
        Assert.Equal(["root", "cat-electronics", "leaf-laptop"], path.ToArray());
    }

    [Fact]
    public async Task CreateNode_AppearsUnderParent_AndMarksParentHasChildren()
    {
        var store = new InMemoryTreeStore();
        var api = CreateClient(store);

        var created = await api.CreateNodeAsync(Dto(
            new StringTreeNode { Name = "书签", CanHaveChildren = false, SortOrder = 0 },
            parentId: "cat-furniture"));

        Assert.False(string.IsNullOrEmpty(created.Id));

        var children = await api.GetChildrenAsync("cat-furniture");
        Assert.Contains(children, n => n.Text == "书签");
        Assert.True(children[0].Value!.Parent?.Name == "家具（空分类）");
    }

    [Fact]
    public async Task UpdateNode_MovesLeaf_ToNewParent()
    {
        var store = new InMemoryTreeStore();
        var api = CreateClient(store);

        // 智能手机：电子产品 → 办公用品
        var phone = store.GetChildren("cat-electronics").Single(n => n.Id == "leaf-phone").Value!;
        phone.ParentId = "cat-office";
        await api.UpdateNodeAsync(phone.Id, Dto(phone, "cat-office"));

        var office = await api.GetChildrenAsync("cat-office");
        Assert.Contains(office, n => n.Id == "leaf-phone");

        var electronics = await api.GetChildrenAsync("cat-electronics");
        Assert.DoesNotContain(electronics, n => n.Id == "leaf-phone");

        var path = await api.GetAncestorPathAsync("leaf-phone");
        Assert.Equal(["root", "cat-office", "leaf-phone"], path.ToArray());
    }

    [Fact]
    public async Task UpdateChildren_ReordersSiblings_ByRequestOrder()
    {
        var store = new InMemoryTreeStore();
        var api = CreateClient(store);

        var a = (await api.CreateNodeAsync(Dto(
            new StringTreeNode { Name = "甲", CanHaveChildren = false, SortOrder = 0 }, "cat-furniture"))).Value!;
        var b = (await api.CreateNodeAsync(Dto(
            new StringTreeNode { Name = "乙", CanHaveChildren = false, SortOrder = 1 }, "cat-furniture"))).Value!;

        // 请求顺序 [乙, 甲] → 乙排前
        await api.UpdateChildrenAsync(new TreeNodeDto<StringTreeNode>
        {
            Id = "cat-furniture",
            Children =
            [
                new TreeNodeDto<StringTreeNode> { Id = b.Id, Value = b },
                new TreeNodeDto<StringTreeNode> { Id = a.Id, Value = a },
            ],
        });

        var children = await api.GetChildrenAsync("cat-furniture");
        Assert.Equal(["乙", "甲"], children.Select(n => n.Text!).ToArray());
        Assert.Equal([0, 1], children.Select(n => n.SortOrder).ToArray());
    }

    [Fact]
    public async Task DeleteNode_RemovesAllDescendants()
    {
        var store = new InMemoryTreeStore();
        var api = CreateClient(store);

        var deleted = await api.DeleteNodeAsync("cat-electronics");

        Assert.True(deleted);
        var rootsChildren = await api.GetChildrenAsync("root");
        Assert.DoesNotContain(rootsChildren, n => n.Id == "cat-electronics");

        // 叶子随之删除：祖先路径 404 时 handler 返回错误，这里直接验证存储层
        Assert.Empty(store.GetAncestorPath("leaf-laptop"));
    }

    [Fact]
    public async Task MoveNode_UnderOwnDescendant_IsRejected()
    {
        var store = new InMemoryTreeStore();
        var api = CreateClient(store);

        // 试图把根节点移到其后代 leaf-paper 下：存储层必须拒绝（防环）。
        // 用副本构造请求体（模拟客户端序列化的数据），不污染存储层图结构。
        var root = store.GetRoots()[0].Value!;
        var requestPayload = new StringTreeNode
        {
            Id = root.Id,
            Name = root.Name,
            CanHaveChildren = root.CanHaveChildren,
            SortOrder = root.SortOrder,
            ParentId = "leaf-paper",
        };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            api.UpdateNodeAsync(root.Id, Dto(requestPayload, "leaf-paper")));

        // 拒绝后图结构未被破坏：leaf-paper 仍是 root→办公用品 下的叶子，没有任何子节点
        Assert.Equal(["root", "cat-office", "leaf-paper"], store.GetAncestorPath("leaf-paper").ToArray());
        Assert.Empty(store.GetChildren("leaf-paper"));
    }

    [Fact]
    public async Task MoveEndpoint_RelocatesNode_ToRoot()
    {
        var api = CreateClient(new InMemoryTreeStore());

        var moved = await api.MoveNodeAsync("leaf-phone", newParentId: null);

        Assert.True(moved);
        var roots = await api.GetRootNodesAsync();
        // 移到根层后，根层除了“物品总类”还应有“智能手机”
        Assert.Contains(roots, n => n.Id == "leaf-phone");
    }
}

