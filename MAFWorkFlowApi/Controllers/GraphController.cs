using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiteGraph.Sdk;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Models;
using MAFWorkFlowApi.Models.Graph;
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

    // ─── 顶点查询 ──────────────────────────────────────

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

    // ─── 边查询 ────────────────────────────────────────

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

    // ─── 图查询 ────────────────────────────────────────

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

    // ─── 创建图 ────────────────────────────────────────

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

    // ══════════════════════════════════════════════════════
    // 图更新 / 删除
    // ══════════════════════════════════════════════════════

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

    [HttpDelete("{graphGuid}")]
    public async Task<IActionResult> DeleteGraph(
        [FromRoute] Guid graphGuid,
        CancellationToken ct)
    {
        var tenant = _liteGraph.TenantGuid;

        // 先删边
        var edgesJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/edges", ct);
        if (edgesJson.HasValue &&
            edgesJson.Value.TryGetProperty("Objects", out var edgesObj) &&
            edgesObj.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in edgesObj.EnumerateArray())
            {
                if (e.TryGetProperty("GUID", out var eg))
                    await _liteGraph.DeleteAsync(
                        $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/edges/{eg.GetGuid()}", ct);
            }
        }

        // 再删节点
        var nodesJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/nodes", ct);
        if (nodesJson.HasValue &&
            nodesJson.Value.TryGetProperty("Objects", out var nodesObj) &&
            nodesObj.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in nodesObj.EnumerateArray())
            {
                if (n.TryGetProperty("GUID", out var ng))
                    await _liteGraph.DeleteAsync(
                        $"/v1.0/tenants/{tenant}/graphs/{graphGuid}/nodes/{ng.GetGuid()}", ct);
            }
        }

        // 最后删图
        await _liteGraph.DeleteAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}", ct);

        return NoContent();
    }

    // ══════════════════════════════════════════════════════
    // ★ 修复：节点 / 边更新 —— 用 JsonNode 快照避免僵尸引用
    // ══════════════════════════════════════════════════════

    [HttpPut("{graphGuid}/nodes/{nodeGuid}")]
    public async Task<IActionResult> UpdateNode(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid nodeGuid,
        [FromBody] UpdateNodeRequest request,
        CancellationToken ct)
    {
        var existingJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}",
            ct);

        if (existingJson is null) return NotFound();

        var node = existingJson.Value;

        // ★ 关键：立即从 JsonElement 抽出独立 JsonNode 快照，
        //   之后即使父 JsonDocument 被回收，这些节点数据仍安全
        var labelsNode = GetNodeSnapshot(node, "Labels");
        var tagsNode = GetNodeSnapshot(node, "Tags");
        var dataNode = GetNodeSnapshot(node, "Data");

        var result = await _liteGraph.PutAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}",
            new
            {
                GUID = nodeGuid,
                TenantGUID = _liteGraph.TenantGuid,
                GraphGUID = graphGuid,
                Name = request.Name,
                Labels = labelsNode,   // JsonNode?
                Tags = tagsNode,       // JsonNode?
                Data = dataNode        // JsonNode?
            },
            ct);

        return Ok(result);
    }

    [HttpPut("{graphGuid}/edges/{edgeGuid}")]
    public async Task<IActionResult> UpdateEdge(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid edgeGuid,
        [FromBody] UpdateEdgeRequest request,
        CancellationToken ct)
    {
        var existingJson = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}",
            ct);

        if (existingJson is null) return NotFound();

        var edge = existingJson.Value;

        // ★ 同上：抽出独立快照
        var labelsNode = GetNodeSnapshot(edge, "Labels");
        var tagsNode = GetNodeSnapshot(edge, "Tags");
        var dataNode = GetNodeSnapshot(edge, "Data");

        // From / To 直接读 Guid（立即使用，无跨方法引用问题）
        var fromGuid = edge.TryGetProperty("From", out var f)
            && f.ValueKind == JsonValueKind.String
            ? f.GetGuid() : Guid.Empty;

        var toGuid = edge.TryGetProperty("To", out var t)
            && t.ValueKind == JsonValueKind.String
            ? t.GetGuid() : Guid.Empty;

        var result = await _liteGraph.PutAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}",
            new
            {
                GUID = edgeGuid,
                TenantGUID = _liteGraph.TenantGuid,
                GraphGUID = graphGuid,
                From = fromGuid,
                To = toGuid,
                Name = request.Name,
                Labels = labelsNode,
                Tags = tagsNode,
                Data = dataNode
            },
            ct);

        return Ok(result);
    }

    // ══════════════════════════════════════════════════════
    // 分页枚举
    // ══════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/nodes/enumerate")]
    public async Task<IActionResult> EnumerateNodes(
        [FromRoute] Guid graphGuid,
        [FromBody] EnumerateRequest request,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["TenantGUID"] = _liteGraph.TenantGuid,
            ["GraphGUID"] = graphGuid,
            ["Ordering"] = "CreatedDescending",
            ["MaxResults"] = Math.Clamp(request.MaxResults, 1, 1000)
        };
        if (request.ContinuationToken.HasValue)
            body["ContinuationToken"] = request.ContinuationToken.Value;

        var result = await _liteGraph.PostAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes/enumerate",
            body, ct);
        return Ok(result);
    }

    [HttpPost("{graphGuid}/edges/enumerate")]
    public async Task<IActionResult> EnumerateEdges(
        [FromRoute] Guid graphGuid,
        [FromBody] EnumerateRequest request,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["TenantGUID"] = _liteGraph.TenantGuid,
            ["GraphGUID"] = graphGuid,
            ["Ordering"] = "CreatedDescending",
            ["MaxResults"] = Math.Clamp(request.MaxResults, 1, 1000)
        };
        if (request.ContinuationToken.HasValue)
            body["ContinuationToken"] = request.ContinuationToken.Value;

        var result = await _liteGraph.PostAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges/enumerate",
            body, ct);
        return Ok(result);
    }

    [HttpPost("{graphGuid}/vectors/enumerate")]
    public async Task<IActionResult> EnumerateVectors(
        [FromRoute] Guid graphGuid,
        [FromBody] EnumerateRequest request,
        CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["TenantGUID"] = _liteGraph.TenantGuid,
            ["GraphGUID"] = graphGuid,
            ["Ordering"] = "CreatedDescending",
            ["MaxResults"] = Math.Clamp(request.MaxResults, 1, 1000)
        };
        if (request.ContinuationToken.HasValue)
            body["ContinuationToken"] = request.ContinuationToken.Value;

        var result = await _liteGraph.PostAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/vectors/enumerate",
            body, ct);
        return Ok(result);
    }

    // ══════════════════════════════════════════════════════
    // 向量 CRUD
    // ══════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/vectors")]
    public async Task<IActionResult> CreateVector(
        [FromRoute] Guid graphGuid,
        [FromBody] CreateVectorRequest request,
        CancellationToken ct)
    {
        var body = new
        {
            TenantGUID = _liteGraph.TenantGuid,
            GraphGUID = graphGuid,
            NodeGUID = request.NodeGuid,
            Model = request.Model,
            Dimensionality = request.Vector.Count,
            Vectors = request.Vector
        };

        var result = await _liteGraph.PutAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/vectors",
            body, ct);
        return Ok(result);
    }

    [HttpDelete("{graphGuid}/vectors/{vectorGuid}")]
    public async Task<IActionResult> DeleteVector(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid vectorGuid,
        CancellationToken ct)
    {
        await _liteGraph.DeleteAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/vectors/{vectorGuid}",
            ct);
        return NoContent();
    }

    // ══════════════════════════════════════════════════════
    // ★ 辅助：从 JsonElement 抽取独立 JsonNode 快照
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 从 JsonElement 里安全提取一个字段，转为独立的 JsonNode。
    /// - 字段不存在 → null
    /// - 字段为 null/undefined → null
    /// - 其它 → JsonNode.Parse 一个独立副本，不依赖父 JsonDocument
    /// </summary>
    private static JsonNode? GetNodeSnapshot(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
            return null;

        // ★ GetRawText() 从父 JsonDocument 的底层字节读出原文，
        //   然后 Parse 成完全独立的 JsonNode
        return JsonNode.Parse(prop.GetRawText());
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