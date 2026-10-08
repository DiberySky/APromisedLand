using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Shared;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Blazor.Tests.Components;

/// <summary>
/// QueryFilterBuilder 组件测试。
///
/// 焦点：AddFilter / RemoveFilter / ClearAll 是否触发正确的状态与回调。
/// 不测 MudSelect 的下拉交互（依赖 JS，bUnit 不覆盖）。
/// </summary>
public class QueryFilterBuilderTests : BunitTestBase
{
    public QueryFilterBuilderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static AttributeSchemaDto MakeAttr(
        string name, string dataType = "string", bool searchable = true)
        => new(
            AttributeName: name,
            DisplayName: name,
            DataType: dataType,
            IsRequired: false,
            IsSearchable: searchable,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: null);

    // ============================================================
    // 空状态
    // ============================================================

    [Fact]
    public void NoSupportedAttributes_RendersInfoAlert()
    {
        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, Array.Empty<AttributeSchemaDto>()));

        Assert.Contains("没有可搜索的属性", cut.Markup);
    }

    [Fact]
    public void EmptyFilters_DoesNotRenderApplyButton()
    {
        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, new List<AttributeFilter>())
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("name")
            }));

        // 无 filters 时，只有「添加条件」，没有「应用查询 / 清空条件」
        Assert.Contains("添加条件", cut.Markup);
        Assert.DoesNotContain("应用查询", cut.Markup);
        Assert.DoesNotContain("清空条件", cut.Markup);
    }

    // ============================================================
    // AddFilter
    // ============================================================

    [Fact]
    public void AddFilter_AddsToFiltersCollection()
    {
        var filters = new List<AttributeFilter>();
        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        // 找「添加条件」按钮并点击
        var addBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("添加条件"));
        addBtn.Click();

        Assert.Single(filters);
        // 默认属性名与默认运算符由 catalog 决定
        Assert.Equal("name", filters[0].AttributeName);
        Assert.False(string.IsNullOrEmpty(filters[0].Operator));
    }

    [Fact]
    public void AddFilter_WhenFiltersNonEmpty_RendersApplyAndClearButtons()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" }
        };

        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") }));

        Assert.Contains("应用查询", cut.Markup);
        Assert.Contains("清空条件", cut.Markup);
    }

    // ============================================================
    // RemoveFilter
    // ============================================================

    [Fact]
    public async Task RemoveFilter_RemovesFromCollectionAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
        };
        var appliedCount = 0;

        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        // ★ 修复：用 title 属性定位，不依赖 SVG 内部字符串
        var removeBtn = cut.FindAll("button")
            .First(b => b.GetAttribute("title") == "移除"
                     || b.GetAttribute("aria-label") == "移除");
        await cut.InvokeAsync(() => removeBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // ClearAll
    // ============================================================

    [Fact]
    public async Task ClearAll_RemovesAllAndInvokesOnApply()
    {
        var filters = new List<AttributeFilter>
        {
            new() { AttributeName = "name", Operator = "eq" },
            new() { AttributeName = "name", Operator = "neq" }
        };
        var appliedCount = 0;

        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[] { MakeAttr("name") })
            .Add(x => x.OnApply, EventCallback.Factory.Create(this, () => appliedCount++)));

        var clearBtn = cut.FindAll("button")
            .First(b => b.TextContent.Contains("清空条件"));
        await cut.InvokeAsync(() => clearBtn.Click());

        Assert.Empty(filters);
        Assert.Equal(1, appliedCount);
    }

    // ============================================================
    // AddFilter 默认运算符来自 FilterOperatorCatalog
    // ============================================================

    [Fact]
    public void AddFilter_ForNumericAttribute_PicksNumericOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("price", dataType: "decimal")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("price", filters[0].AttributeName);
        // 数值属性首个运算符是 eq（catalog 里定义的第一项）
        Assert.Equal("eq", filters[0].Operator);
    }

    [Fact]
    public void AddFilter_ForBoolAttribute_PicksBoolOperator()
    {
        var filters = new List<AttributeFilter>();
        var cut = Render<QueryFilterBuilder>(p => p
            .Add(x => x.Filters, filters)
            .Add(x => x.SupportedAttributes, new[]
            {
                MakeAttr("active", dataType: "bool")
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("添加条件")).Click();

        Assert.Single(filters);
        Assert.Equal("eq", filters[0].Operator);
    }
}
