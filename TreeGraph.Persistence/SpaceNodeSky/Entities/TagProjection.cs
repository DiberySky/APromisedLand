namespace TreeGraph.Persistence.SpaceNodeSky.Entities;

/// <summary>
/// Tag 投影账本——由 NodeEavSky 组合属性派生的图 Tag 物化记录。
///
/// 一个记录 = 某个空间内一个 Tag 的物化 schema 与版本；Tag 本身不是可手工维护的实体，
/// 其来源是 <see cref="SourceAttributeId"/> 指向的组合属性定义。
/// 删除采用墓碑（<see cref="IsDeleted"/>）：图侧 DROP 成功前保留记录，供启动对齐补偿。
/// </summary>
public class TagProjection
{
    /// <summary>归属图空间。</summary>
    public string SpaceId { get; set; } = "";

    /// <summary>图侧 Tag 名（由实体类型与属性名派生，创建后不可改）。</summary>
    public string TagName { get; set; } = "";

    /// <summary>来源属性定义 ID（NodeEavSky AttributeDefinition）。</summary>
    public string SourceAttributeId { get; set; } = "";

    /// <summary>来源实体类型名。</summary>
    public string SourceEntityType { get; set; } = "";

    /// <summary>已物化的字段与类型快照（JSON，用于与 Nebula DESCRIBE 对齐）。</summary>
    public string SchemaSnapshot { get; set; } = "{}";

    /// <summary>版本号：每次创建/修改/删除递增，用于消费者条件收敛。</summary>
    public int Version { get; set; } = 1;

    /// <summary>墓碑标记：图侧 DROP 成功前保持 true。</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
