using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.Helper;

namespace APromisedLand.Razor.DiberyTree.Attributes.Values;

public class CellValue
{
    public string? Text { get; set; }
    public long? Integer { get; set; }
    public decimal? Decimal { get; set; }
    public DateTime? Date { get; set; }
    public TimeSpan? Time { get; set; }
    public DateTime? DateTime { get; set; }

    public AttributeDefinition? Definition { get; set; }
    
    public List<AttributeJsonValueDto> AttributeDtos { get; set; } = [];

    public bool IsEmpty() =>
        string.IsNullOrEmpty(Text) && Integer == null && Decimal == null
        && Date == null && Time == null && DateTime == null;

    // private void SetJsonElement(object? raw, AttributeTypeEnum? type)
    // {
    //     if (raw is not JsonElement je) return;
    //     try
    //     {
    //         switch (type)
    //         {
    //             case AttributeTypeEnum.整数: Integer = je.GetInt32(); break;
    //             case AttributeTypeEnum.小数: Decimal = je.GetDecimal(); break;
    //             case AttributeTypeEnum.日期: Date = je.GetDateTime(); break;
    //             case AttributeTypeEnum.时间: Time = je.GetValue<TimeSpan>(); break;
    //             case AttributeTypeEnum.日期时间: DateTime = je.GetDateTime(); break;
    //             default: Text = je.ToString(); break;
    //         }
    //     }
    //     catch
    //     {
    //         Text = je.ToString();
    //     }
    //
    //     this.type = type; // ✅ 保存类型
    // }
    
    public void SetFrom(object? raw, AttributeTypeEnum? type)
    {
        if (raw is not JsonElement je) return;
        try
        {
            switch (type)
            {
                case AttributeTypeEnum.整数: Integer = je.GetInt32(); break;
                case AttributeTypeEnum.小数: Decimal = je.GetDecimal(); break;
                case AttributeTypeEnum.日期: Date = je.GetDateTime(); break;
                case AttributeTypeEnum.时间: Time = je.GetValue<TimeSpan>(); break;
                case AttributeTypeEnum.日期时间: DateTime = je.GetDateTime(); break;
                default: Text = je.ToString(); break;
            }
        }
        catch
        {
            Text = je.ToString();
        }

        this.type = type; // ✅ 保存类型
    }

    public JsonElement ToJsonElement() => type switch
    {
        AttributeTypeEnum.整数 => JsonSerializer.SerializeToElement(Integer),
        AttributeTypeEnum.小数 => JsonSerializer.SerializeToElement(Decimal),
        AttributeTypeEnum.日期 => JsonSerializer.SerializeToElement(Date),
        AttributeTypeEnum.时间 => JsonSerializer.SerializeToElement(Time),
        AttributeTypeEnum.日期时间 => JsonSerializer.SerializeToElement(DateTime),
        _ => JsonSerializer.SerializeToElement(Text)
    };

    private AttributeTypeEnum? type;
    public void SetType(AttributeTypeEnum t) => type = t;
}