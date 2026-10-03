using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/option-sets 页面 E2E。
/// 注意：MudBlazor 9 展开面板 header 类名是 .mud-expand-panel-header。
/// </summary>
public class MetadataOptionSetsTests : E2ETestBase
{
    public MetadataOptionSetsTests(PlaywrightFixture fixture) : base(fixture) { }

    private HttpClient NewHttp() => new()
    {
        BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
    };

    // ============================================================
    // 场景 1：页面加载 + 显示种子选项集
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsSeededSets()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
        await Page.WaitForSelectorAsync("h5:has-text('选项集管理')");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // EavSeeder 建了 gender / quality_grade
        await Page.WaitForSelectorAsync("text=gender");
        await Page.WaitForSelectorAsync("text=quality_grade");
    }

    // ============================================================
    // 场景 2：新建选项集
    // ============================================================

    [Fact]
    public async Task Create_NewSet_Succeeds()
    {
        var setName = MetadataHelpers.Unique("e2e_set");
        var displayName = MetadataHelpers.Unique("E2E选项集");

        await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('新建选项集')").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

        // 实体类型（默认 Shared，不用改）
        // 集合名
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('集合名')").First,
            setName);
        // 显示名
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('显示名')").First,
            displayName);

        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('创建')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 展开面板出现
        await Page.WaitForSelectorAsync(
            $"{MudSelectors.ExpandPanel}:has-text('{displayName}')");
    }

    // ============================================================
    // 场景 3：添加选项
    // ============================================================

    [Fact]
    public async Task AddItem_ToNewSet_ShowsInList()
    {
        // 前置：API 建选项集
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_items");
        var createResp = await http.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType = "Shared", setName, displayName = setName });
        createResp.EnsureSuccessStatusCode();

        await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        // 展开我们建的那个面板
        var panel = Page.Locator(
            $"{MudSelectors.ExpandPanel}:has-text('{setName}')").First;
        await BlazorHelpers.ExpandPanelAsync(Page, panel);

        // 点"添加选项"
        await BlazorHelpers.ClickAsync(Page,
            panel.Locator("button:has-text('添加选项')").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('Value')").First,
            "e2e_val");
        await BlazorHelpers.FillMudTextFieldAsync(
            dlg.Locator(".mud-input-control:has-text('Label')").First,
            "E2E标签");

        await BlazorHelpers.ClickAsync(Page,
            dlg.Locator("button:has-text('保存')").Last);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "添加成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 行出现
        await panel.Locator(".mud-table-row:has-text('e2e_val')")
            .First.WaitForAsync();
    }

    // ============================================================
    // 场景 4：设为默认
    // ============================================================

    [Fact]
    public async Task SetDefault_ShowsStarIcon()
    {
        // 前置：API 建选项集 + 2 个选项
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_default");
        var createResp = await http.PostAsJsonAsync(
            "/api/eav/metadata/option-sets",
            new { entityType = "Shared", setName, displayName = setName });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content
            .ReadFromJsonAsync<IdResponse>();
        var setId = created!.OptionSetId;

        foreach (var v in new[] { "a", "b" })
        {
            await http.PostAsJsonAsync(
                $"/api/eav/metadata/option-sets/{setId}/items",
                new { value = v, label = v.ToUpper(), displayOrder = 0, isDefault = false });
        }

        await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
        await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

        var panel = Page.Locator(
            $"{MudSelectors.ExpandPanel}:has-text('{setName}')").First;
        await BlazorHelpers.ExpandPanelAsync(Page, panel);

        // 等表格出现（限定 tbody，避免 has-text('a') 匹配到含 "Value" 文本的表头行）
        var rowA = panel.Locator(".mud-table-body .mud-table-row:has-text('a')").First;
        await rowA.WaitForAsync();

        // 点"设为默认"（星号）
        await BlazorHelpers.ClickAsync(Page,
            rowA.Locator("button[title='设为默认']").First);

        await BlazorHelpers.WaitForSnackbarAsync(Page, "设为默认");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 校验 a 行有实心星
        await rowA.Locator(".mud-icon-root").First.WaitForAsync();
    }

    private sealed record IdResponse(string OptionSetId);
}
