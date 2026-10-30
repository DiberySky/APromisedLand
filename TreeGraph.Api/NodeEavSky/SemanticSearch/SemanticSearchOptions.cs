namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>语义搜索配置（SemanticSearch 节）。</summary>
public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";

    public bool Enabled { get; set; } = true;

    /// <summary>Qdrant gRPC 地址（Aspire 注入 ConnectionStrings:Qdrant 优先）。</summary>
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    public string CollectionName { get; set; } = "tree_nodes";

    /// <summary>向量维度（bge-m3 = 1024）。</summary>
    public int VectorDimension { get; set; } = 1024;

    /// <summary>vLLM OpenAI 兼容端点（含 /v1）。</summary>
    public string EmbeddingEndpoint { get; set; } = "http://localhost:5719/v1/";

    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>
    /// Embedding 服务失败后的短路冷却（秒）：失败后该窗口内所有向量化请求直接失败、不再发起 HTTP，
    /// 避免每次向量同步都在 60s 超时上等待、进而连坐同消息的图同步 handler。
    /// 0 = 禁用短路（每次都真实请求）。
    /// </summary>
    public int EmbeddingFailureCooldownSeconds { get; set; } = 60;

    public int DefaultTopK { get; set; } = 10;
    public int MaxTopK { get; set; } = 50;

    /// <summary>
    /// 相似度绝对下限：低于此分的命中直接不返回（请求值可覆盖，见 SemanticSearchRequest.MinScore）。
    /// 依据实测：与库内节点无关的查询最高分约 0.48，相关查询最高分 0.9 以上，取 0.5 可挡住全局噪声。
    /// </summary>
    public float MinScore { get; set; } = 0.5f;

    /// <summary>
    /// 相对容差：只保留 score ≥ (本轮最高分 − 该值) 的命中，用于剔除同批低相关噪声
    /// （实测同一父下兄弟节点间分差约 0.2，无此门槛时相关查询会带出一串无关兄弟节点）。
    /// 0 = 关闭相对过滤。
    /// </summary>
    public float RelativeMargin { get; set; } = 0.10f;
}

/// <summary>向量同步管道配置（VectorSync 节）。</summary>
public sealed class VectorSyncOptions
{
    public const string SectionName = "VectorSync";

    public bool Enabled { get; set; } = true;

    /// <summary>滑动去重窗口（秒）：同一 inodeId 在窗口内重复事件合并为一次刷新。</summary>
    public int DedupWindowSeconds { get; set; } = 5;

    /// <summary>对账周期（小时，0 = 禁用）。</summary>
    public double ReconciliationIntervalHours { get; set; } = 6;
}
