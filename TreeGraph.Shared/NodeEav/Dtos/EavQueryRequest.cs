namespace TreeGraph.Shared.NodeEav.Dtos;

/// <summary>EAV 动态查询请求</summary>
public class EavQueryRequest
{
    public string EntityType { get; set; } = "";

    public List<AttributeFilter> Filters { get; set; } = new();

    /// <summary>预留：按动态属性排序（当前按 EntityId 排序）</summary>
    public string? OrderByAttribute { get; set; }

    public bool OrderDescending { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
