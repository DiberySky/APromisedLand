using TreeGraph.Shared.LineSky.Entities;

namespace TreeGraph.Shared.LineSky.Dtos;

// ============ 线路结构（LineStructure）============

/// <summary>结构中的节点（含名称与端子角色）。</summary>
public class LineStructureNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>端子角色；非端子节点为 null。</summary>
    public TerminalRole? TerminalRole { get; set; }
    /// <summary>外部连接节点 ID（仅端子可能非空）。</summary>
    public string? ExternalNodeId { get; set; }
    /// <summary>外部连接节点显示名（能解析到时填充）。</summary>
    public string? ExternalNodeName { get; set; }
}

/// <summary>结构中的段（一条 EdgeInstance）。</summary>
public class LineStructureSegmentDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNodeId { get; set; } = string.Empty;
    public string FromNodeName { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public string ToNodeName { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public string? DisplayName { get; set; }
}

/// <summary>结构诊断：拓扑校验结果（不阻断 GET，仅展示）。</summary>
public class LineStructureDiagnosticsDto
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = new();
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int NodeDegreeMax { get; set; }
    public int NodeDegreeOneCount { get; set; }
    public bool HasCycle { get; set; }
    public int FeedInCount { get; set; }
}

/// <summary>线路结构完整视图。</summary>
public class LineStructureDto
{
    /// <summary>结构是否存在（false = 该线路尚无结构，Segments 为空）。</summary>
    public bool Exists { get; set; }
    public string LineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public string? EdgeTypeId { get; set; }
    public string? EdgeTypeName { get; set; }
    public string? EdgeTypeDisplayName { get; set; }
    public TopologyMode TopologyMode { get; set; } = TopologyMode.Linear;
    public DateTimeOffset? UpdatedAt { get; set; }
    /// <summary>拓扑推导出的节点顺序（Linear/Radial 为链/树遍历顺序；Network 为发现顺序）。</summary>
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
    public decimal? Weight { get; set; }
}

/// <summary>建/替换结构时的端子输入。</summary>
public class LineStructureTerminalInput
{
    public string NodeId { get; set; } = string.Empty;
    public TerminalRole Role { get; set; } = TerminalRole.FeedIn;
    public string? ExternalNodeId { get; set; }
}

/// <summary>
/// 建/替换线路结构请求。
///
/// Linear 模式可只传 OrderedNodeIds（相邻两点自动成段）；
/// 其余情况传 Segments（可同时覆盖 Linear/Radial/Network）。
/// </summary>
public class LineStructureRequest
{
    public string EdgeTypeId { get; set; } = string.Empty;
    public TopologyMode TopologyMode { get; set; } = TopologyMode.Linear;
    public List<string>? OrderedNodeIds { get; set; }
    public List<LineStructureSegmentInput>? Segments { get; set; }
    public List<LineStructureTerminalInput>? Terminals { get; set; }
}

/// <summary>新增单段请求（节点须同属该 Line）。</summary>
public class AddLineSegmentRequest
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public string? DisplayName { get; set; }
}

/// <summary>新增/更新端子请求。</summary>
public class AddLineTerminalRequest
{
    public string NodeId { get; set; } = string.Empty;
    public TerminalRole Role { get; set; } = TerminalRole.FeedIn;
    public string? ExternalNodeId { get; set; }
}
