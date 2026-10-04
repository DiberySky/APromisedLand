using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;

namespace TreeGraph.Blazor.Services.DemoTree;

public class InMemoryStringTreeDataSource(InMemoryStringTreeStore store)
    : IStringTreeDataSource
{
    public Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(CancellationToken ct = default)
        => Task.FromResult(store.GetRoots());

    public Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
        string parentId, CancellationToken ct = default)
        => Task.FromResult(store.GetChildren(parentId));

    public Task<StringNodeMeta?> GetByIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(store.GetById(id));

    public Task<List<string>?> GetAncestorPathAsync(
        string id, CancellationToken ct = default)
        => Task.FromResult(store.GetAncestorPath(id));
}
