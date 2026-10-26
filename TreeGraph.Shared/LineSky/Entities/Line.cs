using TreeGraph.Shared.NodeEavSky.Abstractions;

namespace TreeGraph.Shared.LineSky.Entities;

/// <summary>
/// 线路 —— 独立的一等公民实体，实现 <see cref="Inode"/>。
///
/// 表达一条完整线路（如输电线路、物流线路），与承载线路点的 LineNode 平级。
/// </summary>
public class Line : Inode
{
    /// <summary>线路 ID（GUID 字符串），EAV 内作为 inode_id 使用。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>线路名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>线路描述。</summary>
    public string? Description { get; set; }

    /// <summary>所属图空间 Id（FK → spaces；线路的结构段边 line_segment 写入该空间）。</summary>
    public string SpaceId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
