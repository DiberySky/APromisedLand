using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// relation 属性元数据（R6）：relation_type 显式指定 / 驼峰回退 / 自动名拒绝 /
/// 字符集与保留名校验 / 非 relation 填写拒绝 / 创建后不可修改 / schema 透出。
/// </summary>
public class RelationMetadataTests : IntegrationTestBase
{
    private const string Type = "relmeta";

    public RelationMetadataTests(EavApiFactory factory) : base(factory) { }

    private sealed record CreatedDto(string AttributeId, string AttributeName);

    private sealed record AttrDto(
        string AttributeId,
        string AttributeName,
        string DataType,
        bool IsSearchable,
        bool IsSortable,
        string? RelationType);

    private async Task EnsureEntityTypeAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/eav/entity-types",
            new { entityType = Type, displayName = "关系元数据测试" });
        if (resp.StatusCode != HttpStatusCode.Conflict)
            resp.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> CreateAttributeAsync(
        string? attributeName,
        string? relationType,
        bool isSearchable = true,
        bool isSortable = true,
        string dataType = "relation")
        => await Client.PostAsJsonAsync("/api/eav/metadata/attributes", new
        {
            entityType = Type,
            attributeName,
            displayName = "关系",
            dataType,
            relationType,
            isSearchable,
            isSortable
        });

    private async Task<AttrDto> GetAttributeAsync(string id)
    {
        var list = await Client.GetEavAsync<List<AttrDto>>(
            $"/api/eav/metadata/attributes?entityType={Type}");
        Assert.NotNull(list);
        return Assert.Single(list!, a => a.AttributeId == id);
    }

    [Fact]
    public async Task Create_ExplicitRelationType_IsNormalizedToLowercase()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync("rel_explicit", "BELONGS_TO");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var created = await resp.Content.ReadEavAsync<CreatedDto>();
        Assert.NotNull(created);

        var dto = await GetAttributeAsync(created!.AttributeId);
        Assert.Equal("belongs_to", dto.RelationType);

        // relation 不支持检索 / 排序：请求为 true 也落库 false
        Assert.False(dto.IsSearchable);
        Assert.False(dto.IsSortable);
    }

    [Fact]
    public async Task Create_CamelCaseName_DerivesSnakeCaseRelationType()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync("UserDept", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var created = await resp.Content.ReadEavAsync<CreatedDto>();
        Assert.Equal("user_dept",
            (await GetAttributeAsync(created!.AttributeId)).RelationType);
    }

    [Fact]
    public async Task Create_ExplicitRelationType_WinsOverNameDerivation()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync("DeptRef", "member_of");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var created = await resp.Content.ReadEavAsync<CreatedDto>();
        Assert.Equal("member_of",
            (await GetAttributeAsync(created!.AttributeId)).RelationType);
    }

    [Fact]
    public async Task Create_AutoAttributeName_WithoutRelationType_Returns400()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync(null, null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("relation_type", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_AutoAttributeName_WithExplicitRelationType_Succeeds()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync(null, "auto_named_edge");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var created = await resp.Content.ReadEavAsync<CreatedDto>();
        Assert.StartsWith("attr_", created!.AttributeName);
        Assert.Equal("auto_named_edge",
            (await GetAttributeAsync(created.AttributeId)).RelationType);
    }

    [Fact]
    public async Task Create_NonRelationWithRelationType_Returns400()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync("plain_text", "belongs_to", dataType: "string");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("relation", await resp.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("bad-type")]
    [InlineData("_lead")]
    [InlineData("1abc")]
    [InlineData("user dept")]
    [InlineData("用户关系")]
    [InlineData("parent_of")]
    [InlineData("connects_to")]
    [InlineData("line_segment")]
    public async Task Create_InvalidOrReservedRelationType_Returns400(string relationType)
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync($"bad_{Guid.NewGuid():N}", relationType);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("relation_type", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_RelationTypeOver100Chars_Returns400()
    {
        await EnsureEntityTypeAsync();

        var resp = await CreateAttributeAsync("too_long", new string('a', 101));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("100", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_RelationType_CannotBeChanged()
    {
        await EnsureEntityTypeAsync();

        var created = await (await CreateAttributeAsync("rel_immutable", "belongs_to"))
            .Content.ReadEavAsync<CreatedDto>();
        Assert.NotNull(created);

        // UpdateAttributeRequest 无 relationType 字段：传入被忽略，值保持不变
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/attributes/{created!.AttributeId}",
            new { displayName = "改名", relationType = "other_edge" });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var read = await GetAttributeAsync(created.AttributeId);
        Assert.Equal("belongs_to", read.RelationType);
        Assert.Equal("改名", await GetDisplayNameAsync(created.AttributeId));
    }

    private async Task<string?> GetDisplayNameAsync(string id)
    {
        var list = await Client.GetEavAsync<List<JsonElement>>(
            $"/api/eav/metadata/attributes?entityType={Type}");
        var item = Assert.Single(list!, a => a.GetProperty("attributeId").GetString() == id);
        return item.GetProperty("displayName").GetString();
    }

    [Fact]
    public async Task Schema_ExposesRelationType()
    {
        await EnsureEntityTypeAsync();

        var created = await (await CreateAttributeAsync("rel_schema", "connects_edge"))
            .Content.ReadEavAsync<CreatedDto>();
        Assert.NotNull(created);

        var resp = await Client.GetAsync($"/api/eav/{Type}/schema");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var attr = Assert.Single(
            doc.RootElement.GetProperty("data").GetProperty("attributes").EnumerateArray(),
            a => a.GetProperty("attributeName").GetString() == "rel_schema");
        Assert.Equal("connects_edge", attr.GetProperty("relationType").GetString());
    }
}
