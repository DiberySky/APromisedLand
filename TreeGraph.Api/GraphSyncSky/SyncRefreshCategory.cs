namespace TreeGraph.Api.GraphSyncSky;

/// <summary>
/// 同步刷新数据类别（同步刷新对话框按类别分步调度）。
/// space = 空间树；tree = 单位/分类/知识树；line = 线路 LineNode；edge = 语义边。
/// </summary>
public enum SyncCategory
{
    Space,
    Tree,
    Line,
    Edge,
}

/// <summary>SyncCategory 字符串解析（路由/查询参数用，忽略大小写）。</summary>
public static class SyncCategoryParser
{
    public static bool TryParse(string? value, out SyncCategory category)
    {
        category = default;
        return Enum.TryParse(value?.Trim(), ignoreCase: true, out category)
               && Enum.IsDefined(category);
    }
}
