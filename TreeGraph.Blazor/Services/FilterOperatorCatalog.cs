using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// 动态查询运算符的元数据。
///
/// 职责：
///   1) 按属性类型列出可用运算符
///   2) 提供运算符的显示名
///   3) 标记运算符是否需要第二个值（between）或多个值（in/nin）
///
/// 与后端 EavQueryService 的运算符支持严格对应，见：
///   TreeGraph.Api/Services/EavQueryService.cs
/// </summary>
public static class FilterOperatorCatalog
{
    public sealed record OperatorInfo(
        string Code,
        string DisplayName,
        bool NeedsValue2 = false,
        bool IsMultiValue = false);

    private static readonly OperatorInfo[] _numeric = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("gt", "大于"),
        new OperatorInfo("gte", "大于等于"),
        new OperatorInfo("lt", "小于"),
        new OperatorInfo("lte", "小于等于"),
        new OperatorInfo("between", "区间", NeedsValue2: true),
        new OperatorInfo("in", "包含于", IsMultiValue: true)
    };

    private static readonly OperatorInfo[] _string = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("like", "包含"),
        new OperatorInfo("startswith", "开头是"),
        new OperatorInfo("endswith", "结尾是"),
        new OperatorInfo("in", "包含于", IsMultiValue: true)
    };

    private static readonly OperatorInfo[] _bool = new[]
    {
        new OperatorInfo("eq", "等于")
    };

    private static readonly OperatorInfo[] _datetime = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("gt", "晚于"),
        new OperatorInfo("gte", "不早于"),
        new OperatorInfo("lt", "早于"),
        new OperatorInfo("lte", "不晚于"),
        new OperatorInfo("between", "区间", NeedsValue2: true)
    };

    private static readonly OperatorInfo[] _date = _datetime;
    private static readonly OperatorInfo[] _time = _datetime;

    private static readonly OperatorInfo[] _singleChoice = new[]
    {
        new OperatorInfo("eq", "等于"),
        new OperatorInfo("neq", "不等于"),
        new OperatorInfo("in", "包含于", IsMultiValue: true),
        new OperatorInfo("nin", "不包含于", IsMultiValue: true)
    };

    /// <summary>属性的基准类型（忽略 unit 与 IsMultiValue）。</summary>
    public static string BaseKind(string dataType) => dataType switch
    {
        "int" or "decimal" => "numeric",
        "string" => "string",
        "bool" => "bool",
        "datetime" => "datetime",
        "date" => "date",
        "time" => "time",
        "single_choice" => "single_choice",
        _ => "unsupported"
    };

    /// <summary>按属性返回可用运算符列表。</summary>
    public static IReadOnlyList<OperatorInfo> For(AttributeSchemaDto attr) => attr.DataType switch
    {
        "int" or "decimal" => _numeric,
        "string" => _string,
        "bool" => _bool,
        "datetime" => _datetime,
        "date" => _date,
        "time" => _time,
        "single_choice" => _singleChoice,
        _ => Array.Empty<OperatorInfo>()
    };

    /// <summary>该属性是否支持动态查询。</summary>
    public static bool IsSupported(AttributeSchemaDto attr) =>
        attr.DataType is "int" or "decimal" or "string" or "bool"
            or "datetime" or "date" or "time" or "single_choice";

    /// <summary>根据 Code 取运算符元数据。</summary>
    public static OperatorInfo? Get(AttributeSchemaDto attr, string code)
        => For(attr).FirstOrDefault(o => o.Code == code);
}
