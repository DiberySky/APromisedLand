using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 线路结构（LineStructure）集成测试 —— R7 边集薄层。
/// 节点集 = nodes where line_id=lineId；边集 = 两端均属本线的 relation 值；
/// 拓扑模式已下线；诊断只报告不阻断；段 Weight 已砍（DisplayName → relation.name）。
/// </summary>
public class LineStructureTests : IntegrationTestBase
{
    public LineStructureTests(EavApiFactory factory) : base(factory) { }

    private static readonly JsonSerializerOptions EnumJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ============================================================
    // 读取
    // ============================================================

    [Fact]
    public async Task Get_UnknownLine_Returns404()
    {
        var resp = await Client.GetAsync($"/api/eav/lines/{Guid.NewGuid():N}/structure");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_LineWithoutNodes_ExistsFalse()
    {
        var lineId = await CreateLineAsync();

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists);
        Assert.Empty(dto.Segments);
        Assert.Empty(dto.Nodes);
        Assert.Equal(0, dto.Diagnostics.NodeCount);
    }

    [Fact]
    public async Task Get_LineWithNodesNoEdges_ExistsTrue()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        Assert.True(dto!.Exists); // R7：有节点即存在
        Assert.Empty(dto.Segments);
        Assert.Equal(2, dto.Nodes.Count);
        Assert.Equal(2, dto.OrderedNodeIds.Count);
    }

    // ============================================================
    // 建链 / 链序推导
    // ============================================================

    [Fact]
    public async Task Create_BuildChain_OrderedNodeIdsFollowEdges()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        Assert.True(dto.Exists);
        Assert.Equal(2, dto.Segments.Count);
        Assert.Equal(n, dto.OrderedNodeIds); // 无入边者为起点，沿链推导
        Assert.All(dto.Segments, s => Assert.Equal("line_segment", s.EdgeType));
        Assert.True(dto.Diagnostics.Valid);
        Assert.False(dto.Diagnostics.HasCycle);
        Assert.Equal(2, dto.Diagnostics.NodeDegreeOneCount);
        Assert.Equal(3, dto.Diagnostics.NodeCount);
        Assert.Equal(2, dto.Diagnostics.EdgeCount);
    }

    [Fact]
    public async Task Create_ExistingSegmentEdges_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]))
        });

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[1], n[0]))
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("已存在结构段边", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_SelfLoop_Returns400()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[0]))
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("自环", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_CrossLineSegment_Returns400()
    {
        var (lineA, na) = await CreateLineWithNodesAsync(1);
        var (_, nb) = await CreateLineWithNodesAsync(1);

        var resp = await PostStructureAsync(lineA, new LineStructureRequest
        {
            Segments = Segments((na[0], nb[0]))
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("同属一条线路", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_MultipleOutEdges_SameFrom_Returns400()
    {
        // next 为单值属性：同一 from 只允许一条出边；分支请定义新 relation 属性
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[0], n[2]))
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("有多条出边", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 全量替换 / 清空
    // ============================================================

    [Fact]
    public async Task Replace_Empty_ClearsSegmentsAndTerminals()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure", new LineStructureRequest());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        Assert.True(dto!.Exists); // 节点仍在，仅边/端子清空
        Assert.Empty(dto.Segments);
        Assert.Empty(dto.Terminals);
    }

    [Fact]
    public async Task Replace_NewSegments_RemovesOldOnes()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(4);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        var dto = await ReplaceStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[3]), (n[3], n[2]))
        });

        Assert.Equal(2, dto.Segments.Count);
        Assert.Contains(dto.Segments, s => s.FromNodeId == n[0] && s.ToNodeId == n[3]);
        Assert.Contains(dto.Segments, s => s.FromNodeId == n[3] && s.ToNodeId == n[2]);
        Assert.DoesNotContain(dto.Segments, s => s.ToNodeId == n[1]);
    }

    [Fact]
    public async Task Replace_DisplayName_RoundTrips()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments =
            [
                new LineStructureSegmentInput
                {
                    FromNodeId = n[0], ToNodeId = n[1], DisplayName = "主线段"
                }
            ]
        });

        var seg = Assert.Single(dto.Segments);
        Assert.Equal("主线段", seg.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(seg.EdgeId));
    }

    // ============================================================
    // 单段加/删
    // ============================================================

    [Fact]
    public async Task AddSegment_SameFrom_ReplacesOldEdge()
    {
        // 单值语义：同 from 再写 next → 旧边被替换（不会两条出边）
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]))
        });

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = n[0], ToNodeId = n[2] });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        var seg = Assert.Single(dto!.Segments);
        Assert.Equal(n[0], seg.FromNodeId);
        Assert.Equal(n[2], seg.ToNodeId);
    }

    [Fact]
    public async Task AddSegment_SelfLoop_Returns400()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = n[0], ToNodeId = n[0] });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("自环", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddSegment_CrossLine_Returns400()
    {
        var (lineA, na) = await CreateLineWithNodesAsync(1);
        var (_, nb) = await CreateLineWithNodesAsync(1);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineA}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = na[0], ToNodeId = nb[0] });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("同属一条线路", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AddSegment_UnknownLine_Returns404()
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{Guid.NewGuid():N}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = "a", ToNodeId = "b" });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteSegment_RoundTrip()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });
        var edgeId = created.Segments
            .Single(s => s.FromNodeId == n[1] && s.ToNodeId == n[2]).EdgeId;

        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{edgeId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        var seg = Assert.Single(dto!.Segments);
        Assert.Equal(n[0], seg.FromNodeId);
        Assert.Equal(n[1], seg.ToNodeId);
    }

    [Fact]
    public async Task DeleteSegment_UnknownEdge_Returns404()
    {
        var (lineId, _) = await CreateLineWithNodesAsync(1);

        // 四元组编码格式合法但边不存在 → 404
        var edgeId = $"line_segment:{Guid.NewGuid()}:0:{Guid.NewGuid()}";
        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{edgeId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteSegment_EdgeNotInThisLine_Returns400()
    {
        var (lineA, na) = await CreateLineWithNodesAsync(2);
        var (lineB, _) = await CreateLineWithNodesAsync(1);
        var created = await CreateStructureOkAsync(lineA, new LineStructureRequest
        {
            Segments = Segments((na[0], na[1]))
        });
        var edgeId = Assert.Single(created.Segments).EdgeId;

        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineB}/structure/segments/{edgeId}");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("不属于线路", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 多边类型分支（自定义 EdgeType = 新边类型，经 Edge API 写入）
    // ============================================================

    [Fact]
    public async Task BranchEdgeType_ShowsAsSecondEdgeType()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]))
        });

        // 用户自定义分支边类型（顶级 EdgeType，对齐 Nebula CREATE EDGE）
        var branchType = $"branch_{Guid.NewGuid():N}";
        var typeResp = await Client.PostAsJsonAsync("/api/edge/types", new
        {
            name = branchType,
            props = new[] { new { propName = "name", dataType = "STRING", nullable = true } }
        });
        Assert.True(typeResp.IsSuccessStatusCode, await typeResp.Content.ReadAsStringAsync());

        // n1 → n2 写分支边（UPSERT 覆盖语义）
        var put = await Client.PutAsJsonAsync(
            $"/api/edge/edges/{branchType}/{n[1]}/0/{n[2]}",
            new { props = new Dictionary<string, object?> { ["name"] = "分支段" } });
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());

        var dto = await GetStructureAsync(lineId);
        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Segments.Count);
        var segment = Assert.Single(dto.Segments, s => s.EdgeType == "line_segment");
        Assert.Equal(n[0], segment.FromNodeId);
        Assert.Equal(n[1], segment.ToNodeId);
        var branch = Assert.Single(dto.Segments, s => s.EdgeType == branchType);
        Assert.Equal(n[1], branch.FromNodeId);
        Assert.Equal(n[2], branch.ToNodeId);
        Assert.Equal("分支段", branch.DisplayName);
    }

    // ============================================================
    // 端子
    // ============================================================

    [Fact]
    public async Task Terminal_Upsert_ThenDelete()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        var upsert = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/terminals",
            new AddLineTerminalRequest
            {
                NodeId = n[1], Role = TerminalRole.FeedOut,
                OperatingState = TerminalOperatingState.HotStandby
            });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var afterUpsert = await upsert.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.Equal(2, afterUpsert!.Terminals.Count);
        var feedOut = Assert.Single(afterUpsert.Terminals, t => t.NodeId == n[1]);
        Assert.Equal(TerminalOperatingState.HotStandby, feedOut.OperatingState);

        var del = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/terminals/{n[1]}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        var afterDelete = await del.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.Single(afterDelete!.Terminals);
    }

    [Fact]
    public async Task Terminal_TwoEnergizedFeedIn_Returns400()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput
                {
                    NodeId = n[0], Role = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized
                },
                new LineStructureTerminalInput
                {
                    NodeId = n[2], Role = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized
                }
            ]
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("同时投运的 FeedIn 端子最多 1 个",
            await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Terminal_NodeNotInLine_Returns400()
    {
        var (lineA, _) = await CreateLineWithNodesAsync(1);
        var (_, nb) = await CreateLineWithNodesAsync(1);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineA}/structure/terminals",
            new AddLineTerminalRequest { NodeId = nb[0], Role = TerminalRole.FeedIn });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("不属于线路", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Terminal_DefaultOperatingState_IsMaintenance()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        var term = Assert.Single(dto.Terminals);
        Assert.Equal(TerminalOperatingState.Maintenance, term.OperatingState);
    }

    // ============================================================
    // 诊断（只报告不阻断）
    // ============================================================

    [Fact]
    public async Task Diagnostics_Cycle_StillValid_WithWarning()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0]))
        });

        Assert.True(dto.Diagnostics.Valid); // 成环只提示，不阻断
        Assert.True(dto.Diagnostics.HasCycle);
        Assert.Contains(dto.Diagnostics.Warnings, w => w.Contains("环"));
    }

    [Fact]
    public async Task Diagnostics_Disconnected_WarningOnly()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[2], n[3]))
        });

        Assert.True(dto.Diagnostics.Valid);
        Assert.False(dto.Diagnostics.HasCycle);
        Assert.Contains(dto.Diagnostics.Warnings, w => w.Contains("不连通"));
    }

    [Fact]
    public async Task Diagnostics_TerminalCounts()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput
                {
                    NodeId = n[0], Role = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized
                },
                new LineStructureTerminalInput
                {
                    NodeId = n[2], Role = TerminalRole.Tie
                }
            ]
        });

        Assert.Equal(1, dto.Diagnostics.FeedInCount);
        Assert.Equal(1, dto.Diagnostics.FeedInEnergizedCount);
        Assert.Equal(1, dto.Diagnostics.TieCount);
        Assert.True(dto.Diagnostics.Valid);
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private static List<LineStructureSegmentInput> Segments(
        params (string From, string To)[] pairs)
        => pairs.Select(p => new LineStructureSegmentInput
        {
            FromNodeId = p.From,
            ToNodeId = p.To
        }).ToList();

    private async Task<string> CreateLineAsync()
    {
        var lineId = $"L_{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync(
            "/api/Lines", new { id = lineId, name = "结构测试线路" });
        Assert.True(resp.IsSuccessStatusCode,
            $"创建线路失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return lineId;
    }

    private async Task<(string LineId, List<string> NodeIds)> CreateLineWithNodesAsync(int count)
    {
        var lineId = await CreateLineAsync();
        var nodeIds = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var nodeId = $"{Guid.NewGuid()}";
            var resp = await Client.PostAsJsonAsync("/api/LineNode", new
            {
                id = nodeId, lineId, name = $"节点{i}",
                description = (string?)null, predecessorId = (string?)null
            });
            Assert.True(resp.IsSuccessStatusCode,
                $"创建线路节点失败: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
            nodeIds.Add(nodeId);
        }
        return (lineId, nodeIds);
    }

    private Task<HttpResponseMessage> PostStructureAsync(
        string lineId, LineStructureRequest req)
        => Client.PostAsJsonAsync($"/api/eav/lines/{lineId}/structure", req);

    private async Task<LineStructureDto?> GetStructureAsync(string lineId)
        => await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);

    private async Task<LineStructureDto> CreateStructureOkAsync(
        string lineId, LineStructureRequest req)
    {
        var resp = await PostStructureAsync(lineId, req);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            $"创建线路结构失败: {(int)resp.StatusCode} {body}");
        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task<LineStructureDto> ReplaceStructureOkAsync(
        string lineId, LineStructureRequest req)
    {
        var resp = await Client.PutAsJsonAsync($"/api/eav/lines/{lineId}/structure", req);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            $"替换线路结构失败: {(int)resp.StatusCode} {body}");
        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        return dto!;
    }
}
