using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 实体类型「共享/独占」（OwnerSpaceId）集成测试。
/// 覆盖：创建时范围持久化与空间存在性校验、
///       details 端点按 spaceId 过滤（共享 ∪ 本空间独占）。
/// </summary>
public class EntityTypeScopeTests : IntegrationTestBase
{
    public EntityTypeScopeTests(EavApiFactory factory) : base(factory) { }

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    /// <summary>创建空间（客户端预生成 Id，避免依赖响应体）。</summary>
    private async Task<string> CreateSpaceAsync()
    {
        var id = Guid.NewGuid().ToString("N");
        var resp = await Client.PostAsJsonAsync("/Space", new { id, name = NewName("空间") });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return id;
    }

    /// <summary>创建实体类型，返回 (statusCode, entityTypeId)。</summary>
    private async Task<(HttpStatusCode Status, string? TypeId)> CreateTypeAsync(
        string displayName, string? ownerSpaceId)
    {
        var resp = await Client.PostAsJsonAsync("/api/eav/entity-types", new
        {
            displayName,
            ownerSpaceId
        });
        if (resp.StatusCode != HttpStatusCode.OK)
            return (resp.StatusCode, null);

        var body = await resp.Content.ReadEavAsync<System.Text.Json.Nodes.JsonObject>();
        return (resp.StatusCode, body?["entityTypeId"]?.GetValue<string>());
    }

    private async Task<List<EntityTypeDetailDto>> GetDetailsAsync(string? spaceId = null)
    {
        var url = "/api/eav/entity-types/details";
        if (spaceId is not null) url += $"?spaceId={spaceId}";
        return await Client.GetEavAsync<List<EntityTypeDetailDto>>(url) ?? [];
    }

    // ------------------------------------------------------------
    // 创建：范围持久化与校验
    // ------------------------------------------------------------

    [Fact]
    public async Task Create_WithoutOwnerSpace_DefaultsToShared()
    {
        var name = NewName("共享类型");
        var (status, typeId) = await CreateTypeAsync(name, null);

        Assert.Equal(HttpStatusCode.OK, status);
        var detail = await Client.GetEavAsync<EntityTypeDetailDto>(
            $"/api/eav/entity-types/{typeId}");
        Assert.NotNull(detail);
        Assert.Null(detail!.OwnerSpaceId);
    }

    [Fact]
    public async Task Create_WithOwnerSpace_PersistsScope()
    {
        var spaceId = await CreateSpaceAsync();
        var name = NewName("独占类型");
        var (status, typeId) = await CreateTypeAsync(name, spaceId);

        Assert.Equal(HttpStatusCode.OK, status);
        var detail = await Client.GetEavAsync<EntityTypeDetailDto>(
            $"/api/eav/entity-types/{typeId}");
        Assert.NotNull(detail);
        Assert.Equal(spaceId, detail!.OwnerSpaceId);
    }

    [Fact]
    public async Task Create_WithUnknownOwnerSpace_Returns400()
    {
        var (status, _) = await CreateTypeAsync(NewName("悬空独占"), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    // ------------------------------------------------------------
    // details 端点：spaceId 过滤
    // ------------------------------------------------------------

    [Fact]
    public async Task Details_WithSpaceId_ReturnsSharedPlusOwnExclusive()
    {
        var spaceA = await CreateSpaceAsync();
        var spaceB = await CreateSpaceAsync();

        var sharedName = NewName("共享");
        var ownName = NewName("本空间独占");
        var otherName = NewName("他空间独占");
        await CreateTypeAsync(sharedName, null);
        await CreateTypeAsync(ownName, spaceA);
        await CreateTypeAsync(otherName, spaceB);

        var list = await GetDetailsAsync(spaceA);
        var names = list.Select(t => t.DisplayName).ToList();

        Assert.Contains(sharedName, names);   // 共享可见
        Assert.Contains(ownName, names);      // 本空间独占可见
        Assert.DoesNotContain(otherName, names); // 他空间独占被排除
    }

    [Fact]
    public async Task Details_WithoutSpaceId_ReturnsAllIncludingExclusive()
    {
        var space = await CreateSpaceAsync();
        var exclusiveName = NewName("全局可见性检查");
        await CreateTypeAsync(exclusiveName, space);

        var list = await GetDetailsAsync();
        Assert.Contains(list, t => t.DisplayName == exclusiveName && t.OwnerSpaceId == space);
    }

    [Fact]
    public async Task List_WithSharedOnly_ExcludesExclusiveTypes()
    {
        var space = await CreateSpaceAsync();
        var sharedName = NewName("列表共享");
        var exclusiveName = NewName("列表独占");
        await CreateTypeAsync(sharedName, null);
        await CreateTypeAsync(exclusiveName, space);

        var list = await Client.GetEavAsync<List<EntityTypeSummaryDto>>(
            "/api/eav/entity-types?sharedOnly=true") ?? [];
        var names = list.Select(t => t.DisplayName).ToList();

        Assert.Contains(sharedName, names);
        Assert.DoesNotContain(exclusiveName, names);
    }

    [Fact]
    public async Task Details_WithSharedOnly_ExcludesExclusiveTypes()
    {
        var space = await CreateSpaceAsync();
        var sharedName = NewName("详情共享");
        var exclusiveName = NewName("详情独占");
        await CreateTypeAsync(sharedName, null);
        await CreateTypeAsync(exclusiveName, space);

        var list = await Client.GetEavAsync<List<EntityTypeDetailDto>>(
            "/api/eav/entity-types/details?sharedOnly=true") ?? [];
        var names = list.Select(t => t.DisplayName).ToList();

        Assert.Contains(sharedName, names);
        Assert.DoesNotContain(exclusiveName, names);
    }

    [Fact]
    public async Task Details_WithSpaceIdAndExclusiveOnly_ReturnsOnlyOwnExclusive()
    {
        var spaceA = await CreateSpaceAsync();
        var spaceB = await CreateSpaceAsync();
        var sharedName = NewName("排他共享");
        var ownName = NewName("排他本空间");
        var otherName = NewName("排他他空间");
        await CreateTypeAsync(sharedName, null);
        await CreateTypeAsync(ownName, spaceA);
        await CreateTypeAsync(otherName, spaceB);

        var list = await Client.GetEavAsync<List<EntityTypeDetailDto>>(
            $"/api/eav/entity-types/details?spaceId={spaceA}&exclusiveOnly=true") ?? [];
        var names = list.Select(t => t.DisplayName).ToList();

        Assert.Contains(ownName, names);          // 本空间独占可见
        Assert.DoesNotContain(sharedName, names); // 共享被排除
        Assert.DoesNotContain(otherName, names);  // 他空间独占被排除
    }
}
