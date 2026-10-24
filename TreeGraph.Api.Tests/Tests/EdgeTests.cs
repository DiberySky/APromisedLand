using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using TreeGraph.Shared.NodeEavSky.Dtos;
using TreeGraph.Shared.NodeSky.Entities;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// Edge（关系/边）集成测试（R4 简化版）。
/// 覆盖：全局路由 CRUD、nodes 端点外键、有向/无向与重复判定、
///       EAV 属性（entity_type = type_name）、图查询、BFS 遍历、删除守卫。
/// </summary>
public class EdgeTests : IntegrationTestBase
{
    public EdgeTests(EavApiFactory factory) : base(factory) { }

    private static string NewTypeName(string prefix = "EdgeTest")
        => $"{prefix}_{Guid.NewGuid():N}";

    // ============================================================
    // 创建 / 读取
    // ============================================================

    [Fact]
    public async Task Edge_Create_ThenGet_ReturnsSame()
    {
        var typeName = NewTypeName();
        var fromNode = await CreateFreeNodeAsync("起点设备");
        var toNode = await CreateFreeNodeAsync("终点设备");

        var createResp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNode,
                ToNodeId = toNode
            });

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(created);
        Assert.Equal(typeName, created!.TypeName);
        Assert.Equal(fromNode, created.FromNodeId);
        Assert.Equal(toNode, created.ToNodeId);
        Assert.True(created.IsDirected);
        // 端点显示名由 nodes 解析（测试环境未注册守卫时为空）

        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{created.EdgeId}");
        Assert.NotNull(got);
        Assert.Equal(created.EdgeId, got!.EdgeId);
        Assert.Equal(typeName, got.TypeName);
    }

    [Fact]
    public async Task Edge_Create_RegistersNodeRow()
    {
        var edge = await CreateEdgeAsync(NewTypeName(), "node-a", "node-b");

        var nodeResp = await Client.GetAsync($"/api/Node/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.OK, nodeResp.StatusCode);
        var node = await nodeResp.Content.ReadFromJsonAsync<Node>();
        Assert.NotNull(node);
        Assert.Equal("EdgeInstance", node!.HostType);
        Assert.Null(node.Name);
    }

    [Fact]
    public async Task Edge_Create_EmptyTypeName_Returns400()
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = "",
                FromNodeId = "n1",
                ToNodeId = "n2"
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("类型名", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Edge_Create_MissingFromNode_Returns400()
    {
        var typeName = NewTypeName();
        var toNode = await CreateFreeNodeAsync("终点");

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = "ghost-from",
                ToNodeId = toNode
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("ghost-from", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Edge_Create_MissingToNode_Returns400()
    {
        var typeName = NewTypeName();
        var fromNode = await CreateFreeNodeAsync("起点");

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNode,
                ToNodeId = "ghost-to"
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("ghost-to", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Edge_Create_SelfLoop_Allowed()
    {
        var typeName = NewTypeName();
        var node = await CreateFreeNodeAsync("自环节点");

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = node,
                ToNodeId = node
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        Assert.Equal(node, dto!.FromNodeId);
        Assert.Equal(node, dto.ToNodeId);
    }

    [Fact]
    public async Task Edge_Create_Duplicate_Returns409()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();

        await CreateEdgeAsync(typeName, fromNode, toNode);

        var second = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNode,
                ToNodeId = toNode
            });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Edge_Create_Undirected_ReverseDuplicate_Returns409()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();

        await CreateEdgeAsync(typeName, fromNode, toNode, isDirected: false);

        // 无向边反向存储视为同一条边
        var reverse = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = toNode,
                ToNodeId = fromNode,
                IsDirected = false
            });

        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
    }

    [Fact]
    public async Task Edge_Create_Directed_Reverse_Allowed()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();

        await CreateEdgeAsync(typeName, fromNode, toNode);
        await CreateEdgeAsync(typeName, toNode, fromNode);

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?typeName={typeName}");
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Edge_Create_SameNodes_DifferentTypes_Allowed()
    {
        var (fromNode, toNode) = await CreateNodePairAsync();

        await CreateEdgeAsync(NewTypeName("TypeA"), fromNode, toNode);
        await CreateEdgeAsync(NewTypeName("TypeB"), fromNode, toNode);
    }

    [Fact]
    public async Task Edge_Get_Unknown_Returns404()
    {
        var resp = await Client.GetAsync($"/api/eav/edges/{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ============================================================
    // 更新 / 删除
    // ============================================================

    [Fact]
    public async Task Edge_Update_IsDirected_ChangesValue()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();
        var edge = await CreateEdgeAsync(typeName, fromNode, toNode);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{edge.EdgeId}",
            new UpdateEdgeRequest { IsDirected = false });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(updated);
        Assert.False(updated!.IsDirected);
        Assert.Equal(typeName, updated.TypeName);
    }

    [Fact]
    public async Task Edge_Update_Unknown_Returns404()
    {
        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{Guid.NewGuid():N}",
            new UpdateEdgeRequest { IsDirected = false });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Edge_Update_ToUndirected_WhenReverseExists_Returns409()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();
        var forward = await CreateEdgeAsync(typeName, fromNode, toNode);
        await CreateEdgeAsync(typeName, toNode, fromNode);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{forward.EdgeId}",
            new UpdateEdgeRequest { IsDirected = false });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);

        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{forward.EdgeId}");
        Assert.NotNull(got);
        Assert.True(got!.IsDirected);
    }

    [Fact]
    public async Task Edge_Delete_ThenGet_Returns404()
    {
        var edge = await CreateEdgeAsync(NewTypeName(), "del-x", "del-y");

        var del = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var resp = await Client.GetAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Edge_Delete_RemovesRegisteredNodeRow()
    {
        var edge = await CreateEdgeAsync(NewTypeName(), "del-n1", "del-n2");

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}")).StatusCode);

        var nodeResp = await Client.GetAsync($"/api/Node/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.NotFound, nodeResp.StatusCode);
    }

    [Fact]
    public async Task Edge_Delete_Unknown_Returns404()
    {
        var resp = await Client.DeleteAsync($"/api/eav/edges/{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Edge_Delete_WhenReferencedAsEndpoint_Returns409()
    {
        var typeName = NewTypeName();
        var (a, b) = await CreateNodePairAsync();
        var target = await CreateEdgeAsync(typeName, a, b);

        var c = await CreateFreeNodeAsync("第三节点");
        var referencing = await CreateEdgeAsync(NewTypeName("Ref"), target.EdgeId, c);

        // 目标边已注册进 nodes，被另一条边当作端点 → 拒删
        var del = await Client.DeleteAsync($"/api/eav/edges/{target.EdgeId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Contains("引用", await del.Content.ReadAsStringAsync());

        // 解除引用后可删
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{referencing.EdgeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{target.EdgeId}")).StatusCode);
    }

    // ============================================================
    // 全局列表（typeName / nodeIds / search）
    // ============================================================

    [Fact]
    public async Task Edge_List_ByTypeName_ReturnsAll()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("L-A");
        var b = await CreateFreeNodeAsync("L-B");
        var c = await CreateFreeNodeAsync("L-C");

        await CreateEdgeAsync(typeName, a, b);
        await CreateEdgeAsync(typeName, b, c);

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?typeName={typeName}");

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.All(list, e => Assert.Equal(typeName, e.TypeName));
    }

    [Fact]
    public async Task Edge_List_FilteredByNodeIds()
    {
        var type1 = NewTypeName("N1");
        var type2 = NewTypeName("N2");
        var a = await CreateFreeNodeAsync("NA");
        var b = await CreateFreeNodeAsync("NB");
        var c = await CreateFreeNodeAsync("NC");

        await CreateEdgeAsync(type1, a, b);
        await CreateEdgeAsync(type2, b, a);
        await CreateEdgeAsync(type1, b, c);

        var all = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?nodeIds={a},{b},{c}");
        Assert.NotNull(all);
        Assert.Equal(3, all!.Count);

        var onlyA = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?nodeIds={a}");
        Assert.NotNull(onlyA);
        Assert.Equal(2, onlyA!.Count);
        Assert.All(onlyA, e => Assert.True(e.FromNodeId == a || e.ToNodeId == a));
    }

    [Fact]
    public async Task Edge_List_Search_MatchesNodeName()
    {
        var typeName = NewTypeName();
        var named = await CreateFreeNodeAsync($"搜索命中_{Guid.NewGuid():N}");
        var other = await CreateFreeNodeAsync("其它节点");

        var hit = await CreateEdgeAsync(typeName, named, other);
        await CreateEdgeAsync(typeName, other, await CreateFreeNodeAsync("第三个"));

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?search=搜索命中");

        Assert.NotNull(list);
        Assert.Contains(list!, e => e.EdgeId == hit.EdgeId);
    }

    [Fact]
    public async Task Edge_List_Search_MatchesTypeName()
    {
        var typeName = NewTypeName("SearchableType");
        var (a, b) = await CreateNodePairAsync();
        var hit = await CreateEdgeAsync(typeName, a, b);

        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?search=SearchableType");

        Assert.NotNull(list);
        Assert.Contains(list!, e => e.EdgeId == hit.EdgeId);
    }

    // ============================================================
    // 图查询：FindOutgoing / FindIncoming
    // ============================================================

    [Fact]
    public async Task EdgeGraph_Outgoing_ReturnsOnlyOutgoing()
    {
        var typeName = NewTypeName();
        var center = await CreateFreeNodeAsync("中心");
        var out1 = await CreateFreeNodeAsync("出1");
        var out2 = await CreateFreeNodeAsync("出2");
        var in1 = await CreateFreeNodeAsync("入1");

        await CreateEdgeAsync(typeName, center, out1);
        await CreateEdgeAsync(typeName, center, out2);
        await CreateEdgeAsync(typeName, in1, center);

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/from/{center}");

        Assert.NotNull(outgoing);
        Assert.Equal(2, outgoing!.Count);
        Assert.All(outgoing, e => Assert.Equal(center, e.FromNodeId));
    }

    [Fact]
    public async Task EdgeGraph_Incoming_ReturnsOnlyIncoming()
    {
        var typeName = NewTypeName();
        var center = await CreateFreeNodeAsync("中心");
        var out1 = await CreateFreeNodeAsync("出");
        var in1 = await CreateFreeNodeAsync("入1");
        var in2 = await CreateFreeNodeAsync("入2");

        await CreateEdgeAsync(typeName, center, out1);
        await CreateEdgeAsync(typeName, in1, center);
        await CreateEdgeAsync(typeName, in2, center);

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/to/{center}");

        Assert.NotNull(incoming);
        Assert.Equal(2, incoming!.Count);
        Assert.All(incoming, e => Assert.Equal(center, e.ToNodeId));
    }

    [Fact]
    public async Task EdgeGraph_Undirected_OutgoingReturnsConnectedEdge()
    {
        // 无向：存储为 A → B，从 B 查出边也应命中
        var (a, b) = await CreateNodePairAsync();
        await CreateEdgeAsync(NewTypeName("UndirOut"), a, b, isDirected: false);

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/from/{b}");

        Assert.NotNull(outgoing);
        Assert.Contains(outgoing!, e => e.FromNodeId == a && e.ToNodeId == b);
    }

    [Fact]
    public async Task EdgeGraph_Undirected_IncomingReturnsConnectedEdge()
    {
        // 无向：存储为 A → B，从 A 查入边也应命中
        var (a, b) = await CreateNodePairAsync();
        await CreateEdgeAsync(NewTypeName("UndirIn"), a, b, isDirected: false);

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/to/{a}");

        Assert.NotNull(incoming);
        Assert.Contains(incoming!, e => e.FromNodeId == a && e.ToNodeId == b);
    }

    [Fact]
    public async Task EdgeGraph_NoEdges_ReturnsEmpty()
    {
        var noEdgeNode = await CreateFreeNodeAsync("孤立节点");

        var outgoing = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/from/{noEdgeNode}");
        Assert.NotNull(outgoing);
        Assert.Empty(outgoing!);

        var incoming = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/to/{noEdgeNode}");
        Assert.NotNull(incoming);
        Assert.Empty(incoming!);
    }

    [Fact]
    public async Task EdgeGraph_TypeNameFilter_Applies()
    {
        var type1 = NewTypeName("G1");
        var type2 = NewTypeName("G2");
        var center = await CreateFreeNodeAsync("过滤中心");
        var other = await CreateFreeNodeAsync("过滤端点");

        await CreateEdgeAsync(type1, center, other);
        await CreateEdgeAsync(type2, center, other);

        var filtered = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges/from/{center}?typeName={type1}");

        Assert.NotNull(filtered);
        var single = Assert.Single(filtered!);
        Assert.Equal(type1, single.TypeName);
    }

    // ============================================================
    // BFS 遍历
    // ============================================================

    [Fact]
    public async Task Traverse_Out_Depth2_VisitsAllNeighbors()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("遍历A");
        var b = await CreateFreeNodeAsync("遍历B");
        var c = await CreateFreeNodeAsync("遍历C");

        await CreateEdgeAsync(typeName, a, b);
        await CreateEdgeAsync(typeName, b, c);

        var result = await TraverseAsync(typeName, a, "out", 2);

        Assert.Contains(a, result.VisitedNodeIds);
        Assert.Contains(b, result.VisitedNodeIds);
        Assert.Contains(c, result.VisitedNodeIds);
        Assert.Equal(3, result.VisitedNodeIds.Count);
        Assert.Equal(2, result.Edges.Count);
    }

    [Fact]
    public async Task Traverse_In_Depth1_FindsPredecessors()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("入A");
        var x = await CreateFreeNodeAsync("入X");
        var y = await CreateFreeNodeAsync("入Y");

        await CreateEdgeAsync(typeName, x, a);
        await CreateEdgeAsync(typeName, y, a);

        var result = await TraverseAsync(typeName, a, "in", 1);

        Assert.Equal(2, result.Edges.Count);
        Assert.Contains(a, result.VisitedNodeIds);
        Assert.Contains(x, result.VisitedNodeIds);
        Assert.Contains(y, result.VisitedNodeIds);
    }

    [Fact]
    public async Task Traverse_Both_HandlesBidirectional()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("双A");
        var b = await CreateFreeNodeAsync("双B");
        var c = await CreateFreeNodeAsync("双C");

        await CreateEdgeAsync(typeName, a, b);
        await CreateEdgeAsync(typeName, c, b);

        var result = await TraverseAsync(typeName, b, "both", 1);

        Assert.Equal(2, result.Edges.Count);
        Assert.Contains(a, result.VisitedNodeIds);
        Assert.Contains(c, result.VisitedNodeIds);
    }

    [Fact]
    public async Task Traverse_Undirected_IgnoresDirection()
    {
        // 无向边存储为 A → B，从 B 出发按 out 亦应走到 A
        var typeName = NewTypeName("UndirTraverse");
        var a = await CreateFreeNodeAsync("无A");
        var b = await CreateFreeNodeAsync("无B");

        await CreateEdgeAsync(typeName, a, b, isDirected: false);

        var result = await TraverseAsync(typeName, b, "out", 1);

        Assert.Single(result.Edges);
        Assert.Contains(a, result.VisitedNodeIds);
    }

    [Fact]
    public async Task Traverse_Depth1_OnlyFirstHop()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("深A");
        var b = await CreateFreeNodeAsync("深B");
        var c = await CreateFreeNodeAsync("深C");
        var d = await CreateFreeNodeAsync("深D");

        await CreateEdgeAsync(typeName, a, b);
        await CreateEdgeAsync(typeName, b, c);
        await CreateEdgeAsync(typeName, c, d);

        var result = await TraverseAsync(typeName, a, "out", 1);

        Assert.Single(result.Edges);
        Assert.Equal(2, result.VisitedNodeIds.Count);
    }

    [Fact]
    public async Task Traverse_CyclicGraph_NoInfiniteLoop()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("环A");
        var b = await CreateFreeNodeAsync("环B");
        var c = await CreateFreeNodeAsync("环C");

        await CreateEdgeAsync(typeName, a, b);
        await CreateEdgeAsync(typeName, b, c);
        await CreateEdgeAsync(typeName, c, a);

        var result = await TraverseAsync(typeName, a, "out", 10);

        Assert.Equal(3, result.VisitedNodeIds.Count);
        Assert.Equal(3, result.Edges.Count);
    }

    [Fact]
    public async Task Traverse_TypeNameScoped_IgnoresOtherTypes()
    {
        var type1 = NewTypeName("Scope1");
        var type2 = NewTypeName("Scope2");
        var a = await CreateFreeNodeAsync("域A");
        var b = await CreateFreeNodeAsync("域B");
        var c = await CreateFreeNodeAsync("域C");

        await CreateEdgeAsync(type1, a, b);
        await CreateEdgeAsync(type2, b, c);

        var result = await TraverseAsync(type1, a, "out", 2);

        Assert.Single(result.Edges);
        Assert.DoesNotContain(c, result.VisitedNodeIds);
    }

    [Fact]
    public async Task Traverse_ZeroDepth_ReturnsStartOnly()
    {
        var typeName = NewTypeName();
        var a = await CreateFreeNodeAsync("零A");
        var b = await CreateFreeNodeAsync("零B");
        await CreateEdgeAsync(typeName, a, b);

        var result = await TraverseAsync(typeName, a, "out", 0);

        Assert.Empty(result.Edges);
        var only = Assert.Single(result.VisitedNodeIds);
        Assert.Equal(a, only);
    }

    [Fact]
    public async Task Edge_Create_TypeNameIsTrimmed()
    {
        var rawTypeName = NewTypeName("Trimmed");
        var (fromNode, toNode) = await CreateNodePairAsync();

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = $"  {rawTypeName}  ",
                FromNodeId = fromNode,
                ToNodeId = toNode
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        Assert.Equal(rawTypeName, dto!.TypeName);
    }

    [Fact]
    public async Task Edge_Update_UndirectedToDirected_Succeeds()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();
        var edge = await CreateEdgeAsync(typeName, fromNode, toNode, isDirected: false);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{edge.EdgeId}",
            new UpdateEdgeRequest { IsDirected = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(updated);
        Assert.True(updated!.IsDirected);
    }

    [Fact]
    public async Task Edge_Create_Undirected_SameDirectionDuplicate_Returns409()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();

        await CreateEdgeAsync(typeName, fromNode, toNode, isDirected: false);

        var second = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNode,
                ToNodeId = toNode,
                IsDirected = false
            });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Edge_Create_SelfLoop_Duplicate_Returns409()
    {
        var typeName = NewTypeName();
        var node = await CreateFreeNodeAsync("自环重复节点");
        await CreateEdgeAsync(typeName, node, node);

        var second = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = node,
                ToNodeId = node
            });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Edge_Update_NullIsDirected_KeepsValue()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();
        var edge = await CreateEdgeAsync(typeName, fromNode, toNode, isDirected: false);

        var resp = await Client.PutAsJsonAsync(
            $"/api/eav/edges/{edge.EdgeId}",
            new UpdateEdgeRequest { IsDirected = null });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(updated);
        Assert.False(updated!.IsDirected);
    }

    [Fact]
    public async Task Edge_Create_Duplicate_AfterDelete_Allowed()
    {
        var typeName = NewTypeName();
        var (fromNode, toNode) = await CreateNodePairAsync();
        var first = await CreateEdgeAsync(typeName, fromNode, toNode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{first.EdgeId}")).StatusCode);

        // 删掉后同类型同端点可再建（唯一约束只在存活行上生效）
        await CreateEdgeAsync(typeName, fromNode, toNode);
    }

    [Fact]
    public async Task Edge_List_TypeName_NoMatch_ReturnsEmpty()
    {
        var list = await Client.GetEavAsync<List<EdgeInstanceDto>>(
            $"/api/eav/edges?typeName=NotExists_{Guid.NewGuid():N}");

        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Traverse_In_Undirected_ReachesSource()
    {
        // 无向边存储为 A → B，从 A 出发按 in 亦应走到 B
        var typeName = NewTypeName("UndirInTraverse");
        var a = await CreateFreeNodeAsync("无入A");
        var b = await CreateFreeNodeAsync("无入B");

        await CreateEdgeAsync(typeName, a, b, isDirected: false);

        var result = await TraverseAsync(typeName, a, "in", 1);

        Assert.Single(result.Edges);
        Assert.Contains(b, result.VisitedNodeIds);
    }

    // ============================================================
    // EAV 属性（entity_type = type_name）
    // ============================================================

    [Fact]
    public async Task Edge_WithEavProperties_OnCreate_RoundTrips()
    {
        var typeName = NewTypeName("EavEdge");
        await EnsureEntityTypeAndAttributeAsync(typeName, "strength", "decimal");
        var (fromNode, toNode) = await CreateNodePairAsync();

        var dto = await CreateEdgeAsync(
            typeName, fromNode, toNode,
            properties: new Dictionary<string, object> { ["strength"] = 42.5 });

        Assert.NotNull(dto.Properties);
        Assert.True(dto.Properties!.TryGetValue("strength", out var strength));
        Assert.Equal(42.5m, strength.GetDecimal());

        // EAV 实体端点同样可读（entity_type = type_name, entity_id = edgeId）
        var entity = await Client.GetEavAsync<DynamicEntityDto>(
            $"/api/eav/{typeName}/entities/{dto.EdgeId}");
        Assert.NotNull(entity);
        Assert.Equal(42.5m, entity!.Properties["strength"].GetDecimal());
    }

    [Fact]
    public async Task Edge_EavPut_ThenGet_ReturnsProperties()
    {
        var typeName = NewTypeName("EavEdgePut");
        await EnsureEntityTypeAndAttributeAsync(typeName, "strength", "decimal");
        var (fromNode, toNode) = await CreateNodePairAsync();
        var created = await CreateEdgeAsync(typeName, fromNode, toNode);

        var putResp = await Client.PutAsJsonAsync(
            $"/api/eav/{typeName}/entities/{created.EdgeId}",
            new { strength = 7.25 });
        Assert.True(putResp.IsSuccessStatusCode,
            await putResp.Content.ReadAsStringAsync());

        var got = await Client.GetEavAsync<EdgeInstanceDto>(
            $"/api/eav/edges/{created.EdgeId}");
        Assert.NotNull(got);
        Assert.NotNull(got!.Properties);
        Assert.Equal(7.25m, got.Properties!["strength"].GetDecimal());
    }

    [Fact]
    public async Task Edge_Delete_WhenPropertiesExist_Returns409()
    {
        var typeName = NewTypeName("EavEdgeDel");
        await EnsureEntityTypeAndAttributeAsync(typeName, "strength", "decimal");
        var (fromNode, toNode) = await CreateNodePairAsync();
        var edge = await CreateEdgeAsync(
            typeName, fromNode, toNode,
            properties: new Dictionary<string, object> { ["strength"] = 1.5 });

        var del = await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        Assert.Contains("属性数据", await del.Content.ReadAsStringAsync());

        // 清空属性值后可删
        var clear = await Client.PutAsJsonAsync(
            $"/api/eav/{typeName}/entities/{edge.EdgeId}", new { });
        Assert.True(clear.IsSuccessStatusCode, await clear.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/eav/edges/{edge.EdgeId}")).StatusCode);
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private async Task<string> CreateFreeNodeAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var resp = await Client.PostAsJsonAsync("/api/Node", new { id, name });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<(string From, string To)> CreateNodePairAsync()
    {
        var from = await CreateFreeNodeAsync("起点节点");
        var to = await CreateFreeNodeAsync("终点节点");
        return (from, to);
    }

    private async Task<EdgeInstanceDto> CreateEdgeAsync(
        string typeName, string fromNodeId, string toNodeId,
        bool isDirected = true, object? properties = null)
    {
        // 端点不存在时先注册为自由节点，保证 FK 成立（自环场景只注册一次）
        await EnsureNodeAsync(fromNodeId);
        await EnsureNodeAsync(toNodeId);

        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges",
            new CreateEdgeRequest
            {
                TypeName = typeName,
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                IsDirected = isDirected,
                Properties = properties is null ? null : ToJsonElements(properties)
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    /// <summary>确保节点存在于 nodes 注册表；已存在则跳过。</summary>
    private async Task EnsureNodeAsync(string nodeId)
    {
        var existing = await Client.GetAsync($"/api/Node/{nodeId}");
        if (existing.StatusCode == HttpStatusCode.OK) return;

        var resp = await Client.PostAsJsonAsync("/api/Node", new
        {
            id = nodeId,
            name = $"节点_{nodeId[^Math.Min(6, nodeId.Length)..]}"
        });
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    private async Task<EdgeTraverseResult> TraverseAsync(
        string typeName, string startNodeId, string direction, int maxDepth)
    {
        var resp = await Client.PostAsJsonAsync(
            "/api/eav/edges/traverse",
            new EdgeTraverseRequest(typeName, startNodeId, direction, maxDepth));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadEavAsync<EdgeTraverseResult>();
        Assert.NotNull(result);
        return result!;
    }

    private async Task EnsureEntityTypeAndAttributeAsync(
        string entityType, string attributeName, string dataType)
    {
        var typeResp = await Client.PostAsJsonAsync(
            "/api/eav/entity-types",
            new { entityType, displayName = entityType });
        Assert.True(typeResp.IsSuccessStatusCode || typeResp.StatusCode == HttpStatusCode.Conflict,
            await typeResp.Content.ReadAsStringAsync());

        var attrResp = await Client.PostAsJsonAsync(
            "/api/eav/metadata/attributes",
            new
            {
                entityType,
                attributeName,
                displayName = attributeName,
                dataType,
                isRequired = false
            });
        Assert.True(attrResp.IsSuccessStatusCode || attrResp.StatusCode == HttpStatusCode.Conflict,
            await attrResp.Content.ReadAsStringAsync());
    }

    private static Dictionary<string, System.Text.Json.JsonElement> ToJsonElements(
        object properties)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(properties);
        var dict = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var prop in json.EnumerateObject())
            dict[prop.Name] = prop.Value;
        return dict;
    }
}
