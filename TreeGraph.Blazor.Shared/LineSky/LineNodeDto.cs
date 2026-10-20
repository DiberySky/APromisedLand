namespace TreeGraph.Blazor.Shared.LineSky;

/// <summary>
/// 线路节点（LineNode）DTO，对齐后端 LineNode 实体（line_nodes 表）。
/// </summary>
public class LineNodeDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>所属线路 ID（Line.Id）。</summary>
    public string LineId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
