using System.Text.Json;
using MAFWorkFlowApi.Models.Graph;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace MAFWorkFlowApi.Services;

/// <summary>
/// LLM 意图解析 + L1(内存) / L2(Redis) 两级缓存。
/// 设计原则：
///   1. L1 命中 → 最快（< 1ms）
///   2. L2 命中 → 回填 L1（跨实例共享）
///   3. 都未命中 → 走 LLM + 规则兜底
///   4. 写入时 L1 和 L2 双写
/// </summary>
public sealed class IntentParserService
{
    // ── 缓存 TTL ──
    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan L2Ttl = TimeSpan.FromMinutes(30);

    // ── JSON 序列化配置（用于 L2 存 Redis） ──
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // ── 规则兜底映射 ──
    private static readonly Dictionary<string, (string Rel, string Dir)> RuleMap = new()
    {
        ["父亲"] = ("PARENT_OF", "in"), ["父节点"] = ("PARENT_OF", "in"),
        ["儿子"] = ("PARENT_OF", "out"), ["孩子"] = ("PARENT_OF", "out"),
        ["子领域"] = ("SUBFIELD", "out"), ["基于"] = ("BASED_ON", "out"),
        ["框架"] = ("FRAMEWORK", "out"), ["使用"] = ("USES", "out"),
        ["用到"] = ("USES", "out"), ["应用于"] = ("APPLIED_TO", "out"),
        ["实现"] = ("IMPLEMENTED_IN", "out"), ["变体"] = ("VARIANT", "out"),
        ["例子"] = ("EXAMPLE", "out"), ["方法"] = ("METHOD", "out"),
        ["相关"] = ("RELATED_TO", "out"),
    };

    private readonly IChatClient _chat;
    private readonly ILogger<IntentParserService> _logger;
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;

    public IntentParserService(
        IChatClient chat,
        ILogger<IntentParserService> logger,
        IMemoryCache l1,
        IDistributedCache l2)
    {
        _chat = chat;
        _logger = logger;
        _l1 = l1;
        _l2 = l2;
    }

