using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/entity-types 页面 E2E。
/// </summary>
public class MetadataEntityTypesTests : E2ETestBase
{
    public MetadataEntityTypesTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：页面加载 + 表格渲染
    // ============================================================

    [Fact]
    public async Task Page_Loads_ShowsTable()
    {
        await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

        await Page.WaitForSelectorAsync("h5:has-text('实体类型管理')");
        await Page.WaitForSelectorAsync("button:has-text('新建实体类型')");

        // 至少 EavSeeder 的 Product 在
        await Page.WaitForSelectorAsync(".mud-table");
    }

    // ============================================================
    // 场景 2：新建实体类型（只填 DisplayName）
    // ============================================================

    [Fact]
    public async Task Create_OnlyDisplayName_Succeeds()
    {
        var displayName = MetadataHelpers.Unique("E2E类型");

        await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

        // 点"新建实体类型"
        await BlazorHelpers.ClickAsync(Page,
            Page.Locator("button:has-text('新建实体类型')").First);

        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

        // 只填名称，不填内部标识（新方案已隐藏）
        var nameField = dlg.Locator(".mud-input-control:has-text('实体类型名称')").First;
        await BlazorHelpers.FillMudTextFieldAsync(nameField, displayName);

        // 点"保存"
        var saveBtn = dlg.Locator("button:has-text('保存')").Last;
        await saveBtn.ClickAsync();

        var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page, "已创建");
        Assert.Contains(displayName, snackbar);

        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 表格出现该名称
        await Page.WaitForSelectorAsync(
            $".mud-table-row:has-text('{displayName}')");
    }

    // ============================================================
    // 场景 3：编辑显示名
    // ============================================================

    [Fact]
    public async Task Edit_DisplayName_UpdatesRow()
    {
        // 前置：API 建类型
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E编辑前"));
        var newDisplayName = MetadataHelpers.Unique("E2E编辑后");

        try
        {
            await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

            // 找目标行
            var row = Page.Locator($".mud-table-row:has-text('{displayName}')").First;
            await row.WaitForAsync();

            // 点编辑图标
            var editBtn = row.Locator("button[title='编辑']").First;
            await BlazorHelpers.ClickAsync(Page, editBtn);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
            var nameField = dlg.Locator(".mud-input-control:has-text('实体类型名称')").First;
            await BlazorHelpers.FillMudTextFieldAsync(nameField, newDisplayName);

            var saveBtn = dlg.Locator("button:has-text('保存')").Last;
            await saveBtn.ClickAsync();

            await BlazorHelpers.WaitForSnackbarAsync(Page, "更新成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 表格更新
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{newDisplayName}')");
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 4：删除（确认对话框）
    // ============================================================

    [Fact]
    public async Task Delete_RemovesRow()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (_, _, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E删除"));

        await BlazorHelpers.GoToAsync(Page, "/metadata/entity-types");

        var row = Page.Locator($".mud-table-row:has-text('{displayName}')").First;
        await row.WaitForAsync();

        var delBtn = row.Locator("button[title='删除']").First;
        await BlazorHelpers.ClickAsync(Page, delBtn);

        // MudBlazor MessageBox 是 dialog
        var dlg = await BlazorHelpers.WaitForDialogAsync(Page);
        var confirmBtn = dlg.Locator("button:has-text('删除')").Last;
        await confirmBtn.ClickAsync();

        await BlazorHelpers.WaitForSnackbarAsync(Page, "删除成功");
        await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

        // 表格不再有该行
        var remaining = Page.Locator($".mud-table-row:has-text('{displayName}')");
        Assert.Equal(0, await remaining.CountAsync());
    }
}
