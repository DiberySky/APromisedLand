namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>
/// 位置属性值表（location 类型独立存储）：一条记录 = 某实体某位置属性的一个坐标点。
///
/// 与 <see cref="AttributeValue"/> 的类型化列模式分离，因为经纬度是成对的强结构数据，
/// 供地图导航按坐标检索 / 跳转。一个位置属性在单个实体上最多一个坐标点
/// （唯一索引 uq_lav_entity_attr 保证）。
/// </summary>
public class LocationAttributeValue
{
    /// <summary>值 Id（空字符串触发 DB DEFAULT gen_random_uuid()，HasSentinel 语义）。</summary>
    public string ValueId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string AttributeId { get; set; } = "";

    /// <summary>纬度（GCJ-02）。</summary>
    public double Latitude { get; set; }

    /// <summary>经度（GCJ-02）。</summary>
    public double Longitude { get; set; }

    /// <summary>地点名称（导航终点 / 地图标记标题）。</summary>
    public string? Label { get; set; }

    /// <summary>地址文本。</summary>
    public string? Address { get; set; }

    /// <summary>坐标系标识，当前固定 gcj02。</summary>
    public string CoordinateSystem { get; set; } = "gcj02";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AttributeDefinition Attribute { get; set; } = null!;
}
