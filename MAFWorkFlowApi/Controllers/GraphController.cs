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
    LiteGraphRestClient liteGraph,
    IntentParserService intentParser,
    RerankerService reranker, // ★ 新增
    LiteGraphSdk sdk,
    IConfiguration configuration, // ★ 新增
    ILogger<GraphController> logger) : ControllerBase
{
    private readonly NodeAuthoringService _nodeService = nodeService;
    private readonly EdgeAuthoringService _edgeService = edgeService;
    private readonly LiteGraphRestClient _liteGraph = liteGraph;
    private readonly LiteGraphSdk _sdk = sdk;
    private readonly IConfiguration _configuration = configuration; // ★ 新增
    private readonly ILogger<GraphController> _logger = logger;

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

        await _liteGraph.DeleteAsync(
            $"/v1.0/tenants/{tenant}/graphs/{graphGuid}", ct);

        return NoContent();
    }

    // ══════════════════════════════════════════════════════
    // 节点 / 边更新 —— 用 JsonNode 快照避免僵尸引用
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
                Labels = labelsNode,
                Tags = tagsNode,
                Data = dataNode
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

        var labelsNode = GetNodeSnapshot(edge, "Labels");
        var tagsNode = GetNodeSnapshot(edge, "Tags");
        var dataNode = GetNodeSnapshot(edge, "Data");

        var fromGuid = edge.TryGetProperty("From", out var f)
                       && f.ValueKind == JsonValueKind.String
            ? f.GetGuid()
            : Guid.Empty;

        var toGuid = edge.TryGetProperty("To", out var t)
                     && t.ValueKind == JsonValueKind.String
            ? t.GetGuid()
            : Guid.Empty;

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

    // ══════════════════════════════════════════════════════
    // ★ 向量 CRUD —— 走 LiteGraphSdk
    // ══════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/vectors/enumerate")]
    public async Task<IActionResult> EnumerateVectors(
        [FromRoute] Guid graphGuid,
        [FromBody] EnumerateRequest request,
        CancellationToken ct)
    {
        var query = new EnumerationRequest
        {
            TenantGUID = _sdk.TenantGuid ?? Guid.Empty,
            GraphGUID = graphGuid,
            Ordering = EnumerationOrderEnum.CreatedDescending,
            MaxResults = Math.Clamp(request.MaxResults, 1, 1000),
            ContinuationToken = request.ContinuationToken
        };

        var result = await _sdk.Vector.Enumerate(query, ct);
        return Ok(result);
    }

    [HttpPost("{graphGuid}/vectors")]
    public async Task<IActionResult> CreateVector(
        [FromRoute] Guid graphGuid,
        [FromBody] CreateVectorRequest request,
        CancellationToken ct)
    {
        // ══════════════════════════════════════════════════════
        // ★ 用服务端配置的模型名，忽略 request.Model
        //   前端不应持有模型名，服务端才是真相源
        // ══════════════════════════════════════════════════════
        var modelName = _configuration["Embedding:Model"] ?? "bge-large";

        var metadata = new VectorMetadata
        {
            TenantGUID = Guid.Empty,
            GraphGUID = graphGuid,
            NodeGUID = request.NodeGuid,
            Model = modelName,
            Dimensionality = request.Vector.Count,
            Vectors = request.Vector
        };

        _logger.LogInformation(
            "创建向量：Graph={Graph}, Node={Node}, Model={Model}, Dim={Dim}",
            graphGuid, request.NodeGuid, modelName, request.Vector.Count);

        var created = await _sdk.Vector.Create(metadata, ct);

        _logger.LogInformation("向量创建成功：GUID={Guid}", created.GUID);

        return Ok(created);
    }

    [HttpDelete("{graphGuid}/vectors/{vectorGuid}")]
    public async Task<IActionResult> DeleteVector(
        [FromRoute] Guid graphGuid,
        [FromRoute] Guid vectorGuid,
        CancellationToken ct)
    {
        await _sdk.Vector.DeleteByGuid(_sdk.TenantGuid ?? Guid.Empty, vectorGuid, ct);
        return NoContent();
    }

    // ══════════════════════════════════════════════════════════
