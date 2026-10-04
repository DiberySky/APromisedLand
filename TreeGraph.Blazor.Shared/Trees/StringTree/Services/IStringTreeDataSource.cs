using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// T=string 树的数据源接口（只读）。
///
/// 与 <see cref="Trees.Services.ITreeClientService{TTree}"/> 的区别：
///   - 泛型被固定为 string（节点 ID）
///   - 元数据通过 <see cref="StringNodeMeta"/> 传递
/// </summary>
public interface IStringTreeDataSource
{
    /// <summary>获取根节点列表（按 SortOrder 排序）。</summary>
    Task<IReadOnlyList<StringNodeMeta>> GetRootsAsync(
        CancellationToken ct = default);

    /// <summary>获取指定父节点的子节点（懒加载）。</summary>
    Task<IReadOnlyList<StringNodeMeta>> GetChildrenAsync(
        string parentId, CancellationToken ct = default);

    /// <summary>按 ID 获取单个节点（刷新 / 详情用）。不存在返回 null。</summary>
    Task<StringNodeMeta?> GetByIdAsync(
        string id, CancellationToken ct = default);

    // ★ 批次 3 新增：返回从根到目标的 ID 路径（含目标自身，根在前）
    //    语义对齐 Trees.Services.ITreeClientService<TTree>.GetAncestorPathFromApiAsync
    Task<List<string>?> GetAncestorPathAsync(string id, CancellationToken ct = default);
}
