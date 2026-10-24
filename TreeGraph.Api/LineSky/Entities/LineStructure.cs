using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Entities;

/// <summary>
/// 线路结构头表：一条 Line 至多一个结构（line_id 即主键）。
///
/// 存在即代表「该线路有结构」；segments = 0 时头表必须不存在
/// （删除最后一段时同事务删除头表）。拓扑模式默认 Network。
/// </summary>
public class LineStructure
{
    /// <summary>线路 ID（lines.id），同时是主键。</summary>
    public string LineId { get; set; } = string.Empty;

    /// <summary>拓扑模式（Linear / Radial / Network）。</summary>
    public TopologyMode TopologyMode { get; set; } = TopologyMode.Network;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
