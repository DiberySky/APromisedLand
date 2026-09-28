namespace MafSampleApi.Models;

/// <summary>POST /api/rerank 的请求体。</summary>
public sealed record RerankRequestDto
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<string> Documents { get; init; } = Array.Empty<string>();
    public int? TopK { get; init; }
}

/// <summary>单个重排结果。</summary>
public sealed record RerankResultDto
{
    public int Index { get; init; }
    public double Score { get; init; }
    public string? Document { get; init; }
}

/// <summary>POST /api/rerank 的响应体。</summary>
public sealed record RerankResponseDto
{
    public string Query { get; init; } = string.Empty;
    public int TotalDocuments { get; init; }
    public IReadOnlyList<RerankResultDto> Results { get; init; } = Array.Empty<RerankResultDto>();
}