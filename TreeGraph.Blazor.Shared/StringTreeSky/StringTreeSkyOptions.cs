
namespace TreeGraph.Blazor.Shared.StringTreeSky;

public class StringTreeSkyOptions
{
    public string BasePath { get; set; } = "api/string-tree";
    public int DefaultExpandLevel { get; set; } = 1;

    public bool AllowRename { get; set; } = true;
    public bool AllowDelete { get; set; } = true;
    public bool AllowCreate { get; set; } = true;
    public bool AllowSort { get; set; } = true;

    public List<string> SummaryAttributeNames { get; set; } = new();
    public int SummaryMaxLength { get; set; } = 60;

    // ============ 新增：属性过滤 ============

    /// <summary>是否显示属性过滤面板。默认 false，避免影响既有宿主。</summary>
    public bool AllowFilter { get; set; } = false;

    /// <summary>过滤查询单页最大返回条数（防止一次拉太多）。</summary>
    public int FilterPageSize { get; set; } = 500;
}
