using System.Text.Json;
using TreeGraph.TreeSky.Models;
using Xunit;

namespace TreeGraph.TreeSky.Tests.Models;

/// <summary>
/// StringTreeNode POCO 单元测试。
/// 覆盖：ITreeNodeBase 接口实现的基本契约 + JSON 循环引用防护。
/// </summary>
public class StringTreeNodeTests
{
    // ============================================================
    // Text()
    // ============================================================

    [Fact]
    public void Text_ReturnsName()
    {
        var node = new StringTreeNode { Name = "分类A" };

        Assert.Equal("分类A", node.Text());
    }

    [Fact]
    public void Text_DefaultName_ReturnsEmpty()
    {
        var node = new StringTreeNode();

        Assert.Equal(string.Empty, node.Text());
    }

    // ============================================================
    // 默认值
    // ============================================================

    [Fact]
    public void DefaultValues_AreSane()
    {
        var node = new StringTreeNode();

        Assert.Equal(string.Empty, node.Id);
        Assert.Equal(string.Empty, node.Name);
        Assert.Null(node.Description);
        Assert.Null(node.ParentId);
        Assert.Null(node.Parent);
        Assert.True(node.CanHaveChildren);   // ★ 默认 true
        Assert.False(node.HasChildren);
        Assert.Equal(0, node.SortOrder);
        Assert.NotNull(node.Children);
        Assert.Empty(node.Children);
    }

    // ============================================================
    // 属性设置
    // ============================================================

    [Fact]
    public void Properties_RoundTrip()
    {
        var node = new StringTreeNode
        {
            Id = "abc-123",
            Name = "测试节点",
            Description = "描述",
            ParentId = "parent-1",
            SortOrder = 5,
            CanHaveChildren = false,
            HasChildren = true,
        };

        Assert.Equal("abc-123", node.Id);
        Assert.Equal("测试节点", node.Name);
        Assert.Equal("描述", node.Description);
        Assert.Equal("parent-1", node.ParentId);
        Assert.Equal(5, node.SortOrder);
        Assert.False(node.CanHaveChildren);
        Assert.True(node.HasChildren);
    }

    // ============================================================
    // [JsonIgnore] 循环引用防护（Parent / Children 不进 JSON）
    // ============================================================

    [Fact]
    public void Json_IgnoresParentAndChildrenNavProperties()
    {
        var node = new StringTreeNode
        {
            Id = "child",
            Name = "子节点",
            Parent = new StringTreeNode { Id = "parent", Name = "父节点" },
            Children = [new StringTreeNode { Id = "grand", Name = "孙节点" }],
        };

        var json = JsonSerializer.Serialize(node);

        // 默认 PascalCase 输出；精确判断导航属性不存在（避免 parentId/canHaveChildren 子串误伤）
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("Parent", out _));
        Assert.False(root.TryGetProperty("Children", out _));
        Assert.Equal("child", root.GetProperty("Id").GetString());
    }
}
