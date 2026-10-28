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

        var id = GuidFromInt(91001);
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

        var id = GuidFromInt(91002);
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
        await TestData.EnsureSchemaAsync(Client);

        // 先查询已有 custom table，避免重复创建（响应被 ApiEnvelope 包装）
        var listHttpResp = await Client.GetAsync(
            "/api/eav/metadata/custom-tables");
        listHttpResp.EnsureSuccessStatusCode();
        var listJson = await listHttpResp.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listJson);
        var listData = listDoc.RootElement.GetProperty("data");
        string? tableId = null;
        if (listData.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in listData.EnumerateArray())
            {
                if (t.GetProperty("tableName").GetString() == "certifications")
                {
                    tableId = t.GetProperty("tableDefinitionId").GetString();
                    break;
                }
            }
        }

        if (tableId is null)
        {
            var tableResp = await Client.PostAsJsonAsync("/api/eav/metadata/custom-tables", new
            {
                tableName = "certifications",
                displayName = "认证资质",
                displayOrder = 1
            });
            tableResp.EnsureSuccessStatusCode();
            var tableJson = await tableResp.Content.ReadAsStringAsync();
            using var tableDoc = JsonDocument.Parse(tableJson);
            tableId = tableDoc.RootElement.GetProperty("data")
                .GetProperty("tableDefinitionId").GetString()!;
        }

        var attrResp = await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = TestData.EntityType,
            attributeName = "certifications",
            displayName = "认证资质",
            dataType = "table",
            isRequired = false,
            isSearchable = false,
            isSortable = false,
            displayOrder = 1,
            refTableDefinitionId = tableId
        });
        // 409 = 已存在，视为成功
        if (attrResp.StatusCode != HttpStatusCode.Conflict)
            attrResp.EnsureSuccessStatusCode();

        var payload = new Dictionary<string, object?>
        {
            ["certifications"] = Array.Empty<object>()
        };

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/{TestData.EntityType}/entities/{GuidFromInt(92001)}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("table", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tables/", body);
    }
}
