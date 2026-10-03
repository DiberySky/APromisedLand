using System.Text.Json;

namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>
/// iNode 下的类型卡片（UI 展示用）。
/// </summary>
public record InodeTypeCardDto(
    string EntityType,                    // 类型名，如 "Product"
    string DisplayName,                   // 中文显示名
    string? EntityTypeId,                 // 对应 entity_type_catalog 的 ID（可空）
    string? Description,
    bool Declared,                        // 是否已声明
    bool HasEntity,                       // 该类型下是否已创建实体
    DateTimeOffset? EntityUpdatedAt);

/// <summary>
/// iNode 下的实体概要（列表项）。
/// </summary>
public record InodeEntitySummaryDto(
    string InodeId,
    string EntityId,
    string EntityType,
    string? DisplayName,
    DateTimeOffset? UpdatedAt,
    int PropertyCount);

/// <summary>
/// 跨 iNode 查询请求。
/// </summary>
public class InodeQueryRequest
{
    /// <summary>可选：限定 iNode。null = 跨所有 iNode 查询。</summary>
    public string? InodeId { get; set; }

    /// <summary>必填：实体类型名（如 "Product"）。</summary>
    public string EntityType { get; set; } = "";

    public List<AttributeFilter> Filters { get; set; } = new();

    public string? OrderByAttribute { get; set; }
    public bool OrderDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// 跨 iNode 查询结果项。
/// </summary>
public record InodeEntityDto(
    string InodeId,
    string EntityId,
    string EntityType,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt);
