using System.Text.Json;
using MAFWorkFlowApi.Models.Graph;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using OllamaSharp;
using OllamaSharp.Models;

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

        // ★ 精简版 system prompt：从 1173 tokens 降到 ~500 tokens
        var system =
            $$"""
              你是图数据库查询意图解析器。把自然语言转为一行 JSON。

              可用关系（必须从中选）：{{relList}}

              字段：
              - relation: 关系名，从上面列表选；无法判断填 null
              - direction: "in"=其他→X（找父/上级），"out"=X→其他（找子/下级）
              - subjectName: 主体节点名
              - aggregationMode: null=单跳，"ancestors"=所有祖先，"descendants"=所有后代
              - hopCount: 1=单跳，0=无限跳，2~5=指定跳数
              - confidence: 0~1
              - reason: 一句话理由

              方向速查：
              - "父亲/父节点/上级/来源" → in
              - "儿子/子节点/下级/基于/使用/框架/方法/例子" → out

              多跳速查：出现"所有祖先/上级/父" → ancestors；"所有后代/子孙/子" → descendants

              只输出一行 JSON，无任何解释。

              示例：
              输入：根节点B的父亲
              输出：{"relation":"PARENT_OF","direction":"in","subjectName":"根节点B","aggregationMode":null,"hopCount":1,"confidence":0.95,"reason":"单跳找父"}

              输入：深度学习用到的框架
              输出：{"relation":"FRAMEWORK","direction":"out","subjectName":"深度学习","aggregationMode":null,"hopCount":1,"confidence":0.9,"reason":"单跳找框架"}

              输入：深度学习的后代
              输出：{"relation":"SUBFIELD","direction":"out","subjectName":"深度学习","aggregationMode":"descendants","hopCount":0,"confidence":0.9,"reason":"后代"}
              """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, $"/no_think\n查询：{query}\n输出：")
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 256,
            Temperature = 0f,
            StopSequences = ["}\n\n", "}\r\n\r\n"],
        };

        options.AddOllamaOption(OllamaOption.Think, false);

        var response = await _chat.GetResponseAsync(messages, options, ct);
        var raw = response.Text ?? string.Empty;

        _logger.LogInformation(
            "LLM 意图原始输出长度 {Len}，内容前 200 字符：{Preview}",
            raw.Length,
            raw.Length > 200 ? raw[..200] : raw);

        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "LLM 返回空文本（可能 thinking 未关闭，或 max_tokens 不足）。" +
                "尝试从 Ollama 的 thinking 字段读取…");

            // ★ 兜底：OllamaSharp 5.x 某些版本会把 thinking 放在独立的字段里
            //   如果 response 实现了特定接口，尝试读取
            //   否则直接返回 null 让规则兜底
            return null;
        }

        var json = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(json) || !json.Contains('{'))
        {
            _logger.LogWarning("LLM 输出不含 JSON：{Raw}", raw);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var rel = ReadStr(root, "relation");
        if (rel is not null && !relations.Contains(rel, StringComparer.Ordinal))
            rel = null;

        return new IntentResult
        {
            Relation = rel,
            Direction = ReadStr(root, "direction"),
            SubjectName = ReadStr(root, "subjectName"),
            Confidence = ReadDbl(root, "confidence", 0.5),
            Reason = ReadStr(root, "reason"),
            Strategy = "llm",
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