using TreeGraph.Shared.NodeEavSky.Abstractions;

namespace TreeGraph.Shared.NodeSky.Entities;

/// <summary>
/// 通用节点 —— 无归属的一等公民实体，实现 <see cref="Inode"/>。
///
/// 与四棵宿主树节点的差异：没有树归属外键（树的 ParentId 在各自宿主表）。
/// 只承载"确实无归属"的点（如独立设备、临时设施）；能进树进树、能上线上线。
///
/// 双角色（specs/node-sky §8.2）：
///   一期：独立 Node 的宿主主表（host_type 恒为 "Node"）；
///   重构期：全系统节点身份注册表（Line / LineNode / TreeNode 等注册于此）。
///
/// 通用性约束：不设 Kind/NodeType 业务分类列——"它是什么"完全交给 EAV 实体类型声明表达。
/// </summary>
public class Node : Inode
{
    /// <summary>节点 ID（GUID 字符串），EAV 内作为 inode_id 使用；重构期即全局节点身份。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 宿主类型名（与 InodeOwnerGuard.NodeType / EAV entity_type 一致）。
    /// 一期恒为 "Node"；重构期各宿主注册自己的类型名（"Line"/"LineNode"/"SpaceTree"…）。
    /// </summary>
    public string HostType { get; set; } = "Node";

    /// <summary>
    /// 节点名称。可空：重构期边注册进 nodes 时名字为派生物（from名 → to名），存 null（spec §8.6 R3）。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>节点描述。</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 身份锚点（R7）：仅 host_type='LineNode' 时必填，恒 = 所属 Line 的 nodes.id；
    /// 目标行必须 host_type='Line'（服务层校验）。通用 Node / 树节点恒 null。
    /// </summary>
    public string? LineId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
