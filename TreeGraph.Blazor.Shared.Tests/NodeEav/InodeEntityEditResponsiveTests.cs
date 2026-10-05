using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEav.Pages.Inodes;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEav;

/// <summary>InodeEntityEdit 紧凑布局测试（bUnit + Fake API）。</summary>
public class InodeEntityEditResponsiveTests : BunitTestBase {
    private readonly FakePlatformContext _platform;

    public InodeEntityEditResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _platform = NodeEavTestSetup.RegisterServices(Services, (path, _) => path switch
        {
            "/api/eav/item/schema" => NodeEavTestSetup.Json.Schema(),
            "/api/inode/test-inode/entities/item" => NodeEavTestSetup.Json.Entity(),
            _ => null
        });
    }

    [Fact]
    public void Desktop_RendersButtonGroup()
    {
        _platform.IsCompact = false;
        var cut = Render<InodeEntityEdit>(p =>
        {
            p.Add(x => x.InodeId, "test-inode");
            p.Add(x => x.EntityType, "item");
        });

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll("button.mud-button-root");
            Assert.True(buttons.Count >= 3,
                $"Expected >= 3 buttons, got {buttons.Count}");
            Assert.Empty(cut.FindAll(".mud-menu"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_RendersMenuForSecondaryActions()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeEntityEdit>(p =>
        {
            p.Add(x => x.InodeId, "test-inode");
            p.Add(x => x.EntityType, "item");
        });

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll(".mud-menu"));
            Assert.NotEmpty(cut.FindAll("button.mud-button-filled.mud-button-filled-primary"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_MudPaper_NarrowPadding()
    {
        _platform.IsCompact = true;
        var cut = Render<InodeEntityEdit>(p =>
        {
            p.Add(x => x.InodeId, "test-inode");
            p.Add(x => x.EntityType, "item");
        });

        cut.WaitForAssertion(() =>
        {
            var paper = cut.Find(".mud-paper");
            Assert.Contains("pa-2", paper.ClassName, StringComparison.Ordinal);
            Assert.Contains("mb-3", paper.ClassName, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }
}
