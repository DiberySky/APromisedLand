using System.Text.Json;

namespace TreeGraph.Shared.EdgeSky.Dtos;

// ============ Edge Definition（关系类型元数据） ============

public class CreateEdgeTypeRequest
{
    /// <summary>PascalCase，如 Collaboration。作为 EAV 属性的 entityType。</summary>
    public string TypeName { get; set; } = "";
    /// <summary>中文显示名，如 协作关系。</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>可选：起点节点类型过滤（NodeType）。</summary>
    public string? FromNodeType { get; set; }
    /// <summary>可选：终点节点类型过滤（NodeType）。</summary>
    public string? ToNodeType { get; set; }
    /// <summary>全局共享（所有节点类型可用），默认 true。</summary>
    public bool IsGlobal { get; set; } = true;
    /// <summary>有向（true：from→to 有方向）/无向（false：边等同双向），默认 true。</summary>
    public bool IsDirected { get; set; } = true;
    public string? Description { get; set; }
    /// <summary>链式结构类型（如输电线路），默认 false。</summary>
    public bool IsChain { get; set; } = false;
    /// <summary>严格线性（入度≤1，创建边时自动校验），仅 IsChain=true 时生效。</summary>
    public bool IsLinear { get; set; } = true;
}

public class UpdateEdgeTypeRequest
{
    public string? DisplayName { get; set; }
    public string? FromNodeType { get; set; }
    public string? ToNodeType { get; set; }
    public bool? IsGlobal { get; set; }
    public bool? IsDirected { get; set; }
    public string? Description { get; set; }
    public bool? IsChain { get; set; }
    public bool? IsLinear { get; set; }
}

public record EdgeTypeDto(
    string EdgeTypeId,
    string TypeName,
    string DisplayName,
    string? FromNodeType,
    string? ToNodeType,
    bool IsGlobal,
    bool IsDirected,
    string? Description,
    bool IsActive,
    bool IsChain,
    bool IsLinear);

// ============ Edge Instance（关系实例） ============

public class CreateEdgeRequest
{
    public string FromNodeId { get; set; } = "";
    public string ToNodeId { get; set; } = "";
    /// <summary>可选权重。</summary>
    public decimal? Weight { get; set; }
    /// <summary>EAV 属性值（与 PUT entities/{id} 格式一致）。</summary>
    public Dictionary<string, JsonElement>? Properties { get; set; }
}

public class UpdateEdgeRequest
{
    public decimal? Weight { get; set; }
    /// <summary>如需更新属性，走 EAV PUT /api/eav/{edgeTypeId}/entities/{edgeId}。</summary>
}

public record EdgeInstanceDto(
    string EdgeId,
    string EdgeTypeId,
    string EdgeTypeName,
    string EdgeTypeDisplayName,
    string FromNodeId,
    string FromNodeText,
    string ToNodeId,
    string ToNodeText,
    decimal? Weight,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Dictionary<string, JsonElement>? Properties);

// ============ 图查询 ============

public record EdgeTraverseRequest(
    string StartNodeId,
    string Direction = "out",   // out / in / both
    int MaxDepth = 3);

public record EdgeTraverseResult(
    IReadOnlyList<EdgeInstanceDto> Edges,
    IReadOnlyList<string> VisitedNodeIds);

/// <summary>EdgeDefinition.Delete 返回的级联清理统计。</summary>
public record EdgeDeleteResult(
    bool Deleted,
    int InstancesDeleted,
    int AttributeValuesDeleted,
    int AttributeDefinitionsDeleted);
