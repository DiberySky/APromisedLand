namespace TreeGraph.Shared.NodeEavSky.Dtos;

public class CreateOptionSetRequest
{
    public string EntityType { get; set; } = "";
    public string SetName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public record OptionSetSummaryDto(
    string OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName,
    bool IsDeleted = false);

public class CreateOptionItemRequest
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateOptionItemRequest
{
    public string? Label { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsDefault { get; set; }
}

public class ReorderOptionItem
{
    public string OptionItemId { get; set; } = "";
    public int DisplayOrder { get; set; }
}

public record OptionSetDetailDto(
    string OptionSetId,
    string EntityType,
    string SetName,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OptionItemDetailDto> Items,
    bool IsDeleted = false);

public record OptionItemDetailDto(
    string OptionItemId,
    string Value,
    string Label,
    int DisplayOrder,
    bool IsDefault,
    bool IsDeleted,
    DateTimeOffset CreatedAt);

public record OptionSetReferenceDto(
    string AttributeId,
    string EntityType,
    string AttributeName,
    string DisplayName);

public class UpdateOptionSetRequest
{
    public string? DisplayName { get; set; }
}
