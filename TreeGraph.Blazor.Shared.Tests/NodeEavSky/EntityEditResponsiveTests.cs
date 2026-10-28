using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Pages.Entities;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>EntityEdit 紧凑布局测试（bUnit + Fake API）。</summary>
public class EntityEditResponsiveTests : BunitTestBase {
    private readonly FakePlatformContext _platform;

    public EntityEditResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        _platform = NodeEavTestSetup.RegisterServices(Services, (path, method) => path switch
        {
            "/api/eav/item/schema" => NodeEavTestSetup.Json.Schema(),
            "/api/eav/item/entities/e1" => NodeEavTestSetup.Json.Entity(),
            _ => null
        });
    }

    [Fact]
    public void Desktop_RendersButtonGroup()
    {
        _platform.IsCompact = false;
        var cut = Render<EntityEdit>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.EntityId, "e1");
        });

        cut.WaitForAssertion(() =>
        {
            // 桌面端：4 个按钮（返回、保存、重新加载、清空、删除）
            var buttons = cut.FindAll("button.mud-button-root");
            Assert.True(buttons.Count >= 4,
                $"Expected >= 4 buttons, got {buttons.Count}");
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_RendersMenuForSecondaryActions()
    {
        _platform.IsCompact = true;
        var cut = Render<EntityEdit>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.EntityId, "e1");
        });

        cut.WaitForAssertion(() =>
        {
            // 紧凑布局：存在 mud-menu 容器（MoreVert 图标按钮收纳次要操作）
            Assert.NotEmpty(cut.FindAll(".mud-menu"));
            // 主按钮（返回 + 保存）仍存在
            Assert.NotEmpty(cut.FindAll("button.mud-button-filled.mud-button-filled-primary"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_MudPaper_NarrowPadding()
    {
        _platform.IsCompact = true;
        var cut = Render<EntityEdit>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.EntityId, "e1");
        });

        cut.WaitForAssertion(() =>
        {
            var paper = cut.Find(".mud-paper");
            Assert.Contains("pa-2", paper.ClassName, StringComparison.Ordinal);
            Assert.Contains("mb-3", paper.ClassName, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }
}
