namespace MAFWorkFlowApi.Services;

/// <summary>
/// 轻量级 BM25 打分器。适合图规模 &lt; 1 万文档的场景。
///
/// 特点：
/// - 英文按 word 切分 + 小写化
/// - 中文按 unigram + bigram 混合切分（不依赖分词库）
/// - 标准 BM25 公式（k1=1.5, b=0.75）
/// - 支持归一化到 [0,1] 便于与向量分数融合
/// </summary>
public sealed class Bm25Scorer
{
    // BM25 标准参数
    private const double K1 = 1.5;
    private const double B = 0.75;

    private readonly List<string[]> _tokenizedDocs;
    private readonly Dictionary<string, int> _docFreq;    // 词 → 出现的文档数
    private readonly double _avgDocLen;
    private readonly int _docCount;

    public Bm25Scorer(IReadOnlyList<string> documents)
    {
        _docCount = documents.Count;
        _tokenizedDocs = documents.Select(Tokenize).ToList();
        _docFreq = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var doc in _tokenizedDocs)
        {
            foreach (var term in doc.Distinct(StringComparer.Ordinal))
                _docFreq[term] = _docFreq.GetValueOrDefault(term) + 1;
        }

        _avgDocLen = _tokenizedDocs.Count > 0
            ? _tokenizedDocs.Average(d => d.Length)
            : 1.0;

        if (_avgDocLen <= 0) _avgDocLen = 1.0;
    }

    /// <summary>对单个文档计算 BM25 分数（原始值，未归一化）。</summary>
    public double Score(string query, int docIndex)
    {
        if (docIndex < 0 || docIndex >= _tokenizedDocs.Count) return 0;

        var queryTerms = Tokenize(query);
        if (queryTerms.Length == 0) return 0;

        var doc = _tokenizedDocs[docIndex];
        var docLen = doc.Length;
        if (docLen == 0) return 0;

        // 预计算文档内的词频
        var docTermFreq = doc
            .GroupBy(t => t, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        double score = 0;

        foreach (var term in queryTerms.Distinct(StringComparer.Ordinal))
        {
            if (!_docFreq.TryGetValue(term, out var df)) continue;
            if (!docTermFreq.TryGetValue(term, out var tf)) continue;

            // IDF = ln(1 + (N - df + 0.5) / (df + 0.5))
            var idf = Math.Log(1 + (_docCount - df + 0.5) / (df + 0.5));

            // 分子：tf * (k1 + 1)
            // 分母：tf + k1 * (1 - b + b * |d| / avgdl)
            var numerator = tf * (K1 + 1);
            var denominator = tf + K1 * (1 - B + B * docLen / _avgDocLen);

            score += idf * numerator / denominator;
        }

        return score;
    }

    /// <summary>
    /// 对所有文档打分并归一化到 [0,1]。
    /// 全 0 时返回全 0 数组（避免除零）。
    /// </summary>
    public double[] ScoreAllNormalized(string query)
    {
        var raw = new double[_docCount];
        for (int i = 0; i < _docCount; i++)
            raw[i] = Score(query, i);

        var max = raw.Length > 0 ? raw.Max() : 0;
        if (max <= 0) return raw;   // 全 0 原样返回

        for (int i = 0; i < raw.Length; i++)
            raw[i] /= max;

        return raw;
    }

    // ══════════════════════════════════════════════════════
    // 分词：英文按 word，中文按 unigram + bigram
    // ══════════════════════════════════════════════════════

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var tokens = new List<string>();
        var asciiBuffer = new System.Text.StringBuilder();
        var chineseChars = new List<char>();

        // 把当前累积的中文字符 flush 成 unigram + bigram
        void FlushChinese()
        {
            if (chineseChars.Count == 0) return;

            // unigram
            foreach (var c in chineseChars)
                tokens.Add(c.ToString());

            // bigram
            for (int i = 0; i < chineseChars.Count - 1; i++)
                tokens.Add($"{chineseChars[i]}{chineseChars[i + 1]}");

            chineseChars.Clear();
        }

        // 把当前累积的 ASCII 词 flush
        void FlushAscii()
        {
            if (asciiBuffer.Length > 0)
            {
                tokens.Add(asciiBuffer.ToString());
                asciiBuffer.Clear();
            }
        }

        foreach (var c in text)
        {
            // ASCII 字母数字 → 累积为 word
            if (char.IsLetterOrDigit(c) && c < 128)
            {
                FlushChinese();
                asciiBuffer.Append(char.ToLowerInvariant(c));
            }
            // 中文汉字 → 累积为汉字列表
            else if (c >= 0x4E00 && c <= 0x9FFF)
            {
                FlushAscii();
                chineseChars.Add(c);
            }
            // 分隔符 → 全部 flush
            else
            {
                FlushAscii();
                FlushChinese();
            }
        }

        FlushAscii();
        FlushChinese();

        return tokens.ToArray();
    }
}