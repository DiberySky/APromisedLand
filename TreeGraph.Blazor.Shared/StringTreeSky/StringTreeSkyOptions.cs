namespace TreeGraph.Blazor.Shared.StringTreeSky;

public class StringTreeSkyOptions
{
    /// <summary>API 基础路径，与后端控制器 [Route] 前缀一致。</summary>
    public string BasePath { get; set; } = "api/string-tree";

    /// <summary>默认展开层级（0 = 不展开）。</summary>
    public int DefaultExpandLevel { get; set; } = 1;

    public bool AllowRename { get; set; } = true;
    public bool AllowDelete { get; set; } = true;
    public bool AllowCreate { get; set; } = true;
}
