using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.Tests.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.StringTreeSky.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.StringTreeSky;

// 当前命名空间向上查找时 StringTreeSky 命中命名空间而非组件类型（CS0118），
// 别名放在命名空间内部优先于外层命名空间成员。
using StringTreeSky = TreeGraph.Blazor.Shared.StringTreeSky.StringTreeSky;

/// <summary>
/// 增强项 5/5 属性过滤测试：
///   A. StringTreeFilterPanel 面板单元测试（不测 MudSelect 下拉交互，与仓库既有约定一致，
///      运算符切换通过直接触发 MudSelect.ValueChanged 模拟）；
///   B. StringTreeSky 过滤态端到端：查询 → 祖先链 → 匹配+祖先渲染、无匹配、清除恢复。
/// </summary>
public class StringTreeFilterPanelTests : BunitTestBase
{
    public StringTreeFilterPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    private static AttributeSchemaDto Attr(
        string name, string display, string dataType)
        => new(
            AttributeName: name,
            DisplayName: display,
            DataType: dataType,
            IsRequired: false,
            IsSearchable: false,
            IsSortable: false,
            DisplayOrder: 0,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: null);

    private static bool IsDisabled(AngleSharp.Dom.IElement button)
        => button.HasAttribute("disabled")
           || button.GetAttribute("aria-disabled") == "true"
           || button.ClassList.Any(c => c.Contains("disabled", StringComparison.Ordinal));

    private sealed class PanelContext
    {
        public List<AttributeFilter> Applied { get; set; } = new();
        public int Cleared { get; set; }
    }

    private (IRenderedComponent<StringTreeFilterPanel> Cut, PanelContext Ctx) RenderPanel(
        IReadOnlyList<AttributeSchemaDto> schema)
    {
        var ctx = new PanelContext();
        var cut = Render<StringTreeFilterPanel>(p => p
            .Add(x => x.Schema, schema)
            .Add(x => x.OnApply,
                EventCallback.Factory.Create<List<AttributeFilter>>(ctx, f => ctx.Applied = f.ToList()))
            .Add(x => x.OnClear,
                EventCallback.Factory.Create(ctx, () => ctx.Cleared++)));
        return (cut, ctx);
    }

    [Fact]
    public void EmptyState_ShowsHintAndDisablesApplyClear()
    {
        var (cut, _) = RenderPanel(new[] { Attr("brand", "品牌", "string") });

        Assert.Contains("尚未添加过滤条件", cut.Markup, StringComparison.Ordinal);
        var buttons = cut.FindAll("button");
        Assert.True(IsDisabled(buttons.First(b => b.TextContent.Trim() == "应用")));
        Assert.True(IsDisabled(buttons.First(b => b.TextContent.Trim() == "清除")));
        Assert.False(IsDisabled(buttons.First(b => b.TextContent.Trim() == "添加条件")));
    }

    [Fact]
    public void AddAndApply_DefaultsToFirstSupportedAttributeAndMapsValue()
    {
        var (cut, ctx) = RenderPanel(
            new[]
            {
                // file 不支持动态查询，应被跳过；首项可选属性是 brand
                Attr("attachment", "附件", "file"),
                Attr("brand", "品牌", "string")
            });

        cut.FindAll("button").First(b => b.TextContent.Trim() == "添加条件").Click();

        var valueInput = cut.Find("input.mud-input-slot:not(.mud-select-input)");
        valueInput.Change("华为");

        cut.FindAll("button").First(b => b.TextContent.Trim() == "应用").Click();

        var applied = ctx.Applied;
        Assert.Single(applied);
        Assert.Equal("brand", applied[0].AttributeName);
        Assert.Equal("eq", applied[0].Operator);
        Assert.Equal("华为", applied[0].Value as string);
        Assert.Null(applied[0].Value2);
    }

    [Fact]
    public async Task Between_ShowsSecondValueAndMapsBoth()
    {
        var (cut, ctx) = RenderPanel(
            new[] { Attr("price", "价格", "decimal") });

        cut.FindAll("button").First(b => b.TextContent.Trim() == "添加条件").Click();
        Assert.Single(cut.FindAll("input.mud-input-slot:not(.mud-select-input)"));

        // 模拟运算符下拉选择 between（不依赖 JS 弹出层）
        var opSelect = cut.FindComponents<MudSelect<string>>()
            .First(s => s.Instance.Label == "运算符");
        await cut.InvokeAsync(() => opSelect.Instance.ValueChanged.InvokeAsync("between"));

        var inputs = cut.FindAll("input.mud-input-slot:not(.mud-select-input)");
        Assert.Equal(2, inputs.Count);
        inputs[0].Change("100");
        inputs[1].Change("500");

        cut.FindAll("button").First(b => b.TextContent.Trim() == "应用").Click();

        var applied = ctx.Applied;
        Assert.Single(applied);
        Assert.Equal("between", applied[0].Operator);
        Assert.Equal("100", applied[0].Value as string);
        Assert.Equal("500", applied[0].Value2 as string);
    }

