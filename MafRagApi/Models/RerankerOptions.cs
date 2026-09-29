namespace MafRagApi.Models;

/// <summary>Reranker 配置，绑定 appsettings.json 的 "Reranker" 节。</summary>
public sealed class RerankerOptions
{
    public const string SectionName = "Reranker";

    /// <summary>Reranker 端点（不带路径）。Aspire 注入的 Reranker__Endpoint 覆盖此值。</summary>
    public string Endpoint { get; set; } = "http://localhost:5919";

    /// <summary>单次请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>单次请求允许的最大文档数。</summary>
    public int MaxDocuments { get; set; } = 100;

    /// <summary>不传 topK 时的默认值。</summary>
    public int DefaultTopK { get; set; } = 5;
}