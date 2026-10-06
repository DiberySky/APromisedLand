using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TreeGraph.Blazor.Shared.NodeEav.Services;
using TreeGraph.Blazor.Shared.StringTreeSky.Services;
using TreeGraph.Blazor.Shared.Tests.NodeEav;
using TreeGraph.StringTree.Contracts;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.StringTreeSky;

/// <summary>
/// NodeSchemaCache（增强项 3/4）单元测试：
/// 按 EntityType 键控——首次拉取、命中缓存、null 兜底、
/// Invalidate 重拉、并发单次、多 EntityType 隔离。
/// </summary>
public class NodeSchemaCacheTests
{
    private const string ProductsType = "StringTreeNode:products";

    private static string SchemaJson(string attributeName = "brand") =>
        NodeEavTestSetup.Json.Serialize(new
        {
            entityType = "StringTreeNode",
            attributes = new object[]
            {
                new
                {
                    attributeName,
                    displayName = "品牌",
                    dataType = "string",
                    isRequired = false,
                    isSearchable = false,
                    isSortable = false,
                    displayOrder = 0
                }
            }
        });

    private static (NodeSchemaCache Cache, Func<int> Count) Build(
        Func<string, (HttpStatusCode Code, string? Body)> route)
    {
        var count = 0;
        var http = new HttpClient(new FakeHandler(path =>
        {
            count++;
            return route(path);
        }))
        {
            BaseAddress = new Uri("https://test/")
        };

        var api = new EavApiClient(http, NullLogger<EavApiClient>.Instance);
        return (new NodeSchemaCache(api), () => count);
    }

    [Fact]
    public async Task GetAsync_FetchesOnce_ThenServesFromCache()
    {
        var (cache, count) = Build(path =>
            path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, SchemaJson())
                : (HttpStatusCode.NotFound, null));

        var first = await cache.GetAsync(StringTreeEntityTypes.Node);
        var second = await cache.GetAsync(StringTreeEntityTypes.Node);

        Assert.Single(first);
        Assert.Same(first, second);
        Assert.True(cache.IsLoaded(StringTreeEntityTypes.Node));
        Assert.Equal(1, count());
    }

    [Fact]
    public async Task GetAsync_NullResponse_CachedAsEmptyList()
    {
        var (cache, count) = Build(_ => (HttpStatusCode.NotFound, null));

        var first = await cache.GetAsync(StringTreeEntityTypes.Node);
        var second = await cache.GetAsync(StringTreeEntityTypes.Node);

        Assert.Empty(first);
        Assert.Same(first, second);
        // 非 2xx 时 EavApiClient 返回 null，空列表也必须缓存以防重试风暴
        Assert.Equal(1, count());
    }

    [Fact]
    public async Task GetAsync_BlankEntityType_ReturnsEmptyWithoutRequest()
    {
        var (cache, count) = Build(_ => (HttpStatusCode.OK, SchemaJson()));

        var result = await cache.GetAsync("  ");

        Assert.Empty(result);
        Assert.False(cache.IsLoaded("  "));
        Assert.Equal(0, count());
    }

    [Fact]
    public async Task Invalidate_ForcesRefetchOnNextGet()
    {
        var version = 0;
        var (cache, count) = Build(path =>
        {
            if (!path.EndsWith("/schema", StringComparison.Ordinal))
                return (HttpStatusCode.NotFound, null);

            version++;
            var body = NodeEavTestSetup.Json.Serialize(new
            {
                entityType = "StringTreeNode",
                attributes = version == 1
                    ? Array.Empty<object>()
                    : new object[]
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
                        }
                    }
            });
            return (HttpStatusCode.OK, body);
        });

        Assert.Empty(await cache.GetAsync(StringTreeEntityTypes.Node));
        cache.Invalidate(StringTreeEntityTypes.Node);
        Assert.False(cache.IsLoaded(StringTreeEntityTypes.Node));

        var refreshed = await cache.GetAsync(StringTreeEntityTypes.Node);
        Assert.Single(refreshed);
        Assert.Equal(2, count());
    }

    [Fact]
    public async Task ConcurrentGetAsync_FetchesSchemaOnlyOncePerEntityType()
    {
        var (cache, count) = Build(path =>
            path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, SchemaJson())
                : (HttpStatusCode.NotFound, null));

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20)
                .Select(_ => cache.GetAsync(StringTreeEntityTypes.Node)));

        Assert.Equal(20, results.Length);
        Assert.All(results, r => Assert.Single(r));
        Assert.Equal(1, count());
    }

    [Fact]
    public async Task DifferentEntityTypes_AreCachedIndependently()
    {
        var (cache, count) = Build(path =>
        {
            if (!path.EndsWith("/schema", StringComparison.Ordinal))
                return (HttpStatusCode.NotFound, null);

            // 路径末段即 entityType：…/api/eav/{entityType}/schema
            var segments = path.Trim('/').Split('/');
            var entityType = segments[^2];
            var name = entityType == ProductsType ? "price" : "brand";
            return (HttpStatusCode.OK, SchemaJson(name));
        });

        var defaultSchema = await cache.GetAsync(StringTreeEntityTypes.Node);
        var productsSchema = await cache.GetAsync(ProductsType);
        var defaultAgain = await cache.GetAsync(StringTreeEntityTypes.Node);

        Assert.Single(defaultSchema);
        Assert.Single(productsSchema);
        Assert.Equal("brand", defaultSchema[0].AttributeName);
        Assert.Equal("price", productsSchema[0].AttributeName);
        Assert.Same(defaultSchema, defaultAgain);

        // 两个 EntityType 各拉一次，默认树二次命中不重复请求
        Assert.Equal(2, count());
        Assert.True(cache.IsLoaded(StringTreeEntityTypes.Node));
        Assert.True(cache.IsLoaded(ProductsType));

        // 失效 products 不影响默认树
        cache.Invalidate(ProductsType);
        Assert.False(cache.IsLoaded(ProductsType));
        Assert.True(cache.IsLoaded(StringTreeEntityTypes.Node));

        var productsAgain = await cache.GetAsync(ProductsType);
        Assert.Equal("price", productsAgain[0].AttributeName);
        Assert.Equal(3, count());
    }

    [Fact]
    public async Task InvalidateAll_ClearsEveryEntityType()
    {
        var (cache, count) = Build(path =>
            path.EndsWith("/schema", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, SchemaJson())
                : (HttpStatusCode.NotFound, null));

        await cache.GetAsync(StringTreeEntityTypes.Node);
        await cache.GetAsync(ProductsType);
        Assert.Equal(2, count());

        cache.InvalidateAll();
        Assert.False(cache.IsLoaded(StringTreeEntityTypes.Node));
        Assert.False(cache.IsLoaded(ProductsType));

        await cache.GetAsync(StringTreeEntityTypes.Node);
        await cache.GetAsync(ProductsType);
        Assert.Equal(4, count());
    }

    private sealed class FakeHandler(
        Func<string, (HttpStatusCode Code, string? Body)> route) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var (code, body) = route(path);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8, "application/json")
            });
        }
    }
}
