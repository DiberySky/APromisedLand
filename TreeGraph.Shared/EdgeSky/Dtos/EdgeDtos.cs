using System.Text.Json;

namespace TreeGraph.Shared.EdgeSky.Dtos;

// ============ Edge Instance（关系实例） ============

public class CreateEdgeRequest
{
    /// <summary>关系类型名（PascalCase 约定）；同时作为 EAV entity_type 与图边类型名。</summary>
    public string TypeName { get; set; } = "";

    /// <summary>起点节点 ID（nodes.id）。</summary>
    public string FromNodeId { get; set; } = "";

    /// <summary>终点节点 ID（nodes.id）。</summary>
    public string ToNodeId { get; set; } = "";

    /// <summary>true = 有向（from → to）；false = 无向（反向等同），默认有向。</summary>
    public bool IsDirected { get; set; } = true;

    /// <summary>EAV 属性值（与 PUT /api/eav/{entityType}/entities/{id} 格式一致）。</summary>
    public Dictionary<string, JsonElement>? Properties { get; set; }
}

public class UpdateEdgeRequest
{
    /// <summary>有向/无向。其余业务属性走 EAV。</summary>
    public bool? IsDirected { get; set; }
}

public record EdgeInstanceDto(
    string EdgeId,
    string TypeName,
    string FromNodeId,
    string FromNodeText,
    string ToNodeId,
    string ToNodeText,
    bool IsDirected,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Dictionary<string, JsonElement>? Properties);

// ============ 图查询 ============

public record EdgeTraverseRequest(
    string TypeName,
    string StartNodeId,
    string Direction = "out",   // out / in / both
    int MaxDepth = 3);

public record EdgeTraverseResult(
    IReadOnlyList<EdgeInstanceDto> Edges,
    IReadOnlyList<string> VisitedNodeIds);
