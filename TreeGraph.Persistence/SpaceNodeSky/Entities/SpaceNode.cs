using System.Text.Json.Serialization;
using TreeGraph.Shared.NodeSky.Entities;

namespace TreeGraph.Persistence.SpaceNodeSky.Entities;

/// <summary>
/// 空间节点（SpaceNode）——归属某个图空间的通用节点。
///
/// 与自由 Node 的差异：本表持有空间归属（space_id），图同步只进入该空间；
/// 身份仍以 nodes.id 为唯一真源，本表 Id 与 nodes.id 共享主键。
/// 名称/描述不回写本表，读取时经 <see cref="Node"/> 读穿 nodes。
/// </summary>
public class SpaceNode
{
    /// <summary>nodes.host_type 取值。</summary>
    public const string HostTypeName = "SpaceNode";

    /// <summary>节点 ID（GUID 字符串），与 nodes.id 同值。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>归属图空间 ID（创建后不可改）。</summary>
    public string SpaceId { get; set; } = "";

    /// <summary>共享主键导航：名称/描述唯一真源在 nodes。</summary>
    [JsonIgnore]
    public Node? Node { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
