using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.Blazor.Shared.NodeEavSky.Services;
using TreeGraph.Blazor.Shared.StringTreeSky;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.Tests.NodeEavSky;
using TreeGraph.Shared.StringTreeSky.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.StringTreeSky;

/// <summary>
/// NodePropertySummaryService 单元测试：
/// 启用开关、类型格式化、顺序、截断、缓存失效、404 降级、批量去重、多树 (EntityType) 隔离。
/// </summary>
public class NodePropertySummaryServiceTests
{
    private const string Type = StringTreeEntityTypes.Node;
    private const string ProductsType = "StringTreeNode:products";
    private const string NodeId = "11111111-2222-3333-4444-555555555555";
    private const string NodeId2 = "22222222-3333-4444-5555-666666666666";

    private static string SchemaJson() =>
        NodeEavTestSetup.Json.Envelope(new
        {
            entityType = "StringTreeNode",
            attributes = new object[]
            {
                Attr("brand", "品牌", "string"),
                Attr("stock", "库存", "int"),
                Attr("price", "价格", "decimal"),
                Attr("active", "启用", "bool"),
                Attr("launchedAt", "上市时间", "datetime"),
                Attr("grade", "等级", "single_choice"),
                Attr("complex", "组合", "composite"),
                Attr("detailTable", "明细表", "table")
            }
        });

    private static object Attr(string name, string display, string type) => new
    {
        attributeName = name,
        displayName = display,
        dataType = type,
        isRequired = false,
        isSearchable = false,
        isSortable = false,
        displayOrder = 0
    };

    private static string EntityJson(string id, Dictionary<string, object?> props) =>
        NodeEavTestSetup.Json.Envelope(new
        {
            entityId = id,
            entityType = "StringTreeNode",
            properties = props,
            updatedAt = (DateTimeOffset?)null
        });

    [Fact]
    public async Task Disabled_ReturnsNullWithoutHttp()
    {
        var (service, count) = BuildWithCounter(
            (_, _) => (HttpStatusCode.OK, SchemaJson()),
            names: null);

        var summary = await service.GetSummaryAsync(Type, NodeId);

        Assert.Null(summary);
        Assert.Equal(0, count());
    }

