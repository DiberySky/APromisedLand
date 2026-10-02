using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class ValidationTests : IntegrationTestBase
{
    public ValidationTests(EavApiFactory factory) : base(factory) { }

    /// <summary>P0-3：未知属性 → 400 + errors[] 包含拼错的键名。</summary>
    [Fact]
    public async Task Put_UnknownAttribute_Returns400WithErrorList()
    {
        await TestData.EnsureSchemaAsync(Client);

        long id = 91001;
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 1L,
            ["amoutn"] = 2L   // typo
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("amoutn", body);
    }

    /// <summary>int 属性填小数 → 400（不静默截断）。</summary>
    [Fact]
    public async Task Put_IntWithDecimal_Returns400()
    {
        await TestData.EnsureSchemaAsync(Client);

        long id = 91002;
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = 3.14m
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("int", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>table 类型属性通过 PUT entities 写入 → 400 + 引导正确端点。</summary>
    [Fact]
    public async Task Put_TableAttribute_Returns400WithGuidance()
    {
        // 依赖 EavSeeder 的 Product.certifications 属性
        var payload = new Dictionary<string, object?>
        {
            ["certifications"] = Array.Empty<object>()
        };

        var resp = await Client.PutAsJsonAsync(
            "/api/eav/Product/entities/92001", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("table", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tables/", body);
    }
}
