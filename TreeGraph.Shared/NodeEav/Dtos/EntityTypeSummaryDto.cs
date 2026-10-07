namespace TreeGraph.Shared.NodeEav.Dtos;

/// <summary>
/// 实体类型摘要（GET api/eav/entity-types）。
/// 用于侧边栏/首页列出所有已定义的实体类型。
/// </summary>
public record EntityTypeSummaryDto(
    string EntityType,
    int AttributeCount,
    int SearchableAttributeCount,
    string? FirstDisplayName,
    string? EntityTypeId = null,     // 新增
    string? DisplayName = null);     // 新增
