using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.LineSky.Dtos;
using TreeGraph.Shared.LineSky.Entities;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 线路结构（LineStructure）集成测试。
/// 覆盖：空结构语义、三种拓扑模式校验、全量替换、段/端子增量、IsLine 守卫。
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
        var et = await CreateLineTypeAsync("LSLinear");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSDefaultMode");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        Assert.Equal(TopologyMode.Network, dto.TopologyMode);
    }

    [Fact]
    public async Task Create_Linear_Branch_Returns409()
    {
        var et = await CreateLineTypeAsync("LSLinearBranch");
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[1], n[3]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("Linear", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Radial_WithoutFeedIn_Returns409()
    {
        var et = await CreateLineTypeAsync("LSRadialNoFeed");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("FeedIn", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_Radial_WithFeedIn_Succeeds_AndOrderStartsAtFeedIn()
    {
        var et = await CreateLineTypeAsync("LSRadial");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSRadialCycle");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSNetwork");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSRadialMultiFeed");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSNetworkNoFeed");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSNetworkDisconnected");
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[2], n[3]))
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("不连通", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_BothSegmentsAndOrderedNodeIds_Returns400()
    {
        var et = await CreateLineTypeAsync("LSBothInputs");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            Segments = Segments((n[0], n[1])),
            OrderedNodeIds = n.ToList()
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("二选一", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_NonLineEdgeType_Returns400()
    {
        var et = await CreateEdgeTypeAsync("LSNonLine", isLine: false);
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("IsLine=false", await resp.Content.ReadAsStringAsync());
    }

    // ============================================================
    // 全量替换 / 清空
    // ============================================================

    [Fact]
    public async Task Replace_Empty_ClearsStructure()
    {
        var et = await CreateLineTypeAsync("LSClear");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure", new LineStructureRequest());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists);

        // 旧结构的边实例一并删除
        var edges = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");
        Assert.NotNull(edges);
        Assert.Empty(edges!);
    }

    [Fact]
    public async Task Replace_NewMode_RemovesOldSegments()
    {
        var et = await CreateLineTypeAsync("LSReplace");
        var (lineId, n) = await CreateLineWithNodesAsync(4);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.Take(3).ToList()
        });

        var dto = await ReplaceStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Radial,
            Segments = Segments((n[0], n[1]), (n[1], n[2]), (n[1], n[3])),
            Terminals =
            [
                new LineStructureTerminalInput { NodeId = n[0], Role = TerminalRole.FeedIn }
            ]
        });

        Assert.Equal(TopologyMode.Radial, dto.TopologyMode);
        Assert.Equal(3, dto.Segments.Count);

        var edges = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");
        Assert.NotNull(edges);
        Assert.Equal(3, edges!.Count);
    }

    // ============================================================
    // 段增量
    // ============================================================

    [Fact]
    public async Task AddSegment_Duplicate_Returns409()
    {
        var et = await CreateLineTypeAsync("LSDup");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/lines/{lineId}/structure/segments",
            new AddLineSegmentRequest { FromNodeId = n[0], ToNodeId = n[1] });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_LastSegment_RemovesHeaderAndEdge()
    {
        var et = await CreateLineTypeAsync("LSDelLast");
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });
        var edgeId = Assert.Single(created.Segments).EdgeId;

        var resp = await Client.DeleteAsync(
            $"/api/eav/lines/{lineId}/structure/segments/{edgeId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadEavAsync<LineStructureDto>(EnumJson);
        Assert.NotNull(dto);
        Assert.False(dto!.Exists);

        // 边实例同步删除
        var edgeResp = await Client.GetAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/{edgeId}");
        Assert.Equal(HttpStatusCode.NotFound, edgeResp.StatusCode);
    }

    [Fact]
    public async Task Delete_SegmentOrphaningTerminal_Returns409()
    {
        var et = await CreateLineTypeAsync("LSDelOrphan");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSTerm");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("LSTermOut");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OSUpsert");
        var (lineId, n) = await CreateLineWithNodesAsync(3);
        await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OSOne");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OSTwo");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var resp = await PostStructureAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OSNone");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OSDefault");
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("TieStart");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("OrphanWarn");
        var (lineId, n) = await CreateLineWithNodesAsync(4);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
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
        var et = await CreateLineTypeAsync("WarnOK");
        var (lineId, n) = await CreateLineWithNodesAsync(3);

        var dto = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Network,
            Segments = Segments((n[0], n[1]), (n[1], n[0])) // loop 只用 2 个节点，n[2] 游离
        });

        Assert.True(dto.Exists);
        Assert.True(dto.Diagnostics.Valid); // Valid=true（Errors 为空）
        Assert.NotEmpty(dto.Diagnostics.Warnings); // Warnings 非空但不阻断
    }

    // ============================================================
    // IsLine 守卫
    // ============================================================

    [Fact]
    public async Task OrdinaryEdge_Create_ForLineType_Returns409()
    {
        var et = await CreateLineTypeAsync("LSGuardCreate");
        var (lineId, n) = await CreateLineWithNodesAsync(2);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest { FromNodeId = n[0], ToNodeId = n[1] });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("结构", await resp.Content.ReadAsStringAsync());

        // 结构未被创建
        var dto = await Client.GetEavAsync<LineStructureDto>(
            $"/api/eav/lines/{lineId}/structure", EnumJson);
        Assert.False(dto!.Exists);
    }

    [Fact]
    public async Task OrdinaryEdge_Delete_WhenReferencedByStructure_Returns409()
    {
        var et = await CreateLineTypeAsync("LSGuardDelete");
        var (lineId, n) = await CreateLineWithNodesAsync(2);
        var created = await CreateStructureOkAsync(lineId, new LineStructureRequest
        {
            EdgeTypeId = et.EdgeTypeId,
            TopologyMode = TopologyMode.Linear,
            OrderedNodeIds = n.ToList()
        });
        var edgeId = Assert.Single(created.Segments).EdgeId;

        var resp = await Client.DeleteAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/{edgeId}");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("线路结构", await resp.Content.ReadAsStringAsync());
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

    private async Task<EdgeTypeDto> CreateLineTypeAsync(
        string prefix, bool isDirected = true, bool isLine = true)
    {
        var typeName = $"{prefix}_{Guid.NewGuid():N}";
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = $"线路类型_{typeName}",
                IsGlobal = true,
                IsDirected = isDirected,
                IsLine = isLine,
                IsLinear = true
            });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task<EdgeTypeDto> CreateEdgeTypeAsync(string prefix, bool isLine)
        => await CreateLineTypeAsync(prefix, isDirected: true, isLine: isLine);

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
