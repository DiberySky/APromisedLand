using TreeGraph.Shared.EdgeSky;

namespace TreeGraph.Blazor.Shared.EdgeSky;

/// <summary>
/// 边属性数据类型的中文显示名（仅 UI 展示；DB/DDL 仍用大写常量，见 <see cref="EdgePropDataTypes"/>）。
/// </summary>
public static class EdgePropDataTypeLabels
{
    public static string Of(string? dataType) => dataType switch
    {
        EdgePropDataTypes.String => "字符串",
        EdgePropDataTypes.Int => "整数",
        EdgePropDataTypes.Double => "浮点数",
        EdgePropDataTypes.Bool => "布尔",
        EdgePropDataTypes.Timestamp => "时间戳",
        EdgePropDataTypes.Date => "日期",
        _ => dataType ?? "—"
    };

    /// <summary>中文名 + 原常量（下拉/表格用），如 "字符串（STRING）"。</summary>
    public static string Full(string? dataType) => dataType switch
    {
        EdgePropDataTypes.String => "字符串（STRING）",
        EdgePropDataTypes.Int => "整数（INT）",
        EdgePropDataTypes.Double => "浮点数（DOUBLE）",
        EdgePropDataTypes.Bool => "布尔（BOOL）",
        EdgePropDataTypes.Timestamp => "时间戳（TIMESTAMP）",
        EdgePropDataTypes.Date => "日期（DATE）",
        _ => dataType ?? "—"
    };
}
