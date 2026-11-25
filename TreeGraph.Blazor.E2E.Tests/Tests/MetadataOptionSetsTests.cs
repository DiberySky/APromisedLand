using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/option-sets 页面 E2E。
///
/// 当前 UI 为平铺 MudTable：行操作为「管理选项定义」图标，选项的增删与
/// 「设为默认」都在弹出的「选项定义」对话框中完成（不再是展开面板）。
/// </summary>
public class MetadataOptionSetsTests : E2ETestBase
{
    public MetadataOptionSetsTests(PlaywrightFixture fixture) : base(fixture) { }

    private HttpClient NewHttp() => new()
    {
        BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
    };

    private const string PageTitleSelector = "h5:has-text('选项集定义管理')";

    private static ILocator RowOf(IPage page, string text)
        => page.Locator($".mud-table-row:has-text('{text}')").First;

    /// <summary>打开某选项集的「选项定义」对话框。</summary>
    private static async Task<ILocator> OpenItemsDialogAsync(IPage page, string setName)
    {
        var row = RowOf(page, setName);
        await row.WaitForAsync();
        await BlazorHelpers.ClickAsync(page, row.Locator("button[title='管理选项定义']").First);
        return await BlazorHelpers.WaitForDialogAsync(page);
    }

    // ============================================================
    // 场景 1：页面加载 + 显示自建选项集
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsCreatedSet()
    {
        var setName = MetadataHelpers.Unique("e2e_seed");
        using var http = NewHttp();
        try
        {
            var createResp = await http.PostAsJsonAsync(
                "/api/eav/metadata/option-sets",
                new { entityType = "Shared", setName, displayName = setName });
            createResp.EnsureSuccessStatusCode();

            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await Page.WaitForSelectorAsync(PageTitleSelector);
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await Page.WaitForSelectorAsync($"text={setName}");
        }
        finally
        {
            await MetadataHelpers.TryDeleteOptionSetByNameAsync(http, setName);
        }
    }

    // ============================================================
    // 场景 2：新建选项集
    // ============================================================

    [Fact]
    public async Task Create_NewSet_Succeeds()
    {
        var setName = MetadataHelpers.Unique("e2e_set");
        var displayName = MetadataHelpers.Unique("E2E选项集");
        using var http = NewHttp();

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await Page.WaitForSelectorAsync(PageTitleSelector);
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建选项集')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                displayName);
            // 中文显示名不会自动生成内部标识，必须显式填写
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('内部标识')").First,
                setName);

            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            await RowOf(Page, displayName).WaitForAsync();
        }
        finally
        {
            await MetadataHelpers.TryDeleteOptionSetByNameAsync(http, setName);
        }
    }

    // ============================================================
    // 场景 3：添加选项
    // ============================================================

    [Fact]
    public async Task AddItem_ToNewSet_ShowsInList()
    {
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_items");
        var itemValue = MetadataHelpers.Unique("e2e_val");

        string? setId = null;
        try
        {
            var createResp = await http.PostAsJsonAsync(
                "/api/eav/metadata/option-sets",
                new { entityType = "Shared", setName, displayName = setName });
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content
                .ReadFromJsonAsync<TreeGraph.Shared.NodeEavSky.Dtos.ApiResponse<IdResponse>>();
            setId = created!.Data!.OptionSetId;

            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await Page.WaitForSelectorAsync(PageTitleSelector);
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            var itemsDialog = await OpenItemsDialogAsync(Page, setName);

            await BlazorHelpers.ClickAsync(Page,
                itemsDialog.Locator("button[title='添加选项']").First);

            var itemDialog = await BlazorHelpers.WaitForDialogAsync(Page);

            await BlazorHelpers.FillMudTextFieldAsync(
                itemDialog.Locator(".mud-input-control:has-text('显示名')").First,
                "E2E标签");
            await BlazorHelpers.FillMudTextFieldAsync(
                itemDialog.Locator(".mud-input-control:has-text('内部标识')").First,
                itemValue);

            await BlazorHelpers.ClickAsync(Page,
                itemDialog.Locator("button:has-text('保存')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "添加成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            await itemsDialog.Locator($".mud-table-row:has-text('{itemValue}')")
                .First.WaitForAsync();
        }
        finally
        {
            if (setId is not null)
                await MetadataHelpers.TryDeleteOptionSetAsync(http, setId);
        }
    }

    // ============================================================
    // 场景 4：设为默认
    // ============================================================

    [Fact]
    public async Task SetDefault_ShowsStarIcon()
    {
        using var http = NewHttp();
        var setName = MetadataHelpers.Unique("e2e_default");

        string? setId = null;
        try
        {
            var createResp = await http.PostAsJsonAsync(
                "/api/eav/metadata/option-sets",
                new { entityType = "Shared", setName, displayName = setName });
            createResp.EnsureSuccessStatusCode();

            var created = await createResp.Content
                .ReadFromJsonAsync<TreeGraph.Shared.NodeEavSky.Dtos.ApiResponse<IdResponse>>();
            setId = created!.Data!.OptionSetId;

            foreach (var v in new[] { "a", "b" })
            {
                await http.PostAsJsonAsync(
                    $"/api/eav/metadata/option-sets/{setId}/items",
                    new { value = v, label = v.ToUpper(), displayOrder = 0, isDefault = false });
            }

            await BlazorHelpers.GoToAsync(Page, "/metadata/option-sets");
            await Page.WaitForSelectorAsync(PageTitleSelector);
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            var itemsDialog = await OpenItemsDialogAsync(Page, setName);

            // 显示名 A / B，避免 has-text('a') 命中其它文本
            var rowA = itemsDialog.Locator(".mud-table-body .mud-table-row:has-text('A')").First;
            await rowA.WaitForAsync();

            await BlazorHelpers.ClickAsync(Page,
                rowA.Locator("button[title='设为默认']").First);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "设为默认");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            await itemsDialog.Locator(".mud-table-body .mud-table-row:has-text('A')")
                .First.Locator(".mud-icon-root").First.WaitForAsync();
        }
        finally
        {
            if (setId is not null)
                await MetadataHelpers.TryDeleteOptionSetAsync(http, setId);
        }
    }

    private sealed record IdResponse(string OptionSetId);
}
