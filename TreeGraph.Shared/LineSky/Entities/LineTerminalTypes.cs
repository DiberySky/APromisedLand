namespace TreeGraph.Shared.LineSky.Entities;

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
