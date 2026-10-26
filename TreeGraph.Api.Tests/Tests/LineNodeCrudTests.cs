using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.LineSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// LineNode CRUD 集成测试（R7：LineNode 收敛进 nodes 注册表，真实 PG 容器）。
/// 存储形态：nodes 行 host_type='LineNode'、line_id → Line；
/// 结构连接 = relation 值（LineStructure.next，line_segment 边，前驱 → 自身）。
/// </summary>
public class LineNodeCrudTests : IntegrationTestBase
{
    public LineNodeCrudTests(EavApiFactory factory) : base(factory) { }

    private static readonly JsonSerializerOptions EnumJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>LineNode Facade GET 返回形状（匿名 DTO：Id/LineId/Name/Description）。</summary>
    private sealed record LineNodeView(string Id, string LineId, string Name, string? Description);

    // ============================================================
    // 创建
    // ============================================================

    [Fact]
    public async Task Create_RoundTrip()
    {
        var lineId = await CreateLineAsync();
        var nodeId = $"{Guid.NewGuid()}";

        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name = "杆塔 #001", description = "10kV 跨越公路",
            predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var view = await Client.GetEavAsync<LineNodeView>($"/api/LineNode/{nodeId}");
        Assert.NotNull(view);
        Assert.Equal(lineId, view!.LineId);
        Assert.Equal("杆塔 #001", view.Name);
        Assert.Equal("10kV 跨越公路", view.Description);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        var lineId = await CreateLineAsync();
        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId, name = "", predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyLineId_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId = "", name = "杆塔", predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_LineNotExists_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId = $"L_{Guid.NewGuid():N}", name = "杆塔",
            predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("所属线路不存在或不是 Line", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_LineIdNotLine_Returns400()
    {
        // line_id 目标必须是 host_type='Line' 的 nodes 行（服务层校验）
        var lineId = await CreateLineAsync();
        var otherNode = await CreateLineNodeAsync(lineId, "已有节点");

        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId = otherNode, name = "杆塔", predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("所属线路不存在或不是 Line", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_PredecessorNotExists_Returns400()
    {
        var lineId = await CreateLineAsync();
        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId, name = "杆塔",
            predecessorId = $"{Guid.NewGuid()}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("前驱节点不存在", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_PredecessorCrossLine_Returns400()
    {
        var lineA = await CreateLineAsync();
        var lineB = await CreateLineAsync();
        var predInB = await CreateLineNodeAsync(lineB, "B 线节点");

        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = (string?)null, lineId = lineA, name = "A 线节点", predecessorId = predInB
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("段边两端必须同属一条线路", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 创建即建边（首节点无边 / 次节点有边）
    // ============================================================

    [Fact]
    public async Task Create_FirstNode_NoSegmentEdge()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "首节点");

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        Assert.True(dto!.Exists); // 有节点即存在
        Assert.Empty(dto.Segments);
        Assert.Equal([nodeId], dto.OrderedNodeIds);
    }

    [Fact]
    public async Task Create_WithPredecessor_WritesLineSegmentEdge()
    {
        var lineId = await CreateLineAsync();
        var first = await CreateLineNodeAsync(lineId, "首节点");
        var second = await CreateLineNodeAsync(lineId, "次节点", pred: first);

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        var seg = Assert.Single(dto!.Segments);
        Assert.Equal(first, seg.FromNodeId);
        Assert.Equal(second, seg.ToNodeId);
        Assert.Equal("line_segment", seg.EdgeType);
        Assert.False(string.IsNullOrWhiteSpace(seg.EdgeId));
        Assert.Equal([first, second], dto.OrderedNodeIds);
    }

    // ============================================================
    // 查询
    // ============================================================

    [Fact]
    public async Task GetAll_Search_FiltersByName()
    {
        var lineId = await CreateLineAsync();
        await CreateLineNodeAsync(lineId, "跨越点 A");
        await CreateLineNodeAsync(lineId, "跨越点 B");
        await CreateLineNodeAsync(lineId, "杆塔 C");

        var all = await Client.GetEavAsync<List<LineNodeView>>("/api/LineNode");
        Assert.NotNull(all);
        Assert.True(all!.Count >= 3);

        var filtered = await Client.GetEavAsync<List<LineNodeView>>("/api/LineNode?search=跨越点");
        Assert.NotNull(filtered);
        Assert.Equal(2, filtered!.Count);
        Assert.All(filtered, n => Assert.Contains("跨越点", n.Name));
    }

    [Fact]
    public async Task GetAll_LineId_FiltersByLine()
    {
        var lineA = await CreateLineAsync();
        var lineB = await CreateLineAsync();
        var nodeA = await CreateLineNodeAsync(lineA, "A 线节点");
        await CreateLineNodeAsync(lineB, "B 线节点");

        var filtered = await Client.GetEavAsync<List<LineNodeView>>($"/api/LineNode?lineId={lineA}");
        Assert.NotNull(filtered);
        var view = Assert.Single(filtered!);
        Assert.Equal(nodeA, view.Id);
        Assert.Equal(lineA, view.LineId);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var resp = await Client.GetAsync($"/api/LineNode/LN_{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // 更新
    // ============================================================

    [Fact]
    public async Task Update_RenameAndDescription()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "旧名称");

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{nodeId}", new
        {
            name = "新名称", description = "更新后的描述",
            lineId = (string?)null, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var view = await Client.GetEavAsync<LineNodeView>($"/api/LineNode/{nodeId}");
        Assert.Equal("新名称", view!.Name);
        Assert.Equal("更新后的描述", view.Description);
    }

    [Fact]
    public async Task Update_ReconnectPredecessor_MovesEdge()
    {
        var lineId = await CreateLineAsync();
        var n1 = await CreateLineNodeAsync(lineId, "节点1");
        var n2 = await CreateLineNodeAsync(lineId, "节点2", pred: n1);
        var n3 = await CreateLineNodeAsync(lineId, "节点3"); // 独立节点，无出边

        // n2 重接前驱：n1 → n3（next 单值语义：旧前驱边删除，新前驱边写入）
        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{n2}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = (string?)null, predecessorId = n3
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        var moved = Assert.Single(dto!.Segments);
        Assert.Equal(n3, moved.FromNodeId);
        Assert.Equal(n2, moved.ToNodeId);
    }

    [Fact]
    public async Task Update_SelfLoop_Returns400()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "节点");

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{nodeId}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = (string?)null, predecessorId = nodeId
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("自环", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_PredecessorNotExists_Returns400()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "节点");

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{nodeId}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = (string?)null, predecessorId = $"{Guid.NewGuid()}"
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("前驱节点不存在", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_PredecessorCrossLine_Returns400()
    {
        var lineA = await CreateLineAsync();
        var lineB = await CreateLineAsync();
        var nodeA = await CreateLineNodeAsync(lineA, "A 线节点");
        var nodeB = await CreateLineNodeAsync(lineB, "B 线节点");

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{nodeA}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = (string?)null, predecessorId = nodeB
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("段边两端必须同属一条线路", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_ChangeLine_WithoutEdges_Succeeds()
    {
        var lineA = await CreateLineAsync();
        var lineB = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineA, "无连接节点");

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{nodeId}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = lineB, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var view = await Client.GetEavAsync<LineNodeView>($"/api/LineNode/{nodeId}");
        Assert.Equal(lineB, view!.LineId);
    }

    [Fact]
    public async Task Update_ChangeLine_WithAlienEdges_Returns400()
    {
        // 换线路连带校验：段边两端仍须同线
        var lineA = await CreateLineAsync();
        var lineB = await CreateLineAsync();
        var n1 = await CreateLineNodeAsync(lineA, "节点1");
        var n2 = await CreateLineNodeAsync(lineA, "节点2", pred: n1);

        var resp = await Client.PutAsJsonAsync($"/api/LineNode/{n2}", new
        {
            name = (string?)null, description = (string?)null,
            lineId = lineB, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("仍有段边连接其他线路", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_Unknown_Returns404()
    {
        var resp = await Client.PutAsJsonAsync($"/api/LineNode/LN_{Guid.NewGuid():N}", new
        {
            name = "x", description = (string?)null,
            lineId = (string?)null, predecessorId = (string?)null
        });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // 删除守卫
    // ============================================================

    [Fact]
    public async Task Delete_NoReferences_Returns204()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "待删除");

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/LineNode/{nodeId}")).StatusCode);

        var resp = await Client.GetAsync($"/api/LineNode/{nodeId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var resp = await Client.DeleteAsync($"/api/LineNode/LN_{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_AsSegmentFrom_Returns409()
    {
        var lineId = await CreateLineAsync();
        var n1 = await CreateLineNodeAsync(lineId, "节点1");
        await CreateLineNodeAsync(lineId, "节点2", pred: n1);

        var resp = await Client.DeleteAsync($"/api/LineNode/{n1}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("边/段边", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_AsSegmentTo_Returns409()
    {
        var lineId = await CreateLineAsync();
        var n1 = await CreateLineNodeAsync(lineId, "节点1");
        var n2 = await CreateLineNodeAsync(lineId, "节点2", pred: n1);

        var resp = await Client.DeleteAsync($"/api/LineNode/{n2}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("边/段边", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_AsTerminal_Returns409()
    {
        var lineId = await CreateLineAsync();
        var nodeId = await CreateLineNodeAsync(lineId, "端子节点");

        var term = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/terminals",
            new
            {
                nodeId,
                role = TreeGraph.Shared.LineSky.Entities.TerminalRole.FeedIn,
                operatingState = TreeGraph.Shared.LineSky.Entities.TerminalOperatingState.Maintenance
            });
        Assert.Equal(HttpStatusCode.OK, term.StatusCode);

        var resp = await Client.DeleteAsync($"/api/LineNode/{nodeId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("端子", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 辅助
    // ============================================================

    private async Task<string> CreateLineAsync()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync("/api/Lines", new { id = lineId, name = "测试线路" });
        Assert.True(resp.IsSuccessStatusCode,
            $"创建线路失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return lineId;
    }

    private async Task<string> CreateLineNodeAsync(string lineId, string name, string? pred = null)
    {
        var nodeId = $"{Guid.NewGuid()}";
        var resp = await Client.PostAsJsonAsync("/api/LineNode", new
        {
            id = nodeId, lineId, name, description = (string?)null, predecessorId = pred
        });
        Assert.True(resp.IsSuccessStatusCode,
            $"创建线路节点失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return nodeId;
    }

    private async Task<LineStructureDto?> GetStructureAsync(string lineId)
        => await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
}
