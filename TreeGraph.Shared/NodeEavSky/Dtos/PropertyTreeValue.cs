using System.Text.Json.Serialization;

namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 属性树（property_tree）运行时值：挂载的专属 eav_trees 根节点。
///
/// - <see cref="RootId"/>：根节点 Id（= eav_tree_roots.id / nodes.id）；创建新树时为 null。
/// - <see cref="Name"/>：根节点名称，唯一真源在 nodes.name；创建时必填（前后端均守卫），
///   已挂载时随对象提交即行内改名。
/// </summary>
public sealed record PropertyTreeValue(
    [property: JsonPropertyName("rootId")] string? RootId,
    [property: JsonPropertyName("name")] string? Name);
