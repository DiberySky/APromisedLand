namespace TreeGraph.Api.SpaceSky.Entities;

/// <summary>
/// 空间类型。
/// </summary>
public enum SpaceType
{
    /// <summary>动态</summary>
    Dynamic = 0,

    /// <summary>类型</summary>
    Type = 1,

    /// <summary>单位</summary>
    Unit = 2,

    /// <summary>知识库</summary>
    Knowledge = 3,
}

/// <summary>
/// 空间（Space）：独立简单实体，spaces 表。
/// </summary>
public class Space
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>空间类型：动态 / 类型 / 单位 / 知识库。</summary>
    public SpaceType Type { get; set; } = SpaceType.Dynamic;

    /// <summary>EF Core 种子数据：四个空间。</summary>
    public static List<Space> SeedData() => new()
    {
        new() { Id = "7a1e3f5d-9b2c-4a8e-1f0d-3c5b7e9a1c2d", Name = "武汉供电段", Description = "武汉供电段", Type = SpaceType.Dynamic },
        new() { Id = "8b2f4a6e-0c3d-5b9f-2a1e-4d6c8f0b2d3e", Name = "类型", Description = "类型", Type = SpaceType.Type },
        new() { Id = "9c3a5b7f-1d4e-6c0a-3b2f-5e7d9a1c3e4f", Name = "单位", Description = "单位", Type = SpaceType.Unit },
        new() { Id = "ad5e6c8f-2b3d-4a7e-9c0f-1d2b3e4f5a6b", Name = "知识库", Description = "知识库", Type = SpaceType.Knowledge },
    };
}
