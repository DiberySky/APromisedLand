namespace MafVectorSearchApi.Models;

/// <summary>
/// vLLM Embedding 服务配置（从原 AgentOptions 拆出，向量搜索服务专用）。
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string Endpoint { get; set; } = "http://localhost:5719";
    public string ApiKey   { get; set; } = "EMPTY";
    public string Model    { get; set; } = "bge-m3";
    public int    Dimension { get; set; } = 1024;
}
