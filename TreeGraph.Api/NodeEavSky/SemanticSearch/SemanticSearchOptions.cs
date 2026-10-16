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

    public int DefaultTopK { get; set; } = 10;
    public int MaxTopK { get; set; } = 50;
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
