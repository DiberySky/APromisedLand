using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 命名绑定与现有 EAV 管道互操作测试。
///
/// 核心验证："通过绑定写入的数据，能用旧 EAV API 读到，反之亦然"。
/// 绑定只是 (binding_id → entity_type + entity_id) 的映射，底层仍是同一条 EAV 管道。
/// </summary>
public class InodeEavFacadeTests : IntegrationTestBase
{
    private const string EntityType = "InodeFacadeTest";

    public InodeEavFacadeTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 绑定写 → 旧 EAV 读
    // ============================================================

    [Fact]
    public async Task WriteViaBinding_ReadViaOldEavApi_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "互操作");
        var put = await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?>
            {
                ["name"] = "via-inode",
                ["amount"] = 77L
            });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        // 用旧 EAV API 读
        var viaEav = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{binding.EntityId}");

        Assert.NotNull(viaEav);
        Assert.Equal(binding.EntityId, viaEav!.EntityId);
        Assert.Equal("via-inode", viaEav.Properties["name"].GetString());
        Assert.Equal(77L, viaEav.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 旧 EAV 写 → 绑定读
    // ============================================================

    [Fact]
    public async Task WriteViaOldEavApi_ReadViaBinding_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "反向");

        // 用旧 EAV API 改
        await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{binding.EntityId}",
            new Dictionary<string, object?>
            {
                ["name"] = "via-eav",
                ["amount"] = 88L
            });

        // 从绑定读
        var viaBinding = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));

        Assert.NotNull(viaBinding);
        Assert.Equal("via-eav", viaBinding!.Properties["name"].GetString());
        Assert.Equal(88L, viaBinding.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 审计历史
    // ============================================================

    [Fact]
    public async Task History_ViaBinding_ReturnsLogs()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "历史");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "v1" });

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "v2" });

        var history = await Client.GetEavAsync<List<EntityHistoryDto>>(
            $"{InodeBindingUrl(inodeId, binding.BindingId)}/history");

        Assert.NotNull(history);
        Assert.NotEmpty(history!);
        Assert.Contains(history, h =>
            h.ChangeType == "Insert" || h.ChangeType == "Update");
    }

    [Fact]
    public async Task History_UnknownBinding_ReturnsEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var history = await Client.GetEavAsync<List<EntityHistoryDto>>(
            $"/api/inode/{inodeId}/bindings/{Guid.NewGuid():D}/history");

        Assert.NotNull(history);
        Assert.Empty(history!);
    }

    // ============================================================
    // 删除绑定后旧 EAV 也不可见
    // ============================================================

    [Fact]
    public async Task Delete_ViaBinding_OldEavApiSeesEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "待删");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "to-delete" });

        var del = await Client.DeleteAsync(
            InodeBindingUrl(inodeId, binding.BindingId));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        // 旧 EAV API 契约：不存在的实体返回 200 + 空 Properties（无 404 分支）
        var viaEav = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{binding.EntityId}");
        Assert.NotNull(viaEav);
        Assert.False(viaEav!.Properties.ContainsKey("name"));
    }
}
