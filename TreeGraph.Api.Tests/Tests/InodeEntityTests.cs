using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// iNode 命名绑定（inode_entity）集成测试。
///
/// 契约：每条绑定 = binding_id + name + 一份私有 EAV 数据（独立 entity_id）。
/// 同一 iNode 同一类型可建多条绑定，绑定之间数据完全隔离。
/// 覆盖 Create / Get / Put / Patch / Delete / 列表 / 隔离 / 重名。
/// </summary>
public class InodeEntityTests : IntegrationTestBase
{
    private const string EntityType = "InodeEntityTest";

    public InodeEntityTests(EavApiFactory factory) : base(factory) { }

    private static string NewInodeId() => Guid.NewGuid().ToString("D");

    // ============================================================
    // 创建
    // ============================================================

    [Fact]
    public async Task CreateBinding_ThenPut_ReadsBack()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var (bindingId, entityId) = await CreateInodeBindingAsync(inodeId, EntityType, "首绑");

        var put = await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, bindingId),
            new Dictionary<string, object?>
            {
                ["name"] = "first",
                ["amount"] = 42L
            });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, bindingId));

        Assert.NotNull(dto);
        Assert.Equal(bindingId, dto!.BindingId);
        Assert.Equal("首绑", dto.Name);
        Assert.Equal(EntityType, dto.EntityType);
        Assert.Equal(entityId, dto.EntityId);
        Assert.Equal("first", dto.Properties["name"].GetString());
        Assert.Equal(42L, dto.Properties["amount"].GetInt64());
    }

    [Fact]
    public async Task CreateBinding_GeneratesDistinctBindingAndEntityIds()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var first = await CreateInodeBindingAsync(inodeId, EntityType, "甲");
        var second = await CreateInodeBindingAsync(inodeId, EntityType, "乙");

        Assert.NotEqual(first.BindingId, second.BindingId);
        Assert.NotEqual(first.EntityId, second.EntityId);
    }

    [Fact]
    public async Task Put_SecondTime_SameEntityId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "反复写");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "first" });

        var dto1 = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "second" });

        var dto2 = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));

        Assert.Equal(dto1!.EntityId, dto2!.EntityId);
        Assert.Equal("second", dto2.Properties["name"].GetString());
    }

    // ============================================================
    // 读取
    // ============================================================

    [Fact]
    public async Task Get_UnknownBinding_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.GetAsync(
            InodeBindingUrl(inodeId, Guid.NewGuid().ToString("D")));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    // ============================================================
    // PATCH
    // ============================================================

    [Fact]
    public async Task Patch_PartialUpdate_KeepsOthers()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "局部改");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "orig", ["amount"] = 1L });

        using var req = new HttpRequestMessage(
            HttpMethod.Patch,
            InodeBindingUrl(inodeId, binding.BindingId))
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["amount"] = 99L
            })
        };
        var resp = await Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, binding.BindingId));

        Assert.Equal("orig", dto!.Properties["name"].GetString());
        Assert.Equal(99L, dto.Properties["amount"].GetInt64());
    }

    // ============================================================
    // DELETE
    // ============================================================

    [Fact]
    public async Task DeleteBinding_RemovesBindingAndItsData()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, EntityType, "待删");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["name"] = "x" });

        var del = await Client.DeleteAsync(
            InodeBindingUrl(inodeId, binding.BindingId));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        using (var doc = JsonDocument.Parse(
            await del.Content.ReadAsStringAsync()))
        {
            Assert.Equal("deleted",
                doc.RootElement.GetProperty("data").GetProperty("action").GetString());
        }

        // 绑定已不可读
        var get = await Client.GetAsync(
            InodeBindingUrl(inodeId, binding.BindingId));
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // 该绑定私有的 EAV 数据也已删除（旧 EAV 接口读不到属性）
        var viaEav = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{EntityType}/entities/{binding.EntityId}");
        Assert.NotNull(viaEav);
        Assert.False(viaEav!.Properties.ContainsKey("name"));
    }

    [Fact]
    public async Task Delete_UnknownBinding_Returns404()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var resp = await Client.DeleteAsync(
            InodeBindingUrl(inodeId, Guid.NewGuid().ToString("D")));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenCreateAgain_GetsNewBindingId()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var first = await CreateInodeBindingAsync(inodeId, EntityType, "重建");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, first.BindingId),
            new Dictionary<string, object?> { ["name"] = "v1" });

        var del = await Client.DeleteAsync(
            InodeBindingUrl(inodeId, first.BindingId));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var second = await CreateInodeBindingAsync(inodeId, EntityType, "重建");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, second.BindingId),
            new Dictionary<string, object?> { ["name"] = "v2" });

        var dto = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, second.BindingId));

        Assert.NotEqual(first.BindingId, second.BindingId);
        Assert.NotEqual(first.EntityId, second.EntityId);
        Assert.Equal("v2", dto!.Properties["name"].GetString());
    }

    // ============================================================
    // 列表
    // ============================================================

    [Fact]
    public async Task List_ReturnsAllBindings()
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

        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, x => x.BindingId == a.BindingId && x.Name == "A");
        Assert.Contains(list, x => x.BindingId == b.BindingId && x.Name == "B");
    }

    [Fact]
    public async Task List_EmptyInode_ReturnsEmpty()
    {
        var inodeId = NewInodeId();

        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");

        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    // ============================================================
    // 隔离
    // ============================================================

    [Fact]
    public async Task TwoInodes_SameType_AreIsolated()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);

        var inodeA = NewInodeId();
        var inodeB = NewInodeId();

        var bindingA = await CreateInodeBindingAsync(inodeA, EntityType, "同名");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeA, bindingA.BindingId),
            new Dictionary<string, object?> { ["name"] = "A-value" });

        var bindingB = await CreateInodeBindingAsync(inodeB, EntityType, "同名");
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeB, bindingB.BindingId),
            new Dictionary<string, object?> { ["name"] = "B-value" });

        var dtoA = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeA, bindingA.BindingId));
        var dtoB = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeB, bindingB.BindingId));

        Assert.NotEqual(dtoA!.EntityId, dtoB!.EntityId);
        Assert.Equal("A-value", dtoA.Properties["name"].GetString());
        Assert.Equal("B-value", dtoB.Properties["name"].GetString());

        // 跨节点同名不被视为冲突
        Assert.Equal("同名", dtoA.Name);
        Assert.Equal("同名", dtoB.Name);
    }

    /// <summary>
    /// 回归：声明但无属性的类型，绑定可创建但无法写入。
    ///
    /// 实体创建（绑定）阶段不校验属性定义；
    /// 写入阶段因"未知属性"被 400 拒绝。
    /// </summary>
    [Fact]
    public async Task Put_OnTypeWithoutAttributes_FailsValidation()
    {
        // 建类型（不建属性）
        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { displayName = $"无属性写入_{Guid.NewGuid():N}" });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content
            .ReadEavAsync<CreateTypeResponse>();
        Assert.NotNull(created);

        var inodeId = NewInodeId();

        var binding = await CreateInodeBindingAsync(inodeId, created!.EntityType, "无属性");

        // PUT 会走到 EavValidationException（未知属性）
        var resp = await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, binding.BindingId),
            new Dictionary<string, object?> { ["whatever"] = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("未知属性", body);
    }

    // ============================================================
    // 同节点同类型多条命名绑定
    // ============================================================

    [Fact]
    public async Task SameInode_SameType_TwoBindings_HaveIndependentData()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var a = await CreateInodeBindingAsync(inodeId, EntityType, "工作表");
        var b = await CreateInodeBindingAsync(inodeId, EntityType, "备用表");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, a.BindingId),
            new Dictionary<string, object?> { ["name"] = "A", ["amount"] = 1L });
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, b.BindingId),
            new Dictionary<string, object?> { ["name"] = "B", ["amount"] = 2L });

        var dtoA = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, a.BindingId));
        var dtoB = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, b.BindingId));

        Assert.Equal("A", dtoA!.Properties["name"].GetString());
        Assert.Equal(1L, dtoA.Properties["amount"].GetInt64());
        Assert.Equal("B", dtoB!.Properties["name"].GetString());
        Assert.Equal(2L, dtoB.Properties["amount"].GetInt64());

        var list = await Client.GetEavAsync<List<InodeBindingDto>>(
            $"/api/inode/{inodeId}/bindings");
        Assert.Equal(2, list!.Count);

        var cards = await Client.GetEavAsync<List<InodeTypeCardDto>>(
            $"/api/inode/{inodeId}/types");
        var card = cards!.Single(c => c.EntityType == EntityType);
        Assert.True(card.HasEntity);
        Assert.Equal(2, card.BindingCount);
    }

    [Fact]
    public async Task DeleteOneBinding_SiblingRemains()
    {
        await InodeTestData.EnsureTypeAsync(Client, EntityType);
        var inodeId = NewInodeId();

        var a = await CreateInodeBindingAsync(inodeId, EntityType, "甲");
        var b = await CreateInodeBindingAsync(inodeId, EntityType, "乙");

        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, a.BindingId),
            new Dictionary<string, object?> { ["name"] = "A" });
        await Client.PutAsJsonAsync(
            InodeBindingUrl(inodeId, b.BindingId),
            new Dictionary<string, object?> { ["name"] = "B" });

        var del = await Client.DeleteAsync(
            InodeBindingUrl(inodeId, a.BindingId));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var dtoB = await Client.GetEavAsync<InodeBindingDetailDto>(
            InodeBindingUrl(inodeId, b.BindingId));
        Assert.NotNull(dtoB);
        Assert.Equal("B", dtoB!.Properties["name"].GetString());
    }

    private sealed record CreateTypeResponse(string EntityTypeId, string EntityType);
}
