using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.Trees.StringTree.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Components;

/// <summary>
/// StringParentSelectDialog 取证测试（bUnit）。
///
/// 背景：浏览器实测发现「点击树节点后选中不更新」：
/// alert 提示条与 MudTreeView 选中类显示的状态互相矛盾
/// （alert 显示"根节点"，树上却高亮 CurrentParent）。
/// 本测试用真实 MudBlazor + 真实参数传递链（MudDialogProvider + IDialogService）
/// 复现该缺陷，定位后转为回归测试。
/// </summary>
public class StringParentSelectDialogTests : BunitTestBase {
    public StringParentSelectDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
    }

    /// <summary>3 节点扁平树：电子产品 → (手机, 电脑)。</summary>
    private static List<StringNodeMeta> Nodes() =>
    [
        new() { Id = "n-electronics", ParentId = null, Text = "电子产品", SortOrder = 0, HasChildren = true },
        new() { Id = "n-phone", ParentId = "n-electronics", Text = "手机", SortOrder = 0, HasChildren = true },
        new() { Id = "n-computer", ParentId = "n-electronics", Text = "电脑", SortOrder = 1, HasChildren = true },
    ];

    /// <summary>经真实 DialogParameters 链打开对话框（与宿主 HandleMoveAsync 一致）。</summary>
    private Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync()
        => OpenDialogAsync(allowRoot: true, currentParent: Nodes().Single(n => n.Text == "电子产品"));

    private static string AlertText(IRenderedComponent<MudDialogProvider> provider)
        => provider.Find(".mud-alert").TextContent.Trim();

    private static List<string> SelectedLabels(IRenderedComponent<MudDialogProvider> provider)
        => provider.FindAll(".mud-treeview-item-selected .mud-treeview-item-label")
            .Select(e => e.TextContent.Trim())
            .ToList();

    /// <summary>点击指定文本节点的内容行（真实 onclick 链路）。</summary>
    private static void ClickNode(
        IRenderedComponent<MudDialogProvider> provider, string text)
    {
        var label = provider.Find($".mud-treeview-item-label:contains('{text}')");
        var li = label.Closest("li.mud-treeview-item")
            ?? throw new InvalidOperationException($"未找到 {text} 的 li");
        var content = li.QuerySelector(".mud-treeview-item-content")
            ?? throw new InvalidOperationException($"未找到 {text} 的 content div");
        content.Click();
        provider.Render();
    }

    [Fact]
    public async Task Open_WithCurrentParent_AlertShowsParentText()
    {
        var provider = await OpenDialogAsync();

        // 期望：提示条显示 CurrentParent（电子产品），而不是"根节点"
        Assert.Contains("电子产品", AlertText(provider), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_WithCurrentParent_MarksParentItemSelected()
    {
        var provider = await OpenDialogAsync();

        // 期望：初始高亮在 CurrentParent 上
        Assert.Equal(["电子产品"], SelectedLabels(provider));
    }

    [Fact]
    public async Task Click_OtherNode_UpdatesAlertAndSelection()
    {
        var provider = await OpenDialogAsync();

        ClickNode(provider, "电脑");

        // 期望：提示条与高亮都切到 电脑
        Assert.Contains("电脑", AlertText(provider), StringComparison.Ordinal);
        Assert.Equal(["电脑"], SelectedLabels(provider));
    }

    // ============================================================
    // 禁用逻辑：不能选自身或其祖先 / 后代
    // ============================================================

    [Fact]
    public async Task CurrentNode_IsMarkedDisabled()
    {
        var provider = await OpenDialogAsync();

        // CurrentNode = 手机 → 手机自身应被禁用
        Assert.True(IsNodeDisabled(provider, "手机"));
    }

    [Fact]
    public async Task AncestorOfCurrentNode_IsNotDisabled()
    {
        var provider = await OpenDialogAsync();

        // 电子产品 是 手机 的父节点 → 不禁用（移到祖先下合法，只是可能"未变化"）
        Assert.False(IsNodeDisabled(provider, "电子产品"));
    }

    [Fact]
    public async Task UnrelatedNode_IsNotDisabled()
    {
        var provider = await OpenDialogAsync();

        // 电脑 与 手机 互为兄弟 → 可作为新父节点
        Assert.False(IsNodeDisabled(provider, "电脑"));
    }

    private static bool IsNodeDisabled(
        IRenderedComponent<MudDialogProvider> provider, string text)
    {
        var label = provider.Find($".mud-treeview-item-label:contains('{text}')");
        var li = label.Closest("li.mud-treeview-item");
        return li?.ClassName?.Contains("mud-treeview-item-disabled") ?? false;
    }

    // ============================================================
    // 搜索
    // ============================================================

    [Fact]
    public async Task Search_FiltersNodes_KeepsAncestorsVisible()
    {
        var provider = await OpenDialogAsync();

        // 用反射设置 _searchText 并触发 OnSearchAsync（避开 MudTextField 防抖时序）
        await SetSearchAsync(provider, "电脑");

        // 可见节点：电脑 + 电子产品（祖链）
        var labels = provider.FindAll(".mud-treeview-item-label")
            .Select(e => e.TextContent.Trim())
            .ToList();

        Assert.Contains("电脑", labels);
        Assert.Contains("电子产品", labels);
        Assert.DoesNotContain("手机", labels);
    }

    [Fact]
    public async Task Search_NoMatch_ShowsEmptyMessage()
    {
        var provider = await OpenDialogAsync();

        await SetSearchAsync(provider, "不存在的关键词");

        // 无匹配时 MudTreeView 不渲染，空消息直接在对话框容器内
        Assert.Contains("未找到匹配的节点",
            provider.Find(".mud-dialog").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_Clear_RestoresAllNodes()
    {
        var provider = await OpenDialogAsync();

        await SetSearchAsync(provider, "电脑");
        Assert.DoesNotContain("手机", provider.Find(".mud-treeview").TextContent);

        await SetSearchAsync(provider, string.Empty);

        var labels = provider.FindAll(".mud-treeview-item-label")
            .Select(e => e.TextContent.Trim())
            .ToList();
        Assert.Contains("手机", labels);
        Assert.Contains("电脑", labels);
    }

    /// <summary>
    /// 通过真实 UI 触发搜索：MudTextField 的 @bind-Value 更新 _searchText，
    /// DebounceInterval(300ms) 到期后调用 OnSearchAsync。
    /// 不反射私有成员，重命名/重构不会静默失效。
    /// </summary>
    private static async Task SetSearchAsync(
        IRenderedComponent<MudDialogProvider> provider, string keyword)
    {
        var input = provider.Find(".mud-dialog input");
        await input.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
        {
            Value = keyword
        });

        // MudTextField DebounceInterval=300ms，等待防抖触发 OnSearchAsync
        await Task.Delay(500);
        provider.Render();
    }

    // ============================================================
    // 确认 / 取消
    // ============================================================

    [Fact]
    public async Task Cancel_ClosesDialog()
    {
        var provider = await OpenDialogAsync();

        ClickButton(provider, "取消");

        Assert.Empty(provider.FindAll(".mud-dialog"));
    }

    [Fact]
    public async Task Confirm_AfterSelect_ClosesDialog()
    {
        var provider = await OpenDialogAsync();

        ClickNode(provider, "电脑");
        ClickButton(provider, "确认选择");

        Assert.Empty(provider.FindAll(".mud-dialog"));
    }

    [Fact]
    public async Task AllowRootSelectionFalse_WithoutSelection_ConfirmDisabled()
    {
        var provider = await OpenDialogAsync(allowRoot: false, currentParent: null);

        var confirm = provider.FindAll("button")
            .First(b => b.TextContent.Trim() == "确认选择");

        // MudButton Disabled=true 时渲染 disabled 属性
        Assert.NotNull(confirm.GetAttribute("disabled"));
    }

    private static void ClickButton(
        IRenderedComponent<MudDialogProvider> provider, string text)
    {
        var btn = provider.FindAll("button")
            .First(b => b.TextContent.Trim() == text);
        btn.Click();
        provider.Render();
    }

    /// <summary>允许自定义参数的重载。currentParent 为 null 时传 null（不移到默认值）。</summary>
    private async Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync(
        bool allowRoot = true,
        StringNodeMeta? currentParent = null)
    {
        var provider = Render<MudDialogProvider>();

        var nodes = Nodes();
        var parameters = new DialogParameters
        {
            { "AllNodes", nodes },
            { "CurrentNode", nodes.Single(n => n.Text == "手机") },
            { "CurrentParent", currentParent },
            { "AllowRootSelection", allowRoot },
        };

        var dialogService = Services.GetRequiredService<IDialogService>();
        await dialogService.ShowAsync<StringParentSelectDialog>(
            "选择父节点", parameters, new DialogOptions());
        provider.Render();

        return provider;
    }
}
