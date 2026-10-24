using System.Text.Json.Serialization;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Api.EdgeSky.Entities;

/// <summary>
/// 边实例：两个 Node 之间的自由语义关系。
///
/// 终态（specs/node-sky §8.6）：
///   - 边本身注册进 nodes（host_type = "EdgeInstance"，name = null），EdgeId 即 nodes.id；
///   - 两端 from/to 是指向 nodes(id) 的真外键（Restrict），存在性由数据库兜底；
///   - type_name 为裸列：兼 EAV entity_type 键、唯一约束判别列、NebulaGraph 边类型名；
///   - is_directed 是边内核语义（有向/无向），其余业务属性一律走 EAV。
/// </summary>
public class EdgeInstance
{
    public string EdgeId { get; set; } = "";

    /// <summary>共享主键导航：边 Id 即 nodes.id；边的显示名为派生物，nodes.name 存 null。</summary>
    [JsonIgnore]
    public Node? Node { get; set; }

    /// <summary>关系类型名（PascalCase 约定），同时作为 EAV entity_type 与图边类型名。</summary>
    public string TypeName { get; set; } = "";

    /// <summary>起点节点 ID（nodes.id）。</summary>
    public string FromNodeId { get; set; } = "";

    /// <summary>终点节点 ID（nodes.id）。</summary>
    public string ToNodeId { get; set; } = "";

    /// <summary>true = 有向（from → to）；false = 无向（反向等同）。</summary>
    public bool IsDirected { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
