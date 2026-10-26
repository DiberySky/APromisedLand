using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Shared.LineSky.Dtos;

// ============ 线路结构（边集薄层，结构由 edges 表 line_segment 边定义） ============

/// <summary>结构中的节点（含名称与端子角色）。</summary>
public class LineStructureNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>端子角色；非端子节点为 null。</summary>
    public TerminalRole? TerminalRole { get; set; }
    /// <summary>端子运行状态；非端子节点或未设置状态为 null。</summary>
    public TerminalOperatingState? OperatingState { get; set; }
    /// <summary>外部连接节点 ID（仅端子可能非空）。</summary>
    public string? ExternalNodeId { get; set; }
    /// <summary>外部连接节点显示名（能解析到时填充）。</summary>
    public string? ExternalNodeName { get; set; }
}

/// <summary>结构中的段边（edges 表记录；DisplayName = props.name）。</summary>
public class LineStructureSegmentDto
{
    /// <summary>边实例标识：四元组编码 "{edgeType}:{srcVid}:{rank}:{dstVid}"（段边 rank 恒为 0）。</summary>
    public string EdgeId { get; set; } = string.Empty;
    public string FromNodeId { get; set; } = string.Empty;
    public string FromNodeName { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string ToNodeName { get; set; } = string.Empty;
    /// <summary>边类型名（edges.edge_type；内置段边为 line_segment）。</summary>
    public string EdgeType { get; set; } = string.Empty;
    /// <summary>边实例展示名（props.name）。</summary>
    public string? DisplayName { get; set; }
}

/// <summary>结构诊断：连通/成环/度数/端子统计（只报告不阻断）。</summary>
public class LineStructureDiagnosticsDto
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
    /// <summary>非阻断提示（只提示不阻断创建/替换）。</summary>
    public List<string> Warnings { get; set; } = new();
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int NodeDegreeMax { get; set; }
    public int NodeDegreeOneCount { get; set; }
    public bool HasCycle { get; set; }
    public int FeedInCount { get; set; }
    public int FeedInEnergizedCount { get; set; }
    public int TieCount { get; set; }
}

/// <summary>线路结构完整视图（节点集 = line_id 挂靠；边集 = 两端同线的 edges 记录）。</summary>
public class LineStructureDto
{
    /// <summary>结构是否存在（false = 该线路尚无线路节点，Segments 为空）。</summary>
    public bool Exists { get; set; }
    public string LineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>沿 line_segment 链推导的节点顺序（无入边者为起点；分叉/环按发现序）。</summary>
    public List<string> OrderedNodeIds { get; set; } = new();
    public List<LineStructureNodeDto> Nodes { get; set; } = new();
    public List<LineStructureSegmentDto> Segments { get; set; } = new();
    public List<LineStructureNodeDto> Terminals { get; set; } = new();
    /// <summary>无结构时返回该线路全部候选 LineNode（供前端选择建结构节点）。</summary>
    public List<LineStructureNodeDto> CandidateNodes { get; set; } = new();
    public LineStructureDiagnosticsDto Diagnostics { get; set; } = new();
}

/// <summary>建/替换结构时的段输入。</summary>
public class LineStructureSegmentInput
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    /// <summary>边实例展示名（可选，落 props.name）。</summary>
    public string? DisplayName { get; set; }
}

/// <summary>线路模板 DTO：UI 层下拉预填用。字段用 string（枚举名），避免 MudBlazor source generator 对枚举类型的 bug。</summary>
public record LineTemplateDto(
    string Key,
    string DisplayName,
    string Description,
    string DefaultTerminalRole,        // TerminalRole 枚举名，例 "FeedIn"
    string DefaultOperatingState);     // TerminalOperatingState 枚举名，例 "Maintenance"

public class LineStructureTerminalInput
{
    public string NodeId { get; set; } = string.Empty;
    public TerminalRole Role { get; set; } = TerminalRole.FeedIn;
    public TerminalOperatingState OperatingState { get; set; } = TerminalOperatingState.Maintenance;
    public string? ExternalNodeId { get; set; }
}

/// <summary>
/// 建/替换线路结构请求（拓扑模式已下线，结构由边连接定义）。
/// Segments = 全量替换本线的 line_segment 边集；Terminals = 全量替换端子集。
/// </summary>
public class LineStructureRequest
{
    public List<LineStructureSegmentInput>? Segments { get; set; }
    public List<LineStructureTerminalInput>? Terminals { get; set; }
}

/// <summary>新增单段请求（节点须同属该 Line；写 edges 表 line_segment 边，rank=0）。</summary>
public class AddLineSegmentRequest
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

/// <summary>新增/更新端子请求。</summary>
public class AddLineTerminalRequest
{
    public string NodeId { get; set; } = string.Empty;
    public TerminalRole Role { get; set; } = TerminalRole.FeedIn;
    public TerminalOperatingState OperatingState { get; set; } = TerminalOperatingState.Maintenance;
    public string? ExternalNodeId { get; set; }
}
