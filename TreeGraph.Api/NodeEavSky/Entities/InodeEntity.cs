namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// iNode ↔ EAV 实体的「命名绑定」。
///
/// 语义：绑定本身是一等对象，拥有自己的名字与一条<b>私有</b> EAV 数据，
/// 因此同一 iNode 同一类型可以建立多条互不干扰的绑定。
///
/// 约束：
///   - PK: BindingId（GUID 字符串）
///   - UNIQUE (InodeId, EntityType, Name)：同一 iNode 同一类型下绑定名唯一
///   - UNIQUE (EntityType, EntityId)：一条 EAV 数据只属于一条绑定
///
/// EntityId 直接复用现有 EAV 的 entity_id
/// （即 attribute_values.entity_id 指向的值）。
/// </summary>
public class InodeEntity
{
    /// <summary>绑定 ID（GUID 字符串），绑定行主键</summary>
    public string BindingId { get; set; } = "";

    /// <summary>绑定名（用户输入，同一 iNode 同一类型下唯一）</summary>
    public string Name { get; set; } = "";

    /// <summary>iNode ID（GUID 字符串）</summary>
    public string InodeId { get; set; } = "";

    /// <summary>实体类型名（如 "UnitTree"）</summary>
    public string EntityType { get; set; } = "";

    /// <summary>该绑定私有的实体 ID（GUID 字符串）</summary>
    public string EntityId { get; set; } = "";

    /// <summary>显示顺序（同 iNode 内升序展示；新建时按现有条目数 + 1 生成）</summary>
    public int DisplayOrder { get; set; }

    public DateTimeOffset AttachedAt { get; set; } = DateTimeOffset.UtcNow;
}
