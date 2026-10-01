namespace TreeGraphApi.Entities;

/// <summary>
/// 树节点实体(数据库模型)。
/// 采用邻接表(ParentId) + 物化路径(string Path)混合存储。
/// 不使用 PostgreSQL ltree 扩展,改用标准 SQL(递归 CTE + LIKE)实现树形查询,便于跨数据库迁移与维护。
/// </summary>
public class TreeNodeEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string NodeType { get; set; } = "default";
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }

    // 优化 #8:DateTimeOffset 替代 DateTime,消除时区歧义
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>乐观并发控制列,由 PostgreSQL xmin 系统列映射</summary>
    public uint RowVersion { get; set; }

    /// <summary>
    /// 物化路径:从根到当前节点的 Id 序列,以 '.' 分隔。例如 "rootId.parentId.thisId"。
    /// 不使用 ltree 类型,纯字符串,查询走 LIKE 或递归 CTE。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    // 导航属性
    public TreeNodeEntity? Parent { get; set; }
    public ICollection<TreeNodeEntity> Children { get; set; } = new List<TreeNodeEntity>();
}
