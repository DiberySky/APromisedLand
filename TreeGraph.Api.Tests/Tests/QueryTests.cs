using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEav.Dtos;
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
            .ReadFromJsonAsync<PagedResult<DynamicEntityDto>>();
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
}
