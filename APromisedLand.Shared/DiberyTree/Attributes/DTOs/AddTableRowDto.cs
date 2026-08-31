using System.Text.Json;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

/// <summary>新增行 DTO：列定义 Id -> 列值（JSON，由 ValueValidator 按列类型解析）。</summary>
public class AddTableRowDto
{   
    public string NodeId { get; set; } = null!;
    public string DefinitionId { get; set; } = null!;
    public string TableId { get; set; } = null!;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public Dictionary<string, JsonElement> Values { get; set; } = new();
}