using Bunit;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Pages.Entities;
using TreeGraph.Blazor.Shared.Platform;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>EntityList 紧凑布局测试（bUnit + Fake API）。</summary>
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

    [Fact]
    public void Compact_RendersCardsNoTable()
    {
        _platform.IsCompact = true;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".mud-table"));
            Assert.NotEmpty(cut.FindAll(".mud-card"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Compact_CardsShowPreviewColumns()
    {
        _platform.IsCompact = true;
        var cut = Render<EntityList>(p =>
            p.Add(x => x.EntityType, "item"));

        cut.WaitForAssertion(() =>
        {
            var card = cut.Find(".mud-card");
            Assert.Contains("测试项", card.TextContent, StringComparison.Ordinal);
            Assert.Contains("名称", card.TextContent, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));
    }
}
