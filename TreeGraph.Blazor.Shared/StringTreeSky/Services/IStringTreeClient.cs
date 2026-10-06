using TreeGraph.StringTree.Contracts;

namespace TreeGraph.Blazor.Shared.StringTreeSky.Services;

public interface IStringTreeClient
{
    Task<List<StringNodeDto>> GetRootNodesAsync(CancellationToken ct = default);
    Task<List<StringNodeDto>> GetChildrenAsync(int parentId, CancellationToken ct = default);
    Task<StringNodeDto?> GetNodeAsync(int id, CancellationToken ct = default);
    Task<List<StringNodeDto>> GetAncestorPathAsync(int id, CancellationToken ct = default);

    Task<StringNodeDto> CreateNodeAsync(StringNodeDto dto, CancellationToken ct = default);
    Task<StringNodeDto?> UpdateNodeAsync(StringNodeDto dto, CancellationToken ct = default);
    Task<bool> DeleteNodeAsync(int id, CancellationToken ct = default);
    Task<bool> MoveNodeAsync(int id, int? newParentId, int newSortOrder, CancellationToken ct = default);
    Task<bool> SortChildrenAsync(int parentId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);
}
