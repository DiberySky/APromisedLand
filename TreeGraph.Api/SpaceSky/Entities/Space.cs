namespace TreeGraph.Api.SpaceSky.Entities;

/// <summary>
/// 空间（Space）：独立简单实体，spaces 表。
/// </summary>
public class Space
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
