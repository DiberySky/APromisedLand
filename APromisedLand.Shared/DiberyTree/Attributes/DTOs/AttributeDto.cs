namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DefinitionId { get; set; } = null!;
    public string DefinitionName { get; set; } = null!;
    public string Type { get; set; } = null!;                 // 类型名称
    public string? TypeDescription { get; set; }              // 类型描述
    public string? Unit { get; set; }                         // 单位符号或名称
    public int? Lines { get; set; }
    public string? Value { get; set; }
}