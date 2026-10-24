using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky;
using TreeGraph.Shared.TreeEavSky.Entities;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests;

/// <summary>
/// 迭代遍历的深树安全性与防环测试（原递归实现在此规模会 StackOverflowException）。
/// </summary>
public class TreeHelperIterationTests
{
    private const int Depth = 10_000;

    /// <summary>构造 1→2→…→Depth 的单链，子集合在构造期就全部挂好。</summary>
    private static List<TreeNodeItem<UnitTree>> BuildLine(int depth)
    {
        TreeNodeItem<UnitTree>? leaf = null;

        for (int i = depth; i >= 1; i--)
        {
            var node = new TreeNodeItem<UnitTree>
            {
                Value = new UnitTree { Id = i.ToString(), Name = $"N{i}" },
                Children = leaf is null ? null : [leaf],
            };
            leaf = node;
        }

        return [leaf!];
    }

    [Fact]
    public void FindTreeItem_DeepLine_DoesNotStackOverflow()
    {
        var roots = BuildLine(Depth);

        var found = roots.FindTreeItem(Depth.ToString());

        Assert.Equal(Depth.ToString(), found?.Value?.Id);
    }

    [Fact]
    public void GetPathToNode_DeepLine_ReturnsFullPath()
    {
        var roots = BuildLine(Depth);

        var path = roots.GetPathToNode(Depth.ToString());

        Assert.NotNull(path);
        Assert.Equal(Depth, path!.Count);
        Assert.Equal("1", path.First());
        Assert.Equal(Depth.ToString(), path.Last());
    }

    [Fact]
    public void GetPathToNode_CyclicGraph_DoesNotInfiniteLoop()
    {
        // 手工构造环：a ↔ b
        var a = new TreeNodeItem<UnitTree>
        {
            Value = new UnitTree { Id = "a", Name = "A" },
        };
        var b = new TreeNodeItem<UnitTree>
        {
            Value = new UnitTree { Id = "b", Name = "B" },
            Children = [a],
        };
        a.Children = [b];

        var path = new List<TreeNodeItem<UnitTree>> { a }.GetPathToNode("b");

        Assert.Equal(["a", "b"], path);
    }
}
