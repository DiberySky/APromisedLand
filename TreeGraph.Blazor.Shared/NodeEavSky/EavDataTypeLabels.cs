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
        EavDataTypes.PropertyTree => "属性树",
        _ => dataType ?? ""
    };

    /// <summary>
    /// 按宿主位置生成类型下拉选项（过滤与顺序均派生自 EavDataTypes.Catalog）。
    /// 容器不进容器：composite / table 仅 TopLevel；file / location 仅 TopLevel；
    /// single_choice 不进 TableColumn。
    /// </summary>
    public static IReadOnlyList<(string Value, string Label)> OptionsFor(EavHost host)
        => EavDataTypes.Catalog.Values
            .Where(t => t.Hosts.HasFlag(host))
            .Select(t => (t.Value, GetLabel(t.Value)))
            .ToArray();
}
