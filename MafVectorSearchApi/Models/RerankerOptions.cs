namespace MafVectorSearchApi.Models;

/// <summary>Reranker 服务配置。</summary>
public sealed class RerankerOptions
{
    public const string SectionName = "Reranker";

    public string Endpoint       { get; set; } = "http://localhost:5919";
    public int    TimeoutSeconds { get; set; } = 30;
    public int    MaxDocuments   { get; set; } = 100;
    public int    DefaultTopK    { get; set; } = 5;
}
