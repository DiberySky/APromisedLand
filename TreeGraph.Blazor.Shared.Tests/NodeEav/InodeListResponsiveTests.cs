using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEav.Pages.Inodes;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEav;

/// <summary>InodeList 紧凑布局测试（纯 UI，无 API 依赖）。</summary>
public class InodeListResponsiveTests : BunitTestBase {
    private readonly FakePlatformContext _platform;

    public InodeListResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _platform = NodeEavTestSetup.RegisterServices(Services, (_, _) => null);
    }

    [Fact]
    public void Desktop_QuickAccessPaper_WideInput()
    {
        _platform.IsCompact = false;
        var cut = Render<InodeList>();

        Assert.Contains("min-width: 480px", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_QuickAccessPaper_FullWidthInput()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeList>();

        Assert.Contains("width: 100%", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("min-width: 480px", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_QuickAccessPaper_NarrowPadding()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeList>();

        var paper = cut.Find(".mud-paper");
        Assert.Contains("pa-2", paper.ClassName, StringComparison.Ordinal);
        Assert.Contains("mb-3", paper.ClassName, StringComparison.Ordinal);
    }
}