    [Fact]
    public void Remove_DropsRow()
    {
        var (cut, _) = RenderPanel(new[] { Attr("brand", "品牌", "string") });

        cut.FindAll("button").First(b => b.TextContent.Trim() == "添加条件").Click();
        Assert.Single(cut.FindAll("input.mud-input-slot:not(.mud-select-input)"));

        cut.Find("button.mud-icon-button").Click();

        Assert.Empty(cut.FindAll("input.mud-input-slot:not(.mud-select-input)"));
        Assert.Contains("尚未添加过滤条件", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_RemovesRowsAndInvokesOnClear()
    {
        var (cut, ctx) = RenderPanel(new[] { Attr("brand", "品牌", "string") });

        cut.FindAll("button").First(b => b.TextContent.Trim() == "添加条件").Click();
        cut.FindAll("button").First(b => b.TextContent.Trim() == "清除").Click();

        Assert.Equal(1, ctx.Cleared);
        Assert.Empty(cut.FindAll("input.mud-input-slot:not(.mud-select-input)"));
        Assert.Contains("尚未添加过滤条件", cut.Markup, StringComparison.Ordinal);
    }
}

/// <summary>StringTreeSky 过滤态集成测试（真实 EavApiClient + 内存树桩）。</summary>
public class StringTreeFilterTests : BunitTestBase
{
    private const string RootId = "11111111-2222-3333-4444-555555555555";
    private const string ChildId = "22222222-3333-4444-5555-666666666666";

    private readonly InMemoryTree _tree = new();
    private readonly List<string> _paths = new();

    public StringTreeFilterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IStringTreeClient>(_tree);
    }

    private IRenderedComponent<StringTreeSky> RenderSky(
        bool allowFilter, string queryBody)
    {
        Services.AddSingleton(new StringTreeSkyOptions
        {
            DefaultExpandLevel = 0,
            AllowFilter = allowFilter
        });

        NodeEavTestSetup.RegisterServices(Services, (path, method) =>
        {
            _paths.Add(path);
            if (path.EndsWith("/schema", StringComparison.Ordinal))
                return SchemaJson();
            if (path.EndsWith("/entities/query", StringComparison.Ordinal))
                return queryBody;
            return null;
        });
        Services.AddScoped<NodeSchemaCache>();
        Services.AddScoped<NodePropertySummaryService>();

        return Render<StringTreeSky>();
    }

    private static string SchemaJson() =>
        NodeEavTestSetup.Json.Serialize(new
        {
            entityType = "StringTreeNode",
            attributes = new object[]
            {
                new
                {
                    attributeName = "brand",
                    displayName = "品牌",
                    dataType = "string",
                    isRequired = false,
                    isSearchable = false,
                    isSortable = false,
                    displayOrder = 0
                },
                new
                {
                    attributeName = "price",
                    displayName = "价格",
                    dataType = "decimal",
                    isRequired = false,
                    isSearchable = false,
                    isSortable = false,
                    displayOrder = 1
                }
            }
        });

    private static string QueryJson(bool match) =>
        NodeEavTestSetup.Json.Serialize(new
        {
            items = match
                ? new[]
                {
                    new
                    {
                        entityId = ChildId,
                        entityType = "StringTreeNode",
                        properties = new Dictionary<string, object?> { ["brand"] = "华为" },
                        updatedAt = (DateTimeOffset?)null
                    }
                }
                : Array.Empty<object>(),
            total = match ? 1 : 0,
            page = 1,
            pageSize = 500
        });

    private static void AddDefaultFilterAndApply(IRenderedComponent<StringTreeSky> cut)
    {
        cut.FindAll("button").First(b => b.TextContent.Trim() == "添加条件").Click();
        cut.Find("input.mud-input-slot:not(.mud-select-input)").Change("华为");
        cut.FindAll("button").First(b => b.TextContent.Trim() == "应用").Click();
    }

    [Fact]
    public void FilterDisabled_PanelNotRenderedAndNoEavRequests()
    {
        var cut = RenderSky(allowFilter: false, QueryJson(true));

        cut.WaitForState(() => cut.Markup.Contains("电子产品", StringComparison.Ordinal));

        Assert.Empty(cut.FindAll(".string-tree-filter-panel"));
        Assert.Empty(_paths);
    }

    [Fact]
    public void ApplyFilter_ShowsMatchedWithAncestors_HidesMutations()
    {
        var cut = RenderSky(allowFilter: true, QueryJson(match: true));

        cut.WaitForState(
            () => cut.Markup.Contains("添加条件", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        AddDefaultFilterAndApply(cut);

        cut.WaitForState(
            () => cut.Markup.Contains("已过滤：1 个匹配", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        // 匹配节点 + 祖先均渲染
        Assert.Contains("电子产品", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("华为手机", cut.Markup, StringComparison.Ordinal);

        var matches = cut.FindAll("li.string-tree-sky__item--match");
        Assert.Single(matches);
        Assert.Contains("华为手机", matches[0].TextContent, StringComparison.Ordinal);

        // 过滤态标记：匹配 ★、祖先 ·，懒加载折叠三角消失
        Assert.Contains("★", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("·", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("▶", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("▼", cut.Markup, StringComparison.Ordinal);

        // 增/删/改按钮隐藏，属性按钮保留
        Assert.DoesNotContain("＋子节点", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("重命名", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">删除<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("属性", cut.Markup, StringComparison.Ordinal);

        // 确实打到了查询端点
        Assert.Contains(_paths, p =>
            p.EndsWith("/api/eav/StringTreeNode/entities/query", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyFilter_NoMatch_ShowsEmptyMessage()
    {
        var cut = RenderSky(allowFilter: true, QueryJson(match: false));

        cut.WaitForState(
            () => cut.Markup.Contains("添加条件", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        AddDefaultFilterAndApply(cut);

        cut.WaitForState(
            () => cut.Markup.Contains("无匹配节点", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.Contains("已过滤：0 个匹配", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearFilter_RestoresLazyTree()
    {
        var cut = RenderSky(allowFilter: true, QueryJson(match: true));

        cut.WaitForState(
            () => cut.Markup.Contains("添加条件", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        AddDefaultFilterAndApply(cut);
        cut.WaitForState(
            () => cut.Markup.Contains("已过滤：1 个匹配", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "清除").Click();

        cut.WaitForState(
            () => !cut.Markup.Contains("已过滤", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        // 懒加载树回归：根节点折叠三角回来，过滤态的 ·/★ 消失，子节点不再可见
        Assert.Contains("▶", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("★", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".string-tree-sky__toggle--leaf"));
        Assert.Contains("电子产品", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("华为手机", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // 内存树桩
    // ============================================================

    private sealed class InMemoryTree : IStringTreeClient
    {
        private readonly StringNodeDto _root = new()
        {
            Id = RootId,
            Name = "电子产品",
            ParentId = null,
            HasChildren = true,
            SortOrder = 0
        };

        private readonly StringNodeDto _child = new()
        {
            Id = ChildId,
            Name = "华为手机",
            ParentId = RootId,
            HasChildren = false,
            SortOrder = 0
        };

        public Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default)
            => Task.FromResult(new List<StringNodeDto> { _root });

        public Task<List<StringNodeDto>> GetChildrenAsync(
            string parentId, CancellationToken ct = default)
            => Task.FromResult(
                parentId == RootId
                    ? new List<StringNodeDto> { _child }
                    : new List<StringNodeDto>());

        public Task<StringNodeDto?> GetNodeAsync(
            string id, CancellationToken ct = default)
            => Task.FromResult<StringNodeDto?>(
                id == RootId ? _root : id == ChildId ? _child : null);

        public Task<List<StringNodeDto>> GetAncestorPathAsync(
            string id, CancellationToken ct = default)
            => Task.FromResult(
                id == ChildId
                    ? new List<StringNodeDto> { _root, _child }
                    : new List<StringNodeDto>());

        public Task<StringNodeDto> CreateNodeAsync(
            StringNodeDto dto, CancellationToken ct = default)
            => Task.FromResult(dto);

        public Task<StringNodeDto?> UpdateNodeAsync(
            StringNodeDto dto, CancellationToken ct = default)
            => Task.FromResult<StringNodeDto?>(dto);

        public Task<bool> DeleteNodeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> MoveNodeAsync(
            string id, string? newParentId, int newSortOrder,
            CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> SortChildrenAsync(
            string parentId, IReadOnlyList<string> orderedIds,
            CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
