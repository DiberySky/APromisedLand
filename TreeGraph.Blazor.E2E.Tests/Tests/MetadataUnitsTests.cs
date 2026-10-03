using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/units 页面 E2E。
/// 注意：分类名（如 length / weight）显示在分类标题 h6 里，
/// 不在表格内 —— 分类断言用 h6，单位名断言用表格行。
/// </summary>
public class MetadataUnitsTests : E2ETestBase
{
    public MetadataUnitsTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 显示分类
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsCategories()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await Page.WaitForSelectorAsync("h5:has-text('单位管理')");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // Seed 数据里至少有 length / weight（分类名在 h6 标题）
        await Page.WaitForSelectorAsync("h6:has-text('length')");
        await Page.WaitForSelectorAsync("h6:has-text('weight')");
    }

    // ============================================================
    // 场景 2：新建单位
    // ============================================================

    [Fact]
    public async Task Create_NewUnit_Succeeds()
    {
        var uniqueCat = MetadataHelpers.Unique("e2e_cat");
        var uniqueName = MetadataHelpers.Unique("E2E单位");

        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('新建单位')").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

        // 分类
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('分类')").First,
            uniqueCat);
        // 名称
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('名称')").First,
            uniqueName);
        // 符号
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('符号')").First,
            "EU");
        // 换算系数
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('换算系数')").First,
            "1");

        // 保存
        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('创建')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 新分类出现（h6 标题）+ 单位行出现
        await Page.WaitForSelectorAsync($"h6:has-text('{uniqueCat}')");
        await Page.WaitForSelectorAsync(
            $".mud-table-row:has-text('{uniqueName}')");
    }

    // ============================================================
    // 场景 3：编辑单位名称
    // ============================================================

    [Fact]
    public async Task Edit_UnitName_UpdatesRow()
    {
        // 前置：API 建单位（直接用 UI 建太慢，且需要分类已有）
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var uniqueCat = MetadataHelpers.Unique("e2e_edit");
        var uniqueName = MetadataHelpers.Unique("E2E原");
        var newName = MetadataHelpers.Unique("E2E新");

        // 建单位
        var createResp = await http.PostAsJsonAsync("/api/units", new
        {
            category = uniqueCat,
            name = uniqueName,
            symbol = "EU",
            toBaseFactor = 1m,
            isBaseUnit = true,
            displayOrder = 1
        });
        createResp.EnsureSuccessStatusCode();

        await BlazorHelpers.GoToAsync(Page, "/metadata/units");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 等分类出现（h6 标题）
        await Page.WaitForSelectorAsync($"h6:has-text('{uniqueCat}')");

        var row = Page.Locator($".mud-table-row:has-text('{uniqueName}')").First;
        await row.WaitForAsync();

        // 点编辑
        await BlazorHelpers.ClickAsync(Page,
            row.Locator("button[title='编辑']").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('名称')").First,
            newName);

        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('保存')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 表更新
        await Page.WaitForSelectorAsync(
            $".mud-table-row:has-text('{newName}')");
    }
}
