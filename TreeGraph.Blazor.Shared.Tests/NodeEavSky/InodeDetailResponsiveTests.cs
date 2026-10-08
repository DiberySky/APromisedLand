using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Pages.Inodes;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>InodeDetail 紧凑布局测试（bUnit + Fake API）。</summary>
public class InodeDetailResponsiveTests : BunitTestBase {
    private readonly FakePlatformContext _platform;

    public InodeDetailResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _platform = NodeEavTestSetup.RegisterServices(Services, (path, _) => path switch
        {
            "/api/inode/test-inode/types" => NodeEavTestSetup.Json.EmptyArray(),
            "/api/eav/entity-types" => NodeEavTestSetup.Json.EmptyArray(),
            _ => null
        });
    }

    [Fact]
    public void Desktop_Toolbar_ShowsAllButtons()
    {
        _platform.IsCompact = false;
        var cut = Render<InodeDetail>(p =>
            p.Add(x => x.InodeId, "test-inode"));

        cut.WaitForAssertion(() =>
        {
            // 桌面端：三个操作按钮（JSON 预览/声明新类型/刷新）平铺，无收纳菜单
            Assert.Contains("JSON 预览", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("声明新类型", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("刷新", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll(".mud-menu"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_Toolbar_ShowsMenuForSecondaryActions()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeDetail>(p =>
            p.Add(x => x.InodeId, "test-inode"));

        cut.WaitForAssertion(() =>
        {
            // 紧凑布局：存在 MudMenu 容器
            Assert.NotEmpty(cut.FindAll(".mud-menu"));
            // 「声明新类型」主按钮仍保留（Fille 变体 Primary）
            Assert.NotEmpty(cut.FindAll("button.mud-button-filled.mud-button-filled-primary"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_Paper_NarrowPadding()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeDetail>(p =>
            p.Add(x => x.InodeId, "test-inode"));

        cut.WaitForAssertion(() =>
        {
            var paper = cut.Find(".mud-paper");
            Assert.Contains("pa-2", paper.ClassName, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_JsonDrawer_Width100Percent()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeDetail>(p =>
            p.Add(x => x.InodeId, "test-inode"));

        cut.WaitForAssertion(() =>
        {
            // MudDrawer Width 渲染为 CSS 变量 --mud-drawer-width
            var drawer = cut.Find(".mud-drawer");
            Assert.Contains("--mud-drawer-width:100%", drawer.GetAttribute("style"),
                StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Desktop_JsonDrawer_Width600px()
    {
        _platform.IsCompact = false;
        var cut = Render<InodeDetail>(p =>
            p.Add(x => x.InodeId, "test-inode"));

        cut.WaitForAssertion(() =>
        {
            var drawer = cut.Find(".mud-drawer");
            Assert.Contains("--mud-drawer-width:600px", drawer.GetAttribute("style"),
                StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }
}
