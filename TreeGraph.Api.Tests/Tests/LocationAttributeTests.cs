using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// location（位置）类型端到端：独立表 location_attribute_values 的写入 / 读取 /
/// 经纬度范围校验 / 删除，均通过 EAV 主实体端点（主链路分流）。
/// </summary>
public class LocationAttributeTests : IntegrationTestBase
{
    private const string Type = "loctest";
    private const string Attr = "pos";

    public LocationAttributeTests(EavApiFactory factory) : base(factory) { }

    /// <summary>逆地理编码：未配置高德 Key 时返回 400（前端据此静默降级）。</summary>
    [Fact]
    public async Task Regeo_WithoutAmapKey_Returns400()
    {
        var resp = await Client.GetAsync(
            "/api/eav/geo/regeo?latitude=39.9&longitude=116.4");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task EnsureLocationSchemaAsync()
    {
        var typeResp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "位置测试" });
        if (typeResp.StatusCode != HttpStatusCode.Conflict)
            typeResp.EnsureSuccessStatusCode();

        var attrResp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = Type,
            attributeName = Attr,
            displayName = "位置",
            dataType = "location",
            isRequired = false,
            isSearchable = false,
            isSortable = false
        });
        if (attrResp.StatusCode != HttpStatusCode.Conflict)
            attrResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Put_Location_ThenGet_RoundTripsCoordinate()
    {
        await EnsureLocationSchemaAsync();
        var id = GuidFromInt(92001);

        var put = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?>
            {
                [Attr] = new
                {
                    latitude = 31.2304,
                    longitude = 121.4737,
                    label = "人民广场",
                    address = "上海市黄浦区",
                    coordinateSystem = "gcj02"
                }
            });
        put.EnsureSuccessStatusCode();

        var get = await Client.GetAsync($"/api/eav/{Type}/entities/{id}");
        get.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var pos = doc.RootElement.GetProperty("data")
            .GetProperty("properties")
            .GetProperty(Attr);

        Assert.Equal(31.2304, pos.GetProperty("latitude").GetDouble(), 6);
        Assert.Equal(121.4737, pos.GetProperty("longitude").GetDouble(), 6);
        Assert.Equal("人民广场", pos.GetProperty("label").GetString());
        Assert.Equal("上海市黄浦区", pos.GetProperty("address").GetString());
        Assert.Equal("gcj02", pos.GetProperty("coordinateSystem").GetString());
    }

    [Fact]
    public async Task Put_LocationWithLatitudeOutOfRange_Returns400()
    {
        await EnsureLocationSchemaAsync();
        var id = GuidFromInt(92002);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?>
            {
                [Attr] = new { latitude = 200.0, longitude = 121.4737 }
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("纬度", body);
    }

    [Fact]
    public async Task Put_NullLocation_DeletesCoordinate()
    {
        await EnsureLocationSchemaAsync();
        var id = GuidFromInt(92003);

        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?>
            {
                [Attr] = new { latitude = 39.9042, longitude = 116.4074, label = "天安门" }
            })).EnsureSuccessStatusCode();

        // 显式置 null → 删除坐标点
        (await Client.PutAsJsonAsync(
            $"/api/eav/{Type}/entities/{id}",
            new Dictionary<string, object?> { [Attr] = null })).EnsureSuccessStatusCode();

        var get = await Client.GetAsync($"/api/eav/{Type}/entities/{id}");
        get.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var props = doc.RootElement.GetProperty("data").GetProperty("properties");
        Assert.False(props.TryGetProperty(Attr, out _));
    }
}
