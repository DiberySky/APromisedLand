namespace TreeGraph.Api.LineSky.Entities;

/// <summary>
/// 线路结构段：线路内部的一段拓扑，自存两端与线路归属，不再引用 edge_instances。
///
/// 顺序不落库（无 seq），由拓扑推导；from/to 显式存库以承载线路方向。
/// 复合外键 (line_id, from_node_id) / (line_id, to_node_id) → line_nodes(line_id, id)，
/// 把「同线归属」下推到数据库；UNIQUE(edge_id) 保证段标识全局唯一。
/// </summary>
public class LineStructureSegment
{
    public string LineId { get; set; } = string.Empty;

    /// <summary>段标识（不再对应 edge_instances.edge_id）。</summary>
    public string EdgeId { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    /// <summary>可选权重（线路自身属性，与通用边无关）。</summary>
    public decimal? Weight { get; set; }

    /// <summary>可选段显示名。</summary>
    public string? DisplayName { get; set; }
}
