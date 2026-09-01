using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

public class AttributeItemDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    
    public AttributeDefinition? Def { get; set; }
    
    public string? TextValue { get; set; }
    public long IntegerValue { get; set; }
    public decimal? DecimalValue { get; set; }
    public DateTimeOffset? DateValue { get; set; }
    public DateTimeOffset? DateTimeValue { get; set; }
    public TimeSpan? TimeValue { get; set; }
    public string? LocationName { get; set; }
    public string? FileName { get; set; }
    public string? TableName { get; set; }
    
    public JsonElement ToJsonElement() => Def?.TypeEnum switch
    {
        AttributeTypeEnum.整数 => JsonSerializer.SerializeToElement(IntegerValue),
        AttributeTypeEnum.小数 => JsonSerializer.SerializeToElement(DecimalValue),
        AttributeTypeEnum.日期 => JsonSerializer.SerializeToElement(DateValue),
        AttributeTypeEnum.时间 => JsonSerializer.SerializeToElement(TimeValue),
        AttributeTypeEnum.日期时间 => JsonSerializer.SerializeToElement(DateTimeValue),
        AttributeTypeEnum.定位 => JsonSerializer.SerializeToElement(LocationName),
        AttributeTypeEnum.文件 => JsonSerializer.SerializeToElement(FileName),
        AttributeTypeEnum.表格 => JsonSerializer.SerializeToElement(TableName),
        _ => JsonSerializer.SerializeToElement(TextValue)
    };
}