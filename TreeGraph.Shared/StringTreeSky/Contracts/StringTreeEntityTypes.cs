namespace TreeGraph.Shared.StringTreeSky.Contracts;

/// <summary>
/// StringTree 节点在 EAV 体系中使用的实体类型名。
/// 节点 Id（int）的字符串形式即 EAV EntityId；节点结构字段不进 EAV。
/// </summary>
public static class StringTreeEntityTypes
{
    /// <summary>默认树类型；多棵树时可用 "StringTreeNode:{treeKey}"。</summary>
    public const string Node = "StringTreeNode";
}
