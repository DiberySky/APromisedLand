using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Shared.TreeEavSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// SearchService.BuildPath 纯函数测试：祖先链 → (Path, ParentName, RootId)。
/// Path 含本节点（"根 / … / 父 / 本节点"）；RootId 取最顶端已知祖先。
/// </summary>
public class SearchServicePathTests
{
    private static SpaceTree Node(string id, string name, string? parentId)
        => new() { Id = id, Name = name, ParentId = parentId };

    [Fact]
    public void 三级祖先链_路径含本节点_父名与根Id正确()
    {
        var nodes = new Dictionary<string, SpaceTree>
        {
            ["r"] = Node("r", "空间树", null),
            ["d"] = Node("d", "生产部", "r"),
            ["w"] = Node("w", "注塑车间", "d"),
            ["m"] = Node("m", "注塑机 A-101", "w")
        };

        var (path, parentName, rootId) = SearchService.BuildPath(nodes["m"], nodes);

        Assert.Equal("空间树 / 生产部 / 注塑车间 / 注塑机 A-101", path);
        Assert.Equal("注塑车间", parentName);
        Assert.Equal("r", rootId);
    }

    [Fact]
    public void 根节点_路径即自身_父名Null_根Id为自身()
    {
        var nodes = new Dictionary<string, SpaceTree>
        {
            ["r"] = Node("r", "空间树", null)
        };

        var (path, parentName, rootId) = SearchService.BuildPath(nodes["r"], nodes);

        Assert.Equal("空间树", path);
        Assert.Null(parentName);
        Assert.Equal("r", rootId);
    }

    [Fact]
    public void 父链断裂_退化为最顶端已知祖先()
    {
        // 祖父 "g" 不在字典中（如加载遗漏）：链在 dept 处停止
        var nodes = new Dictionary<string, SpaceTree>
        {
            ["d"] = Node("d", "生产部", "g"),
            ["m"] = Node("m", "注塑机 A-101", "d")
        };

        var (path, parentName, rootId) = SearchService.BuildPath(nodes["m"], nodes);

        Assert.Equal("生产部 / 注塑机 A-101", path);
        Assert.Equal("生产部", parentName);
        Assert.Equal("d", rootId);
    }

    [Fact]
    public void 二级节点_父名即根_根Id正确()
    {
        var nodes = new Dictionary<string, SpaceTree>
        {
            ["r"] = Node("r", "空间树", null),
            ["d"] = Node("d", "生产部", "r")
        };

        var (path, parentName, rootId) = SearchService.BuildPath(nodes["d"], nodes);

        Assert.Equal("空间树 / 生产部", path);
        Assert.Equal("空间树", parentName);
        Assert.Equal("r", rootId);
    }
}
