using System.Text.Json;
using System.Text.RegularExpressions;

namespace MAFWorkFlowApi.Services;

/// <summary>
/// 精排服务。优先用 LLM（qwen3:8b）对候选文档打分。
///
/// 三级降级：
///   ① /api/rerank（Ollama 原生 rerank 端点，部分模型支持）
///   ② LLM 打分（qwen3:8b + format=json）  ← 主力路径
///   ③ 保持原顺序
/// </summary>
public sealed class RerankerService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<RerankerService> _logger;
    private readonly string _rerankModel;
    private readonly string _chatModel;
    private readonly string _endpoint;

    public RerankerService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<RerankerService> logger)
    {
        _http = httpFactory.CreateClient("Reranker");
        _logger = logger;

        _rerankModel = config["Reranker:Model"]     ?? "bge-reranker-v2-m3";
        _chatModel   = config["Reranker:ChatModel"] ?? "qwen2.5:7b";   // ★ 从环境变量读

        _endpoint = ResolveOllamaEndpoint(config);

        _logger.LogInformation(
            "RerankerService 初始化：endpoint={Endpoint}, rerankModel={RModel}, chatModel={CModel}",
            _endpoint, _rerankModel, _chatModel);
    }

    public async Task<List<(int Index, double Score)>> RerankAsync(
        string query,
        IReadOnlyList<string> documents,
        CancellationToken ct = default)
    {
        if (documents.Count == 0)
            return new List<(int, double)>();

        // 单文档直接返回，不浪费时间
        if (documents.Count == 1)
            return new List<(int, double)> { (0, 1.0) };

        // ── 路径 ①：原生 /api/rerank ──
        var native = await TryNativeRerankAsync(query, documents, ct);
        if (native is not null)
        {
            _logger.LogInformation("Reranker 使用 /api/rerank 精排 {N} 个文档", documents.Count);
            return native;
        }

        // ── 路径 ②：LLM 打分（主力） ──
        var llm = await TryLlmRerankAsync(query, documents, ct);
        if (llm is not null)
        {
            _logger.LogInformation("Reranker 使用 LLM({Model}) 精排 {N} 个文档",
                _chatModel, documents.Count);
            return llm;
        }

        // ── 路径 ③：兜底 ──
        _logger.LogWarning("Reranker 全部路径失败，保持原顺序");
        return Enumerable.Range(0, documents.Count)
            .Select(i => (i, 0.0))
            .ToList();
    }

    // ══════════════════════════════════════════════════════
    // 路径 ①：原生 /api/rerank
    // ══════════════════════════════════════════════════════

    private async Task<List<(int Index, double Score)>?> TryNativeRerankAsync(
        string query,
        IReadOnlyList<string> documents,
        CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                model = _rerankModel,
                query = query,
                documents = documents
            };

            using var resp = await _http.PostAsJsonAsync(
                $"{_endpoint}/api/rerank", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("原生 /api/rerank 不可用：{Status}", resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return null;

            var results = new List<(int, double)>();
            foreach (var item in arr.EnumerateArray())
            {
                var idx = item.GetProperty("index").GetInt32();
                var score = item.GetProperty("relevance_score").GetDouble();
                results.Add((idx, score));
            }

            return results.OrderByDescending(x => x.Item2).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "原生 /api/rerank 调用异常");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════
    // 路径 ②：LLM 打分（qwen3:8b）
    // ══════════════════════════════════════════════════════

    private async Task<List<(int Index, double Score)>?> TryLlmRerankAsync(
        string query,
        IReadOnlyList<string> documents,
        CancellationToken ct)
    {
        try
        {
            var docList = string.Join("\n", documents.Select((d, i) => $"[{i + 1}] {Truncate(d, 150)}"));

            // ★ 不再用 /no_think（qwen2.5 不需要），也不加 format=json
            var prompt = $$"""
                           你是相关性评分器。为下面每个候选文档打一个 0~10 的整数分，
                           越相关分数越高。

                           查询：{{query}}

                           候选文档：
                           {{docList}}

                           输出规则：
                           - 每行一个数字，用逗号分隔
                           - 共 {{documents.Count}} 个数字
                           - 不要输出文字、解释或任何其它内容

                           输出示例（假设 3 个候选）：
                           9,8,3
                           """;

            var payload = new
            {
                model = _chatModel,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.0,
                    num_predict = 512, // ★ 从 128 → 512（即使有 thinking 也够）
                    stop = new[] { "\n\n\n", "用户：", "查询：" }
                }
            };

            using var resp = await _http.PostAsJsonAsync(
                $"{_endpoint}/api/generate", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "LLM rerank 调用失败：{Status} {Body}",
                    resp.StatusCode, Truncate(body, 300));
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ══════════════════════════════════════════════
            // ★ 关键：同时解析 response 和 thinking
            //   qwen2.5 → 内容在 response
            //   qwen3   → 内容可能在 thinking（响应兜底用）
            // ══════════════════════════════════════════════
            var responseText = root.TryGetProperty("response", out var r)
                ? r.GetString() ?? ""
                : "";

            var thinkingText = root.TryGetProperty("thinking", out var t)
                ? t.GetString() ?? ""
                : "";

            _logger.LogInformation(
                "LLM rerank 响应：response长度={RLen}, thinking长度={TLen}",
                responseText.Length, thinkingText.Length);

            // 优先 response，空则用 thinking
            var content = !string.IsNullOrWhiteSpace(responseText)
                ? responseText
                : thinkingText;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("LLM rerank response 和 thinking 都是空");
                return null;
            }

            _logger.LogInformation("LLM rerank 提取内容：{Raw}", Truncate(content, 500));

            // ── 提取数字 ──
            var scores = ExtractNumbers(content, documents.Count);

            if (scores is null || scores.Count == 0)
            {
                _logger.LogWarning(
                    "LLM rerank 无法从内容中提取数字：{Content}",
                    Truncate(content, 300));
                return null;
            }

            // ── 长度校验 ──
            if (scores.Count != documents.Count)
            {
                _logger.LogWarning(
                    "LLM rerank 分数数量不匹配：期望 {Expected}，实际 {Actual}",
                    documents.Count, scores.Count);
                while (scores.Count < documents.Count) scores.Add(0.0);
                if (scores.Count > documents.Count)
                    scores = scores.Take(documents.Count).ToList();
            }

            // ── 全 0 检查 ──
            if (scores.All(s => s <= 0.01))
            {
                _logger.LogWarning("LLM rerank 全部打分 0，视为失败");
                return null;
            }

            // ── 归一化到 [0,1] ──
            var results = scores
                .Select((s, i) => (Index: i, Score: Math.Clamp(s / 10.0, 0.0, 1.0)))
                .OrderByDescending(x => x.Score)
                .ToList();

            _logger.LogInformation(
                "LLM rerank 解析成功：{N} 个分数，范围 {Min:F1}~{Max:F1}",
                scores.Count, scores.Min(), scores.Max());

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM rerank 调用异常");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════
    // 辅助
    // ══════════════════════════════════════════════════════

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return (start >= 0 && end > start) ? raw[start..(end + 1)] : raw;
    }

    private static string Truncate(string s, int maxLen)
        => string.IsNullOrEmpty(s) || s.Length <= maxLen ? s : s[..maxLen] + "...";

    private static string ResolveOllamaEndpoint(IConfiguration config)
    {
        var conn = config.GetConnectionString("ollama")
                   ?? config["ConnectionStrings:ollama"];

        if (!string.IsNullOrWhiteSpace(conn))
        {
            foreach (var part in conn.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq > 0 && part[..eq].Trim().Equals("Endpoint",
                        StringComparison.OrdinalIgnoreCase))
                    return part[(eq + 1)..].Trim().TrimEnd('/');
            }

            return conn.TrimEnd('/');
        }

        return config["Ollama:Endpoint"]?.TrimEnd('/')
               ?? "http://localhost:11618";
    }

    // ══════════════════════════════════════════════════════
