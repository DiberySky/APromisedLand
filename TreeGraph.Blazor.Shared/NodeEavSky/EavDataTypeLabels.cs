using TreeGraph.Shared.NodeEavSky;

namespace TreeGraph.Blazor.Shared.NodeEavSky;

/// <summary>EAV 数据类型内部标识 → 中文显示名（仅用于 UI 展示）。</summary>
public static class EavDataTypeLabels
{
    public static string GetLabel(string? dataType) => dataType switch
    {
        EavDataTypes.String => "字符串",
        EavDataTypes.Int => "整数",
        EavDataTypes.Decimal => "小数",
        EavDataTypes.Bool => "布尔",
        EavDataTypes.Datetime => "日期时间",
        EavDataTypes.Date => "日期",
        EavDataTypes.Time => "时间",
        EavDataTypes.File => "文件",
        EavDataTypes.Json => "JSON",
        EavDataTypes.Composite => "组合类型",
        EavDataTypes.Table => "自定义表",
        EavDataTypes.SingleChoice => "单选",
        EavDataTypes.Location => "位置",
        EavDataTypes.Relation => "关系",
        _ => dataType ?? ""
    };
}
