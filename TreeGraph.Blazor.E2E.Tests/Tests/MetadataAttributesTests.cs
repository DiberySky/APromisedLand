using System.Net.Http.Json;
using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>
/// /metadata/attributes 页面 E2E。
/// </summary>
public class MetadataAttributesTests : E2ETestBase
{
    public MetadataAttributesTests(PlaywrightFixture fixture) : base(fixture) { }

    // ============================================================
    // 场景 1：选择实体类型 → 显示属性列表
    // ============================================================

    [Fact]
    public async Task SelectEntityType_ShowsAttributes()
    {
        // 前置：建类型 + 1 个属性
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E属性页"));
        var attrDisplay = MetadataHelpers.Unique("屏幕");

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "screen_size", attrDisplay, "decimal");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await Page.WaitForSelectorAsync("h5:has-text('属性定义管理')");

            // 等下拉加载
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 打开实体类型下拉
            var typeSelect = Page.Locator(".mud-select").First;
            await BlazorHelpers.ClickAsync(Page, typeSelect);

            // 选中我们建的类型
            var option = Page.Locator(
                $".mud-list-item:has-text('{displayName}')").First;
            await option.WaitForAsync();
            await option.ClickAsync();

            // 表格出现该属性
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{attrDisplay}')");
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 2：新建属性（不展开高级选项，AttributeName 自动生成）
    // ============================================================

    [Fact]
    public async Task Create_OnlyDisplayName_AutoGeneratesName()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E新属性"));

        try
        {
            // 用一个已有属性占位，让页面能加载（不然该类型无属性，页面显示空）
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 选类型
            var typeSelect = Page.Locator(".mud-select").First;
            await BlazorHelpers.ClickAsync(Page, typeSelect);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();

            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            // 点"新建属性"
            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);

            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 显示名（不展开高级选项）
            var attrDisplay = MetadataHelpers.Unique("自动命名");
            var nameField = dlg.Locator(
                ".mud-input-control:has-text('显示名')").First;
            await BlazorHelpers.FillMudTextFieldAsync(nameField, attrDisplay);

            // 保存
            var saveBtn = dlg.Locator("button:has-text('创建')").Last;
            await BlazorHelpers.ClickAsync(Page, saveBtn);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 表格出现
            await Page.WaitForSelectorAsync(
                $".mud-table-row:has-text('{attrDisplay}')");

            // 副行包含 attr_ 前缀
            var row = Page.Locator(
                $".mud-table-row:has-text('{attrDisplay}')").First;
            var rowText = await row.InnerTextAsync();
            Assert.Contains("attr_", rowText);
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 3：新建属性（展开高级选项，显式指定 AttributeName）
    // ============================================================

    [Fact]
    public async Task Create_WithExplicitName_UsesGiven()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E显式名"));

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page, Page.Locator(".mud-select").First);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);
            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            // 显示名
            var attrDisplay = MetadataHelpers.Unique("显式命名");
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                attrDisplay);

            // 展开"高级选项（内部标识）"
            var advanced = dlg.Locator(
                $"{MudSelectors.ExpandPanel}:has-text('高级选项')").First;
            await BlazorHelpers.ExpandPanelAsync(Page, advanced);

            // 填内部标识
            var explicitName = $"my_field_{Guid.NewGuid():N}"[..20];
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('内部标识')").First,
                explicitName);

            // 保存
            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            await BlazorHelpers.WaitForSnackbarAsync(Page, "创建成功");
            await BlazorHelpers.WaitForSnackbarGoneAsync(Page);

            // 副行含显式名
            var row = Page.Locator(
                $".mud-table-row:has-text('{attrDisplay}')").First;
            var rowText = await row.InnerTextAsync();
            Assert.Contains(explicitName, rowText);
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }

    // ============================================================
    // 场景 4：非法内部标识 → 前端拦截
    // ============================================================

    [Fact]
    public async Task Create_InvalidExplicitName_ShowsWarning()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl)
        };
        var (id, name, displayName) = await MetadataHelpers.CreateEntityTypeAsync(
            http, MetadataHelpers.Unique("E2E非法名"));

        try
        {
            await MetadataHelpers.CreateAttributeAsync(
                http, name, "seed", "占位", "string");

            await BlazorHelpers.GoToAsync(Page, "/metadata/attributes");
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page, Page.Locator(".mud-select").First);
            await Page.Locator($".mud-list-item:has-text('{displayName}')")
                .First.ClickAsync();
            await BlazorHelpers.WaitForLoadingCompleteAsync(Page);

            await BlazorHelpers.ClickAsync(Page,
                Page.Locator("button:has-text('新建属性')").First);
            var dlg = await BlazorHelpers.WaitForDialogAsync(Page);

            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('显示名')").First,
                MetadataHelpers.Unique("测试"));

            // 展开高级选项
            await BlazorHelpers.ExpandPanelAsync(Page,
                dlg.Locator($"{MudSelectors.ExpandPanel}:has-text('高级选项')").First);

            // 填非法名（数字开头）
            await BlazorHelpers.FillMudTextFieldAsync(
                dlg.Locator(".mud-input-control:has-text('内部标识')").First,
                "123_invalid");

            // 保存
            await BlazorHelpers.ClickAsync(Page,
                dlg.Locator("button:has-text('创建')").Last);

            // 前端拦截：出现 Warning Snackbar
            var snackbar = await BlazorHelpers.WaitForSnackbarAsync(Page,
                expectedText: "内部标识");
            Assert.Contains("内部标识", snackbar);

            // 对话框仍未关闭（因为校验失败）
            Assert.True(await dlg.IsVisibleAsync());
        }
        finally
        {
            await MetadataHelpers.TryDeleteEntityTypeAsync(http, id);
        }
    }
}
