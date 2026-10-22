namespace TreeGraph.Api.LineSky.Entities;

/// <summary>
/// 线路结构段：一条已存在的 EdgeInstance 归属到某条 Line 的结构中。
///
/// 顺序不落库（无 seq），由拓扑推导；from/to 显式存库以承载线路方向。
/// 复合外键 (line_id, from_node_id) / (line_id, to_node_id) → line_nodes(line_id, id)，
/// 把「同线归属」下推到数据库；UNIQUE(edge_id) 保证一条边只属于一个结构。
/// </summary>
public class LineStructureSegment
{
    public string LineId { get; set; } = string.Empty;

    /// <summary>边实例 ID（edge_instances.edge_id），全局唯一归属。</summary>
    public string EdgeId { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;
}
