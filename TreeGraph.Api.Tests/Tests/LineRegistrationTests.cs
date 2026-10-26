using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.LineSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// R7 Line 注册 nodes。
///   - Line 创建：同事务双写 lines + nodes（host_type='Line'）；
///   - Line 更新：同步 nodes 注册行（名称唯一真源在 nodes）；
///   - Line 删除：仍有线路节点 → 409；通过后双删。
/// （原 LineStructure 实体类型 / next 属性内置种子已随 relation 体系整体下线。）
/// </summary>
public class LineRegistrationTests : IntegrationTestBase
{
    public LineRegistrationTests(EavApiFactory factory) : base(factory) { }

    private static readonly JsonSerializerOptions EnumJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ============================================================
    // Line 创建双写 / 更新同步 / 删除双删
    // ============================================================

    [Fact]
    public async Task LineCreate_DoubleWritesNodesRow_ThenLineNodeCreatable()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "注册测试线路" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        // nodes 注册行存在 → 结构端点可读取（线路身份已在 nodes）
        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists); // 尚无线路节点
        Assert.Equal("注册测试线路", dto.LineName);

        // nodes 注册行存在 → 可建线路节点（line_id 锚点校验通过）
        var nodeResp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = $"{Guid.NewGuid()}", lineId, name = "杆塔 #001",
            description = (string?)null, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, nodeResp.StatusCode);
    }

    [Fact]
    public async Task LineUpdate_SyncsNodesRow()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "旧名" });
        create.EnsureSuccessStatusCode();

        var update = await Client.PutAsJsonAsync(
            $"/api/Lines/{lineId}", new { id = lineId, name = "新名", description = "改后" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
        Assert.NotNull(dto);
        Assert.Equal("新名", dto!.LineName); // LineName 取自 nodes 注册行
    }

    [Fact]
    public async Task LineDelete_WithNodes_Returns409_ThenDoubleDelete()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "待删线路" });
        create.EnsureSuccessStatusCode();
        var nodeId = $"{Guid.NewGuid()}";
        var node = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name = "杆塔",
            description = (string?)null, predecessorId = (string?)null
        });
        node.EnsureSuccessStatusCode();

        // 仍有线路节点 → 409
        Assert.Equal(HttpStatusCode.Conflict,
            (await Client.DeleteAsync($"/api/Lines/{lineId}")).StatusCode);

        // 删节点后 → 双删 lines + nodes 注册行
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/LineNode/{nodeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/Lines/{lineId}")).StatusCode);

        // nodes 注册行已删 → 结构端点 404、再建节点 400
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/api/eav/lines/{lineId}/structure")).StatusCode);
        var recreate = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = $"{Guid.NewGuid()}", lineId, name = "杆塔",
            description = (string?)null, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, recreate.StatusCode);
    }

    // ============================================================
    // 段边冒烟：建链后结构端点可见 line_segment 边（edges 表）
    // ============================================================

    [Fact]
    public async Task SegmentEdge_SmokeTest()
    {
        // 建链冒烟：段边直写 edges 表，结构端点可见
        var lineId = $"L_{Guid.NewGuid():N}";
        var create = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "种子可用线路" });
        create.EnsureSuccessStatusCode();
        var n1 = $"{Guid.NewGuid()}";
        var n2 = $"{Guid.NewGuid()}";
        foreach (var (id, name, pred) in new[]
        {
            (n1, "节点1", (string?)null),
            (n2, "节点2", (string?)n1)
        })
        {
            var nodeResp = await Client.PostAsJsonAsync("/api/LineNode", new
            {
                id, lineId, name, description = (string?)null, predecessorId = pred
            });
            nodeResp.EnsureSuccessStatusCode();
        }

        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
        var seg = Assert.Single(dto!.Segments);
        Assert.Equal("line_segment", seg.EdgeType);
    }
}
