using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 命名绑定（name）语义集成测试。
///
/// 契约：
///   - name 在同一 (inode_id, entity_type) 下唯一，跨类型 / 跨节点可同名
///   - 绑定之间互不共享数据（不再有 attach/detach/transfer）
///   - 重命名同样受唯一约束
/// </summary>
public class InodeBindingNamingTests : IntegrationTestBase
{
    private const string EntityType = "InodeBindingNameTest";

    public InodeBindingNamingTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 同节点同类型两条命名绑定
    // ============================================================

    [Fact]
    public async Task TwoNames_SameType_BothVisible()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var a = await CreateInodeBindingAsync(inodeId, EntityType, "第一次");
        var b = await CreateInodeBindingAsync(inodeId, EntityType, "第二次");

        // 两条绑定数据各自独立
        await Client.PutAsJsonAsync(InodeBindingUrl(inodeId, a.BindingId),
            new Dictionary<string, object?> { ["name"] = "甲" });
        await Client.PutAsJsonAsync(InodeBindingUrl(inodeId, b.BindingId),
            new Dictionary<string, object?> { ["name"] = "乙" });

        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, x => x.Name == "第一次");
        Assert.Contains(list, x => x.Name == "第二次");

        var dtoA = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, a.BindingId));
        var dtoB = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, b.BindingId));

        Assert.Equal("甲", dtoA!.Properties["name"].GetString());
        Assert.Equal("乙", dtoB!.Properties["name"].GetString());
    }

    // ============================================================
    // 唯一性
    // ============================================================

    [Fact]
    public async Task DuplicateName_SameInodeSameType_Returns400()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await CreateInodeBindingAsync(inodeId, EntityType, "重复名");

        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{inodeId}/bindings",
            new CreateBindingRequest { EntityType = EntityType, Name = "重复名" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("重复名", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SameName_DifferentTypes_Allowed()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        await InodeTestData.EnsureTypeAsync(Client, EntityType + "B");
        var inodeId = NewInodeId();

        var a = await CreateInodeBindingAsync(inodeId, EntityType, "共用名");
        var b = await CreateInodeBindingAsync(inodeId, EntityType + "B", "共用名");

        Assert.NotEqual(a.BindingId, b.BindingId);
    }

    [Fact]
    public async Task SameName_DifferentInodes_Allowed()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var a = await CreateInodeBindingAsync(NewInodeId(), EntityType, "同名");
        var b = await CreateInodeBindingAsync(NewInodeId(), EntityType, "同名");

        Assert.NotEqual(a.BindingId, b.BindingId);
    }

    [Fact]
    public async Task EmptyName_Returns400()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.PostAsJsonAsync(
            $"/api/inode/{inodeId}/bindings",
            new CreateBindingRequest { EntityType = EntityType, Name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ============================================================
    // 重命名
    // ============================================================

    [Fact]
    public async Task Rename_ChangesName()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "旧名");

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{InodeBindingUrl(inodeId, binding.BindingId)}/name")
        {
            Content = JsonContent.Create(new RenameBindingRequest { Name = "新名" })
        };
        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));
        Assert.Equal("新名", dto!.Name);
    }

    [Fact]
    public async Task Rename_ToExistingName_Returns400()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        await CreateInodeBindingAsync(inodeId, EntityType, "甲");
        var b = await CreateInodeBindingAsync(inodeId, EntityType, "乙");

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{InodeBindingUrl(inodeId, b.BindingId)}/name")
        {
            Content = JsonContent.Create(new RenameBindingRequest { Name = "甲" })
        };
        var resp = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        // 名字保持原样
        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, b.BindingId));
        Assert.Equal("乙", dto!.Name);
    }

    [Fact]
    public async Task Rename_UnknownBinding_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{InodeBindingUrl(inodeId, Guid.NewGuid().ToString("D"))}/name")
        {
            Content = JsonContent.Create(new RenameBindingRequest { Name = "新名" })
        };
        var resp = await Client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
