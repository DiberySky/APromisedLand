namespace TreeGraph.Shared.NodeEavSky.Abstractions;

/// <summary>
/// EAV 宿主契约。
///
/// 外部实体（树节点、边实例等）实现此接口即声明"我可作为 EAV 宿主"。
/// EAV 内部通过 <see cref="Id"/>（作为 inode_id）关联宿主实例与 EAV 实体，
/// 实体类型由用户在 EAV 内部自定义，与宿主具体类型无关。
/// </summary>
public interface Inode
{
    /// <summary>宿主实例主键（GUID 字符串），EAV 内作为 inode_id 使用。</summary>
    string Id { get; }

    /// <summary>宿主显示名。可空：名字为派生物的宿主（如注册进 nodes 的边）允许为 null。</summary>
    string? Name { get; }
}
