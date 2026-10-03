using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// iNode 完整用户流程 E2E。
///
/// 每个测试用独立的 entityType + 独立 iNodeId，互不干扰。
/// 前置条件：Aspire AppHost 已启动（Blazor 5783 / API 5773）。
/// </summary>
public class InodeFlowTests : E2ETestBase
{
    public InodeFlowTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：从列表页进入空 iNode，声明类型
    // ============================================================

    [Fact]
    public async Task Flow_NewInode_DeclareType_ShowsCard()
    {
        // 前置：建类型
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E商品_{Guid.NewGuid():N}");

        // 1. 打开 /inodes
        await BlazorHelpers.GoToAsync(Page, "/inodes");
        await Page.WaitForSelectorAsync("h5:has-text('iNode 列表')");

        // 2. 输入 iNode GUID → 点"访问"
        var inodeId = NewInodeId();
        var input = Page.Locator(
            "input[placeholder='xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx']").First;
        await input.FillAsync(inodeId);

        var accessBtn = Page.Locator("button:has-text('访问')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, accessBtn, $"**/inodes/{inodeId}");

        // 3. 跳到详情页
        await Page.WaitForSelectorAsync("h5:has-text('iNode 详情')");

        // 4. 空状态：显示"声明新类型"按钮
        await Page.WaitForSelectorAsync("text=该 iNode 尚未声明任何类型");

        // 5. 点"声明新类型"
        var declareBtn = Page.Locator("button:has-text('声明新类型')").First;
        await declareBtn.ClickAsync();

        // 6. 对话框里选类型
        var dialog = await BlazorHelpers.WaitForDialogAsync(Page);
        var select = dialog.Locator(".mud-select").First;
        await select.ClickAsync();

        // 7. 选中类型（按 displayName 匹配）
        var option = Page.Locator($".mud-list-item:has-text('{typeName}')").First;
        await option.ClickAsync();

        // 8. 点"声明"
        var confirmBtn = dialog.Locator("button:has-text('声明')").Last;
        await confirmBtn.ClickAsync();

        // 9. 校验 Snackbar（传期望文本，避免抓到旧 Snackbar）
        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        Assert.Contains("声明成功", snackbar);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 10. 卡片出现
        await Page.WaitForSelectorAsync($".mud-card:has-text('{typeName}')");
        await Page.WaitForSelectorAsync("text=未填写");
    }

    // ============================================================
    // 场景 2：填写实体 → 刷新后数据持久化
    // ============================================================

    [Fact]
    public async Task Flow_FillEntity_Reload_Persists()
    {
        // 前置：建类型 + 建 string / int 属性
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E订单_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "order_no", "订单号", "string");
        await CreateAttributeViaApiAsync(
            typeName, "amount", "金额", "int");

        var inodeId = NewInodeId();

        // 1. 直接访问详情页 + 声明 + 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");

        // ★ 关键操作前确认连接正常
        Assert.True(await BlazorHelpers.IsSignalRConnectedAsync(Page),
            "SignalR 应处于连接状态");

        // 声明
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dialog = await BlazorHelpers.WaitForDialogAsync(Page);
        await dialog.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dialog.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 2. 点"填写"进入编辑页
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await BlazorHelpers.ClickAndNavigateAsync(
            Page, card.Locator("button:has-text('填写')"),
            $"**/inodes/{inodeId}/types/**");

        // 3. 填表单
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // string 属性 → MudTextField
        var orderNoField = Page.Locator(".mud-input-control:has-text('订单号')").First;
        await BlazorHelpers.FillMudTextFieldAsync(orderNoField, "SO-001");

        // int 属性 → MudNumericField（同样是 input 元素）
        var amountField = Page.Locator(".mud-input-control:has-text('金额')").First;
        await BlazorHelpers.FillMudTextFieldAsync(amountField, "999");

        // 4. 点保存
        var saveBtn = Page.Locator("button:has-text('保存')").First;
        await saveBtn.ClickAsync();

        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", snackbar);

        // 5. 刷新页面
        await Page.ReloadAsync();
        await BlazorHelpers.WaitForBlazorAsync(Page);
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 6. 校验表单值持久化
        var orderNoAfterReload = Page.Locator(".mud-input-control:has-text('订单号')")
            .Locator("input").First;
        var orderNoValue = await orderNoAfterReload.InputValueAsync();
        Assert.Equal("SO-001", orderNoValue);

        var amountAfterReload = Page.Locator(".mud-input-control:has-text('金额')")
            .Locator("input").First;
        var amountValue = await amountAfterReload.InputValueAsync();
        Assert.Equal("999", amountValue);
    }

