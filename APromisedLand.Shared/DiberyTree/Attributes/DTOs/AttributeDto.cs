using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DefinitionId { get; set; } = null!;
    public AttributeDefinition Definition { get; set; } = new();
    
    // public AttributeTypeEnum Type { get; set; } = AttributeTypeEnum.文本;                 // 类型名称
    // public string? TypeDescription { get; set; }              // 类型描述
    // public string? Unit { get; set; }                         // 单位符号或名称
    //
    //
    // public int Lines { get; set; }
    public JsonElement Value { get; set; }

}