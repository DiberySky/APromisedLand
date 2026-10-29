using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 绑定约束测试。
///
/// 契约：每条绑定的 entity_id 由服务端独立生成且固定不变，
/// 重复写入不会产生新绑定；跨节点 / 跨类型的绑定彼此隔离。
/// </summary>
public class InodeConstraintTests : IntegrationTestBase
{
    private const string EntityType = "InodeConstraintTest";

    public InodeConstraintTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 同一条绑定重复 PUT 不产生新绑定
    // ============================================================

    [Fact]
    public async Task PutSameBindingFiveTimes_KeepsSingleBinding()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "反复写");

        // 5 次 PUT 同一绑定
        for (int i = 0; i < 5; i++)
        {
            await Client.PutAsJsonAsync(
                InodeBindingUrl(inodeId, binding.BindingId),
                new Dictionary<string, object?> { ["name"] = $"v{i}" });
        }

        // 只有 1 条绑定
        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");
        Assert.NotNull(list);
        Assert.Single(list!);

        // 最后一次值生效
        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));
        Assert.Equal(binding.EntityId, dto!.EntityId);
        Assert.Equal("v4", dto.Properties["name"].GetString());
    }

    // ============================================================
    // 不同 iNode 各自创建 → 不同 entityId
    // ============================================================

    [Fact]
    public async Task DifferentInodes_ProduceDifferentEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var ids = new List<string>();
        foreach (var inodeId in new[] { NewInodeId(), NewInodeId(), NewInodeId() })
        {
            var binding = await CreateInodeBindingAsync(inodeId, EntityType, "自身");
            await Client.PutAsJsonAsync(
                InodeBindingUrl(inodeId, binding.BindingId),
                new Dictionary<string, object?> { ["name"] = inodeId[..8] });
            ids.Add(binding.EntityId);
        }

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

        var a = await CreateInodeBindingAsync(inodeId, EntityType, "A");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, a.BindingId),
            new Dictionary<string, object?> { ["name"] = "A" });

        var b = await CreateInodeBindingAsync(inodeId, EntityType + "B", "B");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, b.BindingId),
            new Dictionary<string, object?> { ["name"] = "B" });

        var dtoA = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, a.BindingId));
        var dtoB = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, b.BindingId));

        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);
        Assert.NotEqual(dtoA.EntityType, dtoB.EntityType);
    }

    // ============================================================
    // 声明层与绑定层的独立幂等性
    // ============================================================

    [Fact]
    public async Task ExplicitAttach_ThenCreateBinding_ReusesDeclaration()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 显式声明
        await Client.PostAsync(
            $"/api/inode/{inodeId}/types/{EntityType}", null);

        // 然后创建绑定并 PUT（声明已存在，不再重复添加）
        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "唯一");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "x" });

        // 声明列表里只有 1 条
        var list = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        Assert.NotNull(list);
        var card = Assert.Single(list!);
        Assert.Equal(EntityType, card.EntityType);
        Assert.True(card.HasEntity);
        Assert.Equal(1, card.BindingCount);
    }

    // ============================================================
    // 重复写入不产生重复绑定
    // ============================================================

    [Fact]
    public async Task Patch_DoesNotCreateDuplicate()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "补丁");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "v1" });

        // PATCH 同一绑定
        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            InodeBindingUrl(inodeId, binding.BindingId))
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 100L
            })
        };
        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // 仍然只有 1 条绑定
        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");
        Assert.NotNull(list);
        Assert.Single(list!);
    }
}
