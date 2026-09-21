using System.ComponentModel.DataAnnotations;
using LiteGraph.Sdk;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Models;
using MAFWorkFlowApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

[ApiController]
[Route("api/graph")]
[Produces("application/json")]
public sealed class GraphController(
    NodeAuthoringService nodeService,
    EdgeAuthoringService edgeService,
    LiteGraphRestClient liteGraph) : ControllerBase
{
    private readonly NodeAuthoringService _nodeService = nodeService;
    private readonly EdgeAuthoringService _edgeService = edgeService;
    private readonly LiteGraphRestClient _liteGraph = liteGraph;

    // ─── 顶点单条 ──────────────────────────────────────

    [HttpPost("{graphGuid}/nodes")]
    public async Task<ActionResult<NodeUpsertResult>> UpsertNode(
        [FromRoute] Guid graphGuid,
        [FromBody] NodeUpsertRequest request,
        CancellationToken ct)
    {
        var result = await _nodeService.UpsertAsync(graphGuid, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{graphGuid}/nodes/{nodeGuid}")]
    public async Task<IActionResult> DeleteNode(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid nodeGuid,
        CancellationToken ct)
    {
        await _nodeService.DeleteAsync(graphGuid, nodeGuid, ct);
        return NoContent();
    }

    // ─── 顶点查询（新增）───────────────────────────────

    [HttpGet("{graphGuid}/nodes")]
    public async Task<IActionResult> ListNodes(
        [FromRoute] Guid graphGuid,
        CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{graphGuid}/nodes/{nodeGuid}")]
    public async Task<IActionResult> GetNode(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid nodeGuid,
        CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    // ─── 顶点批量 ──────────────────────────────────────

    [HttpPost("{graphGuid}/nodes/batch")]
    public async Task<ActionResult<List<Node>>> CreateNodesBatch(
        [FromRoute] Guid graphGuid,
        [FromBody] List<NodeUpsertRequest> requests,
        CancellationToken ct)
    {
        var nodes = await _nodeService.CreateBatchAsync(graphGuid, requests, ct);
        return Ok(nodes);
    }

    // ─── 向量搜索 ──────────────────────────────────────

    [HttpPost("{graphGuid}/nodes/vector-search")]
    public async Task<ActionResult<List<Node>>> VectorSearch(
        [FromRoute] Guid graphGuid,
        [FromBody] VectorSearchRequest request,
        CancellationToken ct)
    {
        var nodes = await _nodeService.SearchByVectorAsync(
            graphGuid, request.QueryVector,
            request.Model, request.TopK,
            request.MinScore, ct);
        return Ok(nodes);
    }

    // ─── 边单条 ────────────────────────────────────────

    [HttpPost("{graphGuid}/edges")]
    public async Task<ActionResult<EdgeUpsertResult>> UpsertEdge(
        [FromRoute] Guid graphGuid,
        [FromBody] EdgeUpsertRequest request,
        CancellationToken ct)
    {
        var result = await _edgeService.UpsertAsync(graphGuid, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{graphGuid}/edges/{edgeGuid}")]
    public async Task<IActionResult> DeleteEdge(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid edgeGuid,
        CancellationToken ct)
    {
        await _edgeService.DeleteAsync(graphGuid, edgeGuid, ct);
        return NoContent();
    }

    // ─── 边查询（新增）─────────────────────────────────

    [HttpGet("{graphGuid}/edges")]
    public async Task<IActionResult> ListEdges(
        [FromRoute] Guid graphGuid,
        CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{graphGuid}/edges/{edgeGuid}")]
    public async Task<IActionResult> GetEdge(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid edgeGuid,
        CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    // ─── 边批量 ────────────────────────────────────────

    [HttpPost("{graphGuid}/edges/batch")]
    public async Task<ActionResult<List<Edge>>> CreateEdgesBatch(
        [FromRoute] Guid graphGuid,
        [FromBody] List<EdgeUpsertRequest> requests,
        CancellationToken ct)
    {
        var edges = await _edgeService.CreateBatchAsync(graphGuid, requests, ct);
        return Ok(edges);
    }

    // ─── 图查询（新增）─────────────────────────────────

    [HttpGet("{graphGuid}")]
    public async Task<IActionResult> GetGraph(
        [FromRoute] Guid graphGuid,
        CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> ListGraphs(CancellationToken ct)
    {
        var result = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs",
            ct);
        return result is null ? NotFound() : Ok(result.Value);
    }

    // ─── 创建图（新增）─────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateGraph(
        [FromBody] CreateGraphRequest request,
        CancellationToken ct)
    {
        var result = await _liteGraph.PutAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs",
            new { Name = request.Name, Data = request.Data ?? new() },
            ct);
        return Ok(result);
    }
    
        // ─── 更新图（重命名）────────────────────────────

    [HttpPut("{graphGuid}")]
    public async Task<IActionResult> UpdateGraph(
        [FromRoute] Guid graphGuid,
        [FromBody] UpdateGraphRequest request,
        CancellationToken ct)
    {
        var result = await _liteGraph.PutAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}",
            new { Name = request.Name, Data = request.Data ?? new() },
            ct);
        return Ok(result);
    }

    // ─── 删除图（级联清空节点和边）───────────────────

    [HttpDelete("{graphGuid}")]
    public async Task<IActionResult> DeleteGraph(
        [FromRoute] Guid graphGuid,
        CancellationToken ct)
    {
        var tenant = _liteGraph.TenantGuid;

        // 1. 先删除所有边
        var edgesJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/edges", ct);
        if (edgesJson.HasValue &&
            edgesJson.Value.TryGetProperty("Objects", out var edgesObj) &&
            edgesObj.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var e in edgesObj.EnumerateArray())
            {
                if (e.TryGetProperty("GUID", out var eg))
                {
                    await _liteGraph.DeleteAsync(
                        $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/edges/{eg.GetGuid()}",
                        ct);
                }
            }
        }

        // 2. 再删除所有节点
        var nodesJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/nodes", ct);
        if (nodesJson.HasValue &&
            nodesJson.Value.TryGetProperty("Objects", out var nodesObj) &&
            nodesObj.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var n in nodesObj.EnumerateArray())
            {
                if (n.TryGetProperty("GUID", out var ng))
                {
                    await _liteGraph.DeleteAsync(
                        $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/nodes/{ng.GetGuid()}",
                        ct);
                }
            }
        }

        // 3. 最后删除图本身
        await _liteGraph.DeleteAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}", ct);

        return NoContent();
    }
}

// ─── 请求 DTO ─────────────────────────────────────────

public sealed class VectorSearchRequest
{
    [Required]
    public List<float> QueryVector { get; set; } = [];

    [Required]
    public string Model { get; set; } = "bge-large";

    [Range(1, 100)]
    public int TopK { get; set; } = 10;

    [Range(0.0, 1.0)]
    public double? MinScore { get; set; } = 0.5;
}

public sealed record CreateGraphRequest(
    string Name,
    Dictionary<string, object?>? Data = null);
    
public sealed record UpdateGraphRequest(
    string Name,
    Dictionary<string, object?>? Data = null);