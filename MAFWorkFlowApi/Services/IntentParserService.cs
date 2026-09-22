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

            ★ direction 的定义（关键！必须按这个判断）：
              假设用户问 "X 的【某关系】"，direction 描述的是
              "这条关系相对于 X 的方向"：

              - "in"  = 边的终点是 X（即 其他节点 --关系--> X）
                        例："X的父亲"、"X的父节点"、"X的上级" → in
                        因为关系边是 父 --PARENT_OF--> X

              - "out" = 边的起点是 X（即 X --关系--> 其他节点）
                        例："X的儿子"、"X的子节点"、"X的框架" → out
                        因为关系边是 X --PARENT_OF--> 子

            ★ 中文方向词速查：
              - "父亲/父节点/上级/来源/被...引用" → in
              - "儿子/子节点/下级/目标/基于/使用/框架/例子/方法" → out

            输出 JSON 字段：
            - relation: 从列表选；无法判断填 null
            - direction: "in" 或 "out"，按上面规则判断
            - subjectName: 查询里的主体节点名（如"根节点B的父亲"中的"根节点B"）
            - confidence: 0~1
            - reason: 一句话理由

            只输出 JSON，不要 markdown。

            示例：
            输入：根节点B的父亲
            输出：{"relation":"PARENT_OF","direction":"in","subjectName":"根节点B","confidence":0.95,"reason":"父亲指 B 的父节点，父 --PARENT_OF--> B，所以是入边"}

            输入：深度学习用到的框架
            输出：{"relation":"FRAMEWORK","direction":"out","subjectName":"深度学习","confidence":0.9,"reason":"深度学习 --FRAMEWORK--> 框架，所以是出边"}
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, $"/no_think\n用户查询：{query}")
        };

        var response = await _chat.GetResponseAsync(messages, cancellationToken: ct);
        var raw = ExtractJson(response.Text ?? string.Empty);

        _logger.LogInformation("LLM 意图原始输出：{Raw}", raw);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var rel = ReadStr(root, "relation");
        if (rel is not null && !relations.Contains(rel, StringComparer.Ordinal))
            rel = null;   // 白名单校验

        return new IntentResult
        {
            Relation = rel,
            Direction = ReadStr(root, "direction"),
            SubjectName = ReadStr(root, "subjectName"),
            Confidence = ReadDbl(root, "confidence", 0.5),
            Reason = ReadStr(root, "reason"),
            Strategy = "llm"
        };
    }

    // ══════════════════════════════════════════════════════════
    // 规则兜底（不变）
    // ══════════════════════════════════════════════════════════

    private static IntentResult? ParseByRule(
        string query,
        IReadOnlyCollection<string> relations)
    {
        foreach (var (kw, (rel, dir)) in RuleMap)
        {
            if (!query.Contains(kw) || !relations.Contains(rel, StringComparer.Ordinal))
                continue;

            return new IntentResult
            {
                Relation = rel,
                Direction = dir,
                Confidence = 0.6,
                Reason = $"规则匹配 '{kw}'",
                Strategy = "rule"
            };
        }
        return null;
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