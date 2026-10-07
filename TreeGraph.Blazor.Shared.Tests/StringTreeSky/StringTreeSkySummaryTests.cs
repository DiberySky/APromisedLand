using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Shared.Platform;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.Tests.NodeEav;
using TreeGraph.Shared.StringTreeSky.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.StringTreeSky;

// 当前命名空间向上查找时 StringTreeSky 命中命名空间而非组件类型（CS0118），
// 别名放在命名空间内部优先于外层命名空间成员。
using StringTreeSky = TreeGraph.Blazor.Shared.StringTreeSky.StringTreeSky;

/// <summary>
/// StringTreeSky 节点属性摘要（增强项 2/5）组件测试：
///   1. 默认旁路：未配置属性名时零 EAV 请求、不渲染摘要；
///   2. 配置后按 schema 渲染格式化摘要；
///   3. 属性对话框保存成功后失效缓存并刷新摘要。
/// 真实 EavApiClient + 按路由 canned JSON 的桩 Handler。
/// </summary>
public class StringTreeSkySummaryTests : BunitTestBase
{
    private const string NodeId = "11111111-2222-3333-4444-555555555555";

    private readonly StubTreeClient _tree = new();
    private int _eavCalls;

    public StringTreeSkySummaryTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IStringTreeClient>(_tree);
        _tree.Roots.Add(new StringNodeDto { Id = NodeId, Name = "电子产品" });
    }

    private void RegisterEav(
        StringTreeSkyOptions options,
        Func<string, HttpMethod, string?> route)
    {
        Services.AddSingleton(options);

        // DynamicForm 字段渲染器所需依赖（同 NodeEavTestSetup.RegisterServices）
        Services.AddSingleton<IPlatformContext>(new FakePlatformContext());
        Services.AddScoped<EntityTypeDisplayService>();
        Services.AddScoped<IEavFieldValidator, EavFieldValidator>();

        var http = new HttpClient(new FakeHandler((path, method) =>
        {
            _eavCalls++;
            return route(path, method);
        }))
        {
            BaseAddress = new Uri("https://test/")
        };
        Services.AddSingleton(new EavApiClient(
            http, NullLogger<EavApiClient>.Instance));
        Services.AddScoped<NodeSchemaCache>();
        Services.AddScoped<NodePropertySummaryService>();
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

    private static string EntityJson(string? brand, object? price = null)
    {
        var props = new Dictionary<string, object?>();
        if (brand is not null) props["brand"] = brand;
        if (price is not null) props["price"] = price;

        return NodeEavTestSetup.Json.Serialize(new
        {
            entityId = NodeId,
            entityType = "StringTreeNode",
            properties = props,
            updatedAt = (DateTimeOffset?)null
        });
    }

    // ============================================================
    // 1. 默认旁路
    // ============================================================

    [Fact]
    public void Disabled_NoSummaryAndNoEavRequests()
    {
        RegisterEav(
            new StringTreeSkyOptions { DefaultExpandLevel = 0 },
            (_, _) => null);

        var cut = Render<StringTreeSky>();

        Assert.Contains("电子产品", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".string-tree-sky__summary"));
        Assert.Equal(0, _eavCalls);
    }

    // ============================================================
    // 2. 配置后渲染格式化摘要
    // ============================================================

    [Fact]
    public void Enabled_RendersFormattedSummary()
    {
        var options = new StringTreeSkyOptions
        {
            DefaultExpandLevel = 0,
            SummaryAttributeNames = new() { "brand", "price" }
        };
        RegisterEav(options, (path, method) =>
        {
            if (path.EndsWith("/schema", StringComparison.Ordinal))
                return SchemaJson();
            if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                return EntityJson("华为", 4999);
            return null;
        });

        var cut = Render<StringTreeSky>();

        cut.WaitForState(
            () => cut.Markup.Contains("品牌: 华为 · 价格: 4999", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
    }

    // ============================================================
    // 2b. TreeKey → 派生独立 EntityType（增强项 4/5）
    // ============================================================

    [Fact]
    public void TreeKey_UsesDedicatedEntityTypePaths()
    {
        var paths = new List<string>();
        RegisterEav(
            new StringTreeSkyOptions
            {
                DefaultExpandLevel = 0,
                SummaryAttributeNames = new() { "brand" }
            },
            (path, method) =>
            {
                paths.Add(path);
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return SchemaJson();
                if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                    return EntityJson("华为");
                return null;
            });

        var cut = Render<StringTreeSky>(p => p.Add(x => x.TreeKey, "products"));

        cut.WaitForState(
            () => cut.Markup.Contains("品牌: 华为", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        Assert.Contains("/api/eav/StringTreeNode:products/schema", paths);
        Assert.Contains(paths, p =>
            p.StartsWith("/api/eav/StringTreeNode:products/entities/", StringComparison.Ordinal));
        // 默认 EntityType 的 schema/实体端点均不应被触及
        Assert.DoesNotContain(paths, p =>
            p.StartsWith("/api/eav/StringTreeNode/schema", StringComparison.Ordinal)
            || p.StartsWith("/api/eav/StringTreeNode/entities/", StringComparison.Ordinal));
    }

    // ============================================================
    // 3. 对话框保存成功后刷新摘要
    // ============================================================

    [Fact]
    public void SaveProperties_RefreshesSummary()
    {
        var options = new StringTreeSkyOptions
        {
            DefaultExpandLevel = 0,
            SummaryAttributeNames = new() { "brand" }
        };

        var saved = false;
        RegisterEav(options, (path, method) =>
        {
            // DynamicForm 保存：PUT 成功后切换 canned 实体值
            if (method == HttpMethod.Put
                && path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
            {
                saved = true;
                return "{}";
            }
            if (path.EndsWith("/schema", StringComparison.Ordinal))
                return SchemaJson();
            if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                return EntityJson(saved ? "新值" : "旧值");
            return null;
        });

        var provider = Render<MudDialogProvider>();
        var cut = Render<StringTreeSky>();

        cut.WaitForState(
            () => cut.Markup.Contains("品牌: 旧值", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        // 打开属性对话框并触发表单保存（DynamicForm 的“保存”按钮）
        cut.FindAll("button")
            .First(b => b.TextContent.Contains("属性", StringComparison.Ordinal))
            .Click();

        provider.WaitForState(
            () => provider.Markup.Contains("保存", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));

        provider.FindAll("button")
            .First(b => b.TextContent.Trim() == "保存")
            .Click();

        // 对话框关闭
        provider.WaitForState(
            () => !provider.Markup.Contains("mud-dialog", StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(2));

        // 摘要失效重取为新值
        cut.WaitForState(
            () => cut.Markup.Contains("品牌: 新值", StringComparison.Ordinal),
            TimeSpan.FromSeconds(2));
        Assert.DoesNotContain("品牌: 旧值", cut.Markup, StringComparison.Ordinal);
    }

    // ============================================================
    // Doubles
    // ============================================================

    private sealed class StubTreeClient : IStringTreeClient
    {
        public List<StringNodeDto> Roots { get; } = new();

        public Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default)
            => Task.FromResult(Roots.ToList());

        public Task<List<StringNodeDto>> GetChildrenAsync(
            string parentId, CancellationToken ct = default)
            => Task.FromResult(new List<StringNodeDto>());

        public Task<StringNodeDto?> GetNodeAsync(
            string id, CancellationToken ct = default)
            => Task.FromResult<StringNodeDto?>(null);

        public Task<List<StringNodeDto>> GetAncestorPathAsync(
            string id, CancellationToken ct = default)
            => Task.FromResult(new List<StringNodeDto>());

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

    private sealed class FakeHandler(
        Func<string, HttpMethod, string?> route) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var body = route(path, request.Method);
            return Task.FromResult(new HttpResponseMessage(
                body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(
                    body ?? "", Encoding.UTF8, "application/json")
            });
        }
    }
}
