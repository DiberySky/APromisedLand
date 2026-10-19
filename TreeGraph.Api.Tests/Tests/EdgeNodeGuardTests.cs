using System.Net;
using System.Net.Http.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.EdgeSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// Edge 起点/终点节点必须是 Inode 宿主的端到端测试。
/// 使用注册了 UnitTreeInodeGuard 的 <see cref="EdgeGuardApiFactory"/>；
/// 节点 ID 取 unit_trees 幂等种子数据（固定 GUID）。
/// </summary>
[Collection("EdgeGuard")]
public class EdgeNodeGuardTests
{
    private readonly EdgeGuardApiFactory _factory;
    private readonly HttpClient _client;

    public EdgeNodeGuardTests(EdgeGuardApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>unit_trees 种子节点：长度（LENGTH_ID，固定 GUID）。</summary>
    private const string SeededUnitNodeId = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";

    private static string NewTypeName() => $"EdgeGuard_{Guid.NewGuid():N}";

    private async Task<EdgeTypeDto> CreateNonGlobalTypeAsync(
        string fromNodeType, string toNodeType)
    {
        var typeName = NewTypeName();
        var resp = await _client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = $"EdgeGuardType_{typeName}",
                IsGlobal = false,
                IsDirected = true,
                FromNodeType = fromNodeType,
                ToNodeType = toNodeType
            });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeTypeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task Edge_NonGlobal_SeededUnitNode_Creates()
    {
        var et = await CreateNonGlobalTypeAsync("UnitTree", "UnitTree");

        var resp = await _client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = SeededUnitNodeId,
                ToNodeId = SeededUnitNodeId
            });

        // 自环由链式规则拒绝，但本类型非链式（IsChain=false），存在性校验通过 → 201
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadEavAsync<EdgeInstanceDto>();
        Assert.NotNull(dto);
        Assert.Equal(SeededUnitNodeId, dto!.FromNodeId);
        Assert.Equal(SeededUnitNodeId, dto.ToNodeId);
    }

    [Fact]
    public async Task Edge_NonGlobal_NodeMissing_Returns400()
    {
        var et = await CreateNonGlobalTypeAsync("UnitTree", "UnitTree");

        var resp = await _client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = SeededUnitNodeId,
                ToNodeId = "ghost-node-id"
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("不存在于 UnitTree", body);
    }

    [Fact]
    public async Task Edge_NonGlobal_UnclaimedNodeType_Returns400()
    {
        var et = await CreateNonGlobalTypeAsync("GhostType", "GhostType");

        var resp = await _client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = "node-x",
                ToNodeId = "node-y"
            });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("未实现 Inode", body);
    }

    [Fact]
    public async Task Edge_IsGlobal_UnregisteredNodes_StillCreate()
    {
        // IsGlobal 边不声明节点类型 → 不校验，保持既有行为
        var typeName = NewTypeName();
        var resp = await _client.PostAsJsonAsync(
            "/api/eav/metadata/edge-types",
            new CreateEdgeTypeRequest
            {
                TypeName = typeName,
                DisplayName = $"EdgeGuardGlobal_{typeName}",
                IsGlobal = true,
                IsDirected = true
            });
        resp.EnsureSuccessStatusCode();
        var et = (await resp.Content.ReadEavAsync<EdgeTypeDto>())!;

        var create = await _client.PostAsJsonAsync(
            $"/api/eav/edges/{et.EdgeTypeId}",
            new CreateEdgeRequest
            {
                FromNodeId = "arbitrary-node-a",
                ToNodeId = "arbitrary-node-b"
            });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }
}
