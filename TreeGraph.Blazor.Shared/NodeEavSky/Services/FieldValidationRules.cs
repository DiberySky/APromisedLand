using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TreeGraph.Blazor.Shared.NodeEavSky.Services;

/// <summary>
/// 字段校验规则引擎（纯静态，无状态）。
///
/// 被 EavFieldValidator 与 CustomTableEditor 共用，
/// 保证顶层属性 / 组合字段 / 自定义表列的规则语义一致。
/// </summary>
public static class FieldValidationRules
{
    /// <summary>数值 min/max。rule 为空或非对象 → 返回空列表。</summary>
    public static List<string> ValidateNumeric(decimal value, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("min", out var min) && TryGetDecimal(min, out var minVal)
            && value < minVal)
            errors.Add($"不能小于 {minVal}");

        if (r.TryGetProperty("max", out var max) && TryGetDecimal(max, out var maxVal)
            && value > maxVal)
            errors.Add($"不能大于 {maxVal}");

        return errors;
    }

    /// <summary>字符串：AllowedValues + minLength / maxLength / regex。</summary>
    public static List<string> ValidateString(
        string s, JsonElement? rule, JsonElement? allowedValues)
    {
        var errors = new List<string>();

        // AllowedValues 优先
        if (allowedValues is JsonElement av && av.ValueKind == JsonValueKind.Array)
        {
            var allowed = av.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToList();

            if (allowed.Count > 0 && !allowed.Contains(s))
                errors.Add($"值必须是以下之一：{string.Join("、", allowed)}");
        }

        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minLength", out var minL) && minL.TryGetInt32(out var minLen)
            && s.Length < minLen)
            errors.Add($"长度不能少于 {minLen} 个字符");

        if (r.TryGetProperty("maxLength", out var maxL) && maxL.TryGetInt32(out var maxLen)
            && s.Length > maxLen)
            errors.Add($"长度不能超过 {maxLen} 个字符");

        if (r.TryGetProperty("regex", out var rx) && rx.ValueKind == JsonValueKind.String)
        {
            var pattern = rx.GetString();
            if (!string.IsNullOrEmpty(pattern))
            {
                try
                {
                    if (!Regex.IsMatch(s, pattern))
                        errors.Add(r.TryGetProperty("message", out var m)
                            && m.ValueKind == JsonValueKind.String
                            ? m.GetString()!
                            : "格式不正确");
                }
                catch (ArgumentException) { /* 非法正则忽略 */ }
            }
        }

        return errors;
    }

    /// <summary>日期 minDate / maxDate。</summary>
    public static List<string> ValidateDate(DateOnly d, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minDate", out var minD)
            && minD.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(minD.GetString(), out var minDate) && d < minDate)
            errors.Add($"日期不能早于 {minDate:yyyy-MM-dd}");

        if (r.TryGetProperty("maxDate", out var maxD)
            && maxD.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(maxD.GetString(), out var maxDate) && d > maxDate)
            errors.Add($"日期不能晚于 {maxDate:yyyy-MM-dd}");

        return errors;
    }

    /// <summary>时间 minTime / maxTime。</summary>
    public static List<string> ValidateTime(TimeOnly t, JsonElement? rule)
    {
        var errors = new List<string>();
        if (rule is not JsonElement r || r.ValueKind != JsonValueKind.Object)
            return errors;

        if (r.TryGetProperty("minTime", out var minT)
            && minT.ValueKind == JsonValueKind.String
            && TimeOnly.TryParse(minT.GetString(), out var minTime) && t < minTime)
            errors.Add($"时间不能早于 {minTime:HH:mm:ss}");

        if (r.TryGetProperty("maxTime", out var maxT)
            && maxT.ValueKind == JsonValueKind.String
            && TimeOnly.TryParse(maxT.GetString(), out var maxTime) && t > maxTime)
            errors.Add($"时间不能晚于 {maxTime:HH:mm:ss}");

        return errors;
    }

    public static bool TryGetDecimal(JsonElement elem, out decimal value)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDecimal(out value))
            return true;
        if (elem.ValueKind == JsonValueKind.String)
            return decimal.TryParse(elem.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        value = 0;
        return false;
    }
}
