using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

public interface IEavFieldValidator
{
    /// <summary>校验属性值，返回结构化错误（Path 相对该属性）。</summary>
    IReadOnlyList<FieldValidationError> Validate(AttributeSchemaDto attr, object? value);

    /// <summary>仅校验 JSON 文本格式，用于 json / file / 自定义表行输入框。</summary>
    string? ValidateJsonText(string? text);
}

public class EavFieldValidator : IEavFieldValidator
{
    // ============================================================
    // 入口
    // ============================================================

    public IReadOnlyList<FieldValidationError> Validate(
        AttributeSchemaDto attr, object? value)
    {
        var errors = new List<FieldValidationError>();
        ValidateAttribute(attr, value, "", errors);
        return errors;
    }

    public string? ValidateJsonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return null;
        }
        catch (JsonException ex)
        {
            return $"JSON 格式错误：{ex.Message}";
        }
    }

    // ============================================================
    // 属性级
    // ============================================================

    private void ValidateAttribute(
        AttributeSchemaDto attr, object? value, string path,
        List<FieldValidationError> errors)
    {
        // 必填（单选有默认值时放行）
        if (attr.IsRequired && IsEmpty(value))
        {
            if (!HasDefaultOption(attr.DataType, attr.OptionSet?.Items))
            {
                errors.Add(new FieldValidationError(path, "必填字段"));
                return;
            }
        }

        if (value is null || IsEmpty(value)) return;

        switch (attr.DataType)
        {
            case "string":
                ValidateStringRules(
                    value.ToString() ?? "", attr.ValidationRule,
                    attr.AllowedValues, path, errors);
                break;

            case "int":
            case "decimal":
                ValidateNumericAttribute(value, attr, path, errors);
                break;

            case "date":
                if (value is string ds && DateOnly.TryParse(ds, out var d))
                    ValidateDateRules(d, attr.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "日期格式错误（应为 yyyy-MM-dd）"));
                break;

            case "time":
                if (value is string ts && TimeOnly.TryParse(ts, out var t))
                    ValidateTimeRules(t, attr.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "时间格式错误（应为 HH:mm:ss）"));
                break;

            case "single_choice":
                if (attr.OptionSet is null)
                {
                    errors.Add(new FieldValidationError(path, "属性未绑定选项集"));
                }
                else
                {
                    var v = value.ToString() ?? "";
                    if (attr.OptionSet.Items.All(i => i.Value != v))
                    {
                        var valid = string.Join("、",
                            attr.OptionSet.Items.Select(i => i.Value));
                        errors.Add(new FieldValidationError(
                            path, $"值 '{v}' 不在选项集中（有效值：{valid}）"));
                    }
                }
                break;

            case "composite":
                if (attr.CompositeType is not null)
                    ValidateComposite(attr.CompositeType, value, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "组合类型未定义"));
                break;

            case "json":
            case "file":
                // 由控件层用 ValidateJsonText 保证文本合法性；这里只做非空检查
                // （已在上方必填检查处理）。若 value 是原始字符串，可进一步解析。
                if (value is string rawJson && !string.IsNullOrWhiteSpace(rawJson))
                {
                    var jsonErr = ValidateJsonText(rawJson);
                    if (jsonErr is not null)
                        errors.Add(new FieldValidationError(path, jsonErr));
                }
                break;

            // table 类型不走 Validate（由 CustomTableEditor 处理）
        }
    }

    /// <summary>
    /// 顶层数值属性校验。
    ///
    /// ★ int 无单位：拒绝小数（写入 ValueInt 会截断）
    /// ★ int 有单位：允许小数（归一化到基准单位可能产生小数，写 ValueDecimal）
    /// ★ 未绑定单位时不允许指定 UnitId；绑定单位时 UnitId 必须在可用范围内
    /// </summary>
    private void ValidateNumericAttribute(
        object value, AttributeSchemaDto attr, string path,
        List<FieldValidationError> errors)
    {
        // 单位归属
        if (value is NumericInput ni)
        {
            if (attr.Unit is null && ni.UnitId is not null)
            {
                errors.Add(new FieldValidationError(
                    path, "该属性未绑定基准单位，不允许指定单位"));
                return;
            }

            if (attr.Unit is not null && ni.UnitId is { } uid
                && attr.AvailableUnits?.All(u => u.Id != uid) == true)
            {
                errors.Add(new FieldValidationError(
                    path, "指定的单位不属于该属性的可用单位"));
                return;
            }
        }

        var d = ToDecimal(value);
        if (d is null)
        {
            errors.Add(new FieldValidationError(path, "数值格式错误"));
            return;
        }

        // ★ int 无单位时才拒绝小数
        if (attr.DataType == "int"
            && attr.Unit is null
            && d.Value != Math.Truncate(d.Value))
        {
            errors.Add(new FieldValidationError(path, "int 类型（无单位）不接受小数"));
            return;
        }

        ValidateNumericRules(d.Value, attr.ValidationRule, path, errors);
    }

    // ============================================================
    // 组合类型递归（含数组）
    // ============================================================

    private void ValidateComposite(
        CompositeTypeSchemaDto type, object? value, string basePath,
        List<FieldValidationError> errors)
    {
        // ★ 修复 P0-1（组合结构非法时静默通过 → 现在显式报错）
        if (value is not IReadOnlyDictionary<string, object?> dict)
        {
            errors.Add(new FieldValidationError(
                basePath, "组合值期望字典结构"));
            return;
        }

        foreach (var field in type.Fields)
        {
            dict.TryGetValue(field.FieldName, out var fieldValue);
            var fieldPath = JoinPath(basePath, field.FieldName);

            // 数组字段
            if (field.IsArray)
            {
                ValidateArrayField(field, fieldValue, fieldPath, errors);
                continue;
            }

            // 必填：single_choice 有默认值时放行（★ 修复 P0-4）
            if (field.IsRequired && IsEmpty(fieldValue))
            {
                if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                    errors.Add(new FieldValidationError(fieldPath, "必填字段"));
                continue;
            }
            if (fieldValue is null || IsEmpty(fieldValue)) continue;

            // 嵌套组合
            if (field.DataType == "composite" && field.NestedType is not null)
            {
                ValidateComposite(field.NestedType, fieldValue, fieldPath, errors);
                continue;
            }

            // 叶子字段
            ValidateLeafField(field, fieldValue, fieldPath, errors);
        }
    }

    private void ValidateArrayField(
        CompositeFieldSchemaDto field, object? value, string path,
        List<FieldValidationError> errors)
    {
        if (field.IsRequired && IsEmpty(value))
        {
            if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                errors.Add(new FieldValidationError(path, "必填字段"));
            return;
        }
        if (value is null) return;

        // 期望 IEnumerable（排除 string）
        if (value is not IEnumerable items || value is string)
        {
            errors.Add(new FieldValidationError(path, "期望数组"));
            return;
        }

        int i = 0;
        foreach (var item in items)
        {
            var itemPath = $"{path}[{i}]";

            if (field.IsRequired && IsEmpty(item))
            {
                if (!HasDefaultOption(field.DataType, field.OptionSet?.Items))
                    errors.Add(new FieldValidationError(itemPath, "必填字段"));
            }
            else if (!IsEmpty(item))
            {
                if (field.DataType == "composite" && field.NestedType is not null)
                    ValidateComposite(field.NestedType, item, itemPath, errors);
                else
                    ValidateLeafField(field, item, itemPath, errors);
            }
            i++;
        }
    }

    /// <summary>
    /// 组合内叶子字段校验：value 保证非空。
    ///
    /// ★ int 无单位：拒绝小数（写入 ValueInt 会截断）
    /// ★ int 有单位：允许小数（归一化到基准单位可能产生小数，写 ValueDecimal）
    /// ★ 未绑定单位时不允许指定 UnitId；绑定单位时 UnitId 必须在可用范围内
    /// </summary>
    private void ValidateLeafField(
        CompositeFieldSchemaDto field, object value, string path,
        List<FieldValidationError> errors)
    {
        switch (field.DataType)
        {
            case "string":
                ValidateStringRules(
                    value.ToString() ?? "", field.ValidationRule,
                    field.AllowedValues, path, errors);
                break;

            case "int":
            case "decimal":
            {
                // 组合内 decimal 带单位（NumericInput 负载）
                if (value is NumericInput ni)
                {
                    if (field.Unit is null && ni.UnitId is not null)
                    {
                        errors.Add(new FieldValidationError(
                            path, "该字段未绑定基准单位，不允许指定单位"));
                        break;
                    }

                    if (field.Unit is not null && ni.UnitId is { } uid
                        && field.AvailableUnits?.All(u => u.Id != uid) == true)
                    {
                        errors.Add(new FieldValidationError(
                            path, "指定的单位不属于该字段的可用单位"));
                        break;
                    }

                    // ★ int 无单位时才拒绝小数
                    if (field.DataType == "int"
                        && field.Unit is null
                        && ni.Value != Math.Truncate(ni.Value))
                    {
                        errors.Add(new FieldValidationError(
                            path, "int 类型（无单位）不接受小数"));
                        break;
                    }

                    // 范围校验：前端不做单位归一化，按原始输入值近似
                    // （严格的归一化范围校验由后端负责）
                    if (field.ValidationRule is not null)
                        ValidateNumericRules(ni.Value, field.ValidationRule, path, errors);
                    break;
                }

                // 裸数值路径
                var num = ToDecimal(value);
                if (num is null)
                {
                    errors.Add(new FieldValidationError(path, "数值格式错误"));
                    break;
                }

                // ★ int 无单位时才拒绝小数
                if (field.DataType == "int"
                    && field.Unit is null
                    && num.Value != Math.Truncate(num.Value))
                {
                    errors.Add(new FieldValidationError(
                        path, "int 类型（无单位）不接受小数"));
                    break;
                }

                ValidateNumericRules(num.Value, field.ValidationRule, path, errors);
                break;
            }

            case "date":
                if (value is string ds && DateOnly.TryParse(ds, out var d))
                    ValidateDateRules(d, field.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "日期格式错误（应为 yyyy-MM-dd）"));
                break;

            case "time":
                if (value is string ts && TimeOnly.TryParse(ts, out var t))
                    ValidateTimeRules(t, field.ValidationRule, path, errors);
                else
                    errors.Add(new FieldValidationError(
                        path, "时间格式错误（应为 HH:mm:ss）"));
                break;

            case "single_choice":
                // 优先用选项集校验，无则回退到 AllowedValues
                if (field.OptionSet is not null)
                {
                    var v = value.ToString() ?? "";
                    if (field.OptionSet.Items.All(i => i.Value != v))
                    {
                        var valid = string.Join("、",
                            field.OptionSet.Items.Select(i => i.Value));
                        errors.Add(new FieldValidationError(
                            path, $"值 '{v}' 不在选项集中（有效值：{valid}）"));
                    }
                }
                else if (field.AllowedValues is JsonElement av
                         && av.ValueKind == JsonValueKind.Array)
                {
                    var allowed = av.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!)
                        .ToList();

                    if (allowed.Count > 0)
                    {
                        var v = value.ToString() ?? "";
                        if (!allowed.Contains(v))
                            errors.Add(new FieldValidationError(
                                path, $"值必须是以下之一：{string.Join("、", allowed)}"));
                    }
                }
                break;

            case "json":
            case "file":
                if (value is string rawJson && !string.IsNullOrWhiteSpace(rawJson))
                {
                    var jsonErr = ValidateJsonText(rawJson);
                    if (jsonErr is not null)
                        errors.Add(new FieldValidationError(path, jsonErr));
                }
                break;
        }
    }

    // ============================================================
    // 规则方法
    // ============================================================

    private static void ValidateStringRules(
        string s, JsonElement? validationRule, JsonElement? allowedValues,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateString(s, validationRule, allowedValues))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateNumericRules(
        decimal value, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateNumeric(value, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateDateRules(
        DateOnly d, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateDate(d, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    private static void ValidateTimeRules(
        TimeOnly t, JsonElement? validationRule,
        string path, List<FieldValidationError> errors)
    {
        foreach (var msg in FieldValidationRules.ValidateTime(t, validationRule))
            errors.Add(new FieldValidationError(path, msg));
    }

    // ============================================================
    // 工具
    // ============================================================

    private static string JoinPath(string basePath, string name)
        => string.IsNullOrEmpty(basePath) ? name : $"{basePath}.{name}";

    private static bool IsEmpty(object? v) => v switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        // 数组（List<object?> / object?[]）无元素视为空
        ICollection c when c.Count == 0 => true,
        _ => false
    };

    /// <summary>
    /// ★ 修复 P0-4 的辅助：判断 single_choice 是否含默认选项（有默认值时必填放行）。
    /// 与后端 EavValidationService.ValidateSingleChoice 语义一致。
    /// </summary>
    private static bool HasDefaultOption(
        string dataType, IReadOnlyList<OptionItemSchemaDto>? items)
        => dataType == "single_choice"
        && items is { Count: > 0 }
        && items.Any(i => i.IsDefault);

    private static decimal? ToDecimal(object? v) => v switch
    {
        decimal d => d,
        long l => l,
        int i => i,
        double db => (decimal)db,
        NumericInput ni => ni.Value,
        string s when decimal.TryParse(s, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };
}