// ★ 边向量：附着到 EdgeGUID，方向由 From/To 保证
// ══════════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/edge-vectors")]
    public async Task<IActionResult> CreateEdgeVector(
        [FromRoute] Guid graphGuid,
        [FromBody] CreateEdgeVectorRequest request,
        CancellationToken ct)
    {
        var modelName = _configuration["Embedding:Model"] ?? "bge-large";

        var metadata = new VectorMetadata
        {
            // ★ 必须显式设置！SDK 默认是 Guid.NewGuid()，会存到随机租户
            TenantGUID = Guid.Empty,
            GraphGUID = graphGuid,
            NodeGUID = null,
            EdgeGUID = request.EdgeGuid, // ★ 附着到边
            Model = modelName,
            Dimensionality = request.Vector.Count,
            Content = request.Content, // ★ 保存原文
            Vectors = request.Vector
        };

        _logger.LogInformation(
            "创建边向量：Edge={Edge}, Model={Model}, Dim={Dim}, Content={Content}",
            request.EdgeGuid, modelName, request.Vector.Count, request.Content);

        var created = await _sdk.Vector.Create(metadata, ct);
        _logger.LogInformation("边向量创建成功：GUID={Guid}", created.GUID);

        return Ok(created);
    }

// ══════════════════════════════════════════════════════════
// ★ 意图解析端点
// ══════════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/parse-intent")]
    public async Task<ActionResult<IntentResult>> ParseIntent(
        [FromRoute] Guid graphGuid,
        [FromBody] ParseIntentRequest request,
        CancellationToken ct)
    {
        var relations = request.AvailableRelations is { Count: > 0 }
            ? request.AvailableRelations
            : await LoadGraphRelationsAsync(graphGuid, ct);

        if (relations.Count == 0)
            return Ok(new IntentResult { Strategy = "none" });

        var result = await intentParser.ParseAsync(request.Query, relations, ct);

        _logger.LogInformation(
            "意图解析：Query={Q}, Rel={R}, Dir={D}, Subj={S}, Conf={C:F2}, Strategy={St}",
            request.Query, result.Relation, result.Direction,
            result.SubjectName, result.Confidence, result.Strategy);

        return Ok(result);
    }

