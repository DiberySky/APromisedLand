namespace TreeGraph.Blazor.Shared.LineSky;

/// <summary>
/// 线路（Line）DTO，对齐后端 Line 实体（lines 表）。
/// </summary>
public class LineDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>所属图空间 Id（lines.space_id；创建后不可变更）。</summary>
    public string SpaceId { get; set; } = string.Empty;
}
