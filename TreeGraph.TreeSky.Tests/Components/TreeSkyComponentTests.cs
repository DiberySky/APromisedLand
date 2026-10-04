using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.TreeSky;
using TreeGraph.TreeSky.Components.Base;
using TreeGraph.TreeSky.Models;
using TreeGraph.TreeSky.Services;
using Xunit;

namespace TreeGraph.TreeSky.Tests.Components;

/// <summary>
/// TreeSky 组件渲染测试（bUnit）。
/// MudBlazor 的弹层/对话框 JS 交互无法在此完整模拟，只覆盖：
/// 加载中占位、数据到达后渲染树、空数据、加载异常不崩溃、初始加载调用一次。
/// </summary>
public class TreeSkyComponentTests : TestContext
{
    private readonly Mock<ITreeClientService<StringTreeNode>> _clientService = new();

    public TreeSkyComponentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddMudServices();

        // 注册 TreeSky 全部服务（BlazorService/MessageService/TreeNodeDialogService/
        // 导航历史/DiberyTreeApiClient + AddMudExtensions + HttpClient 工厂）
        Services.AddTreeSky();

        _clientService.SetupProperty(s => s.Title, "测试树");
        _clientService.SetupProperty(s => s.NewPageShow, false);
        _clientService.SetupProperty(s => s.SelectLeaf, false);
        Services.AddSingleton(_clientService.Object);
    }

    private static TreeNodeDto<StringTreeNode> RootDto() => new()
    {
        Id = "1",
        Text = "根节点",
        Value = new StringTreeNode { Id = "1", Name = "根节点" },
        HasChildren = false,
    };

    // ============================================================
    // 初始渲染：加载中
    // ============================================================

    [Fact]
    public void Render_WhileLoading_ShowsProgressCircular()
    {
        // 永不完成 → 组件停在首次 await，保持 loading
        var tcs = new TaskCompletionSource<IReadOnlyList<TreeNodeDto<StringTreeNode>>>();
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .Returns(tcs.Task);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        Assert.Contains("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-treeview", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 加载完成：渲染树 + 节点文本
    // ============================================================

    [Fact]
    public void Render_AfterLoad_ShowsTreeViewWithNodeText()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { RootDto() });

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-treeview", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.Contains("根节点", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("mud-progress-circular", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 空数据：与加载中同占位
    // ============================================================

    [Fact]
    public void Render_EmptyData_ShowsProgressCircular()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>>());

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        cut.WaitForState(
            () => cut.Markup.Contains("mud-progress-circular", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.DoesNotContain("mud-treeview", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 加载异常：组件内部吞掉异常（Message.Details），不崩溃
    // ============================================================

    [Fact]
    public void Render_LoadThrows_DoesNotCrash()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("API 挂了"));

        var exception = Record.Exception(() => RenderComponent<TreeSky<StringTreeNode>>());

        Assert.Null(exception);
    }

    // ============================================================
    // 初始加载只调用一次
    // ============================================================

    [Fact]
    public void Render_CallsLoadInitialDataOnce()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { RootDto() });

        RenderComponent<TreeSky<StringTreeNode>>();

        _clientService.Verify(
            s => s.LoadInitialDataAsync(It.IsAny<string?>()),
            Times.Once);
    }

    // ============================================================
    // 深路径：ClickNodeId 不在首屏节点中 → 沿祖先路径懒加载并选中
    // ============================================================

    [Fact]
    public void DeepLink_ClickNodeIdNotLoaded_ExpandsAncestorPathAndSelects()
    {
        var root = new TreeNodeDto<StringTreeNode>
        {
            Id = "1",
            Text = "根",
            Value = new StringTreeNode { Id = "1", Name = "根" },
            HasChildren = true,
        };
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { root });

        _clientService
            .Setup(s => s.GetAncestorPathFromApiAsync("3"))
            .ReturnsAsync(["1", "2", "3"]);

        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1")))
            .ReturnsAsync([NodeDto("2", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2")))
            .ReturnsAsync([NodeDto("3")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>(
            ("ClickNodeId", "3"));

        cut.WaitForAssertion(
            () => Assert.Equal("3", cut.Instance.SelectedValue?.Id),
            TimeSpan.FromSeconds(2));

        // 关键：初始恢复与 OnParametersSetAsync 不会重复展开（各层只加载一次）
        _clientService.Verify(
            s => s.GetAncestorPathFromApiAsync("3"), Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1")),
            Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2")),
            Times.Once);
    }

    // ============================================================
    // GetAllLoadedNodes 只读不触发懒加载；EnsureAllNodesLoadedAsync 显式加载
    // ============================================================

    [Fact]
    public async Task GetAllLoadedNodes_DoesNotTriggerLazyLoad()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>()))
            .ReturnsAsync([NodeDto("1", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1")))
            .ReturnsAsync([NodeDto("2")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        Assert.Single(cut.Instance.GetAllLoadedNodes());
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.IsAny<StringTreeNode>()), Times.Never);

        var all = await cut.Instance.EnsureAllNodesLoadedAsync();

        Assert.Equal(2, all.Count);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1")),
            Times.Once);
    }

    // ============================================================
    // RemoveNodeFromParent：深层移除必须回写父节点 Children 与 HasChildren
    // ============================================================

    [Fact]
    public void RemoveNodeFromParent_DeepRemoval_WritesBackToOwner()
    {
        TreeItemData<StringTreeNode> Leaf(string id) => new()
        {
            Value = new StringTreeNode { Id = id, Name = id },
        };

        var root = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode { Id = "1", Name = "根", HasChildren = true },
            Children = new List<ITreeItemData<StringTreeNode>>
            {
                new TreeItemData<StringTreeNode>
                {
                    Value = new StringTreeNode { Id = "2", Name = "父", HasChildren = true },
                    Children = new List<ITreeItemData<StringTreeNode>> { Leaf("3"), Leaf("4") },
                },
            },
        };
        var roots = new List<TreeItemData<StringTreeNode>> { root };

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        Assert.True(cut.Instance.RemoveNodeFromParent(roots, "3"));

        var parent = (TreeItemData<StringTreeNode>)root.Children!.Single();
        Assert.Single(parent.Children!);
        Assert.Equal("4", parent.Children!.Single().Value?.Id);
        Assert.True(parent.Value!.HasChildren);

        Assert.True(cut.Instance.RemoveNodeFromParent(roots, "4"));
        Assert.Empty(parent.Children!);
        Assert.False(parent.Value.HasChildren);
    }

    private static TreeNodeDto<StringTreeNode> NodeDto(string id, bool hasChildren = false) => new()
    {
        Id = id,
        Text = id,
        Value = new StringTreeNode { Id = id, Name = id, HasChildren = hasChildren },
        HasChildren = hasChildren,
    };
}
