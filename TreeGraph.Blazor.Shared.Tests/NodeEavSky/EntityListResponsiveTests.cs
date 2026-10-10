using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Pages.Entities;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>
/// EntityList 响应式测试（bUnit + Fake API）。
/// 重构后对齐 DiberyTree 模式：MudTable 始终渲染，通过 Breakpoint.Sm 实现
/// 小屏自动切卡片布局；行操作通过 MoreHoriz 按钮聚合。
/// </summary>
public class EntityListResponsiveTests : BunitTestBase {
    private readonly FakePlatformContext _platform;

    public EntityListResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _platform = NodeEavTestSetup.RegisterServices(Services, (path, method) =>
            (path, method.Method) switch
            {
                ("/api/eav/item/schema", "GET") => NodeEavTestSetup.Json.Schema(),
                ("/api/eav/item/entities/query", "POST") => NodeEavTestSetup.Json.Paged(),
                _ => null
            });
    }

    [Fact]
    public void Desktop_RendersMudTable()
    {
        _platform.IsCompact = false;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".mud-table"));
            Assert.Empty(cut.FindAll(".mud-card"));
        }, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 重构后 compact 模式也渲染 MudTable（Breakpoint.Sm 通过 CSS 自动切卡片，
    /// 不再走 Platform.IsCompact 组件分支）。
    /// </summary>
    [Fact]
    public void Compact_StillRendersMudTable()
    {
        _platform.IsCompact = true;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".mud-table"));
        }, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 表格内容始终包含预览列（不再区分卡片/表格模式）。
    /// </summary>
    [Fact]
    public void TableShowsPreviewColumns()
    {
        _platform.IsCompact = false;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            var table = cut.Find(".mud-table");
            Assert.Contains("测试项", table.TextContent, StringComparison.Ordinal);
            Assert.Contains("名称", table.TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// 重构后行操作通过 MoreHoriz 按钮聚合（对齐 DiberyTree TreeNodeActionsDialog 模式）。
    /// </summary>
    [Fact]
    public void RowActionsUseMoreHorizButton()
    {
        _platform.IsCompact = false;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            var moreHorizButtons = cut.FindAll("button[aria-label='操作'], .mud-icon-button");
            Assert.NotEmpty(moreHorizButtons);
        }, TimeSpan.FromSeconds(2));
    }
}
