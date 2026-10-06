using TreeGraph.Api.StringTreeSky.Entities;
using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Api.StringTreeSky.Mapping;

public static class StringNodeMapper
{
    public static StringNodeDto ToDto(this StringNodeEntity e, bool hasChildren = false) => new()
    {
        Id = e.Id,
        Name = e.Name,
        ParentId = e.ParentId,
        SortOrder = e.SortOrder,
        Description = e.Description,
        EntityType = string.IsNullOrEmpty(e.EntityType)
            ? StringTreeEntityTypes.Node
            : e.EntityType,
        HasChildren = hasChildren
    };
}
