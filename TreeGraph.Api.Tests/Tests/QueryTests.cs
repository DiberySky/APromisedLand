using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class QueryTests : IntegrationTestBase
{
    public QueryTests(EavApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Query_OrderByDecimalDesc_SortsCorrectly()
    {
        await TestData.EnsureSchemaAsync(Client);

        // 三档 price
        var ids = new[] { GuidFromInt(93001), GuidFromInt(93002), GuidFromInt(93003) };
        var prices = new[] { 10.5m, 99.9m, 5.0m };
        for (int i = 0; i < ids.Length; i++)
        {
            await PutEntityAsync(TestData.EntityType, ids[i],
                new Dictionary<string, object?>
                {
                    ["amount"] = 1L,
                    ["price"] = prices[i]
                });
        }

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            OrderByAttribute = "price",
            OrderDescending = true,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<DynamicEntityDto>>();
        Assert.NotNull(result);

        var sorted = result!.Items
            .Where(x => ids.Contains(x.EntityId))
            .Select(x =>
            {
                Assert.True(x.Properties.TryGetValue("price", out var elem));
                return elem.GetDecimal();
            })
            .ToList();

        Assert.Equal(new[] { 99.9m, 10.5m, 5.0m }, sorted);
    }

    /// <summary>decimal 的 `in` 运算符 → 200（修复前 500）。</summary>
    [Fact]
    public async Task Query_DecimalInOperator_Returns200()
    {
        await TestData.EnsureSchemaAsync(Client);

        await PutEntityAsync(TestData.EntityType, GuidFromInt(93010),
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 42m });
        await PutEntityAsync(TestData.EntityType, GuidFromInt(93011),
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 43m });

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "price",
                    Operator = "in",
                    Value = new[] { 42m, 43m }
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>不可排序属性 → 400。</summary>
    [Fact]
    public async Task Query_OrderByNonSortable_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            OrderByAttribute = "label",   // IsSortable = false
            Page = 1,
            PageSize = 10
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    /// <summary>
    /// SQL 排序（LEFT JOIN）null 键位置：ASC 时未设排序属性的实体排最前。
    /// 回归保护：内存排序 → SQL 排序重构后语义不变。
    /// </summary>
    [Fact]
    public async Task Query_OrderByAsc_NullKeysFirst()
    {
        await TestData.EnsureSchemaAsync(Client);

        var noPriceId = GuidFromInt(93020);
        var (lowId, highId) = (GuidFromInt(93021), GuidFromInt(93022));
        await PutEntityAsync(TestData.EntityType, noPriceId,
            new Dictionary<string, object?> { ["amount"] = 1L });   // 无 price
        await PutEntityAsync(TestData.EntityType, lowId,
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 5.0m });
        await PutEntityAsync(TestData.EntityType, highId,
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 99.9m });

        var result = await QueryIdsAsync(orderDescending: false);
        var mine = result.Where(id => new[] { noPriceId, lowId, highId }.Contains(id)).ToList();

        Assert.Equal(new[] { noPriceId, lowId, highId }, mine);
    }

    /// <summary>
    /// SQL 排序（LEFT JOIN）null 键位置：DESC 时未设排序属性的实体排最后。
    /// </summary>
    [Fact]
    public async Task Query_OrderByDesc_NullKeysLast()
    {
        await TestData.EnsureSchemaAsync(Client);

        var noPriceId = GuidFromInt(93023);
        var (lowId, highId) = (GuidFromInt(93024), GuidFromInt(93025));
        await PutEntityAsync(TestData.EntityType, noPriceId,
            new Dictionary<string, object?> { ["amount"] = 1L });   // 无 price
        await PutEntityAsync(TestData.EntityType, lowId,
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 5.0m });
        await PutEntityAsync(TestData.EntityType, highId,
            new Dictionary<string, object?> { ["amount"] = 1L, ["price"] = 99.9m });

        var result = await QueryIdsAsync(orderDescending: true);
        var mine = result.Where(id => new[] { noPriceId, lowId, highId }.Contains(id)).ToList();

        Assert.Equal(new[] { highId, lowId, noPriceId }, mine);
    }

    private async Task<List<string>> QueryIdsAsync(bool orderDescending)
    {
        var req = new EavQueryRequest
        {
            EntityType = TestData.EntityType,
            OrderByAttribute = "price",
            OrderDescending = orderDescending,
            Page = 1,
            PageSize = 200
        };

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<DynamicEntityDto>>();
        Assert.NotNull(result);
        return result!.Items.Select(x => x.EntityId).ToList();
    }
}
