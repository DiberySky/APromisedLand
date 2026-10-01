using APromisedLand.Shared.TreeGraph.Models;

namespace TreeGraphApi.Services;

/// <summary>
/// 服务层契约,对外暴露 DTO 而非 Entity(优化 #5:DTO 隔离)。
/// </summary>
public interface ITreeService
{
    Task<IReadOnlyCollection<TreeNodeDto>> GetRootsAsync(TreeQueryOptions opts, CancellationToken ct = default);
    Task<IReadOnlyCollection<TreeNodeDto>> GetChildrenAsync(string parentId, CancellationToken ct = default);
    Task<IReadOnlyCollection<TreeNodeDto>> GetSubtreeAsync(string rootId, CancellationToken ct = default);
    Task<TreeNodeDto> CreateAsync(TreeNodeDto dto, CancellationToken ct = default);
    Task<TreeNodeDto> UpdateAsync(string id, TreeNodeDto dto, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task MoveAsync(string nodeId, string newParentId, CancellationToken ct = default);
}
