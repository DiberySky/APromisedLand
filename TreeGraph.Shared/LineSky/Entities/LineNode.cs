using TreeGraph.Shared.NodeEavSky.Abstractions;

namespace TreeGraph.Shared.LineSky.Entities;

/// <summary>
/// 线路节点 —— 独立的一等公民实体，实现 <see cref="Inode"/>。
///
/// 用于承载线路上的点，与四棵宿主树节点无关。
/// 典型场景：输电线路杆塔、物流站点、工序工位、交叉跨越点等。
/// </summary>
public class LineNode : Inode
{
    /// <summary>节点 ID（GUID 字符串），EAV 内作为 inode_id 使用。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>所属线路 ID（Line.Id）。线路节点隶属于某条线路，随线路一起维护。</summary>
    public string LineId { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点描述。</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
