using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /tree-sky-demo 页面的 smoke E2E —— TreeSky 通用树组件的回归保护。
///
/// 演示页就是裸 TreeSky&lt;StringTreeNode&gt;，因此这 3 个场景同时覆盖：
///   1. 初始根节点渲染 + MudTreeView ServerData 懒加载（Items 与 ServerData 共存）
///   2. 全对话框链路创建子节点 → 成功提示 → 树局部刷新出新节点
///      然后同节点走"节点操作"对话框 + 确认框删除（创建/删除两条写链路）
///   3. API 前置 + UI 编辑（对话框链路 + ITreeActionHandler.UpdateNodeAsync）
///
/// DOM 事实（2026-10-04 对 MudBlazor 9.11 实测）：
///   - 行内容容器 .mud-treeview-item-content 只含本行内容，子节点在祖先 li 下的独立 li 中
///   - 行内 MoreHoriz 按钮 class 含 mud-icon-button-size-small
///   - 展开按钮在同行 li 下 .mud-treeview-item-arrow 内、class 含 mud-treeview-item-expand-button
///   - 创建/编辑对话框提交按钮（DialogSky）为 Text 变体 Success 色，class 含
///     mud-button-text-success；用 class 锚定以与操作对话框的其他 Text 按钮区分
///   - 操作对话框里还有一个 label 为"名称"的只读字段；编辑表单的名称 label 是"名称（string）"
/// </summary>
public class TreeSkyDemoSmokeTests : E2ETestBase
{
    private const string RootName = "物品总类";
    private const string FurnitureName = "家具（空分类）";
    private const string ElectronicsName = "电子产品";

    public TreeSkyDemoSmokeTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 种子根节点渲染 + 懒加载展开
    // ============================================================

    [Fact]
    public async Task Page_Loads_SeededTree_AndLazyExpandsChildren()
    {
        await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");

        await Page.WaitForSelectorAsync("h4:has-text('TreeSky 树组件演示')");
        await Row(RootName).First.WaitForAsync(Visible());

        // 初始只有根，子节点懒加载
        Assert.Equal(0, await Row(ElectronicsName).CountAsync());

        await ExpandAsync(RootName, ElectronicsName);

        // 展开后三个一级分类都在
        await Row("电子产品").First.WaitForAsync(Visible());
        await Row("办公用品").First.WaitForAsync(Visible());
        await Row(FurnitureName).First.WaitForAsync(Visible());
    }

    // ============================================================
    // 场景 2：UI 创建子节点 → 出现 + 成功提示；随后 UI 删除 → 消失 + 成功提示
    // ============================================================

    [Fact]
    public async Task CreateChild_ThenDelete_FullDialogChain_Works()
    {
        var uniqueName = MetadataHelpers.Unique("e2e_树节点");

        await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);
        await Row(RootName).First.WaitForAsync(Visible());
        await ExpandAsync(RootName, FurnitureName);

        // 打开"家具（空分类）"的节点操作对话框 → 创建子项
        await OpenActionsAsync(FurnitureName);
        var actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.ClickAsync(Page,
            actionsDlg.Locator("button:has-text('创建子项')").First);

        // 创建对话框：名称（string）为必填
        var nameField = Page.Locator(
            ".mud-dialog .mud-input-control:has-text('名称（string）')").First;
        await nameField.WaitForAsync(Visible());
        await BlazorHelpers.FillMudTextFieldAsync(nameField, uniqueName);

        // 提交按钮无文字，用其实测 class 锚定（仅 SaveAs 图标）
        await BlazorHelpers.ClickAsync(Page, SubmitButton());
        await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 父节点自动展开、局部刷新后新节点出现在树中
        await Row(uniqueName).First.WaitForAsync(Visible());

        // 同节点：节点操作 → 删除 → 确认框
        await OpenActionsAsync(uniqueName);
        actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.ClickAsync(Page,
            actionsDlg.Locator("button:has-text('删除')").First);

