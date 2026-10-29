using System.Text.Json;

namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// iNode 下的类型卡片（UI 展示用）。
/// </summary>
public record InodeTypeCardDto(
    string EntityType,                    // 类型名，如 "UnitTree"
    string DisplayName,                   // 中文显示名
    string? EntityTypeId,                 // 对应 entity_type_catalog 的 ID（可空）
    string? Description,
    bool Declared,                        // 是否已声明
    bool HasEntity,                       // 该类型下是否已存在命名绑定
    DateTimeOffset? EntityUpdatedAt,
    int BindingCount = 0);                // 该类型下的命名绑定数量（可 >1）

/// <summary>
/// iNode 下的命名绑定概要（列表项 / 下拉切换用）。
/// </summary>
public record InodeBindingDto(
    string BindingId,
    string Name,
    string EntityType,
    string EntityId,
    int DisplayOrder,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// 命名绑定详情（含属性值）。
/// </summary>
public record InodeBindingDetailDto(
    string BindingId,
    string Name,
    string EntityType,
    string EntityId,
    Dictionary<string, JsonElement> Properties,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// 新建命名绑定请求。
/// </summary>
public class CreateBindingRequest
{
    /// <summary>必填：实体类型名（如 "UnitTree"）。</summary>
    public string EntityType { get; set; } = "";

    /// <summary>必填：绑定名（同一 iNode 同一类型下唯一）。</summary>
    public string Name { get; set; } = "";
}

/// <summary>
/// 重命名绑定请求。
/// </summary>
public class RenameBindingRequest
{
    public string Name { get; set; } = "";
}

/// <summary>
/// 绑定排序请求（相邻行互换 DisplayOrder，逐条提交）。
/// </summary>
public class ReorderBindingRequest
{
    public int DisplayOrder { get; set; }
}

/// <summary>
/// 跨 iNode 查询请求。
/// </summary>
public class InodeQueryRequest
{
    /// <summary>可选：限定 iNode。null = 跨所有 iNode 查询。</summary>
    public string? InodeId { get; set; }

    /// <summary>必填：实体类型名（如 "UnitTree"）。</summary>
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
