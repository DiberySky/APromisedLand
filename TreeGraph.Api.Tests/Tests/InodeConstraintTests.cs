using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEav.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 核心约束（R2 / R3）测试。
///
/// R2：(inodeId, entityType) → 唯一 entityId
/// R3：entityId → 唯一 inodeId
///
/// 由于 entityId 由服务端生成、用户不感知，R3 通过"两 iNode 独立创建"来间接验证。
/// </summary>
public class InodeConstraintTests : IntegrationTestBase
{
    private const string EntityType = "InodeConstraintTest";

    public InodeConstraintTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // R2：同 iNode 同类型只能 1 个实体
    // ============================================================

    [Fact]
    public async Task SameInodeSameType_AlwaysReturnsSameEntityId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 5 次 PUT 同 (iNode, type)
        for (int i = 0; i < 5; i++)
        {
            await Client.PutAsJsonAsync(
                $"/api/inode/{inodeId}/entities/{EntityType}",
                new Dictionary<string, object?> { ["name"] = $"v{i}" });
        }

        // 只有 1 个实体
        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");
        Assert.NotNull(list);
        Assert.Single(list!);

        // 最后一次值生效
        Assert.Equal("v4", list![0].Properties["name"].GetString());
    }

    // ============================================================
    // R3：两个 iNode 同类型 → 2 个不同 entityId
    // ============================================================

    [Fact]
    public async Task DifferentInodes_ProduceDifferentEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();
        var inodeC = NewInodeId();

        foreach (var inodeId in new[] { inodeA, inodeB, inodeC })
        {
            await Client.PutAsJsonAsync(
                $"/api/inode/{inodeId}/entities/{EntityType}",
                new Dictionary<string, object?> { ["name"] = inodeId[..8] });
        }

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeA}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeB}/entities/{EntityType}");
        var dtoC = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeC}/entities/{EntityType}");

        var ids = new[] { dtoA!.EntityId, dtoB!.EntityId, dtoC!.EntityId };
        Assert.Equal(3, ids.Distinct().Count());
    }

    // ============================================================
    // 同 iNode 多类型 → 各自独立 entityId
    // ============================================================

    [Fact]
    public async Task SameInodeDifferentTypes_HaveIndependentEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");

        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "A" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}B",
            new Dictionary<string, object?> { ["name"] = "B" });

        var dtoA = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        var dtoB = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}B");

        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);
        Assert.NotEqual(dtoA.EntityType, dtoB.EntityType);
    }

    // ============================================================
    // 声明层与实体层的独立幂等性
    // ============================================================

    [Fact]
    public async Task ExplicitAttach_ThenPut_ReusesDeclaration()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 显式声明
        await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        // 然后 PUT
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "x" });

        // 声明列表里只有 1 条
        var list = await Client.GetFromJsonAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.True(list![0].HasEntity);
    }

    // ============================================================
    // 唯一性跨 PUT / PATCH
    // ============================================================

    [Fact]
    public async Task Patch_CannotCreateDuplicate()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        // 对不存在的实体 PATCH（首次）→ 会自动创建
        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/inode/{inodeId}/entities/{EntityType}")
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 100L
            })
        };
        await Client.SendAsync(req);

        // 仍然只有 1 个实体
        var list = await Client.GetFromJsonAsync<List<DynamicEntityDto>>(
            $"/api/inode/{inodeId}/entities");
        Assert.NotNull(list);
        Assert.Single(list!);
    }
}
