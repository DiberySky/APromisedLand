namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>LiteGraph 列表端点的分页包装。</summary>
public record LiteGraphListResponse<T>
{
    public List<T> Objects { get; init; } = [];
    public int TotalRecords { get; init; }
    public bool EndOfResults { get; init; }
}