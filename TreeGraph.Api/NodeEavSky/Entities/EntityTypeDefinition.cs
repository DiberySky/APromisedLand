namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 实体类型目录（独立于属性定义）。
/// 创建属性前，其 EntityType 必须已存在于本表。
/// </summary>
public class EntityTypeDefinition
{
    public string EntityTypeId { get; set; } = "";
    public string EntityType { get; set; } = "";         // "Product"
    public string DisplayName { get; set; } = "";        // "商品"
    public string? Description { get; set; }             // 说明
    public int DisplayOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
