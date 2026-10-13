using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    // 辅助方法
    // ============================================================

    private async Task<EdgeTypeDto> CreateEdgeTypeAsync(string? prefix = null)
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
                IsDirected = true
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        return dto!;
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
