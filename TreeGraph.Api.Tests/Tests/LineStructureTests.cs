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
/// 线路结构（LineStructure）集成测试。
/// 覆盖：空结构语义、三种拓扑模式校验、全量替换、段/端子增量、
///       R4 段自存标识（不再创建/引用通用边）。
/// </summary>
public class LineStructureTests : IntegrationTestBase
{
    public LineStructureTests(EavApiFactory factory) : base(factory) { }

    /// <summary>服务端枚举以字符串传输，测试读取需同样开启字符串枚举转换。</summary>
    private static readonly JsonSerializerOptions EnumJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ============================================================
    // 读取 / 空结构语义
    // ============================================================

    [Fact]
    public async Task Get_UnknownLine_Returns404()
    {
        var resp = await Client.GetAsync($"/api/eav/lines/{Guid.NewGuid():N}/structure");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_NoStructure_ReturnsExistsFalseWithCandidateNodes()
    {
        var (lineId, nodeIds) = await CreateLineWithNodesAsync(3);

        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);

        Assert.NotNull(dto);
        Assert.False(dto!.Exists);
        Assert.Empty(dto.Segments);
        Assert.Equal(3, dto.CandidateNodes.Count);
        Assert.Equal(3, nodeIds.Intersect(dto.CandidateNodes.Select(n => n.NodeId)).Count());
    }

    // ============================================================
    // 三种拓扑模式
    // ============================================================

