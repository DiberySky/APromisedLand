using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Blazor.Shared.Trees.Models;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// TreeSky 后端集成测试：覆盖 TreeControllerBase&lt;StringTreeNode&gt; 的 9 个端点
/// （roots/children/query/full/POST 创建/POST children 排序/PUT/DELETE/move/ancestors）。
/// 每个测试自建节点、用后清理，互不依赖种子数据。
/// </summary>
public class TreeSkyTests(EavApiFactory factory) : IntegrationTestBase(factory)
{
    private const string Base = "/StringTreeNode";

    // ==================== 辅助 ====================

    private async Task<TreeNodeDto<StringTreeNode>> CreateNodeAsync(
        string name, string? parentId = null, int sortOrder = 0)
    {
        var resp = await Client.PostAsJsonAsync(Base, new TreeNodeDto<StringTreeNode>
        {
            Text = name,
            ParentId = parentId,
            SortOrder = sortOrder,
            Value = new StringTreeNode { Name = name, SortOrder = sortOrder },
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>();
        Assert.True(body!.Success, body.Message);
        return body.Data!;
    }

    private async Task DeleteNodeAsync(string id)
        => await Client.DeleteAsync($"{Base}/{id}");

    // ==================== 查询端点 ====================

    [Fact]
    public async Task Roots_ReturnsSeededRoot()
    {
        var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>($"{Base}/roots");
        Assert.True(body!.Success);
        Assert.Contains(body.Data!, n => n.Text == "物品总类");
    }

    [Fact]
    public async Task Roots_ById_ReturnsSingleRoot()
    {
        var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>($"{Base}/roots/root");
        Assert.True(body!.Success);
        var node = Assert.Single(body.Data!);
        Assert.Equal("root", node.Id);
    }

    [Fact]
    public async Task Children_ReturnsSortedChildren()
    {
        var parent = await CreateNodeAsync("子查询父");
        var c2 = await CreateNodeAsync("B 子", parent.Id, 2);
        var c1 = await CreateNodeAsync("A 子", parent.Id, 1);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{parent.Id}");
            Assert.True(body!.Success);
            Assert.Equal(2, body.Data!.Count);
            Assert.Equal(c1.Id, body.Data![0].Id); // SortOrder 1 在前
            Assert.Equal(c2.Id, body.Data![1].Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id); // 级联删除子节点
        }
    }

    [Fact]
    public async Task Query_FiltersByParentId()
    {
        var parent = await CreateNodeAsync("查询父");
        var child = await CreateNodeAsync("查询子", parent.Id);
        try
        {
            var resp = await Client.PostAsJsonAsync($"{Base}/query",
                new TreeQueryParams { ParentId = parent.Id });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>();
            var hit = Assert.Single(body!.Data!);
            Assert.Equal(child.Id, hit.Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id);
        }
    }

    [Fact]
    public async Task FullTree_NestsAllDescendants()
    {
        var root = await CreateNodeAsync("整树根");
        var child = await CreateNodeAsync("整树子", root.Id);
        var grandchild = await CreateNodeAsync("整树孙", child.Id);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>(
                $"{Base}/full?rootId={root.Id}");
            Assert.True(body!.Success);
            var tree = body.Data!;
            Assert.Equal(root.Id, tree.Id);
            var childDto = Assert.Single(tree.Children!);
            Assert.Equal(child.Id, childDto.Id);
            var grandDto = Assert.Single(childDto.Children!);
            Assert.Equal(grandchild.Id, grandDto.Id);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    [Fact]
    public async Task Ancestors_ReturnsRootToTargetPath()
    {
        var root = await CreateNodeAsync("路径根");
        var child = await CreateNodeAsync("路径子", root.Id);
        try
        {
            var body = await Client.GetFromJsonAsync<ApiResponse<List<string>>>($"{Base}/{child.Id}/ancestors");
            Assert.True(body!.Success);
            Assert.Equal(new List<string> { root.Id, child.Id }, body.Data!);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    // ==================== 写入端点 ====================

    [Fact]
    public async Task Create_AssignsIdAndPersists()
    {
        var created = await CreateNodeAsync("新建节点");
        try
        {
            Assert.False(string.IsNullOrWhiteSpace(created.Id));
            Assert.Equal("新建节点", created.Text);
            Assert.False(created.HasChildren);
        }
        finally
        {
            await DeleteNodeAsync(created.Id);
        }
    }

    [Fact]
    public async Task Update_ChangesNameAndSortOrder()
    {
        var created = await CreateNodeAsync("改前");
        try
        {
            var resp = await Client.PutAsJsonAsync($"{Base}/{created.Id}", new TreeNodeDto<StringTreeNode>
            {
                Id = created.Id,
                Text = "改后",
                SortOrder = 7,
                Value = new StringTreeNode { Id = created.Id, Name = "改后", SortOrder = 7 },
            });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ApiResponse<TreeNodeDto<StringTreeNode>>>();
            Assert.Equal("改后", body!.Data!.Value!.Name);
            Assert.Equal(7, body.Data.SortOrder);
        }
        finally
        {
            await DeleteNodeAsync(created.Id);
        }
    }

    [Fact]
    public async Task UpdateChildren_ReordersByGivenSequence()
    {
        var parent = await CreateNodeAsync("排序父");
        var a = await CreateNodeAsync("A", parent.Id, 0);
        var b = await CreateNodeAsync("B", parent.Id, 1);
        try
        {
            // 传入 b 在前 a 在后 → SortOrder 应翻转
            var resp = await Client.PostAsJsonAsync($"{Base}/children", new TreeNodeDto<StringTreeNode>
            {
                Id = parent.Id,
                Text = "排序父",
                Children =
                [
                    new TreeNodeDto<StringTreeNode> { Id = b.Id, Text = "B" },
                    new TreeNodeDto<StringTreeNode> { Id = a.Id, Text = "A" },
                ],
            });
            resp.EnsureSuccessStatusCode();

            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{parent.Id}");
            Assert.Equal(b.Id, body!.Data![0].Id);
            Assert.Equal(a.Id, body.Data![1].Id);
        }
        finally
        {
            await DeleteNodeAsync(parent.Id);
        }
    }

    [Fact]
    public async Task Delete_CascadesAllDescendants()
    {
        var root = await CreateNodeAsync("删除根");
        var child = await CreateNodeAsync("删除子", root.Id);
        var grandchild = await CreateNodeAsync("删除孙", child.Id);

        var resp = await Client.DeleteAsync($"{Base}/{root.Id}");
        resp.EnsureSuccessStatusCode();

        // 子孙应已被级联删除
        var childResp = await Client.GetAsync($"{Base}/{child.Id}/ancestors");
        Assert.Equal(HttpStatusCode.NotFound, childResp.StatusCode);
        var grandResp = await Client.GetAsync($"{Base}/{grandchild.Id}/ancestors");
        Assert.Equal(HttpStatusCode.NotFound, grandResp.StatusCode);
    }

    [Fact]
    public async Task Move_ReparentsNode()
    {
        var p1 = await CreateNodeAsync("父一");
        var p2 = await CreateNodeAsync("父二");
        var child = await CreateNodeAsync("被移动", p1.Id);
        try
        {
            var resp = await Client.PostAsync($"{Base}/move?nodeId={child.Id}&newParentId={p2.Id}", null);
            resp.EnsureSuccessStatusCode();

            var body = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{p2.Id}");
            Assert.Contains(body!.Data!, n => n.Id == child.Id);

            var oldBody = await Client.GetFromJsonAsync<ApiResponse<List<TreeNodeDto<StringTreeNode>>>>(
                $"{Base}/children/{p1.Id}");
            Assert.Empty(oldBody!.Data!);
        }
        finally
        {
            await DeleteNodeAsync(p1.Id);
            await DeleteNodeAsync(p2.Id); // 级联删除 child
        }
    }

    [Fact]
    public async Task Move_ToOwnDescendant_IsRejected()
    {
        var root = await CreateNodeAsync("防环根");
        var child = await CreateNodeAsync("防环子", root.Id);
        try
        {
            // 把 root 移到自己的子节点 child 下 → 应失败
            var resp = await Client.PostAsync($"{Base}/move?nodeId={root.Id}&newParentId={child.Id}", null);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

            // 自环同样拒绝
            var selfResp = await Client.PostAsync($"{Base}/move?nodeId={root.Id}&newParentId={root.Id}", null);
            Assert.Equal(HttpStatusCode.BadRequest, selfResp.StatusCode);
        }
        finally
        {
            await DeleteNodeAsync(root.Id);
        }
    }

    [Fact]
    public async Task Delete_Nonexistent_ReturnsNotFound()
    {
        var resp = await Client.DeleteAsync($"{Base}/no-such-node-id");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
