using System.Text.Json;
using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>组合类型值服务：递归验证 / JSONB 序列化 / 反序列化</summary>
public class CompositeValueService
{
    private readonly ICompositeTypeCache _cache;
    private readonly EavValidationService _validator;

    /// <summary>
    /// ★ #4：注入单位服务以支持组合内 decimal 字段的单位换算。
    /// UnitId 存在于 CompositeFieldDefinition 上（数据库 CHECK 约束：
    /// 仅 decimal 类型可以绑定）。
    /// </summary>
    private readonly IUnitCache _unitCache;
    private readonly UnitConverter _converter;

    public CompositeValueService(
        ICompositeTypeCache cache,
        EavValidationService validator,
        IUnitCache unitCache,
        UnitConverter converter)
    {
        _cache = cache;
        _validator = validator;
        _unitCache = unitCache;
        _converter = converter;
    }

    // ============================================================
    // 验证
    // ============================================================

    public ValidationResult Validate(DynamicCompositeValue value, long compositeTypeId)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var errors = new List<ValidationError>();

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            value.TryGet<object?>(field.FieldName, out var raw);

            if (field.IsRequired && raw is null)
            {
                errors.Add(new(field.FieldName, "必填字段"));
                continue;
            }
            if (raw is null) continue;

            if (field.IsArray)
            {
                if (raw is not IEnumerable<object?> items)
                {
                    errors.Add(new(field.FieldName, "期望数组"));
                    continue;
                }
                int i = 0;
                foreach (var item in items)
                {
                    ValidateField(field, item, $"{field.FieldName}[{i}]", errors);
                    i++;
                }
            }
            else
            {
                ValidateField(field, raw, field.FieldName, errors);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private void ValidateField(
        CompositeFieldDefinition field, object? value,
        string path, List<ValidationError> errors)
    {
        if (field.DataType == EavDataTypes.Composite)
        {
            if (value is not DynamicCompositeValue nested)
            {
                errors.Add(new(path, "期望嵌套组合值"));
                return;
            }
            var nestedResult = Validate(nested, field.RefCompositeTypeId!.Value);
            foreach (var e in nestedResult.Errors)
                errors.Add(new($"{path}.{e.Field}", e.Message));
            return;
        }

        // ★ #4：组合内 decimal 带单位时，校验单位分类与归一化后的范围
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (field.UnitId is { } baseUnitId && nv.UnitId is { } inputUnitId)
            {
                try
                {
                    var baseUnit = _unitCache.Get(baseUnitId);
                    var inputUnit = _unitCache.Get(inputUnitId);
                    if (inputUnit.Category != baseUnit.Category)
                    {
                        errors.Add(new(path,
                            $"单位分类不匹配：期望 {baseUnit.Category}，实际 {inputUnit.Category}"));
                    }
                }
                catch (InvalidOperationException)
                {
                    errors.Add(new(path, "单位不存在"));
                }
            }
            else if (field.UnitId is null && nv.UnitId is not null)
            {
                errors.Add(new(path, "该字段未绑定基准单位，不允许指定单位"));
            }
            // 用归一化后的值做范围校验
            if (field.ValidationRule is not null)
            {
                var normalized = NormalizeToBase(field, nv.Value, nv.UnitId);
                var result = ValidateNumericRange(field, normalized);
                if (result is not null)
                    errors.Add(new(path, result));
            }
            return;
        }

        // 其它类型：构造临时 AttributeDefinition 复用基础验证器
        var tempDef = new AttributeDefinition
        {
            AttributeName = path,
            DataType = field.DataType,
            IsRequired = field.IsRequired,
            ValidationRule = field.ValidationRule,
            AllowedValues = field.AllowedValues
        };
        var result2 = _validator.ValidateBaseValue(tempDef, value);
        errors.AddRange(result2.Errors);
    }

    private decimal NormalizeToBase(
        CompositeFieldDefinition field, decimal value, Guid? inputUnitId)
    {
        if (field.UnitId is not { } baseUnitId) return value;
        if (inputUnitId is null || inputUnitId == baseUnitId) return value;
        return _converter.ToBase(value, inputUnitId.Value, baseUnitId);
    }

