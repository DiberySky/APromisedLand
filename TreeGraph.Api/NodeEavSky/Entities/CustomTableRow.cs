using System.Text.Json;

namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 自定义表行数据：所有表共享一张物理行表，按 TableDefinitionId 区分，
/// 行内结构由列定义驱动，数据存 JSONB
/// </summary>
public class CustomTableRow
{
    public string RowId { get; set; } = "";
    public string TableDefinitionId { get; set; } = "";
    public string AttributeId { get; set; } = "";     // 关联的属性（一个属性对应一张表）
    public string ParentEntityId { get; set; } = "";
    public string ParentEntityType { get; set; } = "";

    /// <summary>行内数据 JSONB：{ "cert_name": "CE", "issuer": "TÜV" }</summary>
    public JsonDocument RowData { get; set; } = null!;

    public int RowOrder { get; set; }                 // 行的展示顺序
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AttributeDefinition Attribute { get; set; } = null!;
    public CustomTableDefinition Table { get; set; } = null!;
}
