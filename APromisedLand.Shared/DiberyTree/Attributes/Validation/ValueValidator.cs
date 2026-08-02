using System.Globalization;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Shared.DiberyTree.Attributes.Validation;

public static class ValueValidator
{
    public static ValueValidationResult ValidateAndBuild(
        AttributeDefinition def,
        string inputValue,
        string nodeId)
    {
        if (def.AttributeType == null)
            throw new InvalidOperationException("AttributeDefinition must include AttributeType navigation.");

        var systemType = def.AttributeType.SystemType;
        var result = new ValueValidationResult();

        switch (systemType)
        {
            case AttributeTypeEnum.文本:
                if (string.IsNullOrWhiteSpace(inputValue))
                    return Fail("文本值不能为空");
                result.ValueEntity = new TextAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = inputValue
                };
                break;

            case AttributeTypeEnum.整数:
                if (!long.TryParse(inputValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
                    return Fail("无效的整数");
                result.ValueEntity = new IntegerAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = longVal
                };
                break;

            case AttributeTypeEnum.小数:
                if (!decimal.TryParse(inputValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var decVal))
                    return Fail("无效的小数");
                if (!ValidatePrecision(def, inputValue, out var error))
                    return Fail(error);
                result.ValueEntity = new DecimalAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = decVal
                };
                break;

            case AttributeTypeEnum.日期:
                if (!DateTimeOffset.TryParse(inputValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return Fail("无效的日期（格式如 yyyy-MM-dd）");
                result.ValueEntity = new DateAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = date
                };
                break;

            case AttributeTypeEnum.时间:
                if (!TimeSpan.TryParse(inputValue, CultureInfo.InvariantCulture, out var time))
                    return Fail("无效的时间（格式如 HH:mm:ss）");
                result.ValueEntity = new TimeAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = time
                };
                break;

            case AttributeTypeEnum.日期时间:
                if (!DateTimeOffset.TryParse(inputValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return Fail("无效的日期时间（格式如 yyyy-MM-ddTHH:mm:ss）");
                result.ValueEntity = new DateTimeAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = dt
                };
                break;

            case AttributeTypeEnum.文件:
                if (string.IsNullOrWhiteSpace(inputValue))
                    return Fail("文件路径不能为空");
                result.ValueEntity = new FileAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Value = inputValue
                };
                break;

            case AttributeTypeEnum.定位:
                var parts = inputValue.Split(',');
                if (parts.Length != 2 ||
                    !double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
                    return Fail("无效的经纬度，格式应为：纬度,经度 (如 39.9042,116.4074)");
                if (lat < -90 || lat > 90)
                    return Fail("纬度必须在 -90 到 90 之间");
                if (lng < -180 || lng > 180)
                    return Fail("经度必须在 -180 到 180 之间");
                result.ValueEntity = new LocationAttributeValue
                {
                    NodeId = nodeId,
                    AttributeDefinitionId = def.Id,
                    Latitude = lat,
                    Longitude = lng
                };
                break;

            default:
                return Fail($"未知的属性类型: {systemType}");
        }

        result.IsValid = true;
        return result;
    }

    private static bool ValidatePrecision(AttributeDefinition def, string inputValue, out string error)
    {
        error = string.Empty;
        if (!def.Precision.HasValue && !def.Scale.HasValue)
            return true;

        var parts = inputValue.Split('.');
        var intPart = parts[0].TrimStart('-');
        var fracPart = parts.Length > 1 ? parts[1] : "";
        int intDigits = intPart.Length;
        int fracDigits = fracPart.Length;

        if (def.Precision.HasValue && intDigits + fracDigits > def.Precision.Value)
        {
            error = $"数字总位数不能超过 {def.Precision}";
            return false;
        }
        if (def.Scale.HasValue && fracDigits > def.Scale.Value)
        {
            error = $"小数位数不能超过 {def.Scale}";
            return false;
        }
        return true;
    }

    private static ValueValidationResult Fail(string msg) =>
        new() { IsValid = false, ErrorMessage = msg };
}