namespace APromisedLand.Shared.TreeGraph.Models;

/// <summary>
/// 树查询分页参数。前后端共享,Controller 接收 [FromQuery] 时由 ASP.NET Core 自动绑定。
/// </summary>
public class TreeQueryOptions
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
}
