using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// Edge（关系/边）集成测试。
/// 覆盖：EdgeDefinition CRUD、EdgeInstance CRUD + UNIQUE 约束、
///       EAV 属性集成、图查询（FindOutgoing/FindIncoming）、BFS 遍历、级联删除。
/// </summary>
public class EdgeTests : IntegrationTestBase
{
    public EdgeTests(EavApiFactory factory) : base(factory) { }

    private static string NewTypeName(string prefix = "EdgeTest")
        => $"{prefix}_{Guid.NewGuid():N}";

    // ============================================================
    // Edge Definition CRUD
    // ============================================================

    [Fact]
    public async Task EdgeType_Create_ThenGet_ReturnsSame()
    {
        var typeName = NewTypeName();
        var displayName = $"测试关系_{Guid.NewGuid():N}";

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = displayName,
                IsGlobal = true,
                IsDirected = true,
                Description = "集成测试创建"
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(created);
        Assert.Equal(typeName, created!.TypeName);
        Assert.Equal(displayName, created.DisplayName);
        Assert.True(created.IsGlobal);
        Assert.True(created.IsDirected);

        // GetById
        var got = await Client.GetEavAsync<EdgeTypeDto>(
            $"/api/eav/metadata/edge-types/{created.EdgeTypeId}");
        Assert.NotNull(got);
        Assert.Equal(created.EdgeTypeId, got!.EdgeTypeId);
        Assert.Equal(typeName, got.TypeName);
    }

