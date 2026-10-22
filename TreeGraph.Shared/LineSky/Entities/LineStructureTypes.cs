namespace TreeGraph.Shared.LineSky.Entities;

/// <summary>
/// 线路结构拓扑模式。
///
/// Linear  —— 严格线性（一条链，每点度 ≤ 2，恰两个端点，不允许成环）；
/// Radial  —— 辐射式（无环树状，边数 = 节点数 - 1，且恰有 1 个 FeedIn 端子）；
/// Network —— 网络式（允许环与多电源，连通即可，不要求端子）。
/// 默认 Network：本项目主用于铁路牵引供电，上下行并联、分区所联络与多电源接入
/// 会自然形成环与分叉，只有 Network 不会误拒合法拓扑；单条供电臂可显式选 Linear。
///
/// 共同约束：至少一段、无自环、段端点须属本线路、结构必须连通、
/// 端子节点必须落在结构内。<br/>
/// 结构成员口径：只取段端点 —— 线路下未纳入任何段、也未设置端子的节点不参与校验，
/// 视为尚未纳入结构。
/// </summary>
public enum TopologyMode
{
    Linear = 0,
    Radial = 1,
    Network = 2
}

/// <summary>
/// 外部连接端子角色。
///
/// FeedIn  —— 馈入点（外部电源接入本结构）；
/// FeedOut —— 馈出点（本结构向外供电）；
/// Tie     —— 联络点（与外部结构互联/备用）。
/// </summary>
public enum TerminalRole
{
    FeedIn = 0,
    FeedOut = 1,
    Tie = 2
}

/// <summary>
/// 端子运行状态（铁路牵引供电用：多电源同一时刻只投一路）。
///
/// Energized    —— 投运（带电运行中）；
/// HotStandby   —— 热备用（带电但未投运，可立即切换）；
/// ColdStandby  —— 冷备用（断开，需要合闸才能带电）；
/// Maintenance  —— 检修（接地/停电，不可合闸）。
/// </summary>
public enum TerminalOperatingState
{
    Energized = 0,
    HotStandby = 1,
    ColdStandby = 2,
    Maintenance = 3
}
