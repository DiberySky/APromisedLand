using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// TreeHelper 扩展方法单元测试。
/// 覆盖：ToTreeNodeItem / FindTreeItem / GetPathToNode / ExpandAsync / ExpandToNodeAsync。
/// </summary>
public class TreeHelperTests
{
    // ============================================================
    // 测试数据辅助
    // ============================================================

    private static UnitTree Node(
        string id, string? parentId = null, string? name = null)
        => new() { Id = id, Name = name ?? $"Node-{id}", ParentId = parentId };

    private static TreeNodeDto<UnitTree> Dto(
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
    // ToTreeNodeItem
    // ============================================================

    [Fact]
    public void ToTreeNodeItem_MapsAllScalarFields()
    {
        var dto = Dto("1", text: "Hello", hasChildren: true, sortOrder: 5);

        var item = dto.ToTreeNodeItem<UnitTree>();

        Assert.Equal("1", item.Value?.Id);
        Assert.Equal("Hello", item.Text);
        Assert.True(item.Expandable);
        Assert.Equal(TreeHelper.TreeItemIcons, item.Icon);
    }

    [Fact]
    public void ToTreeNodeItem_WithNullValue_DoesNotThrow()
    {
        var dto = new TreeNodeDto<UnitTree>
        {
            Id = "1",
            Text = "Orphan",
            Value = null,   // ★ 关键：Value 为 null
        };

        // 修复前会 NRE；现在应该正常返回
        var item = dto.ToTreeNodeItem<UnitTree>();

        Assert.Null(item.Value);
        Assert.Equal("Orphan", item.Text);
    }

    [Fact]
    public void ToTreeNodeItem_WithChildren_Recurses()
    {
        var childDto = Dto("2", parentId: "1");
        var dto = new TreeNodeDto<UnitTree>
        {
            Id = "1",
            Text = "Parent",
            Value = Node("1"),
            Children = [childDto],
        };

        var item = dto.ToTreeNodeItem<UnitTree>();

        Assert.NotNull(item.Children);
        var child = Assert.Single(item.Children);
        Assert.Equal("2", child.Value?.Id);
    }

    [Fact]
    public void ToTreeNodeItem_SetsParentOnValue()
    {
        var parent = Node("p1");
        var dto = new TreeNodeDto<UnitTree>
        {
            Id = "1",
            Text = "Child",
            Value = Node("1", "p1"),
            Parent = parent,
        };

        var item = dto.ToTreeNodeItem<UnitTree>();

        Assert.Same(parent, item.Value?.Parent);
    }

    // ============================================================
    // FindTreeItem
    // ============================================================

    [Fact]
    public void FindTreeItem_DirectChild_Found()
    {
        var items = new List<TreeNodeItem<UnitTree>>
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
        var grandChild = new TreeNodeItem<UnitTree> { Value = Node("3") };
        var child = new TreeNodeItem<UnitTree>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeNodeItem<UnitTree>
        {
            Value = Node("1"),
            Children = [child],
        };

        var result = new List<TreeNodeItem<UnitTree>> { root }.FindTreeItem("3");

        Assert.NotNull(result);
        Assert.Equal("3", result!.Value?.Id);
    }

    [Fact]
    public void FindTreeItem_NotFound_ReturnsNull()
    {
        var items = new List<TreeNodeItem<UnitTree>>
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
        var items = new List<TreeNodeItem<UnitTree>>
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
        var grandChild = new TreeNodeItem<UnitTree> { Value = Node("3") };
        var child = new TreeNodeItem<UnitTree>
        {
            Value = Node("2"),
            Children = [grandChild],
        };
        var root = new TreeNodeItem<UnitTree>
        {
            Value = Node("1"),
            Children = [child],
        };

        var path = new List<TreeNodeItem<UnitTree>> { root }.GetPathToNode("3");

        Assert.NotNull(path);
        Assert.Equal(["1", "2", "3"], path);
    }

    [Fact]
    public void GetPathToNode_NotFound_ReturnsNull()
    {
        var items = new List<TreeNodeItem<UnitTree>>
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
        var item = new TreeNodeItem<UnitTree> { Value = Node("1") };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeNodeItem<UnitTree>>> Load(UnitTree? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeNodeItem<UnitTree>>>(
                new List<TreeNodeItem<UnitTree>>
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
        var existingChild = new TreeNodeItem<UnitTree> { Value = Node("2") };
        var item = new TreeNodeItem<UnitTree>
        {
            Value = Node("1"),
            Children = [existingChild],
        };

        var loadCount = 0;
        Task<IReadOnlyCollection<TreeNodeItem<UnitTree>>> Load(UnitTree? _)
        {
            loadCount++;
            return Task.FromResult<IReadOnlyCollection<TreeNodeItem<UnitTree>>>(
                Array.Empty<TreeNodeItem<UnitTree>>());
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
        var root = new TreeNodeItem<UnitTree> { Value = Node("1") };
        var items = new List<TreeNodeItem<UnitTree>> { root };

        // 模拟 API：加载 "1" 的子节点 → 返回 "2"；加载 "2" 的子节点 → 返回 "3"
        Task<IReadOnlyCollection<TreeNodeItem<UnitTree>>> Load(
            UnitTree? parent)
        {
            var result = parent?.Id switch
            {
                "1" => new List<TreeNodeItem<UnitTree>>
                {
                    new() { Value = Node("2", parentId: "1") },
                },
                "2" => new List<TreeNodeItem<UnitTree>>
                {
                    new() { Value = Node("3", parentId: "2") },
                },
                _ => new List<TreeNodeItem<UnitTree>>(),
            };
            return Task.FromResult<IReadOnlyCollection<TreeNodeItem<UnitTree>>>(result);
        }

        UnitTree? selected = null;
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
