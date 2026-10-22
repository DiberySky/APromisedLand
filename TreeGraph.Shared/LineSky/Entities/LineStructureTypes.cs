namespace TreeGraph.Shared.LineSky.Entities;

/// <summary>
/// 线路结构拓扑模式。
///
/// Linear  —— 严格线性（一条链，每点度 ≤ 2，恰两个端点）；
/// Radial  —— 辐射式（无环树状，单电源多馈出，边数 = 节点数 - 1）；
/// Network —— 网络式（允许环与多电源，连通即可）。
/// 默认 Radial。
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
