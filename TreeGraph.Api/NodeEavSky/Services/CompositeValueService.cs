using System.Text.Json;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Shared.NodeEavSky;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>组合类型值服务：递归验证 / JSONB 序列化 / 反序列化</summary>
public class CompositeValueService
{
    private readonly ICompositeTypeCache _cache;
    private readonly EavValidationService _validator;

    public CompositeValueService(
        ICompositeTypeCache cache,
        EavValidationService validator)
    {
        _cache = cache;
        _validator = validator;
    }

    // ============================================================
    // 验证
    // ============================================================

    public ValidationResult Validate(DynamicCompositeValue value, string compositeTypeId)
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
            var nestedResult = Validate(nested, field.RefCompositeTypeId!);
            foreach (var e in nestedResult.Errors)
                errors.Add(new($"{path}.{e.Field}", e.Message));
            return;
        }

        // ★ #4：组合内 decimal 带单位（NumericValue）时，单位仅作记录，直接校验数值范围
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (field.ValidationRule is not null)
            {
                var result = ValidateNumericRange(field, nv.Value);
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
    /// ★ #4：组合内 decimal 字段带输入单位时，序列化为
    /// `{ value: <输入数值>, unitId: <unit_trees 节点 Id> }`（单位仅作记录，不做换算）。
    /// </summary>
    public JsonDocument Serialize(DynamicCompositeValue value, string compositeTypeId)
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
            var inner = Serialize(nested, field.RefCompositeTypeId!);
            return inner.RootElement.Clone();
        }

        // ★ #4：组合内 decimal 的 NumericValue 序列化（单位仅作记录）
        if (field.DataType == EavDataTypes.Decimal && value is NumericValue nv)
        {
            if (nv.UnitId is null)
                return nv.Value;  // 无单位：存裸数值

            return new { value = nv.Value, unitId = nv.UnitId };
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
    /// 组合内 decimal 的 {value, unitId} 对象还原为 NumericValue（单位仅作记录）。
    /// </summary>
    public DynamicCompositeValue Deserialize(
        JsonDocument doc, string compositeTypeId)
    {
        var typeDef = _cache.GetType(compositeTypeId);
        var result = new DynamicCompositeValue(typeDef.TypeName);
        var root = doc.RootElement;

        foreach (var field in typeDef.Fields.Where(f => !f.IsDeleted))
        {
            if (!root.TryGetProperty(field.FieldName, out var elem)) continue;
            result[field.FieldName] = DeserializeFieldValue(elem, field);
        }

        return result;
    }

    private object? DeserializeFieldValue(
        JsonElement elem, CompositeFieldDefinition field)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.IsArray)
            return elem.EnumerateArray()
                .Select(e => DeserializeFieldValue(e, field)).ToList();

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nestedDoc = JsonDocument.Parse(elem.GetRawText());
            return Deserialize(nestedDoc, field.RefCompositeTypeId!);
        }

        // ★ #4：组合内 decimal 的 {value, unitId} 对象
        if (field.DataType == EavDataTypes.Decimal && elem.ValueKind == JsonValueKind.Object)
        {
            var value = elem.GetProperty("value").GetDecimal();
            string? unitId = elem.TryGetProperty("unitId", out var u)
                             && u.ValueKind == JsonValueKind.String
                ? u.GetString()
                : null;

            return new NumericValue(value, unitId ?? field.UnitId);
        }

        // ★ 修复 D1：decimal 字段绑定了单位时，裸数字（旧数据形态）也返回
        //   NumericValue，与 Object 分支（{ value, unitId }）保持类型一致；
        //   未绑定单位时仍返回裸 decimal。when 子句必须在裸 Decimal 分支之前。
        return field.DataType switch
        {
            EavDataTypes.Int => elem.GetInt64(),
            EavDataTypes.Decimal when field.UnitId is { } unitId
                => new NumericValue(elem.GetDecimal(), unitId),
            EavDataTypes.Decimal => elem.GetDecimal(),
            EavDataTypes.Bool => elem.GetBoolean(),
            EavDataTypes.Datetime => elem.GetDateTimeOffset(),
            EavDataTypes.Date => DateOnly.Parse(elem.GetString()!),
            EavDataTypes.Time => TimeOnly.Parse(elem.GetString()!),
            _ => elem.GetString()
        };
    }
}
