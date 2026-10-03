using System.Text.Json;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>
/// 把 HTTP 请求体 Dictionary&lt;string, JsonElement&gt; 转成 EAV 写入管道
/// 接受的强类型字典。
///
/// 与 EavController 内部的转换逻辑一致（重复以防污染旧控制器）。
/// </summary>
public static class EavRequestBodyConverter
{
    public static Dictionary<string, object?> Convert(
        Dictionary<string, JsonElement> values,
        IReadOnlyDictionary<string, AttributeDefinition> defs,
        CompositeValueService compositeService)
    {
        var result = new Dictionary<string, object?>();
        var tableAttrs = new List<string>();

        // 未知属性检查
        var unknownKeys = values.Keys.Where(k => !defs.ContainsKey(k)).ToList();
        if (unknownKeys.Count > 0)
        {
            throw new EavValidationException(unknownKeys
                .Select(k => new ValidationError(k, "未知属性"))
                .ToList());
        }

        foreach (var (name, elem) in values)
        {
            var def = defs[name];

            if (def.DataType == EavDataTypes.Table)
            {
                tableAttrs.Add(name);
                continue;
            }

            result[name] = ConvertElement(elem, def, compositeService);
        }

        if (tableAttrs.Count > 0)
        {
            throw new ArgumentException(
                $"table 类型属性不能通过此端点写入，请使用子表端点。" +
                $"涉及属性: {string.Join(", ", tableAttrs)}");
        }

        return result;
    }

    private static object? ConvertElement(
        JsonElement elem, AttributeDefinition def,
        CompositeValueService compositeService)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        return def.DataType switch
        {
            EavDataTypes.Int or EavDataTypes.Decimal => ParseNumeric(elem),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            EavDataTypes.String => elem.GetString(),
            EavDataTypes.SingleChoice => ParseSingleChoice(elem),
            EavDataTypes.Json => JsonDocument.Parse(elem.GetRawText()),
            EavDataTypes.File => JsonDocument.Parse(elem.GetRawText()),
            EavDataTypes.Composite => ParseComposite(elem, def, compositeService),
            _ => elem.GetString()
        };
    }

    private static NumericValue ParseNumeric(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Number)
            return new NumericValue(elem.GetDecimal(), null);

        if (elem.ValueKind == JsonValueKind.Object)
        {
            var value = elem.GetProperty("value").GetDecimal();
            Guid? unitId = elem.TryGetProperty("unitId", out var u)
                           && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;
            return new NumericValue(value, unitId);
        }
        throw new ArgumentException("数值格式错误");
    }

    private static string? ParseSingleChoice(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String) return elem.GetString();
        if (elem.ValueKind == JsonValueKind.Object
            && elem.TryGetProperty("value", out var v)
            && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return elem.ToString();
    }

    private static DynamicCompositeValue ParseComposite(
        JsonElement elem, AttributeDefinition def,
        CompositeValueService compositeService)
    {
        if (def.RefCompositeTypeId is null)
            throw new ArgumentException(
                $"属性 {def.AttributeName} 是 composite 但未配置 RefCompositeTypeId");

        using var doc = JsonDocument.Parse(elem.GetRawText());
        return compositeService.Deserialize(doc, def.RefCompositeTypeId);
    }
}
