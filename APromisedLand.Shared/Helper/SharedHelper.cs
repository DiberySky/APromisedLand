using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.Helper;

public static partial class SharedHelper
{
    public static string Ellipsis(this string text, int length = 10)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text.Length < length ? text : $"{text[..length]}...";
    }
    
    // public static AttributeTypeEnum GetTypeEnum(this AttributeDto dto)
    // {
    //     var systemType = AttributeTypeMapping.GetType(dto.Definition.AttributeTypeId);
    //     // return Enum.TryParse<AttributeTypeEnum>(systemType, true, out var result)
    //     //     ? result
    //     //     : AttributeTypeEnum.文本;
    //     return systemType;
    // }

    public static T? GetValue<T>(this JsonElement element)
    {
        try
        {
            return element.Deserialize<T>();
        }
        catch
        {
            return default;
        }
    }
}
