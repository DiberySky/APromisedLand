using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.TreeSky;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// TreeHelper 扩展方法单元测试。
/// 覆盖：ToTreeItemData / FindTreeItem / GetPathToNode / ExpandAsync / ExpandToNodeAsync。
/// </summary>
public class TreeHelperTests
{
    // ============================================================
    // 测试数据辅助
    // ============================================================

    private static StringTreeNode Node(
        string id, string? parentId = null, string? name = null)
        => new() { Id = id, Name = name ?? $"Node-{id}", ParentId = parentId };

    private static TreeNodeDto<StringTreeNode> Dto(
        string id,
        string? parentId = null,
        string? text = null,
        bool hasChildren = false,
        int sortOrder = 0)
        => new()
        {
            Id = id,
            Text = text ?? $"Text-{id}",
            ParentId = parentId,
            SortOrder = sortOrder,
            HasChildren = hasChildren,
            Value = Node(id, parentId),
        };

    // ============================================================
    // ToTreeItemData
    // ============================================================

    [Fact]
    public void ToTreeItemData_MapsAllScalarFields()
    {
        var dto = Dto("1", text: "Hello", hasChildren: true, sortOrder: 5);

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Equal("1", item.Value?.Id);
        Assert.Equal("Hello", item.Text);
        Assert.True(item.Expandable);
        Assert.Equal(TreeHelper.TreeItemIcons, item.Icon);
    }

    [Fact]
    public void ToTreeItemData_WithNullValue_DoesNotThrow()
    {
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Orphan",
            Value = null,   // ★ 关键：Value 为 null
        };

        // 修复前会 NRE；现在应该正常返回
        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Null(item.Value);
        Assert.Equal("Orphan", item.Text);
    }

    [Fact]
    public void ToTreeItemData_WithChildren_Recurses()
    {
        var childDto = Dto("2", parentId: "1");
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Parent",
            Value = Node("1"),
            Children = [childDto],
        };

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.NotNull(item.Children);
        var child = Assert.Single(item.Children);
        Assert.Equal("2", child.Value?.Id);
    }

    [Fact]
    public void ToTreeItemData_SetsParentOnValue()
    {
        var parent = Node("p1");
        var dto = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "Child",
            Value = Node("1", "p1"),
            Parent = parent,
        };

        var item = dto.ToTreeItemData<StringTreeNode>();

        Assert.Same(parent, item.Value?.Parent);
    }

    // ============================================================
    // FindTreeItem
    // ============================================================

    [Fact]
    public void FindTreeItem_DirectChild_Found()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
            new() { Value = Node("2") },
        };

        var result = items.FindTreeItem("2");

        Assert.NotNull(result);
        Assert.Equal("2", result!.Value?.Id);
    }

    [Fact]
    public void FindTreeItem_DeepChild_Found()
    {
        var grandChild = new TreeItemData<StringTreeNode> { Value = Node("3") };
        var child = new TreeItemData<StringTreeNode>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [child],
        };

        var result = new List<TreeItemData<StringTreeNode>> { root }.FindTreeItem("3");

        Assert.NotNull(result);
        Assert.Equal("3", result!.Value?.Id);
    }

    [Fact]
    public void FindTreeItem_NotFound_ReturnsNull()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        Assert.Null(items.FindTreeItem("999"));
    }

    // ============================================================
    // GetPathToNode
    // ============================================================

    [Fact]
    public void GetPathToNode_Root_ReturnsSingleElement()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        var path = items.GetPathToNode("1");

        Assert.NotNull(path);
        Assert.Equal(["1"], path);
    }

    [Fact]
    public void GetPathToNode_DeepNode_ReturnsFullPath()
    {
        var grandChild = new TreeItemData<StringTreeNode> { Value = Node("3") };
        var child = new TreeItemData<StringTreeNode>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [child],
        };

        var path = new List<TreeItemData<StringTreeNode>> { root }.GetPathToNode("3");

        Assert.NotNull(path);
        Assert.Equal(["1", "2", "3"], path);
    }

    [Fact]
    public void GetPathToNode_NotFound_ReturnsNull()
    {
        var items = new List<TreeItemData<StringTreeNode>>
        {
            new() { Value = Node("1") },
        };

        Assert.Null(items.GetPathToNode("999"));
    }

    // ============================================================
    // ExpandAsync（单层展开）
    // ============================================================

    [Fact]
    public async Task ExpandAsync_LoadsChildren()
    {
        var item = new TreeItemData<StringTreeNode> { Value = Node("1") };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(StringTreeNode? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(
                new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("2", parentId: "1") },
                    new() { Value = Node("3", parentId: "1") },
                });
        }

        await item.ExpandAsync(Load);

        Assert.True(item.Expanded);
        Assert.Equal(1, loadCount);
        Assert.Equal(2, item.Children?.Count);
    }

    [Fact]
    public async Task ExpandAsync_AlreadyLoaded_DoesNotReload()
    {
        var existingChild = new TreeItemData<StringTreeNode> { Value = Node("2") };
        var item = new TreeItemData<StringTreeNode>
        {
            Value = Node("1"),
            Children = [existingChild],
        };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(StringTreeNode? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(
                Array.Empty<TreeItemData<StringTreeNode>>());
        }

        await item.ExpandAsync(Load);

        Assert.True(item.Expanded);
        Assert.Equal(0, loadCount);   // 未重新加载
        Assert.Single(item.Children!);
    }

    // ============================================================
    // ExpandToNodeAsync（沿路径展开）
    // ============================================================

    [Fact]
    public async Task ExpandToNodeAsync_ExpandsAlongPath()
    {
        // 预置：根节点
        var root = new TreeItemData<StringTreeNode> { Value = Node("1") };
        var items = new List<TreeItemData<StringTreeNode>> { root };

        // 模拟 API：加载 "1" 的子节点 → 返回 "2"；加载 "2" 的子节点 → 返回 "3"
        Task<IReadOnlyCollection<TreeItemData<StringTreeNode>>> Load(
            StringTreeNode? parent)
        {
            var result = parent?.Id switch
            {
                "1" => new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("2", parentId: "1") },
                },
                "2" => new List<TreeItemData<StringTreeNode>>
                {
                    new() { Value = Node("3", parentId: "2") },
                },
                _ => new List<TreeItemData<StringTreeNode>>(),
            };
            return Task.FromResult<IReadOnlyCollection<TreeItemData<StringTreeNode>>>(result);
        }

        StringTreeNode? selected = null;
        await items.ExpandToNodeAsync(
            path: ["1", "2", "3"],
            loadChildren: Load,
            onSelected: v => selected = v);

        Assert.NotNull(selected);
        Assert.Equal("3", selected!.Id);

        // 沿路径的节点应都被展开
        Assert.True(root.Expanded);
        var level2 = Assert.Single(root.Children!);
        Assert.Equal("2", level2.Value?.Id);
        Assert.True(level2.Expanded);
    }
}
