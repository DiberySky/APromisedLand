using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 实体转移（换宿主）集成测试。
///
/// 转移语义：
///   - 只改归属映射（inode_entity.InodeId），EAV 属性数据原样保留
///   - 目标侧自动补声明（混合模式，与 GetOrCreate 一致）
///   - 目标宿主已有同类型实体 → 409；源无该类型实体 → 404
/// </summary>
public class InodeTransferTests : IntegrationTestBase
{
    private const string EntityType = "InodeTransferTest";

    public InodeTransferTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    [Fact]
    public async Task Transfer_Success_MovesEntityAndKeepsData()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var source = NewInodeId();
        var target = NewInodeId();

        // 源宿主上创建实体（PUT 自动建映射 + 声明）
        var put = await Client.PutAsJsonAsync(
            $"/api/inode/{source}/entities/{EntityType}", new { name = "待转移" });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        // 转移到目标宿主
        var transfer = await Client.PostAsJsonAsync(
            $"/api/inode/{source}/entities/{EntityType}/transfer",
            new InodeTransferRequest { TargetInodeId = target });
        Assert.Equal(HttpStatusCode.NoContent, transfer.StatusCode);

        // 源侧该类型已无实体
        var afterSrc = await Client.GetAsync($"/api/inode/{source}/entities/{EntityType}");
        Assert.Equal(HttpStatusCode.NotFound, afterSrc.StatusCode);

        // 目标侧实体数据原样可读
        var afterTarget = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/inode/{target}/entities/{EntityType}");
        Assert.NotNull(afterTarget);
        Assert.Equal("待转移", afterTarget!.Properties["name"].GetString());

        // 目标侧声明已自动补齐（混合模式）
        var targetCards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{target}/types");
        var card = Assert.Single(targetCards!);
        Assert.Equal(EntityType, card.EntityType);
        Assert.True(card.Declared);
        Assert.True(card.HasEntity);
    }

    [Fact]
    public async Task Transfer_WhenSourceHasNoEntity_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{NewInodeId()}/entities/{EntityType}/transfer",
            new InodeTransferRequest { TargetInodeId = NewInodeId() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Transfer_WhenTargetOccupied_Returns409()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var source = NewInodeId();
        var target = NewInodeId();

        // 源与目标都创建该类型实体
        await Client.PutAsJsonAsync(
            $"/api/inode/{source}/entities/{EntityType}", new { name = "源" });
        await Client.PutAsJsonAsync(
            $"/api/inode/{target}/entities/{EntityType}", new { name = "目标" });

        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{source}/entities/{EntityType}/transfer",
            new InodeTransferRequest { TargetInodeId = target });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("无法转移", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Transfer_ToSameHost_Returns409()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inode = NewInodeId();

        await Client.PutAsJsonAsync(
            $"/api/inode/{inode}/entities/{EntityType}", new { name = "自身" });

        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{inode}/entities/{EntityType}/transfer",
            new InodeTransferRequest { TargetInodeId = inode });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }
}
