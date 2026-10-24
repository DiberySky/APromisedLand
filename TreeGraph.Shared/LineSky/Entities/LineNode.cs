using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Shared.LineSky.Entities;

/// <summary>
/// 线路节点 —— 独立的一等公民实体（R3 起不再实现 Inode，身份由 nodes 注册表承载）。
///
/// 用于承载线路上的点，与四棵宿主树节点无关。
/// 典型场景：输电线路杆塔、物流站点、工序工位、交叉跨越点等。
/// </summary>
public class LineNode
{
    /// <summary>节点 ID（GUID 字符串），EAV 内作为 inode_id 使用。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>共享主键导航：宿主 Id 即 nodes.id，名称唯一真源在 Node.Name。</summary>
    [JsonIgnore]
    public Node? Node { get; set; }

    private string? _inputName;

    /// <summary>名称（非映射列）：运行时读穿 Node.Name；入参绑定时通过 setter 暂存（Create 时写入 nodes）。</summary>
    [NotMapped]
    public string? Name
    {
        get => Node?.Name ?? _inputName;
        set => _inputName = value;
    }

    /// <summary>所属线路 ID（Line.Id）。线路节点隶属于某条线路，随线路一起维护。</summary>
    public string LineId { get; set; } = string.Empty;

    /// <summary>节点描述。</summary>
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
