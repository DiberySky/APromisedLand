namespace TreeGraph.Api.NodeEavSky.Entities;

/// <summary>计量单位（按分类组织，每个分类一个基准单位）</summary>
public class Unit
{
    public Guid Id { get; set; }

    /// <summary>分类：length / weight / volume ...</summary>
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";

    /// <summary>换算到分类基准单位的系数</summary>
    public decimal ToBaseFactor { get; set; }

    /// <summary>是否为该分类的基准单位</summary>
    public bool IsBaseUnit { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
