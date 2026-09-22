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
// ★ 语义搜索：意图解析 + 边向量搜索 + 方向过滤（核心）
// ══════════════════════════════════════════════════════════

// ══════════════════════════════════════════════════════════
// ★ 语义搜索：意图解析 + 混合检索（向量 + BM25）+ 方向过滤
// ══════════════════════════════════════════════════════════

    [HttpPost("{graphGuid}/semantic-search")]
    public async Task<ActionResult<SemanticSearchResponseDto>> SemanticSearch(
        [FromRoute] Guid graphGuid,
        [FromBody] SemanticSearchRequest request,
        CancellationToken ct)
    {
        // ══════════════════════════════════════════════════════
        // ① 意图解析
        // ══════════════════════════════════════════════════════
        var relations = await LoadGraphRelationsAsync(graphGuid, ct);
        var intent = await intentParser.ParseAsync(request.Query, relations, ct);

        _logger.LogInformation(
            "语义搜索：Query={Q}, Rel={R}, Dir={D}, Subj={S}",
            request.Query, intent.Relation, intent.Direction, intent.SubjectName);

        // ══════════════════════════════════════════════════════
        // ② 生成查询向量
        // ══════════════════════════════════════════════════════
        var queryVec = await GenerateQueryEmbeddingAsync(request.Query, ct);
        if (queryVec is null || queryVec.Count == 0)
            return BadRequest("无法生成查询向量");

    // ══════════════════════════════════════════════════════
    // ③ 加载所有边向量（分页拉取，LiteGraph 单次上限 1000）
    // ══════════════════════════════════════════════════════
        var allVectors = await EnumerateAllVectorsAsync(graphGuid, ct: ct);

        var edgeVecs = allVectors
            .Where(v => v.EdgeGUID.HasValue && v.Vectors is { Count: > 0 })
            .ToList();

        if (edgeVecs.Count == 0)
        {
            return Ok(new SemanticSearchResponseDto
            {
                Intent = intent,
                Stats = new { Note = "图中无边向量，请先重建所有向量" }
            });
        }

        // ══════════════════════════════════════════════════════
        // ④ 混合检索：向量 + BM25
        // ══════════════════════════════════════════════════════
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

        // 日志：可观测性
        if (scored.Count > 0)
        {
            _logger.LogInformation(
                "混合检索 Top1: V={V:F4}, B={B:F4}, Final={F:F4}, Content={C}",
                scored[0].VectorScore, scored[0].Bm25Score, scored[0].FinalScore,
                scored[0].Vector.Content);
        }

        // ══════════════════════════════════════════════════════
        // ⑤ 边方向过滤 + 反查节点
        // ══════════════════════════════════════════════════════
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

            // ── 方向过滤（subject 优先 + 自动纠偏）──
            Guid targetGuid;
            string effectiveDir;

            if (subjectGuid is not null)
            {
                bool subjectIsFrom = edge.From == subjectGuid;
                bool subjectIsTo = edge.To == subjectGuid;

                if (!subjectIsFrom && !subjectIsTo)
                    continue; // 与 subject 无关

                if (intent.Direction == "in")
                {
                    if (subjectIsTo)
                    {
                        targetGuid = edge.From;
                        effectiveDir = "in";
                    }
                    else
                    {
                        targetGuid = edge.To;
                        effectiveDir = "out";
                        _logger.LogWarning(
                            "方向纠偏：LLM=in, 实际=out, Subject={Subj}, Edge={Edge}",
                            intent.SubjectName, edgeGuid);
                    }
                }
                else if (intent.Direction == "out")
                {
                    if (subjectIsFrom)
                    {
                        targetGuid = edge.To;
                        effectiveDir = "out";
                    }
                    else
                    {
                        targetGuid = edge.From;
                        effectiveDir = "in";
                        _logger.LogWarning(
                            "方向纠偏：LLM=out, 实际=in, Subject={Subj}, Edge={Edge}",
                            intent.SubjectName, edgeGuid);
                    }
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

            // 反查目标节点
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
                VectorScore = item.VectorScore, // ★ 分量
                Bm25Score = item.Bm25Score, // ★ 分量
                ViaEdgeName = edge.Name,
                Direction = effectiveDir,
                MatchedContent = item.Vector.Content
            });
        }

        return Ok(new SemanticSearchResponseDto
        {
            Hits = hits,
            Intent = intent,
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

    // ══════════════════════════════════════════════════════════
    // ★ 辅助：分页枚举全量向量
    //   LiteGraph 的 EnumerationRequest.MaxResults 硬上限 = 1000，
    //   超过会抛 ArgumentException，所以必须分页循环。
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 分页拉取一个图的全部向量。
    /// </summary>
    /// <param name="graphGuid">图 GUID</param>
    /// <param name="maxTotal">最多拉多少条，防止无限循环（默认 10 万）</param>
    private async Task<List<VectorMetadata>> EnumerateAllVectorsAsync(
        Guid graphGuid,
        int maxTotal = 100_000,
        CancellationToken ct = default)
    {
        const int PageSize = 1000;   // ★ LiteGraph 硬上限

        var result = new List<VectorMetadata>();
        Guid? token = null;
        int guard = 0;

        while (result.Count < maxTotal)
        {
            // 防御性：最多 1000 页
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
                MaxResults = PageSize,
                ContinuationToken = token
            };

            var page = await _sdk.Vector.Enumerate(query, ct);

            if (page.Objects is { Count: > 0 })
                result.AddRange(page.Objects);

            // 没有下一页就退出
            if (page.ContinuationToken is null)
                break;

            // 防止 token 死循环
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
    // ★ 辅助：分页加载全图所有边向量（每页 1000，自动翻页）
    // ══════════════════════════════════════════════════════════
    private async Task<List<VectorMetadata>> LoadAllEdgeVectorsAsync(
        Guid graphGuid, CancellationToken ct)
    {
        var all = new List<VectorMetadata>();
        Guid? continuationToken = null;
        int pageNo = 0;

        while (true)
        {
            pageNo++;
            var query = new EnumerationRequest
            {
                TenantGUID = Guid.Empty,
                GraphGUID = graphGuid,
                MaxResults = 1000, // SDK 上限
                ContinuationToken = continuationToken
            };

            var page = await _sdk.Vector.Enumerate(query, ct);
            if (page?.Objects is null || page.Objects.Count == 0) break;

            // 只收集边向量
            all.AddRange(page.Objects.Where(v =>
                v.EdgeGUID.HasValue && v.Vectors is { Count: > 0 }));

            continuationToken = page.ContinuationToken;
            if (continuationToken is null) break;

            _logger.LogDebug("加载向量第 {Page} 页，累计 {Count} 条", pageNo, all.Count);

            // 防御：避免死循环
            if (pageNo > 100) break;
        }

        _logger.LogInformation(
            "图 {Graph} 共加载 {Count} 条边向量（{Pages} 页）",
            graphGuid, all.Count, pageNo);

        return all;
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