using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Models;

/// <summary>
/// StringNodeMeta 单元测试：默认值 + Clone 深拷贝语义 + StringNodeAction 枚举完整性。
/// </summary>
public class StringNodeMetaTests
{
    [Fact]
    public void DefaultValues_AreSane()
    {
        var meta = new StringNodeMeta();

        Assert.Equal(string.Empty, meta.Id);
        Assert.Equal(string.Empty, meta.Text);
        Assert.Null(meta.ParentId);
        Assert.Null(meta.Description);
        Assert.Null(meta.Subtitle);
        Assert.Null(meta.Icon);
        Assert.Null(meta.ExtraData);
        Assert.False(meta.HasChildren);
        Assert.True(meta.CanHaveChildren);   // ★ 默认 true
        Assert.Equal(0, meta.SortOrder);
    }

    [Fact]
    public void Clone_CopiesAllScalars()
    {
        var original = new StringNodeMeta
        {
            Id = "n1",
            ParentId = "p1",
            Text = "文本",
            Description = "描述",
            Subtitle = "副标题",
            Icon = "Icons.Material.Filled.Folder",
            HasChildren = true,
            CanHaveChildren = false,
            SortOrder = 42,
        };

        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.ParentId, clone.ParentId);
        Assert.Equal(original.Text, clone.Text);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal(original.Subtitle, clone.Subtitle);
        Assert.Equal(original.Icon, clone.Icon);
        Assert.Equal(original.HasChildren, clone.HasChildren);
        Assert.Equal(original.CanHaveChildren, clone.CanHaveChildren);
        Assert.Equal(original.SortOrder, clone.SortOrder);
    }

    [Fact]
    public void Clone_CopiesExtraDataAsNewDictionary()
    {
        var original = new StringNodeMeta
        {
            ExtraData = new Dictionary<string, object?> { ["k"] = "v" }
        };

        var clone = original.Clone();

        Assert.NotNull(clone.ExtraData);
        Assert.Equal("v", clone.ExtraData!["k"]);
        Assert.NotSame(original.ExtraData, clone.ExtraData);   // ★ 新字典
    }

    [Fact]
    public void Clone_NullExtraData_StaysNull()
    {
        var clone = new StringNodeMeta { ExtraData = null }.Clone();
        Assert.Null(clone.ExtraData);
    }

    [Fact]
    public void Clone_IndependentAfterMutation()
    {
        var original = new StringNodeMeta { Text = "原" };
        var clone = original.Clone();

        clone.Text = "改";

        Assert.Equal("原", original.Text);
        Assert.Equal("改", clone.Text);
    }

    [Fact]
    public void StringNodeAction_HasSixMembers()
    {
        var values = Enum.GetValues<StringNodeAction>();
        Assert.Contains(StringNodeAction.View, values);
        Assert.Contains(StringNodeAction.AddChild, values);
        Assert.Contains(StringNodeAction.Edit, values);
        Assert.Contains(StringNodeAction.Delete, values);
        Assert.Contains(StringNodeAction.Move, values);
        Assert.Contains(StringNodeAction.Sort, values);
        Assert.Equal(6, values.Length);
    }
}
