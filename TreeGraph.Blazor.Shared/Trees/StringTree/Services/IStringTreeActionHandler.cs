using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// StringTreeSky 的写操作接口。
///
/// 与 <see cref="IStringTreeDataSource"/> 的关系：
///   - 读操作 → IStringTreeDataSource
///   - 写操作 → 本接口
///
/// 默认实现 <see cref="NoopStringTreeActionHandler"/> 抛 NotSupportedException，
/// 纯展示场景无需实现；管理场景请注册自定义 Handler。
/// </summary>
public interface IStringTreeActionHandler
{
    /// <summary>
    /// 创建子节点。
    /// 入参：父节点 ID + 新节点元数据（Id 可为空，由实现方生成）。
    /// 返回：创建后的节点（含新 Id）；失败返回 null。
    /// </summary>
    Task<StringNodeMeta?> CreateChildAsync(
        string parentId, StringNodeMeta newChild, CancellationToken ct = default);

    /// <summary>更新节点信息（不含 ParentId，移动请用 <see cref="MoveNodeAsync"/>）。</summary>
    Task<StringNodeMeta?> UpdateNodeAsync(
        StringNodeMeta node, CancellationToken ct = default);

    /// <summary>删除节点及其全部后代。返回是否删除成功。</summary>
    Task<bool> DeleteNodeAsync(
        string id, CancellationToken ct = default);

    /// <summary>移动节点到新父（null = 移至根）。返回是否成功。</summary>
    Task<bool> MoveNodeAsync(
        string id, string? newParentId, CancellationToken ct = default);

    /// <summary>
    /// 重排子节点顺序。
    ///
    /// ★ 语义：orderedChildIds 顺序即最终顺序，按列表位置重编号。
    ///   不要按每个节点的 SortOrder 排序（会撤销拖拽结果）。
    /// </summary>
    Task<bool> SortChildrenAsync(
        string parentId, IReadOnlyList<string> orderedChildIds,
        CancellationToken ct = default);
}
