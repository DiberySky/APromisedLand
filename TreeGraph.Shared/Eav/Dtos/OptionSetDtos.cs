namespace TreeGraph.Shared.Eav.Dtos;

/// <summary>创建选项集（POST api/eav/metadata/option-sets）</summary>
public class CreateOptionSetRequest
{
    public string EntityType { get; set; } = "";
    public string SetName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

/// <summary>选项集摘要（GET api/eav/metadata/option-sets）</summary>
public record OptionSetSummaryDto(
    long OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName);

/// <summary>添加选项（POST api/eav/metadata/option-sets/{setId}/items）</summary>
public class CreateOptionItemRequest
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>更新选项（PUT .../items/{itemId}）。Value 不可改，改值请删除后新增。</summary>
public class UpdateOptionItemRequest
{
    public string? Label { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsDefault { get; set; }
}

/// <summary>选项重排序项（PUT .../items/reorder）</summary>
public class ReorderOptionItem
{
    public long OptionItemId { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>选项集详情（GET api/eav/metadata/option-sets/{setId}，含未删除选项列表）</summary>
public record OptionSetDetailDto(
    long OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OptionItemDetailDto> Items);

/// <summary>选项详情（含软删除状态，供管理页展示）</summary>
public record OptionItemDetailDto(
    long OptionItemId,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsDefault,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

/// <summary>选项集被引用信息（GET api/eav/metadata/option-sets/{setId}/references 返回项）</summary>
public record OptionSetReferenceDto(
    long AttributeId,
    string EntityType,
    string AttributeName,
    string DisplayName);

/// <summary>更新选项集基本信息（PUT api/eav/metadata/option-sets/{setId}；SetName/EntityType 不可修改）</summary>
public class UpdateOptionSetRequest
{
    public string? DisplayName { get; set; }
}
