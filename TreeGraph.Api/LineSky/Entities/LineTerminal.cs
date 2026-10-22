using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Api.LineSky.Entities;

/// <summary>
/// 线路外部连接端子：线路结构中的某个 LineNode 与外部 iNode 的馈入/馈出/联络关系。
///
/// external_node_id 可空（空 = 只在本结构内声明角色，不指向外部节点）；
/// 非空时不建外键，存在性由 InodeOwnerGuardRegistry 在服务层校验。
/// </summary>
public class LineTerminal
{
    public string LineId { get; set; } = string.Empty;

    /// <summary>结构内的线路节点 ID（line_nodes.id）。</summary>
    public string NodeId { get; set; } = string.Empty;

    public TerminalRole Role { get; set; } = TerminalRole.FeedIn;

    /// <summary>外部连接节点 ID（任意 iNode，可空）。</summary>
    public string? ExternalNodeId { get; set; }

    /// <summary>运行状态（牵引供电多电源「同一时刻只投一路」）。默认 Maintenance（检修），存量端子进入此态。</summary>
    public TerminalOperatingState OperatingState { get; set; } = TerminalOperatingState.Maintenance;
}
