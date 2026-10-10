using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 跨 iNode 查询（/api/inode/query）集成测试。
/// </summary>
public class InodeQueryTests : IntegrationTestBase
{
    private const string EntityType = "InodeQueryTest";

    public InodeQueryTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    /// <summary>
    /// 无 inodeId 限定的查询会扫全库同类型实体，
    /// 用每次唯一的 entityType 隔离，避免同类其他用例的数据污染。
    /// </summary>
    private async Task<string> NewTypeAsync()
    {
        var t = "InodeQ" + Guid.NewGuid().ToString("N")[..8];
        await InodeTestData.EnsureTypeAsync(Client, t);
        return t;
    }

    private async Task SeedAsync(
        string entityType, string inodeId, string name, long amount)
    {
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{entityType}",
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["amount"] = amount
            });
    }

    // ============================================================
    // 基础查询
    // ============================================================

    [Fact]
    public async Task Query_ByInodeId_ReturnsOnlyThatInode()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(EntityType, inodeA, "A-item", 10L);
        await SeedAsync(EntityType, inodeB, "B-item", 20L);

        var req = new InodeQueryRequest
        {
            InodeId = inodeA,
            EntityType = EntityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(inodeA, result.Items[0].InodeId);
        Assert.Equal("A-item", result.Items[0].Properties["name"].GetString());
    }

    [Fact]
    public async Task Query_NoInodeId_ReturnsAcrossInodes()
    {
        var entityType = await NewTypeAsync();

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(entityType, inodeA, "A-item", 10L);
        await SeedAsync(entityType, inodeB, "B-item", 20L);

        var req = new InodeQueryRequest
        {
            InodeId = null,       // 跨 iNode
            EntityType = entityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Items.Count);

        // 每条都带正确的 inodeId
        Assert.Contains(result.Items, x =>
            x.InodeId == inodeA && x.Properties["name"].GetString() == "A-item");
        Assert.Contains(result.Items, x =>
            x.InodeId == inodeB && x.Properties["name"].GetString() == "B-item");
    }

    // ============================================================
    // 属性过滤 + iNode 限定
    // ============================================================

    [Fact]
    public async Task Query_WithAttributeFilter_ReturnsMatches()
    {
        var entityType = await NewTypeAsync();

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(entityType, inodeA, "small", 10L);
        await SeedAsync(entityType, inodeB, "large", 100L);

        var req = new InodeQueryRequest
        {
            EntityType = entityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "amount",
                    Operator = "gt",
                    Value = 50L
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal("large", result.Items[0].Properties["name"].GetString());
    }

    [Fact]
    public async Task Query_WithInodeAndFilter_Intersects()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        await SeedAsync(EntityType, inodeA, "A-large", 100L);
        await SeedAsync(EntityType, inodeB, "B-large", 200L);

        var req = new InodeQueryRequest
        {
            InodeId = inodeA,
            EntityType = EntityType,
            Filters = new List<AttributeFilter>
            {
                new()
                {
                    AttributeName = "amount",
                    Operator = "gt",
                    Value = 50L
                }
            },
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Equal(inodeA, result.Items[0].InodeId);
    }

    // ============================================================
    // 空结果 & 错误
    // ============================================================

    [Fact]
    public async Task Query_InodeWithoutEntity_ReturnsEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var req = new InodeQueryRequest
        {
            InodeId = inodeId,
            EntityType = EntityType,
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadEavAsync<PagedResult<InodeEntityDto>>();

        Assert.NotNull(result);
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Query_MissingEntityType_Returns400()
    {
        var req = new InodeQueryRequest
        {
            EntityType = "",       // 缺失
            Page = 1,
            PageSize = 50
        };

        var resp = await Client.PostAsJsonAsync("/api/inode/query", req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
