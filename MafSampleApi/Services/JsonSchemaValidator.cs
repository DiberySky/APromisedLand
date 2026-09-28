using System.Text.Json;

namespace MafSampleApi.Services;

/// <summary>
/// 极简 JSON Schema 校验器。覆盖常见约束：
///   type / required / properties / items / enum
/// 足够用在"模型输出后置校验"场景。
/// 如需完整 JSON Schema 支持，可换成 JsonSchema.Net。
/// </summary>
public static class JsonSchemaValidator
{
    /// <summary>校验 json 文本是否符合 schemaText；不合规时输出可读错误。</summary>
    public static bool TryValidate(string json, string schemaText, out string? error)
    {
        try
        {
            using var jsonDoc   = JsonDocument.Parse(json);
            using var schemaDoc = JsonDocument.Parse(schemaText);
            error = ValidateNode(jsonDoc.RootElement, schemaDoc.RootElement, "$");
            return error is null;
        }
        catch (JsonException ex)
        {
            error = $"JSON 解析失败: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Schema 解析失败: {ex.Message}";
            return false;
        }
    }

    private static string? ValidateNode(JsonElement value, JsonElement schema, string path)
    {
        // type
        if (schema.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            var t = typeEl.GetString();
            bool ok = t switch
            {
                "object"  => value.ValueKind == JsonValueKind.Object,
                "array"   => value.ValueKind == JsonValueKind.Array,
                "string"  => value.ValueKind == JsonValueKind.String,
                "number"  => value.ValueKind == JsonValueKind.Number,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null"    => value.ValueKind == JsonValueKind.Null,
                _         => true,
            };
            if (!ok)
                return $"{path}: 类型不符，期望 {t}，实际 {value.ValueKind}";
        }

        // enum
        if (schema.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
        {
            bool found = false;
            foreach (var e in enumEl.EnumerateArray())
            {
                if (JsonElement.DeepEquals(e, value)) { found = true; break; }
            }
            if (!found)
                return $"{path}: 值不在 enum 允许集合内";
        }

        // 对象：required + properties
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in reqEl.EnumerateArray())
                {
                    var key = r.GetString();
                    if (key is not null && !value.TryGetProperty(key, out _))
                        return $"{path}: 缺少必需字段 '{key}'";
                }
            }

            if (schema.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in propsEl.EnumerateObject())
                {
                    if (value.TryGetProperty(prop.Name, out var child))
                    {
                        var err = ValidateNode(child, prop.Value, $"{path}.{prop.Name}");
                        if (err is not null) return err;
                    }
                }
            }
        }

        // 数组：items
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var itemsEl))
        {
            int i = 0;
            foreach (var item in value.EnumerateArray())
            {
                var err = ValidateNode(item, itemsEl, $"{path}[{i}]");
                if (err is not null) return err;
                i++;
            }
        }

        return null;
    }
}