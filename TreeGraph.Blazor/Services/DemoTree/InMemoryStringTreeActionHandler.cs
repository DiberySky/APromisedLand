using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

public class InMemoryStringTreeActionHandler(InMemoryStringTreeStore store)
    : IStringTreeActionHandler
{
    public Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default)
        => Task.FromResult(store.Create(parentId, newChild));

    public Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default)
        => Task.FromResult(store.Update(node) ? node : null);

    public Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default)
        => Task.FromResult(store.Delete(id));

    public Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default)
        => Task.FromResult(store.Move(id, newParentId));

    public Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default)
        => Task.FromResult(store.Sort(parentId, orderedChildIds));
}
