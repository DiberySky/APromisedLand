namespace APromisedLand.Shared.TreeGraph.Models;

/// <summary>
/// 树节点数据传输对象,默认使用 string 主键。
/// 前后端共享:前端 TreeGraph&lt;T&gt; 用作泛型 T,后端 TreeController 用作 [FromBody] 参数。
/// RowVersion 字段供后端 EF Core 乐观并发检查(映射到 PostgreSQL xmin)。
/// </summary>
public class TreeNodeDto : TreeNodeBase<TreeNodeDto, string>
{
    public string NodeType { get; set; } = "default";

    /// <summary>乐观并发标记,后端通过 xmin 自动填充并下发;前端回传以支持并发控制</summary>
    public byte[]? RowVersion { get; set; }
}