    // ============================================================
    // 场景 3：编辑已有实体 → iNode 详情页显示"已填写"
    // ============================================================

    [Fact]
    public async Task Flow_AfterSave_DetailShowsFilled()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E库存_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "warehouse", "仓库", "string");

        var inodeId = NewInodeId();

        // 声明 + 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var whField = Page.Locator(".mud-input-control:has-text('仓库')").First;
        await BlazorHelpers.FillMudTextFieldAsync(whField, "BJ-01");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var savedSnack = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", savedSnack);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 返回 iNode 详情
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        // 校验"已填写"
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await card.Locator("text=已填写").WaitForAsync();
    }

    // ============================================================
    // 场景 4：审计历史显示
    // ============================================================

    [Fact]
    public async Task Flow_History_ShowsChanges()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E审计_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "note", "备注", "string");

        var inodeId = NewInodeId();

        // 快速填写 2 次
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 第一次保存
        var noteField = Page.Locator(".mud-input-control:has-text('备注')").First;
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "v1");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved1 = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved1);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 第二次保存
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "v2");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved2 = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved2);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 返回 iNode 详情，点"历史"
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        var histBtn = Page.Locator(
            $".mud-card:has-text('{typeName}') button:has-text('历史')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, histBtn, "**/history");
        await Page.WaitForSelectorAsync("h5:has-text('审计历史')");

        // 至少有一条时间线项
        var timelineItems = Page.Locator(".mud-timeline-item");
        await timelineItems.First.WaitForAsync();
        var count = await timelineItems.CountAsync();
        Assert.True(count >= 2, $"期望 >= 2 条历史，实际 {count}");

        // 包含 v1 / v2
        var pageText = await Page.ContentAsync();
        Assert.Contains("v1", pageText);
        Assert.Contains("v2", pageText);
    }

    // ============================================================
    // 场景 5：删除实体
    // ============================================================

    [Fact]
    public async Task Flow_DeleteEntity_BackToUnfilled()
    {
        var (typeId, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2E删除_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(
            typeName, "x", "X", "string");

        var inodeId = NewInodeId();

        // 填写
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var xField = Page.Locator(".mud-input-control:has-text('X')").First;
        await BlazorHelpers.FillMudTextFieldAsync(xField, "value");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 删除
        var deleteBtn = Page.Locator("button:has-text('删除实体')").First;
        await deleteBtn.ClickAsync();

        // 确认对话框
        var confirmDlg = await BlazorHelpers.WaitForDialogAsync(Page);
        var yesBtn = confirmDlg.Locator("button:has-text('永久删除')").First;
        await BlazorHelpers.ClickAndNavigateAsync(
            Page, yesBtn, $"**/inodes/{inodeId}");

        // 卡片显示"未填写"
        var card = Page.Locator($".mud-card:has-text('{typeName}')").First;
        await card.Locator("text=未填写").WaitForAsync();
    }

    // ============================================================
    // 场景 6：JSON 预览抽屉
    //
    // 只保护：
    //   - 抽屉能打开（UI 入口没坏）
    //   - JSON 契约（含 entities / 类型名 / 我们填的值）
    //   - includeNull 开关生效（保护"ValueChanged 忽略参数"类回归）
    //   - 关闭按钮工作
    //
    // 不测：
    //   - displayName / originalUnits（与 includeNull 同模式，重复覆盖）
    //   - 复制按钮（浏览器剪贴板权限在 CI 环境不稳定）
    //   - JSON 格式化细节（脆弱，断言我们控得住的字段）
    // ============================================================

    [Fact]
    public async Task Flow_JsonPreviewDrawer_Works()
    {
        // 前置：建类型 + 2 个 string 属性。
        // ★ 必须只填 note、留 extra 不填——否则 includeNull=true 时
        //   没有未写入属性可补 null，"等 null 出现"永远超时。
        var (_, typeName) = await CreateEntityTypeWithNameAsync(
            $"E2EJSON_{Guid.NewGuid():N}");
        await CreateAttributeViaApiAsync(typeName, "note", "备注", "string");
        await CreateAttributeViaApiAsync(typeName, "extra", "附加", "string");

        var inodeId = NewInodeId();

        // 声明 + 填写（确保 JSON 里有数据）
        await BlazorHelpers.GoToAsync(Page, $"/inodes/{inodeId}");
        await Page.Locator("button:has-text('声明新类型')").First.ClickAsync();
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await dlg.Locator(".mud-select").First.ClickAsync();
        await Page.Locator($".mud-list-item:has-text('{typeName}')").First.ClickAsync();
        await dlg.Locator("button:has-text('声明')").Last.ClickAsync();
        await BlazorHelpers.WaitForSnackbarAsync(Page, "声明成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        await BlazorHelpers.ClickAndNavigateAsync(
            Page,
            Page.Locator($".mud-card:has-text('{typeName}') button:has-text('填写')").First,
            $"**/inodes/{inodeId}/types/**");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var noteField = Page.Locator(".mud-input-control:has-text('备注')").First;
        await BlazorHelpers.FillMudTextFieldAsync(noteField, "hello-json");
        await Page.Locator("button:has-text('保存')").First.ClickAsync();
        var saved = await BlazorHelpers.WaitForSnackbarAsync(Page, "保存成功");
        Assert.Contains("保存成功", saved);
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 回到 iNode 详情
        var backBtn = Page.Locator("button:has-text('返回 iNode')").First;
        await BlazorHelpers.ClickAndNavigateAsync(Page, backBtn, $"**/inodes/{inodeId}");

        // ---- 1. 打开抽屉 ----
        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('JSON 预览')").First);

        // ★ 用标题锚定真抽屉（MudBlazor Temporary 抽屉会残留隐藏 DOM，
        //   .First 可能命中残留节点）
        var drawer = BlazorHelpers.FindDrawerByTitle(Page, "原始 JSON 预览");
        await drawer.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        // ---- 2. 等 JSON 渲染完成（pre 非空）----
        // ★ querySelector 只取第一个匹配（可能命中残留 drawer），
        //   必须用 querySelectorAll + some()
        await Page.WaitForFunctionAsync(
            @"() => {
                const pres = document.querySelectorAll('.mud-drawer pre');
                return Array.from(pres).some(p => p.textContent.length > 10);
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var textDefault = await drawer.Locator("pre").First.InnerTextAsync();

        // ---- 3. 断言关键契约（只断言我们控得住的）----
        Assert.Contains("entities", textDefault);
        Assert.Contains(typeName, textDefault);
        Assert.Contains("hello-json", textDefault);

        // 默认（includeNull=false）只含已写属性，不应出现 null
        Assert.DoesNotContain("null", textDefault);

        // ---- 4. 切 includeNull → 未填的 extra 以 null 出现 ----
        await drawer.Locator(".mud-switch:has-text('includeNull')")
            .Locator("input").First.ClickAsync();

        await Page.WaitForFunctionAsync(
            "() => document.querySelector('.mud-drawer pre')" +
            "?.textContent?.includes('null')",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // ---- 5. 关闭抽屉 ----
        await drawer.Locator("button[title='关闭']").First.ClickAsync();

        // ★ MudBlazor 9.9 关闭的 Temporary drawer 用 transform 移出屏幕，
        //   元素无 display:none → Playwright 判定仍"可见"，
        //   WaitForAsync(Hidden) 永远超时。关闭信号 = class 含
        //   mud-drawer--closed（双横线）；元素被移除（!d）也算关闭。
        await Page.WaitForFunctionAsync(
            @"() => {
                const d = Array.from(document.querySelectorAll('.mud-drawer'))
                    .find(x => x.textContent.includes('原始 JSON 预览'));
                return !d || d.className.includes('mud-drawer--closed');
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });
    }
}
