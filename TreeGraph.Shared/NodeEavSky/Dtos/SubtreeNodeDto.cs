namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 子树节点 DTO。EAV 数据以预渲染文本形式承载（异构数据无法用字典表达）。
/// </summary>
public sealed record SubtreeNodeDto(
    string Id,
    string Name,
    string? Path,
    /// <summary>
    /// 预渲染的 EAV 文本（多行，含 entityType 分组、单位、子表展开）。
    /// 例：
    ///   【设备信息】
    ///     生产厂家：海天
    ///     额定功率：18.5 kW
    ///   【维保记录】
    ///     - 日期：2024-03-12 | 内容：更换液压油
    /// </summary>
    string? EavRendered,
    List<SubtreeNodeDto> Children,
    /// <summary>
    /// 预渲染的关系文本（子树内每个节点均可有值：根节点最多 50 条、子节点最多 10 条）。
    /// 例：
    ///   关系：
    ///     - 出：属于 → 武汉供电段（生效日期=2024-01-01）
    ///     - 入：3号设备 -[巡检记录]-> 本节点
    /// </summary>
    string? RelationsRendered = null);

/// <summary>
/// 子树加载结果。
/// </summary>
public sealed record SubtreeLoadResult(
    SubtreeNodeDto? Root,
    int TotalNodes,
    bool Truncated);
