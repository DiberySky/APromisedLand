using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Trees.Extensions;
using TreeGraph.Blazor.Shared.Trees;
using TreeGraph.Blazor.Shared.Trees.Models;
using TreeGraph.Blazor.Shared.Trees.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Components;

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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { RootDto() });

        RenderComponent<TreeSky<StringTreeNode>>();

        _clientService.Verify(
            s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
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
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreeNodeDto<StringTreeNode>> { root });

        _clientService
            .Setup(s => s.GetAncestorPathFromApiAsync("3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(["1", "2", "3"]);

        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("3")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>(
            ("ClickNodeId", "3"));

        cut.WaitForAssertion(
            () => Assert.Equal("3", cut.Instance.SelectedValue?.Id),
            TimeSpan.FromSeconds(2));

        // 关键：初始恢复与 OnParametersSetAsync 不会重复展开（各层只加载一次）
        _clientService.Verify(
            s => s.GetAncestorPathFromApiAsync("3", It.IsAny<CancellationToken>()), Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "2"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================
    // GetAllLoadedNodes 只读不触发懒加载；EnsureAllNodesLoadedAsync 显式加载
    // ============================================================

    [Fact]
    public async Task GetAllLoadedNodes_DoesNotTriggerLazyLoad()
    {
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("1", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();
        cut.WaitForState(() => cut.Instance.GetAllLoadedNodes().Count == 1,
            TimeSpan.FromSeconds(2));

        Assert.Single(cut.Instance.GetAllLoadedNodes());
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.IsAny<StringTreeNode>(), It.IsAny<CancellationToken>()), Times.Never);

        var all = await cut.Instance.EnsureAllNodesLoadedAsync();

        Assert.Equal(2, all.Count);
        _clientService.Verify(
            s => s.LoadChildrenAsync(It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
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

    // ============================================================
    // 懒加载回写：用户点击展开（MudBlazor 内部 ServerData 路径）
    // 验证 ServerData 结果经 @bind-Items 回写进组件 _items 同一对象树
    // ============================================================

    [Fact]
    public void UserClickExpand_ServerDataResult_WritesBackToComponentItems()
    {
        // 首屏：仅根节点 1，子节点 2 未加载
        _clientService
            .Setup(s => s.LoadInitialDataAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("1", hasChildren: true)]);
        _clientService
            .Setup(s => s.LoadChildrenAsync(
                It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NodeDto("2")]);

        var cut = RenderComponent<TreeSky<StringTreeNode>>();

        // 初始：组件侧 _items 只有 1，2 尚未加载
        Assert.Single(cut.Instance.GetAllLoadedNodes());

        // 用户点击 1 的展开箭头 → MudTreeViewItem 内部 OnItemExpanded
        // → TryInvokeServerLoadFunc → ServerData(1) → _itemsState.SetValueAsync
        var toggle = cut.Find(".mud-treeview-item-arrow button");
        toggle.Click();

        // 关键断言：MudBlazor 加载结果必须回写到组件 _items（context.Children），
        // 组件侧遍历必须能看到用户展开出来的 2
        cut.WaitForAssertion(() =>
        {
            var loaded = cut.Instance.GetAllLoadedNodes();
            Assert.Equal(2, loaded.Count);
            Assert.Contains(loaded, n => n.Id == "2");
            // UI 同步：两行树节点（MudCollapse 折叠后 DOM 保留，未加载则不存在）
            Assert.Equal(2, cut.FindAll("li.mud-treeview-item").Count);
        }, TimeSpan.FromSeconds(5));

        // 守卫实证：折叠后再次展开，Children 已存在 → 不得重复请求
        toggle.Click(); // 折叠
        toggle.Click(); // 重新展开
        _clientService.Verify(
            s => s.LoadChildrenAsync(
                It.Is<StringTreeNode>(n => n.Id == "1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