// ══════════════════════════════════════════════════════════
// ★ 语义搜索：意图解析 + 多跳分支 + 混合检索 + 方向过滤
// ══════════════════════════════════════════════════════════
// ══════════════════════════════════════════════════════════
// ★ 语义搜索：意图 + 多跳 + 混合检索 + 严格方向过滤 + 建议
// ══════════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/semantic-search")]
    public async Task<ActionResult<SemanticSearchResponseDto>> SemanticSearch(
        [FromRoute] Guid graphGuid,
        [FromBody] SemanticSearchRequest request,
        CancellationToken ct)
    {
        // ══════════════════════════════════════════════════
        // ① 意图解析
        // ══════════════════════════════════════════════════
        var relations = await LoadGraphRelationsAsync(graphGuid, ct);
        var intent = await intentParser.ParseAsync(request.Query, relations, ct);

        // ★ 方向覆盖：前端显式指定时优先
        if (!string.IsNullOrWhiteSpace(request.DirectionOverride)
            && (request.DirectionOverride == "in" || request.DirectionOverride == "out"))
        {
            _logger.LogInformation(
                "方向覆盖：LLM={Llm} → 前端={Ov}",
                intent.Direction, request.DirectionOverride);
            intent.Direction = request.DirectionOverride;
        }

        _logger.LogInformation(
            "语义搜索：Query={Q}, Rel={R}, Dir={D}, Subj={S}, Mode={M}, Hop={H}",
            request.Query, intent.Relation, intent.Direction, intent.SubjectName,
            intent.AggregationMode, intent.HopCount);

        // ══════════════════════════════════════════════════
        // ★ 阶段 4：多跳分支（不走向量）
        // ══════════════════════════════════════════════════
        if (intent.AggregationMode is "ancestors" or "descendants"
            && intent.HopCount != 1
            && !string.IsNullOrWhiteSpace(intent.SubjectName))
        {
            var multiHopSubjectGuid = await FindNodeByNameAsync(
                graphGuid, intent.SubjectName, new Dictionary<Guid, Node>(), ct);

            if (multiHopSubjectGuid is null)
            {
                return Ok(new SemanticSearchResponseDto
                {
                    Intent = intent,
                    Hint = "主体节点未找到：" + intent.SubjectName,
                    Suggestions = new List<SuggestedRelationDto>()
                });
            }

            var maxHops = intent.HopCount <= 0 ? 10 : intent.HopCount;

            var reachable = await BfsTraverseAsync(
                graphGuid, multiHopSubjectGuid.Value,
                intent.Relation!, intent.AggregationMode, maxHops, ct);

            _logger.LogInformation(
                "多跳遍历：Mode={Mode}, Rel={Rel}, MaxHops={Max}, Found={Count}",
                intent.AggregationMode, intent.Relation, maxHops, reachable.Count);

            // ── 空结果兜底：给出可用关系建议 ──
            if (reachable.Count == 0)
            {
                var availableRelations = await GetSubjectRelationsAsync(
                    graphGuid, multiHopSubjectGuid.Value,
                    intent.AggregationMode, ct);

                var availText = availableRelations.Count > 0
                    ? string.Join("、", availableRelations)
                    : "无";

                return Ok(new SemanticSearchResponseDto
                {
                    Hits = new List<SemanticSearchHitDto>(),
                    Intent = intent,
                    Hint = "「" + intent.SubjectName + "」没有 " + intent.Relation
                           + " 关系，但它有：" + availText,
                    Suggestions = new List<SuggestedRelationDto>(),
                    Stats = new
                    {
                        Mode = intent.AggregationMode,
                        MaxHops = maxHops,
                        Found = 0,
                        AvailableRelations = availableRelations
                    }
                });
            }

            var multiHits = new List<SemanticSearchHitDto>();
            foreach (var (nodeGuid, distance, viaEdgeName) in
                     reachable.OrderBy(x => x.Distance).Take(request.TopK))
            {
                var node = await FetchNodeAsync(graphGuid, nodeGuid, ct);
                if (node is null) continue;

                multiHits.Add(new SemanticSearchHitDto
                {
                    NodeGuid = node.GUID,
                    NodeName = node.Name,
                    Score = 1.0 / (1.0 + distance),
                    ViaEdgeName = viaEdgeName,
                    Direction = intent.AggregationMode == "ancestors" ? "in" : "out",
                    MatchedContent = distance + " 跳：" + intent.SubjectName + " → " + node.Name,
                    HopDistance = distance,
                    IsMultiHop = true
                });
            }

            return Ok(new SemanticSearchResponseDto
            {
                Hits = multiHits,
                Intent = intent,
                Suggestions = new List<SuggestedRelationDto>(),
                Stats = new
                {
                    Mode = intent.AggregationMode,
                    MaxHops = maxHops,
                    Found = reachable.Count,
                    Returned = multiHits.Count
                }
            });
        }

        // ══════════════════════════════════════════════════
        // ② 生成查询向量
        // ══════════════════════════════════════════════════
        var queryVec = await GenerateQueryEmbeddingAsync(request.Query, ct);
        if (queryVec is null || queryVec.Count == 0)
            return BadRequest("无法生成查询向量");

        // ══════════════════════════════════════════════════
        // ③ 加载所有边向量
        // ══════════════════════════════════════════════════
        var allVectors = await EnumerateAllVectorsAsync(graphGuid, ct: ct);
        var edgeVecs = allVectors
            .Where(v => v.EdgeGUID.HasValue && v.Vectors is { Count: > 0 })
            .ToList();

        if (edgeVecs.Count == 0)
        {
            return Ok(new SemanticSearchResponseDto
            {
                Intent = intent,
                Hint = "图中无边向量，请先重建所有向量",
                Suggestions = new List<SuggestedRelationDto>()
            });
        }

        // ══════════════════════════════════════════════════
        // ④ 混合检索：向量 + BM25
        // ══════════════════════════════════════════════════
        var docs = edgeVecs.Select(v => v.Content ?? "").ToList();
        var bm25 = new Bm25Scorer(docs);
        var bm25Scores = bm25.ScoreAllNormalized(request.Query);

        var alpha = request.VectorWeight;
        var beta = request.Bm25Weight;

        var scored = edgeVecs
            .Select((v, i) => new
            {
                Vector = v,
                VectorScore = Cosine(v.Vectors!, queryVec),
                Bm25Score = bm25Scores[i]
            })
            .Select(x => new
            {
                x.Vector,
                x.VectorScore,
                x.Bm25Score,
                FinalScore = alpha * x.VectorScore + beta * x.Bm25Score
            })
            .Where(x => request.MinScore is null || x.FinalScore >= request.MinScore)
            .OrderByDescending(x => x.FinalScore)
            .ToList();

        if (scored.Count > 0)
        {
            _logger.LogInformation(
                "混合检索 Top1: V={V:F4}, B={B:F4}, Final={F:F4}, Content={C}",
                scored[0].VectorScore, scored[0].Bm25Score, scored[0].FinalScore,
                scored[0].Vector.Content);
        }

    // ══════════════════════════════════════════════════════
    // ★ 阶段 1：Reranker 精排（LLM 打分）
    // ══════════════════════════════════════════════════════
        const int RecallTopK = 20;

        if (request.UseReranker && scored.Count > 1)
        {
            var candidates = scored.Take(RecallTopK).ToList();
            var candidateDocs = candidates.Select(c => c.Vector.Content ?? "").ToList();

            _logger.LogInformation(
                "Reranker 精排启动：候选 {N} 个，Query={Q}",
                candidates.Count, request.Query);

            try
            {
                var reranked = await reranker.RerankAsync(request.Query, candidateDocs, ct);

                // ══════════════════════════════════════════════
                // ★ 判定 reranker 是否真正生效
                //   失效：返回空 / 全 0
                // ══════════════════════════════════════════════
                var allZero = reranked.Count == 0
                              || reranked.All(x => Math.Abs(x.Score) < 1e-6);

                if (allZero)
                {
                    _logger.LogWarning(
                        "Reranker 未生效（返回空或全 0），保留原混合分顺序");
                    // 不修改 scored
                }
                else
                {
                    var rerankMap = reranked.ToDictionary(x => x.Index, x => x.Score);

                    // ★ 融合分 = 0.5 × LLM分 + 0.5 × 原混合分
                    //   （LLM 分数已经是 [0,1] 归一化过的）
                    const double RerankWeight = 0.5;
                    const double OriginalWeight = 0.5;

                    var reorderedCandidates = candidates
                        .Select((c, i) =>
                        {
                            var llmScore = rerankMap.GetValueOrDefault(i, 0.0);
                            var fusedScore =
                                RerankWeight * llmScore +
                                OriginalWeight * c.FinalScore;

                            return new
                            {
                                c.Vector,
                                c.VectorScore,
                                c.Bm25Score,
                                LlmScore = llmScore,
                                FinalScore = fusedScore
                            };
                        })
                        .OrderByDescending(x => x.FinalScore)
                        .ToList();

                    var rest = scored.Skip(RecallTopK).ToList();

                    scored = reorderedCandidates
                        .Select(x => new
                        {
                            x.Vector,
                            x.VectorScore,
                            x.Bm25Score,
                            x.FinalScore
                        })
                        .Concat(rest)
                        .ToList();

                    _logger.LogInformation(
                        "Reranker 精排完成：{N} 个候选，" +
                        "Top1 原分={Orig:F4}, LLM分={Llm:F4}, 融合分={Fused:F4}",
                        candidates.Count,
                        candidates[0].FinalScore,
                        reorderedCandidates[0].LlmScore,
                        reorderedCandidates[0].FinalScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reranker 精排异常，使用原始顺序");
            }
        }
        else if (!request.UseReranker)
        {
            _logger.LogInformation("Reranker 已禁用（UseReranker=false）");
        }

        // ══════════════════════════════════════════════════
        // ⑤ 严格方向过滤 + 反查节点
        // ══════════════════════════════════════════════════
        var hits = new List<SemanticSearchHitDto>();
        var seen = new HashSet<Guid>();

        var edgeCache = new Dictionary<Guid, Edge>();
        var nodeCache = new Dictionary<Guid, Node>();

        Guid? subjectGuid = null;
        if (!string.IsNullOrWhiteSpace(intent.SubjectName))
            subjectGuid = await FindNodeByNameAsync(
                graphGuid, intent.SubjectName, nodeCache, ct);

        foreach (var item in scored)
        {
            if (hits.Count >= request.TopK) break;

            var edgeGuid = item.Vector.EdgeGUID!.Value;
            if (!edgeCache.TryGetValue(edgeGuid, out var edge))
            {
                edge = await FetchEdgeAsync(graphGuid, edgeGuid, ct);
                if (edge is null) continue;
                edgeCache[edgeGuid] = edge;
            }

            // 关系名过滤
            if (intent.Relation is not null &&
                !string.Equals(edge.Name, intent.Relation, StringComparison.Ordinal))
                continue;

            Guid targetGuid;
            string effectiveDir;

            if (subjectGuid is not null)
            {
                bool subjectIsFrom = edge.From == subjectGuid;
                bool subjectIsTo = edge.To == subjectGuid;

                if (!subjectIsFrom && !subjectIsTo)
                    continue;

                // ★ 严格方向过滤：direction 和 edge.From/To 必须"与"匹配
                if (intent.Direction == "out")
                {
                    if (!subjectIsFrom) continue;
                    targetGuid = edge.To;
                    effectiveDir = "out";
                }
                else if (intent.Direction == "in")
                {
                    if (!subjectIsTo) continue;
                    targetGuid = edge.From;
                    effectiveDir = "in";
                }
                else
                {
                    targetGuid = subjectIsFrom ? edge.To : edge.From;
                    effectiveDir = subjectIsFrom ? "out" : "in";
                }
            }
            else
            {
                targetGuid = intent.Direction switch
                {
                    "out" => edge.To,
                    "in" => edge.From,
                    _ => Guid.Empty
                };
                effectiveDir = intent.Direction ?? "out";
                if (targetGuid == Guid.Empty) continue;
            }

            if (seen.Contains(targetGuid)) continue;
            seen.Add(targetGuid);

            if (!nodeCache.TryGetValue(targetGuid, out var target))
            {
                target = await FetchNodeAsync(graphGuid, targetGuid, ct);
                if (target is null) continue;
                nodeCache[targetGuid] = target;
            }

            hits.Add(new SemanticSearchHitDto
            {
                NodeGuid = target.GUID,
                NodeName = target.Name,
                Score = item.FinalScore,
                VectorScore = item.VectorScore,
                Bm25Score = item.Bm25Score,
                ViaEdgeName = edge.Name,
                Direction = effectiveDir,
                MatchedContent = item.Vector.Content
            });
        }

        // ══════════════════════════════════════════════════
        // ★ 生成建议：基于 subject 的"实际"关系
        // ══════════════════════════════════════════════════
        var suggestions = await BuildSuggestionsAsync(
            graphGuid, subjectGuid, intent, nodeCache, ct);

        // ══════════════════════════════════════════════════
        // 空结果：给出明确提示 + 建议
        // ══════════════════════════════════════════════════
        if (hits.Count == 0)
        {
            var dirZh = intent.Direction == "in" ? "入边/找父" : "出边/找子";
            var finalHint = suggestions.Count > 0
                ? "「" + intent.SubjectName + "」没有通过 " + intent.Relation
                  + "（" + dirZh + "）连接的节点。试试下面这些："
                : "「" + intent.SubjectName + "」在图里没有符合条件的关系";

            return Ok(new SemanticSearchResponseDto
            {
                Hits = new List<SemanticSearchHitDto>(),
                Intent = intent,
                Hint = finalHint,
                Suggestions = suggestions.Take(8).ToList(),
                Stats = new
                {
                    TotalEdgeVectors = edgeVecs.Count,
                    Recalled = scored.Count,
                    Returned = 0,
                    VectorWeight = alpha,
                    Bm25Weight = beta
                }
            });
        }

        // ══════════════════════════════════════════════════
        // 有结果：只在有建议时才附 Hint
        // ══════════════════════════════════════════════════
        string? okHint = null;
        if (suggestions.Count > 0)
            okHint = "「" + intent.SubjectName + "」还有其他关系可以查：";

        return Ok(new SemanticSearchResponseDto
        {
            Hits = hits,
            Intent = intent,
            Hint = okHint,
            Suggestions = suggestions.Take(8).ToList(),
            Stats = new
            {
                TotalEdgeVectors = edgeVecs.Count,
                Recalled = scored.Count,
                Returned = hits.Count,
                VectorWeight = alpha,
                Bm25Weight = beta
            }
        });
    }

    // ══════════════════════════════════════════════════════
// ★ 辅助：基于图数据构建查询建议
// ══════════════════════════════════════════════════════

    /// <summary>
    /// 扫描 subject 的所有入边 + 出边，生成"其他可查关系"建议。
    /// 排除掉 intent 里已经用的关系（避免重复）。
    /// </summary>
// ══════════════════════════════════════════════════════
// ★ 基于图数据生成"其他可查关系"建议
// ══════════════════════════════════════════════════════

    /// <summary>
    /// 扫描 subject 的所有入边 + 出边，生成"其他可查关系"建议。
    /// 优先级：反向关系（可能用户方向理解反了）> 同方向其他关系（可能选错关系名）。
    /// </summary>
    private async Task<List<SuggestedRelationDto>> BuildSuggestionsAsync(
        Guid graphGuid,
        Guid? subjectGuid,
        IntentResult intent,
        Dictionary<Guid, Node> nodeCache,
        CancellationToken ct)
    {
        if (subjectGuid is null || string.IsNullOrWhiteSpace(intent.SubjectName))
            return new List<SuggestedRelationDto>();

        var allEdges = await LoadAllEdgesAsync(graphGuid, ct);

        // 关系名 → (数量, 示例节点列表)
        var outgoing = new Dictionary<string, (int Count, List<string> Nodes)>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, (int Count, List<string> Nodes)>(StringComparer.Ordinal);

        // 本地辅助：按 GUID 拿节点名（带缓存）
        async Task<string> NameOfAsync(Guid g)
        {
            if (nodeCache.TryGetValue(g, out var cached)) return cached.Name;
            var node = await FetchNodeAsync(graphGuid, g, ct);
            var name = node?.Name ?? g.ToString("N")[..8];
            if (node is not null) nodeCache[g] = node;
            return name;
        }

        // 扫描所有边，按 subject 分类
        foreach (var e in allEdges)
        {
            if (e.From == subjectGuid)
            {
                if (!outgoing.TryGetValue(e.Name, out var info))
                    info = (0, new List<string>());
                info.Count++;
                var n = await NameOfAsync(e.To);
                if (info.Nodes.Count < 3 && !info.Nodes.Contains(n))
                    info.Nodes.Add(n);
                outgoing[e.Name] = info;
            }

            if (e.To == subjectGuid)
            {
                if (!incoming.TryGetValue(e.Name, out var info))
                    info = (0, new List<string>());
                info.Count++;
                var n = await NameOfAsync(e.From);
                if (info.Nodes.Count < 3 && !info.Nodes.Contains(n))
                    info.Nodes.Add(n);
                incoming[e.Name] = info;
            }
        }

        var suggestions = new List<SuggestedRelationDto>();

        // ─── 优先级 1：反向关系（用户可能方向理解反了） ───
        var opposite = intent.Direction == "out" ? incoming : outgoing;
        var oppDir = intent.Direction == "out" ? "in" : "out";

        foreach (var (rel, info) in opposite.OrderByDescending(x => x.Value.Count))
        {
            var sample = info.Nodes.FirstOrDefault() ?? "";
            var dirText = oppDir == "in" ? "反向(找父)" : "反向(找子)";

            var label = string.IsNullOrEmpty(sample)
                ? rel + " " + dirText
                : rel + " " + dirText + " · " + sample;

            suggestions.Add(new SuggestedRelationDto
            {
                Relation = rel,
                Direction = oppDir,
                Query = intent.SubjectName + " 的 " + rel,
                Count = info.Count,
                SampleNodes = info.Nodes,
                DisplayLabel = label,
                Tooltip = "会返回 " + info.Count + " 个节点：" + string.Join("、", info.Nodes)
            });
        }

        // ─── 优先级 2：同方向的其他关系（用户可能选错关系名） ───
        var same = intent.Direction == "out" ? outgoing : incoming;

        foreach (var (rel, info) in same.OrderByDescending(x => x.Value.Count))
        {
            if (string.Equals(rel, intent.Relation, StringComparison.Ordinal))
                continue; // 已用的是这个，不重复推荐

            var sample = info.Nodes.FirstOrDefault() ?? "";
            var dirText = (intent.Direction == "in") ? "反向(找父)" : "正向(找子)";

            var label = string.IsNullOrEmpty(sample)
                ? rel + " " + dirText
                : rel + " " + dirText + " · " + sample;

            suggestions.Add(new SuggestedRelationDto
            {
                Relation = rel,
                Direction = intent.Direction ?? "out",
                Query = intent.SubjectName + " 的 " + rel,
                Count = info.Count,
                SampleNodes = info.Nodes,
                DisplayLabel = label,
                Tooltip = "会返回 " + info.Count + " 个节点：" + string.Join("、", info.Nodes)
            });
        }

        return suggestions;
    }

    /// <summary>方向的中文解释。</summary>
    private static string DirZh(string? dir) => dir switch
    {
        "in" => "入边/找父",
        "out" => "出边/找子",
        _ => "无方向"
    };

    // ══════════════════════════════════════════════════════
// ★ 辅助：找出某个节点实际拥有的关系名
// ══════════════════════════════════════════════════════

    /// <summary>
    /// 列出某个节点沿指定方向实际存在的关系名。
    /// </summary>
    /// <param name="mode">"ancestors" = 入边关系；"descendants" = 出边关系</param>
    private async Task<List<string>> GetSubjectRelationsAsync(
        Guid graphGuid,
        Guid nodeGuid,
        string mode,
        CancellationToken ct)
    {
        var allEdges = await LoadAllEdgesAsync(graphGuid, ct);
        var relations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in allEdges)
        {
            if (mode == "ancestors")
            {
                // 入边：目标节点是当前节点
                if (e.To == nodeGuid)
                    relations.Add(e.Name);
            }
            else
            {
                // 出边：源节点是当前节点
                if (e.From == nodeGuid)
                    relations.Add(e.Name);
            }
        }

        return relations.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    // ══════════════════════════════════════════════════════════
    // ★ 辅助：分页枚举全量向量
    //   LiteGraph 的 EnumerationRequest.MaxResults 硬上限 = 1000，
    //   超过会抛 ArgumentException，所以必须分页循环。
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 分页拉取一个图的全部向量。
    /// LiteGraph 的 EnumerationRequest.MaxResults 硬上限 = 1000。
    /// </summary>
    /// <param name="graphGuid">图 GUID</param>
    /// <param name="maxTotal">最多拉多少条，防止无限循环（默认 10 万）</param>
    /// <param name="ct">取消令牌</param>
    private async Task<List<VectorMetadata>> EnumerateAllVectorsAsync(
        Guid graphGuid,
        int maxTotal = 100_000,
        CancellationToken ct = default)
    {
        const int pageSize = 1000; // ★ 改小写（避免规则告警）

        var result = new List<VectorMetadata>();
        Guid? token = null;
        int guard = 0;

        while (result.Count < maxTotal)
        {
            if (++guard > 1000)
            {
                _logger.LogWarning(
                    "EnumerateAllVectorsAsync 达到 1000 页上限，已拉取 {Count} 条，提前退出",
                    result.Count);
                break;
            }

            var query = new EnumerationRequest
            {
                TenantGUID = Guid.Empty,
                GraphGUID = graphGuid,
                Ordering = EnumerationOrderEnum.CreatedDescending,
                MaxResults = pageSize,
                ContinuationToken = token
            };

            var page = await _sdk.Vector.Enumerate(query, ct);

            if (page.Objects is { Count: > 0 })
                result.AddRange(page.Objects);

            if (page.ContinuationToken is null) break;

            if (page.ContinuationToken == token)
            {
                _logger.LogWarning("ContinuationToken 未推进，提前退出");
                break;
            }

            token = page.ContinuationToken;
        }

        _logger.LogDebug(
            "EnumerateAllVectorsAsync: Graph={Graph}, Total={Count}",
            graphGuid, result.Count);

        return result;
    }

// ══════════════════════════════════════════════════════════
// 辅助方法
// ══════════════════════════════════════════════════════════

    private async Task<List<string>> LoadGraphRelationsAsync(Guid graphGuid, CancellationToken ct)
    {
        var json = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges", ct);

        var set = new HashSet<string>(StringComparer.Ordinal);
        if (json.HasValue &&
            json.Value.TryGetProperty("Objects", out var objs) &&
            objs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in objs.EnumerateArray())
            {
                if (e.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    var v = n.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) set.Add(v);
                }
            }
        }

        return set.ToList();
    }

    private async Task<List<float>?> GenerateQueryEmbeddingAsync(string text, CancellationToken ct)
    {
        var gen = HttpContext.RequestServices.GetRequiredKeyedService<
            Microsoft.Extensions.AI.IEmbeddingGenerator<string,
                Microsoft.Extensions.AI.Embedding<float>>>("embedding");

        var r = await gen.GenerateAsync(new[] { text }, cancellationToken: ct);
        return r.Count > 0 ? r[0].Vector.ToArray().ToList() : null;
    }

    private async Task<Edge?> FetchEdgeAsync(Guid graphGuid, Guid edgeGuid, CancellationToken ct)
    {
        var json = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges/{edgeGuid}", ct);
        if (json is null) return null;

        var e = json.Value;
        if (e.TryGetProperty("Objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
            foreach (var o in objs.EnumerateArray())
            {
                e = o;
                break;
            }

        return new Edge
        {
            GUID = e.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
            Name = e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
            From = e.TryGetProperty("From", out var f) ? f.GetGuid() : Guid.Empty,
            To = e.TryGetProperty("To", out var t) ? t.GetGuid() : Guid.Empty
        };
    }

    private async Task<Node?> FetchNodeAsync(Guid graphGuid, Guid nodeGuid, CancellationToken ct)
    {
        var json = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes/{nodeGuid}", ct);
        if (json is null) return null;

        var n = json.Value;
        if (n.TryGetProperty("Objects", out var objs) && objs.ValueKind == JsonValueKind.Array)
            foreach (var o in objs.EnumerateArray())
            {
                n = o;
                break;
            }

        return new Node
        {
            GUID = n.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
            Name = n.TryGetProperty("Name", out var nm) ? nm.GetString() ?? "" : ""
        };
    }

    private async Task<Guid?> FindNodeByNameAsync(
        Guid graphGuid, string name,
        Dictionary<Guid, Node> cache, CancellationToken ct)
    {
        if (cache.Count == 0)
        {
            var json = await _liteGraph.GetAsync(
                $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/nodes", ct);

            if (json.HasValue &&
                json.Value.TryGetProperty("Objects", out var objs) &&
                objs.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in objs.EnumerateArray())
                {
                    var g = o.TryGetProperty("GUID", out var gg) ? gg.GetGuid() : Guid.Empty;
                    var nm = o.TryGetProperty("Name", out var nn) ? nn.GetString() ?? "" : "";
                    if (g != Guid.Empty) cache[g] = new Node { GUID = g, Name = nm };
                }
            }
        }

        var exact = cache.Values.FirstOrDefault(n =>
            string.Equals(n.Name, name, StringComparison.Ordinal));
        if (exact is not null) return exact.GUID;

        return cache.Values.FirstOrDefault(n =>
            n.Name.Contains(name, StringComparison.Ordinal))?.GUID;
    }

    private static double Cosine(List<float> a, List<float> b)
    {
        if (a.Count != b.Count) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Count; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }

        return na > 0 && nb > 0 ? dot / (Math.Sqrt(na) * Math.Sqrt(nb)) : 0;
    }

    // ══════════════════════════════════════════════════════
    // 辅助：从 JsonElement 抽取独立 JsonNode 快照
    // ══════════════════════════════════════════════════════

    private static JsonNode? GetNodeSnapshot(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
            return null;

        return JsonNode.Parse(prop.GetRawText());
    }

    // ══════════════════════════════════════════════════════════
// ★ 阶段 4：BFS 多跳遍历
// ══════════════════════════════════════════════════════════

    /// <summary>
    /// 从起点沿指定关系 BFS 遍历。
    /// </summary>
    /// <param name="graphGuid">图 GUID</param>
    /// <param name="startGuid">起点节点 GUID</param>
    /// <param name="relation">关系名（如 PARENT_OF）</param>
    /// <param name="mode">"ancestors" = 沿 To→From 反向；"descendants" = 沿 From→To 正向</param>
    /// <param name="maxHops">最大跳数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>(节点GUID, 距离, 通过边名) 列表，按距离升序</returns>
    private async Task<List<(Guid NodeGuid, int Distance, string ViaEdgeName)>> BfsTraverseAsync(
        Guid graphGuid,
        Guid startGuid,
        string relation,
        string mode,
        int maxHops,
        CancellationToken ct)
    {
        const int maxNodes = 10_000; // ★ 改小写

        var result = new List<(Guid, int, string)>();
        var visited = new HashSet<Guid> { startGuid };
        var queue = new Queue<(Guid Node, int Dist, string Via)>();

        queue.Enqueue((startGuid, 0, ""));

        var allEdges = await LoadAllEdgesAsync(graphGuid, ct);

        _logger.LogDebug(
            "BFS 起点：{Start}, 关系={Rel}, 模式={Mode}, 图中边数={N}",
            startGuid, relation, mode, allEdges.Count);

        while (queue.Count > 0 && result.Count < maxNodes)
        {
            var (cur, dist, _) = queue.Dequeue();
            if (dist >= maxHops) continue;

            foreach (var e in allEdges)
            {
                if (!string.Equals(e.Name, relation, StringComparison.Ordinal)) continue;

                Guid next;
                if (mode == "ancestors")
                {
                    if (e.To != cur) continue;
                    next = e.From;
                }
                else
                {
                    if (e.From != cur) continue;
                    next = e.To;
                }

                if (next == Guid.Empty) continue;
                if (!visited.Add(next)) continue;

                result.Add((next, dist + 1, e.Name));
                queue.Enqueue((next, dist + 1, e.Name));
            }
        }

        return result;
    }

    /// <summary>加载图中所有边（用于 BFS）。</summary>
    private async Task<List<Edge>> LoadAllEdgesAsync(Guid graphGuid, CancellationToken ct)
    {
        var json = await _liteGraph.GetAsync(
            $"/v1.0/tenants/{_liteGraph.TenantGuid}/graphs/{graphGuid}/edges", ct);

        var list = new List<Edge>();
        if (json.HasValue &&
            json.Value.TryGetProperty("Objects", out var objs) &&
            objs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in objs.EnumerateArray())
            {
                list.Add(new Edge
                {
                    GUID = e.TryGetProperty("GUID", out var g) ? g.GetGuid() : Guid.Empty,
                    Name = e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
                    From = e.TryGetProperty("From", out var f) ? f.GetGuid() : Guid.Empty,
                    To = e.TryGetProperty("To", out var t) ? t.GetGuid() : Guid.Empty
                });
            }
        }

        return list;
    }
}

// ─── 请求 DTO ─────────────────────────────────────────

public sealed class VectorSearchRequest
{
    [Required] public List<float> QueryVector { get; set; } = [];

    /// <summary>★ 保留字段但服务端会忽略，用配置里的 Embedding:Model。</summary>
    [Required]
    public string Model { get; set; } = "bge-large";

    [Range(1, 100)] public int TopK { get; set; } = 10;

    [Range(0.0, 1.0)] public double? MinScore { get; set; } = 0.5;
}

public sealed record CreateGraphRequest(
    string Name,
    Dictionary<string, object?>? Data = null);