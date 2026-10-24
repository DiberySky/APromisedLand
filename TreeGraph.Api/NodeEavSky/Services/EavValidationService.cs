using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>EAV 验证引擎：必填 -> 类型转换 -> 枚举约束 -> 规则校验</summary>
public class EavValidationService
{
    private readonly ICompositeTypeCache _compositeCache;
    private readonly IOptionSetCache _optionSetCache;

    public EavValidationService(
        ICompositeTypeCache compositeCache,
        IOptionSetCache optionSetCache)
    {
        _compositeCache = compositeCache;
        _optionSetCache = optionSetCache;
    }

    /// <summary>
    /// 验证属性值（基础类型）。
    ///
    /// ★ int / decimal 均可绑定单位（单位仅作记录，不参与校验与换算）。
    ///   - int 无单位：必须为整数，写入 ValueInt
    ///   - int 有单位：允许小数，写入 ValueDecimal
    ///   - decimal：无论有无单位，写入 ValueDecimal
    /// </summary>
    public ValidationResult ValidateBaseValue(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();

        // 单选类型：必填默认值放行逻辑与基础类型不同，走独立分支
        if (def.DataType == EavDataTypes.SingleChoice)
            return ValidateSingleChoice(def, value);

        // 必填校验
        if (def.IsRequired && value is null)
        {
            errors.Add(new(def.AttributeName, "必填字段"));
            return new ValidationResult(false, errors);
        }
        if (value is null) return new ValidationResult(true, errors);

        // 数量类型：解包 NumericValue（单位仅作记录，不参与校验与换算）
        if (def.DataType is EavDataTypes.Int or EavDataTypes.Decimal)
        {
            var (numericValue, _) = UnwrapNumeric(value);
            if (numericValue is null)
            {
                errors.Add(new(def.AttributeName, "数值格式错误"));
                return new ValidationResult(false, errors);
            }

            // ★ int 无单位时必须为整数
            if (def.DataType == EavDataTypes.Int
                && def.UnitId is null
                && numericValue.Value != Math.Truncate(numericValue.Value))
            {
                errors.Add(new(def.AttributeName,
                    $"int 类型（无单位）不接受小数，收到 {numericValue.Value}"));
                return new ValidationResult(false, errors);
            }

            ValidateNumericRange(def, numericValue.Value, errors);

            return new ValidationResult(errors.Count == 0, errors);
        }

        // 位置类型：结构 + 经纬度范围校验（GCJ-02 坐标）
        if (def.DataType == EavDataTypes.Location)
            return ValidateLocation(def, value);

        // 关系类型：结构校验（目标存在性由写入服务批量查库）
        if (def.DataType == EavDataTypes.Relation)
            return ValidateRelation(def, value);

        // 类型转换
        if (!TryConvert(def.DataType, value, out var converted))
        {
            errors.Add(new(def.AttributeName, $"类型不匹配，期望 {def.DataType}"));
            return new ValidationResult(false, errors);
        }

        // 枚举约束
        if (def.AllowedValues is not null && converted is not null)
        {
            var allowed = def.AllowedValues.RootElement.EnumerateArray()
                .Select(x => x.GetString()).ToList();
            if (!allowed.Contains(converted.ToString()))
                errors.Add(new(def.AttributeName, "值不在允许范围内"));
        }

        // 规则校验（正则、日期范围等）
        if (def.ValidationRule is not null)
            ValidateRule(def, converted, errors);

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>单选类型验证：null 且存在默认选项时放行（写入端自动填充默认值），否则校验选项存在性</summary>
    private ValidationResult ValidateSingleChoice(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();
        var set = def.RefOptionSetId is { } sid ? _optionSetCache.GetSet(sid) : null;

        if (value is null)
        {
            if (set is { } s && s.Items.Any(i => i.IsDefault))
                return new ValidationResult(true, errors);

            if (def.IsRequired)
                errors.Add(new(def.AttributeName, "必填字段"));

            return new ValidationResult(errors.Count == 0, errors);
        }

        if (set is null)
        {
            errors.Add(new(def.AttributeName, "属性未绑定选项集"));
            return new ValidationResult(false, errors);
        }

        var valueStr = value.ToString() ?? "";
        if (set.Items.All(i => i.Value != valueStr))
        {
            var validValues = string.Join(", ", set.Items.Select(i => i.Value));
            errors.Add(new(def.AttributeName,
                $"值 '{valueStr}' 不在选项集中，有效值：{validValues}"));
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 位置值校验：必须为 EavLocationValue，纬度 [-90,90]、经度 [-180,180]，
    /// 坐标系当前只接受 gcj02。地点名 / 地址做长度约束。
    /// </summary>
    private ValidationResult ValidateLocation(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();

        if (value is not EavLocationValue loc)
        {
            errors.Add(new(def.AttributeName, "位置值格式错误，应包含纬度 latitude 与经度 longitude"));
            return new ValidationResult(false, errors);
        }

        if (loc.Latitude < -90 || loc.Latitude > 90)
            errors.Add(new(def.AttributeName, $"纬度必须在 -90 到 90 之间，收到 {loc.Latitude}"));
        if (loc.Longitude < -180 || loc.Longitude > 180)
            errors.Add(new(def.AttributeName, $"经度必须在 -180 到 180 之间，收到 {loc.Longitude}"));

        if (!string.Equals(loc.CoordinateSystem, "gcj02", StringComparison.OrdinalIgnoreCase))
            errors.Add(new(def.AttributeName, $"当前仅支持 GCJ-02（火星坐标），收到 {loc.CoordinateSystem}"));

        if (loc.Label is { Length: > 200 })
            errors.Add(new(def.AttributeName, "地点名称不能超过 200 个字符"));
        if (loc.Address is { Length: > 500 })
            errors.Add(new(def.AttributeName, "地址不能超过 500 个字符"));

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// 关系值校验：必须为 EavRelationValue，toNodeId 非空，name ≤200。
    /// 目标节点是否存在于 nodes 由写入服务统一批量校验。
    /// </summary>
    private ValidationResult ValidateRelation(AttributeDefinition def, object? value)
    {
        var errors = new List<ValidationError>();

        if (value is not EavRelationValue rel)
        {
            errors.Add(new(def.AttributeName, "关系值格式错误，应包含 toNodeId 与可选 name"));
            return new ValidationResult(false, errors);
        }

        if (string.IsNullOrWhiteSpace(rel.ToNodeId))
            errors.Add(new(def.AttributeName, "toNodeId 必填"));

        if (rel.Name is { Length: > 200 })
            errors.Add(new(def.AttributeName, "关系名称不能超过 200 个字符"));

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static (decimal? Value, string? UnitId) UnwrapNumeric(object value)
    {
        return value switch
        {
            NumericValue nv => (nv.Value, nv.UnitId),
            decimal d => (d, null),
            int i => (i, null),
            long l => (l, null),
            double db => ((decimal)db, null),
            string s when decimal.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => (parsed, null),
            _ => (null, null)
        };
    }

    private static void ValidateNumericRange(
        AttributeDefinition def, decimal value, List<ValidationError> errors)
    {
        if (def.ValidationRule is null) return;
        var rule = def.ValidationRule.RootElement;

        if (rule.TryGetProperty("min", out var min)
            && decimal.TryParse(min.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var minVal)
            && value < minVal)
            errors.Add(new(def.AttributeName, $"不能小于 {minVal}"));

        if (rule.TryGetProperty("max", out var max)
            && decimal.TryParse(max.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var maxVal)
            && value > maxVal)
            errors.Add(new(def.AttributeName, $"不能大于 {maxVal}"));

        // 小数精度（ValidationRule.precision）：仅对 decimal 生效，约束小数位上限
        if (def.DataType == EavDataTypes.Decimal
            && rule.TryGetProperty("precision", out var prec)
            && prec.TryGetInt32(out var p)
            && p >= 0
            && value != decimal.Round(value, p))
            errors.Add(new(def.AttributeName, $"小数位不能超过 {p} 位"));
    }

    public bool TryConvert(string dataType, object value, out object? converted)
    {
        converted = null;
        try
        {
            converted = dataType switch
            {
                EavDataTypes.Int => Convert.ToInt64(value),
                EavDataTypes.Decimal => Convert.ToDecimal(value),
                EavDataTypes.Bool => Convert.ToBoolean(value),
                EavDataTypes.Datetime => value is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.Parse(value.ToString()!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                EavDataTypes.Date => value is DateOnly d
                    ? d
                    : DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                EavDataTypes.Time => value is TimeOnly t
                    ? t
                    : TimeOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                EavDataTypes.String => value.ToString(),
                EavDataTypes.Json => value is JsonDocument jd
                    ? jd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value)),
                EavDataTypes.File => value is JsonDocument fd
                    ? fd
                    : JsonDocument.Parse(JsonSerializer.Serialize(value)),
                _ => value.ToString()
            };
            return converted is not null;
        }
        catch { return false; }
    }

    private static void ValidateRule(
        AttributeDefinition def, object? value, List<ValidationError> errors)
    {
        var rule = def.ValidationRule!.RootElement;

        switch (def.DataType)
        {
            case EavDataTypes.Date when value is DateOnly dt:
                if (rule.TryGetProperty("minDate", out var minD)
                    && dt < DateOnly.Parse(minD.GetString()!))
                    errors.Add(new(def.AttributeName, "日期超出范围"));
                if (rule.TryGetProperty("maxDate", out var maxD)
                    && dt > DateOnly.Parse(maxD.GetString()!))
                    errors.Add(new(def.AttributeName, "日期超出范围"));
                break;

            case EavDataTypes.Time when value is TimeOnly tm:
                if (rule.TryGetProperty("minTime", out var minT)
                    && tm < TimeOnly.Parse(minT.GetString()!))
                    errors.Add(new(def.AttributeName, "时间超出范围"));
                if (rule.TryGetProperty("maxTime", out var maxT)
                    && tm > TimeOnly.Parse(maxT.GetString()!))
                    errors.Add(new(def.AttributeName, "时间超出范围"));
                break;

            case EavDataTypes.String when value is string str:
                if (rule.TryGetProperty("regex", out var regex)
                    && !Regex.IsMatch(str, regex.GetString()!))
                    errors.Add(new(def.AttributeName,
                        rule.TryGetProperty("message", out var m)
                            ? m.GetString()! : "格式不正确"));
                break;
        }
    }
}

public class EavValidationException : Exception
{
    public List<ValidationError> Errors { get; }
    public EavValidationException(List<ValidationError> errors)
        : base("EAV validation failed") => Errors = errors;
}
