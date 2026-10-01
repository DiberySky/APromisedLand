using TreeGraphApi.Entities;

namespace TreeGraphApi.Repositories;

public interface ITreeRepository
{
    Task<IReadOnlyCollection<TreeNodeEntity>> GetRootsAsync(int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyCollection<TreeNodeEntity>> GetChildrenAsync(string parentId, CancellationToken ct = default);
    Task<TreeNodeEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyCollection<TreeNodeEntity>> GetSubtreeAsync(string rootId, CancellationToken ct = default);
    Task<IReadOnlyCollection<TreeNodeEntity>> GetAncestorsAsync(string nodeId, CancellationToken ct = default);
    Task<TreeNodeEntity> AddAsync(TreeNodeEntity node, CancellationToken ct = default);
    Task<TreeNodeEntity> UpdateAsync(TreeNodeEntity node, CancellationToken ct = default);
    Task DeleteSubtreeAsync(string nodeId, CancellationToken ct = default);
    Task MoveAsync(string nodeId, string newParentId, CancellationToken ct = default);
}