    public async Task<IntentResult> ParseAsync(
        string query,
        IReadOnlyCollection<string> relations,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new IntentResult { Strategy = "none" };

        var cacheKey = BuildCacheKey(query, relations);

        // ══════════════════════════════════════════════════════
        // L1 命中
        // ══════════════════════════════════════════════════════
        if (_l1.TryGetValue(cacheKey, out IntentResult? l1Hit) && l1Hit is not null)
        {
            _logger.LogDebug("意图 L1 命中：{Query}", query);
            return l1Hit;
        }

        // ══════════════════════════════════════════════════════
        // L2 命中
        // ══════════════════════════════════════════════════════
        try
        {
            var l2Bytes = await _l2.GetAsync(cacheKey, ct);
            if (l2Bytes is { Length: > 0 })
            {
                var l2Hit = JsonSerializer.Deserialize<IntentResult>(l2Bytes, JsonOpts);
                if (l2Hit is not null)
                {
                    _logger.LogDebug("意图 L2 命中：{Query}", query);
                    // 回填 L1
                    _l1.Set(cacheKey, l2Hit, L1Ttl);
                    return l2Hit;
                }
            }
        }
        catch (Exception ex)
        {
            // Redis 挂了不影响主流程
            _logger.LogWarning(ex, "L2 读取失败，降级到 LLM");
        }

        // ══════════════════════════════════════════════════════
        // 都未命中：走 LLM
        // ══════════════════════════════════════════════════════
        IntentResult? result = null;
        try
        {
            result = await ParseByLlmAsync(query, relations, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 意图解析失败：{Query}", query);
        }

        if (result is null || result.Confidence < 0.5)
        {
            var ruleResult = ParseByRule(query, relations);
            result = ruleResult ?? result ?? new IntentResult { Strategy = "none" };
        }

        // ══════════════════════════════════════════════════════
        // 双写 L1 + L2
        // ══════════════════════════════════════════════════════
        _l1.Set(cacheKey, result, L1Ttl);

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result, JsonOpts);
            await _l2.SetAsync(
                cacheKey,
                bytes,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = L2Ttl
                },
                ct);
        }
        catch (Exception ex)
        {
            // 写失败不影响主流程
            _logger.LogWarning(ex, "L2 写入失败（不影响主流程）");
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════
    // 缓存 Key 构建
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 缓存 Key 格式：intent:v1:{query}:{relations sorted}
    /// 加 v1 前缀方便将来 schema 变更时无痛失效。
    /// </summary>
    private static string BuildCacheKey(
        string query,
        IReadOnlyCollection<string> relations)
    {
        var relKey = string.Join(",", relations.OrderBy(x => x, StringComparer.Ordinal));
        // 用 | 分隔避免 query 里含冒号导致歧义
        return $"intent:v1:{query}|{relKey}";
    }

    // ══════════════════════════════════════════════════════════
    // LLM 路径（不变）
    // ══════════════════════════════════════════════════════════

    private async Task<IntentResult?> ParseByLlmAsync(
        string query,
        IReadOnlyCollection<string> relations,
        CancellationToken ct)
    {
        var relList = string.Join(", ", relations);

        var system =
            $$"""
              你是图数据库查询意图解析器。把自然语言转为 JSON。

              图中关系名（必须从中选）：{{relList}}

              ═══════════════════════════════════════════════════
              字段 1：relation（关系名）
              ═══════════════════════════════════════════════════
              从上面列表选；无法判断填 null。

              ═══════════════════════════════════════════════════
              字段 2：direction（方向）
              ═══════════════════════════════════════════════════
              假设用户问 "X 的【某关系】"，direction 描述的是
              "这条关系相对于 X 的方向"：

              - "in"  = 边的终点是 X（即 其他节点 --关系--> X）
                        例："X的父亲"、"X的上级" → in

              - "out" = 边的起点是 X（即 X --关系--> 其他节点）
                        例："X的儿子"、"X的框架" → out

              中文方向词速查：
              - "父亲/父节点/上级/来源/被...引用" → in
              - "儿子/子节点/下级/目标/基于/使用/框架/例子/方法" → out

              ═══════════════════════════════════════════════════
              字段 3：aggregationMode（多跳聚合模式）
              ═══════════════════════════════════════════════════
              - null         = 单跳（默认）。例如："X的父亲"、"X的框架"
              - "ancestors"  = 沿关系反向递归找所有祖先
              - "descendants"= 沿关系正向递归找所有后代

              触发词速查：
              - "所有/全部/整条/递归 + 祖先/上级/父/来源" → ancestors
              - "所有/全部/整条/递归 + 后代/下级/子/派生/子孙" → descendants
              - 单独出现"祖先/后代/子孙" → ancestors/descendants

              ═══════════════════════════════════════════════════
              字段 4：hopCount（跳数）
              ═══════════════════════════════════════════════════
              - 1  = 单跳（默认）
              - 0  = 无限跳（遍历到尽头）
              - 2~5 = 指定跳数

              规则：aggregationMode 非 null 时，若用户没明确说"几跳"，默认 hopCount = 0。

              ═══════════════════════════════════════════════════
              字段 5：subjectName / confidence / reason
              ═══════════════════════════════════════════════════
              - subjectName: 主体节点名（如"根节点B的父亲"中的"根节点B"）
              - confidence: 0~1
              - reason: 一句话理由

              只输出 JSON，不要 markdown。

              ═══════════════════════════════════════════════════
              示例
              ═══════════════════════════════════════════════════
              输入：根节点B的父亲
              输出：{"relation":"PARENT_OF","direction":"in","subjectName":"根节点B","aggregationMode":null,"hopCount":1,"confidence":0.95,"reason":"单跳找父"}

              输入：深度学习用到的框架
              输出：{"relation":"FRAMEWORK","direction":"out","subjectName":"深度学习","aggregationMode":null,"hopCount":1,"confidence":0.9,"reason":"单跳找框架"}

              输入：根节点B的所有祖先
              输出：{"relation":"PARENT_OF","direction":"in","subjectName":"根节点B","aggregationMode":"ancestors","hopCount":0,"confidence":0.95,"reason":"所有祖先 → 沿 PARENT_OF 反向递归"}

              输入：深度学习的后代
              输出：{"relation":"SUBFIELD","direction":"out","subjectName":"深度学习","aggregationMode":"descendants","hopCount":0,"confidence":0.9,"reason":"后代 → 沿 SUBFIELD 正向递归"}

              输入：根节点B往上 2 跳
              输出：{"relation":"PARENT_OF","direction":"in","subjectName":"根节点B","aggregationMode":"ancestors","hopCount":2,"confidence":0.9,"reason":"明确 2 跳"}
              """;

        // ══════════════════════════════════════════════════════
        // ★ 关键：以下代码不能省！
        // ══════════════════════════════════════════════════════

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            // qwen3 默认 thinking，/no_think 关掉
            new(ChatRole.User, $"/no_think\n用户查询：{query}")
        };

        var response = await _chat.GetResponseAsync(messages, cancellationToken: ct);
        var raw = ExtractJson(response.Text ?? string.Empty);

        _logger.LogInformation("LLM 意图原始输出：{Raw}", raw);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var rel = ReadStr(root, "relation");
        if (rel is not null && !relations.Contains(rel, StringComparer.Ordinal))
            rel = null; // 白名单校验

        return new IntentResult
        {
            Relation = rel,
            Direction = ReadStr(root, "direction"),
            SubjectName = ReadStr(root, "subjectName"),
            Confidence = ReadDbl(root, "confidence", 0.5),
            Reason = ReadStr(root, "reason"),
            Strategy = "llm",

            // ★ 阶段 4 新增
            HopCount = Math.Clamp(ReadInt(root, "hopCount", 1), 0, 10),
            AggregationMode = NormalizeAggregationMode(ReadStr(root, "aggregationMode"))
        };
    }

    /// <summary>校验 aggregationMode 白名单。</summary>
    private static string? NormalizeAggregationMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "ancestors" => "ancestors",
            "descendants" => "descendants",
            _ => null
        };
    }

    private static int ReadInt(JsonElement root, string name, int dflt)
    {
        if (!root.TryGetProperty(name, out var p)) return dflt;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.GetInt32(),
            JsonValueKind.String => int.TryParse(p.GetString(), out var i) ? i : dflt,
            _ => dflt
        };
    }

    // ══════════════════════════════════════════════════════════
    // 规则兜底（不变）
    // ══════════════════════════════════════════════════════════

    private static IntentResult? ParseByRule(
        string query,
        IReadOnlyCollection<string> relations)
    {
        // ★ 先尝试多跳识别
        var multiHop = ParseMultiHopByRule(query, relations);
        if (multiHop is not null) return multiHop;

        // 单跳（原有逻辑）
        foreach (var (kw, (rel, dir)) in RuleMap)
        {
            if (!query.Contains(kw) || !relations.Contains(rel, StringComparer.Ordinal))
                continue;
            return new IntentResult
            {
                Relation = rel, Direction = dir, Confidence = 0.6,
                Reason = $"规则匹配 '{kw}'", Strategy = "rule",
                HopCount = 1, AggregationMode = null
            };
        }

        return null;
    }

    private static IntentResult? ParseMultiHopByRule(
        string query,
        IReadOnlyCollection<string> relations)
    {
        // 触发词
        bool wantsAncestors =
            query.Contains("所有祖先") || query.Contains("全部祖先") ||
            query.Contains("所有上级") || query.Contains("所有父") ||
            query.Contains("祖先");

        bool wantsDescendants =
            query.Contains("所有后代") || query.Contains("全部后代") ||
            query.Contains("所有子孙") || query.Contains("所有子") ||
            query.Contains("后代") || query.Contains("子孙");

        if (!wantsAncestors && !wantsDescendants) return null;

        // 猜关系：优先 PARENT_OF，其次 SUBFIELD
        string rel = wantsAncestors || wantsDescendants
            ? (relations.Contains("PARENT_OF") ? "PARENT_OF" : relations.FirstOrDefault() ?? "")
            : "";
        if (string.IsNullOrEmpty(rel)) return null;

        // 抽跳数（简单正则）
        int hop = 0;
        var m = System.Text.RegularExpressions.Regex.Match(
            query, @"(\d+)\s*跳");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var h))
            hop = Math.Clamp(h, 2, 5);

        return new IntentResult
        {
            Relation = rel,
            Direction = wantsAncestors ? "in" : "out",
            Confidence = 0.7,
            Reason = $"规则匹配多跳：{(wantsAncestors ? "ancestors" : "descendants")}",
            Strategy = "rule",
            HopCount = hop,
            AggregationMode = wantsAncestors ? "ancestors" : "descendants"
        };
    }

    // ══════════════════════════════════════════════════════════
    // 辅助（不变）
    // ══════════════════════════════════════════════════════════

    private static string ExtractJson(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return (start >= 0 && end > start) ? raw[start..(end + 1)] : raw;
    }

    private static string? ReadStr(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            return null;
        var v = p.GetString();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static double ReadDbl(JsonElement root, string name, double dflt)
    {
        if (!root.TryGetProperty(name, out var p)) return dflt;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.GetDouble(),
            JsonValueKind.String => double.TryParse(p.GetString(), out var d) ? d : dflt,
            _ => dflt
        };
    }
}