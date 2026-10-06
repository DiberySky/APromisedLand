using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Api.StringTreeSky.Services;

public interface IStringTreeService
{
    Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default);
    Task<List<StringNodeDto>> GetChildrenAsync(string parentId, CancellationToken ct = default);
    Task<StringNodeDto?> GetNodeAsync(string id, CancellationToken ct = default);
    Task<List<StringNodeDto>> GetAncestorPathAsync(string id, CancellationToken ct = default);

    Task<StringNodeDto> CreateNodeAsync(StringNodeDto dto, CancellationToken ct = default);
    Task<StringNodeDto?> UpdateNodeAsync(string id, StringNodeDto dto, CancellationToken ct = default);
    Task<bool> DeleteNodeAsync(string id, CancellationToken ct = default);
    Task<bool> MoveNodeAsync(string id, string? newParentId, int newSortOrder, CancellationToken ct = default);
    Task<bool> SortChildrenAsync(string parentId, IReadOnlyList<string> orderedIds, CancellationToken ct = default);
}