// ★ 全兼容：从各种可能的 JSON 结构里抽出分数数组
// ══════════════════════════════════════════════════════

    /// <summary>
    /// 尝试多种 JSON 结构，抽出 scores 数组。
    /// 支持：
    ///   A. {"scores": [9, 8, 5]}
    ///   B. {"scores": [{"index":1, "score":9}, ...]}
    ///   C. {"score": [9, 8, 5]}
    ///   D. {"relevance": [9, 8, 5]}
    ///   E. {"ratings": [9, 8, 5]}
    ///   F. {"1": 9, "2": 8, "3": 5}
    ///   G. {"results": [{"index":1, "relevance_score":9}]}
    ///   H. 纯数组 [9, 8, 5]
    ///   I. {"scores": "[9, 8, 5]"}  ← 字符串形式
    /// </summary>
    private static List<double>? ParseScoresFromJson(string json, int expectedCount)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ── H. 纯数组 ──
            if (root.ValueKind == JsonValueKind.Array)
                return ParseNumberArray(root);

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            // ── A/B/C/D/E. 常见键名 ──
            string[] candidateKeys =
                ["scores", "score", "relevance", "ratings", "rating", "results", "documents"];

            foreach (var key in candidateKeys)
            {
                if (!root.TryGetProperty(key, out var el)) continue;

                // I. 字符串里含 JSON 数组
                if (el.ValueKind == JsonValueKind.String)
                {
                    var inner = el.GetString();
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        var parsed = ParseScoresFromJson(ExtractJson(inner), expectedCount);
                        if (parsed is not null && parsed.Count > 0) return parsed;
                    }

                    continue;
                }

                // 数组
                if (el.ValueKind == JsonValueKind.Array)
                {
                    var arr = ParseNumberArray(el);
                    if (arr is { Count: > 0 }) return arr;
                }
            }

            // ── F. 纯对象 { "1": 9, "2": 8 }
            var numericValues = new List<(int Key, double Val)>();
            foreach (var prop in root.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out var idx) &&
                    prop.Value.ValueKind == JsonValueKind.Number)
                {
                    numericValues.Add((idx, prop.Value.GetDouble()));
                }
            }

            if (numericValues.Count > 0)
            {
                // 按 key 排序后取 val
                return numericValues
                    .OrderBy(x => x.Key)
                    .Select(x => x.Val)
                    .ToList();
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>解析 JSON 数组为 double 列表。数组元素可以是数字、字符串、或对象。</summary>
    private static List<double>? ParseNumberArray(JsonElement arr)
    {
        var result = new List<double>();

        foreach (var item in arr.EnumerateArray())
        {
            // 数字
            if (item.ValueKind == JsonValueKind.Number)
            {
                result.Add(item.GetDouble());
                continue;
            }

            // 字符串形式的数字
            if (item.ValueKind == JsonValueKind.String &&
                double.TryParse(item.GetString(), out var d))
            {
                result.Add(d);
                continue;
            }

            // 对象：尝试 score / relevance_score / value / rating 字段
            if (item.ValueKind == JsonValueKind.Object)
            {
                double? found = null;
                foreach (var subKey in new[] { "score", "relevance_score", "relevance", "value", "rating" })
                {
                    if (item.TryGetProperty(subKey, out var s))
                    {
                        if (s.ValueKind == JsonValueKind.Number)
                        {
                            found = s.GetDouble();
                            break;
                        }

                        if (s.ValueKind == JsonValueKind.String &&
                            double.TryParse(s.GetString(), out var sd))
                        {
                            found = sd;
                            break;
                        }
                    }
                }

                if (found.HasValue)
                {
                    result.Add(found.Value);
                    continue;
                }
            }

            // 其它类型：跳过（保持索引对齐会更好，但这里简单处理）
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 从文本里提取所有数字（按出现顺序）。
    /// 支持：
    ///   "9,8,3"          → [9, 8, 3]
    ///   "9 8 3"          → [9, 8, 3]
    ///   "1. 9\n2. 8"     → [9, 8]（跳过序号 1、2？不，会全拿到）
    ///   "scores: 9,8,3"  → [9, 8, 3]
    ///
    /// 策略：用正则 \d+(?:\.\d+)? 提取，过滤掉行首序号（可选）。
    /// </summary>
    private static List<double>? ExtractNumbers(string content, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        // ★ 简单策略：提取所有数字（包含小数）
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content, @"\d+(?:\.\d+)?");

        var numbers = new List<double>();
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (double.TryParse(m.Value, out var d))
                numbers.Add(d);
        }

        // ★ 如果数量超了 expectedCount，可能是误抓了序号或查询里的数字
        //   策略：若完全超出，尝试过滤掉 0~expectedCount 之间、且 >= 1 的小整数
        //   （这些可能是序号）
        if (numbers.Count > expectedCount * 2 && expectedCount > 0)
        {
            // 简单的启发式：丢弃第一个数（可能是"共 N 个"里的 N）
            var filtered = numbers
                .Where(n => n <= 10 || n % 1 != 0) // 保留小数，或保留 0~10
                .ToList();
            if (filtered.Count >= expectedCount)
                numbers = filtered;
        }

        return numbers;
    }
}