using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Pages.Inodes;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

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

        // 宽度约束改挂在外层包装 div 上，使用 min() 函数保证窄屏不溢出
        Assert.Contains("min-width: min(480px, 100%)", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("max-width: 640px", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Any_QuickAccessPaper_NoFixedMinWidth()
    {
        // 原 IsCompact 三元分支已移除，任何平台下都不应有硬编码 480px min-width
        var cut = Render<InodeList>();

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