    [Fact]
    public async Task Enabled_FormatsSupportedTypesAndSkipsComplex()
    {
        var (service, _) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, EntityJson(NodeId, new()
                    {
                        ["brand"] = "华为",
                        ["stock"] = 100,
                        ["price"] = 4999.5m,
                        ["active"] = true,
                        // 不带时区偏移：TryGetDateTime 得到 Unspecified Kind，
                        // 格式结果不随运行机器时区漂移
                        ["launchedAt"] = "2026-05-01T08:30:00",
                        ["grade"] = new { value = "A" },
                        ["complex"] = new { nested = 1 },
                        ["detailTable"] = new[] { new { c = 1 } }
                    }));
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand", "stock", "price", "active", "launchedAt", "grade",
                    "complex", "detailTable" },
            maxLength: 0);

        var summary = await service.GetSummaryAsync(Type, NodeId);

        Assert.Equal(
            "品牌: 华为 · 库存: 100 · 价格: 4999.5 · 启用: 是 · " +
            "上市时间: 2026-05-01 08:30 · 等级: A",
            summary);
    }

    [Fact]
    public async Task Enabled_RespectsConfiguredOrder()
    {
        var (service, _) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                return (HttpStatusCode.OK, EntityJson(NodeId, new()
                {
                    ["brand"] = "华为",
                    ["price"] = 4999
                }));
            },
            new() { "price", "brand" });

        var summary = await service.GetSummaryAsync(Type, NodeId);

        Assert.Equal("价格: 4999 · 品牌: 华为", summary);
    }

    [Fact]
    public async Task Truncation_AppendsEllipsis_ZeroMeansUnlimited()
    {
        static string LongProps() =>
            EntityJson(NodeId, new() { ["brand"] = "华为技术有限公司超长品牌名称" });

        var (truncated, _) = BuildWithCounter(
            (path, _) => path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, SchemaJson())
                : (HttpStatusCode.OK, LongProps()),
            new() { "brand" },
            maxLength: 4);

        var shortSummary = await truncated.GetSummaryAsync(Type, NodeId);

        Assert.NotNull(shortSummary);
        Assert.Equal(5, shortSummary!.Length);
        Assert.EndsWith("…", shortSummary, StringComparison.Ordinal);
        Assert.Equal("品牌: …", shortSummary);

        var (unlimited, _) = BuildWithCounter(
            (path, _) => path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, SchemaJson())
                : (HttpStatusCode.OK, LongProps()),
            new() { "brand" },
            maxLength: 0);

        var full = await unlimited.GetSummaryAsync(Type, NodeId);

        Assert.DoesNotContain("…", full, StringComparison.Ordinal);
        Assert.Equal("品牌: 华为技术有限公司超长品牌名称", full);
    }

    [Fact]
    public async Task Invalidate_RefetchesAndReturnsNewValue()
    {
        var saved = false;
        var (service, count) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK,
                        EntityJson(NodeId, new() { ["brand"] = saved ? "新值" : "旧值" }));
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand" });

        Assert.Equal("品牌: 旧值", await service.GetSummaryAsync(Type, NodeId));

        saved = true;
        // 缓存仍在 → 旧值
        Assert.Equal("品牌: 旧值", await service.GetSummaryAsync(Type, NodeId));

        service.Invalidate(Type, NodeId);
        Assert.Equal("品牌: 新值", await service.GetSummaryAsync(Type, NodeId));

        // schema 1 次；实体失效前后共 2 次
        Assert.Equal(3, count());
    }

    [Fact]
    public async Task NotFound_ReturnsNullAndCachesResult()
    {
        var (service, count) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand" });

        Assert.Null(await service.GetSummaryAsync(Type, NodeId));
        Assert.Null(await service.GetSummaryAsync(Type, NodeId));

        // schema 1 次；实体只请求 1 次（null 也缓存）
        Assert.Equal(2, count());
    }

    [Fact]
    public async Task EmptySchema_ReturnsNull()
    {
        var (service, _) = BuildWithCounter(
            (path, _) => path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, NodeEavTestSetup.Json.Envelope(
                    new { entityType = "StringTreeNode", attributes = Array.Empty<object>() }))
                : (HttpStatusCode.OK, EntityJson(NodeId, new() { ["brand"] = "华为" })),
            new() { "brand" });

        Assert.Null(await service.GetSummaryAsync(Type, NodeId));
    }

    [Fact]
    public async Task GetSummariesAsync_DedupesIds()
    {
        var (service, count) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, EntityJson(NodeId, new() { ["brand"] = "华为" }));
                if (path.Contains($"/entities/{NodeId2}", StringComparison.Ordinal))
                    return (HttpStatusCode.NotFound, null);
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand" });

        var result = await service.GetSummariesAsync(
            Type, new[] { NodeId, NodeId, NodeId2 });

        Assert.Equal(2, result.Count);
        Assert.Equal("品牌: 华为", result[NodeId]);
        Assert.Null(result[NodeId2]);
        // schema 1 + 两个实体各 1
        Assert.Equal(3, count());
    }

    [Fact]
    public async Task SharedSchemaCache_TwoServiceInstances_FetchSchemaOnce()
    {
        var count = 0;
        var http = new HttpClient(new FakeHandler((path, _) =>
        {
            count++;
            if (path.EndsWith("/schema", StringComparison.Ordinal))
                return (HttpStatusCode.OK, SchemaJson());
            if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                return (HttpStatusCode.OK, EntityJson(NodeId, new() { ["brand"] = "华为" }));
            if (path.Contains($"/entities/{NodeId2}", StringComparison.Ordinal))
                return (HttpStatusCode.OK, EntityJson(NodeId2, new() { ["brand"] = "小米" }));
            return (HttpStatusCode.NotFound, null);
        }))
        {
            BaseAddress = new Uri("https://test/")
        };

        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);
        var cache = new NodeSchemaCache(api);
        var options = new StringTreeSkyOptions
        {
            SummaryAttributeNames = new() { "brand" },
            SummaryMaxLength = 0
        };
        var serviceA = new NodePropertySummaryService(api, options, cache);
        var serviceB = new NodePropertySummaryService(api, options, cache);

        Assert.Equal("品牌: 华为", await serviceA.GetSummaryAsync(Type, NodeId));
        Assert.Equal("品牌: 小米", await serviceB.GetSummaryAsync(Type, NodeId2));

        // 两个服务实例共享同一份 schema：只拉 1 次 schema + 2 次实体
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task InvalidateAll_ClearsCacheAndSchema()
    {
        var schemaVersion = 0;
        var (service, count) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                {
                    schemaVersion++;
                    return (HttpStatusCode.OK, SchemaJson());
                }
                if (path.Contains($"/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK,
                        EntityJson(NodeId, new() { ["brand"] = $"v{schemaVersion}" }));
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand" });

        Assert.Equal("品牌: v1", await service.GetSummaryAsync(Type, NodeId));

        service.InvalidateAll();

        Assert.Equal("品牌: v2", await service.GetSummaryAsync(Type, NodeId));
        // schema 2 次，实体 2 次
        Assert.Equal(4, count());
    }

    [Fact]
    public async Task DifferentEntityTypes_SameNode_SummariesAreIndependent()
    {
        var (service, count) = BuildWithCounter(
            (path, _) =>
            {
                if (path.EndsWith("/schema", StringComparison.Ordinal))
                    return (HttpStatusCode.OK, SchemaJson());
                if (path.Contains($"/{ProductsType}/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK,
                        EntityJson(NodeId, new() { ["brand"] = "小米" }));
                if (path.Contains($"/{Type}/entities/{NodeId}", StringComparison.Ordinal))
                    return (HttpStatusCode.OK,
                        EntityJson(NodeId, new() { ["brand"] = "华为" }));
                return (HttpStatusCode.NotFound, null);
            },
            new() { "brand" });

        var defaultSummary = await service.GetSummaryAsync(Type, NodeId);
        var productsSummary = await service.GetSummariesAsync(
            ProductsType, new[] { NodeId });

        Assert.Equal("品牌: 华为", defaultSummary);
        Assert.Equal("品牌: 小米", productsSummary[NodeId]);

        // 默认树二次命中不发请求
        Assert.Equal("品牌: 华为", await service.GetSummaryAsync(Type, NodeId));

        // 失效 products 的单节点不影响默认树缓存
        service.Invalidate(ProductsType, NodeId);
        Assert.Equal("品牌: 华为", await service.GetSummaryAsync(Type, NodeId));

        // 至此：2 schema + 2 entity；失效 products 后查的是默认树（命中缓存），
        // products 摘要失效本身不触发请求
        Assert.Equal(4, count());

        // InvalidateEntityType：清掉 products 全部摘要（schema 仍在共享缓存），
        // 重取 products 只需再拉 1 次实体
        service.InvalidateEntityType(ProductsType);
        Assert.Equal("品牌: 小米",
            (await service.GetSummariesAsync(ProductsType, new[] { NodeId }))[NodeId]);
        Assert.Equal(5, count());
    }

    // ============================================================
    // 带请求计数的构造助手
    // ============================================================

    private static (NodePropertySummaryService Service, Func<int> Count) BuildWithCounter(
        Func<string, HttpMethod, (HttpStatusCode Code, string? Body)> route,
        List<string>? names,
        int maxLength = 60)
    {
        var count = 0;
        var http = new HttpClient(new FakeHandler((path, method) =>
        {
            count++;
            return route(path, method);
        }))
        {
            BaseAddress = new Uri("https://test/")
        };

        var options = new StringTreeSkyOptions
        {
            DefaultExpandLevel = 0,
            SummaryMaxLength = maxLength
        };
        if (names is not null) options.SummaryAttributeNames = names;

        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);
        var cache = new NodeSchemaCache(api);
        return (new NodePropertySummaryService(api, options, cache), () => count);
    }

    private sealed class FakeHandler(
        Func<string, HttpMethod, (HttpStatusCode Code, string? Body)> route)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var (code, body) = route(path, request.Method);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8, "application/json")
            });
        }
    }
}
