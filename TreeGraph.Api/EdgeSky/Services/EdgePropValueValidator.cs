using System.Globalization;
using System.Text.Json;
using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// 边属性值校验器：按 EdgeTypeProp.DataType 校验 jsonb 值字面量（实例值与 Schema 默认值共用）。
/// TIMESTAMP 接受 int64 epoch 或 ISO 8601 字符串；DATE 接受 "yyyy-MM-dd" 字符串。
/// </summary>
public static class EdgePropValueValidator
{
    /// <summary>校验值字面量；合法返回 null，否则返回错误说明。</summary>
    public static string? Validate(string dataType, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null; // null 的许可由 Nullable 约束判定，此处只管类型

        switch (dataType)
        {
            case EdgePropDataTypes.String:
                return value.ValueKind == JsonValueKind.String ? null : "期望 STRING（json 字符串）";

            case EdgePropDataTypes.Int:
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)
                    ? null
                    : "期望 INT（int64 整数）";

            case EdgePropDataTypes.Double:
                return value.ValueKind == JsonValueKind.Number ? null : "期望 DOUBLE（数值）";

            case EdgePropDataTypes.Bool:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? null
                    : "期望 BOOL（true/false）";

            case EdgePropDataTypes.Timestamp:
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _))
                    return null;
                if (value.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out _))
                    return null;
                return "期望 TIMESTAMP（int64 epoch 或 ISO 8601 字符串）";

            case EdgePropDataTypes.Date:
                return value.ValueKind == JsonValueKind.String
                       && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd",
                           CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? null
                    : "期望 DATE（\"yyyy-MM-dd\" 字符串）";

            default:
                return $"未知数据类型 '{dataType}'";
        }
    }
}
