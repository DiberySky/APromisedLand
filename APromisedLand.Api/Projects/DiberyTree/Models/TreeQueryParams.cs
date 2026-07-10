namespace APromisedLand.Api.Projects.DiberyTree.Models;

/// <summary>
/// 树节点查询参数
/// </summary>
public class TreeQueryParams
{
    /// <summary>
    /// 父节点ID（根节点传 null 或空字符串）
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 搜索关键词（可选）
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// 是否只加载有子节点的节点
    /// </summary>
    public bool OnlyWithChildren { get; set; }

    /// <summary>
    /// 最大深度（0表示不限制）
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// 分页参数 - 页码
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 分页参数 - 每页大小
    /// </summary>
    public int PageSize { get; set; } = 100;
}
