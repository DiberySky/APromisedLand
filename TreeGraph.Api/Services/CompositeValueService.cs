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

    public CompositeValueService(ICompositeTypeCache cache, EavValidationService validator)
    {
        _cache = cache;
        _validator = validator;
    }

    /// <summary>递归验证组合值</summary>
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

        // 构造临时 AttributeDefinition 复用基础验证器
        var tempDef = new AttributeDefinition
        {
            AttributeName = path,
            DataType = field.DataType,
            IsRequired = field.IsRequired,
            ValidationRule = field.ValidationRule,
            AllowedValues = field.AllowedValues
        };
        var result = _validator.ValidateBaseValue(tempDef, value);
        errors.AddRange(result.Errors);
    }

    /// <summary>序列化为 JsonDocument（用于存储 ValueJsonb）</summary>
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

        // 基础类型：按类型返回可序列化对象
        return field.DataType switch
        {
            EavDataTypes.Datetime when value is DateTimeOffset dto => dto,
            EavDataTypes.Date when value is DateOnly d => d.ToString("yyyy-MM-dd"),
            EavDataTypes.Time when value is TimeOnly t => t.ToString("HH:mm:ss"),
            _ => value
        };
    }

    /// <summary>从 JsonDocument 反序列化为运行时值</summary>
    public DynamicCompositeValue Deserialize(JsonDocument doc, long compositeTypeId)
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

    private object? DeserializeFieldValue(JsonElement elem, CompositeFieldDefinition field)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.IsArray)
            return elem.EnumerateArray().Select(e => DeserializeFieldValue(e, field)).ToList();

        if (field.DataType == EavDataTypes.Composite)
        {
            using var nestedDoc = JsonDocument.Parse(elem.GetRawText());
            return Deserialize(nestedDoc, field.RefCompositeTypeId!.Value);
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
