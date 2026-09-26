using System.Text.Json;
using MAFWorkFlowApi.Infrastructure;

namespace MAFWorkFlowApi.Services;

/// <summary>
/// 精排服务。二级降级：
///   ① Cross-Encoder（Python 服务，bge-reranker-v2-m3）  ← 首选，50~150ms，精度 0.95+
///   ② LLM 打分（qwen3:8b，通过 Ollama /api/generate）    ← 兜底，2~5s
///   ③ 保持原顺序                                          ← 完全失败时
/// 任何一级失败自动降级，不影响搜索。
/// </summary>
public sealed class RerankerService
{
    private readonly HttpClient _http;
    private readonly ILogger<RerankerService> _logger;
    private readonly string _chatModel;               // LLM 打分模型（qwen3:8b）
    private readonly string _ollamaEndpoint;          // Ollama 端点（LLM 打分用）
    private readonly string? _crossEncoderEndpoint;   // Python Cross-Encoder 端点

    public RerankerService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<RerankerService> logger)
    {
        _http = httpFactory.CreateClient("Reranker");
        _logger = logger;

        _chatModel = config["Reranker:ChatModel"] ?? "qwen3:8b";
        _ollamaEndpoint = OllamaEndpointResolver.Resolve(config, fallback: "http://localhost:11618");
        _crossEncoderEndpoint = config["Reranker:Endpoint"];

        _logger.LogInformation(
            "RerankerService 初始化：ollama={Ollama}, crossEncoder={Cross}, chatModel={CModel}",
            _ollamaEndpoint,
            _crossEncoderEndpoint ?? "(未配置)",
            _chatModel);
    }

    // ══════════════════════════════════════════════════════
    // 二级降级编排
    // ══════════════════════════════════════════════════════
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

        // ── 路径 ①：Cross-Encoder（Python 服务）★ 首选 ──
        if (!string.IsNullOrWhiteSpace(_crossEncoderEndpoint))
        {
            var cross = await TryCrossEncoderAsync(query, documents, ct);
            if (cross is not null)
            {
                _logger.LogInformation(
                    "Reranker 使用 Cross-Encoder 精排 {N} 个文档", documents.Count);
                return cross;
            }
        }

        // ── 路径 ②：LLM 打分（qwen3:8b） ──
        var llm = await TryLlmRerankAsync(query, documents, ct);
        if (llm is not null)
        {
            _logger.LogInformation(
                "Reranker 使用 LLM({Model}) 精排 {N} 个文档",
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
    // ★ 路径 ①：Cross-Encoder（Python 服务）
    // ══════════════════════════════════════════════════════
    private async Task<List<(int Index, double Score)>?> TryCrossEncoderAsync(
        string query,
        IReadOnlyList<string> documents,
        CancellationToken ct)
    {
        try
        {
            var endpoint = _crossEncoderEndpoint!.TrimEnd('/');

            var payload = new
            {
                query = query,
                documents = documents
            };

            using var resp = await _http.PostAsJsonAsync(
                $"{endpoint}/rerank", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogDebug(
                    "Cross-Encoder 调用失败：{Status} {Body}",
                    resp.StatusCode, Truncate(body, 200));
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("results", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                _logger.LogDebug("Cross-Encoder 响应缺少 results 数组");
                return null;
            }

            var results = new List<(int Index, double Score)>();
            foreach (var item in arr.EnumerateArray())
            {
                if (!item.TryGetProperty("index", out var idxProp) ||
                    !item.TryGetProperty("relevance_score", out var scoreProp))
                    continue;

                var idx = idxProp.GetInt32();
                var score = scoreProp.GetDouble();
                results.Add((idx, score));
            }

            if (results.Count == 0)
            {
                _logger.LogDebug("Cross-Encoder 返回空结果");
                return null;
            }

            // ★ 不做二次归一化
            //   bge-reranker-v2-m3 的 CrossEncoder.predict() 已对 logits 施加 sigmoid，
            //   输出即为 [0,1] 相关性概率。
            return results
                .OrderByDescending(x => x.Score)
                .ToList();
        }
        catch (TaskCanceledException)
        {
            _logger.LogDebug("Cross-Encoder 调用超时或被取消");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cross-Encoder 调用异常");
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
            var docList = string.Join("\n", documents.Select(
                (d, i) => $"[{i + 1}] {Truncate(d, 150)}"));

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
                    num_predict = 512,
                    stop = new[] { "\n\n\n", "用户：", "查询：" }
                }
            };

            using var resp = await _http.PostAsJsonAsync(
                $"{_ollamaEndpoint}/api/generate", payload, ct);

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

            var responseText = root.TryGetProperty("response", out var r)
                ? r.GetString() ?? ""
                : "";

            var thinkingText = root.TryGetProperty("thinking", out var t)
                ? t.GetString() ?? ""
                : "";

            _logger.LogInformation(
                "LLM rerank 响应：response长度={RLen}, thinking长度={TLen}",
                responseText.Length, thinkingText.Length);

            var content = !string.IsNullOrWhiteSpace(responseText)
                ? responseText
                : thinkingText;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("LLM rerank response 和 thinking 都是空");
                return null;
            }

            _logger.LogInformation("LLM rerank 提取内容：{Raw}", Truncate(content, 500));

            var scores = ExtractNumbers(content, documents.Count);

            if (scores is null || scores.Count == 0)
            {
                _logger.LogWarning(
                    "LLM rerank 无法从内容中提取数字：{Content}",
                    Truncate(content, 300));
                return null;
            }

            if (scores.Count != documents.Count)
            {
                _logger.LogWarning(
                    "LLM rerank 分数数量不匹配：期望 {Expected}，实际 {Actual}",
                    documents.Count, scores.Count);
                while (scores.Count < documents.Count) scores.Add(0.0);
                if (scores.Count > documents.Count)
                    scores = scores.Take(documents.Count).ToList();
            }

            if (scores.All(s => s <= 0.01))
            {
                _logger.LogWarning("LLM rerank 全部打分 0，视为失败");
                return null;
            }

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

    private static string Truncate(string s, int maxLen)
        => string.IsNullOrEmpty(s) || s.Length <= maxLen ? s : s[..maxLen] + "...";

    /// <summary>从文本里提取所有数字（按出现顺序）。</summary>
    private static List<double>? ExtractNumbers(string content, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var matches = System.Text.RegularExpressions.Regex.Matches(
            content, @"\d+(?:\.\d+)?");

        var numbers = new List<double>();
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (double.TryParse(m.Value, out var d))
                numbers.Add(d);
        }

        if (numbers.Count > expectedCount * 2 && expectedCount > 0)
        {
            var filtered = numbers
                .Where(n => n <= 10 || n % 1 != 0)
                .ToList();
            if (filtered.Count >= expectedCount)
                numbers = filtered;
        }

        return numbers;
    }
}