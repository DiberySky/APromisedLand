using System.Globalization;
using System.Text.Json;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Validation;

public static class ValueValidator
{
    public static (bool IsValid, string? ErrorMessage, AttributeValueBase? ValueEntity) 
        ValidateAndBuild(AttributeDefinition def, JsonElement jsonValue, string nodeId)
    {
        var typeEnum = def.AttributeTypeId.ToAttributeTypeEnum();
        try
        {
            switch (typeEnum)
            {
                case AttributeTypeEnum.文本: return ValidateText(def, jsonValue, nodeId);
                case AttributeTypeEnum.整数: return ValidateInteger(def, jsonValue, nodeId);
                case AttributeTypeEnum.小数: return ValidateDecimal(def, jsonValue, nodeId);
                case AttributeTypeEnum.日期: return ValidateDate(def, jsonValue, nodeId);
                case AttributeTypeEnum.时间: return ValidateTime(def, jsonValue, nodeId);
                case AttributeTypeEnum.日期时间: return ValidateDateTime(def, jsonValue, nodeId);
                case AttributeTypeEnum.文件: return ValidateFile(def, jsonValue, nodeId);
                case AttributeTypeEnum.定位: return ValidateLocation(def, jsonValue, nodeId);
                case AttributeTypeEnum.表格: return ValidateTable(def, jsonValue, nodeId);
                default: return (false, $"不支持的类型 '{typeEnum}'", null);
            }
        }
        catch (Exception ex)
        {
            return (false, $"解析失败: {ex.Message}", null);
        }
    }

    // ---------- 辅助方法：从对象中提取 value 属性 ----------
    private static JsonElement ExtractValue(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("value", out var inner))
            return inner;
        return json;
    }

    // ---------- 各类型验证（已添加提取逻辑） ----------
    private static (bool, string?, AttributeValueBase?) ValidateText(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.String)
            return (false, "文本类型必须提供字符串", null);
        string value = actual.GetString()!;
        if (value.Length > def.MaxLength)
            return (false, $"文本长度不能超过 {def.MaxLength} 个字符", null);
        return (true, null, new TextAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Value = value
        });
    }

    private static (bool, string?, AttributeValueBase?) ValidateInteger(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.Number)
            return (false, "整数类型必须提供数字", null);
        if (!actual.TryGetInt64(out long value))
            return (false, "数值超出整数范围", null);
        return (true, null, new IntegerAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Value = value
        });
    }

    private static (bool, string?, AttributeValueBase?) ValidateDecimal(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.Number)
            return (false, "小数类型必须提供数字", null);
        if (!actual.TryGetDecimal(out decimal value))
            return (false, "数值无法转换为有效小数", null);
        return (true, null, new DecimalAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Value = value
        });
    }

    private static (bool, string?, AttributeValueBase?) ValidateDate(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(actual.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return (true, null, new DateAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = date.Date
                });
        }
        // 如果前端直接传对象（如 { "date": "2026-08-12" }），可以额外处理，但一般不建议
        return (false, "日期格式无效，请使用 ISO 8601 字符串（如 2026-08-12）", null);
    }

    private static (bool, string?, AttributeValueBase?) ValidateTime(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind == JsonValueKind.String)
        {
            if (TimeSpan.TryParse(actual.GetString(), CultureInfo.InvariantCulture, out var time))
                return (true, null, new TimeAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = time
                });
        }
        return (false, "时间格式无效，请使用 HH:mm:ss 格式", null);
    }

    private static (bool, string?, AttributeValueBase?) ValidateDateTime(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(actual.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return (true, null, new DateTimeAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = dt
                });
        }
        return (false, "日期时间格式无效，请使用 ISO 8601 字符串（如 2026-08-12T14:30:00Z）", null);
    }

    private static (bool, string?, AttributeValueBase?) ValidateFile(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.String)
            return (false, "文件类型必须提供路径或URL字符串", null);
        var value = actual.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return (false, "文件路径不能为空", null);
        return (true, null, new FileAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Value = value
        });
    }

    private static (bool, string?, AttributeValueBase?) ValidateLocation(AttributeDefinition def, JsonElement json, string nodeId)
    {
        // 定位可能需要从对象中提取 value，但若 value 本身也是一个对象（包含 lat/lng），则提取后继续
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.Object)
            return (false, "定位类型必须提供包含 latitude 和 longitude 的对象", null);

        if (!actual.TryGetProperty("latitude", out var latProp) || !actual.TryGetProperty("longitude", out var lonProp))
            return (false, "缺少 latitude 或 longitude 属性", null);

        if (!latProp.TryGetDouble(out double lat) || !lonProp.TryGetDouble(out double lon))
            return (false, "latitude 和 longitude 必须为数字", null);

        if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            return (false, "纬度范围 [-90, 90]，经度范围 [-180, 180]", null);

        return (true, null, new LocationAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Latitude = lat,
            Longitude = lon
        });
    }
    
    private static (bool, string?, AttributeValueBase?) ValidateTable(AttributeDefinition def, JsonElement json, string nodeId)
    {
        var actual = ExtractValue(json);
        if (actual.ValueKind != JsonValueKind.String)
            return (false, "文本类型必须提供字符串", null);
        string value = actual.GetString()!;
        if (value.Length > def.MaxLength)
            return (false, $"文本长度不能超过 {def.MaxLength} 个字符", null);
        return (true, null, new TextAttributeValue
        {
            NodeId = nodeId,
            AttributeDefinitionId = def.Id,
            Value = value
        });
    }

}