namespace TreeGraph.Blazor.Shared.LineSky;

/// <summary>
/// 线路节点（LineNode）DTO，对齐后端 Facade（R7：nodes 表 host_type='LineNode' 注册行）。
/// </summary>
public class LineNodeDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>所属线路 ID（nodes.line_id → Line）。</summary>
    public string LineId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>前驱节点 Id（写入：创建时指定、更新时 null = 不改连接；查询：返回当前前驱）。</summary>
    public string? PredecessorId { get; set; }

    /// <summary>前驱节点名称（只读，随查询返回；写入时忽略）。</summary>
    public string? PredecessorName { get; set; }

    /// <summary>创建时间（只读，随查询返回；写入时忽略）。</summary>
    public DateTimeOffset? CreatedAt { get; set; }
}
