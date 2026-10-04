using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// 默认实现：所有写操作抛 NotSupportedException。
///
/// 用途：
///   - 纯展示场景无需注册 Handler 即可使用 StringTreeSky（只读）
///   - 管理场景请注册自定义 Handler 覆盖此默认
/// </summary>
public class NoopStringTreeActionHandler : IStringTreeActionHandler
{
    public Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default)
        => throw NotSupported(nameof(CreateChildAsync));

    public Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default)
        => throw NotSupported(nameof(UpdateNodeAsync));

    public Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default)
        => throw NotSupported(nameof(DeleteNodeAsync));

    public Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default)
        => throw NotSupported(nameof(MoveNodeAsync));

    public Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default)
        => throw NotSupported(nameof(SortChildrenAsync));

    private static NotSupportedException NotSupported(string method)
        => new($"StringTreeSky 的 {method} 未注册 ActionHandler。" +
               $"请在 DI 中注册 IStringTreeActionHandler 实现。");
}