    [Fact]
    public async Task EdgeType_Create_DuplicateTypeName_Returns409()
    {
        var typeName = NewTypeName();

        await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest { TypeName = typeName, DisplayName = "a" });

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest { TypeName = typeName, DisplayName = "b" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task EdgeType_Create_EmptyTypeName_Returns400()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest { TypeName = "", DisplayName = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task EdgeType_List_ReturnsAllCreated()
    {
        var t1 = await CreateEdgeTypeAsync("ListA");
        var t2 = await CreateEdgeTypeAsync("ListB");

        var list = await Client.GetEavAsync<List<EdgeTypeDto>>(
            "/api/eav/metadata/edge-types");

        Assert.NotNull(list);
        Assert.True(list!.Count >= 2);
        Assert.Contains(list, t => t.TypeName == t1.TypeName);
        Assert.Contains(list, t => t.TypeName == t2.TypeName);
    }

    [Fact]
    public async Task EdgeType_Update_ChangesFields()
    {
        var created = await CreateEdgeTypeAsync();

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/metadata/edge-types/{created.EdgeTypeId}",
            new UpdateEdgeTypeRequest
            {
                DisplayName = "新名称",
                IsDirected = false,
                Description = "更新后"
            });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(updated);
        Assert.Equal("新名称", updated!.DisplayName);
        Assert.False(updated.IsDirected);
        Assert.Equal("更新后", updated.Description);
        // TypeName 不可改
        Assert.Equal(created.TypeName, updated.TypeName);
    }

    [Fact]
    public async Task EdgeType_Delete_Unknown_Returns404()
    {
        var resp = await Client.DeleteAsync(
            $"/api/eav/metadata/edge-types/{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // Edge Instance CRUD
    // ============================================================

    [Fact]
    public async Task Edge_Create_ThenGet_ReturnsSame()
    {
        var et = await CreateEdgeTypeAsync();
        var (fromNode, toNode) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        var createResp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = fromNode,
                ToNodeId = toNode,
                Weight = 3.1415m
            });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(created);
        Assert.Equal(fromNode, created!.FromNodeId);
        Assert.Equal(toNode, created.ToNodeId);
        Assert.Equal(3.1415m, created.Weight);
        // 服务端应填充类型名/显示名
        Assert.Equal(et.TypeName, created.EdgeTypeName);
        Assert.Equal(et.DisplayName, created.EdgeTypeDisplayName);

        // GetById
        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{et.EdgeTypeId}/{created.EdgeId}");
        Assert.NotNull(got);
        Assert.Equal(created.EdgeId, got!.EdgeId);
    }

    [Fact]
    public async Task Edge_Create_UnknownEdgeType_Returns404()
    {
        var unknownId = Guid.NewGuid().ToString("N");
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{unknownId}",
            new CreateEdgeRequest
            {
                FromNodeId = "a",
                ToNodeId = "b"
            });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Edge_Create_Duplicate_Returns409()
    {
        var et = await CreateEdgeTypeAsync();
        var (fromNode, toNode) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        var first = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest { FromNodeId = fromNode, ToNodeId = toNode });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest { FromNodeId = fromNode, ToNodeId = toNode });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Edge_Create_SameNodes_DifferentTypes_Allowed()
    {
        var et1 = await CreateEdgeTypeAsync("TypeA");
        var et2 = await CreateEdgeTypeAsync("TypeB");
        var (fromNode, toNode) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        var r1 = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et1.EdgeTypeId}",
            new CreateEdgeRequest { FromNodeId = fromNode, ToNodeId = toNode });
        var r2 = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et2.EdgeTypeId}",
            new CreateEdgeRequest { FromNodeId = fromNode, ToNodeId = toNode });

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
    }

    [Fact]
    public async Task Edge_List_ByType_ReturnsAll()
    {
        var et = await CreateEdgeTypeAsync();
        var nodeA = Guid.NewGuid().ToString("N");
        var nodeB = Guid.NewGuid().ToString("N");
        var nodeC = Guid.NewGuid().ToString("N");

        await CreateEdgeAsync(et.EdgeTypeId, nodeA, nodeB);
        await CreateEdgeAsync(et.EdgeTypeId, nodeB, nodeC);

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Edge_Update_Weight_ChangesValue()
    {
        var et = await CreateEdgeTypeAsync();
        var nodeA = Guid.NewGuid().ToString("N");
        var nodeB = Guid.NewGuid().ToString("N");
        var edge = await CreateEdgeAsync(et.EdgeTypeId, nodeA, nodeB, weight: 1.0m);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}",
            new UpdateEdgeRequest { Weight = 99.99m });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(updated);
        Assert.Equal(99.99m, updated!.Weight);
    }

    [Fact]
    public async Task Edge_Delete_ThenGet_Returns404()
    {
        var et = await CreateEdgeTypeAsync();
        var edge = await CreateEdgeAsync(et.EdgeTypeId, "nodeX", "nodeY");

        var del = await Client.DeleteAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var resp = await Client.GetAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // 图查询：FindOutgoing / FindIncoming
    // ============================================================

    [Fact]
    public async Task EdgeGraph_Outgoing_ReturnsOnlyOutgoing()
    {
        var et = await CreateEdgeTypeAsync();
        var center = Guid.NewGuid().ToString("N");
        var out1 = Guid.NewGuid().ToString("N");
        var out2 = Guid.NewGuid().ToString("N");
        var in1 = Guid.NewGuid().ToString("N");

        // center → out1, center → out2
        await CreateEdgeAsync(et.EdgeTypeId, center, out1);
        await CreateEdgeAsync(et.EdgeTypeId, center, out2);
        // in1 → center（这条不应出现在 outgoing 中）
        await CreateEdgeAsync(et.EdgeTypeId, in1, center);

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/from/{center}");

        Assert.NotNull(outgoing);
        Assert.Equal(2, outgoing!.Count);
        Assert.All(outgoing, e => Assert.Equal(center, e.FromNodeId));
    }

    [Fact]
    public async Task EdgeGraph_Incoming_ReturnsOnlyIncoming()
    {
        var et = await CreateEdgeTypeAsync();
        var center = Guid.NewGuid().ToString("N");
        var out1 = Guid.NewGuid().ToString("N");
        var in1 = Guid.NewGuid().ToString("N");
        var in2 = Guid.NewGuid().ToString("N");

        await CreateEdgeAsync(et.EdgeTypeId, center, out1);
        await CreateEdgeAsync(et.EdgeTypeId, in1, center);
        await CreateEdgeAsync(et.EdgeTypeId, in2, center);

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/to/{center}");

        Assert.NotNull(incoming);
        Assert.Equal(2, incoming!.Count);
        Assert.All(incoming, e => Assert.Equal(center, e.ToNodeId));
    }

    [Fact]
    public async Task EdgeGraph_NoEdges_ReturnsEmpty()
    {
        var et = await CreateEdgeTypeAsync();
        var noEdgeNode = Guid.NewGuid().ToString("N");

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/from/{noEdgeNode}");
        Assert.Empty(outgoing!);

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/to/{noEdgeNode}");
        Assert.Empty(incoming!);
    }

    // ============================================================
    // BFS 遍历
    // ============================================================

    [Fact]
    public async Task Traverse_Out_Depth2_VisitsAllNeighbors()
    {
        // 图: A → B → C
        var et = await CreateEdgeTypeAsync();
        string A = "T_A", B = "T_B", C = "T_C";
        await CreateEdgeAsync(et.EdgeTypeId, A, B);
        await CreateEdgeAsync(et.EdgeTypeId, B, C);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: A, Direction: "out", MaxDepth: 2));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);

        // visited 应包含 A, B, C
        Assert.Contains(A, result!.VisitedNodeIds);
        Assert.Contains(B, result.VisitedNodeIds);
        Assert.Contains(C, result.VisitedNodeIds);
        Assert.Equal(3, result.VisitedNodeIds.Count);

        // edges 应包含两条
        Assert.Equal(2, result.Edges.Count);
    }

    [Fact]
    public async Task Traverse_In_Depth1_FindsPredecessors()
    {
        // 图: X → A, Y → A
        var et = await CreateEdgeTypeAsync();
        string A = "T2_A", X = "T2_X", Y = "T2_Y";
        await CreateEdgeAsync(et.EdgeTypeId, X, A);
        await CreateEdgeAsync(et.EdgeTypeId, Y, A);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: A, Direction: "in", MaxDepth: 1));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);

        Assert.Equal(2, result!.Edges.Count);
        Assert.Contains(result.VisitedNodeIds, n => n == A);
        Assert.Contains(result.VisitedNodeIds, n => n == X);
        Assert.Contains(result.VisitedNodeIds, n => n == Y);
    }

    [Fact]
    public async Task Traverse_Both_HandlesBidirectional()
    {
        var et = await CreateEdgeTypeAsync();
        string A = "T3_A", B = "T3_B", C = "T3_C";
        await CreateEdgeAsync(et.EdgeTypeId, A, B);
        await CreateEdgeAsync(et.EdgeTypeId, C, B);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: B, Direction: "both", MaxDepth: 1));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Edges.Count);
        Assert.Contains(result.VisitedNodeIds, n => n == A);
        Assert.Contains(result.VisitedNodeIds, n => n == C);
    }

    // ============================================================
    // 无向关系（IsDirected=false）的对称化展开
    // ============================================================

    [Fact]
    public async Task Traverse_Undirected_IgnoresDirection()
    {
        // 无向关系：边存储为 A → B，但从 B 出发按 "out" 亦应走到 A
        var et = await CreateEdgeTypeAsync("Undir", isDirected: false);
        string A = "UD_A", B = "UD_B";
        await CreateEdgeAsync(et.EdgeTypeId, A, B);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: B, Direction: "out", MaxDepth: 1));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);
        Assert.Single(result!.Edges);
        Assert.Contains(A, result.VisitedNodeIds);
    }

    [Fact]
    public async Task EdgeGraph_Undirected_OutgoingReturnsConnectedEdge()
    {
        // 无向：查 B 的出边应包含与此节点相连的边（存储为 A → B）
        var et = await CreateEdgeTypeAsync("UndirOut", isDirected: false);
        await CreateEdgeAsync(et.EdgeTypeId, "UDO_A", "UDO_B");

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/from/UDO_B");

        Assert.NotNull(outgoing);
        Assert.Single(outgoing!);
    }

    [Fact]
    public async Task EdgeGraph_Undirected_IncomingReturnsConnectedEdge()
    {
        // 无向：查 A 的入边应包含与此节点相连的边（存储为 A → B）
        var et = await CreateEdgeTypeAsync("UndirIn", isDirected: false);
        await CreateEdgeAsync(et.EdgeTypeId, "UDI_A", "UDI_B");

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}/to/UDI_A");

        Assert.NotNull(incoming);
        Assert.Single(incoming!);
    }

    [Fact]
    public async Task Traverse_Depth1_OnlyFirstHop()
    {
        // 图: A → B → C → D（深度 3）
        var et = await CreateEdgeTypeAsync();
        string A = "T4_A", B = "T4_B", C = "T4_C", D = "T4_D";
        await CreateEdgeAsync(et.EdgeTypeId, A, B);
        await CreateEdgeAsync(et.EdgeTypeId, B, C);
        await CreateEdgeAsync(et.EdgeTypeId, C, D);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: A, Direction: "out", MaxDepth: 1));

        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);
        // 深度 1: 只拿到 A→B，visited = {A, B}
        Assert.Single(result!.Edges);
        Assert.Equal(2, result.VisitedNodeIds.Count);
    }

    [Fact]
    public async Task Traverse_CyclicGraph_NoInfiniteLoop()
    {
        // 图: A → B → C → A（环）
        var et = await CreateEdgeTypeAsync();
        string A = "T5_A", B = "T5_B", C = "T5_C";
        await CreateEdgeAsync(et.EdgeTypeId, A, B);
        await CreateEdgeAsync(et.EdgeTypeId, B, C);
        await CreateEdgeAsync(et.EdgeTypeId, C, A);

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}/traverse",
            new EdgeTraverseRequest(StartNodeId: A, Direction: "out", MaxDepth: 10));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);
        // visited 应去重，edges 也应去重
        Assert.Equal(3, result!.VisitedNodeIds.Count);
        Assert.Equal(3, result.Edges.Count);
    }

    // ============================================================
    // EAV 属性集成
    // ============================================================

    [Fact]
    public async Task Edge_WithEavProperties_RoundTrips()
    {
        var et = await CreateEdgeTypeAsync();

        // 为该 EdgeType 创建属性定义（dataType 必须是小写 decimal）
        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType = et.TypeName,
                attributeName = "strength",
                displayName = "强度",
                dataType = "decimal",
                isRequired = false
            });
        Assert.True(attrResp.IsSuccessStatusCode,
            $"属性创建失败: {await attrResp.Content.ReadAsStringAsync()}");

        var (fromNode, toNode) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));

        // 先创建边实例（不带 Properties）
        var createResp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = fromNode,
                ToNodeId = toNode
            });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(created);

        // 通过 EAV PUT 端点更新属性（复用标准管道）
        var putResp = await Client.PutAsJsonAsync(
            $"/api/eav/{et.TypeName}/entities/{created!.EdgeId}",
            new { strength = 42.5 });
        Assert.True(putResp.IsSuccessStatusCode,
            $"EAV PUT 失败: {await putResp.Content.ReadAsStringAsync()}");

        // GET 边应能取回 EAV 属性
        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{et.EdgeTypeId}/{created.EdgeId}");
        Assert.NotNull(got);
        Assert.NotNull(got!.Properties);
        Assert.True(got.Properties!.TryGetValue("strength", out var strength));
        Assert.Equal(42.5m, strength.GetDecimal());
    }

    // ============================================================
    // 级联删除
    // ============================================================

    [Fact]
    public async Task EdgeType_Delete_CascadesInstances()
    {
        var et = await CreateEdgeTypeAsync();
        await CreateEdgeAsync(et.EdgeTypeId, "n1", "n2");
        await CreateEdgeAsync(et.EdgeTypeId, "n3", "n4");

        // 确认有实例
        var beforeDelete = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");
        Assert.Equal(2, beforeDelete!.Count);

        // 删除类型
        var delResp = await Client.DeleteAsync(
            $"/api/eav/metadata/edge-types/{et.EdgeTypeId}");
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);

        var delResult = await delResp.Content.ReadEavAsync<EdgeDeleteResult>();
        Assert.NotNull(delResult);
        Assert.True(delResult!.Deleted);
        Assert.Equal(2, delResult.InstancesDeleted);

        // 确认类型已不存在
        var getResp = await Client.GetAsync(
            $"/api/eav/metadata/edge-types/{et.EdgeTypeId}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ============================================================
    // IsGlobal=false 的节点类型约束
    // ============================================================

    [Fact]
    public async Task EdgeType_Create_NonGlobalWithoutNodeTypes_Returns400()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = NewTypeName("EdgeNonGlobal"),
                DisplayName = "非全局无边类型",
                IsGlobal = false,
                IsDirected = true
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task EdgeType_Create_NonGlobalWithNodeTypes_Succeeds()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = NewTypeName("EdgeNonGlobalOk"),
                DisplayName = "非全局限定类型",
                IsGlobal = false,
                IsDirected = true,
                FromNodeType = "UnitTree",
                ToNodeType = "UnitTree"
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        Assert.False(dto!.IsGlobal);
        Assert.Equal("UnitTree", dto.FromNodeType);
    }

    // ============================================================
    // 无向链（IsDirected=false）双向查询
    // ============================================================

    [Fact]
    public async Task Chain_Undirected_DownstreamAndUpstream_BothCoverWholeChain()
    {
        // 无向链 A—B—C：从中间 B 出发，up/down 都应覆盖整条链（方向被忽略）
        var et = await CreateEdgeTypeAsync(
            "UndirChain", isDirected: false, isChain: true, isLinear: true);
        await CreateChainAsync(et.EdgeTypeId, "UC_A", "UC_B", "UC_C");

        var down = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/downstream/UC_B");
        Assert.NotNull(down);
        Assert.Equal(2, down!.Edges.Count);
        Assert.Contains("UC_A", down.NodeIds);
        Assert.Contains("UC_C", down.NodeIds);

        var up = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/upstream/UC_B");
        Assert.NotNull(up);
        Assert.Equal(2, up!.Edges.Count);
    }

    [Fact]
    public async Task Chain_Undirected_FullPath_ReturnsWholeChain()
    {
        var et = await CreateEdgeTypeAsync(
            "UndirChainFull", isDirected: false, isChain: true, isLinear: true);
        await CreateChainAsync(et.EdgeTypeId, "UCF_A", "UCF_B", "UCF_C");

        var path = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/path/UCF_B");
        Assert.NotNull(path);
        Assert.Equal(2, path!.Edges.Count);
        Assert.Equal(3, path.NodeIds.Distinct().Count());
    }

    [Fact]
    public async Task Chain_Directed_Downstream_StillRespectsDirection()
    {
        // 回归：有向链从中间出发，down 只覆盖下游（B→C），不含上游 A
        var et = await CreateEdgeTypeAsync(
            "DirChain", isDirected: true, isChain: true, isLinear: true);
        await CreateChainAsync(et.EdgeTypeId, "DC_A", "DC_B", "DC_C");

        var down = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/downstream/DC_B");
        Assert.NotNull(down);
        Assert.Single(down!.Edges);
        Assert.DoesNotContain("DC_A", down.NodeIds);
        Assert.Contains("DC_C", down.NodeIds);
    }

    // ============================================================
    // 缺陷回归：链创建事务 与 递归 CTE
    // ============================================================

    [Fact]
    public async Task Chain_Create_MultiSegment_PersistsAllEdges()
    {
        // 回归：CreateChainAsync 曾用裸事务与 NpgsqlRetryingExecutionStrategy 冲突，
        // 多段建链直接抛"does not support user-initiated transactions"。
        // 现事务经 CreateExecutionStrategy 包裹，5 节点 4 段应全部落库。
        var et = await CreateEdgeTypeAsync("ChainTx", isChain: true);
        await CreateChainAsync(et.EdgeTypeId, "TX_A", "TX_B", "TX_C", "TX_D", "TX_E");

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");
        Assert.NotNull(list);
        Assert.Equal(4, list!.Count);

        var fromTos = list!.Select(e => (e.FromNodeId, e.ToNodeId)).ToList();
        Assert.Contains(("TX_A", "TX_B"), fromTos);
        Assert.Contains(("TX_B", "TX_C"), fromTos);
        Assert.Contains(("TX_C", "TX_D"), fromTos);
        Assert.Contains(("TX_D", "TX_E"), fromTos);
    }

    [Fact]
    public async Task Chain_Create_MidTransactionFailure_RollsBackEarlierSegments()
    {
        // 回归：第 1 段在事务内插入成功、第 2 段命中重复边校验失败，
        // 整体应回滚（A→B 不残留）；失败原因为业务校验而非策略异常。
        var et = await CreateEdgeTypeAsync("ChainRb", isChain: true);
        await CreateEdgeAsync(et.EdgeTypeId, "RB_B", "RB_C");

        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/chains/{et.EdgeTypeId}/create",
            new CreateChainRequest { OrderedNodeIds = new List<string> { "RB_A", "RB_B", "RB_C" } });
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("已存在", body);

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/{et.EdgeTypeId}");
        Assert.NotNull(list);
        Assert.Single(list!);

        var fromTos = list!.Select(e => (e.FromNodeId, e.ToNodeId)).ToList();
        Assert.DoesNotContain(("RB_A", "RB_B"), fromTos);   // 已回滚
        Assert.Contains(("RB_B", "RB_C"), fromTos);         // 预置边原样保留
    }

    [Fact]
    public async Task Chain_Directed_Upstream_WalksToHead()
    {
        // 回归：UpstreamCteSql 的 visited 数组曾因 varchar(36)[] 与 varchar[] 不一致
        // 触发 Postgres 42804；同时验证反向装配（NodeIds 从起点逆序至源头）。
        var et = await CreateEdgeTypeAsync("ChainUp", isChain: true);
        await CreateChainAsync(et.EdgeTypeId, "CU_A", "CU_B", "CU_C", "CU_D");

        var up = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/upstream/CU_C");
        Assert.NotNull(up);
        Assert.False(up!.Truncated);
        Assert.Equal(2, up.Edges.Count);
        // NodeIds 从起点逆序到源头：C → B → A
        Assert.Equal(new[] { "CU_C", "CU_B", "CU_A" }, up.NodeIds);
        // 边按深度排序：紧邻段 B→C 在前，A→B 在后
        Assert.Equal("CU_B", up.Edges[0].FromNodeId);
        Assert.Equal("CU_C", up.Edges[0].ToNodeId);
        Assert.Equal("CU_A", up.Edges[1].FromNodeId);
        Assert.Equal("CU_B", up.Edges[1].ToNodeId);
    }

    [Fact]
    public async Task Chain_Directed_Downstream_MultiLevel_TraversesAllDepth()
    {
        // 回归：递归 CTE 数组元素须为 text（::text），多轮递归拼接 visited
        // 时类型不一致会触发 42804；5 节点链验证多轮递归全深度覆盖。
        var et = await CreateEdgeTypeAsync("ChainDeep", isChain: true);
        await CreateChainAsync(et.EdgeTypeId, "CD_A", "CD_B", "CD_C", "CD_D", "CD_E");

        var down = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/downstream/CD_A");
        Assert.NotNull(down);
        Assert.False(down!.Truncated);
        Assert.Equal(4, down.Edges.Count);
        Assert.Equal(new[] { "CD_A", "CD_B", "CD_C", "CD_D", "CD_E" }, down.NodeIds);
    }

    [Fact]
    public async Task Chain_Undirected_MultiLevel_CoversWholeChain()
    {
        // 回归：无向 CTE 同时拼接 visited（节点）与 visited_edges（边）两个数组，
        // 多轮递归下任一类型不一致即 42804；从端点出发应按深度覆盖整条链。
        var et = await CreateEdgeTypeAsync("UndirDeep", isDirected: false, isChain: true);
        await CreateChainAsync(et.EdgeTypeId, "UD_A", "UD_B", "UD_C", "UD_D", "UD_E");

        var down = await Client.GetEavAsync<ChainPathResult>(
            $"/api/eav/chains/{et.EdgeTypeId}/downstream/UD_A");
        Assert.NotNull(down);
        Assert.False(down!.Truncated);
        Assert.Equal(4, down.Edges.Count);
        Assert.Equal(new[] { "UD_A", "UD_B", "UD_C", "UD_D", "UD_E" }, down.NodeIds);
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private async Task<EdgeTypeDto> CreateEdgeTypeAsync(
        string? prefix = null, bool isDirected = true, bool isChain = false, bool isLinear = true)
    {
        var typeName = NewTypeName(prefix ?? "EdgeTest");
        var displayName = $"EdgeType_{typeName}";

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = displayName,
                IsGlobal = true,
                IsDirected = isDirected,
                IsChain = isChain,
                IsLinear = isLinear
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private async Task CreateChainAsync(string edgeTypeId, params string[] orderedNodeIds)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/chains/{edgeTypeId}/create",
            new CreateChainRequest { OrderedNodeIds = orderedNodeIds.ToList() });

        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.StatusCode == HttpStatusCode.OK,
            $"创建链失败: {(int)resp.StatusCode} {body}");
    }

    private async Task<EdgeInstanceDto> CreateEdgeAsync(
        string edgeTypeId, string fromNodeId, string toNodeId, decimal? weight = null)
    {
        var resp = await Client.PostAsJsonAsync(
            $"/api/eav/edges/{edgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Weight = weight
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        return dto!;
    }
}
