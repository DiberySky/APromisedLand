namespace TreeGraph.Blazor.Shared.SpaceSky;

/// <summary>
/// 空间（Space）DTO，对齐后端 Space 实体（spaces 表）。
/// </summary>
public class SpaceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
