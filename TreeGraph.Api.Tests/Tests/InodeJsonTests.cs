using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

public class InodeJsonTests : IntegrationTestBase
{
    private const string EntityType = "InodeJsonTest";

    public InodeJsonTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    [Fact]
    public async Task GetAllAsJson_BasicShape()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 写入数据
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "demo",
                ["amount"] = 42L
            });

        // 拉 JSON
        var json = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // 顶层元信息
        Assert.Equal(inodeId, root.GetProperty("inodeId").GetString());
        Assert.True(root.TryGetProperty("generatedAt", out _));

        // entities.Product
        var entity = root.GetProperty("entities")
            .GetProperty(EntityType);
        Assert.Equal("demo", entity.GetProperty("properties")
            .GetProperty("name").GetString());
        Assert.Equal(42, entity.GetProperty("properties")
            .GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task GetAllAsJson_IncludeNull_KeepsKeys()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "only-name" });

        // 默认：amount 不出现在 properties
        var jsonDefault = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");
        using var doc1 = JsonDocument.Parse(jsonDefault);
        var props1 = doc1.RootElement.GetProperty("entities")
            .GetProperty(EntityType).GetProperty("properties");
        Assert.False(props1.TryGetProperty("amount", out _));

        // includeNull=true：amount 出现且为 null
        var jsonInclude = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json?includeNull=true");
        using var doc2 = JsonDocument.Parse(jsonInclude);
        var props2 = doc2.RootElement.GetProperty("entities")
            .GetProperty(EntityType).GetProperty("properties");
        Assert.True(props2.TryGetProperty("amount", out var amount));
        Assert.Equal(JsonValueKind.Null, amount.ValueKind);
    }

    [Fact]
    public async Task GetAllAsJson_InvalidUnits_Returns400()
    {
        var inodeId = NewInodeId();
        var resp = await Client.GetAsync(
            $"/api/inode/{inodeId}/json?units=xxx");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetAllAsJson_EmptyInode_ReturnsEmptyEntities()
    {
        var inodeId = NewInodeId();
        var json = await Client.GetStringAsync(
            $"/api/inode/{inodeId}/json");

        using var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");
        Assert.Equal(JsonValueKind.Object, entities.ValueKind);
        Assert.Equal(0, entities.EnumerateObject().Count());
    }
}
