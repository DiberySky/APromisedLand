using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
}