        var confirm = Page.Locator(".mud-dialog")
            .Filter(new LocatorFilterOptions { HasText = "确认删除" }).First;
        await confirm.WaitForAsync(Visible());
        await BlazorHelpers.ClickAsync(Page,
            confirm.Locator("button:has-text('删除')").First);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "删除成功");

        // 节点从树中消失
        await AssertNodeGoneAsync(uniqueName);
    }

    // ============================================================
    // 场景 3：API 前置建叶节点 → UI 编辑改名 → 成功提示 + 树中文本更新
    // ============================================================

    [Fact]
    public async Task EditNode_ThroughDialog_UpdatesTreeText()
    {
        var oldName = MetadataHelpers.Unique("e2e_编辑前");
        var newName = MetadataHelpers.Unique("e2e_编辑后");

        using var api = NewApiClient();
        var nodeId = await CreateLeafViaApiAsync(api, oldName, "cat-furniture");

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/tree-sky-demo");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);
            await Row(RootName).First.WaitForAsync(Visible());
            await ExpandAsync(RootName, FurnitureName);
            await ExpandAsync(FurnitureName, oldName);

            await OpenActionsAsync(oldName);
            var actionsDlg = await BlazorHelpers.WaitForDialogAsync(Page);
            await BlazorHelpers.ClickAsync(Page,
                actionsDlg.Locator("button:has-text('设置修改')").First);

            var nameField = Page.Locator(
                ".mud-dialog .mud-input-control:has-text('名称（string）')").First;
            await nameField.WaitForAsync(Visible());
            await BlazorHelpers.FillMudTextFieldAsync(nameField, newName);

            await BlazorHelpers.ClickAsync(Page, SubmitButton());
            await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");

            await Row(newName).First.WaitForAsync(Visible());
            Assert.Equal(0, await Row(oldName).CountAsync());
        }
        finally
        {
            await TryDeleteViaApiAsync(api, nodeId);
        }
    }

    // ============================================================
    // 辅助
    // ============================================================

    /// <summary>
    /// 锚定某个节点"本行"的内容容器。MudBlazor 把子节点渲染在独立 li 中，
    /// 因此 .mud-treeview-item-content 内只含本行文本，不会被子孙文本污染。
    /// </summary>
    private ILocator Row(string text) =>
        Page.Locator(".mud-treeview-item-content",
            new PageLocatorOptions { HasText = text });

    private ILocator SubmitButton() =>
        Page.Locator(".mud-dialog button.mud-button-text-success").First;

    private static LocatorWaitForOptions Visible() =>
        new() { State = WaitForSelectorState.Visible, Timeout = 15000 };

    /// <summary>
    /// 确保某节点已展开并可见指定子节点（幂等：子节点已在 DOM 则跳过点击）。
    /// </summary>
    private async Task ExpandAsync(string parentText, string childText)
    {
        if (await Row(childText).CountAsync() > 0) return;

        // 调用时子节点尚未加载，li 内只有本行的展开按钮，不会误命中嵌套行
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

    private async Task AssertNodeGoneAsync(string text)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await Row(text).CountAsync() == 0) return;
            await Task.Delay(150);
        }
        Assert.Equal(0, await Row(text).CountAsync());
    }

    private HttpClient NewApiClient() =>
        new() { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };

    /// <summary>
    /// 通过真实 API 在指定父节点下建叶节点（UI 编辑场景的前置准备）。
    /// 返回服务端生成的节点 ID。
    /// </summary>
    private async Task<string> CreateLeafViaApiAsync(
        HttpClient api, string name, string parentId)
    {
        var resp = await api.PostAsJsonAsync("/StringTreeNode", new
        {
            text = name,
            parentId,
            value = new { name }
        });
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync());
        var data = doc.RootElement.GetProperty("data");
        return data.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()!
            : data.GetProperty("value").GetProperty("id").GetString()!;
    }

    /// <summary>按 ID 删除节点（测试清理，失败不掩盖主体断言）。</summary>
    private static async Task TryDeleteViaApiAsync(HttpClient api, string nodeId)
    {
        try { await api.DeleteAsync($"/StringTreeNode/{nodeId}"); }
        catch { /* 尽力清理 */ }
    }
}
