using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode Facade 与现有 EAV 管道互操作测试。
///
/// 核心验证："通过 iNode 写入的数据，能用旧 EAV API 读到，反之亦然"。
/// 这保证了方案 B 的"零破坏"承诺。
/// </summary>
public class InodeEavFacadeTests : IntegrationTestBase
{
    private const string EntityType = "InodeFacadeTest";

    public InodeEavFacadeTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // iNode 写 → 旧 EAV 读
    // ============================================================

    [Fact]
    public async Task WriteViaInode_ReadViaOldEavApi_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 通过 iNode 写入
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?>
            {
                ["name"] = "via-inode",
                ["amount"] = 77L
            });

        // 从 iNode 拿到 entityId
        var viaInode = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.NotNull(viaInode);

        // 用旧 EAV API 读
        var viaEav = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{viaInode!.EntityId}");

        Assert.NotNull(viaEav);
        Assert.Equal(viaInode.EntityId, viaEav!.EntityId);
        Assert.Equal("via-inode", viaEav.Properties["name"].GetString());
        Assert.Equal(77L, viaEav.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 旧 EAV 写 → iNode 读
    // ============================================================

    [Fact]
    public async Task WriteViaOldEavApi_ReadViaInode_Works()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        // 先通过 iNode 创建，拿到 entityId
        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "seed" });

        var created = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.NotNull(created);
        var entityId = created!.EntityId;

        // 用旧 EAV API 改
        await Client.PutAsJsonAsync(
            $"/api/eav/{EntityType}/entities/{entityId}",
            new Dictionary<string, object?>
            {
                ["name"] = "via-eav",
                ["amount"] = 88L
            });

        // 从 iNode 读
        var viaInode = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");

        Assert.NotNull(viaInode);
        Assert.Equal("via-eav", viaInode!.Properties["name"].GetString());
        Assert.Equal(88L, viaInode.Properties["amount"].GetInt64());
    }

    // ============================================================
    // 审计历史
    // ============================================================

    [Fact]
    public async Task History_ViaInode_ReturnsLogs()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v1" });

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "v2" });

        var history = await Client.GetFromJsonAsync<List<EntityHistoryDto>>(
            $"/api/inode/{inodeId}/entities/{EntityType}/history");

        Assert.NotNull(history);
        Assert.NotEmpty(history!);
        // 至少有一次 Insert 或 Update
        Assert.Contains(history, h =>
            h.ChangeType == "Insert" || h.ChangeType == "Update");
    }

    [Fact]
    public async Task History_NotCreated_ReturnsEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var history = await Client.GetFromJsonAsync<List<EntityHistoryDto>>(
            $"/api/inode/{inodeId}/entities/{EntityType}/history");

        Assert.NotNull(history);
        Assert.Empty(history!);
    }

    // ============================================================
    // 删除后旧 EAV 也不可见
    // ============================================================

    [Fact]
    public async Task Delete_ViaInode_OldEavApiSeesEmpty()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}",
            new Dictionary<string, object?> { ["name"] = "to-delete" });

        var created = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        var entityId = created!.EntityId;

        // 通过 iNode 删除
        var del = await Client.DeleteAsync(
            $"/api/inode/{inodeId}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // 旧 EAV API 契约：不存在的实体返回 200 + 空 Properties（无 404 分支）
        var viaEav = await Client.GetFromJsonAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{entityId}");
        Assert.NotNull(viaEav);
        Assert.False(viaEav!.Properties.ContainsKey("name"));
    }
}
