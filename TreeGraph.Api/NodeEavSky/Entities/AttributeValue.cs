using System.Text.Json;

namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>类型化值表：每种基础类型独立存储列</summary>
public class AttributeValue
{
    public string ValueId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string AttributeId { get; set; } = "";

    // 类型化值列
    public string? ValueString { get; set; }
    public long? ValueInt { get; set; }
    public decimal? ValueDecimal { get; set; }
    public bool? ValueBool { get; set; }
    public DateTimeOffset? ValueDatetime { get; set; }
    public DateOnly? ValueDateOnly { get; set; }
    public TimeOnly? ValueTime { get; set; }
    public JsonDocument? ValueFileMeta { get; set; }
    public JsonDocument? ValueJsonb { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>输入时绑定的单位（指向 unit_trees 节点 Id，仅作记录不参与换算）</summary>
    public string? UnitId { get; set; }
    public TreeGraph.Shared.TreeEavSky.Entities.UnitTree? Unit { get; set; }

    public AttributeDefinition Attribute { get; set; } = null!;
}