    [Fact]
    public async Task Create_Linear_Chain_Succeeds()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        Assert.True(dto.Exists);
        Assert.Equal(TopologyMode.Linear, dto.TopologyMode);
        Assert.Equal(2, dto.Segments.Count);
        Assert.Equal(n, dto.OrderedNodeIds);
        Assert.True(dto.Diagnostics.Valid);
        Assert.False(dto.Diagnostics.HasCycle);
        Assert.Equal(2, dto.Diagnostics.NodeDegreeOneCount);
    }

    [Fact]
    public async Task Create_WithoutTopologyMode_DefaultsToNetwork()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        Assert.Equal(TopologyMode.Network, dto.TopologyMode);
    }

    [Fact]
    public async Task Create_Linear_Branch_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[1], n[3]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("Linear", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Radial_WithoutFeedIn_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("FeedIn", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Radial_WithFeedIn_Succeeds_AndOrderStartsAtFeedIn()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(TopologyMode.Radial, dto.TopologyMode);
        Assert.Equal(n[0], dto.OrderedNodeIds[0]);
        Assert.True(dto.Diagnostics.Valid);
        Assert.Equal(1, dto.Diagnostics.FeedInCount);
        var terminal = Assert.Single(dto.Terminals);
        Assert.Equal(TerminalRole.FeedIn, terminal.TerminalRole);
    }

    [Fact]
    public async Task Create_Radial_WithCycle_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("成环", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Network_WithCycle_Succeeds()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(TopologyMode.Network, dto.TopologyMode);
        Assert.True(dto.Diagnostics.Valid);
        Assert.True(dto.Diagnostics.HasCycle);
        Assert.Equal(3, dto.Segments.Count);
    }

    [Fact]
    public async Task Create_Radial_MultipleFeedIn_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn },
                new LineStructureTerminalInput { NodeId = n[2], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("FeedIn", body);
        Assert.Contains("1", body);
    }

    [Fact]
    public async Task Create_Network_WithoutFeedIn_Succeeds()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0]))
        });

        Assert.Equal(TopologyMode.Network, dto.TopologyMode);
        Assert.True(dto.Diagnostics.Valid);
        Assert.True(dto.Diagnostics.HasCycle);
        Assert.Equal(0, dto.Diagnostics.FeedInCount);
        Assert.Empty(dto.Terminals);
    }

    [Fact]
    public async Task Create_Network_Disconnected_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[2], n[3]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("不连通", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_BothSegmentsAndOrderedNodeIds_Returns400()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            Segments = Segments((n[0], n[1])),
            OrderedNodeIds = n.ToList()
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("二选一", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_AnyLine_WithoutEdgeType_Succeeds()
    {
        // R4：线路结构不再依赖边类型 / IsLine 守卫，任意线路可直接建结构
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        Assert.True(dto.Exists);
        Assert.True(dto.Diagnostics.Valid);
        Assert.Single(dto.Segments);
    }

    // ============================================================
    // 全量替换 / 清空
    // ============================================================

    [Fact]
    public async Task Replace_Empty_ClearsStructure()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure", new LineStructureRequest());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists);
    }

    [Fact]
    public async Task Replace_NewMode_RemovesOldSegments()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(4);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(3).ToList()
        });

        var dto = await ReplaceStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[1], n[3])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(TopologyMode.Radial, dto.TopologyMode);
        Assert.Equal(3, dto.Segments.Count);
    }

    // ============================================================
    // 段增量
    // ============================================================

    [Fact]
    public async Task AddSegment_Duplicate_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = n[0], ToNodeId = n[1] });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_LastSegment_RemovesHeaderAndNoGenericEdge()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });
        var edgeId = Assert.Single(created.Segments).EdgeId;
        Assert.False(string.IsNullOrWhiteSpace(edgeId));

        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{edgeId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists);
        Assert.Empty(dto.Segments);
    }

    [Fact]
    public async Task Delete_SegmentOrphaningTerminal_Returns409()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn },
                new LineStructureTerminalInput { NodeId = n[2], Role = TerminalRole.FeedOut }
            ]
        });
        var tailEdge = created.Segments.Single(s => s.FromNodeId == n[1] && s.ToNodeId == n[2]).EdgeId;

        // 删掉末端后 n[2] 仍是端子但已不在结构内 → 拒绝
        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{tailEdge}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("不在结构段端点内", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 端子
    // ============================================================

    [Fact]
    public async Task Terminal_Upsert_ThenDelete()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        var upsert = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/terminals",
            new AddLineTerminalRequest { NodeId = n[2], Role = TerminalRole.FeedOut });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var afterUpsert = await upsert.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.Equal(2, afterUpsert!.Terminals.Count);

        var del = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/terminals/{n[2]}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        var afterDelete = await del.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.Single(afterDelete!.Terminals);
    }

    [Fact]
    public async Task Terminal_NodeNotInStructure_Returns400()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(2).ToList()
        });

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/terminals",
            new AddLineTerminalRequest { NodeId = n[2], Role = TerminalRole.FeedOut });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("必须属于当前结构", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // P0-1: 端子运行状态（OperatingState）正反用例
    // ============================================================

    [Fact]
    public async Task OperatingState_UpsertTerminal_RoundTrip()
    {
        // 增量 upsert 路径 OperatingState 往返
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(3).ToList()
        });

        var upsert = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/terminals",
            new AddLineTerminalRequest
            {
                NodeId = n[0], Role = TerminalRole.FeedIn,
                OperatingState = TerminalOperatingState.Energized
            });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var dto = await upsert.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        var term = Assert.Single(dto!.Terminals);
        Assert.Equal(TerminalOperatingState.Energized, term.OperatingState);
        Assert.Equal(TerminalRole.FeedIn, term.TerminalRole);
    }

    [Fact]
    public async Task OperatingState_FeedInEnergized_One_Succeeds()
    {
        // 正向：恰好 1 个 FeedIn 投运通过
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput
                {
                    NodeId = n[0], Role = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.Energized
                }
            ]
        });

        Assert.True(dto.Diagnostics.Valid);
        Assert.Equal(1, dto.Diagnostics.FeedInEnergizedCount);
        var term = Assert.Single(dto.Terminals);
        Assert.Equal(TerminalOperatingState.Energized, term.OperatingState);
    }

    [Fact]
    public async Task OperatingState_FeedInEnergized_Two_Returns409()
    {
        // 反向：2 个 FeedIn 投运 → 409
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0])),
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

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("投运", body);
        Assert.Contains("FeedIn", body);
        Assert.Contains("2", body);
    }

    [Fact]
    public async Task OperatingState_FeedInNoneEnergized_Succeeds()
    {
        // 正向：FeedIn 存在但都非投运 → 通过（存量场景）
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2])),
            Terminals =
            [
                new LineStructureTerminalInput
                {
                    NodeId = n[0], Role = TerminalRole.FeedIn,
                    OperatingState = TerminalOperatingState.HotStandby
                }
            ]
        });

        Assert.True(dto.Diagnostics.Valid);
        Assert.Equal(0, dto.Diagnostics.FeedInEnergizedCount);
        Assert.Equal(1, dto.Diagnostics.FeedInCount);
    }

    [Fact]
    public async Task OperatingState_DefaultIsMaintenance()
    {
        // 不传 OperatingState 默认 Maintenance
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n,
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        var term = Assert.Single(dto.Terminals);
        Assert.Equal(TerminalOperatingState.Maintenance, term.OperatingState);
    }

    // ============================================================
    // P1-2: Tie 无 FeedIn 时作 DeriveOrder 起点
    // ============================================================

    [Fact]
    public async Task Tie_AsStart_WhenNoFeedIn_OrderStartsAtTie()
    {
        // Network 模式只有 Tie 没有 FeedIn → 顺序以 Tie 为起点
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[2], n[0])),
            Terminals =
            [
                new LineStructureTerminalInput
                {
                    NodeId = n[0], Role = TerminalRole.Tie,
                    OperatingState = TerminalOperatingState.Maintenance
                }
            ]
        });

        Assert.Equal(n[0], dto.OrderedNodeIds[0]);
        Assert.Equal(1, dto.Diagnostics.TieCount);
        Assert.Equal(0, dto.Diagnostics.FeedInCount);
    }

    // ============================================================
    // P1-3: 游离 LineNode Warning
    // ============================================================

    [Fact]
    public async Task OrphanLineNode_GeneratesWarning()
    {
        // 线路有 4 个节点但结构只用了前 3 个 → 最后一个游离节点出 Warning
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(3).ToList()
        });

        Assert.True(dto.Diagnostics.Valid); // Warning 不阻断
        Assert.NotEmpty(dto.Diagnostics.Warnings);
        var joined = string.Join("", dto.Diagnostics.Warnings);
        Assert.Contains("游离", joined);
        Assert.Contains("节点3", joined); // Warning 里是节点名，不是 ID
    }

    // ============================================================
    // P1-5: Warnings 字段非阻断
    // ============================================================

    [Fact]
    public async Task Warnings_DoNotBlock_CreateStillSucceeds()
    {
        // 有游离节点 Warning 但创建仍成功
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[0])) // loop 只用 2 个节点，n[2] 游离
        });

        Assert.True(dto.Exists);
        Assert.True(dto.Diagnostics.Valid); // Valid=true（Errors 为空）
        Assert.NotEmpty(dto.Diagnostics.Warnings); // Warnings 非空但不阻断
    }

    // ============================================================
    // R4: 段与通用边解耦
    // ============================================================

    [Fact]
    public async Task Segments_EdgeIds_AreUniqueAndNonEmpty()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        Assert.Equal(2, created.Segments.Count);
        Assert.All(created.Segments,
            s => Assert.False(string.IsNullOrWhiteSpace(s.EdgeId)));
        Assert.Equal(
            created.Segments.Count,
            created.Segments.Select(s => s.EdgeId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AddSegment_WeightAndDisplayName_RoundTrip()
    {
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(2).ToList()
        });

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new AddLineSegmentRequest
            {
                FromNodeId = n[1],
                ToNodeId = n[2],
                Weight = 12.5m,
                DisplayName = "主线段"
            });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
        Assert.NotNull(dto);
        var segment = Assert.Single(dto!.Segments, s => s.ToNodeId == n[2]);
        Assert.Equal<decimal?>(12.5m, segment.Weight);
        Assert.Equal("主线段", segment.DisplayName);
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

    private async Task<(string LineId, List<string> NodeIds)> CreateLineWithNodesAsync(int count)
    {
        var lineId = Guid.NewGuid().ToString("N");

        var lineResp = await Client.PostAsJsonAsync(
            "/api/Lines", new Line { Id = lineId, Name = "结构测试线路" });
        Assert.True(lineResp.IsSuccessStatusCode,
            $"创建线路失败: {(int)lineResp.StatusCode} {await lineResp.Content.ReadAsStringAsync()}");

        var nodeIds = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var nodeId = Guid.NewGuid().ToString("N");
            var nodeResp = await Client.PostAsJsonAsync(
                "/api/LineNode",
                new LineNode { Id = nodeId, LineId = lineId, Name = $"节点{i}" });
            Assert.True(nodeResp.IsSuccessStatusCode,
                $"创建线路节点失败: {(int)nodeResp.StatusCode} {await nodeResp.Content.ReadAsStringAsync()}");
            nodeIds.Add(nodeId);
        }

        return (lineId, nodeIds);
    }

    private Task<HttpResponseMessage> PostStructureAsync(
        string lineId, LineStructureRequest req)
        => Client.PostAsJsonAsync($"/api/eav/lines/{lineId}/structure", req);

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
