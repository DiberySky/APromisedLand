using System.Text.Json;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

/// <summary>更新行 DTO：列定义 Id -> 列值（JSON）。</summary>
public class UpdateTableRowDto
{
    public string NodeId { get; set; } = null!;
    public string DefinitionId { get; set; } = null!;
    public string TableId { get; set; } = null!;
    public string RowId { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, JsonElement> Values { get; set; } = new();
}
