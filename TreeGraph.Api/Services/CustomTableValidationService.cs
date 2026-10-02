using TreeGraph.Api.Entities;
using TreeGraph.Shared.Eav;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Services;

/// <summary>
/// 自定义表验证引擎：逐行按列定义验证，复用基础类型与组合类型验证器，本身不实现新的验证逻辑
/// </summary>
public class CustomTableValidationService
{
    private readonly ICustomTableCache _tableCache;
    private readonly EavValidationService _baseValidator;
    private readonly CompositeValueService _compositeValidator;

    public CustomTableValidationService(
        ICustomTableCache tableCache,
        EavValidationService baseValidator,
        CompositeValueService compositeValidator)
    {
        _tableCache = tableCache;
        _baseValidator = baseValidator;
        _compositeValidator = compositeValidator;
    }

    public ValidationResult Validate(CustomTableValue value, string tableDefinitionId)
    {
        var table = _tableCache.GetTable(tableDefinitionId);
        var errors = new List<ValidationError>();

        // 行内列唯一性校验的缓存
        var uniqueColumnValues = new Dictionary<string, HashSet<string>>();

        for (int rowIdx = 0; rowIdx < value.Rows.Count; rowIdx++)
        {
            var row = value.Rows[rowIdx];
            var rowPath = $"[{rowIdx}]";

            foreach (var col in table.Columns.OrderBy(c => c.DisplayOrder))
            {
                row.Fields.TryGetValue(col.ColumnName, out var rawValue);

                // 必填
                if (col.IsRequired && rawValue is null)
                {
                    errors.Add(new($"{rowPath}.{col.ColumnName}", "必填字段"));
                    continue;
                }
                if (rawValue is null) continue;

                // 类型验证（复用基础类型验证器）
                if (col.DataType == EavDataTypes.Composite)
                {
                    if (rawValue is not DynamicCompositeValue cv)
                    {
                        errors.Add(new($"{rowPath}.{col.ColumnName}", "期望组合值"));
                        continue;
                    }
                    var result = _compositeValidator.Validate(cv, col.RefCompositeTypeId!);
                    foreach (var e in result.Errors)
                        errors.Add(new($"{rowPath}.{col.ColumnName}.{e.Field}", e.Message));
                }
                else
                {
                    // 构造临时 AttributeDefinition 复用基础验证器
                    var tempDef = new AttributeDefinition
                    {
                        AttributeName = col.ColumnName,
                        DataType = col.DataType,
                        IsRequired = col.IsRequired,
                        ValidationRule = col.ValidationRule,
                        AllowedValues = col.AllowedValues
                    };
                    var result = _baseValidator.ValidateBaseValue(tempDef, rawValue);
                    errors.AddRange(result.Errors.Select(e =>
                        new ValidationError($"{rowPath}.{e.Field}", e.Message)));
                }

                // 列内唯一性
                if (col.IsUnique)
                {
                    var key = rawValue.ToString() ?? "";
                    if (!uniqueColumnValues.TryGetValue(col.ColumnName, out var set))
                    {
                        set = new HashSet<string>();
                        uniqueColumnValues[col.ColumnName] = set;
                    }
                    if (!set.Add(key))
                    {
                        errors.Add(new($"{rowPath}.{col.ColumnName}",
                            $"值 '{key}' 在列中重复"));
                    }
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}
