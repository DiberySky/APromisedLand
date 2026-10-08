using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Components;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>DynamicForm 栅格化与 CompactLayout 测试。</summary>
public class DynamicFormResponsiveTests : BunitTestBase {
    public DynamicFormResponsiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
    }

    private static List<AttributeSchemaDto> BuildSchema(int count)
    {
        return Enumerable.Range(0, count).Select(i => new AttributeSchemaDto(
            AttributeName: $"attr{i}",
            DisplayName: $"属性{i}",
            DataType: "string",
            IsRequired: false,
            IsSearchable: false,
            IsSortable: false,
            DisplayOrder: i,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: null)).ToList();
    }

    [Fact]
    public void Default_RendersMudGridWithResponsiveItems()
    {
        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.Schema, BuildSchema(3));
        });

        Assert.NotEmpty(cut.FindAll(".mud-grid"));
        // MudBlazor 9：MudItem 渲染为 mud-grid-item
        var items = cut.FindAll(".mud-grid-item");
        Assert.Equal(3, items.Count);
        // 默认：xs=12 sm=6 md=4
        foreach (var item in items)
        {
            Assert.Contains("mud-grid-item-xs-12", item.ClassName);
            Assert.Contains("mud-grid-item-sm-6", item.ClassName);
            Assert.Contains("mud-grid-item-md-4", item.ClassName);
        }
    }

    [Fact]
    public void CompactLayout_FixedXs12Only()
    {
        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.Schema, BuildSchema(2));
            p.Add(x => x.CompactLayout, true);
        });

        var items = cut.FindAll(".mud-grid-item");
        Assert.Equal(2, items.Count);
        foreach (var item in items)
        {
            Assert.Contains("mud-grid-item-xs-12", item.ClassName);
            Assert.Contains("mud-grid-item-sm-12", item.ClassName);
            Assert.Contains("mud-grid-item-md-12", item.ClassName);
        }
    }

    [Fact]
    public void LoadFrom_StringValue_IsBoundToField()
    {
        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.Schema, BuildSchema(1));
        });

        var elem = System.Text.Json.JsonDocument.Parse("\"hello\"").RootElement.Clone();
        cut.InvokeAsync(() => cut.Instance.LoadFrom(
            new Dictionary<string, System.Text.Json.JsonElement> { ["attr0"] = elem }));

        var input = cut.Find("input");
        Assert.Equal("hello", input.GetAttribute("value"));
    }

    [Fact]
    public void SaveButton_InFlexEndStack()
    {
        var cut = Render<DynamicForm>(p =>
        {
            p.Add(x => x.EntityType, "item");
            p.Add(x => x.Schema, BuildSchema(1));
        });

        // MudStack Row 渲染为 d-flex flex-row
        var stack = cut.Find(".mud-form .d-flex.flex-row");
        Assert.NotNull(stack);
        // Justify.FlexEnd 渲染为 justify-end
        Assert.Contains("justify-end", stack.ClassName, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("button.mud-button-filled.mud-button-filled-primary"));
    }
}