    private static string? ValidateNumericRange(
        CompositeFieldDefinition field, decimal value)
    {
        if (field.ValidationRule is null) return null;
        var rule = field.ValidationRule.RootElement;

        if (rule.TryGetProperty("min", out var min)
            && min.TryGetDecimal(out var minVal)
            && value < minVal)
            return $"不能小于 {minVal}";

        if (rule.TryGetProperty("max", out var max)
            && max.TryGetDecimal(out var maxVal)
            && value > maxVal)
            return $"不能大于 {maxVal}";

        return null;
    }

    // ============================================================
    // 序列化
    // ============================================================

    /// <summary>
    /// 序列化为 JsonDocument（用于存储 ValueJsonb）。
    ///
    /// ★ #4：组合内 decimal 字段带单位时，序列化为
    /// `{ value: <归一化到基准单位的数值>, unitId: <原始输入单位 Guid> }`。
    /// 读取端（Deserialize）根据 originalUnits 决定是否还原。
    /// </summary>
    public JsonDocument Serialize(DynamicCompositeValue value, long compositeTypeId)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var dict = new Dictionary<string, object?>();

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            value.TryGet<object?>(field.FieldName, out var raw);
            dict[field.FieldName] = SerializeFieldValue(raw, field);
        }

        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    private object? SerializeFieldValue(object? value, CompositeFieldDefinition field)
    {
        if (value is null) return null;

        if (field.IsArray && value is IEnumerable<object?> items)
            return items.Select(i => SerializeFieldValue(i, field)).ToList();

        if (field.DataType == EavDataTypes.Composite && value is DynamicCompositeValue nested)
        {
            var inner = Serialize(nested, field.RefCompositeTypeId!.Value);
            return inner.RootElement.Clone();
        }

        // ★ #4：组合内 decimal 的 NumericValue 序列化
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (field.UnitId is not { } baseUnitId || nv.UnitId is null)
                return nv.Value;  // 无单位：存裸数值

            var baseValue = _converter.ToBase(nv.Value, nv.UnitId.Value, baseUnitId);
            return new { value = baseValue, unitId = nv.UnitId.Value };
        }

        // 基础类型：按类型返回可序列化对象
        return field.DataType switch
        {
            EavDataTypes.Datetime when value is DateTimeOffset dto => dto,
            EavDataTypes.Date when value is DateOnly d => d.ToString("yyyy-MM-dd"),
            EavDataTypes.Time when value is TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }

    // ============================================================
    // 反序列化
    // ============================================================

    /// <summary>
    /// 从 JsonDocument 反序列化为运行时值。
    ///
    /// ★ #4：新增 originalUnits 参数。
    ///   - false（默认）：组合内 decimal 返回基准单位值
    ///   - true：按原始输入单位还原
    /// </summary>
    public DynamicCompositeValue Deserialize(
        JsonDocument doc, long compositeTypeId, bool originalUnits = false)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var result = new DynamicCompositeValue(typeDef.TypeName);
        var root = doc.RootElement;

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            if (!root.TryGetProperty(field.FieldName, out var elem)) continue;
            result[field.FieldName] = DeserializeFieldValue(elem, field, originalUnits);
        }

        return result;
    }

    private object? DeserializeFieldValue(
        JsonElement elem, CompositeFieldDefinition field, bool originalUnits)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.IsArray)
            return elem.EnumerateArray()
                .Select(e => DeserializeFieldValue(e, field, originalUnits)).ToList();

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nestedDoc = JsonDocument.Parse(elem.GetRawText());
            return Deserialize(nestedDoc, field.RefCompositeTypeId!.Value, originalUnits);
        }

        // ★ #4：组合内 decimal 的 {value, unitId} 对象
        if (field.DataType == EavDataTypes.Decimal && elem.ValueKind == JsonValueKind.Object)
        {
            var baseValue = elem.GetProperty("value").GetDecimal();
            Guid? originalUnitId = elem.TryGetProperty("unitId", out var u)
                                    && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;

            if (field.UnitId is not { } baseUnitId)
                return new NumericValue(baseValue, null);

            if (!originalUnits || originalUnitId is null || originalUnitId == baseUnitId)
                return new NumericValue(baseValue, baseUnitId);

            try
            {
                var restored = _converter.FromBase(baseValue, baseUnitId, originalUnitId.Value);
                return new NumericValue(restored, originalUnitId);
            }
            catch (InvalidOperationException)
            {
                return new NumericValue(baseValue, baseUnitId);
            }
        }

        return field.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
