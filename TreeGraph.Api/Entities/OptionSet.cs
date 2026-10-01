namespace TreeGraph.Api.Entities;

/// <summary>选项集（一组互斥的单选值，可跨实体类型共享）</summary>
public class OptionSet
{
    public long OptionSetId { get; set; }
    public string EntityType { get; set; } = "";      // "Shared" 表示全局共享
    public string SetName { get; set; } = "";         // "gender"、"status"
    public string DisplayName { get; set; } = "";     // "性别"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<OptionItem> Items { get; set; } = new();
}

/// <summary>选项项：Value 是存储的实际值，Label 是展示名</summary>
public class OptionItem
{
    public long OptionItemId { get; set; }
    public long OptionSetId { get; set; }
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public int DisplayOrder { get; set; }

    /// <summary>是否默认选项（同集内业务代码保证只有一个 true）</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软删除（历史数据仍存有旧 Value，删除后读取端降级显示 Value）</summary>
    public bool IsDeleted { get; set; }

    public OptionSet OptionSet { get; set; } = null!;
}
