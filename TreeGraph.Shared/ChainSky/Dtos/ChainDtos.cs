using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Shared.ChainSky.Dtos;

// ============ 链式结构（Chain）专用 ============

/// <summary>批量创建链：按 orderedNodeIds 顺序创建 from→to 边。</summary>
public class CreateChainRequest
{
    /// <summary>有序节点列表，如 ["A", "B", "C", "D"] → 创建 A→B, B→C, C→D。</summary>
    public List<string> OrderedNodeIds { get; set; } = new();
    /// <summary>可选：给每段边分配权重。按顺序与节点对一一对应，长度应 = OrderedNodeIds.Count - 1。</summary>
    public List<decimal?>? Weights { get; set; }
}

/// <summary>链式完整路径查询结果（WITH RECURSIVE CTE 返回）。</summary>
public record ChainPathResult(
    /// <summary>路径上的节点 ID 列表（按方向排列，起点在索引 0）。</summary>
    IReadOnlyList<string> NodeIds,
    /// <summary>路径上的边（段）列表，与 NodeIds 对应。</summary>
    IReadOnlyList<EdgeInstanceDto> Edges,
    /// <summary>路径长度（边数）。</summary>
    int Length,
    /// <summary>是否因达到递归深度上限而截断（true 表示结果可能不完整）。</summary>
    bool Truncated = false);

/// <summary>链式拓扑校验结果（入度/成环检测）。</summary>
public record ChainValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors,
    /// <summary>入度超标的节点（入度值）。</summary>
    IReadOnlyDictionary<string, int> InDegreeMap,
    /// <summary>出度超标的节点（仅 IsLinear=false 时收集）。</summary>
    IReadOnlyDictionary<string, int> OutDegreeMap,
    /// <summary>成环节点（Kahn 拓扑排序未能消解的剩余节点）。</summary>
    IReadOnlyList<string> CyclicNodeIds,
    int EdgeCount,
    int NodeCount,
    /// <summary>
    /// 非致命告警：非线性链（IsChain &amp;&amp; !IsLinear）下的入度 &gt; 1 与成环仅作提示，
    /// 不计入 Errors（不影响 Valid）。严格线性链下这些问题会进入 Errors。
    /// </summary>
    IReadOnlyList<string> Warnings);

/// <summary>连通分量：互不相连的节点组。</summary>
public record ChainConnectedComponent(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<EdgeInstanceDto> Edges,
    int Size);
