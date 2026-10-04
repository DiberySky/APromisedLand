using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /string-tree-demo 页面的 smoke E2E —— StringTreeSky（T=string）的回归保护。
///
/// 覆盖：
///   1. 初始渲染 + 懒加载展开（根 → 一级 → 二级）
///   2. 移动流程完整对话框链路（节点操作 → 父选择 → BoolBox → toast + 树结构）
///
/// ★ 选中逻辑是本组件历史上最易出问题的点（见 StringParentSelectDialog 排查记录）：
///   场景 2 专门断言"点击节点后 alert 更新"，防止选中逻辑回归。
///
/// ★ 选择器全部限定 .mud-dialog，避免背景主树状态污染（历史排查教训）。
/// </summary>
public class StringTreeDemoSmokeTests : E2ETestBase
{
    private const string RootElectronics = "电子产品";
    private const string RootClothing = "服装";
    private const string Phone = "手机";
    private const string Computer = "电脑";
    private const string Menswear = "男装";

    public StringTreeDemoSmokeTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 种子 + 懒加载展开
    // ============================================================

    [Fact]
    public async Task Page_Loads_SeededTree_AndLazyExpandsChildren()
    {
        await BlazorHelpers.GoToAsync(Page, "/string-tree-demo");
        await Page.WaitForSelectorAsync("h5:has-text('StringTreeSky 演示')");

        // 两个根
        await Row(RootElectronics).First.WaitForAsync(Visible());
        await Row(RootClothing).First.WaitForAsync(Visible());

        // 初始只有根，子节点懒加载
        Assert.Equal(0, await Row(Phone).CountAsync());

        // 展开「电子产品」→ 手机、电脑
        await ExpandAsync(RootElectronics, Phone);
        await Row(Phone).First.WaitForAsync(Visible());
        await Row(Computer).First.WaitForAsync(Visible());

        // 展开「手机」→ 安卓手机、iPhone
        await ExpandAsync(Phone, "iPhone");
        await Row("安卓手机").First.WaitForAsync(Visible());
        await Row("iPhone").First.WaitForAsync(Visible());
    }

    // ============================================================
    // 场景 2：移动流程完整闭环
    //
    // 链路：节点操作对话框 → 移动 → 父选择对话框 → 选目标 → 确认选择
    //       → BoolBox 确认 → toast「移动成功」→ 树结构变更
    //
    // ★ 核心断言：点击目标节点后，父选择对话框 alert 从「服装」更新为「电子产品」
    //   （这是历史排查中反复出问题的选中逻辑，必须防回归）
    //
    // ★ 自我清理：finally 把男装移回服装，保证可重复跑（不依赖 fresh 环境）。
    // ============================================================

    [Fact]
    public async Task Move_Node_ViaFullDialogChain_Works()
    {
        await BlazorHelpers.GoToAsync(Page, "/string-tree-demo");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);
        await Row(RootClothing).First.WaitForAsync(Visible());

        // 展开「服装」，看到唯一的子节点「男装」
        await ExpandAsync(RootClothing, Menswear);

        try
        {
            // 男装 服装 → 电子产品
            await MoveToParentAsync(Menswear, RootElectronics,
                assertAlertUpdateFrom: RootClothing);

            // ---- 验证树结构：展开「电子产品」，应看到「男装」 ----
            await Row(RootElectronics).First.WaitForAsync(Visible());
            await ExpandAsync(RootElectronics, Menswear);
            await Row(Menswear).First.WaitForAsync(Visible());
        }
        finally
        {
            // 清理：男装 电子产品 → 服装（恢复种子状态，保证可重跑）
            try { await MoveToParentAsync(Menswear, RootClothing); }
            catch { /* 清理失败不掩盖主体断言 */ }
        }
    }

    /// <summary>
    /// 把 nodeText 移动到 targetParentText 下，走完整对话框链路。
    /// assertAlertUpdateFrom 非 null 时，断言点击目标后 alert 从该值更新为目标。
    /// </summary>
    private async Task MoveToParentAsync(
        string nodeText, string targetParentText, string? assertAlertUpdateFrom = null)
    {
        await OpenActionsAsync(nodeText);
        var actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.ClickAsync(Page,
            actionsDlg.Locator("button:has-text('移动')").First);

        // ---- 父选择对话框 ----
        var parentDlg = Page.Locator(".mud-dialog")
            .Filter(new LocatorFilterOptions { HasText = "当前选中" }).First;
        await parentDlg.WaitForAsync(Visible());

        if (assertAlertUpdateFrom is not null)
        {
            var initialAlert = await parentDlg.Locator(".mud-alert").InnerTextAsync();
            Assert.Contains(assertAlertUpdateFrom, initialAlert);
        }

        // 点击目标节点
        var targetItem = parentDlg.Locator(".mud-treeview-item-content",
            new LocatorLocatorOptions { HasText = targetParentText }).First;
        await BlazorHelpers.ClickAsync(Page, targetItem);

        if (assertAlertUpdateFrom is not null)
        {
            // ★ 核心断言：alert 更新为目标节点
            await Page.WaitForFunctionAsync(
                $"() => document.querySelector('.mud-dialog .mud-alert')" +
                $"?.textContent?.includes('{targetParentText}')",
                null,
                new PageWaitForFunctionOptions { Timeout = 5000 });
        }

        // ---- 确认选择 ----
        await BlazorHelpers.ClickAsync(Page,
            parentDlg.Locator("button:has-text('确认选择')").First);

        // ---- BoolBox 确认框 ----
        var confirmBox = Page.Locator(".mud-dialog")
            .Filter(new LocatorFilterOptions { HasText = "确定将" }).First;
        await confirmBox.WaitForAsync(Visible());
        await BlazorHelpers.ClickAsync(Page,
            confirmBox.Locator("button:has-text('确认')").First);

        // ---- 断言 toast ----
        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "移动成功");
        Assert.Contains("移动成功", snackbar);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);
    }

    // ============================================================
    // 辅助
    // ============================================================

    /// <summary>
    /// 锚定某个节点"本行"的内容容器。
    /// MudBlazor 把子节点渲染在独立 li 中，
    /// 因此 .mud-treeview-item-content 内只含本行文本，不会被子孙污染。
    /// ★ 加 :visible 过滤，避免已关闭对话框残留 DOM 的干扰。
    /// </summary>
    private ILocator Row(string text) =>
        Page.Locator(".mud-treeview-item-content:visible",
            new PageLocatorOptions { HasText = text });

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 15000 };

    /// <summary>展开某节点并确认子节点出现（幂等：子节点已在 DOM 则跳过点击）。</summary>
    private async Task ExpandAsync(string parentText, string childText)
    {
        if (await Row(childText).CountAsync() > 0) return;

        var arrow = Row(parentText).First
            .Locator("xpath=ancestor::li[1]//button[contains(@class,'mud-treeview-item-expand-button')]")
            .First;
        await BlazorHelpers.ClickAsync(Page, arrow);
        await Row(childText).First.WaitForAsync(Visible());
    }

    /// <summary>点击节点行右侧的 MoreHoriz（三个点）图标按钮。</summary>
    private async Task OpenActionsAsync(string nodeText)
    {
        await BlazorHelpers.ClickAsync(Page,
            Row(nodeText).First.Locator("button.mud-icon-button-size-small").First);
    }
}
