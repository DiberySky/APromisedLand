using System.Text.Json;
using LiteGraph.Sdk;
using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Models.Graph;
using Microsoft.Extensions.AI;

namespace MAFWorkFlowApi.Services;

/// <summary>
/// 语义搜索编排服务。
/// 从 GraphController 提取，职责：
///   1. 意图解析（LLM + 规则兜底 + 两级缓存）
///   2. 多跳 BFS 遍历（祖先/后代模式）
///   3. 混合检索：向量余弦 + BM25
///   4. Reranker 精排融合
///   5. 严格方向过滤 + 反查节点
///   6. 基于图数据的查询建议生成
/// </summary>
public sealed class SemanticSearchService
{
    private const int RecallTopK = 20;
    private const double RerankWeight = 0.5;
    private const double OriginalWeight = 0.5;

    private readonly LiteGraphRestClient _liteGraph;
    private readonly LiteGraphSdk _sdk;
    private readonly IntentParserService _intentParser;
    private readonly RerankerService _reranker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly string _embeddingModelName;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        LiteGraphRestClient liteGraph,
        LiteGraphSdk sdk,
        IntentParserService intentParser,
        RerankerService reranker,
        [FromKeyedServices("embedding")] IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IConfiguration configuration,
        ILogger<SemanticSearchService> logger)
    {
        _liteGraph = liteGraph;
        _sdk = sdk;
        _intentParser = intentParser;
        _reranker = reranker;
        _embeddingGenerator = embeddingGenerator;
        _embeddingModelName = configuration["Embedding:Model"] ?? "bge-m3";
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════
    // 公开入口
    // ══════════════════════════════════════════════════════════

    public async Task<SemanticSearchResponseDto> SearchAsync(
        Guid graphGuid,
        SemanticSearchRequest request,
        CancellationToken ct)
    {
        // ① 意图解析
        var relations = await LoadGraphRelationsAsync(graphGuid, ct);
        var intent = await _intentParser.ParseAsync(request.Query, relations, ct);

        // 方向覆盖：前端显式指定时优先
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

        // ② 多跳分支（不走向量）
        if (intent.AggregationMode is "ancestors" or "descendants"
            && intent.HopCount != 1
            && !string.IsNullOrWhiteSpace(intent.SubjectName))
        {
            return await SearchMultiHopAsync(graphGuid, intent, request.TopK, ct);
        }

        // ③ 生成查询向量
        var queryVec = await GenerateQueryEmbeddingAsync(request.Query, ct);
        if (queryVec is null || queryVec.Count == 0)
            return new SemanticSearchResponseDto
            {
                Intent = intent,
                Hint = "无法生成查询向量",
                Suggestions = new List<SuggestedRelationDto>()
            };

        // ④ 加载所有边向量
        var allVectors = await EnumerateAllVectorsAsync(graphGuid, ct: ct);
        var edgeVecs = allVectors
            .Where(v => v.EdgeGUID.HasValue && v.Vectors is { Count: > 0 })
            .ToList();

        if (edgeVecs.Count == 0)
        {
            return new SemanticSearchResponseDto
            {
                Intent = intent,
                Hint = "图中无边向量，请先重建所有向量",
                Suggestions = new List<SuggestedRelationDto>()
            };
        }

        // ⑤ 混合检索：向量 + BM25
        var docs = edgeVecs.Select(v => v.Content ?? "").ToList();
        var bm25 = new Bm25Scorer(docs);
        var bm25Scores = bm25.ScoreAllNormalized(request.Query);

        var alpha = request.VectorWeight;
        var beta = request.Bm25Weight;

        var scored = edgeVecs
            .Select((v, i) => new ScoredEdge
            {
                Vector = v,
                VectorScore = Cosine(v.Vectors!, queryVec),
                Bm25Score = bm25Scores[i]
            })
            .Select(x => x with { FinalScore = alpha * x.VectorScore + beta * x.Bm25Score })
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

        // ⑥ Reranker 精排
        if (request.UseReranker && scored.Count > 1)
        {
            scored = ApplyReranker(scored, request.Query, ct);
        }
        else if (!request.UseReranker)
        {
            _logger.LogInformation("Reranker 已禁用（UseReranker=false）");
        }

        // ⑦ 严格方向过滤 + 反查节点
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

                // 严格方向过滤
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

        // ⑧ 生成建议
        var suggestions = await BuildSuggestionsAsync(
            graphGuid, subjectGuid, intent, nodeCache, ct);

        // ⑨ 组装响应
        if (hits.Count == 0)
        {
            var dirZh = DirZh(intent.Direction);
            var finalHint = suggestions.Count > 0
                ? "「" + intent.SubjectName + "」没有通过 " + intent.Relation
                  + "（" + dirZh + "）连接的节点。试试下面这些："
                : "「" + intent.SubjectName + "」在图里没有符合条件的关系";

            return new SemanticSearchResponseDto
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
            };
        }

        string? okHint = null;
        if (suggestions.Count > 0)
            okHint = "「" + intent.SubjectName + "」还有其他关系可以查：";

        return new SemanticSearchResponseDto
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
        };
    }

    // ══════════════════════════════════════════════════════════
    // 多跳搜索分支
    // ══════════════════════════════════════════════════════════

    private async Task<SemanticSearchResponseDto> SearchMultiHopAsync(
        Guid graphGuid,
        IntentResult intent,
        int topK,
        CancellationToken ct)
    {
        var multiHopSubjectGuid = await FindNodeByNameAsync(
            graphGuid, intent.SubjectName!, new Dictionary<Guid, Node>(), ct);

        if (multiHopSubjectGuid is null)
        {
            return new SemanticSearchResponseDto
            {
                Intent = intent,
                Hint = "主体节点未找到：" + intent.SubjectName,
                Suggestions = new List<SuggestedRelationDto>()
            };
        }

        var maxHops = intent.HopCount <= 0 ? 10 : intent.HopCount;

        var reachable = await BfsTraverseAsync(
            graphGuid, multiHopSubjectGuid.Value,
            intent.Relation!, intent.AggregationMode!, maxHops, ct);

        _logger.LogInformation(
            "多跳遍历：Mode={Mode}, Rel={Rel}, MaxHops={Max}, Found={Count}",
            intent.AggregationMode, intent.Relation, maxHops, reachable.Count);

        // 空结果兜底：给出可用关系建议
        if (reachable.Count == 0)
        {
            var availableRelations = await GetSubjectRelationsAsync(
                graphGuid, multiHopSubjectGuid.Value,
                intent.AggregationMode!, ct);

            var availText = availableRelations.Count > 0
                ? string.Join("、", availableRelations)
                : "无";

            return new SemanticSearchResponseDto
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
            };
        }

        var multiHits = new List<SemanticSearchHitDto>();
        foreach (var (nodeGuid, distance, viaEdgeName) in
                 reachable.OrderBy(x => x.Distance).Take(topK))
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

        return new SemanticSearchResponseDto
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
        };
    }

    // ══════════════════════════════════════════════════════════
    // Reranker 精排
    // ══════════════════════════════════════════════════════════

    private List<ScoredEdge> ApplyReranker(
        List<ScoredEdge> scored,
        string query,
        CancellationToken ct)
    {
        var candidates = scored.Take(RecallTopK).ToList();
        var candidateDocs = candidates.Select(c => c.Vector.Content ?? "").ToList();

        _logger.LogInformation(
            "Reranker 精排启动：候选 {N} 个，Query={Q}",
            candidates.Count, query);

        try
        {
            var reranked = _reranker.RerankAsync(query, candidateDocs, ct).GetAwaiter().GetResult();

            var allZero = reranked.Count == 0
                          || reranked.All(x => Math.Abs(x.Score) < 1e-6);

            if (allZero)
            {
                _logger.LogWarning(
                    "Reranker 未生效（返回空或全 0），保留原混合分顺序");
                return scored;
            }

            var rerankMap = reranked.ToDictionary(x => x.Index, x => x.Score);

            var reorderedCandidates = candidates
                .Select((c, i) =>
                {
                    var llmScore = rerankMap.GetValueOrDefault(i, 0.0);
                    return c with
                    {
                        LlmScore = llmScore,
                        FinalScore = RerankWeight * llmScore + OriginalWeight * c.FinalScore
                    };
                })
                .OrderByDescending(x => x.FinalScore)
                .ToList();

            var rest = scored.Skip(RecallTopK).ToList();

            var merged = reorderedCandidates
                .Select(x => new ScoredEdge
                {
                    Vector = x.Vector,
                    VectorScore = x.VectorScore,
                    Bm25Score = x.Bm25Score,
                    FinalScore = x.FinalScore
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

            return merged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranker 精排异常，使用原始顺序");
            return scored;
        }
    }

    // ══════════════════════════════════════════════════════════
    // 建议生成
    // ══════════════════════════════════════════════════════════

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

        var outgoing = new Dictionary<string, (int Count, List<string> Nodes)>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, (int Count, List<string> Nodes)>(StringComparer.Ordinal);

        async Task<string> NameOfAsync(Guid g)
        {
            if (nodeCache.TryGetValue(g, out var cached)) return cached.Name;
            var node = await FetchNodeAsync(graphGuid, g, ct);
            var name = node?.Name ?? g.ToString("N")[..8];
            if (node is not null) nodeCache[g] = node;
            return name;
        }

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

        // 优先级 1：反向关系
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

        // 优先级 2：同方向的其他关系
        var same = intent.Direction == "out" ? outgoing : incoming;

        foreach (var (rel, info) in same.OrderByDescending(x => x.Value.Count))
        {
            if (string.Equals(rel, intent.Relation, StringComparison.Ordinal))
                continue;

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

    // ══════════════════════════════════════════════════════════
    // 数据访问辅助
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
        var r = await _embeddingGenerator.GenerateAsync(new[] { text }, cancellationToken: ct);
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

    private async Task<List<string>> GetSubjectRelationsAsync(
        Guid graphGuid, Guid nodeGuid, string mode, CancellationToken ct)
    {
        var allEdges = await LoadAllEdgesAsync(graphGuid, ct);
        var relations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in allEdges)
        {
            if (mode == "ancestors")
            {
                if (e.To == nodeGuid) relations.Add(e.Name);
            }
            else
            {
                if (e.From == nodeGuid) relations.Add(e.Name);
            }
        }

        return relations.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

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

    /// <summary>
    /// 分页拉取一个图的全部向量。
    /// LiteGraph 的 EnumerationRequest.MaxResults 硬上限 = 1000。
    /// </summary>
    private async Task<List<VectorMetadata>> EnumerateAllVectorsAsync(
        Guid graphGuid, int maxTotal = 100_000, CancellationToken ct = default)
    {
        const int pageSize = 1000;

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
    // BFS 多跳遍历
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 从起点沿指定关系 BFS 遍历。
    /// </summary>
    /// <param name="mode">"ancestors" = 沿 To→From 反向；"descendants" = 沿 From→To 正向</param>
    private async Task<List<(Guid NodeGuid, int Distance, string ViaEdgeName)>> BfsTraverseAsync(
        Guid graphGuid, Guid startGuid, string relation, string mode,
        int maxHops, CancellationToken ct)
    {
        const int maxNodes = 10_000;

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

    // ══════════════════════════════════════════════════════════
    // 纯函数辅助
    // ══════════════════════════════════════════════════════════

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

    private static string DirZh(string? dir) => dir switch
    {
        "in" => "入边/找父",
        "out" => "出边/找子",
        _ => "无方向"
    };

    // ══════════════════════════════════════════════════════════
    // 内部类型
    // ══════════════════════════════════════════════════════════

    /// <summary>带分数的边向量（不可变记录）。</summary>
    private sealed record ScoredEdge
    {
        public required VectorMetadata Vector { get; init; }
        public double VectorScore { get; init; }
        public double Bm25Score { get; init; }
        public double FinalScore { get; init; }
        public double LlmScore { get; init; }
    }
}
