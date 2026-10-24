namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 关系属性（relation）写入值：一条记录 = 一条有向边（宿主实体 → toNodeId）。
/// 请求体形态 { "toNodeId": "…", "name": "…" }。
/// </summary>
public sealed class EavRelationValue
{
    /// <summary>目标节点 Id（必填，必须存在于 nodes）。</summary>
    public string ToNodeId { get; set; } = "";

    /// <summary>边实例展示名（可选，如「主供」「备用」）。</summary>
    public string? Name { get; set; }
}

/// <summary>
/// 关系属性读取值：{ valueId, toNodeId, name }。
/// valueId 由服务端生成，也是图边实例 id。
/// </summary>
public sealed record EavRelationReadValue(string ValueId, string ToNodeId, string? Name);

/// <summary>节点选择项（关系字段搜索目标节点用）。</summary>
public sealed record NodeOptionDto(string Id, string? Name, string? HostType);
